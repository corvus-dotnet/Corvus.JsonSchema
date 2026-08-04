// <copyright file="MachinePrincipal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Resolves the machine principal a request is made as (design §16.4, ADR 0065 decision 2). A runner registers under
/// this identity, an administrator authorizes that identity, and the runner API resolves its bindings and its lease
/// ownership from it, so the three have to agree on what the identity is.
/// </summary>
/// <remarks>
/// <para>
/// It lives here, rather than at either surface, because the surfaces have to agree. Registration binding one value and
/// the runner API reading another does not fail loudly: the runner resolves to no environments and is offered nothing,
/// which is indistinguishable from a runner an administrator has not authorized yet. One definition removes the
/// possibility rather than documenting it.
/// </para>
/// <para>
/// A machine principal is a client rather than a person, so the client id is preferred: an OAuth2 client-credentials
/// token names it in <c>azp</c> (or <c>client_id</c>), while its <c>sub</c> is the issuer's service-account user id,
/// which is stable but names nothing an operator would recognise or type. A deployment identifying its runners by
/// certificate has no client claim at all, and falls through to the subject.
/// </para>
/// </remarks>
public static class MachinePrincipal
{
    /// <summary>The claim an OpenID Connect issuer names the authorized party in.</summary>
    public const string AuthorizedPartyClaimType = "azp";

    /// <summary>The claim an OAuth2 issuer names the client in, where it does not use <see cref="AuthorizedPartyClaimType"/>.</summary>
    public const string ClientIdClaimType = "client_id";

    /// <summary>The default claim carrying the token subject.</summary>
    public const string DefaultSubjectClaimType = "sub";

    /// <summary>Resolves which identity a principal's claims name it as.</summary>
    /// <param name="principal">The principal, or <see langword="null"/> when the request carries none.</param>
    /// <param name="subjectClaimType">The claim carrying the subject, for a deployment whose issuer does not use <c>sub</c>.</param>
    /// <returns>
    /// The identity, or <see langword="null"/> when nothing names one. There is no further fallback: a principal that
    /// cannot be established is refused, because guessing it would decide lease ownership and binding reach from
    /// whatever the token happened to carry.
    /// </returns>
    /// <remarks>
    /// Whether the request was authenticated at all is the caller's question, not this one's. A surface that admits only
    /// authenticated callers checks that at its door and is then asking a narrower question here: which of the claims it
    /// already trusts carries the identity.
    /// </remarks>
    public static string? Resolve(ClaimsPrincipal? principal, string subjectClaimType = DefaultSubjectClaimType)
    {
        if (principal is null)
        {
            return null;
        }

        string? resolved = principal.FindFirst(AuthorizedPartyClaimType)?.Value
            ?? principal.FindFirst(ClientIdClaimType)?.Value
            ?? principal.FindFirst(subjectClaimType)?.Value
            ?? principal.Identity?.Name;

        return string.IsNullOrEmpty(resolved) ? null : resolved;
    }
}