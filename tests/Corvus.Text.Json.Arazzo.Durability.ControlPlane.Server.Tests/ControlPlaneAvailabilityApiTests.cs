// <copyright file="ControlPlaneAvailabilityApiTests.cs" company="Endjin Limited">
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
/// Tests the control-plane availability ("promotion") API (§7.8): making a workflow version available in a deployment
/// environment over <c>/catalog/{id}/versions/{n}/availability/{environment}</c>, the two list views, governed by the
/// target environment's administrators and readiness-gated (§7.7). Gated by the <c>availability:read</c>/<c>write</c> scopes.
/// </summary>
[TestClass]
public sealed class ControlPlaneAvailabilityApiTests
{
    private const string Write = "availability:write";
    private const string Read = "availability:read";

    [TestMethod]
    public async Task A_version_is_made_available_listed_and_withdrawn_by_an_environment_administrator()
    {
        await using Scoped host = await StartAsync();

        // acme provisions the environment (granting itself administration) and a source-less version (ready by vacuity).
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "environments:write", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // Make available → 201; idempotent repeat → 200.
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The version lists the environments it is available in.
        using (Stj.JsonDocument byVersion = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/availability", Read, "acme")))
        {
            byVersion.RootElement.GetProperty("availability").EnumerateArray().Select(e => e.GetProperty("environment").GetString()).ShouldBe(["production"]);
        }

        // The environment lists the (workflow, version) pairs available in it.
        using (Stj.JsonDocument byEnv = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/availability", Read, "acme")))
        {
            Stj.JsonElement entry = byEnv.RootElement.GetProperty("availability").EnumerateArray().Single();
            entry.GetProperty("baseWorkflowId").GetString().ShouldBe("checkout");
            entry.GetProperty("versionNumber").GetInt32().ShouldBe(1);
        }

        // Withdraw → 204; withdrawing again is not-found.
        (await host.SendAsync(HttpMethod.Delete, "/catalog/checkout/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Delete, "/catalog/checkout/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Making_available_requires_target_environment_administration_and_existing_resources()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "environments:write", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // globex is not an administrator of 'production' → 403 (after passing the environment-visibility check).
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/1/availability/production", Write, "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A non-existent environment → 404; a non-existent version → 404.
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/1/availability/nowhere", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/99/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Readiness_gates_making_a_version_available()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "environments:write", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A version that references a source with no credential in 'production' is not ready → 409.
        await host.SeedVersionAsync("billing", "acme", "payments");
        (await host.SendAsync(HttpMethod.Put, "/catalog/billing/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Once the source has a credential in 'production', the version is ready → 201.
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"payments","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://payments#1"}]}""",
            "credentials:write",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Put, "/catalog/billing/versions/1/availability/production", Write, "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "environments:write", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/availability", null, "acme")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The read scope cannot make available → 403.
        (await host.SendAsync(HttpMethod.Put, "/catalog/checkout/versions/1/availability/production", Read, "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");

        // The catalog skips its §13 catalog-time credential gate (credentials: null), so a version may reference a source
        // with no credential yet — the §7.8 availability readiness gate is what we exercise here.
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

    /// <summary>Maps the bearer-free test headers to a deployment identity: <c>X-Tenant</c> becomes <c>sys:tenant=&lt;t&gt;</c>
    /// (so create-grants-admin and the current-administrator gate are driven per caller), with full read reach.</summary>
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

        public async Task SeedVersionAsync(string workflowId, string tenant, params string[] sourceNames)
        {
            SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);
            await catalog.AddAsync(Package(workflowId, sourceNames), new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private static ReadOnlyMemory<byte> Package(string workflowId, string[] sourceNames)
        {
            string descriptions = string.Join(",", sourceNames.Select(n => $$"""{ "name": "{{n}}", "url": "./{{n}}.json", "type": "openapi" }"""));
            byte[] workflow = Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [{{descriptions}}],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);
            byte[] sourceDoc = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Source","version":"1.0"},"paths":{}}""");
            var sources = sourceNames
                .Select(n => new KeyValuePair<string, byte[]>(n, sourceDoc))
                .ToList();
            return CatalogPackage.Build(workflow, sources);
        }

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