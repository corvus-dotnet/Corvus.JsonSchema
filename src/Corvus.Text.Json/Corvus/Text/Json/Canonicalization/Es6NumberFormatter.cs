// <copyright file="Es6NumberFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

        // Get the shortest round-trip representation from .NET's Grisu3 implementation
        string r = GetShortestRepresentation(abs);

        // Parse the round-trip string into significant digits and exponent
        ParseRoundTripString(r, out ReadOnlySpan<char> digits, out int n);

        // Format according to ECMA-262 §7.1.12.1
        return FormatEs6(negative, digits, n, destination, ref bytesWritten);
    }

    private static string GetShortestRepresentation(double abs)
    {
#if NET
        // On .NET Core 3.0+, ToString("R") uses Grisu3 — produces shortest round-trip representation.
        return abs.ToString("R", CultureInfo.InvariantCulture);
#else
        // On .NET Framework, ToString("R") can produce too few digits.
        // Use G17 (always enough digits) and verify round-trip, then try to shorten.
        string g17 = abs.ToString("G17", CultureInfo.InvariantCulture);

        // Try "R" first — if it round-trips, it's shorter.
        string r = abs.ToString("R", CultureInfo.InvariantCulture);
        if (double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture) == abs)
        {
            return r;
        }

        return g17;
#endif
    }

    /// <summary>
    /// Parses a .NET round-trip string (e.g., "1.23E+5", "0.002", "56") into significant digits
    /// and the ES6 'n' value (decimal position).
    /// </summary>
    private static void ParseRoundTripString(string r, out ReadOnlySpan<char> digits, out int n)
    {
        // Find optional exponent
        int eIdx = r.IndexOf('E');
        ReadOnlySpan<char> mantissaSpan;
        int explicitExp = 0;

        if (eIdx >= 0)
        {
            mantissaSpan = r.AsSpan(0, eIdx);
#if NETSTANDARD2_0
            explicitExp = int.Parse(r.Substring(eIdx + 1), CultureInfo.InvariantCulture);
#else
            explicitExp = int.Parse(r.AsSpan(eIdx + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
#endif
        }
        else
        {
            mantissaSpan = r.AsSpan();
        }

        // Find decimal point position in mantissa
        int dotIdx = mantissaSpan.IndexOf('.');
        int intPartLen = dotIdx >= 0 ? dotIdx : mantissaSpan.Length;

        // Extract all digit characters (skip '.')
        Span<char> allDigits = stackalloc char[mantissaSpan.Length];
        int digitCount = 0;
        for (int i = 0; i < mantissaSpan.Length; i++)
        {
            if (mantissaSpan[i] != '.')
            {
                allDigits[digitCount++] = mantissaSpan[i];
            }
        }

        // Find first non-zero digit (skip leading zeros from values like "0.002")
        int firstNonZero = 0;
        while (firstNonZero < digitCount && allDigits[firstNonZero] == '0')
        {
            firstNonZero++;
        }

        digits = new string(allDigits.Slice(firstNonZero, digitCount - firstNonZero).ToArray()).AsSpan();

        // n = decimal position in ES6 terms (ECMA-262 §7.1.12.1)
        // n satisfies: significand × 10^(n - k) = value, where k = number of significant digits
        n = intPartLen + explicitExp - firstNonZero;
    }

    /// <summary>
    /// Formats significant digits according to ECMA-262 §7.1.12.1 rules.
    /// </summary>
    private static bool FormatEs6(
        bool negative,
        ReadOnlySpan<char> digits,
        int n,
        Span<byte> destination,
        ref int bytesWritten)
    {
        int k = digits.Length;

        // Calculate required size first
        int required = EstimateSize(negative, digits, n, k);
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
                destination[pos++] = (byte)digits[i];
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
                destination[pos++] = (byte)digits[i];
            }

            destination[pos++] = (byte)'.';

            for (int i = n; i < k; i++)
            {
                destination[pos++] = (byte)digits[i];
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
                destination[pos++] = (byte)digits[i];
            }
        }
        else
        {
            // Cases 4 & 5: exponential notation
            int exp = n - 1;

            destination[pos++] = (byte)digits[0];

            if (k > 1)
            {
                destination[pos++] = (byte)'.';
                for (int i = 1; i < k; i++)
                {
                    destination[pos++] = (byte)digits[i];
                }
            }

            destination[pos++] = (byte)'e';

            if (exp >= 0)
            {
                destination[pos++] = (byte)'+';
            }

            // Write exponent (may be negative; int.ToString handles the minus sign)
            pos += WriteInt(exp, destination.Slice(pos));
        }

        bytesWritten = pos;
        return true;
    }

    private static int EstimateSize(bool negative, ReadOnlySpan<char> digits, int n, int k)
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