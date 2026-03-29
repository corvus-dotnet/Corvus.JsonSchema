// <copyright file="JsonElementHelpers.Numeric.Core.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Text;
using System.Runtime.CompilerServices;

#if CORVUS_TEXT_JSON_CODEGENERATION

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.CodeGeneration.Internal;

#else

namespace Corvus.Text.Json.Internal;
#endif

/// <summary>
/// Core helper methods for parsing and processing JSON numeric values into their component parts.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Parses a JSON number into its component parts using normal-form decimal representation.
    /// </summary>
    /// <param name="span">The UTF-8 encoded span containing the JSON number to parse.</param>
    /// <param name="isNegative">When this method returns, indicates whether the number is negative.</param>
    /// <param name="integral">When this method returns, contains the integral part of the number without leading zeros.</param>
    /// <param name="fractional">When this method returns, contains the fractional part of the number without trailing zeros.</param>
    /// <param name="exponent">When this method returns, contains the exponent value for scientific notation.</param>
    /// <remarks>
    /// The returned components use a normal-form decimal representation:
    /// Number := sign * &lt;integral + fractional&gt; * 10^exponent
    /// where integral and fractional are sequences of digits whose concatenation
    /// represents the significand of the number without leading or trailing zeros.
    /// Two such normal-form numbers are treated as equal if and only if they have
    /// equal signs, significands, and exponents.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ParseNumber(
        ReadOnlySpan<byte> span,
           out bool isNegative,
           out ReadOnlySpan<byte> integral,
           out ReadOnlySpan<byte> fractional,
           out int exponent)
    {
        if (!TryParseNumber(span, out isNegative, out integral, out fractional, out exponent))
        {
            throw new FormatException();
        }
    }

    /// <summary>
    /// Parses a JSON number into its component parts using normal-form decimal representation.
    /// </summary>
    /// <param name="span">The UTF-8 encoded span containing the JSON number to parse.</param>
    /// <param name="isNegative">When this method returns, indicates whether the number is negative.</param>
    /// <param name="integral">When this method returns, contains the integral part of the number without leading zeros.</param>
    /// <param name="fractional">When this method returns, contains the fractional part of the number without trailing zeros.</param>
    /// <param name="exponent">When this method returns, contains the exponent value for scientific notation.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// The returned components use a normal-form decimal representation:
    /// Number := sign * &lt;integral + fractional&gt; * 10^exponent
    /// where integral and fractional are sequences of digits whose concatenation
    /// represents the significand of the number without leading or trailing zeros.
    /// Two such normal-form numbers are treated as equal if and only if they have
    /// equal signs, significands, and exponents.
    /// </remarks>
    public static bool TryParseNumber(
       ReadOnlySpan<byte> span,
       out bool isNegative,
       out ReadOnlySpan<byte> integral,
       out ReadOnlySpan<byte> fractional,
       out int exponent)
    {
        // Parses a JSON number into its integral, fractional, and exponent parts.
        // The returned components use a normal-form decimal representation:
        // Number := sign * <integral + fractional> * 10^exponent
        // where integral and fractional are sequences of digits whose concatenation
        // represents the significand of the number without leading or trailing zeros.
        // Two such normal-form numbers are treated as equal if and only if they have
        // equal signs, significands, and exponents.
        bool neg;
        ReadOnlySpan<byte> intg;
        ReadOnlySpan<byte> frac;
        int exp;

        if (span.Length <= 0)
        {
            isNegative = default;
            integral = default;
            fractional = default;
            exponent = default;
            return false;
        }

        if (span[0] == '-')
        {
            neg = true;
            span = span.Slice(1);
        }
        else
        {
            if (!char.IsDigit((char)span[0]))
            {
                isNegative = default;
                integral = default;
                fractional = default;
                exponent = default;
                return false;
            }

            neg = false;
        }

        int i = span.IndexOfAny((byte)'.', (byte)'e', (byte)'E');
        if (i < 0)
        {
            intg = span;
            frac = default;
            exp = 0;
            goto Normalize;
        }

        intg = span.Slice(0, i);

        if (span[i] == '.')
        {
            span = span.Slice(i + 1);
            if (span.Length == 0)
            {
                isNegative = default;
                integral = default;
                fractional = default;
                exponent = default;
                return false;
            }

            i = span.IndexOfAny((byte)'e', (byte)'E');
            if (i < 0)
            {
                frac = span;
                exp = 0;
                goto Normalize;
            }

            frac = span.Slice(0, i);
        }
        else
        {
            frac = default;
        }

        if (!(span[i] is (byte)'e' or (byte)'E'))
        {
            isNegative = default;
            integral = default;
            fractional = default;
            exponent = default;
            return false;
        }

        if (!Utf8Parser.TryParse(span.Slice(i + 1), out exp, out _))
        {
            isNegative = default;
            integral = default;
            fractional = default;
            exponent = default;
            return false;
        }

    Normalize: // Calculates the normal form of the number.

        if (IndexOfFirstTrailingZero(frac) is >= 0 and int iz)
        {
            // Trim trailing zeros from the fractional part.
            // e.g. 3.1400 -> 3.14
            frac = frac.Slice(0, iz);
        }

        if (intg[0] == '0')
        {
            if (intg.Length != 1)
            {
                isNegative = default;
                integral = default;
                fractional = default;
                exponent = default;
                return false;
            }

            if (IndexOfLastLeadingZero(frac) is >= 0 and int lz)
            {
                // Trim leading zeros from the fractional part
                // and update the exponent accordingly.
                // e.g. 0.000123 -> 0.123e-3
                frac = frac.Slice(lz + 1);
                exp -= lz + 1;
            }

            // Normalize "0" to the empty span.
            intg = default;
        }

        if (frac.IsEmpty && IndexOfFirstTrailingZero(intg) is >= 0 and int fz)
        {
            // There is no fractional part, trim trailing zeros from
            // the integral part and increase the exponent accordingly.
            // e.g. 1000 -> 1e3
            exp += intg.Length - fz;
            intg = intg.Slice(0, fz);
        }

        // Normalize the exponent by subtracting the length of the fractional part.
        // e.g. 3.14 -> 314e-2
        exp -= frac.Length;

        if (intg.IsEmpty && frac.IsEmpty)
        {
            // Normalize zero representations.
            neg = false;
            exp = 0;
        }

        // Copy to out parameters.
        isNegative = neg;
        integral = intg;
        fractional = frac;
        exponent = exp;

        return true;

        static int IndexOfLastLeadingZero(ReadOnlySpan<byte> span)
        {
#if NET
            int firstNonZero = span.IndexOfAnyExcept((byte)'0');
            return firstNonZero < 0 ? span.Length - 1 : firstNonZero - 1;
#else
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '0')
                {
                    return i - 1;
                }
            }

            return span.Length - 1;
