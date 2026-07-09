// <copyright file="LedgerPageToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// The opaque keyset continuation token for the ledger service's list endpoints: a base64url-encoded cursor payload
/// (a reconciliation cursor <c>ticks|runId</c>, or an account number) the client treats as opaque.
/// </summary>
public static class LedgerPageToken
{
    /// <summary>Encodes a cursor payload as an opaque, url-safe token.</summary>
    /// <param name="payload">The cursor payload.</param>
    /// <returns>An opaque token.</returns>
    public static string Encode(string payload) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));

    /// <summary>Decodes an opaque token back into its cursor payload.</summary>
    /// <param name="token">The token from a previous page, or <see langword="null"/>/empty for the first page.</param>
    /// <param name="payload">The decoded cursor payload.</param>
    /// <returns><see langword="true"/> if a payload was decoded; <see langword="false"/> for a first-page (absent) or malformed token.</returns>
    public static bool TryDecode(string? token, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
