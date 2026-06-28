// <copyright file="SourceContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="ISourceStore.ListAsync"/> results
/// (design §7.6). Stores page by the stable total order <c>(name, tie-breaker)</c> — the contractual primary order is
/// <c>name</c>; the tie-breaker (the management-tag discriminator) makes it total so two reach-isolated sources that
/// share a name page deterministically. The token carries the last row's two key parts so the next request can seek
/// past it; it is opaque and <b>backend-scoped</b> (only ever presented back to the store that issued it, so each
/// backend may choose whatever tie-breaker it can order on).
/// </summary>
/// <remarks>
/// The token is <c>base64url(name \0 tie-breaker)</c>. The <see cref="ReadOnlySpan{T}"/> overloads are the warm seam:
/// <see cref="EncodeToUtf8"/> writes the token straight into a caller buffer (a pooled page buffer) and
/// <see cref="TryDecode(ReadOnlySpan{byte}, out (string, string))"/> reads it straight from the request's UTF-8 —
/// neither mints an intermediate string or <c>byte[]</c>. The <c>string</c> overloads are CLI/test convenience.
/// </remarks>
public static class SourceContinuationToken
{
    // The null control byte cannot appear in a name/discriminator, so it separates the two key parts.
    private const byte Separator = 0;

    // Key parts are short (source names, tag discriminators), so the assemble/decode scratch fits the stack in the
    // common case; the ArrayPool fallback covers the rare long key.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for these key
    /// parts — safe to size a destination buffer with (the exact count is <see cref="EncodeToUtf8"/>'s return value).</summary>
    /// <param name="name">The last row's source name.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(string name, string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tieBreaker);
        return Base64Url.GetEncodedLength(MaxRawLength(name, tieBreaker));
    }

    /// <summary>Writes the opaque continuation token for the last page row's key as UTF-8 into
    /// <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>) — the warm path's bytes-native
    /// encode, with no intermediate string or <c>byte[]</c>.</summary>
    /// <param name="name">The last row's source name.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string name, string tieBreaker, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        int rawLength = MaxRawLength(name, tieBreaker);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            Span<byte> filled = raw[..AssembleKey(name, tieBreaker, raw)];
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
    /// <param name="name">The last row's source name.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string name, string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        int rawLength = MaxRawLength(name, tieBreaker);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            return Base64Url.EncodeToString(raw[..AssembleKey(name, tieBreaker, raw)]);
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
    /// <c>(name, tie-breaker)</c> cursor to page strictly after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, out (string Name, string TieBreaker) cursor)
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
                throw new FormatException("The source page token is not valid base64url.");
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
    public static bool TryDecode(string? token, out (string Name, string TieBreaker) cursor)
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
                throw new FormatException($"'{token}' is not a valid source page token.");
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

    // Writes name \0 tieBreaker as UTF-8 into raw, returning the byte count.
    private static int AssembleKey(string name, string tieBreaker, Span<byte> raw)
    {
        int pos = Encoding.UTF8.GetBytes(name, raw);
        raw[pos++] = Separator;
        pos += Encoding.UTF8.GetBytes(tieBreaker, raw[pos..]);
        return pos;
    }

    // Splits the decoded key on the separator byte and materializes the two cursor parts (genuine string leaves — the
    // stores bind them as ordering/seek parameters). No intermediate decoded string, no Split array.
    private static bool TryReadCursor(ReadOnlySpan<byte> key, out (string Name, string TieBreaker) cursor)
    {
        cursor = default;
        int sep = key.IndexOf(Separator);
        if (sep < 0)
        {
            throw new FormatException("The source page token is malformed.");
        }

        cursor = (
            Encoding.UTF8.GetString(key[..sep]),
            Encoding.UTF8.GetString(key[(sep + 1)..]));
        return true;
    }

    // An upper bound on the assembled key's byte count (each part's GetMaxByteCount + the 1-byte separator); used only to
    // size the scratch buffer, so over-estimating is free and AssembleKey returns the exact filled length.
    private static int MaxRawLength(string name, string tieBreaker)
        => Encoding.UTF8.GetMaxByteCount(name.Length) + 1 + Encoding.UTF8.GetMaxByteCount(tieBreaker.Length);
}