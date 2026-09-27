// <copyright file="CliCountersignTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// The tenant operator's countersignature through the CLI (ADR 0065 phase C): the digest is computed from the executor
/// the control plane serves, a manifest that names another is refused, the framed tuple is signed with the operator's
/// key, and the countersignature is recorded for the environment.
/// </summary>
[TestClass]
public sealed class CliCountersignTests
{
    private static readonly byte[] Executor = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
    private static readonly string Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Executor));
    private const string Hash = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    [TestMethod]
    public async Task The_operator_countersigns_the_served_executors_digest_for_one_version_or_for_every_available_one()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = await WritePemAsync(signer);
        try
        {
            await using FakeControlPlane server = await FakeControlPlane.StartAsync(manifestDigest: Digest);
            (int exit, string stdout, string stderr) = await RunAsync(server, "availability", "countersign", "production", "flow", "3", "--signing-key", pem);
            exit.ShouldBe(0, stderr);
            stdout.ShouldContain("Countersigned version 3 of 'flow' for 'production'");
            stdout.ShouldContain(Digest);
            server.Recorded.Count.ShouldBe(1);
            (string path, Stj.JsonElement body) = server.Recorded[0];
            path.ShouldBe("/environments/production/executors/flow/3");
            body.GetProperty("assemblyDigest").GetString().ShouldBe(Digest, "the digest of the served bytes, not a value copied from anywhere");
            body.GetProperty("packageHash").GetString().ShouldBe(Hash);
            byte[] signature = body.GetProperty("signature").GetBytesFromBase64();
            ExecutorCountersignature.Verify("production", "flow", 3, Hash, Digest, signer.ExportSubjectPublicKeyInfo(), signature).ShouldBe(ExecutorCountersignatureResult.Verified);
            ExecutorCountersignature.Verify("staging", "flow", 3, Hash, Digest, signer.ExportSubjectPublicKeyInfo(), signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid, "the environment is inside the tuple");

            // Without a version: every version available in the environment, as the control plane lists it.
            server.Recorded.Clear();
            (exit, stdout, stderr) = await RunAsync(server, "availability", "countersign", "production", "--signing-key", pem);
            exit.ShouldBe(0, stderr);
            server.Recorded.Select(r => r.Path).ShouldBe(["/environments/production/executors/flow/3", "/environments/production/executors/nightly/1"]);
        }
        finally
        {
            File.Delete(pem);
        }
    }

    [TestMethod]
    public async Task A_manifest_naming_another_executor_or_a_reviewed_digest_that_differs_is_refused_before_anything_is_signed()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = await WritePemAsync(signer);
        try
        {
            // The manifest says one thing and the served executor is another: sign nothing, since the countersignature
            // would name an executor nobody reviewed.
            await using (FakeControlPlane lying = await FakeControlPlane.StartAsync(manifestDigest: "sha256:" + new string('f', 64)))
            {
                (int exit, _, string stderr) = await RunAsync(lying, "availability", "countersign", "production", "flow", "3", "--signing-key", pem);
                exit.ShouldBe(1);
                stderr.ShouldContain("digests to");
                lying.Recorded.ShouldBeEmpty();
            }

            // The served executor is not the one the operator reviewed.
            await using (FakeControlPlane honest = await FakeControlPlane.StartAsync(manifestDigest: Digest))
            {
                (int exit, _, string stderr) = await RunAsync(honest, "availability", "countersign", "production", "flow", "3", "--signing-key", pem, "--expect-digest", "sha256:" + new string('e', 64));
                exit.ShouldBe(1);
                stderr.ShouldContain("not the reviewed");
                honest.Recorded.ShouldBeEmpty();

                // A version with no executor.
                (exit, _, stderr) = await RunAsync(honest, "availability", "countersign", "production", "flow", "9", "--signing-key", pem);
                exit.ShouldBe(1);
                honest.Recorded.ShouldBeEmpty();
            }
        }
        finally
        {
            File.Delete(pem);
        }
    }

    [TestMethod]
    public async Task A_development_api_key_rides_as_a_header()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = await WritePemAsync(signer);
        try
        {
            await using FakeControlPlane server = await FakeControlPlane.StartAsync(manifestDigest: Digest);
            (int exit, _, string stderr) = await RunAsync(server, "availability", "countersign", "production", "flow", "3", "--signing-key", pem, "--api-key", "demo-admin-key");
            exit.ShouldBe(0, stderr);
            server.ApiKeys.ShouldAllBe(k => k == "demo-admin-key");
            server.ApiKeys.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            File.Delete(pem);
        }
    }

    private static async Task<string> WritePemAsync(ECDsa signer)
    {
        string path = Path.Combine(Path.GetTempPath(), "arazzo-cli-countersign-" + Guid.NewGuid().ToString("N") + ".pem");
        await File.WriteAllTextAsync(path, signer.ExportPkcs8PrivateKeyPem());
        return path;
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

    // The endpoints the operator touches: the version's executor and manifest, the environment's availability, and the
    // countersign endpoint, which records what was put.
    private sealed class FakeControlPlane(WebApplication app) : IAsyncDisposable
    {
        public string Url { get; private set; } = string.Empty;

        public List<(string Path, Stj.JsonElement Body)> Recorded { get; } = [];

        public List<string> ApiKeys { get; } = [];

        public static async Task<FakeControlPlane> StartAsync(string manifestDigest)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            var server = new FakeControlPlane(app);
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Api-Key", out Microsoft.Extensions.Primitives.StringValues key))
                {
                    server.ApiKeys.Add(key.ToString());
                }

                await next(context);
            });
            app.MapGet("/catalog/{baseWorkflowId}/versions/{versionNumber}/executor", (string baseWorkflowId, int versionNumber) =>
                versionNumber == 9
                    ? Results.Content("""{"type":"https://corvus-oss.org/arazzo/control-plane/problems/version-not-found","title":"Version not found","status":404,"detail":"No executor."}""", "application/problem+json", statusCode: 404)
                    : Results.Bytes(Executor, "application/octet-stream"));
            app.MapGet("/catalog/{baseWorkflowId}/versions/{versionNumber}/executorManifest", (string baseWorkflowId, int versionNumber) =>
                Results.Content($$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"{{Hash}}","assemblyDigest":"{{manifestDigest}}","entryType":"X","workflowId":"{{baseWorkflowId}}-v{{versionNumber}}"}""", "application/json"));
            app.MapGet("/environments/{environment}/availability", () =>
                Results.Content("""{"availability":[{"baseWorkflowId":"flow","versionNumber":3,"environment":"production","createdBy":"ops","createdAt":"2026-01-01T00:00:00Z","etag":"e1"},{"baseWorkflowId":"nightly","versionNumber":1,"environment":"production","createdBy":"ops","createdAt":"2026-01-01T00:00:00Z","etag":"e2"}]}""", "application/json"));
            app.MapPut("/environments/{environment}/executors/{baseWorkflowId}/{versionNumber}", async (HttpRequest request, string environment, string baseWorkflowId, int versionNumber) =>
            {
                using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer);
                using Stj.JsonDocument body = Stj.JsonDocument.Parse(buffer.ToArray());
                server.Recorded.Add((request.Path.Value!, body.RootElement.Clone()));
                string digest = body.RootElement.GetProperty("assemblyDigest").GetString()!;
                return Results.Content($$"""{"baseWorkflowId":"{{baseWorkflowId}}","versionNumber":{{versionNumber}},"packageHash":"{{Hash}}","assemblyDigest":"{{digest}}","signature":"{{body.RootElement.GetProperty("signature").GetString()}}","signedBy":"operator","signedAt":"2026-09-27T00:00:00Z"}""", "application/json");
            });
            await app.StartAsync();
            server.Url = app.Urls.First();
            return server;
        }

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }
}