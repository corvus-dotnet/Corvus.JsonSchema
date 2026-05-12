// <copyright file="Es6NumberFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;

namespace Corvus.Text.Json.Canonicalization;

/// <summary>
/// Formats IEEE 754 double-precision values using the ECMAScript 6 <c>Number.prototype.toString()</c>
/// algorithm (ECMA-262 §7.1.12.1).
/// </summary>
/// <remarks>
/// <para>
/// This formatter produces the shortest decimal representation that round-trips back to the
/// same double value, formatted according to ES6 rules:
/// </para>
/// <list type="bullet">
/// <item><description>Negative zero is output as <c>"0"</c>.</description></item>
/// <item><description>Integers in [1, 10²¹) use fixed notation without a decimal point.</description></item>
/// <item><description>Non-integers in (10⁻⁷, 10²¹) use fixed notation with a decimal point.</description></item>
/// <item><description>Values outside these ranges use exponential notation with lowercase <c>'e'</c>
/// and explicit <c>'+'</c> for positive exponents.</description></item>
/// </list>
/// </remarks>
internal static class Es6NumberFormatter
{
    /// <summary>
    /// Formats a double value as ES6 number representation and writes UTF-8 bytes to the destination.
    /// </summary>
    /// <param name="value">The value to format. Must be finite (not NaN or Infinity).</param>
    /// <param name="destination">The buffer to write to.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the value was successfully written; <see langword="false"/>
    /// if the destination is too small.</returns>
    public static bool TryFormat(double value, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;

        // Handle zero (both +0 and -0)
        if (value == 0.0)
        {
            if (destination.Length < 1)
            {
                return false;
            }

            destination[0] = (byte)'0';
            bytesWritten = 1;
            return true;
        }

        bool negative = value < 0;
        double abs = negative ? -value : value;

        // Get the shortest round-trip representation into a byte buffer
        Span<byte> rtBuffer = stackalloc byte[32];
        int rtLength = GetShortestRepresentationBytes(abs, rtBuffer);
        ReadOnlySpan<byte> representation = rtBuffer.Slice(0, rtLength);

        // Parse the round-trip bytes into significant digits and exponent
        Span<byte> digitBuffer = stackalloc byte[24];
        ParseRoundTripBytes(representation, digitBuffer, out int digitCount, out int n);
        ReadOnlySpan<byte> digits = digitBuffer.Slice(0, digitCount);

        // Format according to ECMA-262 §7.1.12.1
        return FormatEs6(negative, digits, n, destination, ref bytesWritten);
    }

