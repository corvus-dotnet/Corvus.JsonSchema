// <copyright file="SecurityPagingText.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared bytes-native text matching for the in-memory security pagers' <c>q</c> filter. A case-insensitive substring has
/// no clean UTF-8 primitive, so the field's persisted UTF-8 is transcoded into a <b>reused pooled UTF-16 buffer</b> (never
/// a managed string) and matched against the (already transcoded) query with <see cref="StringComparison.OrdinalIgnoreCase"/>.
/// </summary>
internal static class SecurityPagingText
{
    /// <summary>Tests whether <paramref name="qChars"/> is a case-insensitive substring of <paramref name="fieldUtf8"/>,
    /// transcoding the field into <paramref name="fieldBuffer"/> (rented/grown on demand; the caller returns it once).</summary>
    /// <param name="fieldUtf8">The field's persisted UTF-8.</param>
    /// <param name="qChars">The query, already transcoded to UTF-16 once by the caller.</param>
    /// <param name="fieldBuffer">A reused pooled UTF-16 scratch buffer (<see langword="null"/> on first use; grown as needed).</param>
    /// <returns><see langword="true"/> if the field contains the query (ordinal, case-insensitive).</returns>
    internal static bool ContainsIgnoreCase(ReadOnlySpan<byte> fieldUtf8, ReadOnlySpan<char> qChars, ref char[]? fieldBuffer)
    {
        int maxChars = Encoding.UTF8.GetMaxCharCount(fieldUtf8.Length);
        if (fieldBuffer is null || fieldBuffer.Length < maxChars)
        {
            if (fieldBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(fieldBuffer);
            }

            fieldBuffer = ArrayPool<char>.Shared.Rent(maxChars);
        }

        int written = Encoding.UTF8.GetChars(fieldUtf8, fieldBuffer);
        return fieldBuffer.AsSpan(0, written).Contains(qChars, StringComparison.OrdinalIgnoreCase);
    }
}