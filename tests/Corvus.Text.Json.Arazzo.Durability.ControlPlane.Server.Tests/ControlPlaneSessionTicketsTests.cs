// <copyright file="ControlPlaneSessionTicketsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Server-side session tickets (ADR 0075): signing out revokes the session wherever its cookie was copied, sign-out
/// everywhere ends every session a subject began, a session has a maximum lifetime, and the cache never holds a readable
/// token.
/// </summary>
[TestClass]
public sealed class ControlPlaneSessionTicketsTests
{
    private const string RefreshToken = "refresh-token-that-must-never-be-readable-in-the-cache";

    [TestMethod]
    public async Task A_copy_of_the_cookie_taken_before_sign_out_is_refused_after_it()
    {
        await using Host host = await Host.StartAsync(new ManualTime());
        string cookie = await host.SignInAsync("wanda");
        string copy = cookie;

        (await host.WhoAmIAsync(copy)).ShouldBe(HttpStatusCode.OK);
        await host.SignOutAsync(cookie);

        (await host.WhoAmIAsync(copy)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Sign_out_everywhere_ends_every_session_the_subject_began_and_no_one_elses()
    {
        var time = new ManualTime();
        await using Host host = await Host.StartAsync(time);
        string laptop = await host.SignInAsync("wanda");
        string phone = await host.SignInAsync("wanda");
        string erin = await host.SignInAsync("erin");

        time.Advance(TimeSpan.FromSeconds(1));
        await host.SignOutEverywhereAsync(laptop);

        (await host.WhoAmIAsync(laptop)).ShouldBe(HttpStatusCode.Unauthorized);
        (await host.WhoAmIAsync(phone)).ShouldBe(HttpStatusCode.Unauthorized);
        (await host.WhoAmIAsync(erin)).ShouldBe(HttpStatusCode.OK);

        // A session begun afterwards is unaffected.
        time.Advance(TimeSpan.FromSeconds(1));
        string again = await host.SignInAsync("wanda");
        (await host.WhoAmIAsync(again)).ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_session_in_steady_use_ends_at_the_maximum_lifetime()
    {
        var time = new ManualTime();
        await using Host host = await Host.StartAsync(time);
        string cookie = await host.SignInAsync("wanda");

        // Idle timeout 30 minutes, sliding; maximum lifetime 8 hours. A request every 20 minutes keeps it alive until then.
        for (int i = 0; i < 23; i++)
        {
            time.Advance(TimeSpan.FromMinutes(20));
            (await host.WhoAmIAsync(cookie)).ShouldBe(HttpStatusCode.OK, $"{(i + 1) * 20} minutes after sign-in");
            cookie = host.LatestCookie ?? cookie;
        }

        // Seven hours forty minutes in, a request 25 minutes later is inside the idle window but past eight hours.
        time.Advance(TimeSpan.FromMinutes(25));
        (await host.WhoAmIAsync(cookie)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task The_cookie_carries_only_a_key_and_the_cache_holds_no_readable_token()
    {
        await using Host host = await Host.StartAsync(new ManualTime());
        string cookie = await host.SignInAsync("wanda");

        // The cookie is small: a protected session key, not a ticket with claims and tokens.
        cookie.Length.ShouldBeLessThan(600);
        host.Cache.Entries.ShouldNotBeEmpty();
        foreach ((string key, byte[] value) in host.Cache.Entries)
        {
            Encoding.UTF8.GetString(value).ShouldNotContain(RefreshToken, customMessage: key);
            Encoding.UTF8.GetString(value).ShouldNotContain("wanda", customMessage: key);
        }
    }

    [TestMethod]
    public async Task Cache_entries_expire_with_the_session_and_a_revocation_lasts_the_maximum_lifetime()
    {
        var time = new ManualTime();
        await using Host host = await Host.StartAsync(time);
        DateTimeOffset signedIn = time.GetUtcNow();
        string cookie = await host.SignInAsync("wanda");

        DistributedCacheEntryOptions ticket = host.Cache.EntryOptions.Single(o => o.Key.StartsWith("arazzo:session:ticket:", StringComparison.Ordinal)).Value;
        ticket.AbsoluteExpiration.ShouldNotBeNull();
        ticket.AbsoluteExpiration.Value.ShouldBeLessThanOrEqualTo(signedIn + TimeSpan.FromHours(8));

        await host.SignOutEverywhereAsync(cookie);
        DistributedCacheEntryOptions epoch = host.Cache.EntryOptions.Single(o => o.Key.StartsWith("arazzo:session:epoch:", StringComparison.Ordinal)).Value;
        epoch.AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(8));
        host.Cache.EntryOptions.Keys.ShouldNotContain(k => k.Contains("wanda", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_stored_ticket_that_does_not_unprotect_is_refused_and_removed()
    {
        var cache = new RecordingCache();
        var tickets = new ControlPlaneSessionTickets(cache, new EphemeralDataProtectionProvider(), new ControlPlaneSessionTicketOptions());
        string key = await tickets.StoreAsync(Ticket("wanda"));

        // Another protector's output, as a cache shared with another deployment, or a rotated-out key ring, would give.
        var other = new ControlPlaneSessionTickets(cache, new EphemeralDataProtectionProvider(), new ControlPlaneSessionTicketOptions());

        (await other.RetrieveAsync(key)).ShouldBeNull();
        cache.Entries.Keys.ShouldNotContain(k => k.EndsWith(key, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_session_for_a_principal_with_no_subject_is_not_begun()
    {
        var tickets = new ControlPlaneSessionTickets(new RecordingCache(), new EphemeralDataProtectionProvider(), new ControlPlaneSessionTicketOptions());
        var anonymous = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "wanda")], "test")), "Cookies");

        await Should.ThrowAsync<InvalidOperationException>(() => tickets.StoreAsync(anonymous));
    }

    [TestMethod]
    public void A_maximum_lifetime_that_is_not_positive_is_refused()
        => Should.Throw<ArgumentOutOfRangeException>(() => new ControlPlaneSessionTickets(
            new RecordingCache(), new EphemeralDataProtectionProvider(), new ControlPlaneSessionTicketOptions { MaximumLifetime = TimeSpan.Zero }));

    [TestMethod]
    [DataRow(ClaimTypes.NameIdentifier)]
    [DataRow("sub")]
    public void The_subject_is_the_name_identifier_or_sub(string claimType)
        => ControlPlaneSessionTickets.SubjectOf(new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "user-1")], "test"))).ShouldBe("user-1");

    private static AuthenticationTicket Ticket(string subject)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "refresh_token", Value = RefreshToken }]);
        return new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject), new Claim(ClaimTypes.Name, subject)], "test")),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>A cookie-authenticated host whose tickets are kept by the store, with sign-in, sign-out and whoami.</summary>
    private sealed class Host : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient client;

        private Host(WebApplication app, RecordingCache cache)
        {
            this.app = app;
            this.Cache = cache;
            this.client = app.GetTestClient();
        }

        public RecordingCache Cache { get; }

        /// <summary>Gets the cookie the last response set, when a renewal replaced it.</summary>
        public string? LatestCookie { get; private set; }

        public static async Task<Host> StartAsync(TimeProvider time)
        {
            var cache = new RecordingCache();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IDistributedCache>(cache);
            builder.Services.AddSingleton(time);
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                    options.TimeProvider = time;
                    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                });
            builder.Services.AddArazzoControlPlaneSessionTickets(CookieAuthenticationDefaults.AuthenticationScheme);
            builder.Services.AddAuthorization();

            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapPost("/signin/{subject}", (HttpContext context, string subject) =>
            {
                AuthenticationTicket ticket = Ticket(subject);
                return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ticket.Principal, ticket.Properties);
            });
            app.MapPost("/signout", (HttpContext context) => context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)).RequireAuthorization();
            app.MapPost("/signout/everywhere", async (HttpContext context, IControlPlaneSessionRevocation revocation) =>
            {
                await revocation.RevokeAllAsync(ControlPlaneSessionTickets.SubjectOf(context.User)!);
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }).RequireAuthorization();
            app.MapGet("/whoami", (ClaimsPrincipal user) => user.Identity!.Name).RequireAuthorization();
            await app.StartAsync();
            return new Host(app, cache);
        }

        public async Task<string> SignInAsync(string subject)
        {
            using HttpResponseMessage response = await this.client.PostAsync($"/signin/{subject}", null);
            response.EnsureSuccessStatusCode();
            return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        }

        public async Task<HttpStatusCode> WhoAmIAsync(string cookie)
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Get, "/whoami", cookie);
            this.LatestCookie = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? set) ? set.Single().Split(';')[0] : null;
            return response.StatusCode;
        }

        public async Task SignOutAsync(string cookie)
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, "/signout", cookie);
            response.EnsureSuccessStatusCode();
        }

        public async Task SignOutEverywhereAsync(string cookie)
        {
            using HttpResponseMessage response = await this.SendAsync(HttpMethod.Post, "/signout/everywhere", cookie);
            response.EnsureSuccessStatusCode();
        }

        public async ValueTask DisposeAsync()
        {
            this.client.Dispose();
            await this.app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string cookie)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Cookie", cookie);
            return this.client.SendAsync(request);
        }
    }

    /// <summary>An in-memory distributed cache that records what was written and with which entry options.</summary>
    private sealed class RecordingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public Dictionary<string, byte[]> Entries { get; } = [];

        public Dictionary<string, DistributedCacheEntryOptions> EntryOptions { get; } = [];

        public byte[]? Get(string key) => this.inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => this.inner.GetAsync(key, token);

        public void Refresh(string key) => this.inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => this.inner.RefreshAsync(key, token);

        public void Remove(string key)
        {
            this.Entries.Remove(key);
            this.inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            this.Entries.Remove(key);
            return this.inner.RemoveAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            this.Entries[key] = value;
            this.EntryOptions[key] = options;
            this.inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            this.Entries[key] = value;
            this.EntryOptions[key] = options;

            // The in-memory cache measures an absolute expiry against the real clock; the tests keep their own time, so
            // the recorded options are asserted and the inner cache holds the entry without one.
            return this.inner.SetAsync(key, value, new DistributedCacheEntryOptions(), token);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}