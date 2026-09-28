// <copyright file="BffSession.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// The demo's BFF session: the cookie a browser holds after it signs in, how long it lasts, where sign-in may send the
/// browser back to, and which proxies may say the request arrived over TLS. ADR 0042 makes the session the host's, so
/// these are the demo host's choices, not the library's.
/// </summary>
public static class BffSession
{
    /// <summary>
    /// The session cookie's name. The <c>__Host-</c> prefix has the browser refuse the cookie unless it is
    /// <c>Secure</c>, set for the path <c>/</c>, and has no <c>Domain</c>, so neither a sibling subdomain nor a plain
    /// HTTP response can plant or overwrite it.
    /// </summary>
    public const string CookieName = "__Host-arazzo.session";

    /// <summary>The authentication property that records when the session began.</summary>
    public const string StartedKey = ".arazzo.session.started";

    /// <summary>
    /// Configures the session cookie: <c>Secure</c> whatever the request's scheme, <c>HttpOnly</c>,
    /// <c>SameSite=Lax</c>, and ended by whichever comes first, the idle timeout or the absolute lifetime.
    /// </summary>
    /// <remarks>
    /// The idle timeout is the cookie handler's sliding expiration. It alone would let a session in steady use last for
    /// ever, since each renewal issues a ticket with a fresh expiry, so the time the session began is stamped at sign-in,
    /// carried through every renewal, and checked on every request.
    /// </remarks>
    /// <param name="options">The cookie options to configure.</param>
    /// <param name="lifetime">The idle timeout and absolute lifetime.</param>
    public static void Configure(CookieAuthenticationOptions options, BffSessionLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lifetime);

        // The BFF holds the tokens; the SPA never sees them (it calls same-origin with this HttpOnly cookie).
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = lifetime.IdleTimeout;
        options.SlidingExpiration = true;

        options.Events.OnSigningIn = context =>
        {
            context.Properties.Items.TryAdd(StartedKey, Now(context.Options).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            // A ticket that does not say when its session began is refused rather than trusted to be young.
            if (!context.Properties.Items.TryGetValue(StartedKey, out string? started)
                || !DateTimeOffset.TryParseExact(started, "O", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset startedAt)
                || Now(context.Options) - startedAt >= lifetime.AbsoluteLifetime)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name).ConfigureAwait(false);
            }
        };

        // API calls must get 401/403 (the SPA redirects to /login), never a server-side HTML login redirect.
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    }

    /// <summary>
    /// Gets where sign-in returns the browser: the requested address when it is a path on this host, and <c>/</c>
    /// otherwise, so a link to <c>/login</c> cannot send a freshly signed-in user to another site.
    /// </summary>
    /// <param name="returnUrl">The requested return address.</param>
    /// <returns>A local path.</returns>
    public static string LocalReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && RedirectHttpResult.IsLocalUrl(returnUrl) ? returnUrl : "/";

    /// <summary>
    /// Configures which proxies the host believes about the client's address and the scheme a request arrived over.
    /// </summary>
    /// <remarks>
    /// Behind a TLS-terminating proxy the host sees plain HTTP. Believing the proxy's <c>X-Forwarded-Proto</c> is what
    /// has the OIDC handler build an <c>https</c> redirect URI and the security headers send HSTS. Believing it from
    /// anyone else would let any client claim TLS, so only loopback is trusted unless configuration names more: a list
    /// of addresses under <c>KnownProxies</c> and of CIDR ranges under <c>KnownNetworks</c>.
    /// </remarks>
    /// <param name="options">The forwarded-headers options to configure.</param>
    /// <param name="configuration">The configuration section naming the trusted proxies, or <see langword="null"/>.</param>
    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        if (configuration is null)
        {
            return;
        }

        foreach (IConfigurationSection proxy in configuration.GetSection("KnownProxies").GetChildren())
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy.Value!));
        }

        foreach (IConfigurationSection network in configuration.GetSection("KnownNetworks").GetChildren())
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network.Value!));
        }
    }

    private static DateTimeOffset Now(CookieAuthenticationOptions options) => (options.TimeProvider ?? TimeProvider.System).GetUtcNow();
}

/// <summary>How long a BFF session lasts.</summary>
public sealed class BffSessionLifetime
{
    /// <summary>Gets or sets how long a session survives without a request. Each request renews it.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets how long a session lasts from sign-in, however much it is used.</summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(8);
}