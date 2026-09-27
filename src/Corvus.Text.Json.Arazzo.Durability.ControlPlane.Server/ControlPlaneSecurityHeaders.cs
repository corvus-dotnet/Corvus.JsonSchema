// <copyright file="ControlPlaneSecurityHeaders.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Browser security headers for every response the host serves (ADR 0073): a Content-Security-Policy under which the
/// web kit runs with no inline script, a refusal to be framed, and the headers that stop content sniffing, referrer
/// leakage and cross-origin window access. A host adds them with one registration,
/// <see cref="ControlPlaneSecurityHeadersExtensions.AddArazzoSecurityHeaders"/>, and a secured control plane does not
/// start without it.
/// </summary>
/// <remarks>
/// <para>
/// The headers are written as the response starts, not when the request arrives, so they are present on whatever the
/// host sends, an error page re-executed by an exception handler included. A header the endpoint has already set is
/// left as it is, so a host can serve one page under a policy of its own.
/// </para>
/// <para>
/// <c>style-src</c> admits inline style until the kit's components carry their styles in constructable stylesheets
/// (ADR 0073, second piece). Script is <c>'self'</c> alone: no inline script, no <c>eval</c>.
/// </para>
/// </remarks>
public sealed class ControlPlaneSecurityHeaders
{
    /// <summary>The Content-Security-Policy sent when the host adds no sources.</summary>
    public const string DefaultContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; "
        + "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    private readonly Func<object, Task> applyCallback;
    private readonly string? strictTransportSecurity;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneSecurityHeaders"/> class. Every source is checked and the
    /// header values are built once, here.
    /// </summary>
    /// <param name="options">The host's additions.</param>
    public ControlPlaneSecurityHeaders(ControlPlaneSecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        StringBuilder policy = new("default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'");
        AppendSources(policy, options.ConnectSources, nameof(options.ConnectSources));
        policy.Append("; object-src 'none'; base-uri 'none'; form-action 'self'");
        AppendSources(policy, options.FormActionSources, nameof(options.FormActionSources));
        policy.Append("; frame-ancestors");
        if (options.FrameAncestors.Count == 0)
        {
            policy.Append(" 'none'");
        }
        else
        {
            AppendSources(policy, options.FrameAncestors, nameof(options.FrameAncestors));
        }

        this.ContentSecurityPolicy = policy.ToString();
        this.SendsFrameOptions = options.FrameAncestors.Count == 0;
        if (options.HstsMaxAge > TimeSpan.Zero)
        {
            this.strictTransportSecurity = string.Create(
                CultureInfo.InvariantCulture,
                $"max-age={(long)options.HstsMaxAge.TotalSeconds}{(options.HstsIncludeSubDomains ? "; includeSubDomains" : string.Empty)}");
        }

        this.applyCallback = state =>
        {
            this.Apply((HttpContext)state);
            return Task.CompletedTask;
        };
    }

    /// <summary>Gets the Content-Security-Policy this host sends.</summary>
    public string ContentSecurityPolicy { get; }

    /// <summary>Gets a value indicating whether <c>X-Frame-Options: DENY</c> is sent, which it is while no origin may frame the pages.</summary>
    public bool SendsFrameOptions { get; }

    /// <summary>Writes the headers into a response that is about to start.</summary>
    /// <param name="context">The request.</param>
    internal void Apply(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;
        SetIfAbsent(headers, HeaderNames.ContentSecurityPolicy, this.ContentSecurityPolicy);
        SetIfAbsent(headers, HeaderNames.XContentTypeOptions, "nosniff");
        SetIfAbsent(headers, "Referrer-Policy", "no-referrer");

        // The kit's connect popups close themselves through the opener reference, so the opener keeps its popups.
        SetIfAbsent(headers, "Cross-Origin-Opener-Policy", "same-origin-allow-popups");
        SetIfAbsent(headers, "Cross-Origin-Resource-Policy", "same-origin");
        if (this.SendsFrameOptions)
        {
            SetIfAbsent(headers, HeaderNames.XFrameOptions, "DENY");
        }

        if (this.strictTransportSecurity is not null && context.Request.IsHttps && !IsLoopback(context.Request.Host))
        {
            SetIfAbsent(headers, HeaderNames.StrictTransportSecurity, this.strictTransportSecurity);
        }
    }

    private static void SetIfAbsent(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }

    // A browser ignores Strict-Transport-Security from a loopback host, and pinning a developer's localhost to HTTPS
    // would outlive the deployment that sent it; ASP.NET's own HSTS middleware excludes the same hosts.
    private static bool IsLoopback(HostString host)
        => string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host.Host, out IPAddress? address) && IPAddress.IsLoopback(address));

    private static void AppendSources(StringBuilder policy, IList<string> sources, string optionName)
    {
        foreach (string source in sources)
        {
            policy.Append(' ').Append(Origin(source, optionName));
        }
    }

    // One origin and nothing else. A path, query, fragment or user information, a scheme other than http or https, or a
    // value that is not an absolute URI at all (a keyword such as 'unsafe-inline', a wildcard, a value carrying ';') is
    // refused, so configuration can add origins to the policy and never weaken or break out of it.
    private static string Origin(string source, string optionName)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.AbsolutePath != "/"
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || uri.UserInfo.Length != 0
            || source.AsSpan().ContainsAny(";, '\"*"))
        {
            ServerThrowHelper.ThrowInvalidSecurityHeaderSource(optionName, source);
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Puts the headers at the start of the request pipeline, whatever the host builds after it.</summary>
    internal sealed class StartupFilter : IStartupFilter
    {
        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                ControlPlaneSecurityHeaders headers = app.ApplicationServices.GetRequiredService<ControlPlaneSecurityHeaders>();
                app.Use((context, following) =>
                {
                    context.Response.OnStarting(headers.applyCallback, context);
                    return following(context);
                });
                next(app);
            };
    }
}