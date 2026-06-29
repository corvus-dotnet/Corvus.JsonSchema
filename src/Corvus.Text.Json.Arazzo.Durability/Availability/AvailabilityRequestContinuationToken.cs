// <copyright file="AvailabilityRequestContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page
/// <see cref="IAvailabilityRequestStore.ListAsync(AvailabilityRequestQuery, int, JsonString, System.Threading.CancellationToken)"/>
/// results (design §7.8). The inbox pages oldest-first by <c>(createdAt, id)</c> — ascending creation instant with the
/// unique <c>id</c> as the tie-breaker — so the token carries the last row's <c>(createdAt, id)</c> for the next request to
/// seek strictly past. It is opaque and <b>backend-scoped</b> (only ever presented back to the store that issued it).
/// Mirrors <see cref="Security.AccessRequestContinuationToken"/>.
/// </summary>
/// <remarks>
/// Fully bytes-native: the token is <c>base64url(utcTicks \0 idUtf8)</c> with the instant written as its UTC tick count
/// (a decimal <see cref="long"/> via <see cref="Utf8Formatter"/>/<see cref="Utf8Parser"/> — an exact, offset-independent
/// instant that orders identically to <see cref="System.DateTimeOffset.CompareTo"/>). <see cref="EncodeToUtf8"/> assembles
/// the key into a pooled scratch then Base64URL-encodes into the caller's buffer; <see cref="TryDecode"/> Base64URL-decodes
/// into the caller's buffer and returns the id as a span into it — no managed string, no intermediate <c>byte[]</c>.
/// </remarks>
public static class AvailabilityRequestContinuationToken
{
    // The null control byte cannot appear in the decimal tick count or the id, so it separates the two key parts.
    private const byte Separator = 0;

    // A signed 64-bit integer is at most 20 ASCII characters ("-9223372036854775808"); ids are short, so the assemble
    // scratch fits the stack in the common case, with the ArrayPool fallback for the rare long id.
    private const int MaxTicksDigits = 20;
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for an id of
    /// <paramref name="idUtf8Length"/> bytes (the tick count contributes a fixed maximum width) — safe to size a buffer with.</summary>
    /// <param name="idUtf8Length">The id's UTF-8 byte length.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(int idUtf8Length) => Base64Url.GetEncodedLength(MaxTicksDigits + 1 + idUtf8Length);

    /// <summary>Writes the continuation token (<c>base64url</c> of <c>utcTicks \0 id</c>) for the last row's
    /// <c>(createdAt, id)</c> into <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="utcTicks">The last row's creation instant as UTC ticks.</param>
    /// <param name="idUtf8">The last row's id as UTF-8.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(long utcTicks, ReadOnlySpan<byte> idUtf8, Span<byte> destination)
    {
        int rawMax = MaxTicksDigits + 1 + idUtf8.Length;
        byte[]? rented = rawMax > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawMax) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (!Utf8Formatter.TryFormat(utcTicks, raw, out int pos))
            {
                throw new FormatException("The availability-request instant could not be formatted into the page token.");
            }

            raw[pos++] = Separator;
            idUtf8.CopyTo(raw[pos..]);
            pos += idUtf8.Length;
            Base64Url.EncodeToUtf8(raw[..pos], destination, out _, out int written);
            return written;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Gets an upper bound, in bytes, on the decoded key <see cref="TryDecode"/> writes for a token of
    /// <paramref name="tokenUtf8Length"/> bytes — safe to size the decode buffer with.</summary>
    /// <param name="tokenUtf8Length">The token's UTF-8 byte length.</param>
    /// <returns>An upper bound on the decoded key length in bytes.</returns>
    public static int GetMaxDecodedLength(int tokenUtf8Length) => Base64Url.GetMaxDecodedLength(tokenUtf8Length);

    /// <summary>Decodes a continuation token's UTF-8 into <paramref name="destination"/> (size it with
    /// <see cref="GetMaxDecodedLength"/>), yielding the <c>(createdAt, id)</c> to page strictly after — the id stays a span
    /// into the caller's buffer, never a managed string.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="destination">The buffer the decoded key is written into.</param>
    /// <param name="utcTicks">The decoded creation instant (UTC ticks) to page strictly after.</param>
    /// <param name="idUtf8">The decoded id to page strictly after (a slice of <paramref name="destination"/>), or empty.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page (empty token).</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, Span<byte> destination, out long utcTicks, out ReadOnlySpan<byte> idUtf8)
    {
        utcTicks = 0;
        idUtf8 = default;
        if (tokenUtf8.IsEmpty)
        {
            return false;
        }

        if (Base64Url.DecodeFromUtf8(tokenUtf8, destination, out _, out int decoded) != OperationStatus.Done)
        {
            throw new FormatException("The availability-request page token is not valid base64url.");
        }

        ReadOnlySpan<byte> key = destination[..decoded];
        int sep = key.IndexOf(Separator);
        if (sep < 0 || !Utf8Parser.TryParse(key[..sep], out utcTicks, out int consumed) || consumed != sep)
        {
            throw new FormatException("The availability-request page token is malformed.");
        }

        idUtf8 = key[(sep + 1)..];
        return true;
    }
}