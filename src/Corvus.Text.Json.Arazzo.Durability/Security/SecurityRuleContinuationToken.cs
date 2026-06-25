// <copyright file="SecurityRuleContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="ISecurityPolicyStore.ListRulesAsync(int, JsonString, string?, System.Threading.CancellationToken)"/>
/// results (design §14.2). Rules page by their unique <c>name</c>, so the token carries just the last page row's name and
/// the next request seeks strictly past it. It is opaque and <b>backend-scoped</b> (only ever presented back to the store
/// that issued it).
/// </summary>
/// <remarks>
/// The token is <c>base64url(name)</c>. The <see cref="ReadOnlySpan{T}"/> seam is the warm path:
/// <see cref="EncodeToUtf8"/> writes it straight into a caller buffer (a pooled page buffer) and
/// <see cref="TryDecode(ReadOnlySpan{byte}, out string)"/> reads it straight from the request's UTF-8 — neither mints an
/// intermediate token string or <c>byte[]</c>. The <c>string</c> overloads are CLI/test convenience over the same core.
/// </remarks>
public static class SecurityRuleContinuationToken
{
    // Names are short, so the assemble/decode scratch fits the stack in the common case; ArrayPool covers the rare long name.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for <paramref name="name"/> —
    /// safe to size a destination buffer with (the exact count is <see cref="EncodeToUtf8"/>'s return value).</summary>
    /// <param name="name">The last page row's rule name.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Base64Url.GetEncodedLength(Encoding.UTF8.GetMaxByteCount(name.Length));
    }

    /// <summary>Writes the opaque continuation token for the last page row's <paramref name="name"/> as UTF-8 into
    /// <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>) — the warm path's bytes-native
    /// encode, with no intermediate string or <c>byte[]</c>.</summary>
    /// <param name="name">The last page row's rule name.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string name, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(name);
        int rawLength = Encoding.UTF8.GetMaxByteCount(name.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            int filled = Encoding.UTF8.GetBytes(name, raw);
            Base64Url.EncodeToUtf8(raw[..filled], destination, out _, out int written);
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

    /// <summary>Encodes the last page row's name into a continuation token string (CLI/test convenience).</summary>
    /// <param name="name">The last page row's rule name.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int rawLength = Encoding.UTF8.GetMaxByteCount(name.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            return Base64Url.EncodeToString(raw[..Encoding.UTF8.GetBytes(name, raw)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) back to the rule
    /// <paramref name="name"/> to page strictly after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="name">The decoded rule name to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, out string name)
    {
        name = string.Empty;
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
                throw new FormatException("The security-rule page token is not valid base64url.");
            }

            name = Encoding.UTF8.GetString(buffer[..decoded]);
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token string back to the rule name to page after (CLI/test convenience).</summary>
    /// <param name="token">A token from a previous page's <c>nextPageToken</c>, or <see langword="null"/>/empty for the first page.</param>
    /// <param name="name">The decoded rule name to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(string? token, out string name)
    {
        name = string.Empty;
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
                throw new FormatException($"'{token}' is not a valid security-rule page token.");
            }

            name = Encoding.UTF8.GetString(buffer[..decoded]);
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}