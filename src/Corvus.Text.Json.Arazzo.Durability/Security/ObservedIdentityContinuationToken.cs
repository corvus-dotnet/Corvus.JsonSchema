// <copyright file="ObservedIdentityContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="IObservedIdentityStore.SearchAsync"/>
/// results (design §16.5.4). The contractual order is ascending <c>subjectValue</c> (the prefix-searched column), made
/// total by the <c>subjectKind</c> tie-breaker so two identities that share a value page deterministically. The token
/// carries the last row's <c>(subjectValue, subjectKind)</c> so the next request can seek strictly past it; it is opaque
/// and <b>backend-scoped</b> (a token is only ever presented back to the store that issued it).
/// </summary>
/// <remarks>
/// The token is <c>base64url(subjectValue \0 subjectKind)</c>. The <see cref="ReadOnlySpan{T}"/> overloads are the warm
/// seam: <see cref="EncodeToUtf8"/> writes the token straight into a caller buffer (the response writer / a pooled page
/// buffer) and <see cref="TryDecode(ReadOnlySpan{byte}, out (string, string))"/> reads it straight from the request's
/// UTF-8 — neither mints an intermediate string or <c>byte[]</c>. The <c>string</c> overloads are CLI/test convenience.
/// </remarks>
public static class ObservedIdentityContinuationToken
{
    // The null control byte cannot appear in a subjectValue/subjectKind token, so it separates the two key parts.
    private const byte Separator = 0;

    // Subject values/kinds are short, so the assemble/decode scratch fits the stack in the common case; the ArrayPool
    // fallback covers the rare long key.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for these key parts —
    /// safe to size a destination buffer with (the exact count is <see cref="EncodeToUtf8"/>'s return value). Computed from
    /// <see cref="Encoding.GetMaxByteCount(int)"/> (a multiply, not a scan), so it over-estimates by a few bytes but never
    /// under-sizes.</summary>
    /// <param name="subjectValue">The last row's subject value.</param>
    /// <param name="subjectKind">The last row's subject kind token (the tie-breaker).</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(string subjectValue, string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(subjectValue);
        ArgumentNullException.ThrowIfNull(subjectKind);
        return Base64Url.GetEncodedLength(MaxRawLength(subjectValue, subjectKind));
    }

    /// <summary>Writes the opaque continuation token for the last page row's key as UTF-8 into <paramref name="destination"/>
    /// (size it with <see cref="GetMaxEncodedLength"/>) — the warm path's bytes-native encode, with no intermediate string or
    /// <c>byte[]</c>.</summary>
    /// <param name="subjectValue">The last row's subject value.</param>
    /// <param name="subjectKind">The last row's subject kind token (the tie-breaker).</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string subjectValue, string subjectKind, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(subjectValue);
        ArgumentNullException.ThrowIfNull(subjectKind);

        int rawLength = MaxRawLength(subjectValue, subjectKind);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            Span<byte> filled = raw[..AssembleKey(subjectValue, subjectKind, raw)];
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
    /// <param name="subjectValue">The last row's subject value.</param>
    /// <param name="subjectKind">The last row's subject kind token (the tie-breaker).</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string subjectValue, string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(subjectValue);
        ArgumentNullException.ThrowIfNull(subjectKind);

        int rawLength = MaxRawLength(subjectValue, subjectKind);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            // Assemble the key once into the scratch buffer, then Base64URL it straight to the token string — no
            // per-part ToString(), no concat string, no separate GetBytes byte[].
            return Base64Url.EncodeToString(raw[..AssembleKey(subjectValue, subjectKind, raw)]);
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
    /// <c>(subjectValue, subjectKind)</c> cursor to page strictly after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, out (string SubjectValue, string SubjectKind) cursor)
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
                throw new FormatException("The observed-identity page token is not valid base64url.");
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
    public static bool TryDecode(string? token, out (string SubjectValue, string SubjectKind) cursor)
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
                throw new FormatException($"'{token}' is not a valid observed-identity page token.");
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

    // Writes subjectValue \0 subjectKind as UTF-8 into raw, returning the byte count.
    private static int AssembleKey(string subjectValue, string subjectKind, Span<byte> raw)
    {
        int pos = Encoding.UTF8.GetBytes(subjectValue, raw);
        raw[pos++] = Separator;
        pos += Encoding.UTF8.GetBytes(subjectKind, raw[pos..]);
        return pos;
    }

    // Splits the decoded key on the separator byte and materializes the two cursor parts (genuine string leaves — the
    // stores bind them as ordering/seek parameters). No intermediate decoded string, no Split array.
    private static bool TryReadCursor(ReadOnlySpan<byte> key, out (string SubjectValue, string SubjectKind) cursor)
    {
        cursor = default;
        int separator = key.IndexOf(Separator);
        if (separator < 0)
        {
            throw new FormatException("The observed-identity page token is malformed.");
        }

        cursor = (Encoding.UTF8.GetString(key[..separator]), Encoding.UTF8.GetString(key[(separator + 1)..]));
        return true;
    }

    // An upper bound on the assembled key's byte count (each part's GetMaxByteCount + the 1-byte separator); used only to
    // size the scratch buffer, so over-estimating is free and AssembleKey returns the exact filled length.
    private static int MaxRawLength(string subjectValue, string subjectKind)
        => Encoding.UTF8.GetMaxByteCount(subjectValue.Length) + 1 + Encoding.UTF8.GetMaxByteCount(subjectKind.Length);
}