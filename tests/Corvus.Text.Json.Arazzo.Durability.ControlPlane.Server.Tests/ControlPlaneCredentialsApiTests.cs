// <copyright file="ControlPlaneCredentialsApiTests.cs" company="Endjin Limited">
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
/// Tests the control-plane source-credential management API (§13): binding CRUD over <c>/credentials</c>, gated by the
/// <c>credentials:read</c>/<c>credentials:write</c> scopes, with the trust-boundary invariant that the API stores and
/// returns references and metadata only — never secret material — and rejects an inline secret (400).
/// </summary>
[TestClass]
public sealed class ControlPlaneCredentialsApiTests
{
    private const string Write = "credentials:write";
    private const string Read = "credentials:read";

    [TestMethod]
    public async Task A_binding_has_a_full_create_get_list_update_delete_lifecycle()
    {
        await using Scoped host = await StartAsync();

        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://petstore-key#3"}],"config":[{"key":"parameterName","value":"X-Api-Key"}],"description":"Petstore key."}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            doc.RootElement.GetProperty("sourceName").GetString().ShouldBe("petstore");
            doc.RootElement.GetProperty("environment").GetString().ShouldBe("production");
            doc.RootElement.GetProperty("authKind").GetString().ShouldBe("apiKey");
            doc.RootElement.GetProperty("secretRefs")[0].GetProperty("ref").GetString().ShouldBe("keyvault://petstore-key#3");
            doc.RootElement.GetProperty("etag").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Get, "/credentials/petstore/production", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/credentials", Read)))
        {
            list.RootElement.GetProperty("credentials").EnumerateArray().Select(c => c.GetProperty("sourceName").GetString()).ShouldBe(["petstore"]);
        }

        HttpResponseMessage updated = await host.SendJsonAsync(
            HttpMethod.Put,
            "/credentials/petstore/production",
            """{"authKind":"bearer","secretRefs":[{"name":"value","ref":"keyvault://petstore-token#9"}]}""",
            Write);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(updated))
        {
            doc.RootElement.GetProperty("authKind").GetString().ShouldBe("bearer");
            doc.RootElement.GetProperty("secretRefs")[0].GetProperty("ref").GetString().ShouldBe("keyvault://petstore-token#9");
            doc.RootElement.GetProperty("lastUpdatedBy").GetString().ShouldNotBeNullOrEmpty();
        }

        (await host.SendAsync(HttpMethod.Delete, "/credentials/petstore/production", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, "/credentials/petstore/production", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task An_inline_secret_is_rejected_at_the_api_edge()
    {
        await using Scoped host = await StartAsync();

        // A bare value with no scheme is not a SecretRef — the API must refuse it so secret material cannot be
        // smuggled into the binding inline.
        HttpResponseMessage response = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"x","environment":"y","authKind":"apiKey","secretRefs":[{"name":"value","ref":"hunter2-the-actual-secret"}]}""",
            Write);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Creating_a_duplicate_binding_conflicts()
    {
        await using Scoped host = await StartAsync();
        const string body = """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://PETSTORE"}]}""";
        (await host.SendJsonAsync(HttpMethod.Post, "/credentials", body, Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Post, "/credentials", body, Write)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Updating_or_getting_a_missing_binding_is_not_found()
    {
        await using Scoped host = await StartAsync();
        (await host.SendAsync(HttpMethod.Get, "/credentials/nope/production", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendJsonAsync(HttpMethod.Put, "/credentials/nope/production", """{"authKind":"bearer","secretRefs":[{"name":"value","ref":"env://X"}]}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendAsync(HttpMethod.Delete, "/credentials/nope/production", Write)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();

        // No scope at all → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/credentials", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/credentials", """{"sourceName":"a","environment":"b","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://A"}]}""", Read))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/credentials", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_binding_carries_independent_management_and_usage_tags()
    {
        await using Scoped host = await StartAsync();

        // Managed by the ops team, but used by acme's runs — two unrelated scopes set independently.
        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://petstore-key"}],"managementTags":[{"key":"team","value":"ops"}],"usageTags":[{"key":"tenant","value":"acme"}]}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using Stj.JsonDocument doc = await ReadJsonAsync(created);
        doc.RootElement.GetProperty("managementTags").EnumerateArray().Select(t => $"{t.GetProperty("key").GetString()}={t.GetProperty("value").GetString()}").ShouldBe(["team=ops"]);
        doc.RootElement.GetProperty("usageTags").EnumerateArray().Select(t => $"{t.GetProperty("key").GetString()}={t.GetProperty("value").GetString()}").ShouldBe(["tenant=acme"]);
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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), requireAuthorization: true, sourceCredentialStore: new InMemorySourceCredentialStore());
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