// <copyright file="RunnerLeaseToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The value of the <c>X-Arazzo-Lease</c> header: the grant's epoch and the store's own lease token, joined by a full
/// stop. A runner presents it on every operation over a run and never interprets it.
/// </summary>
/// <remarks>
/// <para>
/// The store token is the part that authorises: it is minted unguessably by the backend and the store matches it (with
/// the owner, which is the authenticated principal and never a request value) before it extends or releases anything.
/// Carrying it out and back is what lets the server reconstruct the caller's <see cref="WorkflowLease"/> without keeping
/// per-request state, which the runner API needs because it is deployed as several instances and a lease outlives the
/// instance that granted it.
/// </para>
/// <para>
/// The epoch rides along because the contract's renewal must report the epoch of the grant being extended, and a grant
/// mints one epoch rather than one per extension. <strong>In phase A the epoch is carried, not enforced</strong>: nothing
/// reads it back, so a runner rewriting its own copy gains nothing. ADR 0065 decision 6 makes it a fence in phase B, when
/// a checkpoint's epoch is compared against the grant's — which requires the grant's epoch to be persisted with the lease
/// row and paired with the store incarnation, neither of which exists yet.
/// </para>
/// </remarks>
public static class RunnerLeaseToken
{
    private const char Separator = '.';

    /// <summary>Issues the header value for a granted lease.</summary>
    /// <param name="epoch">The grant's epoch.</param>
    /// <param name="storeToken">The store's lease token.</param>
    /// <returns>The header value.</returns>
    public static string Issue(long epoch, string storeToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(storeToken);

        // Invariant, because the value is parsed back by TryParse and compared against a canonical rendering: a culture
        // that formats the digits or the sign differently would issue a token its own server could not read.
        return string.Create(CultureInfo.InvariantCulture, $"{epoch}{Separator}{storeToken}");
    }

    /// <summary>Parses a presented header value.</summary>
    /// <param name="token">The presented value.</param>
    /// <param name="epoch">The grant's epoch.</param>
    /// <param name="storeToken">The store's lease token.</param>
    /// <returns><see langword="true"/> if the value is well formed.</returns>
    public static bool TryParse(string? token, out long epoch, out string storeToken)
    {
        epoch = 0;
        storeToken = string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        // The first separator, not the last: a backend's token is opaque and may itself contain one, while the epoch
        // never does.
        int separator = token.IndexOf(Separator, StringComparison.Ordinal);
        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        // A bare non-negative decimal, and canonical (no sign, whitespace, or leading zeros), so one grant has exactly
        // one header value rather than a family of padded variants.
        ReadOnlySpan<char> epochSegment = token.AsSpan(0, separator);
        if (!long.TryParse(epochSegment, NumberStyles.None, CultureInfo.InvariantCulture, out epoch)
            || !epochSegment.SequenceEqual(epoch.ToString(CultureInfo.InvariantCulture)))
        {
            epoch = 0;
            return false;
        }

        storeToken = token[(separator + 1)..];
        return true;
    }
}