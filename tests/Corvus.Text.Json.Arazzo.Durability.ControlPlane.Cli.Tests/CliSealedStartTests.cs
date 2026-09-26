// <copyright file="CliSealedStartTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// The CLI as the initiator of a sealed start (ADR 0065 decision 9): it seals to the seal key the control plane
/// publishes only when that key's fingerprint is the one pinned on the command line, signs as the initiator, and
/// posts a seal the runner can open and verify; a key with any other fingerprint sends nothing.
/// </summary>
[TestClass]
public sealed class CliSealedStartTests
{
    [TestMethod]
    public async Task A_sealed_start_seals_to_the_pinned_key_signs_as_the_initiator_and_posts_the_seal()
    {
        using var sealKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] sealSpki = sealKey.ExportSubjectPublicKeyInfo();
        string initiatorKeyPath = Path.Combine(Path.GetTempPath(), "arazzo-initiator-" + Guid.NewGuid().ToString("N") + ".pem");
        await File.WriteAllTextAsync(initiatorKeyPath, initiator.ExportPkcs8PrivateKeyPem());
        try
        {
            await using FakeControlPlane server = await FakeControlPlane.StartAsync("production", "k2", sealSpki);

            (int exit, string stdout, string stderr) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--inputs", """{"email":"ada@example.com"}""", "--sealed", "--initiator-key", initiatorKeyPath, "--seal-key-fingerprint", RunStartInitiator.SealKeyFingerprint(sealSpki), "--run-id", "0123456789abcdef0123456789abcdef");

            exit.ShouldBe(0, stderr + stdout);
            stdout.ShouldContain("0123456789abcdef0123456789abcdef");
            server.Posted.ShouldNotBeNull("the seal was posted to the sealed start endpoint");
            server.PostedPath.ShouldBe("/catalog/onboard/versions/2/runs/sealed");
            using ParsedJsonDocument<SealedRunStart> posted = ParsedJsonDocument<SealedRunStart>.Parse(server.Posted!);
            ((string)posted.RootElement.RunId).ShouldBe("0123456789abcdef0123456789abcdef");
            SealedInputs sealedInputs = posted.RootElement.ToSealedInputs();
            sealedInputs.KeyId.ShouldBe("k2");

            // What the runner does at first claim: the binding for this run, the initiator's signature, the seal.
            byte[] binding = new byte[SealedStartSignature.BindingLength("production", "onboard", "k2", "0123456789abcdef0123456789abcdef")];
            SealedStartSignature.WriteBinding("production", "onboard", 2, "k2", "0123456789abcdef0123456789abcdef", binding);
            SealedStartSignature.Verify(initiator.ExportSubjectPublicKeyInfo(), binding, sealedInputs.Enc.Span, sealedInputs.Ciphertext.Span, sealedInputs.Signature.Span).ShouldBeTrue();
            byte[] opened = new byte[sealedInputs.Ciphertext.Length - InputSeal.TagLength];
            InputSeal.Open(sealKey.ExportPkcs8PrivateKey(), sealedInputs.Enc.Span, SealedStartSignature.SealInfo, binding, sealedInputs.Ciphertext.Span, opened);
            Encoding.UTF8.GetString(opened).ShouldBe("""{"email":"ada@example.com"}""");
            Encoding.Latin1.GetString(server.Posted!).Contains("ada@example.com", StringComparison.Ordinal).ShouldBeFalse("the inputs never leave the initiator in the clear");
        }
        finally
        {
            File.Delete(initiatorKeyPath);
        }
    }

    [TestMethod]
    public async Task A_pinned_initiator_follows_a_rotation_along_signed_links_and_never_an_unsigned_one()
    {
        // ADR 0065 decision 12: the operator pinned k1's fingerprint. Once k2 is registered with k1's signature over
        // the rotation, the initiator seals to k2 on its own; a k2 the outgoing key did not hand over to is refused,
        // and with k1 retired that leaves nothing to seal to.
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] firstSpki = first.ExportSubjectPublicKeyInfo();
        byte[] secondSpki = second.ExportSubjectPublicKeyInfo();
        string pinned = RunStartInitiator.SealKeyFingerprint(firstSpki);
        string link = Convert.ToBase64String(EnvironmentKeyRotation.Sign(first, "production", "k1", "k2", secondSpki));
        string strangerLink = Convert.ToBase64String(EnvironmentKeyRotation.Sign(stranger, "production", "k1", "k2", secondSpki));
        string initiatorKeyPath = Path.Combine(Path.GetTempPath(), "arazzo-initiator-" + Guid.NewGuid().ToString("N") + ".pem");
        await File.WriteAllTextAsync(initiatorKeyPath, initiator.ExportPkcs8PrivateKeyPem());
        try
        {
            string chained = $$"""{ "keys": [ { "keyId": "k1", "sealPublicKey": "{{Convert.ToBase64String(firstSpki)}}", "algorithm": "ES256", "state": "Retired", "registeredBy": "ops", "registeredAt": "2026-01-01T00:00:00Z" }, { "keyId": "k2", "sealPublicKey": "{{Convert.ToBase64String(secondSpki)}}", "algorithm": "ES256", "state": "Active", "registeredBy": "ops", "registeredAt": "2026-02-01T00:00:00Z", "predecessorKeyId": "k1", "rotationSignature": "{{link}}" } ] }""";
            await using (FakeControlPlane server = await FakeControlPlane.StartAsync("production", chained))
            {
                (int exit, string stdout, string stderr) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--inputs", """{"email":"ada@example.com"}""", "--sealed", "--initiator-key", initiatorKeyPath, "--seal-key-fingerprint", pinned, "--run-id", "0123456789abcdef0123456789abcdef");
                exit.ShouldBe(0, stderr + stdout);
                using ParsedJsonDocument<SealedRunStart> posted = ParsedJsonDocument<SealedRunStart>.Parse(server.Posted!);
                posted.RootElement.ToSealedInputs().KeyId.ShouldBe("k2", "sealed to the successor the pinned key handed over to");
                stderr.ShouldContain("k2");
            }

            string forged = chained.Replace(link, strangerLink, StringComparison.Ordinal);
            await using (FakeControlPlane server = await FakeControlPlane.StartAsync("production", forged))
            {
                (int exit, string stdout, string stderr) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--inputs", """{"email":"ada@example.com"}""", "--sealed", "--initiator-key", initiatorKeyPath, "--seal-key-fingerprint", pinned);
                exit.ShouldBe(1, stderr + stdout);
                server.Posted.ShouldBeNull("nothing sealed to a successor the outgoing key did not sign for");
                stderr.ShouldContain("rotation");
            }
        }
        finally
        {
            File.Delete(initiatorKeyPath);
        }
    }

    [TestMethod]
    public async Task A_seal_key_whose_fingerprint_is_not_the_pinned_one_seals_nothing_and_sends_nothing()
    {
        using var sealKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string initiatorKeyPath = Path.Combine(Path.GetTempPath(), "arazzo-initiator-" + Guid.NewGuid().ToString("N") + ".pem");
        await File.WriteAllTextAsync(initiatorKeyPath, initiator.ExportPkcs8PrivateKeyPem());
        try
        {
            // The control plane publishes a key of its own in place of the tenant's: the pin catches it.
            await using FakeControlPlane server = await FakeControlPlane.StartAsync("production", "k2", sealKey.ExportSubjectPublicKeyInfo());
            (int exit, _, string stderr) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--inputs", "{}", "--sealed", "--initiator-key", initiatorKeyPath, "--seal-key-fingerprint", "bm90IHRoZSBrZXkgdGhlIG9wZXJhdG9yIHBpbm5lZA==");
            exit.ShouldBe(1);
            stderr.ShouldContain("Nothing was sealed or sent");
            server.Posted.ShouldBeNull();

            // A half-configured sealed start is refused at validation, before any request is made. (The validation
            // message is rendered by the command framework on the console it captured, not on the swapped writers.)
            (int halfExit, _, _) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--sealed", "--initiator-key", initiatorKeyPath);
            halfExit.ShouldNotBe(0);
            server.Posted.ShouldBeNull();
        }
        finally
        {
            File.Delete(initiatorKeyPath);
        }
    }

    [TestMethod]
    public async Task A_plain_start_posts_the_inputs_for_the_control_plane_to_validate()
    {
        await using FakeControlPlane server = await FakeControlPlane.StartAsync("production", "k2", ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo());
        (int exit, string stdout, string stderr) = await RunAsync(server, "start", "onboard", "2", "--environment", "production", "--inputs", """{"email":"ada@example.com"}""", "--idempotency-key", "order-42");
        server.Error.ShouldBeNull();
        exit.ShouldBe(0, stderr + stdout);
        stdout.ShouldContain("runId");
        server.PostedPath.ShouldBe("/catalog/onboard/versions/2/runs");
        Encoding.UTF8.GetString(server.Posted!).ShouldContain("ada@example.com");
        server.IdempotencyKey.ShouldBe("order-42");
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(FakeControlPlane server, params string[] args)
    {
        string[] fullArgs = [.. args, "--server", server.Url, "--token", "t"];
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            int exit = await CliApp.Create().RunAsync(fullArgs);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    // The two endpoints the initiator touches, and nothing else: the published seal keys, and the start endpoints,
    // which record what was posted and accept it.
    private sealed class FakeControlPlane(WebApplication app) : IAsyncDisposable
    {
        public string Url { get; private set; } = string.Empty;

        public byte[]? Posted { get; private set; }

        public string? PostedPath { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public string? Error { get; private set; }

        public static Task<FakeControlPlane> StartAsync(string environment, string keyId, byte[] sealSpki)
            => StartAsync(environment, $$"""{ "keys": [ { "keyId": "{{keyId}}", "sealPublicKey": "{{Convert.ToBase64String(sealSpki)}}", "algorithm": "ES256", "state": "Active", "registeredBy": "ops", "registeredAt": "2026-01-01T00:00:00Z" } ] }""");

        public static async Task<FakeControlPlane> StartAsync(string environment, string keys)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            var server = new FakeControlPlane(app);
            app.MapGet($"/environments/{environment}/keys", () => Results.Content(keys, "application/json"));
            app.MapPost("/catalog/{baseWorkflowId}/versions/{versionNumber}/runs/sealed", async (HttpRequest request) =>
            {
                try
                {
                    server.Posted = await ReadAsync(request);
                    server.PostedPath = request.Path.Value;
                    using ParsedJsonDocument<SealedRunStart> body = ParsedJsonDocument<SealedRunStart>.Parse(server.Posted);
                    return Results.Content($$"""{ "runId": "{{(string)body.RootElement.RunId}}", "workflowId": "onboard-v2", "status": "Pending" }""", "application/json", statusCode: 202);
                }
                catch (Exception ex)
                {
                    server.Error = ex.ToString();
                    throw;
                }
            });
            app.MapPost("/catalog/{baseWorkflowId}/versions/{versionNumber}/runs", async (HttpRequest request) =>
            {
                try
                {
                    server.Posted = await ReadAsync(request);
                    server.PostedPath = request.Path.Value;
                    server.IdempotencyKey = request.Headers["Idempotency-Key"].ToString();
                    return Results.Content("""{ "runId": "00000000000000000000000000000001", "workflowId": "onboard-v2", "status": "Pending" }""", "application/json", statusCode: 202);
                }
                catch (Exception ex)
                {
                    server.Error = ex.ToString();
                    throw;
                }
            });
            await app.StartAsync();
            server.Url = app.Urls.First();
            return server;
        }

        public async ValueTask DisposeAsync() => await app.DisposeAsync();

        private static async Task<byte[]> ReadAsync(HttpRequest request)
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }
}