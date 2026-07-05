// <copyright file="ControlPlaneGitHubApiTests.cs" company="Endjin Limited">
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
/// Tests the brokered GitHub API (workflow-designer design §4.7) against a loopback stub GitHub:
/// the user-to-server sign-in (single-use principal-bound state; the callback authenticates by
/// state, not bearer), per-principal token custody (one caller's session is unreachable from
/// another's), the session status projection, proxied contents reads, and the fails-closed posture
/// when a deployment brokers no App.
/// </summary>
[TestClass]
public sealed class ControlPlaneGitHubApiTests
{
    private const string GoodCode = "good-code";
    private const string StubToken = "user-to-server-token";

    [TestMethod]
    public async Task The_sign_in_flow_connects_a_principal_and_serves_status_and_contents()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        // Begin (authenticated): the authorize URL points at the configured GitHub with a state.
        HttpResponseMessage begun = await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", "ada");
        begun.StatusCode.ShouldBe(HttpStatusCode.OK);
        string state;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await begun.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("authorizeUrl").GetString()!.ShouldStartWith($"{github.Url}/login/oauth/authorize");
            state = doc.RootElement.GetProperty("state").GetString()!;
        }

        // The callback is a top-level navigation: NO bearer — the single-use state IS the authentication.
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Status: the signed-in identity, installations, and the user ∩ installation repositories.
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeTrue();
            session.RootElement.GetProperty("login").GetString().ShouldBe("octo");
            Stj.JsonElement installation = session.RootElement.GetProperty("installations")[0];
            installation.GetProperty("account").GetString().ShouldBe("acme-org");
            installation.GetProperty("repositories")[0].GetProperty("fullName").GetString().ShouldBe("acme-org/specs");
        }

        // Browse: a directory lists entries; a file carries base64 content.
        using (Stj.JsonDocument dir = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents", "workspace:read", "ada")))
        {
            dir.RootElement.GetProperty("kind").GetString().ShouldBe("dir");
            dir.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ShouldBe(["petstore.json", "flows"]);
        }

        using (Stj.JsonDocument file = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents?path=petstore.json", "workspace:read", "ada")))
        {
            file.RootElement.GetProperty("kind").GetString().ShouldBe("file");
            string content = file.RootElement.GetProperty("file").GetProperty("content").GetString()!;
            Encoding.UTF8.GetString(Convert.FromBase64String(content)).ShouldContain("openapi");
        }

        // Disconnect: idempotent; the session reads disconnected afterwards.
        (await host.SendAsync(HttpMethod.Delete, "/github/session", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task Token_custody_is_per_principal_and_the_state_is_single_use()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // ada is connected; bob is NOT — custody keys by principal, so ada's session is unreachable.
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "bob")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }

        (await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents", "workspace:read", "bob"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The state is single-use: replaying the callback refuses; garbage refuses.
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAnonymousAsync("/github/auth/callback?code=x&state=nonsense"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A refused exchange (bad code) does not connect the principal.
        string second = await host.BeginAsync("bob");
        (await host.SendAnonymousAsync($"/github/auth/callback?code=wrong&state={Uri.EscapeDataString(second)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "bob")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task A_deployment_without_a_broker_fails_closed_and_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync(gitHubUrl: null);

        // No broker wired: every operation refuses.
        (await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Scope gating: beginning the sign-in needs workspace:write.
        (await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:read", "ada")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Scoped> StartAsync(string? gitHubUrl)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

        GitHubBroker? broker = null;
        if (gitHubUrl is not null)
        {
            Environment.SetEnvironmentVariable("GITHUB_TEST_APP_SECRET", "app-secret");
            broker = new GitHubBroker(
                new HttpClient(),
                new GitHubBrokerOptions
                {
                    BaseUrl = gitHubUrl,
                    ApiBaseUrl = gitHubUrl,
                    ClientId = "app-client-id",
                    ClientSecretRef = "env://GITHUB_TEST_APP_SECRET",
                    CallbackUrl = "http://localhost/github/auth/callback",
                },
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
            gitHubBroker: broker);
        await app.StartAsync();
        return new Scoped(app, app.GetTestClient());
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public async Task<string> BeginAsync(string subject)
        {
            HttpResponseMessage begun = await this.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", subject);
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
        public const string SubjectHeader = "X-Sub";

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

    /// <summary>A loopback stub GitHub: the token exchange plus the user/installations/contents reads the broker proxies.</summary>
    private sealed class StubGitHub : IAsyncDisposable
    {
        private WebApplication? app;

        public string Url { get; private set; } = string.Empty;

        public static async Task<StubGitHub> StartAsync()
        {
            var stub = new StubGitHub();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");

            app.MapPost("/login/oauth/access_token", async (HttpContext context) =>
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                if (form["client_id"] != "app-client-id" || form["client_secret"] != "app-secret")
                {
                    return Results.Json(new { error = "incorrect_client_credentials" });
                }

                if (form["code"] == GoodCode || form["refresh_token"] == "refresh-1")
                {
                    return Results.Json(new
                    {
                        access_token = StubToken,
                        expires_in = 28800,
                        refresh_token = "refresh-1",
                        refresh_token_expires_in = 15897600,
                        token_type = "bearer",
                    });
                }

                return Results.Json(new { error = "bad_verification_code" });
            });

            app.MapGet("/user", (HttpContext context) => Authorized(context)
                ? Results.Json(new { login = "octo", name = "Octo Cat", avatar_url = "https://example.test/octo.png" })
                : Results.Unauthorized());

            app.MapGet("/user/installations", (HttpContext context) => Authorized(context)
                ? Results.Json(new { installations = new[] { new { id = 7, account = new { login = "acme-org" } } } })
                : Results.Unauthorized());

            app.MapGet("/user/installations/7/repositories", (HttpContext context) => Authorized(context)
                ? Results.Json(new { repositories = new[] { new { name = "specs", full_name = "acme-org/specs", owner = new { login = "acme-org" }, default_branch = "main", @private = true } } })
                : Results.Unauthorized());

            app.MapGet("/repos/acme-org/specs/contents/{*path}", (HttpContext context, string? path) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                if (string.IsNullOrEmpty(path))
                {
                    return Results.Json(new object[]
                    {
                        new { name = "petstore.json", path = "petstore.json", type = "file", size = 123, sha = "abc123" },
                        new { name = "flows", path = "flows", type = "dir", sha = "def456" },
                    });
                }

                if (path == "petstore.json")
                {
                    return Results.Json(new
                    {
                        name = "petstore.json",
                        path = "petstore.json",
                        sha = "abc123",
                        size = 123,
                        encoding = "base64",
                        content = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"openapi":"3.1.0"}""")),
                    });
                }

                return Results.NotFound();
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

        private static bool Authorized(HttpContext context)
            => context.Request.Headers.Authorization.ToString() == $"Bearer {StubToken}";
    }
}