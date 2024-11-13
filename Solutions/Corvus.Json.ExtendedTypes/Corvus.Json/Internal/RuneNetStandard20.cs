// <copyright file="RuneNetStandard20.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// </licensing>

#if !NET8_0_OR_GREATER

using System.Buffers;
using System.Runtime.CompilerServices;

#pragma warning disable

// Contains a polyfill implementation of System.Text.Rune that works on netstandard2.0.
// Implementation copied from:
// https://github.com/dotnet/runtime/blob/177d6f1a0bfdc853ae9ffeef4be99ff984c4f5dd/src/libraries/System.Private.CoreLib/src/System/Text/Rune.cs

namespace Corvus.Json;

public readonly struct Rune
{
    private const int MaxUtf16CharsPerRune = 2; // supplementary plane code points are encoded as 2 UTF-16 code units

    private const char HighSurrogateStart = '\ud800';
    private const char LowSurrogateStart = '\udc00';
    private const int HighSurrogateRange = 0x3FF;

    private readonly uint _value;

    /// <summary>
    /// Creates a <see cref="Rune"/> from the provided Unicode scalar value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="value"/> does not represent a value Unicode scalar value.
    /// </exception>
    public Rune(uint value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a <see cref="Rune"/> from the provided Unicode scalar value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="value"/> does not represent a value Unicode scalar value.
    /// </exception>
    public Rune(int value)
        : this((uint)value)
    {
    }

    /// <summary>
    /// A <see cref="Rune"/> instance that represents the Unicode replacement character U+FFFD.
    /// </summary>
    public static Rune ReplacementChar => UnsafeCreate(UnicodeUtility.ReplacementChar);

    /// <summary>
    /// Returns true if and only if this scalar value is ASCII ([ U+0000..U+007F ])
    /// and therefore representable by a single UTF-8 code unit.
    /// </summary>
    public bool IsAscii => _value <= 0x7Fu;

    public uint Value => _value;

    /// <summary>
    /// Encodes this <see cref="Rune"/> to a destination buffer as UTF-8 bytes.
    /// </summary>
    /// <param name="destination">The buffer to which to write this value as UTF-8.</param>
    /// <returns> The number of <see cref="byte"/>s written to <paramref name="destination"/>,
    /// or 0 if the destination buffer is not large enough to contain the output.</returns>
    public int EncodeToUtf8(Span<byte> destination)
    {
        // The bit patterns below come from the Unicode Standard, Table 3-6.

        if (destination.Length >= 1)
        {
            if (IsAscii)
            {
                destination[0] = (byte)_value;
                return 1;
            }

            if (destination.Length >= 2)
            {
                if (_value <= 0x7FFu)
                {
                    // Scalar 00000yyy yyxxxxxx -> bytes [ 110yyyyy 10xxxxxx ]
                    destination[0] = (byte)((_value + (0b110u << 11)) >> 6);
                    destination[1] = (byte)((_value & 0x3Fu) + 0x80u);
                    return 2;
                }

                if (destination.Length >= 3)
                {
                    if (_value <= 0xFFFFu)
                    {
                        // Scalar zzzzyyyy yyxxxxxx -> bytes [ 1110zzzz 10yyyyyy 10xxxxxx ]
                        destination[0] = (byte)((_value + (0b1110 << 16)) >> 12);
                        destination[1] = (byte)(((_value & (0x3Fu << 6)) >> 6) + 0x80u);
                        destination[2] = (byte)((_value & 0x3Fu) + 0x80u);
                        return 3;
                    }

                    if (destination.Length >= 4)
                    {
                        // Scalar 000uuuuu zzzzyyyy yyxxxxxx -> bytes [ 11110uuu 10uuzzzz 10yyyyyy 10xxxxxx ]
                        destination[0] = (byte)((_value + (0b11110 << 21)) >> 18);
                        destination[1] = (byte)(((_value & (0x3Fu << 12)) >> 12) + 0x80u);
                        destination[2] = (byte)(((_value & (0x3Fu << 6)) >> 6) + 0x80u);
                        destination[3] = (byte)((_value & 0x3Fu) + 0x80u);
                        return 4;
                    }
                }
            }
        }

        // Destination buffer not large enough
        return 0;
    }

    /// <summary>
    /// Decodes the <see cref="Rune"/> at the beginning of the provided UTF-16 source buffer.
    /// </summary>
    /// <returns>
    /// <para>
    /// If the source buffer begins with a valid UTF-16 encoded scalar value, returns <see cref="OperationStatus.Done"/>,
    /// and outs via <paramref name="result"/> the decoded <see cref="Rune"/> and via <paramref name="charsConsumed"/> the
    /// number of <see langword="char"/>s used in the input buffer to encode the <see cref="Rune"/>.
    /// </para>
    /// <para>
    /// If the source buffer is empty or contains only a standalone UTF-16 high surrogate character, returns <see cref="OperationStatus.NeedMoreData"/>,
    /// and outs via <paramref name="result"/> <see cref="ReplacementChar"/> and via <paramref name="charsConsumed"/> the length of the input buffer.
    /// </para>
    /// <para>
    /// If the source buffer begins with an ill-formed UTF-16 encoded scalar value, returns <see cref="OperationStatus.InvalidData"/>,
    /// and outs via <paramref name="result"/> <see cref="ReplacementChar"/> and via <paramref name="charsConsumed"/> the number of
    /// <see langword="char"/>s used in the input buffer to encode the ill-formed sequence.
    /// </para>
    /// </returns>
    /// <remarks>
    /// The general calling convention is to call this method in a loop, slicing the <paramref name="source"/> buffer by
    /// <paramref name="charsConsumed"/> elements on each iteration of the loop. On each iteration of the loop <paramref name="result"/>
    /// will contain the real scalar value if successfully decoded, or it will contain <see cref="ReplacementChar"/> if
    /// the data could not be successfully decoded. This pattern provides convenient automatic U+FFFD substitution of
    /// invalid sequences while iterating through the loop.
    /// </remarks>
    public static OperationStatus DecodeFromUtf16(ReadOnlySpan<char> source, out Rune result, out int charsConsumed)
    {
        if (!source.IsEmpty)
        {
            // First, check for the common case of a BMP scalar value.
            // If this is correct, return immediately.

            char firstChar = source[0];
            if (TryCreate(firstChar, out result))
            {
                charsConsumed = 1;
                return OperationStatus.Done;
            }

            // First thing we saw was a UTF-16 surrogate code point.
            // Let's optimistically assume for now it's a high surrogate and hope
            // that combining it with the next char yields useful results.

            if (source.Length > 1)
            {
                char secondChar = source[1];
                if (TryCreate(firstChar, secondChar, out result))
                {
                    // Success! Formed a supplementary scalar value.
                    charsConsumed = 2;
                    return OperationStatus.Done;
                }
                else
                {
                    // Either the first character was a low surrogate, or the second
                    // character was not a low surrogate. This is an error.
                    goto InvalidData;
                }
            }
            else if (!char.IsHighSurrogate(firstChar))
            {
                // Quick check to make sure we're not going to report NeedMoreData for
                // a single-element buffer where the data is a standalone low surrogate
                // character. Since no additional data will ever make this valid, we'll
                // report an error immediately.
                goto InvalidData;
            }
        }

        // If we got to this point, the input buffer was empty, or the buffer
        // was a single element in length and that element was a high surrogate char.

        charsConsumed = source.Length;
        result = ReplacementChar;
        return OperationStatus.NeedMoreData;

    InvalidData:

        charsConsumed = 1; // maximal invalid subsequence for UTF-16 is always a single code unit in length
        result = ReplacementChar;
        return OperationStatus.InvalidData;
    }


    /// <summary>
    /// Attempts to create a <see cref="Rune"/> from the provided input value.
    /// </summary>
    public static bool TryCreate(char ch, out Rune result)
    {
        uint extendedValue = ch;
        if (!UnicodeUtility.IsSurrogateCodePoint(extendedValue))
        {
            result = UnsafeCreate(extendedValue);
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to create a <see cref="Rune"/> from the provided UTF-16 surrogate pair.
    /// Returns <see langword="false"/> if the input values don't represent a well-formed UTF-16surrogate pair.
    /// </summary>
    public static bool TryCreate(char highSurrogate, char lowSurrogate, out Rune result)
    {
        // First, extend both to 32 bits, then calculate the offset of
        // each candidate surrogate char from the start of its range.

        uint highSurrogateOffset = (uint)highSurrogate - HighSurrogateStart;
        uint lowSurrogateOffset = (uint)lowSurrogate - LowSurrogateStart;

        // This is a single comparison which allows us to check both for validity at once since
        // both the high surrogate range and the low surrogate range are the same length.
        // If the comparison fails, we call to a helper method to throw the correct exception message.

        if ((highSurrogateOffset | lowSurrogateOffset) <= HighSurrogateRange)
        {
            // The 0x40u << 10 below is to account for uuuuu = wwww + 1 in the surrogate encoding.
            result = UnsafeCreate((highSurrogateOffset << 10) + ((uint)lowSurrogate - LowSurrogateStart) + (0x40u << 10));
            return true;
        }
        else
        {
            // Didn't have a high surrogate followed by a low surrogate.
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Creates a <see cref="Rune"/> without performing validation on the input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Rune UnsafeCreate(uint scalarValue) => new Rune(scalarValue);
}

#endif