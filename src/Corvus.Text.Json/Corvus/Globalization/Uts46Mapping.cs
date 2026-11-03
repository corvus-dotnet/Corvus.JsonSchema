// <copyright file="Uts46Mapping.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
#if CORVUS_JSON_EXTENDEDTYPES

// Linked into the V4 engine (Corvus.Json.ExtendedTypes), which has its own public Corvus.Globalization types.
namespace Corvus.Text.Json.Internal.Globalization;
#else

namespace Corvus.Globalization;
#endif

/// <summary>
/// The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the
/// <c>idn-hostname</c> and <c>idn-email</c> formats apply to a name before they check it.
/// </summary>
/// <remarks>
/// <para>
/// An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
/// "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and
/// meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse.
/// ASCII is kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid.
/// </para>
/// <para>
/// The normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name,
/// as the JSON-Schema-Test-Suite expects.
/// </para>
/// </remarks>
internal static partial class Uts46Mapping
{
    private const int EntrySize = 8;

    /// <summary>
    /// Gets a value indicating whether the mapping can change the name: whether it has a byte outside ASCII.
    /// </summary>
    /// <param name="utf8">The UTF-8 encoded name.</param>
    /// <returns><see langword="true"/> if the name has a code point outside ASCII.</returns>
    internal static bool MayChange(ReadOnlySpan<byte> utf8)
    {
        foreach (byte b in utf8)
        {
            if (b > 0x7F)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the mapped form of a name.
    /// </summary>
    /// <param name="utf8">The UTF-8 encoded name.</param>
    /// <param name="destination">The buffer for the mapped name.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the mapped name fit in the buffer and the name was well-formed UTF-8.</returns>
    internal static bool TryMap(ReadOnlySpan<byte> utf8, Span<byte> destination, out int written)
    {
        written = 0;
        int i = 0;
        while (i < utf8.Length)
        {
            byte b = utf8[i];
            if (b < 0x80)
            {
                if (written == destination.Length)
                {
                    return false;
                }

                destination[written++] = b;
                i++;
                continue;
            }

            if (Rune.DecodeFromUtf8(utf8.Slice(i), out Rune rune, out int consumed) != OperationStatus.Done)
            {
                return false;
            }

            ReadOnlySpan<byte> replacement = utf8.Slice(i, consumed);
            if (TryFind(rune.Value, out int offset, out int length))
            {
                replacement = length == 0 ? default : Pool.Slice(offset, length);
            }

            if (written + replacement.Length > destination.Length)
            {
                return false;
            }

            replacement.CopyTo(destination.Slice(written));
            written += replacement.Length;
            i += consumed;
        }

        return true;
    }

    /// <summary>
    /// Finds the entry for a code point outside ASCII.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="offset">The offset of its mapping in the pool.</param>
    /// <param name="length">The length of its mapping in the pool; 0 if it is ignored.</param>
    /// <returns><see langword="true"/> if the code point is ignored or mapped.</returns>
    private static bool TryFind(int codePoint, out int offset, out int length)
    {
        ReadOnlySpan<byte> entries = Entries;
        int low = 0;
        int high = EntryCount - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            ReadOnlySpan<byte> entry = entries.Slice(middle * EntrySize, EntrySize);
            int start = (entry[0] << 16) | (entry[1] << 8) | entry[2];
            if (codePoint < start)
            {
                high = middle - 1;
                continue;
            }

            int end = start + ((entry[3] << 8) | entry[4]);
            if (codePoint > end)
            {
                low = middle + 1;
                continue;
            }

            offset = (entry[5] << 8) | entry[6];
            length = entry[7];
            return true;
        }

        offset = 0;
        length = 0;
        return false;
    }
}