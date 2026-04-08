// <copyright file="Utf8Hash.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides a fast hash function for short UTF-8 byte spans.
/// </summary>
/// <remarks>
/// <para>
/// For keys of 7 bytes or fewer, the hash encodes the entire key value,
/// making it a perfect hash (no collisions possible among keys of that length).
/// For longer keys, the hash mixes the first 8 bytes with the length and last byte.
/// </para>
/// </remarks>
[CLSCompliant(false)]
public static class Utf8Hash
{
    /// <summary>
    /// The number of bytes that are fully encoded in the hash. Keys shorter than
    /// or equal to this length produce perfect (collision-free) hashes.
    /// </summary>
    public const int PerfectHashLength = 7;

    /// <summary>
    /// A mask whose upper bits are set when the key is longer than <see cref="PerfectHashLength"/>.
    /// When <c>(hash &amp; HashMask) == 0</c>, the key was short enough to be perfectly encoded.
    /// </summary>
    public const ulong HashMask = 0xFF00_0000_0000_0000UL;

    /// <summary>
    /// Calculates a hash code for the specified UTF-8 byte span.
    /// </summary>
    /// <param name="key">The UTF-8 key to hash.</param>
    /// <returns>The calculated hash code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetHashCode(in ReadOnlySpan<byte> key)
    {
        int length = key.Length;

        return length switch
        {
            7 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
            6 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40),
            5 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32),
            4 => MemoryMarshal.Read<uint>(key.Slice(0, 4)),
            3 => ((ulong)key[2] << 16)
                    + ((ulong)key[1] << 8)
                    + key[0],
            2 => ((ulong)key[1] << 8)
                    + key[0],
            1 => key[0],
            0 => 0,
            _ => ((ulong)((length + key[7] + key[key.Length - 1]) % 256) << 56)
                    + MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
        };
    }
}