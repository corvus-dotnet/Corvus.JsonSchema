// <copyright file="SourceCredentialContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
/// <remarks>
/// The token is <c>base64url(part0 \0 part1 \0 part2)</c>. The <see cref="ReadOnlySpan{T}"/> overloads are the warm seam:
/// <see cref="EncodeToUtf8"/> writes the token straight into a caller buffer (the response writer / a pooled page buffer)
/// and <see cref="TryDecode(ReadOnlySpan{byte}, out (string, string, string))"/> reads it straight from the request's
/// UTF-8 — neither mints an intermediate string or <c>byte[]</c>. The <c>string</c> overloads are CLI/test convenience
/// over the same core.
/// </remarks>
public static class SourceCredentialContinuationToken
{
    // The null control byte cannot appear in a sourceName/environment/discriminator, so it separates the three key parts.
    private const byte Separator = 0;

    // Key parts are short (source names, environments, tag discriminators), so the assemble/decode scratch fits the
    // stack in the common case; the ArrayPool fallback covers the rare long key.
    private const int StackThreshold = 256;

    /// <summary>Gets the exact length, in bytes, of the Base64URL token <see cref="EncodeToUtf8"/> writes for these key parts.</summary>
    /// <param name="sourceName">The last row's source name.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (e.g. the tag discriminator).</param>
    /// <returns>The encoded token length in bytes.</returns>
    public static int GetEncodedLength(string sourceName, string environment, string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(tieBreaker);
        return Base64Url.GetEncodedLength(RawLength(sourceName, environment, tieBreaker));
    }

    /// <summary>Writes the opaque continuation token for the last page row's key as UTF-8 into <paramref name="destination"/>
    /// (size it with <see cref="GetEncodedLength"/>) — the warm path's bytes-native encode, with no intermediate string or
    /// <c>byte[]</c>.</summary>
    /// <param name="sourceName">The last row's source name.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (e.g. the tag discriminator).</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string sourceName, string environment, string tieBreaker, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        int rawLength = RawLength(sourceName, environment, tieBreaker);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            Span<byte> filled = raw[..AssembleKey(sourceName, environment, tieBreaker, raw)];
            Base64Url.EncodeToUtf8(filled, destination, out _, out int written);
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

    /// <summary>Encodes the last page row's key into a continuation token string (CLI/test convenience).</summary>
    /// <param name="sourceName">The last row's source name.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (e.g. the tag discriminator).</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string sourceName, string environment, string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        int rawLength = RawLength(sourceName, environment, tieBreaker);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            // Assemble the key once into the scratch buffer, then Base64URL it straight to the token string — no
            // per-part ToString(), no concat string, no separate GetBytes byte[].
            return Base64Url.EncodeToString(raw[..AssembleKey(sourceName, environment, tieBreaker, raw)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) back to the
    /// <c>(sourceName, environment, tie-breaker)</c> cursor to page strictly after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, out (string SourceName, string Environment, string TieBreaker) cursor)
    {
        cursor = default;
        if (tokenUtf8.IsEmpty)
        {
            return false;
        }

        int maxLength = Base64Url.GetMaxDecodedLength(tokenUtf8.Length);
        byte[]? rented = maxLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        Span<byte> buffer = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (Base64Url.DecodeFromUtf8(tokenUtf8, buffer, out _, out int decoded) != OperationStatus.Done)
            {
                throw new FormatException("The credential page token is not valid base64url.");
            }

            return TryReadCursor(buffer[..decoded], out cursor);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token string back to the cursor to page after (CLI/test convenience).</summary>
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

        int maxLength = Base64Url.GetMaxDecodedLength(token.Length);
        byte[]? rented = maxLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        Span<byte> buffer = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (Base64Url.DecodeFromChars(token, buffer, out _, out int decoded) != OperationStatus.Done)
            {
                throw new FormatException($"'{token}' is not a valid credential page token.");
            }

            return TryReadCursor(buffer[..decoded], out cursor);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    // Writes part0 \0 part1 \0 part2 as UTF-8 into raw, returning the byte count.
    private static int AssembleKey(string sourceName, string environment, string tieBreaker, Span<byte> raw)
    {
        int pos = Encoding.UTF8.GetBytes(sourceName, raw);
        raw[pos++] = Separator;
        pos += Encoding.UTF8.GetBytes(environment, raw[pos..]);
        raw[pos++] = Separator;
        pos += Encoding.UTF8.GetBytes(tieBreaker, raw[pos..]);
        return pos;
    }

    // Splits the decoded key on the two separator bytes and materializes the three cursor parts (genuine string leaves —
    // the stores bind them as ordering/seek parameters). No intermediate decoded string, no Split array.
    private static bool TryReadCursor(ReadOnlySpan<byte> key, out (string SourceName, string Environment, string TieBreaker) cursor)
    {
        cursor = default;
        int first = key.IndexOf(Separator);
        if (first < 0)
        {
            throw new FormatException("The credential page token is malformed.");
        }

        ReadOnlySpan<byte> afterFirst = key[(first + 1)..];
        int second = afterFirst.IndexOf(Separator);
        if (second < 0)
        {
            throw new FormatException("The credential page token is malformed.");
        }

        cursor = (
            Encoding.UTF8.GetString(key[..first]),
            Encoding.UTF8.GetString(afterFirst[..second]),
            Encoding.UTF8.GetString(afterFirst[(second + 1)..]));
        return true;
    }

    private static int RawLength(string sourceName, string environment, string tieBreaker)
        => Encoding.UTF8.GetByteCount(sourceName) + 1 + Encoding.UTF8.GetByteCount(environment) + 1 + Encoding.UTF8.GetByteCount(tieBreaker);
}