// <copyright file="ControlPlaneEnvironmentsApiTests.cs" company="Endjin Limited">
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
/// Tests the control-plane environments management API (§7.7): governed, reach-scoped environment CRUD over
/// <c>/environments</c> plus its administrator governance under <c>/environments/{name}/administrators</c>, gated by the
/// <c>environments:read</c>/<c>environments:write</c> scopes. Creating an environment grants the creator administration;
/// update/delete and administrator changes require current-administrator membership (so they need a configured identity).
/// </summary>
[TestClass]
public sealed class ControlPlaneEnvironmentsApiTests
{
    private const string Write = "environments:write";
    private const string Read = "environments:read";

    [TestMethod]
    public async Task An_environment_has_a_full_create_get_list_update_delete_lifecycle()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments",
            """{"name":"production","displayName":"Production","description":"The live environment."}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            doc.RootElement.GetProperty("name").GetString().ShouldBe("production");
            doc.RootElement.GetProperty("displayName").GetString().ShouldBe("Production");
            doc.RootElement.GetProperty("createdBy").GetString().ShouldNotBeNullOrEmpty();
            doc.RootElement.GetProperty("etag").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Get, "/environments/production", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments", Read)))
        {
            list.RootElement.GetProperty("environments").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ShouldBe(["production"]);
        }

        HttpResponseMessage updated = await host.SendJsonAsync(
            HttpMethod.Put,
            "/environments/production",
            """{"displayName":"Prod (live)","description":"Updated."}""",
            Write);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(updated))
        {
            doc.RootElement.GetProperty("displayName").GetString().ShouldBe("Prod (live)");
            doc.RootElement.GetProperty("description").GetString().ShouldBe("Updated.");
            doc.RootElement.GetProperty("lastUpdatedBy").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Delete, "/environments/production", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, "/environments/production", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Creating_a_duplicate_environment_conflicts()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        const string body = """{"name":"production"}""";
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", body, Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", body, Write)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Getting_a_missing_environment_is_not_found()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendAsync(HttpMethod.Get, "/environments/nope", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/environments", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"x"}""", Read)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/environments", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Creating_an_environment_grants_the_creator_administration()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // The creator's deployment identity (sys:tenant=acme) is the sole administrator, described back as the grant it
        // maps from (tenant=acme) — never the raw internal tag.
        using Stj.JsonDocument admins = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/administrators", Read));
        Stj.JsonElement[] set = [.. admins.RootElement.GetProperty("administrators").EnumerateArray()];
        set.Length.ShouldBe(1);
        set[0].GetProperty("identity").EnumerateArray()
            .Select(g => $"{g.GetProperty("dimension").GetString()}={g.GetProperty("value").GetString()}")
            .ShouldBe(["tenant=acme"]);
        set[0].GetProperty("digest").GetString().ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task An_environment_administrator_can_be_added_then_removed_by_digest()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Add a second administrator by its deployment-mapped grant; the set grows to two.
        HttpResponseMessage added = await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/administrators/members",
            """{"value":"reconcile","dimension":"workflow"}""",
            Write);
        added.StatusCode.ShouldBe(HttpStatusCode.OK);
        string digest;
        using (Stj.JsonDocument doc = await ReadJsonAsync(added))
        {
            Stj.JsonElement[] set = [.. doc.RootElement.GetProperty("administrators").EnumerateArray()];
            set.Length.ShouldBe(2);

            // The digest of the newly added (workflow=reconcile) administrator is the removal key.
            digest = set
                .Single(a => a.GetProperty("identity").EnumerateArray().Any(g => g.GetProperty("value").GetString() == "reconcile"))
                .GetProperty("digest").GetString()!;
        }

        // Remove it by digest → back to the sole creator.
        HttpResponseMessage removed = await host.SendAsync(HttpMethod.Delete, $"/environments/production/administrators/members/{digest}", Write);
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(removed))
        {
            doc.RootElement.GetProperty("administrators").EnumerateArray().Count().ShouldBe(1);
        }
    }

    [TestMethod]
    public async Task Governance_mutations_require_a_current_administrator()
    {
        // ScopesOnly mode: the caller has scopes but NO deployment identity, so creating an environment establishes no
        // administration (no identity to grant) — and update/delete are then refused as non-administrator (403).
        await using Scoped host = await StartAsync();

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Get, "/environments/production", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"displayName":"x"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(ControlPlaneRowSecurityPolicy? rowSecurity = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), rowSecurity is null ? ControlPlaneSecurityMode.ScopesOnly : ControlPlaneSecurityMode.Scoped, rowSecurity: rowSecurity);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    /// <summary>A scoped policy giving every caller full reach (so environments are visible) and a fixed deployment
    /// identity <c>sys:tenant=acme</c> (so create-grants-admin establishes administration and the current-administrator
    /// governance gate passes). The base class's grant mapping describes <c>sys:tenant=acme</c> back as <c>tenant=acme</c>.</summary>
    private sealed class TenantPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [new SecurityTag("sys:tenant", "acme")];
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}