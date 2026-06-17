// <copyright file="ControlPlaneAntiForgery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// BFF anti-forgery for the control-plane API. When a deployment authenticates browsers with a session cookie
/// (the back-end-for-front-end pattern), that cookie is <em>ambient</em> — the browser attaches it to cross-site
/// requests — so cookie-authenticated state-changing calls need CSRF protection. This adds the token-less
/// "required header" defence (the same approach the Duende BFF framework ships): a request that carries the
/// session cookie, uses an unsafe method, and targets the API must present a fixed anti-forgery header. The
/// header's <em>presence</em> (not its value) forces a CORS preflight for any cross-origin caller; with no CORS
/// policy configured the preflight is denied, isolating the cookie-authenticated API to the same origin.
/// Bearer/mTLS callers carry no session cookie and are unaffected.
/// </summary>
/// <remarks>
/// Add it <strong>before</strong> authentication/authorization so a forged request is rejected up front (defence
/// in depth) rather than after the authorization layer happens to allow or deny it. This is intentionally not the
/// ASP.NET Core synchronizer-token antiforgery (which targets server-rendered forms); the required-header form is
/// the idiomatic choice for a JSON SPA behind a BFF and carries no token lifecycle to manage.
/// </remarks>
public static class ControlPlaneAntiForgery
{
    /// <summary>The default anti-forgery header name a deployment's SPA sends on every API call.</summary>
    public const string DefaultHeaderName = "X-CSRF";

    /// <summary>The default API path prefix the check guards.</summary>
    public const string DefaultApiPathPrefix = "/arazzo/v1";

    /// <summary>
    /// Adds the BFF anti-forgery check to the pipeline. A request under <paramref name="apiPathPrefix"/> that
    /// carries the <paramref name="sessionCookieName"/> cookie and uses an unsafe HTTP method (anything other
    /// than GET/HEAD/OPTIONS) must present the <paramref name="headerName"/> header, or it is rejected with
    /// <c>403 Forbidden</c>. Add it before <c>UseAuthentication</c>/<c>UseAuthorization</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="sessionCookieName">The name of the BFF session cookie the deployment issues (e.g. <c>arazzo.session</c>).</param>
    /// <param name="headerName">The required anti-forgery header (default <see cref="DefaultHeaderName"/>).</param>
    /// <param name="apiPathPrefix">The API base path the check guards (default <see cref="DefaultApiPathPrefix"/>).</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseArazzoControlPlaneAntiForgery(
        this IApplicationBuilder app,
        string sessionCookieName,
        string headerName = DefaultHeaderName,
        string apiPathPrefix = DefaultApiPathPrefix)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(sessionCookieName);
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        ArgumentException.ThrowIfNullOrEmpty(apiPathPrefix);

        return app.Use(async (context, next) =>
        {
            if (RequiresAntiForgery(context.Request, sessionCookieName, headerName, apiPathPrefix))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    $"Anti-forgery header '{headerName}' is required for cookie-authenticated state-changing requests.").ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Determines whether a request must carry the anti-forgery header — a cookie-authenticated, state-changing
    /// call to the guarded API path that does not already present it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="sessionCookieName">The session cookie name.</param>
    /// <param name="headerName">The anti-forgery header name.</param>
    /// <param name="apiPathPrefix">The guarded API path prefix.</param>
    /// <returns><see langword="true"/> if the request must be rejected for lacking the header.</returns>
    public static bool RequiresAntiForgery(HttpRequest request, string sessionCookieName, string headerName, string apiPathPrefix)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Path.StartsWithSegments(apiPathPrefix)
            && request.Cookies.ContainsKey(sessionCookieName)
            && !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !HttpMethods.IsOptions(request.Method)
            && !request.Headers.ContainsKey(headerName);
    }
}