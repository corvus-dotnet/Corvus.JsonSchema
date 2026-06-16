// <copyright file="ControlPlaneAdministratorsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// Tests the control-plane workflow-administration management API (§15): a base id's administrator set over
/// <c>/administrators</c>, gated by the <c>administrators:read</c>/<c>administrators:write</c> scopes. An administrator
/// is a deployment-stamped identity named operator-side by the grant <c>{dimension, value}</c> it maps to; the set is
/// governed by current-administrator membership, is never orphanable, and is non-disclosing (unknown base id and
/// not-an-administrator are both 403).
/// </summary>
[TestClass]
public sealed class ControlPlaneAdministratorsApiTests
{
    private const string Write = "administrators:write";
    private const string Read = "administrators:read";
    private const string Acme = "acme";
    private const string Globex = "globex";

    [TestMethod]
    public async Task An_administrator_set_lists_adds_removes_and_transfers()
    {
        await using Scoped host = await StartAsync();

        // acme establishes the base id by publishing version 1, becoming its sole administrator.
        await EstablishAsync(host.Catalog, "flow", Acme);

        // The founder lists as the single administrator, described back as the grant it maps from.
        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/flow", Read, Acme)))
        {
            Grants(listed).ShouldBe(["tenant=acme"]);
        }

        // acme adds globex as a co-administrator (idempotent membership add).
        using (Stj.JsonDocument added = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"tenant","value":"globex"}""", Write, Acme)))
        {
            Grants(added).Order().ShouldBe(["tenant=acme", "tenant=globex"]);
        }

        // globex, now an administrator, removes acme — the set never empties because globex remains.
        using (Stj.JsonDocument removed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, "/administrators/flow/members/tenant/acme", Write, Globex)))
        {
            Grants(removed).ShouldBe(["tenant=globex"]);
        }

        // globex transfers administration to a fresh set (handing it back to acme).
        using (Stj.JsonDocument transferred = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[{"dimension":"tenant","value":"acme"}]}""", Write, Globex)))
        {
            Grants(transferred).ShouldBe(["tenant=acme"]);
        }
    }

    [TestMethod]
    public async Task A_non_administrator_is_refused()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // globex is not an administrator: adding (or transferring, or removing) is refused, non-disclosingly.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"tenant","value":"globex"}""", Write, Globex))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Delete, "/administrators/flow/members/tenant/acme", Write, Globex))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task An_unknown_base_id_is_refused_identically_to_a_non_administrator()
    {
        await using Scoped host = await StartAsync();

        // No administration established for 'ghost': a mutation is a 403, not a 404 — membership is non-disclosing.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/ghost/members", """{"dimension":"tenant","value":"globex"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Listing an unknown base id is an empty set (no administration), not an error.
        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/ghost", Read, Acme));
        Grants(listed).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_last_administrator_cannot_be_removed()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // acme is the sole administrator: removing itself would orphan the workflow — refused (409).
        (await host.SendAsync(HttpMethod.Delete, "/administrators/flow/members/tenant/acme", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Removing_a_non_member_is_an_idempotent_no_op()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // acme is an administrator; globex is not. Removing globex changes nothing and returns the unchanged set (200).
        using Stj.JsonDocument removed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, "/administrators/flow/members/tenant/globex", Write, Acme));
        Grants(removed).ShouldBe(["tenant=acme"]);
    }

    [TestMethod]
    public async Task A_transfer_requires_at_least_one_administrator()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // An empty administrator set is rejected by the schema (minItems) before the handler runs.
        (await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[]}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Administration_management_is_unavailable_without_a_store()
    {
        // A catalog without an administrator store: listing still works (the version-1-derived sole administrator), but
        // mutation is unavailable (409).
        await using Scoped host = await StartAsync(withAdministratorStore: false);
        await EstablishAsync(host.Catalog, "flow", Acme);

        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/flow", Read, Acme)))
        {
            Grants(listed).ShouldBe(["tenant=acme"]);
        }

        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"tenant","value":"globex"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // No scope at all → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/administrators/flow", null, Acme)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"tenant","value":"globex"}""", Read, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/administrators/flow", Write, Acme)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static IEnumerable<string> Grants(Stj.JsonDocument document)
        => document.RootElement.GetProperty("administrators").EnumerateArray()
            .Select(a => $"{a.GetProperty("dimension").GetString()}={a.GetProperty("value").GetString()}");

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    // Publishes version 1 of a base id stamped with the founder's deployment identity (sys:tenant=<founder>), so the
    // founder becomes its sole administrator — mirroring what a real submitter's stamped identity would carry.
    private static async Task EstablishAsync(WorkflowCatalogClient catalog, string workflowId, string founder)
    {
        SecurityTagSet founderIdentity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", founder)]);
        await catalog.AddAsync(Package(workflowId), new CatalogOwner("Team", "team@example.com", null, null), default, founderIdentity, default);
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        return CatalogPackage.Build(workflow, []);
    }

    private static async Task<Scoped> StartAsync(bool withAdministratorStore = true)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, "ops");
        var catalog = new WorkflowCatalogClient(
            new InMemoryWorkflowCatalogStore(),
            store,
            "ops",
            credentials: null,
            administrators: withAdministratorStore ? new InMemoryWorkflowAdministratorStore() : null);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), requireAuthorization: true, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    /// <summary>A minimal scoped policy: an operator (full reach), with the principal's <c>tenant</c> claim stamped as
    /// the deployment identity <c>sys:tenant=&lt;tenant&gt;</c> — so a caller is recognized as an administrator and the
    /// base class's grant mapping (grant {tenant, value} ↔ sys:tenant=value) round-trips.</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, WorkflowCatalogClient catalog) : IAsyncDisposable
    {
        public WorkflowCatalogClient Catalog => catalog;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope, identity);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope, identity);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope, string? identity)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
                }

                if (identity is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.IdentityHeader, identity);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";
        public const string IdentityHeader = "X-Identity";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            if (this.Request.Headers.TryGetValue(IdentityHeader, out Microsoft.Extensions.Primitives.StringValues who) && !string.IsNullOrEmpty(who.ToString()))
            {
                identity.AddClaim(new Claim("tenant", who.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}