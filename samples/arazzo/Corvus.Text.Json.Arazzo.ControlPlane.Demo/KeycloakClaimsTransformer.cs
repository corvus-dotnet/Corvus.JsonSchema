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
/// Membership in <c>arazzo-admins</c> grants every scope (the §16.2 bootstrap system admin). A domain group
/// (e.g. <c>payments</c>) grants the read scopes only — elevated scopes (run/admin) arrive through the
/// access-request → approval flow (§16.5), not from mere membership. DevApiKey principals carry no <c>groups</c>
/// claim and already supply their own <c>scope</c> claim, so this is a no-op for them.
/// </remarks>
public sealed class KeycloakClaimsTransformer : IClaimsTransformation
{
    private const string AdminsGroup = "arazzo-admins";
    private const string MappedMarker = "arazzo:scopes-mapped";

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

        IReadOnlyList<string> scopes = groups.Contains(AdminsGroup) ? ControlPlaneScopes.All : DomainReadScopes;

        var mapped = new ClaimsIdentity();
        mapped.AddClaim(new Claim("scope", string.Join(' ', scopes)));
        mapped.AddClaim(new Claim(MappedMarker, "true"));
        principal.AddIdentity(mapped);
        return Task.FromResult(principal);
    }
}
