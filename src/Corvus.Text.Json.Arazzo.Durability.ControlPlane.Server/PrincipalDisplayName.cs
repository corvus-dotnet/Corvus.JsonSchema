// <copyright file="PrincipalDisplayName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Resolves the human-facing display name a request stamps as its <c>requesterLabel</c> so approver queues read as
/// people, not raw actor ids: the OIDC <c>name</c> claim when the IdP supplies one (probed both raw and through the
/// handler's inbound claim-type map), then <c>Identity.Name</c> — a host may pin <c>NameClaimType</c> to a
/// machine-matchable claim like <c>preferred_username</c> for grant keying, which makes it a last resort here, and a
/// bare service principal resolves nothing at all (the label is simply absent).
/// </summary>
internal static class PrincipalDisplayName
{
    internal static string? Resolve(ClaimsPrincipal? principal) =>
        principal?.FindFirst("name")?.Value
        ?? principal?.FindFirst(ClaimTypes.Name)?.Value
        ?? principal?.Identity?.Name;
}