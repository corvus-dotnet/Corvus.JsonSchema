// <copyright file="WorkflowContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to page <see cref="WorkflowQuery"/> results. Stores
/// page by ascending run address — environment first, then run id, both ordinal (ADR 0065 decision 9) — so the
/// token is the last address of a page, base64url-encoded to keep it opaque and URL-safe. Centralised here so
/// every backend pages identically.
/// </summary>
/// <remarks>
/// The token is <c>base64url({environment}/{runId})</c>: the environment-name grammar admits no <c>/</c>, so the
/// first <c>/</c> splits the halves unambiguously whatever the id contains. The <see cref="ReadOnlySpan{T}"/>
/// overloads are the warm seam: <see cref="EncodeToUtf8(in WorkflowRunAddress, Span{byte})"/> writes the token straight into a caller buffer (the
/// response writer / a pooled page buffer) and <see cref="Decode(ReadOnlySpan{byte})"/> reads it straight from
/// the request's UTF-8 — neither mints an intermediate <c>byte[]</c>. The <c>string</c> overloads are CLI/test
/// convenience over the same core.
/// </remarks>
public static class WorkflowContinuationToken
{
    // Addresses are short (environment at most 63 bytes, run ids 32 in canon), so the encode/decode scratch fits
    // the stack in the common case; the ArrayPool fallback covers the rare long fixture id.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token
    /// <see cref="EncodeToUtf8(in WorkflowRunAddress, Span{byte})"/> writes for this address — safe to size a
    /// destination buffer with (the exact count is that method's return value). Computed from
    /// <see cref="Encoding.GetMaxByteCount(int)"/> (a multiply, not a scan), so it over-estimates by a few bytes
    /// but never under-sizes.</summary>
    /// <param name="last">The last address returned in the page.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(in WorkflowRunAddress last)
        => Base64Url.GetEncodedLength(Encoding.UTF8.GetMaxByteCount(last.Environment.Length + 1 + last.RunId.Value.Length));

    /// <summary>Writes the opaque continuation token for the last address as UTF-8 into <paramref name="destination"/>
    /// (size it with <see cref="GetMaxEncodedLength(in WorkflowRunAddress)"/>) — the warm path's bytes-native encode,
    /// with no intermediate <c>byte[]</c>.</summary>
    /// <param name="last">The last address returned in the page.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(in WorkflowRunAddress last, Span<byte> destination)
    {
        int rawLength = Encoding.UTF8.GetMaxByteCount(last.Environment.Length + 1 + last.RunId.Value.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            int written = WriteComposite(last, raw);
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

    /// <summary>Encodes the last address of a page into a continuation token string (CLI/test convenience).</summary>
    /// <param name="last">The last address returned in the page.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(in WorkflowRunAddress last)
    {
        int rawLength = Encoding.UTF8.GetMaxByteCount(last.Environment.Length + 1 + last.RunId.Value.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            return Base64Url.EncodeToString(raw[..WriteComposite(last, raw)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8(string, Span{byte})"/>
    /// writes for this raw cursor text — the string-cursor form the catalog's keyset paging uses (its cursor is a
    /// workflow sort key, not a run address).</summary>
    /// <param name="cursor">The raw cursor text.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(string cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        return Base64Url.GetEncodedLength(Encoding.UTF8.GetMaxByteCount(cursor.Length));
    }

    /// <summary>Writes the opaque continuation token for a raw cursor text as UTF-8 into <paramref name="destination"/>
    /// (size it with <see cref="GetMaxEncodedLength(string)"/>) — the string-cursor form the catalog's keyset paging
    /// uses.</summary>
    /// <param name="cursor">The raw cursor text.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string cursor, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        int rawLength = Encoding.UTF8.GetMaxByteCount(cursor.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            int written = Encoding.UTF8.GetBytes(cursor, raw);
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

    /// <summary>Encodes a raw cursor text into a continuation token string (CLI/test convenience for the
    /// string-cursor form).</summary>
    /// <param name="cursor">The raw cursor text.</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        int rawLength = Encoding.UTF8.GetMaxByteCount(cursor.Length);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            return Base64Url.EncodeToString(raw[..Encoding.UTF8.GetBytes(cursor, raw)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token's UTF-8 back to its raw cursor text — the string-cursor form the
    /// catalog's keyset paging uses.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page, or empty for the first page.</param>
    /// <returns>The raw cursor text, or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static string? DecodeText(ReadOnlySpan<byte> tokenUtf8)
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
                ThrowHelper.ThrowContinuationTokenMalformed();
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

    /// <summary>
    /// Builds a page from rows a store fetched with <c>limit + 1</c> ordered by ascending address (environment,
    /// then run id): if more than <paramref name="limit"/> rows came back there is a next page, so the extra row
    /// is dropped and a continuation token is emitted from the last kept row.
    /// </summary>
    /// <param name="rows">The fetched rows (at most <paramref name="limit"/> + 1), ascending by address. Mutated in place.</param>
    /// <param name="limit">The page size requested by the query.</param>
    /// <returns>The page, with a continuation token when a next page exists.</returns>
    public static WorkflowRunPage Paginate(List<WorkflowRunListing> rows, int limit)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            return WorkflowRunPage.Create(rows, rows[^1].Address);
        }

        return WorkflowRunPage.Create(rows);
    }

    /// <summary>Decodes a continuation token's UTF-8 (as carried verbatim by the request) back to the address to
    /// resume after — the warm path's bytes-native decode.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes from a previous page, or empty for the first page.</param>
    /// <returns>The address to page after (exclusive), or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static WorkflowRunAddress? Decode(ReadOnlySpan<byte> tokenUtf8)
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
                ThrowHelper.ThrowContinuationTokenMalformed();
            }

            return SplitComposite(Encoding.UTF8.GetString(buffer[..decoded]));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a continuation token string back to the address to resume after (CLI/test convenience).</summary>
    /// <param name="token">A token from a previous page, or <see langword="null"/> for the first page.</param>
    /// <returns>The address to page after (exclusive), or <see langword="null"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static WorkflowRunAddress? Decode(string? token)
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
                ThrowHelper.ThrowContinuationTokenMalformed();
            }

            return SplitComposite(Encoding.UTF8.GetString(buffer[..decoded]));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int WriteComposite(in WorkflowRunAddress last, Span<byte> raw)
    {
        int written = Encoding.UTF8.GetBytes(last.Environment, raw);
        raw[written++] = (byte)'/';
        return written + Encoding.UTF8.GetBytes(last.RunId.Value, raw[written..]);
    }

    // Splits the decoded composite at its first '/' (the environment grammar admits none) and re-validates the
    // environment half through the address constructor; a token that fails either is malformed, not an argument
    // error — the caller handed us opaque request input.
    private static WorkflowRunAddress SplitComposite(string composite)
    {
        int separator = composite.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0)
        {
            ThrowHelper.ThrowContinuationTokenMalformed();
        }

        try
        {
            return new WorkflowRunAddress(composite[..separator], new WorkflowRunId(composite[(separator + 1)..]));
        }
        catch (ArgumentException)
        {
            ThrowHelper.ThrowContinuationTokenMalformed();
            return default;
        }
    }
}