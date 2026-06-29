// <copyright file="AvailabilityContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="IAvailabilityStore"/> results
/// (design §7.8). The token carries the last row's full key — <c>(baseWorkflowId, versionNumber, environment)</c> — so
/// either list axis can seek strictly past it: the by-version listing orders by <c>environment</c>, the by-environment
/// listing orders by <c>(baseWorkflowId, versionNumber)</c>; each reads the parts it orders on from the same token. It is
/// opaque and <b>backend-scoped</b> (only ever presented back to the store that issued it).
/// </summary>
/// <remarks>
/// The token is <c>base64url(baseWorkflowId \0 versionNumber \0 environment)</c>. The <see cref="ReadOnlySpan{T}"/>
/// overloads are the warm seam: <see cref="EncodeToUtf8"/> writes the token straight into a caller buffer and
/// <see cref="TryDecode(ReadOnlySpan{byte}, out (string, int, string))"/> reads it straight from the request's UTF-8.
/// </remarks>
public static class AvailabilityContinuationToken
{
    // The null control byte cannot appear in a key part, so it separates the three parts.
    private const byte Separator = 0;

    // Key parts are short; the assemble/decode scratch fits the stack in the common case, with an ArrayPool fallback.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for these key parts.</summary>
    /// <param name="baseWorkflowId">The last row's base workflow id.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(string baseWorkflowId, string environment)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(environment);
        return Base64Url.GetEncodedLength(MaxRawLength(baseWorkflowId, environment));
    }

    /// <summary>Writes the opaque continuation token for the last page row's key as UTF-8 into
    /// <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="baseWorkflowId">The last row's base workflow id.</param>
    /// <param name="versionNumber">The last row's version number.</param>
    /// <param name="environment">The last row's environment.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(string baseWorkflowId, int versionNumber, string environment, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(environment);

        int rawLength = MaxRawLength(baseWorkflowId, environment);
        byte[]? rented = rawLength > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawLength) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            Span<byte> filled = raw[..AssembleKey(baseWorkflowId, versionNumber, environment, raw)];
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

    /// <summary>Decodes a continuation token's UTF-8 back to the <c>(baseWorkflowId, versionNumber, environment)</c>
    /// cursor to page strictly after.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 bytes, or empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
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
                throw new FormatException("The availability page token is not valid base64url.");
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

    // Writes baseWorkflowId \0 versionNumber \0 environment as UTF-8 into raw, returning the byte count.
    private static int AssembleKey(string baseWorkflowId, int versionNumber, string environment, Span<byte> raw)
    {
        int pos = Encoding.UTF8.GetBytes(baseWorkflowId, raw);
        raw[pos++] = Separator;
        versionNumber.TryFormat(raw[pos..], out int versionWritten, provider: CultureInfo.InvariantCulture);
        pos += versionWritten;
        raw[pos++] = Separator;
        pos += Encoding.UTF8.GetBytes(environment, raw[pos..]);
        return pos;
    }

    // Splits the decoded key on the separator bytes and materializes the three cursor parts. No intermediate strings.
    private static bool TryReadCursor(ReadOnlySpan<byte> key, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        int firstSep = key.IndexOf(Separator);
        if (firstSep < 0)
        {
            throw new FormatException("The availability page token is malformed.");
        }

        ReadOnlySpan<byte> rest = key[(firstSep + 1)..];
        int secondSep = rest.IndexOf(Separator);
        if (secondSep < 0)
        {
            throw new FormatException("The availability page token is malformed.");
        }

        if (!Utf8Parser.TryParse(rest[..secondSep], out int versionNumber, out _))
        {
            throw new FormatException("The availability page token has an invalid version number.");
        }

        cursor = (
            Encoding.UTF8.GetString(key[..firstSep]),
            versionNumber,
            Encoding.UTF8.GetString(rest[(secondSep + 1)..]));
        return true;
    }

    // An upper bound on the assembled key's byte count (each string part's GetMaxByteCount + a generous version-number
    // budget + the two separator bytes); used only to size the scratch buffer, so over-estimating is free.
    private static int MaxRawLength(string baseWorkflowId, string environment)
        => Encoding.UTF8.GetMaxByteCount(baseWorkflowId.Length) + 16 + Encoding.UTF8.GetMaxByteCount(environment.Length) + 2;
}