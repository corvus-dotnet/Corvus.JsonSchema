// <copyright file="RunnerRegistryContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page
/// <see cref="IRunnerRegistry.ListAsync(int, JsonString, System.Threading.CancellationToken)"/> results. Runners page by
/// their unique <c>runnerId</c>, so the token is simply <c>base64url(runnerIdUtf8)</c> — the last page row's id carried
/// verbatim as bytes so the next request seeks strictly past it. It is opaque and <b>backend-scoped</b> (only ever
/// presented back to the store that issued it).
/// </summary>
/// <remarks>
/// Fully bytes-native: <see cref="EncodeToUtf8"/> Base64URL-encodes the row's UTF-8 id straight into a caller buffer and
/// <see cref="TryDecode"/> Base64URL-decodes the request's token straight into a caller buffer — no managed string and no
/// intermediate <c>byte[]</c> on either path; the decoded cursor stays a span into the caller's buffer.
/// </remarks>
public static class RunnerRegistryContinuationToken
{
    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for an id of
    /// <paramref name="runnerIdUtf8Length"/> UTF-8 bytes — safe to size a destination buffer with.</summary>
    /// <param name="runnerIdUtf8Length">The runner id's UTF-8 byte length.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(int runnerIdUtf8Length) => Base64Url.GetEncodedLength(runnerIdUtf8Length);

    /// <summary>Writes the continuation token (<c>base64url</c> of the last row's <paramref name="runnerIdUtf8"/>) into
    /// <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="runnerIdUtf8">The last page row's runner id as UTF-8.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(ReadOnlySpan<byte> runnerIdUtf8, Span<byte> destination)
    {
        Base64Url.EncodeToUtf8(runnerIdUtf8, destination, out _, out int written);
        return written;
    }

    /// <summary>Gets an upper bound, in bytes, on the decoded id <see cref="TryDecode"/> writes for a token of
    /// <paramref name="tokenUtf8Length"/> bytes — safe to size the decode buffer with.</summary>
    /// <param name="tokenUtf8Length">The token's UTF-8 byte length.</param>
    /// <returns>An upper bound on the decoded id length in bytes.</returns>
    public static int GetMaxDecodedLength(int tokenUtf8Length) => Base64Url.GetMaxDecodedLength(tokenUtf8Length);

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) into <paramref name="destination"/>
    /// (size it with <see cref="GetMaxDecodedLength"/>), yielding the runner id to page strictly after — the cursor stays a
    /// span into the caller's buffer, never a managed string.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="destination">The buffer the decoded id is written into.</param>
    /// <param name="runnerIdUtf8">The decoded runner id to page strictly after (a slice of <paramref name="destination"/>), or empty.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page (empty token).</returns>
    /// <exception cref="FormatException">The token is not valid base64url.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, Span<byte> destination, out ReadOnlySpan<byte> runnerIdUtf8)
    {
        if (tokenUtf8.IsEmpty)
        {
            runnerIdUtf8 = default;
            return false;
        }

        if (Base64Url.DecodeFromUtf8(tokenUtf8, destination, out _, out int decoded) != OperationStatus.Done)
        {
            throw new FormatException("The runner page token is not valid base64url.");
        }

        runnerIdUtf8 = destination[..decoded];
        return true;
    }

    /// <summary>Decodes the keyset cursor from a request's page token to the runner id as a managed string — the form a
    /// backend's native keyset needs at its genuine leaf (a SQL <c>@after</c> parameter, a KV key, or an OData literal).
    /// Bytes-native up to the leaf: the token's UTF-8 is Base64URL-decoded into a pooled buffer and the id is realised to a
    /// string only on return (one transient cursor string per request, never per row).</summary>
    /// <param name="pageToken">The request's page token (its JSON value), or undefined for the first page.</param>
    /// <returns>The runner id to page strictly after, or <see langword="null"/> for the first page (undefined/empty token).</returns>
    /// <exception cref="FormatException">The token is not valid base64url.</exception>
    public static string? DecodeCursorToString(JsonString pageToken)
    {
        if (!pageToken.IsNotUndefined())
        {
            return null;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(GetMaxDecodedLength(tokenUtf8.Span.Length));
        try
        {
            return TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> runnerIdUtf8)
                ? Encoding.UTF8.GetString(runnerIdUtf8)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}