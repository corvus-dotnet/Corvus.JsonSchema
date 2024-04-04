// <copyright file="RuneNetStandard20.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// </licensing>

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;

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
    /// Returns true if and only if this scalar value is ASCII ([ U+0000..U+007F ])
    /// and therefore representable by a single UTF-8 code unit.
    /// </summary>
    public bool IsAscii => _value <= 0x7Fu;

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
}