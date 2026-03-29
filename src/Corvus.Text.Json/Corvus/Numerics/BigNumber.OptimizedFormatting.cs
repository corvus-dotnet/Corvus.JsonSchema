// <copyright file="BigNumber.OptimizedFormatting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Numerics;

/// <summary>
/// Zero-allocation formatting methods for <see cref="BigNumber"/>.
/// </summary>
public readonly partial struct BigNumber
{
    /// <summary>
    /// Tries to format this instance into the provided UTF-16 span with zero allocations.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <param name="format">The format string.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormatOptimized(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        BigNumber normalized = Normalize();

        // Handle empty format - output raw JSON format (significand + 'E' + exponent)
        if (format.IsEmpty)
        {
            return TryFormatRaw(normalized, destination, out charsWritten, provider);
        }

        bool isNegative = normalized.Significand.Sign < 0;

        // Calculate max digits needed
        int maxDigits = normalized.Significand.IsZero ? 1 : (int)(BigInteger.Log10(BigInteger.Abs(normalized.Significand)) + 2);

        // Use destination.Length as upper bound since we know bytes needed <= chars available
        int bufferSize = Math.Min(maxDigits, destination.Length);

        byte[]? rentedBuffer = null;
        Span<byte> significandBuffer = bufferSize <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize)).AsSpan(0, bufferSize);

        try
        {
            if (!TryFormatBigIntegerUtf8(BigInteger.Abs(normalized.Significand), significandBuffer, out int bytesWritten))
            {
                charsWritten = 0;
                return false;
            }

            return JsonElementHelpers.TryFormatNumber(
                ReadOnlySpan<byte>.Empty,
                destination,
                out charsWritten,
                format,
                provider,
                isNegative,
                significandBuffer.Slice(0, bytesWritten),
                ReadOnlySpan<byte>.Empty,
                normalized.Exponent);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private static bool TryFormatRaw(BigNumber normalized, Span<char> destination, out int charsWritten, IFormatProvider? provider)
    {
        if (normalized.Significand.IsZero)
        {
            if (destination.Length < 1)
            {
                charsWritten = 0;
                return false;
            }

            destination[0] = '0';
            charsWritten = 1;
            return true;
        }

        // Format: significand + 'E' + exponent (if exponent != 0)
        if (normalized.Exponent == 0)
        {
            return normalized.Significand.TryFormat(destination, out charsWritten, provider: provider);
        }

        // Try formatting significand
        if (!normalized.Significand.TryFormat(destination, out int sigChars, provider: provider))
        {
            charsWritten = 0;
            return false;
        }

        // Add 'E'
        if (sigChars >= destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        destination[sigChars] = 'E';
        sigChars++;

        // Add exponent
        if (!normalized.Exponent.TryFormat(destination.Slice(sigChars), out int expChars, provider: provider))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = sigChars + expChars;
        return true;
    }

    /// <summary>
    /// Tries to format this instance into the provided UTF-8 span with zero allocations.
    /// </summary>
    /// <param name="utf8Destination">The destination span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <param name="format">The format string.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormatUtf8Optimized(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        BigNumber normalized = Normalize();

        // Handle empty format - output raw JSON format (significand + 'E' + exponent)
        if (format.IsEmpty)
        {
            return TryFormatRawUtf8(normalized, utf8Destination, out bytesWritten, provider);
        }

        bool isNegative = normalized.Significand.Sign < 0;

        // Calculate max digits needed
        int maxDigits = normalized.Significand.IsZero ? 1 : (int)(BigInteger.Log10(BigInteger.Abs(normalized.Significand)) + 2);

        // Use destination.Length as upper bound
        int bufferSize = Math.Min(maxDigits, utf8Destination.Length);

        byte[]? rentedBuffer = null;
        Span<byte> significandBuffer = bufferSize <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize)).AsSpan(0, bufferSize);

        try
        {
            if (!TryFormatBigIntegerUtf8(BigInteger.Abs(normalized.Significand), significandBuffer, out int signBytes))
            {
                bytesWritten = 0;
                return false;
            }

            return JsonElementHelpers.TryFormatNumber(
                ReadOnlySpan<byte>.Empty,
                utf8Destination,
                out bytesWritten,
                format,
                provider,
                isNegative,
                significandBuffer.Slice(0, signBytes),
                ReadOnlySpan<byte>.Empty,
                normalized.Exponent);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private static bool TryFormatRawUtf8(BigNumber normalized, Span<byte> destination, out int bytesWritten, IFormatProvider? provider)
    {
        if (normalized.Significand.IsZero)
        {
            if (destination.Length < 1)
            {
                bytesWritten = 0;
                return false;
            }

            destination[0] = (byte)'0';
            bytesWritten = 1;
            return true;
        }

        // Format: significand + 'E' + exponent (if exponent != 0)
        if (normalized.Exponent == 0)
        {
            return TryFormatBigIntegerUtf8(normalized.Significand, destination, out bytesWritten);
        }

        // Try formatting significand
        if (!TryFormatBigIntegerUtf8(normalized.Significand, destination, out int sigBytes))
        {
            bytesWritten = 0;
            return false;
        }

        // Add 'E'
        if (sigBytes >= destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        destination[sigBytes] = (byte)'E';
        sigBytes++;

        // Add exponent
        if (!TryFormatInt32Utf8(normalized.Exponent, destination.Slice(sigBytes), out int expBytes))
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = sigBytes + expBytes;
        return true;
    }

    private static bool TryFormatInt32Utf8(int value, Span<byte> destination, out int bytesWritten)
    {
#if NET
        return value.TryFormat(destination, out bytesWritten);
#else
        // For older frameworks, format to char span then transcode
        Span<char> charBuffer = stackalloc char[11]; // Max int32 length is 11 chars
        if (!value.TryFormat(charBuffer, out int charsWritten))
        {
            bytesWritten = 0;
            return false;
        }

        if (charsWritten > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < charsWritten; i++)
        {
            destination[i] = (byte)charBuffer[i];
        }

        bytesWritten = charsWritten;
        return true;
#endif
    }

    /// <summary>
    /// Formats a BigInteger to UTF-8 bytes (ASCII digits only).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryFormatBigIntegerUtf8(BigInteger value, Span<byte> destination, out int bytesWritten)
    {
        // Number of chars required is always <= number of bytes in destination
        // We can fail early if destination is too small for even a char buffer
        int bufferSize = destination.Length;

        char[]? rentedBuffer = null;
        try
        {
            Span<char> charBuffer = bufferSize <= JsonConstants.StackallocCharThreshold
                ? stackalloc char[JsonConstants.StackallocCharThreshold]
                : (rentedBuffer = ArrayPool<char>.Shared.Rent(bufferSize)).AsSpan(0, bufferSize);

            if (!value.TryFormat(charBuffer, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                bytesWritten = 0;
                return false;
            }

            return TryConvertAsciiToUtf8(charBuffer.Slice(0, charsWritten), destination, out bytesWritten);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Tries to parse a BigNumber from UTF-8 bytes in JSON format with zero allocations.
    /// </summary>
    /// <param name="utf8Source">The UTF-8 bytes to parse.</param>
    /// <param name="result">The parsed BigNumber.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// This method is optimized for parsing JSON-formatted numbers with InvariantCulture semantics.
    /// It expects input in formats like: "123", "-456", "1234E-3", "1234E2", "0".
    /// </para>
    /// <para>
    /// The method parses directly from UTF-8 bytes without conversion to chars,
    /// maintaining zero heap allocations for typical numbers.
    /// </para>
    /// </remarks>
    public static bool TryParseJsonUtf8(ReadOnlySpan<byte> utf8Source, out BigNumber result)
    {
        // Fast path: Empty input
        if (utf8Source.IsEmpty)
        {
            result = Zero;
            return false;
        }

        // Fast path: Single zero
        if (utf8Source.Length == 1 && utf8Source[0] == (byte)'0')
        {
            result = Zero;
            return true;
        }

        // Parse directly from UTF-8 bytes
        return TryParseUtf8Core(utf8Source, out result);
    }

    /// <summary>
    /// Core UTF-8 parsing logic that operates directly on UTF-8 bytes.
    /// </summary>
    private static bool TryParseUtf8Core(ReadOnlySpan<byte> utf8Source, out BigNumber result)
    {
        int position = 0;

        // Skip leading whitespace
        while (position < utf8Source.Length && IsWhitespace(utf8Source[position]))
        {
            position++;
        }

        if (position >= utf8Source.Length)
        {
            result = Zero;
            return false;
        }

        // Parse sign
        bool isNegative = false;
        if (utf8Source[position] == (byte)'-')
        {
            isNegative = true;
            position++;
        }
        else if (utf8Source[position] == (byte)'+')
        {
            position++;
        }

        if (position >= utf8Source.Length)
        {
            result = Zero;
            return false;
        }

        // Parse significand (integer and decimal parts)
        BigInteger significand = BigInteger.Zero;
        int exponent = 0;
        bool hasDigits = false;
        bool inDecimalPart = false;
        int decimalPlaces = 0;

        // Parse digits before and after decimal point
        while (position < utf8Source.Length)
        {
            byte b = utf8Source[position];

            if (b >= (byte)'0' && b <= (byte)'9')
            {
                hasDigits = true;
                int digit = b - (byte)'0';
                significand = significand * 10 + digit;

                if (inDecimalPart)
                {
                    decimalPlaces++;
                }

                position++;
            }
            else if (b == (byte)'.' && !inDecimalPart)
            {
                inDecimalPart = true;
                position++;
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
        {
            result = Zero;
            return false;
        }

        // Account for decimal places
        exponent -= decimalPlaces;

        // Parse exponent if present
        if (position < utf8Source.Length && (utf8Source[position] == (byte)'E' || utf8Source[position] == (byte)'e'))
        {
            position++;

            if (position >= utf8Source.Length)
            {
                result = Zero;
                return false;
            }

            // Parse exponent sign
            bool expNegative = false;
            if (utf8Source[position] == (byte)'-')
            {
                expNegative = true;
                position++;
            }
            else if (utf8Source[position] == (byte)'+')
            {
                position++;
            }

            if (position >= utf8Source.Length)
            {
                result = Zero;
                return false;
            }

            // Parse exponent digits
            int parsedExponent = 0;
            bool hasExpDigits = false;

            while (position < utf8Source.Length)
            {
                byte b = utf8Source[position];

                if (b >= (byte)'0' && b <= (byte)'9')
                {
                    hasExpDigits = true;
                    parsedExponent = parsedExponent * 10 + (b - (byte)'0');
                    position++;
                }
                else
                {
                    break;
                }
            }

            if (!hasExpDigits)
            {
                result = Zero;
                return false;
            }

            if (expNegative)
            {
                parsedExponent = -parsedExponent;
            }

            exponent += parsedExponent;
        }

        // Skip trailing whitespace
        while (position < utf8Source.Length && IsWhitespace(utf8Source[position]))
        {
            position++;
        }

        // Check for unconsumed input
        if (position != utf8Source.Length)
        {
            result = Zero;
            return false;
        }

        // Apply sign
        if (isNegative)
        {
            significand = -significand;
        }

        result = new BigNumber(significand, exponent);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(byte b)
    {
        // JSON whitespace: space, tab, newline, carriage return
        return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r';
    }

    /// <summary>
    /// Converts ASCII-only chars to UTF-8 bytes (direct copy for ASCII).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertAsciiToUtf8(ReadOnlySpan<char> source, Span<byte> destination, out int bytesWritten)
    {
        if (source.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        // Direct conversion for ASCII characters (0-127)
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            // All numeric output is ASCII (digits, minus, E)
            if (c > 127)
            {
                bytesWritten = 0;
                return false;
            }

            destination[i] = (byte)c;
        }

        bytesWritten = source.Length;
        return true;
    }

    /// <summary>
    /// Estimates the number of digits needed for an int.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NumberOfDigits(int value)
    {
        if (value == 0)
            return 1;

        int digits = value < 0 ? 1 : 0; // Sign
        value = Math.Abs(value);

        // Fast digit count
        return value switch
        {
            < 10 => digits + 1,
            < 100 => digits + 2,
            < 1000 => digits + 3,
            < 10000 => digits + 4,
            < 100000 => digits + 5,
            < 1000000 => digits + 6,
            < 10000000 => digits + 7,
            < 100000000 => digits + 8,
            < 1000000000 => digits + 9,
            _ => digits + 10
        };
    }
}