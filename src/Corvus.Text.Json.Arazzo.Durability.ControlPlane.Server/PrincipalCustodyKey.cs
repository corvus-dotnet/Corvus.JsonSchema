// <copyright file="PrincipalCustodyKey.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Resolves the token-custody key for brokered provider sessions (workflow-designer design §4.7,
/// ADR 0052): the deployment-stamped identity dimensions first (stable across sessions), composed
/// with the authenticated subject; <see langword="null"/> when neither resolves — a provider
/// session cannot bind without a principal to bind to. Shared by the GitHub and connected-provider
/// surfaces so one signed-in principal keys one custody row per provider.
/// </summary>
internal static class PrincipalCustodyKey
{
    internal static string? Resolve(ControlPlaneAccess access, Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext, string subjectClaimType)
    {
        ClaimsPrincipal? principal = access.CurrentPrincipal ?? httpContext?.HttpContext?.User;
        string? subject = principal?.FindFirst(subjectClaimType)?.Value ?? principal?.Identity?.Name;
        IReadOnlyList<SecurityTag> tags = access.InternalTags();
        if (tags.Count == 0 && string.IsNullOrEmpty(subject))
        {
            return null;
        }

        var key = new System.Text.StringBuilder();
        foreach (SecurityTag tag in tags)
        {
            key.Append(tag.Key).Append('=').Append(tag.Value).Append(';');
        }

        if (!string.IsNullOrEmpty(subject))
        {
            key.Append("sub=").Append(subject);
        }

        return key.ToString();
    }
}