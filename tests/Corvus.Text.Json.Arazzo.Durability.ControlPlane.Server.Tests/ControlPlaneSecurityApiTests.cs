// <copyright file="ControlPlaneSecurityApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
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
/// Tests the control-plane security-authoring API (§14.2): rule/binding CRUD over <c>/security/*</c>, gated by the
/// <c>security:read</c>/<c>security:write</c> capability scopes, with grammar validation (400) and duplicate
/// detection (409).
/// </summary>
[TestClass]
public sealed class ControlPlaneSecurityApiTests
{
    private const string Write = "security:write";
    private const string Read = "security:read";

    [TestMethod]
    public async Task A_rule_has_a_full_create_get_list_update_delete_lifecycle()
    {
        await using Scoped host = await StartAsync();

        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"tenant-scoped","expression":"tenant == $claim.tenant","description":"Tenant isolation."}""", Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            doc.RootElement.GetProperty("name").GetString().ShouldBe("tenant-scoped");
            doc.RootElement.GetProperty("expression").GetString().ShouldBe("tenant == $claim.tenant");
            doc.RootElement.GetProperty("etag").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Get, "/security/rules/tenant-scoped", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/rules", Read)))
        {
            list.RootElement.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(["tenant-scoped"]);
        }

        HttpResponseMessage updated = await host.SendJsonAsync(HttpMethod.Put, "/security/rules/tenant-scoped", """{"expression":"sys:tenant == $claim.tenant"}""", Write);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(updated))
        {
            doc.RootElement.GetProperty("expression").GetString().ShouldBe("sys:tenant == $claim.tenant");
        }

        (await host.SendAsync(HttpMethod.Delete, "/security/rules/tenant-scoped", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, "/security/rules/tenant-scoped", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_rule_list_keyset_pages_and_filters_over_http()
    {
        await using Scoped host = await StartAsync();

        foreach (string name in new[] { "charlie", "alpha", "bravo" })
        {
            (await host.SendJsonAsync(HttpMethod.Post, "/security/rules", $$"""{"name":"{{name}}","expression":"tenant == '{{name}}'"}""", Write))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        // First page (limit 2): the first two rules in name order plus a continuation token (the q/limit/pageToken
        // query parameters are bound by the generated handler).
        string token;
        using (Stj.JsonDocument page1 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/rules?limit=2", Read)))
        {
            page1.RootElement.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(["alpha", "bravo"]);
            token = page1.RootElement.GetProperty("nextPageToken").GetString()!;
            token.ShouldNotBeNullOrEmpty();
        }

        // Following the token returns the remainder; the last page omits nextPageToken (no further rules).
        using (Stj.JsonDocument page2 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/security/rules?limit=2&pageToken={token}", Read)))
        {
            page2.RootElement.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(["charlie"]);
            page2.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse();
        }

        // The q filter matches a case-insensitive substring of the name (or expression).
        using (Stj.JsonDocument filtered = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/rules?q=ALPHA", Read)))
        {
            filtered.RootElement.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(["alpha"]);
        }
    }

    [TestMethod]
    public async Task A_malformed_rule_expression_is_a_bad_request()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"bad","expression":"$claims.bogus"}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"bad2","expression":"tenant =="}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Creating_a_duplicate_rule_conflicts()
    {
        await using Scoped host = await StartAsync();
        await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"r","expression":"tenant"}""", Write);
        (await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"r","expression":"team"}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();

        // No scope at all → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/security/rules", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403; and cannot create.
        (await host.SendJsonAsync(HttpMethod.Post, "/security/rules", """{"name":"r","expression":"tenant"}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot necessarily read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/security/rules", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task The_binding_list_keyset_pages_and_filters_over_http()
    {
        await using Scoped host = await StartAsync();

        foreach ((int order, string claim) in new[] { (30, "charlie"), (10, "alpha"), (20, "bravo") })
        {
            (await host.SendJsonAsync(HttpMethod.Post, "/security/bindings", $$"""{"claimType":"role","claimValue":"{{claim}}","read":{"unrestricted":true},"write":{"unrestricted":false},"purge":{"unrestricted":false},"order":{{order}}}""", Write))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        // First page (limit 2): the two lowest-order bindings in (order, id) order plus a continuation token.
        string token;
        using (Stj.JsonDocument page1 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/bindings?limit=2", Read)))
        {
            page1.RootElement.GetProperty("bindings").EnumerateArray().Select(b => b.GetProperty("claimValue").GetString()).ShouldBe(["alpha", "bravo"]);
            token = page1.RootElement.GetProperty("nextPageToken").GetString()!;
            token.ShouldNotBeNullOrEmpty();
        }

        // Following the token returns the remainder; the last page omits nextPageToken.
        using (Stj.JsonDocument page2 = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/security/bindings?limit=2&pageToken={token}", Read)))
        {
            page2.RootElement.GetProperty("bindings").EnumerateArray().Select(b => b.GetProperty("claimValue").GetString()).ShouldBe(["charlie"]);
            page2.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse();
        }

        // The q filter matches a case-insensitive substring of the claim type, claim value, or description.
        using (Stj.JsonDocument filtered = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/bindings?q=ALPHA", Read)))
        {
            filtered.RootElement.GetProperty("bindings").EnumerateArray().Select(b => b.GetProperty("claimValue").GetString()).ShouldBe(["alpha"]);
        }
    }

    [TestMethod]
    public async Task A_binding_has_a_full_lifecycle()
    {
        await using Scoped host = await StartAsync();

        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/security/bindings",
            """{"claimType":"role","claimValue":"tenant-admin","read":{"ruleNames":["tenant-scoped"]},"write":{"ruleNames":["tenant-scoped"]},"purge":{"unrestricted":false},"order":10}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string id;
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            id = doc.RootElement.GetProperty("id").GetString()!;
            doc.RootElement.GetProperty("claimValue").GetString().ShouldBe("tenant-admin");
            doc.RootElement.GetProperty("read").GetProperty("ruleNames").EnumerateArray().Select(x => x.GetString()).ShouldBe(["tenant-scoped"]);
            doc.RootElement.GetProperty("order").GetInt32().ShouldBe(10);
        }

        (await host.SendAsync(HttpMethod.Get, $"/security/bindings/{id}", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/security/bindings", Read)))
        {
            list.RootElement.GetProperty("bindings").GetArrayLength().ShouldBe(1);
        }

        HttpResponseMessage updated = await host.SendJsonAsync(HttpMethod.Put, $"/security/bindings/{id}", """{"claimType":"role","claimValue":"operator","read":{"unrestricted":true},"write":{"unrestricted":true},"purge":{"unrestricted":true}}""", Write);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(updated))
        {
            doc.RootElement.GetProperty("read").GetProperty("unrestricted").GetBoolean().ShouldBeTrue();
        }

        (await host.SendAsync(HttpMethod.Delete, $"/security/bindings/{id}", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, $"/security/bindings/{id}", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [TestMethod]
    public async Task The_orderings_endpoint_projects_the_configured_orderings_and_is_empty_without_a_policy()
    {
        var store = new InMemorySecurityPolicyStore();
        var orderings = new SecurityLabelOrderings(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["classification"] = ["public", "internal", "confidential", "restricted"],
        });
        var handler = new ArazzoControlPlaneSecurityHandler(store, new PersistentRowSecurityPolicy(store, orderings: orderings));

        using (JsonWorkspace workspace = JsonWorkspace.Create())
        {
            ListSecurityOrderingsResult result = await handler.HandleListSecurityOrderingsAsync(default, workspace);
            result.StatusCode.ShouldBe(200);
            using Stj.JsonDocument doc = ReadResultBody(result);
            Stj.JsonElement classification = doc.RootElement.GetProperty("orderings").EnumerateArray()
                .Single(o => o.GetProperty("dimension").GetString() == "classification");
            classification.GetProperty("labels").EnumerateArray().Select(l => l.GetString())
                .ShouldBe(["public", "internal", "confidential", "restricted"]);
        }

        // With no policy (the ScopesOnly/Open posture) there are no orderings — an empty list, never a null.
        var unconfigured = new ArazzoControlPlaneSecurityHandler(store);
        using (JsonWorkspace workspace = JsonWorkspace.Create())
        {
            ListSecurityOrderingsResult result = await unconfigured.HandleListSecurityOrderingsAsync(default, workspace);
            using Stj.JsonDocument doc = ReadResultBody(result);
            doc.RootElement.GetProperty("orderings").GetArrayLength().ShouldBe(0);
        }
    }

    // Reads a result's CTJ body as UTF-8 and re-parses it with System.Text.Json so the test asserts over the wire shape.
    private static Stj.JsonDocument ReadResultBody(ListSecurityOrderingsResult result)
        => Stj.JsonDocument.Parse(JsonMarshal.GetRawUtf8Value(result.Body).Memory);

    [TestMethod]
    public async Task The_self_elevation_guard_rejects_a_caller_granting_itself_write_or_purge()
    {
        var policyStore = new InMemorySecurityPolicyStore();
        await using Scoped host = await StartSecuredAsync(policyStore);

        // The caller is in the payments team. Granting the payments team WRITE reach is self-elevation → 403.
        const string caller = "team=payments";
        (await host.SendJsonAsync(HttpMethod.Post, "/security/bindings", """{"claimType":"team","claimValue":"payments","read":{"unrestricted":false},"write":{"unrestricted":true}}""", Write, caller))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Rule-bounded write the caller matches is also self-elevation.
        (await host.SendJsonAsync(HttpMethod.Post, "/security/bindings", """{"claimType":"team","claimValue":"payments","write":{"ruleNames":["reach-payments"]}}""", Write, caller))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Read-only reach for the caller's own team is allowed (direct group/role policy authoring).
        HttpResponseMessage readOnly = await host.SendJsonAsync(HttpMethod.Post, "/security/bindings", """{"claimType":"team","claimValue":"payments","read":{"unrestricted":true}}""", Write, caller);
        readOnly.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Granting write to a team the caller is NOT in is not self-elevation (a policy decision, allowed).
        (await host.SendJsonAsync(HttpMethod.Post, "/security/bindings", """{"claimType":"team","claimValue":"billing","write":{"unrestricted":true}}""", Write, caller))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // The guard runs on UPDATE too: editing the caller's own read-only binding up to write is self-elevation → 403.
        string bindingId;
        using (Stj.JsonDocument doc = await ReadJsonAsync(readOnly))
        {
            bindingId = doc.RootElement.GetProperty("id").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Put, $"/security/bindings/{bindingId}", """{"claimType":"team","claimValue":"payments","write":{"unrestricted":true}}""", Write, caller))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Scoped> StartSecuredAsync(ISecurityPolicyStore policyStore)
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

        // RowSecurityOnly: the row-security policy is active (so the security handler sees the caller for the
        // self-elevation guard) while capability-scope gating is off (the test exercises the guard, not scopes).
        var policy = new PersistentRowSecurityPolicy(policyStore);
        await policy.RefreshAsync();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.RowSecurityOnly, rowSecurity: policy, securityPolicyStore: policyStore);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    private static async Task<Scoped> StartAsync()
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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly, securityPolicyStore: new InMemorySecurityPolicyStore());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope, string callerClaims)
        {
            var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add(ScopeAuthHandler.ClaimsHeader, callerClaims);
            return this.SendCoreAsync(request, scope);
        }

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

    /// <summary>A test scheme: the <c>X-Scopes</c> header authenticates and carries space-delimited scope claims; no header → unauthenticated (401).</summary>
    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";
        public const string ClaimsHeader = "X-Claims";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));

            // Optional caller identity claims (e.g. the self-elevation guard's `team=payments`), as a comma-separated
            // list of `type=value` pairs in the X-Claims header.
            if (this.Request.Headers.TryGetValue(ClaimsHeader, out Microsoft.Extensions.Primitives.StringValues claimValues))
            {
                foreach (string pair in claimValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int eq = pair.IndexOf('=', StringComparison.Ordinal);
                    if (eq > 0)
                    {
                        identity.AddClaim(new Claim(pair[..eq], pair[(eq + 1)..]));
                    }
                }
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}