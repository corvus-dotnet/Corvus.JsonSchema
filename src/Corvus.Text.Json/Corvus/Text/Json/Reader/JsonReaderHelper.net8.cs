// <copyright file="JsonReaderHelper.net8.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Corvus.Text.Json;

internal static partial class JsonReaderHelper
{
    /// <summary>'"', '\',  or any control characters (i.e. 0 to 31).</summary>
    /// <remarks>https:// tools.ietf.org/html/rfc8259</remarks>
    private static readonly SearchValues<byte> s_controlQuoteBackslash = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F"u8 + // Any Control, < 32 (' ')
        "\""u8 + // Quote
        "\\"u8); // Backslash

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfQuoteOrAnyControlOrBackSlash(this ReadOnlySpan<byte> span) =>
        span.IndexOfAny(s_controlQuoteBackslash);

    /// <summary>
    /// The search a string is scanned with: it stops where <see cref="IndexOfQuoteOrAnyControlOrBackSlash"/> stops, and
    /// also at the first byte that is not ASCII. A scan that reaches the closing quote has therefore passed only ASCII.
    /// </summary>
    /// <param name="span">The text after the opening quote.</param>
    /// <returns>The index of the first such byte, or -1.</returns>
    /// <remarks>
    /// One signed comparison finds both a control character and a byte above 0x7F (which is negative as a signed
    /// byte), so the test costs no more than the search for the quote, backslash and controls alone. A string is
    /// mostly shorter than a vector and the text after it longer, so the common case is one load and one test.
    /// </remarks>
    public static int IndexOfQuoteOrAnyControlOrBackSlashOrNonAscii(this ReadOnlySpan<byte> span)
    {
        ref byte start = ref MemoryMarshal.GetReference(span);
        int length = span.Length;
        int i = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<byte>.Count)
        {
            Vector256<sbyte> space = Vector256.Create((sbyte)0x20);
            Vector256<byte> quote = Vector256.Create((byte)'"');
            Vector256<byte> backslash = Vector256.Create((byte)'\\');
            int last = length - Vector256<byte>.Count;
            while (true)
            {
                Vector256<byte> text = Vector256.LoadUnsafe(ref start, (nuint)i);
                uint hits = (Vector256.LessThan(text.AsSByte(), space).AsByte() | Vector256.Equals(text, quote) | Vector256.Equals(text, backslash)).ExtractMostSignificantBits();
                if (hits != 0)
                {
                    return i + BitOperations.TrailingZeroCount(hits);
                }

                if (i == last)
                {
                    return -1;
                }

                // The last vector overlaps the one before it: the bytes seen twice had no hit.
                i = Math.Min(i + Vector256<byte>.Count, last);
            }
        }

        if (Vector128.IsHardwareAccelerated && length >= Vector128<byte>.Count)
        {
            Vector128<sbyte> space = Vector128.Create((sbyte)0x20);
            Vector128<byte> quote = Vector128.Create((byte)'"');
            Vector128<byte> backslash = Vector128.Create((byte)'\\');
            int last = length - Vector128<byte>.Count;
            while (true)
            {
                Vector128<byte> text = Vector128.LoadUnsafe(ref start, (nuint)i);
                uint hits = (Vector128.LessThan(text.AsSByte(), space).AsByte() | Vector128.Equals(text, quote) | Vector128.Equals(text, backslash)).ExtractMostSignificantBits();
                if (hits != 0)
                {
                    return i + BitOperations.TrailingZeroCount(hits);
                }

                if (i == last)
                {
                    return -1;
                }

                i = Math.Min(i + Vector128<byte>.Count, last);
            }
        }

        for (; i < length; i++)
        {
            byte value = Unsafe.Add(ref start, i);
            if (value < 0x20 || value >= 0x80 || value == (byte)'"' || value == (byte)'\\')
            {
                return i;
            }
        }

        return -1;
    }
}