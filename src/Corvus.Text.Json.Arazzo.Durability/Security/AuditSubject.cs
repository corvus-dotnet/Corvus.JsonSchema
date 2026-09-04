// <copyright file="AuditSubject.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The identity a governance-audit record names as its actor (ADR 0038): the principal's canonical subject and the
/// owner group (tenant) it acts in. One derivation for every surface, so a record joins to the grant or request it
/// concerns rather than to a display label.
/// </summary>
/// <param name="Subject">The canonical subject (see <see cref="ResolveSubject"/>); <see cref="Anonymous"/> when the request carried no principal.</param>
/// <param name="OwnerGroup">The owner group (tenant) the actor acts in, read from its own stamped internal tags; <see langword="null"/> when the deployment stamps none.</param>
/// <remarks>
/// <para>The subject is the claim a grant binding keys on (the deployment's configured subject claim, <c>sub</c> by
/// default), so a person is attributed exactly as authorization sees them. A client-credentials token that names no
/// subject is attributed to its client (the authorized party <c>azp</c>, then <c>client_id</c>), then to the
/// authentication name, and a request with no principal is <see cref="Anonymous"/>: the previous <c>system</c> and
/// <c>control-plane</c> literals named the deployment for actions nobody authenticated.</para>
/// <para>The conversions to and from <see cref="string"/> carry the subject alone, so a persisted <c>createdBy</c> or
/// <c>decidedBy</c> stamp and a caller that only has a name interoperate with the primitive without ceremony.</para>
/// </remarks>
public readonly record struct AuditSubject(string Subject, string? OwnerGroup)
{
    /// <summary>The subject recorded when the request carries no principal.</summary>
    public const string Anonymous = "anonymous";

    /// <summary>Converts a bare subject to an audit subject with no owner group.</summary>
    /// <param name="subject">The subject.</param>
    public static implicit operator AuditSubject(string subject) => new(subject, null);

    /// <summary>Converts an audit subject to its bare subject, for the persisted actor stamps.</summary>
    /// <param name="subject">The audit subject.</param>
    public static implicit operator string(AuditSubject subject) => subject.Subject;

    /// <summary>Resolves the canonical subject a principal's claims name it as.</summary>
    /// <param name="principal">The principal, or <see langword="null"/> when the request carries none.</param>
    /// <param name="subjectClaimType">The claim carrying the subject, for a deployment whose issuer does not use <c>sub</c>.</param>
    /// <returns>The subject claim, then the authorized party, then the client id, then the authentication name, then <see cref="Anonymous"/>.</returns>
    public static string ResolveSubject(ClaimsPrincipal? principal, string subjectClaimType = MachinePrincipal.DefaultSubjectClaimType)
    {
        if (principal is null)
        {
            return Anonymous;
        }

        string? resolved = principal.FindFirst(subjectClaimType)?.Value
            ?? principal.FindFirst(MachinePrincipal.AuthorizedPartyClaimType)?.Value
            ?? principal.FindFirst(MachinePrincipal.ClientIdClaimType)?.Value
            ?? principal.Identity?.Name;
        return string.IsNullOrEmpty(resolved) ? Anonymous : resolved;
    }

    /// <inheritdoc/>
    public override string ToString() => this.Subject;
}