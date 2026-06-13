// <copyright file="ControlPlaneSecurityApiTests.cs" company="Endjin Limited">
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

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, "ops");
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(), store, "ops");

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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), requireAuthorization: true, securityPolicyStore: new InMemorySecurityPolicyStore());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
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

    /// <summary>A test scheme: the <c>X-Scopes</c> header authenticates and carries space-delimited scope claims; no header → unauthenticated (401).</summary>
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