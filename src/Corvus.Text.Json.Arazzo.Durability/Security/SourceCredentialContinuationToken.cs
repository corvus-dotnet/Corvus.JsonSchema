// <copyright file="SourceCredentialContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="ISourceCredentialStore.ListAsync"/>
/// results (design §13). Stores page by the stable total order <c>(sourceName, environment, tie-breaker)</c> — the
/// contractual primary order is <c>(sourceName, environment)</c>; the tie-breaker (typically the tag discriminator)
/// makes it total so two bindings that share a source/environment page deterministically. The token carries the last
/// row's three key parts so the next request can seek past it; it is opaque and <b>backend-scoped</b> (a token is only
/// ever presented back to the store that issued it, so each backend may choose whatever tie-breaker it can order on).
/// </summary>
public static class SourceCredentialContinuationToken
{
    // The null control char cannot appear in a sourceName/environment/discriminator, so it separates the three key parts.
    private static readonly char Separator = (char)0;

    /// <summary>Encodes the last page row's key into a continuation token.</summary>
    /// <param name="sourceName">The last row's source name.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (e.g. the tag discriminator) — whatever the store orders on.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string sourceName, string environment, string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(tieBreaker);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Concat(sourceName, Separator.ToString(), environment, Separator.ToString(), tieBreaker)));
    }

    /// <summary>Decodes a continuation token back to the <c>(sourceName, environment, tie-breaker)</c> cursor to page after.</summary>
    /// <param name="token">A token from a previous page's <c>nextPageToken</c>, or <see langword="null"/>/empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(string? token, out (string SourceName, string Environment, string TieBreaker) cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException ex)
        {
            throw new FormatException($"'{token}' is not a valid credential page token.", ex);
        }

        string[] parts = decoded.Split(Separator);
        if (parts.Length != 3)
        {
            throw new FormatException($"'{token}' is not a valid credential page token.");
        }

        cursor = (parts[0], parts[1], parts[2]);
        return true;
    }
}