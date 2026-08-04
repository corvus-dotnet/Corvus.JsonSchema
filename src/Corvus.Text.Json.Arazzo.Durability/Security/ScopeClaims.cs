// <copyright file="ScopeClaims.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Reads the capability scopes a principal was granted. Both the control-plane API and the runner API gate on scopes,
/// and an issuer packs them the same way for both, so how a scope claim is read is defined once here.
/// </summary>
public static class ScopeClaims
{
    /// <summary>The claim type an OAuth2 issuer carries granted scopes in.</summary>
    public const string DefaultScopeClaimType = "scope";

    /// <summary>Determines whether a principal was granted a scope.</summary>
    /// <param name="user">The authenticated principal.</param>
    /// <param name="scopeClaimType">The claim type carrying granted scopes.</param>
    /// <param name="scope">The scope to look for.</param>
    /// <returns><see langword="true"/> when the principal carries the scope.</returns>
    /// <remarks>
    /// A scope claim may carry one scope or several space-delimited ones, and an issuer may emit several such claims,
    /// so every claim of the type is split and searched. Matching is ordinal: a scope is an identifier the deployment
    /// and its issuer agree on exactly, not text to be compared leniently.
    /// </remarks>
    public static bool Has(ClaimsPrincipal? user, string scopeClaimType, string scope)
    {
        if (user is null)
        {
            return false;
        }

        foreach (Claim claim in user.Claims)
        {
            if (!string.Equals(claim.Type, scopeClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string part in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(part, scope, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}