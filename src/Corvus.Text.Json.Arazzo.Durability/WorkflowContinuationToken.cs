// <copyright file="WorkflowContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to page <see cref="WorkflowQuery"/> results. Stores
/// page by ascending run id (keyset paging), so the token is just the last run id of a page, base64url-encoded
/// to keep it opaque and URL-safe. Centralised here so every backend pages identically.
/// </summary>
/// <remarks>
/// The token is <c>base64url(lastRunId)</c>. The <see cref="ReadOnlySpan{T}"/> overloads are the warm seam:
/// <see cref="EncodeToUtf8"/> writes the token straight into a caller buffer (the response writer / a pooled page buffer)
/// and <see cref="Decode(ReadOnlySpan{byte})"/> reads it straight from the request's UTF-8 — neither mints an
/// intermediate <c>byte[]</c>. The <c>string</c> overloads are CLI/test convenience over the same core.
/// </remarks>
public static class WorkflowContinuationToken
{
    // Run ids are short, so the encode/decode scratch fits the stack in the common case; the ArrayPool fallback covers
    // the rare long id.
    private const int StackThreshold = 256;

    /// <summary>Gets the exact length, in bytes, of the Base64URL token <see cref="EncodeToUtf8"/> writes for this run id.</summary>
    /// <param name="lastRunId">The last run id returned in the page.</param>
    /// <returns>The encoded token length in bytes.</returns>
    public static int GetEncodedLength(string lastRunId)
    {
        ArgumentNullException.ThrowIfNull(lastRunId);
        return Base64Url.GetEncodedLength(Encoding.UTF8.GetByteCount(lastRunId));
    }

    /// <summary>Writes the opaque continuation token for the last run id as UTF-8 into <paramref name="destination"/>
    /// (size it with <see cref="GetEncodedLength"/>) — the warm path's bytes-native encode, with no intermediate
    /// <c>byte[]</c>.</summary>
    /// <param name="lastRunId">The last run id returned in the page.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string lastRunId, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(lastRunId);

        int rawLength = Encoding.UTF8.GetByteCount(lastRunId);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            int written = Encoding.UTF8.GetBytes(lastRunId, raw);
            Base64Url.EncodeToUtf8(raw[..written], destination, out _, out int encoded);
            return encoded;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Encodes the last run id of a page into a continuation token string (CLI/test convenience).</summary>
    /// <param name="lastRunId">The last run id returned in the page.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string lastRunId)
    {
        ArgumentNullException.ThrowIfNull(lastRunId);

        int rawLength = Encoding.UTF8.GetByteCount(lastRunId);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            // Transcode the id once into the scratch buffer, then Base64URL it straight to the token string — no
            // separate GetBytes byte[].
            return Base64Url.EncodeToString(raw[..Encoding.UTF8.GetBytes(lastRunId, raw)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Builds a page from rows a store fetched with <c>limit + 1</c> ordered by ascending run id: if more than
    /// <paramref name="limit"/> rows came back there is a next page, so the extra row is dropped and a
    /// continuation token is emitted from the last kept row.
    /// </summary>
    /// <param name="rows">The fetched rows (at most <paramref name="limit"/> + 1), ascending by run id. Mutated in place.</param>
    /// <param name="limit">The page size requested by the query.</param>
    /// <returns>The page, with a continuation token when a next page exists.</returns>
    public static WorkflowRunPage Paginate(List<WorkflowRunListing> rows, int limit)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            return new WorkflowRunPage(rows, Encode(rows[^1].Id.Value));
        }

        return new WorkflowRunPage(rows);
    }

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) back to the run id to resume
    /// after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page, or empty for the first page.</param>
    /// <returns>The run id to page after (exclusive), or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static string? Decode(ReadOnlySpan<byte> tokenUtf8)
    {
        if (tokenUtf8.IsEmpty)
        {
            return null;
        }

        int maxLength = Base64Url.GetMaxDecodedLength(tokenUtf8.Length);
        byte[]? rented = maxLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        Span<byte> buffer = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (Base64Url.DecodeFromUtf8(tokenUtf8, buffer, out _, out int decoded) != OperationStatus.Done)
            {
                throw new FormatException("The continuation token is malformed.");
            }

            return Encoding.UTF8.GetString(buffer[..decoded]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token string back to the run id to resume after (CLI/test convenience).</summary>
    /// <param name="token">A token from a previous page, or <see langword="null"/> for the first page.</param>
    /// <returns>The run id to page after (exclusive), or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static string? Decode(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        int maxLength = Base64Url.GetMaxDecodedLength(token.Length);
        byte[]? rented = maxLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        Span<byte> buffer = rented ?? stackalloc byte[StackThreshold];
        try
        {
            if (Base64Url.DecodeFromChars(token, buffer, out _, out int decoded) != OperationStatus.Done)
            {
                throw new FormatException("The continuation token is malformed.");
            }

            return Encoding.UTF8.GetString(buffer[..decoded]);
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