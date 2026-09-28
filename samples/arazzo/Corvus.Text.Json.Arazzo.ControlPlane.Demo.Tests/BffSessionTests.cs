// <copyright file="BffSessionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo.Tests;

/// <summary>
/// The demo host's BFF session (GAP-2 of the 2026-08-07 security audit): the cookie's attributes, the idle and absolute
/// lifetimes, the local-only sign-in return, and which proxies may report TLS.
/// </summary>
[TestClass]
public sealed class BffSessionTests
{
    private static readonly BffSessionLifetime Lifetime = new() { IdleTimeout = TimeSpan.FromMinutes(30), AbsoluteLifetime = TimeSpan.FromHours(8) };

    [TestMethod]
    public async Task The_session_cookie_is_host_prefixed_secure_http_only_and_lax_even_over_plain_http()
    {
        await using Session session = await Session.StartAsync(new ManualTime());

        string setCookie = await session.SignInAsync();

        setCookie.ShouldStartWith(BffSession.CookieName + "=");
        string[] attributes = [.. setCookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant())];
        attributes.ShouldContain("secure");
        attributes.ShouldContain("httponly");
        attributes.ShouldContain("samesite=lax");
        attributes.ShouldContain("path=/");
        attributes.ShouldNotContain(a => a.StartsWith("domain=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_session_left_idle_past_the_idle_timeout_is_signed_out()
    {
        var time = new ManualTime();
        await using Session session = await Session.StartAsync(time);
        await session.SignInAsync();

        time.Advance(TimeSpan.FromMinutes(29));
        (await session.WhoAmIAsync()).ShouldBe(HttpStatusCode.OK);

        time.Advance(TimeSpan.FromMinutes(31));
        (await session.WhoAmIAsync()).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task A_session_in_steady_use_ends_at_its_absolute_lifetime()
    {
        var time = new ManualTime();
        await using Session session = await Session.StartAsync(time);
        await session.SignInAsync();

        // A request every 20 minutes keeps renewing the idle window, well past what the idle timeout alone would allow.
        for (TimeSpan elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromHours(7.5); elapsed += TimeSpan.FromMinutes(20))
        {
            time.Advance(TimeSpan.FromMinutes(20));
            (await session.WhoAmIAsync()).ShouldBe(HttpStatusCode.OK, $"{elapsed + TimeSpan.FromMinutes(20)} after sign-in");
        }

        // Seven hours forty minutes in, a request 25 minutes later is inside the idle window but past eight hours.
        time.Advance(TimeSpan.FromMinutes(25));
        (await session.WhoAmIAsync()).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    [DataRow("/", "/")]
    [DataRow("/ui/?tab=runs", "/ui/?tab=runs")]
    [DataRow("/designer", "/designer")]
    [DataRow(null, "/")]
    [DataRow("", "/")]
    [DataRow("https://attacker.example/", "/")]
    [DataRow("//attacker.example/", "/")]
    [DataRow("/\\attacker.example/", "/")]
    [DataRow("javascript:alert(1)", "/")]
    [DataRow("attacker.example", "/")]
    public void Sign_in_returns_only_to_a_path_on_this_host(string? requested, string expected)
        => BffSession.LocalReturnUrl(requested).ShouldBe(expected);

    [TestMethod]
    public async Task Only_a_trusted_proxy_can_say_a_request_arrived_over_tls()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KnownProxies:0"] = "10.0.0.5",
                ["KnownNetworks:0"] = "192.168.10.0/24",
            })
            .Build();

        await using WebApplication app = await StartAsync(builder =>
            builder.Services.Configure<ForwardedHeadersOptions>(options => BffSession.ConfigureForwardedHeaders(options, configuration)),
            app =>
            {
                app.UseForwardedHeaders();
                app.MapGet("/scheme", (HttpContext context) => context.Request.Scheme);
            });

        TestServer server = app.GetTestServer();
        (await SchemeFromAsync(server, IPAddress.Loopback)).ShouldBe("https");
        (await SchemeFromAsync(server, IPAddress.Parse("10.0.0.5"))).ShouldBe("https");
        (await SchemeFromAsync(server, IPAddress.Parse("192.168.10.77"))).ShouldBe("https");
        (await SchemeFromAsync(server, IPAddress.Parse("10.0.0.6"))).ShouldBe("http");
        (await SchemeFromAsync(server, IPAddress.Parse("203.0.113.9"))).ShouldBe("http");
    }

    private static async Task<string> SchemeFromAsync(TestServer server, IPAddress remote)
    {
        HttpContext context = await server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Scheme = "http";
            c.Request.Path = "/scheme";
            c.Connection.RemoteIpAddress = remote;
            c.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task<WebApplication> StartAsync(Action<WebApplicationBuilder> services, Action<WebApplication> pipeline)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        services(builder);
        WebApplication app = builder.Build();
        pipeline(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>A host with the demo's session cookie, a sign-in endpoint and an endpoint that needs the session.</summary>
    private sealed class Session : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient client;
        private string? cookie;

        private Session(WebApplication app)
        {
            this.app = app;
            this.client = app.GetTestClient();
        }

        public static async Task<Session> StartAsync(TimeProvider time)
        {
            WebApplication app = await BffSessionTests.StartAsync(
                builder =>
                {
                    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddCookie(options =>
                        {
                            BffSession.Configure(options, Lifetime);
                            options.TimeProvider = time;
                        });
                    builder.Services.AddAuthorization();
                },
                app =>
                {
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.MapPost("/signin", (HttpContext context) => context.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "wanda")], "test"))));
                    app.MapGet("/whoami", (ClaimsPrincipal user) => user.Identity!.Name).RequireAuthorization();
                });

            return new Session(app);
        }

        public async Task<string> SignInAsync()
        {
            using HttpResponseMessage response = await this.client.PostAsync("/signin", null);
            response.EnsureSuccessStatusCode();
            string setCookie = response.Headers.GetValues("Set-Cookie").Single();
            this.cookie = setCookie.Split(';')[0];
            return setCookie;
        }

        public async Task<HttpStatusCode> WhoAmIAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
            request.Headers.Add("Cookie", this.cookie);
            using HttpResponseMessage response = await this.client.SendAsync(request);

            // A renewal (or the sign-out that ends a session) replaces the cookie the browser holds.
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
            {
                this.cookie = setCookies.Single().Split(';')[0];
            }

            return response.StatusCode;
        }

        public async ValueTask DisposeAsync()
        {
            this.client.Dispose();
            await this.app.DisposeAsync();
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}