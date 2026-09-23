// <copyright file="MicroGuestDeployerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy.Tests;

/// <summary>
/// Proves the micro-guest deployer drives the sidecar admin contract (ADR 0063): it stages the initrd CPIO, evolves
/// the sandbox with the memory size, the egress allowlist (checkpoint surface plus source hosts, nothing else), and
/// the environment pairs, and the attestation with its signature for the sidecar to verify the image against, returns
/// the sidecar's invoke URL as the function URL, and reports a sidecar failure as a failed deploy rather than throwing.
/// A fake HTTP handler stands in for the sidecar; no sockets are involved.
/// </summary>
[TestClass]
public sealed class MicroGuestDeployerTests
{
    private static readonly byte[] Attestation = "{\"engineVersion\":\"5.0.0\",\"formatVersion\":1,\"nativeDigest\":\"sha256:abc\",\"packageHash\":\"sha256:pkg\",\"rid\":\"linux-musl-x64\"}"u8.ToArray();

    private static readonly byte[] Signature = "{\"algorithm\":\"ecdsa-p256-sha256\",\"keyId\":\"release-2026\",\"value\":\"c2ln\"}"u8.ToArray();

    private static readonly ServerlessDeployRequest Request = new(
        "pets/adopt",
        3,
        "prod",
        "linux-musl-x64",
        new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 9, 9 },
        Attestation,
        Signature);

    [TestMethod]
    public async Task Stages_the_initrd_then_evolves_and_returns_the_invoke_url()
    {
        var sidecar = new FakeSidecarHandler();
        MicroGuestDeployer deployer = Deployer(sidecar);

        ServerlessDeployResult result = await deployer.DeployAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeTrue(result.Log);
        result.FunctionUrl.ShouldBe("http://127.0.0.1:9411/invoke/arazzo-mg-pets-adopt-v3-prod-linux-musl-x64");

        sidecar.Requests.Count.ShouldBe(2);
        sidecar.Authorizations.ShouldBe(["Bearer sidecar-admin-token", "Bearer sidecar-admin-token"], customMessage: "every admin call carries the sidecar's admin token (P1-10)");
        (HttpMethod initrdMethod, Uri initrdUri, byte[] initrdBody, string? initrdType) = sidecar.Requests[0];
        initrdMethod.ShouldBe(HttpMethod.Put);
        initrdUri.AbsolutePath.ShouldBe("/sandboxes/arazzo-mg-pets-adopt-v3-prod-linux-musl-x64/initrd");
        initrdType.ShouldBe("application/octet-stream");
        Encoding.ASCII.GetString(initrdBody[..6]).ShouldBe("070701", customMessage: "the staged body must be the newc CPIO");

        (HttpMethod configMethod, Uri configUri, _, string? configType) = sidecar.Requests[1];
        configMethod.ShouldBe(HttpMethod.Put);
        configUri.AbsolutePath.ShouldBe("/sandboxes/arazzo-mg-pets-adopt-v3-prod-linux-musl-x64");
        configType.ShouldBe("application/json");
    }

    [TestMethod]
    public void The_sandbox_configuration_carries_memory_allowlist_and_environment()
    {
        MicroGuestDeployer deployer = Deployer(
            new FakeSidecarHandler(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ARAZZO_SOURCE__petstore"] = "https://petstore.example.com:8443/api",
                ["ARAZZO_SOURCE__billing"] = "http://billing.internal/api",
                ["UNRELATED"] = "not-a-url",
            });

        string configuration = Encoding.UTF8.GetString(deployer.BuildSandboxConfiguration(Request));

        configuration.ShouldContain("\"memoryMib\":64");
        // The allowlist is the checkpoint surface plus each source host with its (defaulted) port, and nothing else.
        configuration.ShouldContain("\"allowedHosts\":[\"172.20.0.10:8199\",\"petstore.example.com:8443\",\"billing.internal:80\"]");
        // The environment rides verbatim; the sidecar freezes it into the snapshot's argv.
        configuration.ShouldContain("\"ARAZZO_SOURCE__petstore\":\"https://petstore.example.com:8443/api\"");

        // The guest's one checkpoint origin is the surface it is allowed egress to, stamped by the deployer itself
        // (ADR 0059 decision 4), so the guest refuses a checkpointUrl that points anywhere else.
        configuration.ShouldContain($"\"{ServerlessCheckpointOrigins.SettingName}\":\"http://172.20.0.10:8199\"");
        configuration.ShouldContain("\"UNRELATED\":\"not-a-url\"");
    }

    [TestMethod]
    public void The_sandbox_configuration_carries_the_attestation_bytes_and_the_signature_verbatim()
    {
        // The sidecar verifies the image for itself (P1-10): the attestation rides as base64 of its exact signed bytes,
        // so nothing re-serializes the message the signature is over, and the signature document rides as it came
        // from the package.
        string configuration = Encoding.UTF8.GetString(Deployer(new FakeSidecarHandler()).BuildSandboxConfiguration(Request));

        configuration.ShouldContain($"\"attestation\":\"{Convert.ToBase64String(Attestation)}\"");
        configuration.ShouldContain($"\"signature\":{Encoding.UTF8.GetString(Signature)}");
    }

    [TestMethod]
    public async Task A_request_without_an_attestation_is_refused_before_the_sidecar_is_called()
    {
        var sidecar = new FakeSidecarHandler();
        MicroGuestDeployer deployer = Deployer(sidecar);

        await Should.ThrowAsync<ArgumentException>(async () => await deployer.DeployAsync(Request with { AttestationUtf8 = default }, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(async () => await deployer.DeployAsync(Request with { SignatureUtf8 = default }, CancellationToken.None));

        sidecar.Requests.ShouldBeEmpty("an unattested image is never staged");
    }

    [TestMethod]
    public async Task A_rejected_initrd_stage_fails_the_deploy_without_evolving()
    {
        var sidecar = new FakeSidecarHandler
        {
            Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/initrd", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.InsufficientStorage) { Content = new StringContent("disk full") }
                : null,
        };

        ServerlessDeployResult result = await Deployer(sidecar).DeployAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("staging the initrd");
        result.Log.ShouldContain("507 disk full");
        sidecar.Requests.Count.ShouldBe(1, customMessage: "a failed stage must never evolve the sandbox");
    }

    [TestMethod]
    public async Task A_rejected_evolve_fails_the_deploy_with_the_sidecar_log()
    {
        var sidecar = new FakeSidecarHandler
        {
            Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/initrd", StringComparison.Ordinal)
                ? null
                : new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("kernel refused the image") },
        };

        ServerlessDeployResult result = await Deployer(sidecar).DeployAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("evolving the sandbox");
        result.Log.ShouldContain("kernel refused the image");
    }

    [TestMethod]
    public async Task An_evolve_response_without_an_invoke_url_fails_the_deploy()
    {
        var sidecar = new FakeSidecarHandler
        {
            Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/initrd", StringComparison.Ordinal)
                ? null
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"ok\"}") },
        };

        ServerlessDeployResult result = await Deployer(sidecar).DeployAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("no invokeUrl");
    }

    [TestMethod]
    public async Task An_unreachable_sidecar_fails_the_deploy_rather_than_throwing()
    {
        var sidecar = new FakeSidecarHandler { Respond = _ => throw new HttpRequestException("connection refused") };

        ServerlessDeployResult result = await Deployer(sidecar).DeployAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("could not be reached");
    }

    [TestMethod]
    public void The_sandbox_id_is_deterministic_sanitized_and_bounded()
    {
        MicroGuestDeployer.SandboxId(Request).ShouldBe("arazzo-mg-pets-adopt-v3-prod-linux-musl-x64");

        // An over-long tuple truncates with a deterministic suffix, staying within 64 characters without colliding.
        var longRequest = new ServerlessDeployRequest(new string('w', 80), 12, "production", "linux-musl-x64", default, AttestationUtf8: default, SignatureUtf8: default);
        string id = MicroGuestDeployer.SandboxId(longRequest);
        id.Length.ShouldBe(64);
        id.ShouldBe(MicroGuestDeployer.SandboxId(longRequest));
        id.ShouldNotBe(MicroGuestDeployer.SandboxId(longRequest with { VersionNumber = 13 }));
    }

    private static MicroGuestDeployer Deployer(FakeSidecarHandler sidecar, IReadOnlyDictionary<string, string>? environment = null)
        => new(
            new MicroGuestDeployerOptions
            {
                SidecarBaseUrl = new Uri("http://127.0.0.1:9411"),
                AdminToken = SecretRef.Parse("env://ARAZZO_SIDECAR_ADMIN_TOKEN"),
                CheckpointSurfaceUrl = new Uri("http://172.20.0.10:8199/checkpoints"),
                GuestEnvironment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
            },
            new FixedSecretResolver("sidecar-admin-token"),
            sidecar);

    private sealed class FixedSecretResolver(string value) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => ValueTask.FromResult(SecretMaterial.FromString(value));
    }

    // The stand-in sidecar: records every request and answers the happy path (204 for the staged initrd, an invokeUrl
    // for the evolve) unless a test supplies its own responder; a null from the responder falls back to the default.
    private sealed class FakeSidecarHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, byte[] Body, string? ContentType)> Requests { get; } = [];

        public List<string?> Authorizations { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage?>? Respond { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] body = request.Content is { } content ? await content.ReadAsByteArrayAsync(cancellationToken) : [];
            this.Requests.Add((request.Method, request.RequestUri!, body, request.Content?.Headers.ContentType?.MediaType));
            this.Authorizations.Add(request.Headers.Authorization?.ToString());

            if (this.Respond?.Invoke(request) is { } response)
            {
                return response;
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/initrd", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            string sandboxId = request.RequestUri.AbsolutePath[(request.RequestUri.AbsolutePath.LastIndexOf('/') + 1)..];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"invokeUrl\":\"http://127.0.0.1:9411/invoke/{sandboxId}\"}}"),
            };
        }
    }
}