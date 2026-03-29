// <copyright file="Ascii.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

public static partial class Ascii
{
    /// <summary>
    /// Determines whether the provided value contains only ASCII bytes.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True if <paramref name="value"/> contains only ASCII bytes or is
    /// empty; False otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(ReadOnlySpan<byte> value) =>
        IsValidCore(ref MemoryMarshal.GetReference(value), value.Length);

    /// <summary>
    /// Determines whether the provided value is ASCII byte.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True if <paramref name="value"/> is ASCII, False otherwise.</returns>
    public static bool IsValid(byte value) => value <= 127;

    /// <summary>
    /// Determines whether the provided value is ASCII char.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True if <paramref name="value"/> is ASCII, False otherwise.</returns>
    public static bool IsValid(char value) => value <= 127;

    private static unsafe bool IsValidCore(ref byte searchSpace, int length)
    {
        const uint elementsPerUlong = sizeof(ulong);

        if (length < elementsPerUlong)
        {
            if (length >= sizeof(uint))
            {
                // Process byte inputs with lengths [4, 7]
                return AllBytesInUInt32AreAscii(
                    Unsafe.ReadUnaligned<uint>(ref searchSpace) |
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref searchSpace, length - sizeof(uint))));
            }

            // Process inputs with lengths [0, 3]
            for (nuint j = 0; j < (uint)length; j++)
            {
                if (Unsafe.Add(ref searchSpace, j) > 127)
                {
                    return false;
                }
            }

            return true;
        }

        nuint i = 0;

        nuint finalStart = (nuint)length - 2 * elementsPerUlong;

        for (; i < finalStart; i += 2 * elementsPerUlong)
        {
            if (!AllCharsInUInt64AreAscii(
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref searchSpace, i)) |
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref searchSpace, i + elementsPerUlong))))
            {
                return false;
            }
        }

        i = finalStart;

        // Process the last [8, 16] bytes.
        return AllCharsInUInt64AreAscii(
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref searchSpace, i)) |
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Subtract(ref Unsafe.Add(ref searchSpace, length), sizeof(ulong))));
    }

    /// <summary>
    /// Returns <see langword="true"/> iff all chars in <paramref name="value"/> are ASCII.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllCharsInUInt64AreAscii(ulong value)
    {
        return (value & ~0x007F007F_007F007Ful) == 0;
    }

    /// <summary>
    /// A mask which selects only the high bit of each byte of the given <see cref="uint"/>.
    /// </summary>
    private const uint UInt32HighBitsOnlyMask = 0x80808080u;

    /// <summary>
    /// Returns <see langword="true"/> iff all bytes in <paramref name="value"/> are ASCII.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AllBytesInUInt32AreAscii(uint value)
    {
        // If the high bit of any byte is set, that byte is non-ASCII.
        return (value & UInt32HighBitsOnlyMask) == 0;
    }
}