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
    /// <para>
    /// A scope claim may carry one scope or several space-delimited ones, and an issuer may emit several such claims,
    /// so every claim of the type is searched. Matching is ordinal: a scope is an identifier the deployment and its
    /// issuer agree on exactly, not text to be compared leniently.
    /// </para>
    /// <para>
    /// It walks the value as spans rather than splitting it, and walks the identities rather than the flattened
    /// <see cref="ClaimsPrincipal.Claims"/>, because this runs inside an authorization policy and therefore on every
    /// request to every scoped endpoint — including the runner API's claim, lease, and checkpoint operations, which are
    /// the execution path rather than the governance one. Splitting cost a string array and a string per scope for an
    /// answer that is a boolean. Measured over a representative three-scope claim: 344 bytes per call before, 80 after.
    /// The remainder is the claim enumerators themselves, which cannot be avoided without reaching past the public API.
    /// </para>
    /// </remarks>
    public static bool Has(ClaimsPrincipal? user, string scopeClaimType, string scope)
    {
        if (user is null)
        {
            return false;
        }

        foreach (ClaimsIdentity identity in user.Identities)
        {
            foreach (Claim claim in identity.Claims)
            {
                if (!string.Equals(claim.Type, scopeClaimType, StringComparison.Ordinal))
                {
                    continue;
                }

                ReadOnlySpan<char> remaining = claim.Value.AsSpan();
                while (!remaining.IsEmpty)
                {
                    int space = remaining.IndexOf(' ');
                    ReadOnlySpan<char> part = space < 0 ? remaining : remaining[..space];
                    remaining = space < 0 ? default : remaining[(space + 1)..];

                    // Trimmed to match the split's TrimEntries, so an issuer padding its delimiters is read the same way.
                    if (part.Trim().SequenceEqual(scope))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}