// <copyright file="SecurityRuleContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page
/// <see cref="ISecurityPolicyStore.ListRulesAsync(int, JsonString, JsonString, System.Threading.CancellationToken)"/>
/// results (design §14.2). Rules page by their unique <c>name</c>, so the token is simply <c>base64url(nameUtf8)</c> — the
/// last page row's name carried verbatim as bytes so the next request seeks strictly past it. It is opaque and
/// <b>backend-scoped</b> (only ever presented back to the store that issued it).
/// </summary>
/// <remarks>
/// Fully bytes-native: <see cref="EncodeToUtf8"/> Base64URL-encodes the row's UTF-8 name straight into a caller buffer and
/// <see cref="TryDecode"/> Base64URL-decodes the request's token straight into a caller buffer — no managed string and no
/// intermediate <c>byte[]</c> on either path. The caller owns both buffers (a pooled page buffer / a pooled scan buffer),
/// and the decoded cursor stays a span into the caller's buffer rather than a realised string.
/// </remarks>
public static class SecurityRuleContinuationToken
{
    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for a name of
    /// <paramref name="nameUtf8Length"/> UTF-8 bytes — safe to size a destination buffer with.</summary>
    /// <param name="nameUtf8Length">The name's UTF-8 byte length.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(int nameUtf8Length) => Base64Url.GetEncodedLength(nameUtf8Length);

    /// <summary>Writes the continuation token (<c>base64url</c> of the last row's <paramref name="nameUtf8"/>) into
    /// <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="nameUtf8">The last page row's name as UTF-8.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(ReadOnlySpan<byte> nameUtf8, Span<byte> destination)
    {
        Base64Url.EncodeToUtf8(nameUtf8, destination, out _, out int written);
        return written;
    }

    /// <summary>Gets an upper bound, in bytes, on the decoded name <see cref="TryDecode"/> writes for a token of
    /// <paramref name="tokenUtf8Length"/> bytes — safe to size the decode buffer with.</summary>
    /// <param name="tokenUtf8Length">The token's UTF-8 byte length.</param>
    /// <returns>An upper bound on the decoded name length in bytes.</returns>
    public static int GetMaxDecodedLength(int tokenUtf8Length) => Base64Url.GetMaxDecodedLength(tokenUtf8Length);

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) into <paramref name="destination"/>
    /// (size it with <see cref="GetMaxDecodedLength"/>), yielding the name to page strictly after — the cursor stays a span
    /// into the caller's buffer, never a managed string.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="destination">The buffer the decoded name is written into.</param>
    /// <param name="nameUtf8">The decoded name to page strictly after (a slice of <paramref name="destination"/>), or empty.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page (empty token).</returns>
    /// <exception cref="FormatException">The token is not valid base64url.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, Span<byte> destination, out ReadOnlySpan<byte> nameUtf8)
    {
        if (tokenUtf8.IsEmpty)
        {
            nameUtf8 = default;
            return false;
        }

        if (Base64Url.DecodeFromUtf8(tokenUtf8, destination, out _, out int decoded) != OperationStatus.Done)
        {
            throw new FormatException("The security-rule page token is not valid base64url.");
        }

        nameUtf8 = destination[..decoded];
        return true;
    }
}