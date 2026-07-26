// <copyright file="ControlPlaneNativeBuildsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
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

/// <summary>
/// Tests the control-plane native-builds API (ADR 0055): enqueuing and polling the asynchronous Native-AOT builds of a
/// workflow version's serverless binary per (environment, runtime target) over
/// <c>/catalog/{id}/versions/{n}/nativeBuilds…</c>. Enqueuing is idempotent per target; every operation is gated by the
/// <c>catalog:read</c>/<c>catalog:write</c> scopes and reach-gated to the version. No build worker runs here, so an
/// enqueued job stays Queued — the worker is tested separately; this exercises the REST vertical.
/// </summary>
[TestClass]
public sealed class ControlPlaneNativeBuildsApiTests
{
    private const string Write = "catalog:write";
    private const string Read = "catalog:read";

    [TestMethod]
    public async Task A_build_is_enqueued_polled_listed_and_counted()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // Enqueue → 202 Accepted, carrying the queued job.
        HttpResponseMessage enqueued = await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64","buildLabel":"nightly"}""", Write, "acme");
        enqueued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using (Stj.JsonDocument job = await ReadJsonAsync(enqueued))
        {
            job.RootElement.GetProperty("status").GetString().ShouldBe("Queued");
            job.RootElement.GetProperty("baseWorkflowId").GetString().ShouldBe("checkout");
            job.RootElement.GetProperty("versionNumber").GetInt32().ShouldBe(1);
            job.RootElement.GetProperty("environment").GetString().ShouldBe("production");
            job.RootElement.GetProperty("runtimeIdentifier").GetString().ShouldBe("linux-x64");
            job.RootElement.GetProperty("buildLabel").GetString().ShouldBe("nightly");
        }

        // Poll one target → 200, still Queued.
        using (Stj.JsonDocument polled = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds/production/linux-x64", Read, "acme")))
        {
            polled.RootElement.GetProperty("status").GetString().ShouldBe("Queued");
        }

        // Idempotent re-enqueue for the same target → 202 (resets to Queued).
        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // A second target (a different RID) for the same version.
        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-arm64"}""", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // List → the two targets (order-independent: same-tick enqueues tie-break by id).
        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds", Read, "acme")))
        {
            list.RootElement.GetProperty("nativeBuilds").EnumerateArray()
                .Select(e => e.GetProperty("runtimeIdentifier").GetString())
                .OrderBy(r => r, StringComparer.Ordinal)
                .ShouldBe(["linux-arm64", "linux-x64"]);
        }

        // Count → 2, uncapped.
        using (Stj.JsonDocument count = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds/count", Read, "acme")))
        {
            count.RootElement.GetProperty("count").GetInt32().ShouldBe(2);
            count.RootElement.GetProperty("capped").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task The_list_and_count_filter_by_status()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");
        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // The enqueued job is Queued: filtering by Queued returns it, by Ready returns none.
        using (Stj.JsonDocument queued = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds?status=Queued", Read, "acme")))
        {
            queued.RootElement.GetProperty("nativeBuilds").GetArrayLength().ShouldBe(1);
        }

        using (Stj.JsonDocument ready = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds?status=Ready", Read, "acme")))
        {
            ready.RootElement.GetProperty("nativeBuilds").GetArrayLength().ShouldBe(0);
        }

        using (Stj.JsonDocument countReady = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds/count?status=Ready", Read, "acme")))
        {
            countReady.RootElement.GetProperty("count").GetInt32().ShouldBe(0);
        }
    }

    [TestMethod]
    public async Task An_unknown_version_or_target_is_not_found()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // Enqueue for a non-existent version → 404.
        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/99/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Poll a target that has no build → 404.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds/production/linux-x64", Read, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/nativeBuilds", null, "acme")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The read scope cannot enqueue → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Read, "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Enqueuing_a_build_emits_a_governance_audit_span()
    {
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        (await host.SendJsonAsync(HttpMethod.Post, "/catalog/checkout/versions/1/nativeBuilds", """{"environment":"production","runtimeIdentifier":"linux-x64"}""", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        audit.Events("checkout-v1-production-linux-x64").ShouldBe([("nativebuild.enqueue", "queued")]);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");

        // The catalog skips its §13 catalog-time credential gate (credentials: null), so a source-less version adds freely.
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeTenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantAuthHandler>(ScopeTenantAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    /// <summary>Maps the bearer-free test headers to a deployment identity, with full read reach.</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope, tenant);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string scope, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope, tenant);

        public async Task SeedVersionAsync(string workflowId, string tenant)
        {
            SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);
            await catalog.AddAsync(Package(workflowId), new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private static ReadOnlyMemory<byte> Package(string workflowId)
            => CatalogPackage.Build(Workflow(workflowId), []);

        private static byte[] Workflow(string workflowId)
            => Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope, string tenant)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeTenantAuthHandler.ScopeHeader, scope);
                    request.Headers.Add(ScopeTenantAuthHandler.TenantHeader, tenant);
                }

                return await client.SendAsync(request);
            }
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