// <copyright file="SecurityBindingContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page
/// <see cref="ISecurityPolicyStore.ListBindingsAsync(int, JsonString, JsonString, System.Threading.CancellationToken)"/>
/// results (design §14.2). Bindings page by <c>(order, id)</c> — ascending <c>order</c> with the unique <c>id</c> as the
/// tie-breaker — so the token carries the last row's <c>(order, id)</c> for the next request to seek strictly past. It is
/// opaque and <b>backend-scoped</b> (only ever presented back to the store that issued it).
/// </summary>
/// <remarks>
/// Fully bytes-native: the token is <c>base64url(order \0 idUtf8)</c> with the integer order written as decimal UTF-8 via
/// <see cref="Utf8Formatter"/> and read back via <see cref="Utf8Parser"/>. <see cref="EncodeToUtf8"/> assembles the key
/// into a pooled scratch then Base64URL-encodes into the caller's buffer; <see cref="TryDecode"/> Base64URL-decodes into
/// the caller's buffer and returns the id as a span into it — no managed string and no intermediate <c>byte[]</c>, and the
/// decoded cursor id stays a span rather than a realised string.
/// </remarks>
public static class SecurityBindingContinuationToken
{
    // The null control byte cannot appear in the decimal order or the id, so it separates the two key parts.
    private const byte Separator = 0;

    // A signed 32-bit integer is at most 11 ASCII characters ("-2147483648"); ids are short, so the assemble scratch fits
    // the stack in the common case, with the ArrayPool fallback for the rare long id.
    private const int MaxOrderDigits = 11;
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for an id of
    /// <paramref name="idUtf8Length"/> bytes (the order contributes a fixed maximum width) — safe to size a buffer with.</summary>
    /// <param name="idUtf8Length">The id's UTF-8 byte length.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(int idUtf8Length) => Base64Url.GetEncodedLength(MaxOrderDigits + 1 + idUtf8Length);

    /// <summary>Writes the continuation token (<c>base64url</c> of <c>order \0 id</c>) for the last row's <c>(order, id)</c>
    /// into <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="order">The last row's order.</param>
    /// <param name="idUtf8">The last row's id as UTF-8.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(int order, ReadOnlySpan<byte> idUtf8, Span<byte> destination)
    {
        int rawMax = MaxOrderDigits + 1 + idUtf8.Length;
        byte[]? rented = rawMax > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawMax) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (!Utf8Formatter.TryFormat(order, raw, out int pos))
            {
                throw new FormatException("The binding order could not be formatted into the page token.");
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
    /// <see cref="GetMaxDecodedLength"/>), yielding the <c>(order, id)</c> to page strictly after — the id stays a span into
    /// the caller's buffer, never a managed string.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="destination">The buffer the decoded key is written into.</param>
    /// <param name="order">The decoded order to page strictly after.</param>
    /// <param name="idUtf8">The decoded id to page strictly after (a slice of <paramref name="destination"/>), or empty.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page (empty token).</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, Span<byte> destination, out int order, out ReadOnlySpan<byte> idUtf8)
    {
        order = 0;
        idUtf8 = default;
        if (tokenUtf8.IsEmpty)
        {
            return false;
        }

        if (Base64Url.DecodeFromUtf8(tokenUtf8, destination, out _, out int decoded) != OperationStatus.Done)
        {
            throw new FormatException("The security-binding page token is not valid base64url.");
        }

        ReadOnlySpan<byte> key = destination[..decoded];
        int sep = key.IndexOf(Separator);
        if (sep < 0 || !Utf8Parser.TryParse(key[..sep], out order, out int consumed) || consumed != sep)
        {
            throw new FormatException("The security-binding page token is malformed.");
        }

        idUtf8 = key[(sep + 1)..];
        return true;
    }
}