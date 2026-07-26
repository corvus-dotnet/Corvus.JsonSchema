// <copyright file="NativeBuildEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The end-to-end proof of the asynchronous native-publish flow (ADR 0055): a client POSTs a build over the REST surface,
/// the hosted <see cref="NativeBuildBackgroundService"/> claims it, verifies the version's signed executor, native-AOT
/// compiles and signs the binary, persists it back into the version, and the client polls the build to Ready — after which
/// the version's package carries a verifiable native binary. The REST surface and the worker run over the same shared
/// stores, exactly as a deployment wires them.
/// </summary>
/// <remarks>
/// The fast proof uses a fake builder (the real container Native-AOT compile is proven separately by
/// <c>WorkflowAotBuildIntegrationTests</c>), so the whole loop is exercised deterministically. The opt-in container proof
/// runs the same loop with the real <see cref="ContainerWorkflowAotBuilder"/> and is skipped unless the feed and runtime
/// version are configured.
/// </remarks>
[TestClass]
public sealed class NativeBuildEndToEndTests
{
    private const string Write = "catalog:write";
    private const string Read = "catalog:read";

    // A fake ELF the fake builder returns; the loop signs, attaches, persists, and verifies it like a real binary.
    private static readonly byte[] FakeNativeBinary = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3, 4];

    [TestMethod]
    public async Task The_full_publish_loop_builds_signs_persists_and_reaches_ready()
    {
        await RunPublishLoopAsync(
            builder: new FakeAotBuilder(),
            runtimePackageVersion: "5.0.0",
            feedSources: [("local", "/tmp/feed")],
            readyTimeout: TimeSpan.FromSeconds(30),
            assertNativeBinary: native => native.ShouldBe(FakeNativeBinary));
    }

    [TestMethod]
    [TestCategory("integration")]
    public async Task The_full_publish_loop_produces_a_real_native_binary_in_the_container()
    {
        string? feed = Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED and ARAZZO_AOT_RUNTIME_VERSION, and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string image = Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
        {
            ContainerImage = image,
            ReadOnlyMounts = [(feed, "/work/local-packages")],
        });

        await RunPublishLoopAsync(
            builder: builder,
            runtimePackageVersion: runtimeVersion!,
            feedSources:
            [
                ("local", "/work/local-packages"),
                ("nuget.org", "https://api.nuget.org/v3/index.json"),
                ("dotnet-eng", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                ("dotnet-libraries", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"),
            ],
            readyTimeout: TimeSpan.FromMinutes(40),
            assertNativeBinary: native =>
            {
                native.Length.ShouldBeGreaterThan(1_000_000);
                native[..4].ShouldBe([0x7F, (byte)'E', (byte)'L', (byte)'F']);
            });
    }

    private static async Task RunPublishLoopAsync(IWorkflowAotBuilder builder, string runtimePackageVersion, IReadOnlyList<(string Name, string Value)> feedSources, TimeSpan readyTimeout, Action<byte[]> assertNativeBinary)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(key, "release-2026");
        IExecutorPackageVerifier verifier = TrustStore(("release-2026", key));

        // The catalog compiles and signs the executor at add-time (bound to the version's content hash); the build store is
        // shared between the REST surface and the worker so an enqueue and a claim see the same job.
        var runStore = new InMemoryWorkflowStateStore();
        var catalogStore = new InMemoryWorkflowCatalogStore(
            timeProvider: null,
            metadataProvider: null,
            executorProvider: new WorkflowExecutorProvider(durable: true),
            signer: signer);
        var management = new SecuredWorkflowManagement(runStore, "ops");
        var catalog = new SecuredWorkflowCatalog(catalogStore, runStore, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());
        var buildStore = new InMemoryNativeBuildJobStore();
        var buildService = new WorkflowAotBuildService(verifier, signer, builder, new AotHostAppOptions { RuntimePackageVersion = runtimePackageVersion, FeedSources = feedSources });

        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();
        appBuilder.Logging.ClearProviders();
        appBuilder.Services
            .AddAuthentication(ScopeTenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantAuthHandler>(ScopeTenantAuthHandler.SchemeName, _ => { });
        appBuilder.Services.AddArazzoControlPlaneAuthorization();
        appBuilder.Services.AddHttpContextAccessor();

        // Wire the hosted build worker over the same stores the REST surface uses.
        appBuilder.Services.AddSingleton<INativeBuildJobStore>(buildStore);
        appBuilder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
        appBuilder.Services.AddSingleton(buildService);
        appBuilder.Services.AddNativeAotBuildWorker(new NativeBuildWorkerOptions { WorkerId = "e2e-worker", PollInterval = TimeSpan.FromMilliseconds(50) });

        await using WebApplication app = appBuilder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), nativeBuildJobStore: buildStore);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        // Seed a version — the store compiles and signs its executor.
        SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme")]);
        await catalog.AddAsync(CatalogPackage.Build(Workflow(), []), new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);

        // Enqueue a build over REST → 202.
        (await Send(client, HttpMethod.Post, "/catalog/adopt/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // The hosted worker drives the build; poll the target until it is terminal.
        (string status, string? reason) = await PollUntilTerminalAsync(client, "/catalog/adopt/versions/1/nativeBuilds/production/linux-x64", readyTimeout);
        status.ShouldBe("Ready", $"the build did not reach Ready (reason: {reason ?? "none"})");

        // The version's package now carries the native binary, and its signed attestation verifies against the trust store.
        ReadOnlyMemory<byte>? package = await catalogStore.GetPackageAsync("adopt", 1, default);
        package.ShouldNotBeNull();
        WorkflowPackage.TryReadNativeArtifact(package!.Value, "linux-x64", out ReadOnlyMemory<byte> native).ShouldBeTrue();
        WorkflowAotBuildService.VerifyNativeArtifact(package.Value, "linux-x64", verifier);
        assertNativeBinary(native.ToArray());
    }

    private static async Task<(string Status, string? Reason)> PollUntilTerminalAsync(HttpClient client, string path, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            HttpResponseMessage response = await Send(client, HttpMethod.Get, path, null, Read);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                string status = doc.RootElement.GetProperty("status").GetString()!;
                if (status is "Ready" or "Failed")
                {
                    string? reason = doc.RootElement.TryGetProperty("failureReason", out Stj.JsonElement r) ? r.GetString() : null;
                    return (status, reason);
                }
            }

            await Task.Delay(50);
        }

        return ("TimedOut", null);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? body, string scope)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        request.Headers.Add(ScopeTenantAuthHandler.ScopeHeader, scope);
        request.Headers.Add(ScopeTenantAuthHandler.TenantHeader, "acme");
        return await client.SendAsync(request);
    }

    private static TrustStoreExecutorPackageVerifier TrustStore(params (string KeyId, ECDsa SigningKey)[] keys)
    {
        var trusted = new Dictionary<string, AsymmetricAlgorithm>(StringComparer.Ordinal);
        foreach ((string keyId, ECDsa signingKey) in keys)
        {
            var publicKey = ECDsa.Create();
            publicKey.ImportParameters(signingKey.ExportParameters(includePrivateParameters: false));
            trusted[keyId] = publicKey;
        }

        return new TrustStoreExecutorPackageVerifier(trusted);
    }

    // A trivial source-less durable workflow: enough for the executor provider to compile a real, signable executor.
    private static byte[] Workflow()
        => Encoding.UTF8.GetBytes("""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Adopt", "description": "A flow." },
          "sourceDescriptions": [],
          "workflows": [ { "workflowId": "adopt", "steps": [] } ]
        }
        """);

    private sealed class FakeAotBuilder : IWorkflowAotBuilder
    {
        public ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
            => new(AotBuildResult.Success(FakeNativeBinary, "fake ilc ok"));
    }

    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class ScopeTenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesTenant";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues scopes))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", scopes.ToString()));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}