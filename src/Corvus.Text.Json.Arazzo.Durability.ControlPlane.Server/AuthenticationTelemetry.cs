// <copyright file="AuthenticationTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Authentication event telemetry (ADR 0071): every request's authentication is counted by scheme and outcome, and a
/// failure is a record in the audit chain, naming the scheme, the reason and the remote address and never any token
/// material. A host adds it with one registration,
/// <see cref="AuthenticationTelemetryExtensions.AddArazzoAuthenticationTelemetry"/>, and a secured control plane does not
/// start without it.
/// </summary>
/// <remarks>
/// <para>
/// It is one middleware that reads the request's authentication result, and not hooks on each scheme's events, so it
/// covers bearer, cookie, OpenID Connect and a handler of the host's own alike, a scheme added later included, and the
/// library takes no dependency on any of them. ASP.NET computes a scheme's result once for a request and hands the
/// same result to whoever asks again, so asking here costs the request nothing it was not going to pay, and where in the
/// pipeline this sits does not matter. It reads the default authentication scheme, which is the one the host's
/// authentication middleware reads; a scheme named only on an endpoint's authorization is not seen.
/// </para>
/// <para>
/// A request with no credential is not a failure: there is nothing to have guessed. It is counted as <c>none</c> and
/// recorded nowhere. The remote address is the connection's, so a host behind a proxy configures forwarded headers, or
/// every failure names the proxy.
/// </para>
/// </remarks>
public sealed class AuthenticationTelemetry
{
    private const string SchemeTag = "corvus.arazzo.auth.scheme";
    private const string ReasonTag = "corvus.arazzo.auth.reason";

    private GovernanceAuditor auditor = GovernanceAuditor.None;

    /// <summary>Binds the deployment's auditor, which the control plane does when it is mapped.</summary>
    /// <param name="deploymentAuditor">The deployment's governance auditor.</param>
    internal void Bind(GovernanceAuditor deploymentAuditor) => Volatile.Write(ref this.auditor, deploymentAuditor);

    /// <summary>Counts the request's authentication, and records it where it failed. It never fails the request.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the authentication is counted.</returns>
    internal async ValueTask ObserveAsync(HttpContext context)
    {
        IAuthenticationSchemeProvider schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        if (await schemes.GetDefaultAuthenticateSchemeAsync().ConfigureAwait(false) is not { } scheme)
        {
            return;
        }

        AuthenticateResult result = await context.AuthenticateAsync(scheme.Name).ConfigureAwait(false);
        if (result.Failure is not { } failure)
        {
            ArazzoTelemetry.Authentications.Add(1, new TagList { { SchemeTag, scheme.Name }, { ArazzoTelemetry.OutcomeTag, result.Succeeded ? "success" : "none" } });
            return;
        }

        string reason = Reason(failure);
        ArazzoTelemetry.Authentications.Add(1, new TagList { { SchemeTag, scheme.Name }, { ArazzoTelemetry.OutcomeTag, "failure" }, { ReasonTag, reason } });

        // A result that failed names a principal only where the token parsed far enough to have one. Nothing is read
        // from the token itself here: not the token, not a hash of it, not a fragment.
        ClaimsPrincipal? principal = result.Principal;
        await Volatile.Read(ref this.auditor).AuthenticationFailedAsync(
            scheme.Name,
            reason,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            principal is null ? null : AuditSubject.ResolveSubject(principal),
            principal?.FindFirst("iss")?.Value).ConfigureAwait(false);
    }

    // The reason is controlled vocabulary, taken from what kind of failure it was and never from its message, which can
    // quote the token. The kinds are the token validation library's, matched by name so that this library need not
    // reference it.
    private static string Reason(Exception failure)
        => failure.GetType().Name switch
        {
            "SecurityTokenExpiredException" => "expired",
            "SecurityTokenNotYetValidException" => "not-yet-valid",
            "SecurityTokenInvalidSignatureException" or "SecurityTokenSignatureKeyNotFoundException" => "invalid-signature",
            "SecurityTokenInvalidIssuerException" => "invalid-issuer",
            "SecurityTokenInvalidAudienceException" => "invalid-audience",
            "SecurityTokenInvalidLifetimeException" or "SecurityTokenNoExpirationException" => "invalid-lifetime",
            "SecurityTokenMalformedException" or "SecurityTokenReplayDetectedException" => "malformed",
            _ => "invalid",
        };

    /// <summary>Puts the telemetry at the start of the request pipeline, whatever the host builds after it.</summary>
    internal sealed class StartupFilter : IStartupFilter
    {
        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                AuthenticationTelemetry telemetry = app.ApplicationServices.GetRequiredService<AuthenticationTelemetry>();
                app.Use(async (context, following) =>
                {
                    await telemetry.ObserveAsync(context).ConfigureAwait(false);
                    await following(context).ConfigureAwait(false);
                });
                next(app);
            };
    }
}