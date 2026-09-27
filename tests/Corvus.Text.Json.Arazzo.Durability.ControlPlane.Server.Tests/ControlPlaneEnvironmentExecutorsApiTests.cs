// <copyright file="ControlPlaneEnvironmentExecutorsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
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

[TestClass]
public sealed class ControlPlaneEnvironmentExecutorsApiTests
{
    private static readonly string Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(FakeExecutorProvider.AssemblyBytes));

    [TestMethod]
    public async Task An_administrator_countersigns_a_versions_executor_reads_it_back_replaces_it_and_withdraws_it()
    {
        // ADR 0065 phase C: the tenant operator countersigns the executor of a version for an environment; the control
        // plane records it on the environment beside the key generations, unverified, and advertises it.
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");
        string hash = await HashAsync(host, "flow", 1);
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        using Stj.JsonDocument recorded = await ReadJsonAsync(
            await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(tenant, "production", "flow", 1, hash, Digest), "acme"), HttpStatusCode.OK);
        recorded.RootElement.GetProperty("baseWorkflowId").GetString().ShouldBe("flow");
        recorded.RootElement.GetProperty("versionNumber").GetInt32().ShouldBe(1);
        recorded.RootElement.GetProperty("assemblyDigest").GetString().ShouldBe(Digest);
        recorded.RootElement.GetProperty("packageHash").GetString().ShouldBe(hash);
        recorded.RootElement.GetProperty("signedBy").GetString().ShouldBe("acme");
        string first = recorded.RootElement.GetProperty("signature").GetString()!;

        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/executors", "acme"), HttpStatusCode.OK))
        {
            listed.RootElement.GetProperty("countersignatures").GetArrayLength().ShouldBe(1);
            listed.RootElement.GetProperty("countersignatures")[0].GetProperty("signature").GetString().ShouldBe(first);
        }

        // Re-signing replaces the record for the version rather than adding a second.
        using ECDsa successor = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(successor, "production", "flow", 1, hash, Digest), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/executors", "acme"), HttpStatusCode.OK))
        {
            listed.RootElement.GetProperty("countersignatures").GetArrayLength().ShouldBe(1);
            listed.RootElement.GetProperty("countersignatures")[0].GetProperty("signature").GetString().ShouldNotBe(first);
        }

        // Withdrawn, and withdrawing again is the same 204.
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/executors/flow/1", "acme")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/executors/flow/1", "acme")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/executors", "acme"), HttpStatusCode.OK))
        {
            listed.RootElement.GetProperty("countersignatures").GetArrayLength().ShouldBe(0);
        }
    }

    [TestMethod]
    public async Task A_countersignature_naming_another_executor_or_malformed_or_for_an_absent_version_is_refused()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");
        string hash = await HashAsync(host, "flow", 1);
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // The signed digest and hash have to be the version's current executor manifest's: an operator who signed a
        // stale or wrong manifest is told now, rather than by a runner refusing every run.
        string otherDigest = "sha256:" + new string('9', 64);
        using (Stj.JsonDocument problem = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(tenant, "production", "flow", 1, hash, otherDigest), "acme"), HttpStatusCode.Conflict))
        {
            problem.RootElement.GetProperty("type").GetString().ShouldEndWith("executor-digest-mismatch");
        }

        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(tenant, "production", "flow", 1, "sha256:" + new string('8', 64), Digest), "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The shape: base64, 64 bytes.
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", $$"""{"packageHash":"{{hash}}","assemblyDigest":"{{Digest}}","signature":"not base64!"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", $$"""{"packageHash":"{{hash}}","assemblyDigest":"{{Digest}}","signature":"{{Convert.ToBase64String(new byte[10])}}"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A version that does not exist, and an environment that does not.
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/99", Countersignature(tenant, "production", "flow", 99, hash, Digest), "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/absent/executors/flow/1", Countersignature(tenant, "absent", "flow", 1, hash, Digest), "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Nothing was recorded by any of them.
        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/executors", "acme"), HttpStatusCode.OK);
        listed.RootElement.GetProperty("countersignatures").GetArrayLength().ShouldBe(0);
    }

    [TestMethod]
    public async Task Countersigning_as_a_non_administrator_is_forbidden_and_the_record_rides_through_a_key_registration()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");
        string hash = await HashAsync(host, "flow", 1);
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // 'contoso' can see the environment (the harness policy grants full read reach) but does not administer it.
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(tenant, "production", "flow", 1, hash, Digest), "contoso")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/executors/flow/1", "contoso")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production/executors/flow/1", Countersignature(tenant, "production", "flow", 1, hash, Digest), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A key registration, which rewrites the environment record through its own draft, carries the
        // countersignature forward; so does an ordinary update of the environment.
        using ECDsa sealKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(sealKey, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"displayName":"Production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/executors", "acme"), HttpStatusCode.OK);
        listed.RootElement.GetProperty("countersignatures").GetArrayLength().ShouldBe(1);
    }

    private static string Countersignature(ECDsa signer, string environment, string baseWorkflowId, int versionNumber, string packageHash, string assemblyDigest)
    {
        byte[] signature = ExecutorCountersignature.Sign(signer, environment, baseWorkflowId, versionNumber, packageHash, assemblyDigest);
        return $$"""{"packageHash":"{{packageHash}}","assemblyDigest":"{{assemblyDigest}}","signature":"{{Convert.ToBase64String(signature)}}"}""";
    }

    private static string Registration(ECDsa key, string environment, string keyId)
    {
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        byte[] tuple = new byte[EnvironmentKeyPossession.MaxTupleLength(environment, keyId, spki.Length)];
        int written = EnvironmentKeyPossession.WriteSignedTuple(tuple, environment, keyId, spki, notBefore);
        byte[] signature = key.SignData(tuple.AsSpan(0, written), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $$"""{"keyId":"{{keyId}}","sealPublicKey":"{{Convert.ToBase64String(spki)}}","algorithm":"ES256","notBefore":"{{notBefore:O}}","signature":"{{Convert.ToBase64String(signature)}}"}""";
    }

    private static async Task<string> HashAsync(Scoped host, string baseWorkflowId, int versionNumber)
    {
        using Stj.JsonDocument version = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/catalog/{baseWorkflowId}/versions/{versionNumber}", "acme"), HttpStatusCode.OK);
        return version.RootElement.GetProperty("hash").GetString()!;
    }

    private static async Task CreateEnvironmentAsync(Scoped host, string name, string tenant)
        => (await host.SendJsonAsync(HttpMethod.Post, "/environments", $$"""{"name":"{{name}}"}""", tenant))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        return Stj.JsonDocument.Parse(body);
    }

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(executorProvider: new FakeExecutorProvider()), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());
        await catalog.AddAsync(
            WorkflowPackage.Pack(Encoding.UTF8.GetBytes("""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"flow","steps":[]}]}"""), []),
            new CatalogOwner("Team", "team@example.com"),
            default,
            default,
            default,
            default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeTenantSubAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantSubAuthHandler>(ScopeTenantSubAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddArazzoAuthenticationTelemetry();
        builder.Services.AddArazzoSecurityHeaders();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), auditor: GovernanceAuditor.CreateInMemory());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    // The executor the catalog bakes: fixed bytes and a manifest naming them, so the digest the operator countersigns
    // is one the test can compute.
    private sealed class FakeExecutorProvider : IWorkflowExecutorProvider
    {
        public static readonly byte[] AssemblyBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03];

        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
            => new(AssemblyBytes, Encoding.UTF8.GetBytes($$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"{{packageHash}}","assemblyDigest":"{{Digest}}","entryType":"Flow.Executor","workflowId":"flow-v1"}"""));
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

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), tenant);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string tenant)
        {
            using (request)
            {
                request.Headers.Add(ScopeTenantSubAuthHandler.ScopeHeader, "authenticated");
                request.Headers.Add(ScopeTenantSubAuthHandler.TenantHeader, tenant);
                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeTenantSubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesTenantSub";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey(ScopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write catalog:read"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}