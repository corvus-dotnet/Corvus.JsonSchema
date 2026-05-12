// <copyright file="Utf8ValueStringBuilder.AppendHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace Corvus.Text;

internal ref partial struct Utf8ValueStringBuilder
{
    /// <summary>
    /// Appends a <see cref="long"/> value formatted as UTF-8 decimal digits
    /// using <see cref="Utf8Formatter"/>.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">The standard format to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(long value, StandardFormat format = default)
    {
        const int MaxInt64Digits = 20;
        int bytesWritten;
        while (!Utf8Formatter.TryFormat(value, _bytes.Slice(_pos), out bytesWritten, format))
        {
            Grow(MaxInt64Digits);
        }

        _pos += bytesWritten;
    }

    /// <summary>
    /// Appends an ASCII string as UTF-8 bytes (one byte per char).
    /// Only valid for strings containing exclusively ASCII characters (U+0000–U+007F).
    /// </summary>
    /// <param name="value">The ASCII string to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendAsciiString(string value)
    {
        Span<byte> dest = AppendSpan(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            dest[i] = (byte)value[i];
        }
    }

    /// <summary>
    /// Appends a single <see cref="char"/> encoded as UTF-8.
    /// Handles BMP characters (U+0000–U+FFFF); does not handle surrogate pairs.
    /// </summary>
    /// <param name="value">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendChar(char value)
    {
        if (value < 0x80)
        {
            Append((byte)value);
        }
        else if (value < 0x800)
        {
            Span<byte> dest = AppendSpan(2);
            dest[0] = (byte)(0xC0 | (value >> 6));
            dest[1] = (byte)(0x80 | (value & 0x3F));
        }
        else
        {
            Span<byte> dest = AppendSpan(3);
            dest[0] = (byte)(0xE0 | (value >> 12));
            dest[1] = (byte)(0x80 | ((value >> 6) & 0x3F));
            dest[2] = (byte)(0x80 | (value & 0x3F));
        }
    }
}