    private static int GetShortestRepresentationBytes(double abs, Span<byte> buffer)
    {
#if NET
        // On .NET Core 3.0+, Utf8Formatter with default 'G' format uses Grisu3 — shortest round-trip.
        if (!Utf8Formatter.TryFormat(abs, buffer, out int written))
        {
            throw new InvalidOperationException("Buffer too small for double formatting.");
        }

        return written;
#else
        // On .NET Framework, ToString("R") can produce too many digits and
        // doesn't always give the shortest round-trip representation.
        // Find the shortest representation by trying precisions from 1 upward.
        // ES6 requires the minimum number of digits such that the value round-trips.
        string chosen = abs.ToString("G17", CultureInfo.InvariantCulture);
        for (int precision = 1; precision <= 16; precision++)
        {
            string candidate = abs.ToString("G" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

            // Use TryParse — Parse throws OverflowException on .NET Framework for values > MaxValue
            if (double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && parsed == abs)
            {
                chosen = candidate;
                break;
            }
        }

        // Copy ASCII chars to byte buffer (number characters are always ASCII)
        for (int i = 0; i < chosen.Length; i++)
        {
            buffer[i] = (byte)chosen[i];
        }

        return chosen.Length;
#endif
    }

    /// <summary>
    /// Parses a round-trip byte representation (e.g., "1.23E+5", "0.002", "56") into
    /// significant digits and the ES6 'n' value (decimal position).
    /// </summary>
    private static void ParseRoundTripBytes(
        ReadOnlySpan<byte> representation,
        Span<byte> digitBuffer,
        out int digitCount,
        out int n)
    {
        // Find optional exponent
        int eIdx = representation.IndexOf((byte)'E');
        ReadOnlySpan<byte> mantissa;
        int explicitExp = 0;

        if (eIdx >= 0)
        {
            mantissa = representation.Slice(0, eIdx);
            explicitExp = ParseIntFromBytes(representation.Slice(eIdx + 1));
        }
        else
        {
            mantissa = representation;
        }

        // Find decimal point position in mantissa
        int dotIdx = mantissa.IndexOf((byte)'.');
        int intPartLen = dotIdx >= 0 ? dotIdx : mantissa.Length;

        // Extract all digit bytes (skip '.')
        Span<byte> allDigits = stackalloc byte[mantissa.Length];
        int allDigitCount = 0;
        for (int i = 0; i < mantissa.Length; i++)
        {
            if (mantissa[i] != (byte)'.')
            {
                allDigits[allDigitCount++] = mantissa[i];
            }
        }

        // Find first non-zero digit (skip leading zeros from values like "0.002")
        int firstNonZero = 0;
        while (firstNonZero < allDigitCount && allDigits[firstNonZero] == (byte)'0')
        {
            firstNonZero++;
        }

        // Copy significant digits to output buffer
        digitCount = allDigitCount - firstNonZero;
        allDigits.Slice(firstNonZero, digitCount).CopyTo(digitBuffer);

        // n = decimal position in ES6 terms (ECMA-262 §7.1.12.1)
        // n satisfies: significand × 10^(n - k) = value, where k = number of significant digits
        n = intPartLen + explicitExp - firstNonZero;
    }

    private static int ParseIntFromBytes(ReadOnlySpan<byte> bytes)
    {
        bool negative = false;
        int start = 0;

        if (bytes.Length > 0 && bytes[0] == (byte)'+')
        {
            start = 1;
        }
        else if (bytes.Length > 0 && bytes[0] == (byte)'-')
        {
            negative = true;
            start = 1;
        }

        int result = 0;
        for (int i = start; i < bytes.Length; i++)
        {
            result = (result * 10) + (bytes[i] - '0');
        }

        return negative ? -result : result;
    }

    /// <summary>
    /// Formats significant digits according to ECMA-262 §7.1.12.1 rules.
    /// </summary>
    private static bool FormatEs6(
        bool negative,
        ReadOnlySpan<byte> digits,
        int n,
        Span<byte> destination,
        ref int bytesWritten)
    {
        int k = digits.Length;

        // Calculate required size first
        int required = EstimateSize(negative, k, n);
        if (required > destination.Length)
        {
            return false;
        }

        int pos = 0;

        if (negative)
        {
            destination[pos++] = (byte)'-';
        }

        if (k <= n && n <= 21)
        {
            // Case 1: integer representation with trailing zeros
            // e.g., digits="1", n=21 → "100000000000000000000"
            for (int i = 0; i < k; i++)
            {
                destination[pos++] = digits[i];
            }

            for (int i = 0; i < n - k; i++)
            {
                destination[pos++] = (byte)'0';
            }
        }
        else if (0 < n && n <= 21)
        {
            // Case 2: fixed notation with decimal point
            // e.g., digits="45", n=1 → "4.5"
            for (int i = 0; i < n; i++)
            {
                destination[pos++] = digits[i];
            }

            destination[pos++] = (byte)'.';

            for (int i = n; i < k; i++)
            {
                destination[pos++] = digits[i];
            }
        }
        else if (-6 < n && n <= 0)
        {
            // Case 3: "0." followed by leading zeros
            // e.g., digits="2", n=-2 → "0.002"
            destination[pos++] = (byte)'0';
            destination[pos++] = (byte)'.';

            for (int i = 0; i < -n; i++)
            {
                destination[pos++] = (byte)'0';
            }

            for (int i = 0; i < k; i++)
            {
                destination[pos++] = digits[i];
            }
        }
        else
        {
            // Cases 4 & 5: exponential notation
            int exp = n - 1;

            destination[pos++] = digits[0];

            if (k > 1)
            {
                destination[pos++] = (byte)'.';
                for (int i = 1; i < k; i++)
                {
                    destination[pos++] = digits[i];
                }
            }

            destination[pos++] = (byte)'e';

            if (exp >= 0)
            {
                destination[pos++] = (byte)'+';
            }

            // Write exponent
            pos += WriteInt(exp, destination.Slice(pos));
        }

        bytesWritten = pos;
        return true;
    }

    private static int EstimateSize(bool negative, int k, int n)
    {
        int size = negative ? 1 : 0;

        if (k <= n && n <= 21)
        {
            size += n; // digits + trailing zeros
        }
        else if (0 < n && n <= 21)
        {
            size += k + 1; // digits + decimal point
        }
        else if (-6 < n && n <= 0)
        {
            size += 2 + (-n) + k; // "0." + leading zeros + digits
        }
        else
        {
            int exp = n - 1;
            size++; // first digit
            if (k > 1)
            {
                size += 1 + (k - 1); // '.' + remaining digits
            }

            size++; // 'e'
            size += exp >= 0 ? 1 : 0; // '+' for positive exponents (negative handled by int)
            size += CountIntDigits(exp); // exponent digits
        }

        return size;
    }

    private static int WriteInt(int value, Span<byte> destination)
    {
        if (value < 0)
        {
            destination[0] = (byte)'-';
            return 1 + WriteUInt(-value, destination.Slice(1));
        }

        return WriteUInt(value, destination);
    }

    private static int WriteUInt(int value, Span<byte> destination)
    {
        if (value == 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        int digits = CountIntDigitsUnsigned(value);
        for (int i = digits - 1; i >= 0; i--)
        {
            destination[i] = (byte)('0' + (value % 10));
            value /= 10;
        }

        return digits;
    }

    private static int CountIntDigits(int value)
    {
        if (value < 0)
        {
            return 1 + CountIntDigitsUnsigned(-value); // '-' + digits
        }

        return CountIntDigitsUnsigned(value);
    }

    private static int CountIntDigitsUnsigned(int value)
    {
        if (value == 0)
        {
            return 1;
        }

        int count = 0;
        while (value > 0)
        {
            count++;
            value /= 10;
        }

        return count;
    }
}