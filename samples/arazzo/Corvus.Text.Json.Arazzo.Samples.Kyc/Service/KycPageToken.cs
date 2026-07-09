// <copyright file="KycPageToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// The opaque keyset continuation token for <see cref="KycStore.ListVerificationsAsync"/>: the (SubmittedAt,
/// AccountId) of the last row on a page, base64url-encoded so the client treats it as opaque.
/// </summary>
public static class KycPageToken
{
    /// <summary>Encodes a cursor as an opaque token.</summary>
    /// <param name="submittedAt">The last row's <c>SubmittedAt</c>.</param>
    /// <param name="accountId">The last row's <c>AccountId</c>.</param>
    /// <returns>An opaque, url-safe token.</returns>
    public static string Encode(DateTimeOffset submittedAt, string accountId)
    {
        string payload = string.Concat(submittedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), "|", accountId);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>Decodes an opaque token back into a cursor.</summary>
    /// <param name="token">The token from a previous page, or <see langword="null"/>/empty for the first page.</param>
    /// <param name="submittedAt">The decoded <c>SubmittedAt</c> cursor.</param>
    /// <param name="accountId">The decoded <c>AccountId</c> cursor.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for a first-page (absent) or malformed token.</returns>
    public static bool TryDecode(string? token, out DateTimeOffset submittedAt, out string accountId)
    {
        submittedAt = default;
        accountId = string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            string payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
            int sep = payload.IndexOf('|', StringComparison.Ordinal);
            if (sep <= 0 || !long.TryParse(payload.AsSpan(0, sep), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
            {
                return false;
            }

            submittedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            accountId = payload[(sep + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
