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
    public async Task An_mtls_binding_is_created_with_certificate_references_and_is_always_shared()
    {
        await using Scoped host = await StartAsync();

        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"mtls","secretRefs":[{"name":"certificate","ref":"keyvault://petstore-cert"},{"name":"privateKey","ref":"keyvault://petstore-key"}]}""",
            Write);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using Stj.JsonDocument doc = await ReadJsonAsync(created);
        doc.RootElement.GetProperty("authKind").GetString().ShouldBe("mtls");

        // mTLS is connection-level (the TLS handshake), so it is never usage-scoped — the binding carries no usageGrantee.
        doc.RootElement.TryGetProperty("usageGrantee", out _).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_mtls_binding_without_a_certificate_reference_is_rejected()
    {
        await using Scoped host = await StartAsync();

        HttpResponseMessage rejected = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"mtls","secretRefs":[{"name":"value","ref":"keyvault://petstore-key"}]}""",
            Write);

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_usage_scoped_mtls_binding_is_rejected()
    {
        await using Scoped host = await StartAsync();

        // A client certificate is presented at the connection, not per request, so it cannot be scoped to a run: an
        // explicit usageGrantee on an mTLS binding is refused (400) rather than silently ignored.
        HttpResponseMessage rejected = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"mtls","secretRefs":[{"name":"certificate","ref":"keyvault://petstore-cert"}],"usageGrantee":{"identity":[{"dimension":"workflow","value":"nightly-reconcile"}],"kind":"workflow"}}""",
            Write);

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
    public async Task A_binding_carries_independent_management_tags_and_usage_grants()
    {
        await using Scoped host = await StartAsync(new GrantMappingPolicy());

        // Managed by the ops team, but used only by runs of a specific workflow (by its immutable identity) — two
        // unrelated scopes set independently. The usage grant maps to an unforgeable internal tag (sys:workflow=...).
        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://petstore-key"}],"managementTags":[{"key":"team","value":"ops"}],"usageGrantee":{"identity":[{"dimension":"workflow","value":"nightly-reconcile"}],"kind":"workflow","label":"Nightly reconcile"}}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using Stj.JsonDocument doc = await ReadJsonAsync(created);
        doc.RootElement.GetProperty("managementTags").EnumerateArray().Select(t => $"{t.GetProperty("key").GetString()}={t.GetProperty("value").GetString()}").ShouldBe(["team=ops"]);
        Stj.JsonElement grantee = doc.RootElement.GetProperty("usageGrantee");
        grantee.GetProperty("identity").EnumerateArray().Select(g => $"{g.GetProperty("dimension").GetString()}={g.GetProperty("value").GetString()}").ShouldBe(["workflow=nightly-reconcile"]);
        grantee.GetProperty("kind").GetString().ShouldBe("workflow");
        grantee.GetProperty("label").GetString().ShouldBe("Nightly reconcile");
    }

    [TestMethod]
    public async Task Creating_a_binding_whose_management_tags_are_out_of_the_callers_write_reach_is_rejected()
    {
        // The caller's WRITE reach requires team=payments; a binding managed by team=ops is outside it, so the create
        // handler's privilege-escalation guard (Admits over the draft's management tags) rejects it (400) and persists
        // nothing. This exercises the guard's rejection path through the post-draft, non-owning-view Admits check.
        await using Scoped host = await StartAsync(new RestrictedWritePolicy());

        HttpResponseMessage rejected = await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://petstore-key"}],"managementTags":[{"key":"team","value":"ops"}]}""",
            Write);

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using Stj.JsonDocument problem = await ReadJsonAsync(rejected);
        problem.RootElement.GetProperty("type").GetString()!.ShouldContain("management-out-of-reach");

        // Nothing was persisted — the binding does not exist.
        HttpResponseMessage get = await host.SendAsync(HttpMethod.Get, "/credentials/petstore/production", Read);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Lifecycle_metadata_round_trips_and_a_status_is_derived()
    {
        await using Scoped host = await StartAsync();

        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        DateTimeOffset rotatedAt = DateTimeOffset.UtcNow.AddDays(-2);
        string body = $$"""{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://PETSTORE"}],"expiresAt":"{{expiresAt:O}}","rotatedAt":"{{rotatedAt:O}}"}""";

        using (Stj.JsonDocument doc = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/credentials", body, Write)))
        {
            DateTimeOffset.Parse(doc.RootElement.GetProperty("expiresAt").GetString()!).ShouldBe(expiresAt);
            DateTimeOffset.Parse(doc.RootElement.GetProperty("rotatedAt").GetString()!).ShouldBe(rotatedAt);
            doc.RootElement.GetProperty("credentialStatus").GetString().ShouldBe("valid"); // 30 days out, beyond the window
        }

        // The metadata survives the get/list read path too.
        using (Stj.JsonDocument got = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/credentials/petstore/production", Read)))
        {
            DateTimeOffset.Parse(got.RootElement.GetProperty("expiresAt").GetString()!).ShouldBe(expiresAt);
            got.RootElement.GetProperty("credentialStatus").GetString().ShouldBe("valid");
        }
    }

    [TestMethod]
    public async Task The_derived_status_reflects_expiry_against_the_window()
    {
        await using Scoped host = await StartAsync();

        async Task<string?> StatusForExpiryAsync(string source, DateTimeOffset expiresAt)
        {
            string body = $$"""{"sourceName":"{{source}}","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://X"}],"expiresAt":"{{expiresAt:O}}"}""";
            using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/credentials", body, Write));
            return doc.RootElement.GetProperty("credentialStatus").GetString();
        }

        // Default expiring window is 7 days: 30d out = valid, 3d out = expiringSoon, 1d past = expired.
        (await StatusForExpiryAsync("far", DateTimeOffset.UtcNow.AddDays(30))).ShouldBe("valid");
        (await StatusForExpiryAsync("near", DateTimeOffset.UtcNow.AddDays(3))).ShouldBe("expiringSoon");
        (await StatusForExpiryAsync("gone", DateTimeOffset.UtcNow.AddDays(-1))).ShouldBe("expired");
    }

    [TestMethod]
    public async Task A_binding_without_an_expiry_is_valid_and_omits_expiresAt()
    {
        await using Scoped host = await StartAsync();

        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://PETSTORE"}]}""",
            Write));

        doc.RootElement.GetProperty("credentialStatus").GetString().ShouldBe("valid");
        doc.RootElement.TryGetProperty("expiresAt", out _).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_update_can_rotate_the_expiry_and_refresh_the_status()
    {
        await using Scoped host = await StartAsync();

        string expired = $$"""{"sourceName":"petstore","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://PETSTORE"}],"expiresAt":"{{DateTimeOffset.UtcNow.AddDays(-1):O}}"}""";
        using (Stj.JsonDocument created = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/credentials", expired, Write)))
        {
            created.RootElement.GetProperty("credentialStatus").GetString().ShouldBe("expired");
        }

        string rotated = $$"""{"authKind":"apiKey","secretRefs":[{"name":"value","ref":"env://PETSTORE#2"}],"expiresAt":"{{DateTimeOffset.UtcNow.AddDays(30):O}}"}""";
        using (Stj.JsonDocument updated = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/credentials/petstore/production", rotated, Write)))
        {
            updated.RootElement.GetProperty("credentialStatus").GetString().ShouldBe("valid");
        }
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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), (rowSecurity is null ? ControlPlaneSecurityMode.ScopesOnly : ControlPlaneSecurityMode.Scoped), rowSecurity: rowSecurity, sourceCredentialStore: new InMemorySourceCredentialStore());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    /// <summary>A minimal scoped policy: an operator (full reach) so the management guard passes, while the base
    /// class's grant mapping (grant {dimension,value} → sys:dimension=value) is used unchanged — so usage grants
    /// resolve to unforgeable internal tags and describe back.</summary>
    private sealed class GrantMappingPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(System.Security.Claims.ClaimsPrincipal? principal) => AccessContext.System;
    }

    /// <summary>A scoped policy whose WRITE reach requires <c>team=payments</c> — so a binding managed by any other team
    /// is outside the caller's write reach, exercising the create handler's privilege-escalation guard.</summary>
    private sealed class RestrictedWritePolicy : ControlPlaneRowSecurityPolicy
    {
        private static readonly SecurityFilter WriteReach = new SecurityShell([]).BuildFilter(
            [SecurityRule.Compile("team == 'payments'")],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

        public override AccessContext Resolve(System.Security.Claims.ClaimsPrincipal? principal) => new(null, WriteReach, null);
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