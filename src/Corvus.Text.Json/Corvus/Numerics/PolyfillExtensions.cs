// <copyright file="PolyfillExtensions.cs" company="endjin">
// Copyright (c) endjin. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#if !NET

using System.Globalization;
using System.Numerics;
using System.Text;

namespace Corvus.Numerics;

/// <summary>
/// Extension members to polyfill TryFormat methods for netstandard2.0.
/// </summary>
internal static class PolyfillExtensions
{
    /// <summary>
    /// Polyfill for BigInteger.TryFormat on netstandard2.0.
    /// </summary>
    public static bool TryFormat(
        this BigInteger value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        // Format to string then copy to span
        string formatted;
        if (format.IsEmpty)
        {
            formatted = value.ToString(provider);
        }
        else if (format.Length == 1)
        {
            // Optimize single-character formats to avoid allocation
            formatted = format[0] switch
            {
                'G' or 'g' => value.ToString("G", provider),
                'D' or 'd' => value.ToString("D", provider),
                'X' or 'x' => value.ToString(format[0] == 'X' ? "X" : "x", provider),
                'N' or 'n' => value.ToString("N", provider),
                'E' or 'e' => value.ToString(format[0] == 'E' ? "E" : "e", provider),
                'F' or 'f' => value.ToString("F", provider),
                'P' or 'p' => value.ToString("P", provider),
                'C' or 'c' => value.ToString("C", provider),
                'R' or 'r' => value.ToString("R", provider),
                _ => value.ToString(format.ToString(), provider)
            };
        }
        else
        {
            formatted = value.ToString(format.ToString(), provider);
        }

        if (formatted.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        formatted.AsSpan().CopyTo(destination);
        charsWritten = formatted.Length;
        return true;
    }

    /// <summary>
    /// Polyfill for long.TryFormat on netstandard2.0.
    /// </summary>
    public static bool TryFormat(
        this long value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        // Format to string then copy to span
        string formatted;
        if (format.IsEmpty)
        {
            formatted = value.ToString(provider);
        }
        else if (format.Length == 1)
        {
            // Optimize single-character formats to avoid allocation
            formatted = format[0] switch
            {
                'G' or 'g' => value.ToString("G", provider),
                'D' or 'd' => value.ToString("D", provider),
                'X' or 'x' => value.ToString(format[0] == 'X' ? "X" : "x", provider),
                'N' or 'n' => value.ToString("N", provider),
                'E' or 'e' => value.ToString(format[0] == 'E' ? "E" : "e", provider),
                'F' or 'f' => value.ToString("F", provider),
                'P' or 'p' => value.ToString("P", provider),
                'C' or 'c' => value.ToString("C", provider),
                'R' or 'r' => value.ToString("R", provider),
                _ => value.ToString(format.ToString(), provider)
            };
        }
        else
        {
            formatted = value.ToString(format.ToString(), provider);
        }

        if (formatted.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        formatted.AsSpan().CopyTo(destination);
        charsWritten = formatted.Length;
        return true;
    }

    /// <summary>
    /// Polyfill for int.TryFormat on netstandard2.0.
    /// </summary>
    public static bool TryFormat(
        this int value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        // Format to string then copy to span
        string formatted;
        if (format.IsEmpty)
        {
            formatted = value.ToString(provider);
        }
        else if (format.Length == 1)
        {
            // Optimize single-character formats to avoid allocation
            formatted = format[0] switch
            {
                'G' or 'g' => value.ToString("G", provider),
                'D' or 'd' => value.ToString("D", provider),
                'X' or 'x' => value.ToString(format[0] == 'X' ? "X" : "x", provider),
                'N' or 'n' => value.ToString("N", provider),
                'E' or 'e' => value.ToString(format[0] == 'E' ? "E" : "e", provider),
                'F' or 'f' => value.ToString("F", provider),
                'P' or 'p' => value.ToString("P", provider),
                'C' or 'c' => value.ToString("C", provider),
                'R' or 'r' => value.ToString("R", provider),
                _ => value.ToString(format.ToString(), provider)
            };
        }
        else
        {
            formatted = value.ToString(format.ToString(), provider);
        }

        if (formatted.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        formatted.AsSpan().CopyTo(destination);
        charsWritten = formatted.Length;
        return true;
    }

    /// <summary>
    /// Polyfill for Encoding.GetBytes(ReadOnlySpan&lt;char&gt;, Span&lt;byte&gt;) on netstandard2.0.
    /// </summary>
    public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        // Convert to string and use array-based GetBytes
        string str = chars.ToString();
        byte[] encoded = encoding.GetBytes(str);

        if (encoded.Length > bytes.Length)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(bytes));
        }

        encoded.AsSpan().CopyTo(bytes);
        return encoded.Length;
    }

    /// <summary>
    /// Polyfill for Encoding.GetByteCount(ReadOnlySpan&lt;char&gt;) on netstandard2.0.
    /// </summary>
    public static int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
    {
        string str = chars.ToString();
        return encoding.GetByteCount(str);
    }

    /// <summary>
    /// Polyfill for Encoding.GetChars(ReadOnlySpan&lt;byte&gt;, Span&lt;char&gt;) on netstandard2.0.
    /// </summary>
    public static int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
    {
        byte[] byteArray = bytes.ToArray();
        char[] charArray = encoding.GetChars(byteArray);

        if (charArray.Length > chars.Length)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(chars));
        }

        charArray.AsSpan().CopyTo(chars);
        return charArray.Length;
    }

    /// <summary>
    /// Polyfill for ReadOnlySpan&lt;char&gt;.Contains on netstandard2.0.
    /// </summary>
    public static bool Contains(this ReadOnlySpan<char> span, char value)
    {
        return span.IndexOf(value) >= 0;
    }

    /// <summary>
    /// Polyfill for Encoding.GetString(ReadOnlySpan&lt;byte&gt;) on netstandard2.0.
    /// </summary>
    public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        byte[] byteArray = bytes.ToArray();
        return encoding.GetString(byteArray);
    }

    /// <summary>
    /// Polyfill for BigInteger.GetBitLength() on netstandard2.0.
    /// </summary>
    public static long GetBitLength(this BigInteger value)
    {
        // Approximation using byte array length
        if (value.IsZero)
        {
            return 0;
        }

        byte[] bytes = BigInteger.Abs(value).ToByteArray();
        long bitLength = (bytes.Length - 1) * 8;

        // Count bits in the most significant byte
        byte msb = bytes[bytes.Length - 1];
        while (msb > 0)
        {
            msb >>= 1;
            bitLength++;
        }

        return bitLength;
    }
}

#endif