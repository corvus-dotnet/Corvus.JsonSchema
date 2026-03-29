// <copyright file="ParsingPolyfills.cs" company="endjin">
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

namespace Corvus.Numerics;

/// <summary>
/// Polyfills for span-based parsing to avoid allocations in netstandard2.0.
/// </summary>
internal static class ParsingPolyfills
{
    /// <summary>
    /// Tries to parse a span of characters as a 32-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="span">The span of characters to parse.</param>
    /// <param name="value">The parsed value, if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseInt32Invariant(ReadOnlySpan<char> span, out int value)
    {
        value = 0;

        if (span.IsEmpty)
        {
            return false;
        }

        int index = 0;
        int sign = 1;

        // Skip leading whitespace
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }

        if (index >= span.Length)
        {
            return false;
        }

        // Handle sign
        if (span[index] == '-')
        {
            sign = -1;
            index++;
        }
        else if (span[index] == '+')
        {
            index++;
        }

        if (index >= span.Length)
        {
            return false;
        }

        // Parse digits
        bool hasDigits = false;
        long result = 0; // Use long to detect overflow

        while (index < span.Length)
        {
            char c = span[index];

            if (c < '0' || c > '9')
            {
                break;
            }

            int digit = c - '0';
            result = (result * 10) + digit;

            // Check for overflow
            if (result > int.MaxValue)
            {
                return false;
            }

            hasDigits = true;
            index++;
        }

        if (!hasDigits)
        {
            return false;
        }

        // Skip trailing whitespace
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }

        // Should have consumed entire span
        if (index < span.Length)
        {
            return false;
        }

        value = (int)(result * sign);

        // Check for negative overflow
        return sign != -1 || result <= -(long)int.MinValue;
    }

    /// <summary>
    /// Tries to parse a span of characters as a 64-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="span">The span of characters to parse.</param>
    /// <param name="value">The parsed value, if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseInt64Invariant(ReadOnlySpan<char> span, out long value)
    {
        value = 0;

        if (span.IsEmpty)
        {
            return false;
        }

        int index = 0;
        int sign = 1;

        // Skip leading whitespace
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }

        if (index >= span.Length)
        {
            return false;
        }

        // Handle sign
        if (span[index] == '-')
        {
            sign = -1;
            index++;
        }
        else if (span[index] == '+')
        {
            index++;
        }

        if (index >= span.Length)
        {
            return false;
        }

        // Parse digits
        bool hasDigits = false;
        ulong result = 0; // Use ulong to detect overflow

        while (index < span.Length)
        {
            char c = span[index];

            if (c < '0' || c > '9')
            {
                break;
            }

            int digit = c - '0';

            // Check for overflow before multiplication
            if (result > (ulong.MaxValue - (ulong)digit) / 10)
            {
                return false;
            }

            result = (result * 10) + (ulong)digit;

            hasDigits = true;
            index++;
        }

        if (!hasDigits)
        {
            return false;
        }

        // Skip trailing whitespace
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }

        // Should have consumed entire span
        if (index < span.Length)
        {
            return false;
        }

        // Check for overflow based on sign
        if (sign == 1)
        {
            if (result > long.MaxValue)
            {
                return false;
            }

            value = (long)result;
        }
        else
        {
            if (result > (ulong)long.MaxValue + 1)
            {
                return false;
            }

            value = -(long)result;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse a span of characters as a BigInteger.
    /// For netstandard2.0, this converts the span to a string for BigInteger.TryParse.
    /// </summary>
    /// <param name="span">The span of characters to parse.</param>
    /// <param name="style">The number style to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="value">The parsed value, if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseBigInteger(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider? provider, out BigInteger value)
    {
        // For netstandard2.0, BigInteger.TryParse only accepts strings
        // We use span.ToString() which allocates, but it's unavoidable for BigInteger on netstandard2.0
        return BigInteger.TryParse(span.ToString(), style, provider, out value);
    }

    /// <summary>
    /// Tries to parse a span of characters as a 64-bit signed integer with culture support.
    /// </summary>
    /// <param name="span">The span of characters to parse.</param>
    /// <param name="style">The number style to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="value">The parsed value, if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseInt64(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider? provider, out long value)
    {
        // Fast path: if it's invariant culture with integer style, use our optimized parser
        if (provider == CultureInfo.InvariantCulture && style == NumberStyles.Integer)
        {
            return TryParseInt64Invariant(span, out value);
        }

        // For complex cases with culture-specific parsing, we need to convert to string
        // This is a limitation of netstandard2.0
        return long.TryParse(span.ToString(), style, provider, out value);
    }

        /// <summary>
    /// Tries to parse a span of characters as a 64-bit signed integer with culture support.
    /// </summary>
    /// <param name="span">The span of characters to parse.</param>
    /// <param name="style">The number style to use.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="value">The parsed value, if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseInt32(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider? provider, out int value)
    {
        // Fast path: if it's invariant culture with integer style, use our optimized parser
        if (provider == CultureInfo.InvariantCulture && style == NumberStyles.Integer)
        {
            return TryParseInt32Invariant(span, out value);
        }

        // For complex cases with culture-specific parsing, we need to convert to string
        // This is a limitation of netstandard2.0
        return int.TryParse(span.ToString(), style, provider, out value);
    }
}

#endif