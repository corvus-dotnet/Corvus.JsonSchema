// <copyright file="AuthenticationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// Unit tests for the CLI's authentication: the token cache, the token-resolution order, and the OAuth2
/// device-authorization and refresh flows driven against an in-process mock OIDC provider over loopback HTTP.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class AuthenticationTests
{
    private const string TokenFileEnv = "ARAZZO_RUNS_TOKEN_FILE";
    private const string TokenEnv = "ARAZZO_RUNS_TOKEN";

    [TestMethod]
    public void TokenCache_round_trips_and_clears()
    {
        using var cacheFile = new TempEnv(TokenFileEnv);
        var token = new TokenSet("at", "rt", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), "https://issuer.example", "client-1", "openid offline_access");

        TokenCache.Save(token);

        TokenSet? loaded = TokenCache.Load();
        loaded.ShouldNotBeNull();
        loaded.AccessToken.ShouldBe("at");
        loaded.RefreshToken.ShouldBe("rt");
        loaded.Authority.ShouldBe("https://issuer.example");
        loaded.ClientId.ShouldBe("client-1");
        loaded.ExpiresAtUtc.ShouldBe(token.ExpiresAtUtc);

        TokenCache.Clear();
        TokenCache.Load().ShouldBeNull();
    }

    [TestMethod]
    public async Task Resolve_prefers_the_explicit_token()
    {
        using var cacheFile = new TempEnv(TokenFileEnv);
        using var tokenEnv = new TempEnv(TokenEnv, "env-token");

        (await TokenSource.ResolveAsync("explicit-token", default)).ShouldBe("explicit-token");
    }

    [TestMethod]
    public async Task Resolve_uses_the_environment_when_no_explicit_token()
    {
        using var cacheFile = new TempEnv(TokenFileEnv); // points at a nonexistent file -> no cache
        using var tokenEnv = new TempEnv(TokenEnv, "env-token");

        (await TokenSource.ResolveAsync(null, default)).ShouldBe("env-token");
    }

    [TestMethod]
    public async Task Resolve_uses_a_valid_cached_token()
    {
        using var cacheFile = new TempEnv(TokenFileEnv);
        using var tokenEnv = new TempEnv(TokenEnv, null);
        TokenCache.Save(new TokenSet("cached-access", "cached-refresh", DateTimeOffset.UtcNow.AddHours(1), "https://issuer.example", "client-1", "openid"));

        (await TokenSource.ResolveAsync(null, default)).ShouldBe("cached-access");
    }

    [TestMethod]
    public async Task Resolve_refreshes_an_expired_cached_token()
    {
        await using MockOidcServer idp = await MockOidcServer.StartAsync();
        using var cacheFile = new TempEnv(TokenFileEnv);
        using var tokenEnv = new TempEnv(TokenEnv, null);

        // Expired access token but a refresh token and the (loopback) authority of our mock IdP.
        TokenCache.Save(new TokenSet("stale-access", "the-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-5), idp.Url, "client-1", "openid offline_access"));

        string? resolved = await TokenSource.ResolveAsync(null, default);

        resolved.ShouldBe("refreshed-access-token");
        TokenCache.Load()!.AccessToken.ShouldBe("refreshed-access-token");
        idp.RefreshTokenGrants.ShouldBe(1);
    }

    [TestMethod]
    public async Task Device_code_flow_polls_until_authorized()
    {
        await using MockOidcServer idp = await MockOidcServer.StartAsync();
        var config = new OAuthConfig(idp.Url, "client-1", "openid offline_access");

        TokenSet token = await OAuthFlows.LoginAsync(config, useDeviceCode: true, default);

        token.AccessToken.ShouldBe("device-access-token");
        token.RefreshToken.ShouldBe("device-refresh-token");
        token.Authority.ShouldBe(idp.Url);
        idp.DeviceTokenGrants.ShouldBeGreaterThanOrEqualTo(2); // at least one authorization_pending, then success
    }

    [TestMethod]
    public async Task Device_code_flow_binds_a_pkce_s256_verifier()
    {
        await using MockOidcServer idp = await MockOidcServer.StartAsync();
        var config = new OAuthConfig(idp.Url, "client-1", "openid");

        await OAuthFlows.LoginAsync(config, useDeviceCode: true, default);

        // The device-authorization request advertises an S256 challenge (providers such as Keycloak enforce PKCE)...
        idp.DeviceCodeChallengeMethod.ShouldBe("S256");
        idp.DeviceCodeChallenge.ShouldNotBeNullOrEmpty();

        // ...and the token request presents a verifier whose S256 hash is exactly that challenge (RFC 7636 §4).
        idp.DeviceCodeVerifier.ShouldNotBeNullOrEmpty();
        string expectedChallenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(idp.DeviceCodeVerifier!)));
        idp.DeviceCodeChallenge.ShouldBe(expectedChallenge);
    }

    [TestMethod]
    public async Task Refresh_exchanges_the_refresh_token()
    {
        await using MockOidcServer idp = await MockOidcServer.StartAsync();
        var existing = new TokenSet("old-access", "the-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-1), idp.Url, "client-1", "openid offline_access");

        TokenSet? refreshed = await OAuthFlows.RefreshAsync(existing, default);

        refreshed.ShouldNotBeNull();
        refreshed.AccessToken.ShouldBe("refreshed-access-token");
        idp.RefreshTokenGrants.ShouldBe(1);
    }

    /// <summary>Sets an environment variable for the test's lifetime (a fresh temp path for the token file when no value is given), restoring it on dispose.</summary>
    private sealed class TempEnv : IDisposable
    {
        private readonly string name;
        private readonly string? previous;
        private readonly string? tempFile;

        public TempEnv(string name, string? value)
        {
            this.name = name;
            this.previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public TempEnv(string name)
        {
            this.name = name;
            this.previous = Environment.GetEnvironmentVariable(name);
            this.tempFile = Path.Combine(Path.GetTempPath(), $"arazzo-runs-token-{Guid.NewGuid():N}.json");
            Environment.SetEnvironmentVariable(name, this.tempFile);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(this.name, this.previous);
            if (this.tempFile is not null && File.Exists(this.tempFile))
            {
                File.Delete(this.tempFile);
            }
        }
    }

    /// <summary>A minimal in-process OIDC provider for the device-code and refresh flows over loopback HTTP.</summary>
    private sealed class MockOidcServer : IAsyncDisposable
    {
        private WebApplication app = null!;
        private int deviceTokenGrants;
        private int refreshTokenGrants;
        private string? deviceCodeChallenge;
        private string? deviceCodeChallengeMethod;
        private string? deviceCodeVerifier;

        public string Url { get; private set; } = string.Empty;

        public int DeviceTokenGrants => Volatile.Read(ref this.deviceTokenGrants);

        public int RefreshTokenGrants => Volatile.Read(ref this.refreshTokenGrants);

        public string? DeviceCodeChallenge => Volatile.Read(ref this.deviceCodeChallenge);

        public string? DeviceCodeChallengeMethod => Volatile.Read(ref this.deviceCodeChallengeMethod);

        public string? DeviceCodeVerifier => Volatile.Read(ref this.deviceCodeVerifier);

        public static async Task<MockOidcServer> StartAsync()
        {
            var server = new MockOidcServer();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            server.Map(app);
            await app.StartAsync();
            server.app = app;
            server.Url = app.Urls.First();
            return server;
        }

        public async ValueTask DisposeAsync() => await this.app.DisposeAsync();

        private static string BaseUrl(HttpContext ctx) => $"{ctx.Request.Scheme}://{ctx.Request.Host}";

        private static string TokenJson(string accessToken, string refreshToken)
            => $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":3600,"refresh_token":"{{refreshToken}}","scope":"openid offline_access"}""";

        private void Map(WebApplication app)
        {
            app.MapGet("/.well-known/openid-configuration", (HttpContext ctx) =>
            {
                string b = BaseUrl(ctx);
                return Results.Content(
                    $$"""{"issuer":"{{b}}","authorization_endpoint":"{{b}}/connect/authorize","token_endpoint":"{{b}}/connect/token","device_authorization_endpoint":"{{b}}/connect/deviceauthorization","jwks_uri":"{{b}}/jwks"}""",
                    "application/json");
            });

            app.MapGet("/jwks", () => Results.Content("""{"keys":[]}""", "application/json"));

            app.MapPost("/connect/deviceauthorization", async (HttpContext ctx) =>
            {
                IFormCollection form = await ctx.Request.ReadFormAsync();
                Volatile.Write(ref this.deviceCodeChallenge, form["code_challenge"].ToString());
                Volatile.Write(ref this.deviceCodeChallengeMethod, form["code_challenge_method"].ToString());
                string b = BaseUrl(ctx);
                return Results.Content(
                    $$"""{"device_code":"dev-code","user_code":"WXYZ-1234","verification_uri":"{{b}}/device","interval":1,"expires_in":300}""",
                    "application/json");
            });

            app.MapPost("/connect/token", async (HttpContext ctx) =>
            {
                IFormCollection form = await ctx.Request.ReadFormAsync();
                string grant = form["grant_type"].ToString();

                if (grant == "urn:ietf:params:oauth:grant-type:device_code")
                {
                    Volatile.Write(ref this.deviceCodeVerifier, form["code_verifier"].ToString());

                    // Pend once, then authorize — exercises the polling loop.
                    return Interlocked.Increment(ref this.deviceTokenGrants) == 1
                        ? Results.Json(new { error = "authorization_pending" }, statusCode: 400)
                        : Results.Content(TokenJson("device-access-token", "device-refresh-token"), "application/json");
                }

                if (grant == "refresh_token")
                {
                    Interlocked.Increment(ref this.refreshTokenGrants);
                    return Results.Content(TokenJson("refreshed-access-token", "refreshed-refresh-token"), "application/json");
                }

                return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);
            });
        }
    }
}