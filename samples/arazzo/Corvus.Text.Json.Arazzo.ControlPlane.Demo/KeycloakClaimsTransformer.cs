// <copyright file="KeycloakClaimsTransformer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Microsoft.AspNetCore.Authentication;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// The demo's concrete §14.1 claim→capability mapping for Keycloak principals. The realm's group-membership
/// mapper emits a <c>groups</c> claim; this transformer turns those groups into the capability scopes they grant,
/// added as a <c>scope</c> claim that the control-plane scope policies read. This is the per-deployment mapping
/// §14.1 leaves open — a real deployment maps its own groups/roles, possibly from realm/client roles instead.
/// </summary>
/// <remarks>
/// No capability is ever ambient (§16.5.3): every group member — <em>including</em> <c>arazzo-admins</c> — gets only
/// the standing <em>read</em> scopes from membership. Elevated capability (run/admin/security write) arrives solely
/// through the access-request → approval flow (or, for an eligible principal, JIT self-elevation), so the effective
/// scopes are the read baseline <b>unioned with this principal's stored per-principal grants</b> — the §16.5.2
/// Decision-A entitlement resolution (<c>claims ∪ stored entitlements → effective scopes</c>), consulted here exactly
/// as the durable server consults it. DevApiKey principals carry no <c>groups</c> claim and already supply their own
/// <c>scope</c> claim, so this is a no-op for them.
/// </remarks>
public sealed class KeycloakClaimsTransformer(PersistentRowSecurityPolicy entitlements) : IClaimsTransformation
{
    private const string MappedMarker = "arazzo:scopes-mapped";

    private readonly PersistentRowSecurityPolicy entitlements = entitlements;

    private static readonly string[] DomainReadScopes =
    [
        ControlPlaneScopes.CatalogRead,
        ControlPlaneScopes.RunsRead,
        ControlPlaneScopes.CredentialsRead,
        ControlPlaneScopes.AdministratorsRead,
        ControlPlaneScopes.SecurityRead,
    ];

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Only OIDC principals carry `groups`; without it (e.g. a DevApiKey principal) there is nothing to map.
        var groups = principal.FindAll("groups").Select(static c => c.Value).ToHashSet(StringComparer.Ordinal);
        if (groups.Count == 0)
        {
            return Task.FromResult(principal);
        }

        // IClaimsTransformation can run more than once per request — don't stack duplicate scope claims.
        if (principal.HasClaim(static c => c.Type == MappedMarker))
        {
            return Task.FromResult(principal);
        }

        // The read baseline (membership) unioned with this principal's stored per-principal grants (the active,
        // time-boxed entitlements an approval/self-elevation wrote). No group — not even arazzo-admins — confers
        // standing elevated scopes; those are only ever held via a stored grant.
        var scopes = new HashSet<string>(DomainReadScopes, StringComparer.Ordinal);
        foreach (string granted in this.entitlements.ResolveGrantedScopes(principal))
        {
            scopes.Add(granted);
        }

        var mapped = new ClaimsIdentity();
        mapped.AddClaim(new Claim("scope", string.Join(' ', scopes)));
        mapped.AddClaim(new Claim(MappedMarker, "true"));
        principal.AddIdentity(mapped);
        return Task.FromResult(principal);
    }
}