#endif
        }

        static int IndexOfFirstTrailingZero(ReadOnlySpan<byte> span)
        {
#if NET
            int lastNonZero = span.LastIndexOfAnyExcept((byte)'0');
            return lastNonZero == span.Length - 1 ? -1 : lastNonZero + 1;
#else
            if (span.IsEmpty)
            {
                return -1;
            }

            for (int i = span.Length - 1; i >= 0; i--)
            {
                if (span[i] != '0')
                {
                    return i == span.Length - 1 ? -1 : i + 1;
                }
            }

            return 0;
#endif
        }
    }

    /// <summary>
    /// Compares two normalized JSON numbers for equality.
    /// </summary>
    /// <param name="leftIsNegative">True if the LHS is negative.</param>
    /// <param name="leftIntegral">When concatenated with <paramref name="leftFractional"/> produces the significand of the LHS number without leading or trailing zeros.</param>
    /// <param name="leftFractional">When concatenated with <paramref name="leftIntegral"/> produces the significand of the LHS number without leading or trailing zeros.</param>
    /// <param name="leftExponent">The LHS exponent.</param>
    /// <param name="rightIsNegative">True if the RHS is negative.</param>
    /// <param name="rightIntegral">When concatenated with <paramref name="rightFractional"/> produces the significand of the RHS number without leading or trailing zeros.</param>
    /// <param name="rightFractional">When concatenated with <paramref name="rightIntegral"/> produces the significand of the RHS number without leading or trailing zeros.</param>
    /// <param name="rightExponent">The RHS exponent.</param>
    /// <returns>-1 if the LHS is less than the RHS, 0 if the are equal, and 1 if the LHS is greater than the RHS.</returns>
    public static int CompareNormalizedJsonNumbers(
        bool leftIsNegative,
        ReadOnlySpan<byte> leftIntegral,
        ReadOnlySpan<byte> leftFractional,
        int leftExponent,
        bool rightIsNegative,
        ReadOnlySpan<byte> rightIntegral,
        ReadOnlySpan<byte> rightFractional,
        int rightExponent)
    {
        // Step 1: Compare signs
        if (leftIsNegative != rightIsNegative)
        {
            return leftIsNegative ? -1 : 1;
        }

        int signMultiplier = leftIsNegative ? -1 : 1;

        int leftTotalLength = leftIntegral.Length + leftFractional.Length;
        int rightTotalLength = rightIntegral.Length + rightFractional.Length;

        // Step 2: Compare effective magnitudes of the numbers
        int leftEffectiveLength = leftTotalLength + leftExponent;
        int rightEffectiveLength = rightTotalLength + rightExponent;

        if (leftEffectiveLength != rightEffectiveLength)
        {
            return (leftEffectiveLength > rightEffectiveLength ? 1 : -1) * signMultiplier;
        }

        // Step 3: Compare digits, accounting for exponent difference
        int leftLeadingZeros = leftExponent < 0 ? Math.Max(0, -(leftTotalLength + leftExponent)) : 0;
        int rightLeadingZeros = rightExponent < 0 ? Math.Max(0, -(rightTotalLength + rightExponent)) : 0;

        int maxDigitLength = Math.Max(leftTotalLength, rightTotalLength);

        // Adjust so we don't bother with matching leading zeros
        if (leftLeadingZeros > rightLeadingZeros)
        {
            leftLeadingZeros -= rightLeadingZeros;
            maxDigitLength -= rightLeadingZeros;
            rightLeadingZeros = 0;
        }
        else
        {
            rightLeadingZeros -= leftLeadingZeros;
            maxDigitLength -= leftLeadingZeros;
            leftLeadingZeros = 0;
        }

        for (int i = 0; i < maxDigitLength; i++)
        {
            byte leftDigit = GetDigitAtPosition(leftIntegral, leftFractional, i - leftLeadingZeros);
            byte rightDigit = GetDigitAtPosition(rightIntegral, rightFractional, i - rightLeadingZeros);

            if (leftDigit != rightDigit)
            {
                return (leftDigit > rightDigit ? 1 : -1) * signMultiplier;
            }
        }

        // Step 4: Numbers are equal
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetDigitAtPosition(
    ReadOnlySpan<byte> integral,
    ReadOnlySpan<byte> fractional,
    int integralIndex)
    {
        if (integralIndex < integral.Length)
        {
            // Position is in the integral part
            return integralIndex >= 0 ? integral[integralIndex] : (byte)'0';
        }
        else
        {
            // Position is in the fractional part
            int fractionalIndex = integralIndex - integral.Length;
            return fractionalIndex >= 0 && fractionalIndex < fractional.Length ? fractional[fractionalIndex] : (byte)'0';
        }
    }

    /// <summary>
    /// Gets the decimal digit at a position in the complete decimal representation,
    /// accounting for the exponent without materializing trailing zeros.
    /// </summary>
    /// <param name="integral">The integral digits.</param>
    /// <param name="fractional">The fractional digits.</param>
    /// <param name="exponent">The exponent.</param>
    /// <param name="position">The position in the complete decimal representation.</param>
    /// <returns>The digit at the position, or '0' if beyond the significant digits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetDecimalDigitAtPosition(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        int position)
    {
        int significandLength = integral.Length + fractional.Length;

        // If position is within the significand, get it from integral/fractional
        if (position < significandLength)
        {
            return GetDigitAtPosition(integral, fractional, position);
        }

        // If position is in the trailing zeros added by positive exponent
        if (exponent > 0 && position < significandLength + exponent)
        {
            return (byte)'0';
        }

        // Beyond the decimal representation
        return (byte)'0';
    }

    /// <summary>
    /// Gets the total length of the decimal representation including exponent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDecimalLength(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        int significandLength = integral.Length + fractional.Length;
        return exponent >= 0 ? significandLength + exponent : significandLength;
    }
}