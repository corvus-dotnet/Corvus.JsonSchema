// <copyright file="ControlPlaneProvidersApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the connected-providers API (ADR 0052) against a loopback stub OIDC provider: the
/// registry listing with per-caller connection state, the brokered sign-in (endpoints resolved by
/// OIDC discovery; a standard authorization-code exchange carrying grant_type and redirect_uri;
/// the single-use provider-bound state), the connection-aware fetch (the caller's user token rides
/// only to a covered host), the one-shot secret (this single fetch, never stored, never echoed),
/// the fetch-auth exclusivity rule, and the empty-registry posture.
/// </summary>
[TestClass]
public sealed class ControlPlaneProvidersApiTests
{
    private const string GoodCode = "good-code";
    private const string StubToken = "portal-user-token";
    private const string OneShotSecret = "pat-one-shot-123";

    private const string PetstoreJson =
        """{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0"},"paths":{}}""";

    [TestMethod]
    public async Task The_sign_in_flow_discovers_endpoints_connects_and_shows_in_the_listing()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(portal.Url);

        // The listing names the provider, its host coverage, and the caller's (disconnected) state.
        using (Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "ada")))
        {
            Stj.JsonElement provider = listing.RootElement.GetProperty("providers")[0];
            provider.GetProperty("name").GetString().ShouldBe("portal");
            provider.GetProperty("displayName").GetString().ShouldBe("Dev Portal");
            provider.GetProperty("hosts")[0].GetString().ShouldBe("127.0.0.1");
            provider.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }

        // Begin: the authorize URL points at the DISCOVERED endpoint with the standard query.
        HttpResponseMessage begun = await host.SendAsync(HttpMethod.Post, "/providers/portal/auth", "workspace:write", "ada");
        begun.StatusCode.ShouldBe(HttpStatusCode.OK);
        string state;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await begun.Content.ReadAsStringAsync()))
        {
            string authorizeUrl = doc.RootElement.GetProperty("authorizeUrl").GetString()!;
            authorizeUrl.ShouldStartWith($"{portal.Url}/oauth/authorize");
            authorizeUrl.ShouldContain("response_type=code");
            authorizeUrl.ShouldContain("scope=openid%20profile");
            state = doc.RootElement.GetProperty("state").GetString()!;
        }

        // The callback is a top-level navigation: NO bearer — the single-use state IS the authentication.
        (await host.SendAnonymousAsync($"/providers/portal/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // The exchange was a STANDARD authorization-code grant (Keycloak requires these; GitHub tolerates them).
        portal.LastTokenForm.ShouldNotBeNull();
        portal.LastTokenForm!["grant_type"].ShouldBe("authorization_code");
        portal.LastTokenForm["redirect_uri"].ShouldBe("http://localhost/providers/portal/auth/callback");

        // Connected for ada; still disconnected for bob (custody keys by principal AND provider).
        using (Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "ada")))
        {
            listing.RootElement.GetProperty("providers")[0].GetProperty("connected").GetBoolean().ShouldBeTrue();
        }

        using (Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "bob")))
        {
            listing.RootElement.GetProperty("providers")[0].GetProperty("connected").GetBoolean().ShouldBeFalse();
        }

        // Disconnect: idempotent; the listing reads disconnected afterwards.
        (await host.SendAsync(HttpMethod.Delete, "/providers/portal/session", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Delete, "/providers/portal/session", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "ada")))
        {
            listing.RootElement.GetProperty("providers")[0].GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task The_state_is_single_use_and_bound_to_its_provider()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(portal.Url);

        string state = await host.BeginAsync("ada");

        // A state begun for 'portal' cannot complete through another provider's callback route.
        (await host.SendAnonymousAsync($"/providers/other/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // It still completes on its own route — the mismatched attempt consumed nothing it owned…
        (await host.SendAnonymousAsync($"/providers/portal/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // …but the state IS single-use: the failed attempt consumed it.

        // Begin again: a refused exchange (bad code) does not connect, and garbage state refuses.
        string second = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/providers/portal/auth/callback?code=wrong&state={Uri.EscapeDataString(second)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAnonymousAsync("/providers/portal/auth/callback?code=x&state=nonsense"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "ada"));
        listing.RootElement.GetProperty("providers")[0].GetProperty("connected").GetBoolean().ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_provider_fetch_rides_the_callers_token_and_only_to_a_covered_host()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(portal.Url);

        // Not connected: the fetch refuses rather than fetching anonymously under a claimed identity.
        HttpResponseMessage refused = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"portal"}}""", "sources:read", "ada");
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("provider-not-connected");

        // Connect ada, then fetch with the provider connection: the user token rides as Bearer.
        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/providers/portal/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument fetched = await ReadAsync(await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"portal"}}""", "sources:read", "ada")))
        {
            fetched.RootElement.GetProperty("type").GetString().ShouldBe("openapi");
            fetched.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Petstore");
        }

        portal.LastSpecAuthorization.ShouldBe($"Bearer {StubToken}");

        // The host gate: the token never rides to a host outside the provider's declared coverage.
        HttpResponseMessage uncovered = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", """{"url":"https://other.example/spec.json","auth":{"provider":"portal"}}""", "sources:read", "ada");
        uncovered.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await uncovered.Content.ReadAsStringAsync()).ShouldContain("provider-host-not-covered");

        // An unregistered provider name is a typed refusal.
        HttpResponseMessage unknown = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"nope"}}""", "sources:read", "ada");
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await unknown.Content.ReadAsStringAsync()).ShouldContain("provider-unknown");

        // bob holds no connection: ada's custody row is unreachable from bob's session.
        HttpResponseMessage bobRefused = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"portal"}}""", "sources:read", "bob");
        bobRefused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await bobRefused.Content.ReadAsStringAsync()).ShouldContain("provider-not-connected");
    }

    [TestMethod]
    public async Task A_one_shot_secret_authenticates_the_single_fetch_and_is_never_stored_or_echoed()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(portal.Url);

        HttpResponseMessage fetched = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"secret":"{{{OneShotSecret}}}"}}""", "sources:read", "ada");
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        portal.LastSpecAuthorization.ShouldBe($"Bearer {OneShotSecret}");
        (await fetched.Content.ReadAsStringAsync()).ShouldNotContain(OneShotSecret);

        // Nothing was stored: the next fetch without auth goes out anonymous and the secured
        // endpoint's 401 maps to the upstream failure.
        (await host.SendJsonAsync(HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json"}""", "sources:read", "ada"))
            .StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        portal.LastSpecAuthorization.ShouldBeNull();
    }

    [TestMethod]
    public async Task Fetch_auth_modes_are_mutually_exclusive()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(portal.Url);

        HttpResponseMessage both = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"portal","secret":"x"}}""", "sources:read", "ada");
        both.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await both.Content.ReadAsStringAsync()).ShouldContain("invalid-fetch-auth");

        HttpResponseMessage none = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{}}""", "sources:read", "ada");
        none.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_deployment_without_providers_lists_empty_refuses_auth_and_scopes_are_enforced()
    {
        await using StubProvider portal = await StubProvider.StartAsync();
        await using Scoped host = await StartAsync(providerIssuerUrl: null);

        using (Stj.JsonDocument listing = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/providers", "workspace:read", "ada")))
        {
            listing.RootElement.GetProperty("providers").GetArrayLength().ShouldBe(0);
        }

        (await host.SendAsync(HttpMethod.Post, "/providers/portal/auth", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAsync(HttpMethod.Delete, "/providers/portal/session", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        HttpResponseMessage fetchRefused = await host.SendJsonAsync(
            HttpMethod.Post, "/sources/fetch", $$$"""{"url":"{{{portal.Url}}}/specs/petstore.json","auth":{"provider":"portal"}}""", "sources:read", "ada");
        fetchRefused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await fetchRefused.Content.ReadAsStringAsync()).ShouldContain("providers-not-configured");

        // Scope gating: beginning a sign-in needs workspace:write.
        (await host.SendAsync(HttpMethod.Post, "/providers/portal/auth", "workspace:read", "ada")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Scoped> StartAsync(string? providerIssuerUrl)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

        // The stub serves plain http on loopback; the fetcher's insecure opt-in mirrors a dev deployment.
        var fetcher = new SourceDocumentFetcher(new HttpClient(), allowInsecureHttp: true, maxDocumentBytes: 32 * 1024);

        ProviderBroker? providers = null;
        if (providerIssuerUrl is not null)
        {
            Environment.SetEnvironmentVariable("PORTAL_TEST_SECRET", "portal-secret");
            providers = new ProviderBroker(
                new HttpClient(),
                [
                    new ConnectedProviderOptions
                    {
                        Name = "portal",
                        DisplayName = "Dev Portal",
                        Issuer = providerIssuerUrl,
                        ClientId = "portal-client-id",
                        ClientSecretRef = "env://PORTAL_TEST_SECRET",
                        Scopes = "openid profile",
                        CallbackUrl = "http://localhost/providers/portal/auth/callback",
                        Hosts = ["127.0.0.1"],
                    },
                ],
                new SecretResolverBuilder().AddEnvironment().Build());
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeSubAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeSubAuthHandler>(ScopeSubAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(
            management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly,
            sourceFetcher: fetcher, providerBroker: providers);
        await app.StartAsync();
        return new Scoped(app, app.GetTestClient());
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public async Task<string> BeginAsync(string subject)
        {
            HttpResponseMessage begun = await this.SendAsync(HttpMethod.Post, "/providers/portal/auth", "workspace:write", subject);
            begun.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await begun.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("state").GetString()!;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string scopes, string subject)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(ScopeSubAuthHandler.ScopeHeader, scopes);
            request.Headers.Add(ScopeSubAuthHandler.SubjectHeader, subject);
            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string scopes, string subject)
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add(ScopeSubAuthHandler.ScopeHeader, scopes);
            request.Headers.Add(ScopeSubAuthHandler.SubjectHeader, subject);
            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> SendAnonymousAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class ScopeSubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesSub";
        public const string ScopeHeader = "X-Scopes";
        public const string SubjectHeader = "X-Subject";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues scopes))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", scopes.ToString()));
            if (this.Request.Headers.TryGetValue(SubjectHeader, out Microsoft.Extensions.Primitives.StringValues subject))
            {
                identity.AddClaim(new Claim("sub", subject.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    /// <summary>A loopback stub OIDC provider: the discovery document, a standard token exchange
    /// (recording the exchange form for the standard-grant assertions), and a bearer-secured spec
    /// endpoint (recording the Authorization it saw).</summary>
    private sealed class StubProvider : IAsyncDisposable
    {
        private WebApplication? app;

        public string Url { get; private set; } = string.Empty;

        public Dictionary<string, string>? LastTokenForm { get; private set; }

        public string? LastSpecAuthorization { get; private set; }

        public static async Task<StubProvider> StartAsync()
        {
            var stub = new StubProvider();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");

            app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
            {
                issuer = stub.Url,
                authorization_endpoint = $"{stub.Url}/oauth/authorize",
                token_endpoint = $"{stub.Url}/oauth/token",
            }));

            app.MapPost("/oauth/token", async (HttpContext context) =>
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                stub.LastTokenForm = form.ToDictionary(f => f.Key, f => f.Value.ToString());
                if (form["client_id"] != "portal-client-id" || form["client_secret"] != "portal-secret")
                {
                    return Results.Json(new { error = "invalid_client" }, statusCode: 401);
                }

                if (form["grant_type"] != "authorization_code" || form["code"] != GoodCode || string.IsNullOrEmpty(form["redirect_uri"]))
                {
                    return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
                }

                return Results.Json(new
                {
                    access_token = StubToken,
                    token_type = "Bearer",
                    expires_in = 300,
                    refresh_token = "refresh-1",
                    refresh_expires_in = 1800,
                });
            });

            app.MapGet("/specs/petstore.json", (HttpContext context) =>
            {
                string authorization = context.Request.Headers.Authorization.ToString();
                stub.LastSpecAuthorization = authorization.Length == 0 ? null : authorization;
                return authorization == $"Bearer {StubToken}" || authorization == $"Bearer {OneShotSecret}"
                    ? Results.Text(PetstoreJson, "application/json")
                    : Results.Unauthorized();
            });

            await app.StartAsync();
            stub.app = app;
            stub.Url = app.Urls.First().TrimEnd('/');
            return stub;
        }

        public async ValueTask DisposeAsync()
        {
            if (this.app is not null)
            {
                await this.app.DisposeAsync();
            }
        }
    }
}