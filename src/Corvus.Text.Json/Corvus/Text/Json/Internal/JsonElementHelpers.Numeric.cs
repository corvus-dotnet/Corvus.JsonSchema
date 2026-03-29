// <copyright file="JsonElementHelpers.Numeric.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Corvus.Numerics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON numeric operations including equality comparisons, divisibility checks, and arithmetic operations.
/// </summary>
public static partial class JsonElementHelpers
{
    private static readonly StandardFormat D3Format = StandardFormat.Parse("D3");

    private const int MaxExponent = 18;

    // This table is used to quickly look up 10^X for X in [0..18]
    // This corresponds to the max exponent of 18.
    private static readonly ulong[] TenPowTable =
    [
        1UL,
        10UL,
        100UL,
        1000UL,
        10000UL,
        100000UL,
        1000000UL,
        10000000UL,
        100000000UL,
        1000000000UL,
        10000000000UL,
        100000000000UL,
        1000000000000UL,
        10000000000000UL,
        100000000000000UL,
        1000000000000000UL,
        10000000000000000UL,
        100000000000000000UL,
    ];

    private static readonly ulong MaxTenPowExponent = (ulong)10e18;

    /// <summary>
    /// Compares two valid UTF-8 encoded JSON numbers for decimal equality.
    /// </summary>
    /// <param name="left">The UTF-8 encoded bytes representing the left JSON number.</param>
    /// <param name="right">The UTF-8 encoded bytes representing the right JSON number.</param>
    /// <returns><see langword="true"/> if the two JSON numbers are equal; otherwise, <see langword="false"/>.</returns>
    public static bool AreEqualJsonNumbers(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Debug.Assert(left.Length > 0 && right.Length > 0);

        ParseNumber(left,
            out bool leftIsNegative,
            out ReadOnlySpan<byte> leftIntegral,
            out ReadOnlySpan<byte> leftFractional,
            out int leftExponent);

        ParseNumber(right,
            out bool rightIsNegative,
            out ReadOnlySpan<byte> rightIntegral,
            out ReadOnlySpan<byte> rightFractional,
            out int rightExponent);

        return AreEqualNormalizedJsonNumbers(leftIsNegative, leftIntegral, leftFractional, leftExponent, rightIsNegative, rightIntegral, rightFractional, rightExponent);
    }

    /// <summary>
    /// Compares two valid normalized JSON numbers for decimal equality.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left number is negative.</param>
    /// <param name="leftIntegral">The integral part of the left number without leading zeros.</param>
    /// <param name="leftFractional">The fractional part of the left number without trailing zeros.</param>
    /// <param name="leftExponent">The exponent of the left number.</param>
    /// <param name="rightIsNegative">Indicates whether the right number is negative.</param>
    /// <param name="rightIntegral">The integral part of the right number without leading zeros.</param>
    /// <param name="rightFractional">The fractional part of the right number without trailing zeros.</param>
    /// <param name="rightExponent">The exponent of the right number.</param>
    /// <returns><see langword="true"/> if the two normalized JSON numbers are equal; otherwise, <see langword="false"/>.</returns>
    public static bool AreEqualNormalizedJsonNumbers(bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent, bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent)
    {
        Debug.Assert(leftIntegral.Length == 0 || leftIntegral[0] != (byte)'0');
        Debug.Assert(leftFractional.Length == 0 || leftFractional[^1] != (byte)'0');
        Debug.Assert(rightIntegral.Length == 0 || rightIntegral[0] != (byte)'0');
        Debug.Assert(rightFractional.Length == 0 || rightFractional[^1] != (byte)'0');
        if (leftIsNegative != rightIsNegative ||
            leftExponent != rightExponent ||
            ((leftIntegral.Length + leftFractional.Length)) !=
                        rightIntegral.Length + rightFractional.Length)
        {
            return false;
        }

        // Need to check that the concatenated integral and fractional parts are equal;
        // break each representation into three parts such that their lengths exactly match.
        ReadOnlySpan<byte> leftFirst;
        ReadOnlySpan<byte> leftMiddle;
        ReadOnlySpan<byte> leftLast;

        ReadOnlySpan<byte> rightFirst;
        ReadOnlySpan<byte> rightMiddle;
        ReadOnlySpan<byte> rightLast;

        int diff = leftIntegral.Length - rightIntegral.Length;
        switch (diff)
        {
            case < 0:
                leftFirst = leftIntegral;
                leftMiddle = leftFractional.Slice(0, -diff);
                leftLast = leftFractional.Slice(-diff);
                int rightOffset = rightIntegral.Length + diff;
                rightFirst = rightIntegral.Slice(0, rightOffset);
                rightMiddle = rightIntegral.Slice(rightOffset);
                rightLast = rightFractional;
                break;

            case 0:
                leftFirst = leftIntegral;
                leftMiddle = default;
                leftLast = leftFractional;
                rightFirst = rightIntegral;
                rightMiddle = default;
                rightLast = rightFractional;
                break;

            case > 0:
                int leftOffset = leftIntegral.Length - diff;
                leftFirst = leftIntegral.Slice(0, leftOffset);
                leftMiddle = leftIntegral.Slice(leftOffset);
                leftLast = leftFractional;
                rightFirst = rightIntegral;
                rightMiddle = rightFractional.Slice(0, diff);
                rightLast = rightFractional.Slice(diff);
                break;
        }

        Debug.Assert(leftFirst.Length == rightFirst.Length);
        Debug.Assert(leftMiddle.Length == rightMiddle.Length);
        Debug.Assert(leftLast.Length == rightLast.Length);
        return leftFirst.SequenceEqual(rightFirst) &&
            leftMiddle.SequenceEqual(rightMiddle) &&
            leftLast.SequenceEqual(rightLast);
    }

    /// <summary>
    /// Determines if a JSON number is an integer.
    /// </summary>
    /// <param name="exponent">The exponent.</param>
    ///
    ///
    /// <returns>True if the normalized JSON number represents an integer.</returns>
    public static bool IsIntegerNormalizedJsonNumber(
        int exponent)
    {
        return exponent >= 0;
    }

    /// <summary>
    /// Determines whether the normalized JSON number is an exact multiple of the given integer divisor.
    /// </summary>
    /// <param name="integral">When concatenated with <paramref name="fractional"/> produces the significand of the number without leading or trailing zeros.</param>
    /// <param name="fractional">When concatenated with <paramref name="integral"/> produces the significand of the number without leading or trailing zeros.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="divisor">The significand of the divisor represented as a <see cref="ulong"/>.</param>
    /// <param name="divisorExponent">The exponent of the divisor. This will be non-zero if the divisor had a fractional component.</param>
    /// <returns>True if the normalized JSON number is a multiple of the divisor (i.e. <c>n mod D == 0</c>).</returns>
    /// <remarks>We do not need to pass the sign of the JSON number as it is irrelevant to the calculation.</remarks>
    [CLSCompliant(false)]
    public static bool IsMultipleOf(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        ulong divisor,
        int divisorExponent)
    {
        // Note that when calculating the divisor we should ensure
        // a) that it is a positive integer
        // b) that we normalize to remove trailing zeros and apply them to the exponent
        // c) that we normalize to remove any fractional component and apply them to the exponent
        // Step 1.
        // Check for a divisor of zero, then check for a number that is trivially zero by length
        // Calculate the length of the significand of the number
        if (divisor == 0)
        {
            // Never true for a divisor of 0
            return false;
        }

        if (integral.Length == 0 && fractional.Length == 0)
        {
            // Always return true for a value of 0
            return true;
        }

        // Step 2.
        // Sum the exponent and the negated divisor exponent (i.e. exponent - divisorExponent) to get the net exponent for the number
        // Why?
        // If the divisor had a fractional value, and had to be multiplied by e.g. 100 to produce an integer,
        // then divisor's exponent will be e.g. -2.
        // Basic algebra tells us that the number must be multiplied by the same factor of e.g. 100 to produce a correct result.
        // The net exponent for the number is therefore the exponent of the number minus the exponent of the divisor.
        int netExponent = exponent - divisorExponent;

        // Step 3.
        // Determine if that significand has a fractional component. If so, return false as it cannot be an exact multiple of an integer
        // Note that this test encompasses the pathological case of netExponent < 0, which makes some component of the integral part
        // fractional.
        if (netExponent < 0)
        {
            return false;
        }

        int totalLength = integral.Length + fractional.Length;

        // Step 4.
        // Determine if the divisor is one of the common "fast path" divisors and use that (e.g. 1, 2, 5, 10) otherwise use the general purpose
        // algorithm
        return divisor switch
        {
            1 => true, // 0 mod 1 == 0
            2 => IsDivisibleByTwo(integral, fractional, totalLength + netExponent - 1),
            3 => IsDivisibleByThree(integral, fractional),
            4 => IsDivisibleByFour(integral, fractional, totalLength + netExponent - 1),
            5 => IsDivisibleByFive(integral, fractional, totalLength + netExponent - 1),
            6 => IsDivisibleBySix(integral, fractional, totalLength + netExponent - 1),
            8 => IsDivisibleByEight(integral, fractional, totalLength + netExponent - 1),
            10 => IsDivisibleByTen(integral, fractional, totalLength + netExponent - 1),
            _ => GeneralPurposeIsMultipleOf(integral, fractional, totalLength + netExponent - 1, divisor),
        };
    }

    /// <summary>
    /// Determines whether the normalized JSON number is an exact multiple of the given integer divisor.
    /// </summary>
    /// <param name="integral">When concatenated with <paramref name="fractional"/> produces the significand of the number without leading or trailing zeros.</param>
    /// <param name="fractional">When concatenated with <paramref name="integral"/> produces the significand of the number without leading or trailing zeros.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="divisor">The significand of the divisor represented as a <see cref="System.Numerics.BigInteger"/>.</param>
    /// <param name="divisorExponent">The exponent of the divisor. This will be non-zero if the divisor had a fractional component.</param>
    /// <returns>True if the normalized JSON number is a multiple of the divisor (i.e. <c>n mod D == 0</c>).</returns>
    /// <remarks>We do not need to pass the sign of the JSON number as it is irrelevant to the calculation.</remarks>
    public static bool IsMultipleOf(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        System.Numerics.BigInteger divisor,
        int divisorExponent)
    {
        // Note that when calculating the divisor we should ensure
        // a) that it is a positive integer
        // b) that we normalize to remove trailing zeros and apply them to the exponent
        // c) that we normalize to remove any fractional component and apply them to the exponent
        // Step 1.
        // Check for a divisor of zero, then check for a number that is trivially zero by length
        // Calculate the length of the significand of the number
        if (divisor == 0)
        {
            // Never true for a divisor of 0
            return false;
        }

        if (integral.Length == 0 && fractional.Length == 0)
        {
            // Always return true for a value of 0
            return true;
        }

        // Step 2.
        // Sum the exponent and the negated divisor exponent (i.e. exponent - divisorExponent) to get the net exponent for the number
        // Why?
        // If the divisor had a fractional value, and had to be multiplied by e.g. 100 to produce an integer,
        // then divisor's exponent will be e.g. -2.
        // Basic algebra tells us that the number must be multiplied by the same factor of e.g. 100 to produce a correct result.
        // The net exponent for the number is therefore the exponent of the number minus the exponent of the divisor.
        int netExponent = exponent - divisorExponent;

        // Step 3.
        // Determine if that significand has a fractional component. If so, return false as it cannot be an exact multiple of an integer
        // Note that this test encompasses the pathological case of netExponent < 0, which makes some component of the integral part
        // fractional.
        if (netExponent < 0)
        {
            return false;
        }

        int totalLength = integral.Length + fractional.Length;

        // Step 4.
        // Determine if the divisor is one of the common "fast path" divisors and use that (e.g. 1, 2, 5, 10) otherwise use the general purpose
        // algorithm
        if (divisor.IsOne)
        {
            return true; // 0 mod 1 == 0
        }

        if (divisor.Equals(2))
        {
            return IsDivisibleByTwo(integral, fractional, totalLength + netExponent - 1);
        }

        if (divisor.Equals(3))
        {
            return IsDivisibleByThree(integral, fractional);
        }

        if (divisor.Equals(4))
        {
            return IsDivisibleByFour(integral, fractional, totalLength + netExponent - 1);
        }

        if (divisor.Equals(5))
        {
            return IsDivisibleByFive(integral, fractional, totalLength + netExponent - 1);
        }

        if (divisor.Equals(6))
        {
            return IsDivisibleBySix(integral, fractional, totalLength + netExponent - 1);
        }

        if (divisor.Equals(8))
        {
            return IsDivisibleByEight(integral, fractional, totalLength + netExponent - 1);
        }

        if (divisor.Equals(10))
        {
            return IsDivisibleByTen(integral, fractional, totalLength + netExponent - 1);
        }

        return GeneralPurposeIsMultipleOf(integral, fractional, totalLength + netExponent - 1, divisor);
    }

    /// <summary>
    /// Format the number as a string.
    /// </summary>
    /// <param name="span">The UTF-8 representation of the number.</param>
    /// <param name="format">The format to apply.</param>
    /// <param name="provider">The (optional) format provider.</param>
    /// <param name="value">The result if formatting succeeds, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if formatting succeeds, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// This will always return <see langword="false"/> if the formatted result exceeds 2048 characters in size.
    /// </remarks>
    public static bool TryFormatNumberAsString(ReadOnlySpan<byte> span, ReadOnlySpan<char> format, IFormatProvider? provider, [NotNullWhen(true)] out string? value)
    {
        Span<char> destination = stackalloc char[JsonConstants.MaximumFormatNumberLength];
        if (!TryFormatNumber(span, destination, out int bytesWritten, format, provider))
        {
            value = null;
            return false;
        }

        value = destination.Slice(0, bytesWritten).ToString();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        ParseNumber(span, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        return TryFormatNumber(span, destination, out charsWritten, format, provider, isNegative, integral, fractional, exponent);
    }

    internal static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider, bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        if (integral.Length == 0 && fractional.Length == 0)
        {
            // Fast path for zero, which is common and can be formatted without any further processing.
            return TryFormatZero(destination, out charsWritten, format, provider);
        }

        if (format.IsEmpty)
        {
            return JsonReaderHelper.TryTranscode(span, destination, out charsWritten);
        }

        char formatType = char.ToUpperInvariant(format[0]);
        int precision = -1;

        if (format.Length > 1)
        {
            if (!TryParseInt32(format.Slice(1), out precision))
            {
                charsWritten = 0;
                return false;
            }
        }

        var formatInfo = NumberFormatInfo.GetInstance(provider);

        return formatType switch
        {
            'G' => TryFormatGeneral(isNegative, integral, fractional, exponent, destination, out charsWritten, precision, char.IsLower(format[0]) ? 'e' : 'E', formatInfo),
            'F' => TryFormatFixedPointWithSeparator(isNegative, integral, fractional, exponent, destination, out charsWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo.NumberDecimalSeparator, formatInfo),
            'N' => TryFormatNumber(isNegative, integral, fractional, exponent, destination, out charsWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'E' => TryFormatExponential(isNegative, integral, fractional, exponent, destination, out charsWritten, precision >= 0 ? precision : 6, char.IsLower(format[0]) ? 'e' : 'E', formatInfo),
            'C' => TryFormatCurrency(isNegative, integral, fractional, exponent, destination, out charsWritten, precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => TryFormatPercent(isNegative, integral, fractional, exponent, destination, out charsWritten, precision >= 0 ? precision : formatInfo.PercentDecimalDigits, formatInfo),
            'X' => TryFormatHexadecimal(isNegative, integral, fractional, exponent, destination, out charsWritten, precision, char.IsLower(format[0])),
            'B' => TryFormatBinary(isNegative, integral, fractional, exponent, destination, out charsWritten, precision),
            _ => UnknownFormat(out charsWritten)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UnknownFormat(out int written)
    {
        written = 0;
        return false;
    }

    internal static bool TryFormatPercent(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, Span<char> destination, out int charsWritten, int precision, NumberFormatInfo formatInfo)
    {
        // Percent format multiplies by 100 (shift exponent by +2), uses group separators, and adds % symbol
        int adjustedExponent = exponent + 2;

        // Use PercentDecimalDigits as the default precision
        int effectivePrecision = precision >= 0 ? precision : formatInfo.PercentDecimalDigits;

        // Create a temporary NumberFormatInfo for formatting the number part using percent settings
        var tempFormatInfo = new NumberFormatInfo
        {
            NumberDecimalSeparator = formatInfo.PercentDecimalSeparator,
            NumberGroupSeparator = formatInfo.PercentGroupSeparator,
            NumberGroupSizes = formatInfo.PercentGroupSizes,
            NegativeSign = formatInfo.NegativeSign
        };

        int pos = 0;
        int numberChars;

        if (isNegative)
        {
            // Handle negative patterns
            int pattern = formatInfo.PercentNegativePattern;
            string negSign = formatInfo.NegativeSign;
            string percentSym = formatInfo.PercentSymbol;

            switch (pattern)
            {
                case 0: // -n %
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + 1 + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                case 1: // -n%
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                case 2: // -%n
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 3: // %-n
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 4: // %n-
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + negSign.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    break;

                case 5: // n-%
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + negSign.Length + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                case 6: // n%-
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + percentSym.Length + negSign.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    break;

                case 7: // -% n
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 8: // n %-
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + 1 + percentSym.Length + negSign.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    break;

                case 9: // % n-
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + negSign.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    break;

                case 10: // % -n
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 11: // n- %
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + negSign.Length + 1 + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    negSign.AsSpan().CopyTo(destination.Slice(pos));
                    pos += negSign.Length;
                    destination[pos++] = ' ';
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                default:
                    charsWritten = 0;
                    return false;
            }
        }
        else
        {
            // Handle positive patterns
            int pattern = formatInfo.PercentPositivePattern;
            string percentSym = formatInfo.PercentSymbol;

            switch (pattern)
            {
                case 0: // n %
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + 1 + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                case 1: // n%
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + percentSym.Length > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    break;

                case 2: // %n
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 3: // % n
                    percentSym.AsSpan().CopyTo(destination.Slice(pos));
                    pos += percentSym.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                default:
                    charsWritten = 0;
                    return false;
            }
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatHexadecimal(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        bool lowercase)
    {
        // Hexadecimal format only works for non-negative integers
        if (isNegative || exponent < 0)
        {
            charsWritten = 0;
            return false;
        }

        // Convert decimal representation to hex using virtualized access
        int totalLength = GetDecimalLength(integral, fractional, exponent);
        return DecimalToHexVirtualized(integral, fractional, exponent, totalLength, destination, out charsWritten, precision, lowercase);
    }

    internal static bool TryFormatBinary(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision)
    {
        // Binary format only works for non-negative integers
        if (isNegative || exponent < 0)
        {
            charsWritten = 0;
            return false;
        }

        // Convert decimal representation to binary using virtualized access
        int totalLength = GetDecimalLength(integral, fractional, exponent);
        return DecimalToBinaryVirtualized(integral, fractional, exponent, totalLength, destination, out charsWritten, precision);
    }

    private static bool DecimalToHexVirtualized(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, int totalLength, Span<char> destination, out int charsWritten, int precision, bool lowercase)
    {
        // Handle zero
        if (totalLength == 0 || (totalLength == 1 && GetDecimalDigitAtPosition(integral, fractional, exponent, 0) == (byte)'0'))
        {
            int requiredLength = precision > 0 ? precision : 1;
            if (destination.Length < requiredLength)
            {
                charsWritten = 0;
                return false;
            }

            destination[0] = '0';
            for (int i = 1; i < requiredLength; i++)
            {
                destination[i] = '0';
            }

            charsWritten = requiredLength;
            return true;
        }

        // Optimize: trailing zeros from positive exponent can be handled separately
        // Each trailing zero represents a factor of 10, which needs processing
        int significandLength = integral.Length + fractional.Length;
        int trailingZeros = exponent > 0 ? exponent : 0;

        // Working buffer needed for repeated division algorithm - stores intermediate quotients
        // that shrink with each division step. Only allocate for the significand digits.
        byte[]? rentedWorkingBuffer = null;
        Span<byte> workingDigits = significandLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[significandLength]
            : (rentedWorkingBuffer = ArrayPool<byte>.Shared.Rent(significandLength)).AsSpan(0, significandLength);

        try
        {
            // Initialize working buffer from significand only (skip virtual trailing zeros)
            for (int i = 0; i < significandLength; i++)
            {
                workingDigits[i] = (byte)(GetDigitAtPosition(integral, fractional, i) - (byte)'0');
            }

            // Generate hex digits directly in reverse order at the end of destination buffer
            // We'll shift them down to the beginning once we know the final count
            int writePos = destination.Length - 1;
            int digitCount = significandLength;
            int remainingTrailingZeros = trailingZeros;
            int hexDigitsGenerated = 0;

            // Repeatedly divide by 16 and write hex digits from right to left
            while (digitCount > 0 || remainingTrailingZeros > 0)
            {
                // Check if we're done
                if (digitCount == 0)
                {
                    break;
                }

                if (digitCount == 1 && workingDigits[0] == 0 && remainingTrailingZeros == 0)
                {
                    break;
                }

                int remainder = 0;
                int newDigitCount = 0;

                for (int i = 0; i < digitCount; i++)
                {
                    int current = remainder * 10 + workingDigits[i];
                    int quotient = current / 16;
                    remainder = current % 16;

                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }
                }

                // Process trailing zeros
                if (remainingTrailingZeros > 0)
                {
                    remainder *= 10;
                    int quotient = remainder / 16;
                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }

                    remainingTrailingZeros--;
                }

                // Convert remainder to hex digit and write directly at end of buffer
                remainder %= 16;
                char hexDigit = remainder < 10
                    ? (char)('0' + remainder)
                    : (lowercase ? (char)('a' + (remainder - 10)) : (char)('A' + (remainder - 10)));

                if (writePos < 0)
                {
                    charsWritten = 0;
                    return false;
                }

                destination[writePos--] = hexDigit;
                hexDigitsGenerated++;
                digitCount = newDigitCount;
            }

            // Calculate final output length with precision
            int outputLength = Math.Max(hexDigitsGenerated, precision > 0 ? precision : hexDigitsGenerated);
            if (outputLength > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            // Shift generated hex digits to the beginning and add leading zeros
            int sourceStart = destination.Length - hexDigitsGenerated;
            int leadingZeros = outputLength - hexDigitsGenerated;

            // Move generated digits to their final position
            if (leadingZeros > 0)
            {
                // Copy from right to left position
                destination.Slice(sourceStart, hexDigitsGenerated).CopyTo(destination.Slice(leadingZeros));

                // Fill leading zeros
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[i] = '0';
                }
            }
            else if (sourceStart > 0)
            {
                // No leading zeros, just move to start
                destination.Slice(sourceStart, hexDigitsGenerated).CopyTo(destination);
            }

            charsWritten = outputLength;
            return true;
        }
        finally
        {
            if (rentedWorkingBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedWorkingBuffer);
            }
        }
    }

    private static bool DecimalToBinaryVirtualized(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, int totalLength, Span<char> destination, out int charsWritten, int precision)
    {
        // Handle zero
        if (totalLength == 0 || (totalLength == 1 && GetDecimalDigitAtPosition(integral, fractional, exponent, 0) == (byte)'0'))
        {
            int requiredLength = precision > 0 ? precision : 1;
            if (destination.Length < requiredLength)
            {
                charsWritten = 0;
                return false;
            }

            destination[0] = '0';
            for (int i = 1; i < requiredLength; i++)
            {
                destination[i] = '0';
            }

            charsWritten = requiredLength;
            return true;
        }

        // Optimize: trailing zeros from positive exponent can be handled separately
        int significandLength = integral.Length + fractional.Length;
        int trailingZeros = exponent > 0 ? exponent : 0;

        // Working buffer needed for repeated division algorithm - stores intermediate quotients
        // that shrink with each division step. Only allocate for the significand digits.
        byte[]? rentedWorkingBuffer = null;
        Span<byte> workingDigits = significandLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[significandLength]
            : (rentedWorkingBuffer = ArrayPool<byte>.Shared.Rent(significandLength)).AsSpan(0, significandLength);

        try
        {
            // Initialize working buffer from significand only (skip virtual trailing zeros)
            for (int i = 0; i < significandLength; i++)
            {
                workingDigits[i] = (byte)(GetDigitAtPosition(integral, fractional, i) - (byte)'0');
            }

            // Generate binary digits directly in reverse order at the end of destination buffer
            // We'll shift them down to the beginning once we know the final count
            int writePos = destination.Length - 1;
            int digitCount = significandLength;
            int remainingTrailingZeros = trailingZeros;
            int binaryDigitsGenerated = 0;

            // Repeatedly divide by 2 and write binary digits from right to left
            while (digitCount > 0 || remainingTrailingZeros > 0)
            {
                // Check if we're done
                if (digitCount == 0)
                {
                    break;
                }

                if (digitCount == 1 && workingDigits[0] == 0 && remainingTrailingZeros == 0)
                {
                    break;
                }

                int remainder = 0;
                int newDigitCount = 0;

                for (int i = 0; i < digitCount; i++)
                {
                    int current = remainder * 10 + workingDigits[i];
                    int quotient = current / 2;
                    remainder = current % 2;

                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }
                }

                // Process trailing zeros
                if (remainingTrailingZeros > 0)
                {
                    remainder *= 10;
                    int quotient = remainder / 2;
                    remainder %= 2;
                    if (quotient > 0 || newDigitCount > 0)
                    {
                        if (newDigitCount < workingDigits.Length)
                        {
                            workingDigits[newDigitCount++] = (byte)quotient;
                        }
                    }

                    remainingTrailingZeros--;
                }
                else
                {
                    remainder %= 2;
                }

                // Write binary digit directly at end of buffer
                if (writePos < 0)
                {
                    charsWritten = 0;
                    return false;
                }

                destination[writePos--] = (char)('0' + remainder);
                binaryDigitsGenerated++;
                digitCount = newDigitCount;
            }

            // Calculate final output length with precision
            int outputLength = Math.Max(binaryDigitsGenerated, precision > 0 ? precision : binaryDigitsGenerated);
            if (outputLength > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            // Shift generated binary digits to the beginning and add leading zeros
            int sourceStart = destination.Length - binaryDigitsGenerated;
            int leadingZeros = outputLength - binaryDigitsGenerated;

            // Move generated digits to their final position
            if (leadingZeros > 0)
            {
                // Copy from right to left position
                destination.Slice(sourceStart, binaryDigitsGenerated).CopyTo(destination.Slice(leadingZeros));

                // Fill leading zeros
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[i] = '0';
                }
            }
            else if (sourceStart > 0)
            {
                // No leading zeros, just move to start
                destination.Slice(sourceStart, binaryDigitsGenerated).CopyTo(destination);
            }

            charsWritten = outputLength;
            return true;
        }
        finally
        {
            if (rentedWorkingBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedWorkingBuffer);
            }
        }
    }

    private static bool DecimalToHexVirtualizedUtf8(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, int totalLength, Span<byte> destination, out int bytesWritten, int precision, bool lowercase)
    {
        // Handle zero
        if (totalLength == 0 || (totalLength == 1 && GetDecimalDigitAtPosition(integral, fractional, exponent, 0) == (byte)'0'))
        {
            int requiredLength = precision > 0 ? precision : 1;
            if (destination.Length < requiredLength)
            {
                bytesWritten = 0;
                return false;
            }

            destination[0] = (byte)'0';
            for (int i = 1; i < requiredLength; i++)
            {
                destination[i] = (byte)'0';
            }

            bytesWritten = requiredLength;
            return true;
        }

        // Optimize: trailing zeros from positive exponent can be handled separately
        int significandLength = integral.Length + fractional.Length;
        int trailingZeros = exponent > 0 ? exponent : 0;

        // Working buffer needed for repeated division algorithm - stores intermediate quotients
        byte[]? rentedWorkingBuffer = null;
        Span<byte> workingDigits = significandLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[significandLength]
            : (rentedWorkingBuffer = ArrayPool<byte>.Shared.Rent(significandLength)).AsSpan(0, significandLength);

        try
        {
            // Initialize working buffer from significand only
            for (int i = 0; i < significandLength; i++)
            {
                workingDigits[i] = (byte)(GetDigitAtPosition(integral, fractional, i) - (byte)'0');
            }

            // Generate hex digits directly in reverse order at the end of destination buffer
            int writePos = destination.Length - 1;
            int digitCount = significandLength;
            int remainingTrailingZeros = trailingZeros;
            int hexDigitsGenerated = 0;

            // Repeatedly divide by 16 and write hex digits from right to left
            while (digitCount > 0 || remainingTrailingZeros > 0)
            {
                if (digitCount == 0)
                {
                    break;
                }

                if (digitCount == 1 && workingDigits[0] == 0 && remainingTrailingZeros == 0)
                {
                    break;
                }

                int remainder = 0;
                int newDigitCount = 0;

                for (int i = 0; i < digitCount; i++)
                {
                    int current = remainder * 10 + workingDigits[i];
                    int quotient = current / 16;
                    remainder = current % 16;

                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }
                }

                // Process trailing zeros
                if (remainingTrailingZeros > 0)
                {
                    remainder *= 10;
                    int quotient = remainder / 16;
                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }

                    remainingTrailingZeros--;
                }

                // Convert remainder to hex digit
                remainder %= 16;
                byte hexDigit = remainder < 10
                    ? (byte)('0' + remainder)
                    : (lowercase ? (byte)('a' + (remainder - 10)) : (byte)('A' + (remainder - 10)));

                if (writePos < 0)
                {
                    bytesWritten = 0;
                    return false;
                }

                destination[writePos--] = hexDigit;
                hexDigitsGenerated++;
                digitCount = newDigitCount;
            }

            // Calculate final output length with precision
            int outputLength = Math.Max(hexDigitsGenerated, precision > 0 ? precision : hexDigitsGenerated);
            if (outputLength > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            // Shift generated hex digits to the beginning and add leading zeros
            int sourceStart = destination.Length - hexDigitsGenerated;
            int leadingZeros = outputLength - hexDigitsGenerated;

            // Move generated digits to their final position
            if (leadingZeros > 0)
            {
                destination.Slice(sourceStart, hexDigitsGenerated).CopyTo(destination.Slice(leadingZeros));
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[i] = (byte)'0';
                }
            }
            else if (sourceStart > 0)
            {
                destination.Slice(sourceStart, hexDigitsGenerated).CopyTo(destination);
            }

            bytesWritten = outputLength;
            return true;
        }
        finally
        {
            if (rentedWorkingBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedWorkingBuffer);
            }
        }
    }

    private static bool DecimalToBinaryVirtualizedUtf8(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, int totalLength, Span<byte> destination, out int bytesWritten, int precision)
    {
        // Handle zero
        if (totalLength == 0 || (totalLength == 1 && GetDecimalDigitAtPosition(integral, fractional, exponent, 0) == (byte)'0'))
        {
            int requiredLength = precision > 0 ? precision : 1;
            if (destination.Length < requiredLength)
            {
                bytesWritten = 0;
                return false;
            }

            destination[0] = (byte)'0';
            for (int i = 1; i < requiredLength; i++)
            {
                destination[i] = (byte)'0';
            }

            bytesWritten = requiredLength;
            return true;
        }

        // Optimize: trailing zeros from positive exponent can be handled separately
        int significandLength = integral.Length + fractional.Length;
        int trailingZeros = exponent > 0 ? exponent : 0;

        // Working buffer needed for repeated division algorithm
        byte[]? rentedWorkingBuffer = null;
        Span<byte> workingDigits = significandLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[significandLength]
            : (rentedWorkingBuffer = ArrayPool<byte>.Shared.Rent(significandLength)).AsSpan(0, significandLength);

        try
        {
            // Initialize working buffer from significand only
            for (int i = 0; i < significandLength; i++)
            {
                workingDigits[i] = (byte)(GetDigitAtPosition(integral, fractional, i) - (byte)'0');
            }

            // Generate binary digits directly in reverse order at the end of destination buffer
            int writePos = destination.Length - 1;
            int digitCount = significandLength;
            int remainingTrailingZeros = trailingZeros;
            int binaryDigitsGenerated = 0;

            // Repeatedly divide by 2 and write binary digits from right to left
            while (digitCount > 0 || remainingTrailingZeros > 0)
            {
                if (digitCount == 0)
                {
                    break;
                }

                if (digitCount == 1 && workingDigits[0] == 0 && remainingTrailingZeros == 0)
                {
                    break;
                }

                int remainder = 0;
                int newDigitCount = 0;

                for (int i = 0; i < digitCount; i++)
                {
                    int current = remainder * 10 + workingDigits[i];
                    int quotient = current / 2;
                    remainder = current % 2;

                    if (quotient > 0 || newDigitCount > 0)
                    {
                        workingDigits[newDigitCount++] = (byte)quotient;
                    }
                }

                // Process trailing zeros
                if (remainingTrailingZeros > 0)
                {
                    remainder *= 10;
                    int quotient = remainder / 2;
                    remainder %= 2;
                    if (quotient > 0 || newDigitCount > 0)
                    {
                        if (newDigitCount < workingDigits.Length)
                        {
                            workingDigits[newDigitCount++] = (byte)quotient;
                        }
                    }

                    remainingTrailingZeros--;
                }
                else
                {
                    remainder %= 2;
                }

                // Write binary digit
                if (writePos < 0)
                {
                    bytesWritten = 0;
                    return false;
                }

                destination[writePos--] = (byte)('0' + remainder);
                binaryDigitsGenerated++;
                digitCount = newDigitCount;
            }

            // Calculate final output length with precision
            int outputLength = Math.Max(binaryDigitsGenerated, precision > 0 ? precision : binaryDigitsGenerated);
            if (outputLength > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            // Shift generated binary digits to the beginning and add leading zeros
            int sourceStart = destination.Length - binaryDigitsGenerated;
            int leadingZeros = outputLength - binaryDigitsGenerated;

            // Move generated digits to their final position
            if (leadingZeros > 0)
            {
                destination.Slice(sourceStart, binaryDigitsGenerated).CopyTo(destination.Slice(leadingZeros));
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[i] = (byte)'0';
                }
            }
            else if (sourceStart > 0)
            {
                destination.Slice(sourceStart, binaryDigitsGenerated).CopyTo(destination);
            }

            bytesWritten = outputLength;
            return true;
        }
        finally
        {
            if (rentedWorkingBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedWorkingBuffer);
            }
        }
    }

    internal static bool TryFormatCurrency(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        // Use CurrencyDecimalDigits as the default precision
        int effectivePrecision = precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits;

        int pos = 0;
        int pattern = isNegative ? formatInfo.CurrencyNegativePattern : formatInfo.CurrencyPositivePattern;
        ReadOnlySpan<char> currencySymbol = formatInfo.CurrencySymbol.AsSpan();
        ReadOnlySpan<char> negativeSign = formatInfo.NegativeSign.AsSpan();

        if (isNegative)
        {
            // Negative patterns: 0: ($n), 1: -$n, 2: $-n, 3: $n-, 4: (n$), 5: -n$, 6: n-$, 7: n$-, 8: -n $, 9: -$ n, 10: n $-, 11: $ n-, 12: $ -n, 13: n- $, 14: ($ n), 15: (n $)
            switch (pattern)
            {
                case 0: // ($n)
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = '(';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars0, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars0;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ')';
                    break;

                case 1: // -$n
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars1, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars1;
                    break;

                case 2: // $-n
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars2, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars2;
                    break;

                case 3: // $n-
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars3, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars3;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    break;

                case 4: // (n$)
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = '(';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars4, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars4;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ')';
                    break;

                case 5: // -n$
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars5, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars5;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                case 6: // n-$
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars6, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars6;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                case 7: // n$-
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars7, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars7;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    break;

                case 8: // -n $
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars8, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars8;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                case 9: // -$ n
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars9, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars9;
                    break;

                case 10: // n $-
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars10, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars10;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    break;

                case 11: // $ n-
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars11, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars11;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    break;

                case 12: // $ -n
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars12, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars12;
                    break;

                case 13: // n- $
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars13, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars13;
                    negativeSign.CopyTo(destination.Slice(pos));
                    pos += negativeSign.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                case 14: // ($ n)
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = '(';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars14, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars14;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ')';
                    break;

                case 15: // (n $)
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = '(';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars15, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars15;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ')';
                    break;

                default:
                    charsWritten = 0;
                    return false;
            }
        }
        else
        {
            // Positive patterns: 0: $n, 1: n$, 2: $ n, 3: n $
            switch (pattern)
            {
                case 0: // $n
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars0, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars0;
                    break;

                case 1: // n$
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars1, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars1;
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                case 2: // $ n
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars2, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars2;
                    break;

                case 3: // n $
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars3, effectivePrecision, formatInfo))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    pos += numberChars3;
                    if (pos + 1 > destination.Length)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[pos++] = ' ';
                    currencySymbol.CopyTo(destination.Slice(pos));
                    pos += currencySymbol.Length;
                    break;

                default:
                    charsWritten = 0;
                    return false;
            }
        }

        charsWritten = pos;
        return true;
    }

    private static bool FormatCurrencyNumber(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        // Handle small numbers (< 1)
        if (decimalPosition <= 0)
        {
            return FormatCurrencySmallNumber(integral, fractional, totalLength, decimalPosition, destination, out charsWritten, precision, formatInfo);
        }

        // Calculate the formatted number components
        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        // Check if rounding the fractional part will carry into the integral part
        bool needsRounding = false;
        int fractionalStart = decimalPosition;
        if (fractionalStart < totalLength)
        {
            int roundingPosition = fractionalStart + precision;
            if (roundingPosition < totalLength)
            {
                byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                if (roundingDigit >= (byte)'5')
                {
                    needsRounding = true;
                }
            }
        }

        Span<byte> roundedDigits = stackalloc byte[totalLength + 1];
        int roundedLength = totalLength;
        bool hasCarry = false;

        if (needsRounding)
        {
            int carry = 1;
            for (int i = fractionalStart + precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            hasCarry = carry > 0;

            if (hasCarry)
            {
                for (int i = totalLength; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength++;
                integralDigits++;
                if (integralDigits > totalLength + 1)
                {
                    trailingZeros++;
                }
            }
            else
            {
                for (int i = fractionalStart + precision; i < totalLength; i++)
                {
                    roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
                }
            }
        }
        else
        {
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Calculate space needed including group separators
        int[] groupSizes = formatInfo.CurrencyGroupSizes;
        int groupSeparatorCount = CalculateGroupSeparatorCount(integralDigits, groupSizes);
        int totalGroupSeparatorLength = groupSeparatorCount * formatInfo.CurrencyGroupSeparator.Length;

        int requiredSize = integralDigits + totalGroupSeparatorLength + (precision > 0 ? formatInfo.CurrencyDecimalSeparator.Length + precision : 0);
        if (requiredSize > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        int pos = 0;

        // Write integral part with group separators (using the rounded digits)
        int newDecimalPosition = hasCarry ? decimalPosition + 1 : decimalPosition;
        WriteIntegralWithGroupSeparatorsFromBytes(roundedDigits.Slice(0, roundedLength), integralDigits, trailingZeros,
            destination, ref pos, formatInfo.CurrencyGroupSeparator, groupSizes);

        // Write fractional part
        if (precision > 0)
        {
            WriteFractionalPartFromBytes(roundedDigits.Slice(0, roundedLength), newDecimalPosition, precision,
                destination, ref pos, formatInfo.CurrencyDecimalSeparator);
        }

        charsWritten = pos;
        return true;
    }

    private static bool FormatCurrencySmallNumber(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int totalLength,
        int decimalPosition,
        Span<char> destination,
        out int charsWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        // Format as 0.xxx
        int requiredSize = 1 + (precision > 0 ? formatInfo.CurrencyDecimalSeparator.Length + precision : 0);
        if (requiredSize > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        int pos = 0;

        destination[pos++] = '0';

        if (precision > 0)
        {
            formatInfo.CurrencyDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.CurrencyDecimalSeparator.Length;

            int leadingZeros = -decimalPosition;

            if (leadingZeros >= precision)
            {
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = '0';
                }
            }
            else
            {
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[pos++] = '0';
                }

                int remainingPrecision = precision - leadingZeros;
                int roundingPosition = remainingPrecision;

                if (roundingPosition < totalLength)
                {
                    byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                    int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                    for (int i = remainingPrecision - 1; i >= 0; i--)
                    {
                        byte digit = GetDigitAtPosition(integral, fractional, i);
                        digit += (byte)carry;
                        if (digit > (byte)'9')
                        {
                            digit = (byte)'0';
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }

                        destination[pos + i] = (char)digit;
                    }

                    pos += remainingPrecision;

                    if (carry > 0)
                    {
                        // Change 0 to 1
                        destination[0] = '1';
                    }
                }
                else
                {
                    for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                    {
                        destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
                    }

                    for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                    {
                        destination[pos++] = '0';
                    }
                }
            }
        }

        charsWritten = pos;
        return true;
    }

    private static void WriteIntegralWithGroupSeparatorsFromBytes(
        ReadOnlySpan<byte> digits,
        int integralDigits,
        int trailingZeros,
        Span<char> buffer,
        ref int pos,
        string groupSeparator,
        int[] groupSizes)
    {
        int digitCount = 0;
        int nextSeparatorAt = integralDigits;
        int separatorsPlaced = 0;
        int totalSeparators = 0;

        if (groupSizes.Length > 0 && groupSeparator.Length > 0)
        {
            totalSeparators = CalculateGroupSeparatorCount(integralDigits, groupSizes);
            if (totalSeparators > 0)
            {
                int groupIndex = 0;
                int accumulated = 0;
                for (int i = 0; i < totalSeparators; i++)
                {
                    int groupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];
                    accumulated += groupSize;
                    groupIndex++;
                }

                nextSeparatorAt = integralDigits - accumulated;
            }
        }

        for (int i = 0; i < Math.Min(integralDigits, digits.Length); i++)
        {
            if (i == nextSeparatorAt && groupSizes.Length > 0 && i > 0)
            {
                groupSeparator.AsSpan().CopyTo(buffer.Slice(pos));
                pos += groupSeparator.Length;

                // Calculate next separator position by moving forward by one group size
                int groupIndex = Math.Min(totalSeparators - separatorsPlaced - 1, groupSizes.Length - 1);
                int groupSize = groupSizes[groupIndex];
                if (groupSize > 0)
                {
                    nextSeparatorAt = i + groupSize;
                    separatorsPlaced++;
                }
                else
                {
                    nextSeparatorAt = integralDigits + 1; // No more separators
                }
            }

            buffer[pos++] = (char)digits[i];
            digitCount++;
        }

        for (int i = 0; i < trailingZeros; i++)
        {
            if (digitCount == nextSeparatorAt && groupSizes.Length > 0 && digitCount > 0)
            {
                groupSeparator.AsSpan().CopyTo(buffer.Slice(pos));
                pos += groupSeparator.Length;

                int remainingDigits = integralDigits - digitCount;
                if (remainingDigits > 0)
                {
                    int groupIndex = Math.Min(totalSeparators - separatorsPlaced - 1, groupSizes.Length - 1);
                    int groupSize = groupSizes[groupIndex];
                    if (groupSize > 0)
                    {
                        nextSeparatorAt = digitCount + groupSize;
                        separatorsPlaced++;
                    }
                }
            }

            buffer[pos++] = '0';
            digitCount++;
        }
    }

    private static void WriteFractionalPartFromBytes(
        ReadOnlySpan<byte> digits,
        int decimalPosition,
        int precision,
        Span<char> buffer,
        ref int pos,
        string decimalSeparator)
    {
        decimalSeparator.AsSpan().CopyTo(buffer.Slice(pos));
        pos += decimalSeparator.Length;

        int fractionalStart = decimalPosition;

        for (int i = 0; i < precision && fractionalStart + i < digits.Length; i++)
        {
            buffer[pos++] = (char)digits[fractionalStart + i];
        }

        // Pad with zeros if needed
        for (int i = Math.Max(0, digits.Length - fractionalStart); i < precision; i++)
        {
            buffer[pos++] = '0';
        }
    }

    internal static bool TryFormatFixedPointWithSeparator(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        string numberDecimalSeparator,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (pos + formatInfo.NegativeSign.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        if (decimalPosition <= 0)
        {
            if (pos + 1 + (precision > 0 ? numberDecimalSeparator.Length + precision : 0) > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            int integralPos = pos;
            destination[pos++] = '0';

            if (precision > 0)
            {
                numberDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
                pos += numberDecimalSeparator.Length;

                int leadingZeros = -decimalPosition;

                if (leadingZeros >= precision)
                {
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = '0';
                    }
                }
                else
                {
                    for (int i = 0; i < leadingZeros; i++)
                    {
                        destination[pos++] = '0';
                    }

                    int remainingPrecision = precision - leadingZeros;
                    int roundingPosition = remainingPrecision;

                    if (roundingPosition < totalLength)
                    {
                        byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                        int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                        for (int i = remainingPrecision - 1; i >= 0; i--)
                        {
                            byte digit = GetDigitAtPosition(integral, fractional, i);
                            digit += (byte)carry;
                            if (digit > (byte)'9')
                            {
                                digit = (byte)'0';
                                carry = 1;
                            }
                            else
                            {
                                carry = 0;
                            }

                            destination[pos + i] = (char)digit;
                        }

                        pos += remainingPrecision;

                        if (carry > 0)
                        {
                            destination[integralPos] = '1';
                        }
                    }
                    else
                    {
                        for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                        {
                            destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
                        }

                        for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                        {
                            destination[pos++] = '0';
                        }
                    }
                }
            }

            charsWritten = pos;
            return true;
        }

        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        if (pos + integralDigits + (precision > 0 ? numberDecimalSeparator.Length + precision : 0) > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        for (int i = 0; i < Math.Min(integralDigits, totalLength); i++)
        {
            destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
        }

        for (int i = 0; i < trailingZeros; i++)
        {
            destination[pos++] = '0';
        }

        if (precision > 0)
        {
            numberDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
            pos += numberDecimalSeparator.Length;

            int fractionalStart = decimalPosition;

            if (fractionalStart >= totalLength)
            {
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = '0';
                }
            }
            else
            {
                int roundingPosition = fractionalStart + precision;

                if (roundingPosition >= totalLength)
                {
                    for (int i = fractionalStart; i < totalLength; i++)
                    {
                        destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
                    }

                    for (int i = totalLength - fractionalStart; i < precision; i++)
                    {
                        destination[pos++] = '0';
                    }
                }
                else
                {
                    byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                    int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                    for (int i = precision - 1; i >= 0; i--)
                    {
                        byte digit = GetDigitAtPosition(integral, fractional, fractionalStart + i);
                        digit += (byte)carry;
                        if (digit > (byte)'9')
                        {
                            digit = (byte)'0';
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }

                        destination[pos + i] = (char)digit;
                    }

                    pos += precision;

                    if (carry > 0)
                    {
                        int backPos = pos - precision - numberDecimalSeparator.Length - 1;
                        while (backPos >= (isNegative ? formatInfo.NegativeSign.Length : 0) && destination[backPos] == '9')
                        {
                            destination[backPos] = '0';
                            backPos--;
                        }

                        if (backPos >= (isNegative ? formatInfo.NegativeSign.Length : 0))
                        {
                            destination[backPos]++;
                        }
                        else
                        {
                            for (int i = pos; i > (isNegative ? formatInfo.NegativeSign.Length : 0); i--)
                            {
                                destination[i] = destination[i - 1];
                            }

                            destination[isNegative ? formatInfo.NegativeSign.Length : 0] = '1';
                            pos++;
                        }
                    }
                }
            }
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatGeneral(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, Span<char> destination, out int charsWritten, int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        int totalLength = integral.Length + fractional.Length;

        // Default precision for General format
        // When not specified, use a reasonable default (15 for double precision)
        int effectivePrecision = precision >= 0 ? precision : 15;

        // Calculate what the exponent would be in scientific notation
        // (position of most significant digit relative to decimal point)
        int scientificExponent = totalLength - 1 + exponent;

        // Check if rounding would cause a carry that increases the exponent
        if (effectivePrecision < totalLength)
        {
            byte roundingDigit = GetDigitAtPosition(integral, fractional, effectivePrecision);
            if (roundingDigit >= (byte)'5')
            {
                // Check if rounding would propagate all the way up
                int carry = 1;
                for (int i = effectivePrecision - 1; i >= 0 && carry > 0; i--)
                {
                    byte digit = GetDigitAtPosition(integral, fractional, i);
                    if (digit != (byte)'9')
                    {
                        carry = 0;
                        break;
                    }
                }

                if (carry > 0)
                {
                    scientificExponent++;
                }
            }
        }

        // Decide between fixed-point and scientific notation
        // Use scientific if exponent < -1 or >= precision (after considering significant digits)
        bool useScientific = scientificExponent < -1 || scientificExponent >= effectivePrecision;

        if (useScientific)
        {
            // Use scientific notation - format as "d.ddd...e±nn"
            return FormatGeneralScientific(isNegative, integral, fractional, exponent, destination, out charsWritten, effectivePrecision, exponentChar, formatInfo);
        }
        else
        {
            // Use fixed-point notation - format as "ddd.ddd"
            return FormatGeneralFixedPoint(isNegative, integral, fractional, exponent, destination, out charsWritten, effectivePrecision, formatInfo);
        }
    }

    private static bool FormatGeneralScientific(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        char exponentChar,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
        }

        int totalLength = integral.Length + fractional.Length;
        int significandStart = pos;

        // Calculate scientific exponent
        int scientificExponent = totalLength - 1 + exponent;

        // Round to precision significant figures
        if (precision > 0 && precision < totalLength)
        {
            byte roundingDigit = GetDigitAtPosition(integral, fractional, precision);
            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            for (int i = precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                destination[pos + i] = (char)digit;
            }

            if (carry > 0)
            {
                // Shift digits right
                for (int i = precision; i > 0; i--)
                {
                    destination[pos + i] = destination[pos + i - 1];
                }

                destination[pos] = '1';
                pos += precision + 1;
                scientificExponent++;
            }
            else
            {
                pos += precision;
            }
        }
        else
        {
            // Output all digits
            for (int i = 0; i < totalLength; i++)
            {
                destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Remove trailing zeros
        for (int trailingZeros = 0; pos > significandStart + 1 && destination[pos - 1] == '0'; trailingZeros++)
        {
            pos--;
        }

        // Insert decimal point after first digit (if there are more digits)
        if (pos > significandStart + 1)
        {
            // Shift digits right to make room for decimal point
            for (int i = pos; i > significandStart + 1; i--)
            {
                destination[i] = destination[i - 1];
            }

            destination[significandStart + 1] = formatInfo.NumberDecimalSeparator[0];
            pos++;

            // Remove trailing zeros after decimal point
            while (pos > significandStart + 2 && destination[pos - 1] == '0')
            {
                pos--;
            }

            // Remove decimal point if no fractional digits remain
            if (destination[pos - 1] == formatInfo.NumberDecimalSeparator[0])
            {
                pos--;
            }
        }
        else if (pos == significandStart)
        {
            // If we removed all digits, write "0"
            destination[pos++] = '0';
            charsWritten = pos;
            return true;
        }

        // Write exponent
        destination[pos++] = exponentChar;

        if (scientificExponent >= 0)
        {
            formatInfo.PositiveSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.PositiveSign.Length;
        }
        else
        {
            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
            scientificExponent = -scientificExponent;
        }

        if (!scientificExponent.TryFormat(destination.Slice(pos), out int expChars))
        {
            charsWritten = 0;
            return false;
        }

        pos += expChars;
        charsWritten = pos;
        return true;
    }

    private static bool FormatGeneralFixedPoint(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (pos + formatInfo.NegativeSign.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        // Apply rounding to precision significant figures
        Span<byte> roundedDigits = stackalloc byte[Math.Max(totalLength, precision + 1)];
        int roundedLength = totalLength;

        if (precision < totalLength)
        {
            // Need to round
            byte roundingDigit = GetDigitAtPosition(integral, fractional, precision);
            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            for (int i = precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            if (carry > 0)
            {
                // Shift digits right and add 1 at front
                for (int i = precision; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength = precision + 1;
                decimalPosition++;
            }
            else
            {
                roundedLength = precision;
            }
        }
        else
        {
            // Copy all digits
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Now format the rounded digits
        if (decimalPosition <= 0)
        {
            // Number less than 1: 0.xxx
            int leadingZeros = -decimalPosition;
            int totalCharsNeeded = 2 + leadingZeros + roundedLength; // "0." + zeros + digits
            if (pos + totalCharsNeeded > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            destination[pos++] = '0';
            destination[pos++] = formatInfo.NumberDecimalSeparator[0];

            // Leading zeros after decimal
            for (int i = 0; i < leadingZeros; i++)
            {
                destination[pos++] = '0';
            }

            // Significant digits
            for (int i = 0; i < roundedLength; i++)
            {
                destination[pos++] = (char)roundedDigits[i];
            }
        }
        else if (decimalPosition >= roundedLength)
        {
            // All digits before decimal, possibly with trailing zeros
            int trailingZeros = decimalPosition - roundedLength;
            int totalCharsNeeded = roundedLength + trailingZeros;
            if (pos + totalCharsNeeded > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            for (int i = 0; i < roundedLength; i++)
            {
                destination[pos++] = (char)roundedDigits[i];
            }

            // Trailing zeros to reach decimal position
            for (int i = 0; i < trailingZeros; i++)
            {
                destination[pos++] = '0';
            }
        }
        else
        {
            // Some digits before and some after decimal
            int fractionalDigits = roundedLength - decimalPosition;
            int totalCharsNeeded = decimalPosition + 1 + fractionalDigits; // integral + "." + fractional
            if (pos + totalCharsNeeded > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            for (int i = 0; i < decimalPosition; i++)
            {
                destination[pos++] = (char)roundedDigits[i];
            }

            destination[pos++] = formatInfo.NumberDecimalSeparator[0];

            for (int i = decimalPosition; i < roundedLength; i++)
            {
                destination[pos++] = (char)roundedDigits[i];
            }
        }

        // Remove trailing zeros after decimal point
        int decimalPointPos = -1;
        for (int i = 0; i < pos; i++)
        {
            if (destination[i] == formatInfo.NumberDecimalSeparator[0])
            {
                decimalPointPos = i;
                break;
            }
        }

        if (decimalPointPos >= 0)
        {
            while (pos > decimalPointPos + 1 && destination[pos - 1] == '0')
            {
                pos--;
            }

            // Remove decimal point if no fractional digits remain
            if (pos == decimalPointPos + 1)
            {
                pos--;
            }
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatNumber(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (pos + formatInfo.NegativeSign.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        if (decimalPosition <= 0)
        {
            if (pos + 1 + (precision > 0 ? formatInfo.NumberDecimalSeparator.Length + precision : 0) > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            int integralPos = pos;
            destination[pos++] = '0';

            if (precision > 0)
            {
                formatInfo.NumberDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
                pos += formatInfo.NumberDecimalSeparator.Length;

                int leadingZeros = -decimalPosition;

                if (leadingZeros >= precision)
                {
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = '0';
                    }
                }
                else
                {
                    for (int i = 0; i < leadingZeros; i++)
                    {
                        destination[pos++] = '0';
                    }

                    int remainingPrecision = precision - leadingZeros;
                    int roundingPosition = remainingPrecision;

                    if (roundingPosition < totalLength)
                    {
                        byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                        int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                        for (int i = remainingPrecision - 1; i >= 0; i--)
                        {
                            byte digit = GetDigitAtPosition(integral, fractional, i);
                            digit += (byte)carry;
                            if (digit > (byte)'9')
                            {
                                digit = (byte)'0';
                                carry = 1;
                            }
                            else
                            {
                                carry = 0;
                            }

                            destination[pos + i] = (char)digit;
                        }

                        pos += remainingPrecision;

                        if (carry > 0)
                        {
                            destination[integralPos] = '1';
                        }
                    }
                    else
                    {
                        for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                        {
                            destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
                        }

                        for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                        {
                            destination[pos++] = '0';
                        }
                    }
                }
            }

            charsWritten = pos;
            return true;
        }

        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        // Check if rounding the fractional part will carry into the integral part
        bool needsRounding = false;
        int fractionalStart = decimalPosition;
        if (fractionalStart < totalLength)
        {
            int roundingPosition = fractionalStart + precision;
            if (roundingPosition < totalLength)
            {
                byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                if (roundingDigit >= (byte)'5')
                {
                    needsRounding = true;
                }
            }
        }

        Span<byte> roundedDigits = stackalloc byte[totalLength + 1];
        int roundedLength = totalLength;
        bool hasCarry = false;

        if (needsRounding)
        {
            int carry = 1;
            for (int i = fractionalStart + precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            hasCarry = carry > 0;

            if (hasCarry)
            {
                for (int i = totalLength; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength++;
                integralDigits++;
                if (integralDigits > totalLength + 1)
                {
                    trailingZeros++;
                }
            }
            else
            {
                for (int i = fractionalStart + precision; i < totalLength; i++)
                {
                    roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
                }
            }
        }
        else
        {
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Calculate space needed including group separators
        int[] groupSizes = formatInfo.NumberGroupSizes;
        int groupSeparatorCount = 0;
        if (groupSizes.Length > 0 && formatInfo.NumberGroupSeparator.Length > 0)
        {
            int digitsRemaining = integralDigits;
            int groupIndex = 0;
            while (digitsRemaining > 0 && groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)] > 0)
            {
                int groupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];
                if (digitsRemaining > groupSize)
                {
                    groupSeparatorCount++;
                    digitsRemaining -= groupSize;
                    groupIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        int totalGroupSeparatorLength = groupSeparatorCount * formatInfo.NumberGroupSeparator.Length;

        if (pos + integralDigits + totalGroupSeparatorLength + (precision > 0 ? formatInfo.NumberDecimalSeparator.Length + precision : 0) > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        // Write integral part with group separators (using the rounded digits)
        int newDecimalPosition = hasCarry ? decimalPosition + 1 : decimalPosition;
        int digitCount = 0;
        WriteIntegralWithGroupSeparatorsFromBytes(roundedDigits.Slice(0, roundedLength), integralDigits, trailingZeros,
            destination.Slice(pos), ref digitCount, formatInfo.NumberGroupSeparator, groupSizes);
        pos += digitCount;

        if (precision > 0)
        {
            WriteFractionalPartFromBytes(roundedDigits.Slice(0, roundedLength), newDecimalPosition, precision,
                destination, ref pos, formatInfo.NumberDecimalSeparator);
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatExponential(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<char> destination,
        out int charsWritten,
        int precision,
        char exponentChar,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
        }

        int totalLength = integral.Length + fractional.Length;

        if (totalLength == 0)
        {
            // Special case for zero
            if (pos + 1 + (precision > 0 ? 1 + precision : 0) + 5 > destination.Length) // "0.000e+000"
            {
                charsWritten = 0;
                return false;
            }

            destination[pos++] = '0';

            if (precision > 0)
            {
                destination[pos++] = formatInfo.NumberDecimalSeparator[0];
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = '0';
                }
            }

            destination[pos++] = exponentChar;
            destination[pos++] = '+';
            destination[pos++] = '0';
            destination[pos++] = '0';
            destination[pos++] = '0';

            charsWritten = pos;
            return true;
        }

        // Calculate the actual exponent for scientific notation
        // We want one digit before the decimal point
        int scientificExponent = totalLength - 1 + exponent;

        // Check buffer size
        if (pos + 1 + (precision > 0 ? 1 + precision : 0) + 1 + 1 + 3 > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        // Write first digit
        byte firstDigit = GetDigitAtPosition(integral, fractional, 0);

        // Handle rounding
        if (precision < totalLength - 1)
        {
            // Need to round
            int roundPos = precision + 1;
            byte roundingDigit = roundPos < totalLength ? GetDigitAtPosition(integral, fractional, roundPos) : (byte)'0';

            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            // Round the fractional digits
            Span<byte> roundedDigits = stackalloc byte[precision + 1];
            roundedDigits[0] = firstDigit;

            for (int i = precision; i >= 1; i--)
            {
                byte digit = i < totalLength ? GetDigitAtPosition(integral, fractional, i) : (byte)'0';
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            // Handle carry into first digit
            firstDigit += (byte)carry;
            if (firstDigit > (byte)'9')
            {
                firstDigit = (byte)'0';
                carry = 1;
            }
            else
            {
                carry = 0;
            }

            roundedDigits[0] = firstDigit;

            // If carry overflowed, adjust
            if (carry > 0)
            {
                scientificExponent++;
                destination[pos++] = '1';

                if (precision > 0)
                {
                    destination[pos++] = formatInfo.NumberDecimalSeparator[0];
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = '0';
                    }
                }
            }
            else
            {
                destination[pos++] = (char)roundedDigits[0];

                if (precision > 0)
                {
                    destination[pos++] = formatInfo.NumberDecimalSeparator[0];
                    for (int i = 1; i <= precision; i++)
                    {
                        destination[pos++] = (char)roundedDigits[i];
                    }
                }
            }
        }
        else
        {
            // No rounding needed
            destination[pos++] = (char)firstDigit;

            if (precision > 0)
            {
                destination[pos++] = formatInfo.NumberDecimalSeparator[0];

                for (int i = 1; i <= precision && i < totalLength; i++)
                {
                    destination[pos++] = (char)GetDigitAtPosition(integral, fractional, i);
                }

                // Pad with zeros if needed
                for (int i = totalLength; i <= precision; i++)
                {
                    destination[pos++] = '0';
                }
            }
        }

        // Write exponent
        destination[pos++] = exponentChar;

        if (scientificExponent >= 0)
        {
            destination[pos++] = '+';
        }
        else
        {
            formatInfo.NegativeSign.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NegativeSign.Length;
            scientificExponent = -scientificExponent;
        }

        // Format exponent with at least 3 digits
        if (scientificExponent >= 100)
        {
            if (!scientificExponent.TryFormat(destination.Slice(pos), out int expChars, "D3", formatInfo))
            {
                charsWritten = 0;
                return false;
            }

            pos += expChars;
        }
        else
        {
            destination[pos++] = (char)('0' + (scientificExponent / 100));
            destination[pos++] = (char)('0' + ((scientificExponent / 10) % 10));
            destination[pos++] = (char)('0' + (scientificExponent % 10));
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatZero(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (format.IsEmpty)
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

        var formatInfo = NumberFormatInfo.GetInstance(provider);
        char formatType = char.ToUpperInvariant(format[0]);
        int precision = -1;

        if (format.Length > 1 && !TryParseInt32(format.Slice(1), out precision))
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

        return formatType switch
        {
            'G' => WriteChar(destination, out charsWritten, '0'),
            'F' or 'N' => TryFormatZeroFixedPoint(destination, out charsWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'E' => TryFormatZeroExponential(destination, out charsWritten, precision >= 0 ? precision : 6, format[0], formatInfo),
            'C' => TryFormatZeroCurrency(destination, out charsWritten, precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => TryFormatZeroPercent(destination, out charsWritten, precision >= 0 ? precision : formatInfo.PercentDecimalDigits, formatInfo),
            'X' => TryFormatZeroHexOrBinary(destination, out charsWritten, precision),
            'B' => TryFormatZeroHexOrBinary(destination, out charsWritten, precision),
            _ => WriteChar(destination, out charsWritten, '0'),
        };
    }

    internal static bool TryFormatZeroFixedPoint(Span<char> destination, out int charsWritten, int precision, NumberFormatInfo formatInfo)
    {
        int requiredLength = 1 + (precision > 0 ? formatInfo.NumberDecimalSeparator.Length + precision : 0);

        if (requiredLength > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        int pos = 0;
        destination[pos++] = '0';

        if (precision > 0)
        {
            formatInfo.NumberDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NumberDecimalSeparator.Length;

            for (int i = 0; i < precision; i++)
            {
                destination[pos++] = '0';
            }
        }

        charsWritten = pos;
        return true;
    }

    internal static bool TryFormatZeroExponential(Span<char> destination, out int charsWritten, int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        int requiredLength = 1 + (precision > 0 ? formatInfo.NumberDecimalSeparator.Length + precision : 0) + 5; // E+000

        if (requiredLength > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        int pos = 0;
        destination[pos++] = '0';

        if (precision > 0)
        {
            formatInfo.NumberDecimalSeparator.AsSpan().CopyTo(destination.Slice(pos));
            pos += formatInfo.NumberDecimalSeparator.Length;

            for (int i = 0; i < precision; i++)
            {
                destination[pos++] = '0';
            }
        }

        destination[pos++] = exponentChar;
        destination[pos++] = '+';
        destination[pos++] = '0';
        destination[pos++] = '0';
        destination[pos++] = '0';

        charsWritten = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryFormatZeroCurrency(Span<char> destination, out int charsWritten, int precision, NumberFormatInfo formatInfo)
    {
        // Format the zero number part first
        Span<char> tempDest = stackalloc char[100];
        if (!TryFormatZeroFixedPoint(tempDest, out int numberChars, precision, formatInfo))
        {
            charsWritten = 0;
            return false;
        }

        // Apply positive currency pattern (zero is not negative)
        int pattern = formatInfo.CurrencyPositivePattern;
        ReadOnlySpan<char> currencySymbol = formatInfo.CurrencySymbol.AsSpan();
        int pos = 0;

        switch (pattern)
        {
            case 0: // $n
                if (pos + currencySymbol.Length + numberChars > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }

                currencySymbol.CopyTo(destination.Slice(pos));
                pos += currencySymbol.Length;
                tempDest.Slice(0, numberChars).CopyTo(destination.Slice(pos));
                pos += numberChars;
                break;

            case 1: // n$
                if (pos + numberChars + currencySymbol.Length > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }

                tempDest.Slice(0, numberChars).CopyTo(destination.Slice(pos));
                pos += numberChars;
                currencySymbol.CopyTo(destination.Slice(pos));
                pos += currencySymbol.Length;
                break;

            case 2: // $ n
                if (pos + currencySymbol.Length + 1 + numberChars > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }

                currencySymbol.CopyTo(destination.Slice(pos));
                pos += currencySymbol.Length;
                destination[pos++] = ' ';
                tempDest.Slice(0, numberChars).CopyTo(destination.Slice(pos));
                pos += numberChars;
                break;

            case 3: // n $
                if (pos + numberChars + 1 + currencySymbol.Length > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }

                tempDest.Slice(0, numberChars).CopyTo(destination.Slice(pos));
                pos += numberChars;
                destination[pos++] = ' ';
                currencySymbol.CopyTo(destination.Slice(pos));
                pos += currencySymbol.Length;
                break;

            default:
                charsWritten = 0;
                return false;
        }

        charsWritten = pos;
        return true;
    }

    private static bool TryFormatZeroPercent(Span<char> destination, out int charsWritten, int precision, NumberFormatInfo formatInfo)
    {
        if (!TryFormatZeroFixedPoint(destination, out int tempChars, precision, formatInfo))
        {
            charsWritten = 0;
            return false;
        }

        int symbolLength = formatInfo.PercentSymbol.Length;
        int requiredLength = tempChars + 1 + symbolLength;

        if (requiredLength > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        destination[tempChars] = ' ';
        formatInfo.PercentSymbol.AsSpan().CopyTo(destination.Slice(tempChars + 1));

        charsWritten = requiredLength;
        return true;
    }

    private static bool TryFormatZeroHexOrBinary(Span<char> destination, out int charsWritten, int precision)
    {
        int requiredLength = precision > 0 ? precision : 1;
        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        for (int i = 0; i < requiredLength; i++)
        {
            destination[i] = '0';
        }

        charsWritten = requiredLength;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInt32(ReadOnlySpan<char> span, out int value)
    {
#if NET
        return int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#else
        return Numerics.ParsingPolyfills.TryParseInt32Invariant(span, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool WriteChar(Span<char> destination, out int charsWritten, char value)
    {
        if (destination.Length == 0)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = value;
        charsWritten = 1;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool WriteByte(Span<byte> destination, out int bytesWritten, byte value)
    {
        if (destination.Length == 0)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = value;
        bytesWritten = 1;
        return true;
    }

    // Optimizations based on the https:// en.wikipedia.org/wiki/Divisibility_rule rules.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByTwo(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        byte value = GetValueAtPosition(integral, fractional, maxSignificandIndex);
        return value % 2 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByThree(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional)
    {
        int accumulator = 0;
        int maxSignificandIndex = integral.Length + fractional.Length;
        for (int i = 0; i < maxSignificandIndex; ++i)
        {
            switch (GetDigitAtPosition(integral, fractional, i))
            {
                case (byte)'1':
                case (byte)'4':
                case (byte)'7':
                    accumulator++;
                    break;

                case (byte)'2':
                case (byte)'5':
                case (byte)'8':
                    accumulator--;
                    break;
            }
        }

        return accumulator % 3 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByFour(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        return (GetValueAtPosition(integral, fractional, maxSignificandIndex) +
               (GetValueAtPosition(integral, fractional, maxSignificandIndex - 1) * 2))
               % 4 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByFive(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        byte value = GetValueAtPosition(integral, fractional, maxSignificandIndex);
        return value == 0 || value == 5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleBySix(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        byte value = GetValueAtPosition(integral, fractional, maxSignificandIndex);
        if (value % 2 != 0)
        {
            return false;
        }

        int accumulator = 0;
        int realMaxSignificandIndex = integral.Length + fractional.Length;
        for (int i = 0; i < realMaxSignificandIndex; ++i)
        {
            switch (GetDigitAtPosition(integral, fractional, i))
            {
                case (byte)'1':
                case (byte)'4':
                case (byte)'7':
                    accumulator++;
                    break;

                case (byte)'2':
                case (byte)'5':
                case (byte)'8':
                    accumulator--;
                    break;
            }
        }

        return accumulator % 3 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByEight(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        return (GetValueAtPosition(integral, fractional, maxSignificandIndex) +
               (GetValueAtPosition(integral, fractional, maxSignificandIndex - 1) * 2) +
               (GetValueAtPosition(integral, fractional, maxSignificandIndex - 2) * 4))
               % 8 == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDivisibleByTen(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        byte value = GetValueAtPosition(integral, fractional, maxSignificandIndex);
        return value == 0;
    }

    private static bool GeneralPurposeIsMultipleOf(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex, ulong divisor)
    {
        // Step 5.
        // for each index i, from 0 to the length of the significand, accumulate the remainder using the mod of the divisor
        // i.e. remainder = (remainder * 10 + digitValue) % divisor;
        // Use the GetDigitAtPosition() method to get the digit value using the integral and fractional parts, and the net exponent.
        // Return remainder == 0.
        // By using a ulong for the divisor, we support ~19 digits of precision in the divisor
        ulong remainder = 0;

        int maxRealSignificandIndex = integral.Length + fractional.Length;

        for (int i = 0; i < maxRealSignificandIndex; ++i)
        {
            remainder = (remainder * 10 + GetValueAtPosition(integral, fractional, i)) % divisor;
        }

        // Now, we need to work on the remaining exponent that is the right-padded zeros
        // We need to do this in chunks, as 10^exponent may be larger than ulong.MaxValue
        int remainingExponent = maxSignificandIndex - maxRealSignificandIndex;
        if (remainingExponent > 0)
        {
            int count = Math.DivRem(remainingExponent, MaxExponent, out int lastExponent);
            for (int i = 0; i < count; ++i)
            {
                remainder = (remainder * (MaxTenPowExponent % divisor)) % divisor;
            }

            if (lastExponent > 0)
            {
                remainder = (remainder * (TenPowTable[lastExponent] % divisor)) % divisor;
            }
        }

        // Note that the remainder is not the *true* remainder - we have not corrected for any initial
        // exponent scaling; however, this is not necessary as we are only interested in the comparison with zero.
        return remainder == 0;
    }

    private static bool GeneralPurposeIsMultipleOf(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex, System.Numerics.BigInteger divisor)
    {
        // Step 5.
        // for each index i, from 0 to the length of the significand, accumulate the remainder using the mod of the divisor
        // i.e. remainder = (remainder * 10 + digitValue) % divisor;
        // Use the GetDigitAtPosition() method to get the digit value using the integral and fractional parts, and the net exponent.
        // Return remainder == 0.
        // By using a ulong for the divisor, we support ~19 digits of precision in the divisor
        System.Numerics.BigInteger remainder = 0;

        int maxRealSignificandIndex = integral.Length + fractional.Length;

        for (int i = 0; i < maxRealSignificandIndex; ++i)
        {
            remainder = (remainder * 10 + GetValueAtPosition(integral, fractional, i)) % divisor;
        }

        // Now, we need to work on the remaining exponent that is the right-padded zeros
        // We need to do this in chunks, as 10^exponent may be larger than ulong.MaxValue
        int remainingExponent = maxSignificandIndex - maxRealSignificandIndex;
        if (remainingExponent > 0)
        {
            int count = Math.DivRem(remainingExponent, MaxExponent, out int lastExponent);
            for (int i = 0; i < count; ++i)
            {
                remainder = (remainder * (MaxTenPowExponent % divisor)) % divisor;
            }

            if (lastExponent > 0)
            {
                remainder = (remainder * (TenPowTable[lastExponent] % divisor)) % divisor;
            }
        }

        // Note that the remainder is not the *true* remainder - we have not corrected for any initial
        // exponent scaling; however, this is not necessary as we are only interested in the comparison with zero.
        return remainder == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetValueAtPosition(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int maxSignificandIndex)
    {
        return (byte)(GetDigitAtPosition(integral, fractional, maxSignificandIndex) - (byte)'0');
    }

    /***** TRY FORMAT UTF-8 *****/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        ParseNumber(span, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        return TryFormatNumber(span, destination, out bytesWritten, format, provider, isNegative, integral, fractional, exponent);
    }

    internal static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider, bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        if (integral.Length == 0 && fractional.Length == 0)
        {
            // Fast path for zero, which is common and can be formatted without any further processing.
            return TryFormatZero(destination, out bytesWritten, format, provider);
        }

        if (format.IsEmpty)
        {
            if (span.TryCopyTo(destination))
            {
                bytesWritten = span.Length;
                return true;
            }

            bytesWritten = 0;
            return false;
        }

        char formatType = char.ToUpperInvariant(format[0]);
        int precision = -1;

        if (format.Length > 1)
        {
            if (!TryParseInt32(format.Slice(1), out precision))
            {
                bytesWritten = 0;
                return false;
            }
        }

        var formatInfo = NumberFormatInfo.GetInstance(provider);

        return formatType switch
        {
            'G' => TryFormatGeneral(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision, char.IsLower(format[0]) ? 'e' : 'E', formatInfo),
            'F' => TryFormatFixedPointWithSeparator(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo.NumberDecimalSeparator, formatInfo),
            'N' => TryFormatNumber(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'E' => TryFormatExponential(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision >= 0 ? precision : 6, char.IsLower(format[0]) ? 'e' : 'E', formatInfo),
            'C' => TryFormatCurrency(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => TryFormatPercent(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision >= 0 ? precision : formatInfo.PercentDecimalDigits, formatInfo),
            'X' => TryFormatHexadecimal(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision, char.IsLower(format[0])),
            'B' => TryFormatBinary(isNegative, integral, fractional, exponent, destination, out bytesWritten, precision),
            _ => UnknownFormat(out bytesWritten)
        };
    }

    internal static bool TryFormatPercent(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, Span<byte> destination, out int bytesWritten, int precision, NumberFormatInfo formatInfo)
    {
        // Percent format multiplies by 100 (shift exponent by +2), uses group separators, and adds % symbol
        int adjustedExponent = exponent + 2;

        // Use PercentDecimalDigits as the default precision
        int effectivePrecision = precision >= 0 ? precision : formatInfo.PercentDecimalDigits;

        // Create a temporary NumberFormatInfo for formatting the number part using percent settings
        var tempFormatInfo = new NumberFormatInfo
        {
            NumberDecimalSeparator = formatInfo.PercentDecimalSeparator,
            NumberGroupSeparator = formatInfo.PercentGroupSeparator,
            NumberGroupSizes = formatInfo.PercentGroupSizes,
            NegativeSign = formatInfo.NegativeSign
        };

        int pos = 0;
        int numberChars;

        if (isNegative)
        {
            // Handle negative patterns
            int pattern = formatInfo.PercentNegativePattern;
            string negSign = formatInfo.NegativeSign;
            string percentSym = formatInfo.PercentSymbol;

            switch (pattern)
            {
                case 0: // -n %
                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength1))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength1;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + 1 + percentSym.Length > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength1))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength1;
                    break;

                case 1: // -n%
                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength2;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (pos + percentSym.Length > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength2;
                    break;

                case 2: // -%n
                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength3;

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength3;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 3: // %-n
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength4;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength4;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 4: // %n-
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength5))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength5;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength5))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength5;
                    break;

                case 5: // n-%
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength6))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength6;

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength6))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength6;
                    break;

                case 6: // n%-
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength7))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength7;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength7))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength7;
                    break;

                case 7: // -% n
                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength8))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength8;

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength8))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength8;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 8: // n %-
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength9))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength9;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength9))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength9;
                    break;

                case 9: // % n-
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength10))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength10;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength10))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength10;
                    break;

                case 10: // % -n
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength11))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength11;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength11))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength11;

                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 11: // n- %
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (!JsonReaderHelper.TryGetUtf8FromText(negSign.AsSpan(), destination.Slice(pos), out int negSignLength12))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negSignLength12;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength12))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength12;
                    break;

                default:
                    bytesWritten = 0;
                    return false;
            }
        }
        else
        {
            // Handle positive patterns
            int pattern = formatInfo.PercentPositivePattern;
            string percentSym = formatInfo.PercentSymbol;

            switch (pattern)
            {
                case 0: // n %
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength1))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength1;
                    break;

                case 1: // n%
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength2;
                    break;

                case 2: // %n
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength3;
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                case 3: // % n
                    if (!JsonReaderHelper.TryGetUtf8FromText(percentSym.AsSpan(), destination.Slice(pos), out int percentSymLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += percentSymLength4;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!TryFormatNumber(false, integral, fractional, adjustedExponent, destination.Slice(pos), out numberChars, effectivePrecision, tempFormatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars;
                    break;

                default:
                    bytesWritten = 0;
                    return false;
            }
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatHexadecimal(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        bool lowercase)
    {
        // Hexadecimal format only works for non-negative integers
        if (isNegative || exponent < 0)
        {
            bytesWritten = 0;
            return false;
        }

        // Convert decimal representation to hex using virtualized access
        int totalLength = GetDecimalLength(integral, fractional, exponent);
        return DecimalToHexVirtualizedUtf8(integral, fractional, exponent, totalLength, destination, out bytesWritten, precision, lowercase);
    }

    internal static bool TryFormatBinary(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision)
    {
        // Binary format only works for non-negative integers
        if (isNegative || exponent < 0)
        {
            bytesWritten = 0;
            return false;
        }

        // Convert decimal representation to binary using virtualized access
        int totalLength = GetDecimalLength(integral, fractional, exponent);
        return DecimalToBinaryVirtualizedUtf8(integral, fractional, exponent, totalLength, destination, out bytesWritten, precision);
    }

    internal static bool TryFormatCurrency(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        // Use CurrencyDecimalDigits as the default precision
        int effectivePrecision = precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits;

        int pos = 0;
        int pattern = isNegative ? formatInfo.CurrencyNegativePattern : formatInfo.CurrencyPositivePattern;
        ReadOnlySpan<char> currencySymbol = formatInfo.CurrencySymbol.AsSpan();
        ReadOnlySpan<char> negativeSign = formatInfo.NegativeSign.AsSpan();

        if (isNegative)
        {
            // Negative patterns: 0: ($n), 1: -$n, 2: $-n, 3: $n-, 4: (n$), 5: -n$, 6: n-$, 7: n$-, 8: -n $, 9: -$ n, 10: n $-, 11: $ n-, 12: $ -n, 13: n- $, 14: ($ n), 15: (n $)
            switch (pattern)
            {
                case 0: // ($n)
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)'(';

                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength1))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength1;

                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars0, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars0;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)')';
                    break;

                case 1: // -$n
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength2;

                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength2;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars1, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars1;
                    break;

                case 2: // $-n
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength3;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength3;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars2, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars2;
                    break;

                case 3: // $n-
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength4;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars3, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars3;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength4;
                    break;

                case 4: // (n$)
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)'(';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars4, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars4;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength5))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength5;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)')';
                    break;

                case 5: // -n$
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength6))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength6;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars5, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars5;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength6))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength6;
                    break;

                case 6: // n-$
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars6, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars6;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength7))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength7;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength7))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength7;
                    break;

                case 7: // n$-
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars7, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars7;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength8))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength8;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength8))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength8;
                    break;

                case 8: // -n $
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength9))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength9;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars8, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars8;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength9))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength9;
                    break;

                case 9: // -$ n
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength10))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength10;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength10))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength10;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars9, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars9;
                    break;

                case 10: // n $-
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars10, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars10;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength11))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength11;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength11))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength11;
                    break;

                case 11: // $ n-
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength12))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength12;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars11, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars11;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength12))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength12;
                    break;

                case 12: // $ -n
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength13))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength13;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength13))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength13;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars12, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars12;
                    break;

                case 13: // n- $
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars13, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars13;
                    if (!JsonReaderHelper.TryGetUtf8FromText(negativeSign, destination.Slice(pos), out int negativeSignLength14))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += negativeSignLength14;

                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';

                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength14))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength14;
                    break;

                case 14: // ($ n)
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)'(';
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength15))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength15;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars14, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars14;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)')';
                    break;

                case 15: // (n $)
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)'(';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars15, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars15;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength16))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength16;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)')';
                    break;

                default:
                    bytesWritten = 0;
                    return false;
            }
        }
        else
        {
            // Positive patterns: 0: $n, 1: n$, 2: $ n, 3: n $
            switch (pattern)
            {
                case 0: // $n
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength1))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength1;
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars0, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars0;
                    break;

                case 1: // n$
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars1, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars1;
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength2))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength2;
                    break;

                case 2: // $ n
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength3))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength3;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars2, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars2;
                    break;

                case 3: // n $
                    if (!FormatCurrencyNumber(integral, fractional, exponent, destination.Slice(pos), out int numberChars3, effectivePrecision, formatInfo))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberChars3;
                    if (pos + 1 > destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[pos++] = (byte)' ';
                    if (!JsonReaderHelper.TryGetUtf8FromText(currencySymbol, destination.Slice(pos), out int currencySymbolLength4))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += currencySymbolLength4;
                    break;

                default:
                    bytesWritten = 0;
                    return false;
            }
        }

        bytesWritten = pos;
        return true;
    }

    private static bool FormatCurrencyNumber(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        // Handle small numbers (< 1)
        if (decimalPosition <= 0)
        {
            return FormatCurrencySmallNumber(integral, fractional, totalLength, decimalPosition, destination, out bytesWritten, precision, formatInfo);
        }

        // Calculate the formatted number components
        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        // Check if rounding the fractional part will carry into the integral part
        bool needsRounding = false;
        int fractionalStart = decimalPosition;
        if (fractionalStart < totalLength)
        {
            int roundingPosition = fractionalStart + precision;
            if (roundingPosition < totalLength)
            {
                byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                if (roundingDigit >= (byte)'5')
                {
                    needsRounding = true;
                }
            }
        }

        Span<byte> roundedDigits = stackalloc byte[totalLength + 1];
        int roundedLength = totalLength;
        bool hasCarry = false;

        if (needsRounding)
        {
            int carry = 1;
            for (int i = fractionalStart + precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            hasCarry = carry > 0;

            if (hasCarry)
            {
                for (int i = totalLength; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength++;
                integralDigits++;
                if (integralDigits > totalLength + 1)
                {
                    trailingZeros++;
                }
            }
            else
            {
                for (int i = fractionalStart + precision; i < totalLength; i++)
                {
                    roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
                }
            }
        }
        else
        {
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Calculate space needed including group separators
        int[] groupSizes = formatInfo.CurrencyGroupSizes;
        int groupSeparatorCount = CalculateGroupSeparatorCount(integralDigits, groupSizes);
        int totalGroupSeparatorLength = groupSeparatorCount * Encoding.UTF8.GetByteCount(formatInfo.CurrencyGroupSeparator);

        int requiredSize = integralDigits + totalGroupSeparatorLength + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.CurrencyDecimalSeparator) + precision : 0);
        if (requiredSize > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        int pos = 0;

        // Write integral part with group separators (using the rounded digits)
        int newDecimalPosition = hasCarry ? decimalPosition + 1 : decimalPosition;
        WriteIntegralWithGroupSeparatorsUnsafeFromBytes(roundedDigits.Slice(0, roundedLength), integralDigits, trailingZeros,
            destination, ref pos, formatInfo.CurrencyGroupSeparator, groupSizes);

        // Write fractional part
        if (precision > 0)
        {
            WriteFractionalPartUnsafeFromBytes(roundedDigits.Slice(0, roundedLength), newDecimalPosition, precision,
                destination, ref pos, formatInfo.CurrencyDecimalSeparator);
        }

        bytesWritten = pos;
        return true;
    }

    private static bool FormatCurrencySmallNumber(
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int totalLength,
        int decimalPosition,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        // Format as 0.xxx
        int requiredSize = 1 + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.CurrencyDecimalSeparator) + precision : 0);
        if (requiredSize > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        int pos = 0;

        destination[pos++] = (byte)'0';

        if (precision > 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.CurrencyDecimalSeparator, destination.Slice(pos), out int decimalSeparatorLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += decimalSeparatorLength;

            int leadingZeros = -decimalPosition;

            if (leadingZeros >= precision)
            {
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = (byte)'0';
                }
            }
            else
            {
                for (int i = 0; i < leadingZeros; i++)
                {
                    destination[pos++] = (byte)'0';
                }

                int remainingPrecision = precision - leadingZeros;
                int roundingPosition = remainingPrecision;

                if (roundingPosition < totalLength)
                {
                    byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                    int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                    for (int i = remainingPrecision - 1; i >= 0; i--)
                    {
                        byte digit = GetDigitAtPosition(integral, fractional, i);
                        digit += (byte)carry;
                        if (digit > (byte)'9')
                        {
                            digit = (byte)'0';
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }

                        destination[pos + i] = digit;
                    }

                    pos += remainingPrecision;

                    if (carry > 0)
                    {
                        // Change 0 to 1
                        destination[0] = (byte)'1';
                    }
                }
                else
                {
                    for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                    {
                        destination[pos++] = GetDigitAtPosition(integral, fractional, i);
                    }

                    for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }
                }
            }
        }

        bytesWritten = pos;
        return true;
    }

    private static int CalculateGroupSeparatorCount(int integralDigits, int[] groupSizes)
    {
        if (groupSizes.Length == 0) return 0;

        int count = 0;
        int digitsRemaining = integralDigits;
        int groupIndex = 0;

        while (digitsRemaining > 0 && groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)] > 0)
        {
            int groupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];
            if (digitsRemaining > groupSize)
            {
                count++;
                digitsRemaining -= groupSize;
                groupIndex++;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    private static void WriteIntegralWithGroupSeparatorsUnsafeFromBytes(
        ReadOnlySpan<byte> digits,
        int integralDigits,
        int trailingZeros,
        Span<byte> buffer,
        ref int pos,
        string groupSeparator,
        int[] groupSizes)
    {
        int digitCount = 0;
        int nextSeparatorAt = integralDigits;
        int separatorsPlaced = 0;
        int totalSeparators = 0;

        if (groupSizes.Length > 0 && groupSeparator.Length > 0)
        {
            totalSeparators = CalculateGroupSeparatorCount(integralDigits, groupSizes);
            if (totalSeparators > 0)
            {
                int groupIndex = 0;
                int accumulated = 0;
                for (int i = 0; i < totalSeparators; i++)
                {
                    int groupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];
                    accumulated += groupSize;
                    groupIndex++;
                }

                nextSeparatorAt = integralDigits - accumulated;
            }
        }

        for (int i = 0; i < Math.Min(integralDigits, digits.Length); i++)
        {
            if (i == nextSeparatorAt && groupSizes.Length > 0 && i > 0)
            {
                JsonReaderHelper.TryGetUtf8FromText(groupSeparator, buffer.Slice(pos), out int groupSeparatorLength);
                pos += groupSeparatorLength;

                // Calculate next separator position by moving forward by one group size
                int groupIndex = Math.Min(totalSeparators - separatorsPlaced - 1, groupSizes.Length - 1);
                int groupSize = groupSizes[groupIndex];
                if (groupSize > 0)
                {
                    nextSeparatorAt = i + groupSize;
                    separatorsPlaced++;
                }
                else
                {
                    nextSeparatorAt = integralDigits + 1; // No more separators
                }
            }

            buffer[pos++] = digits[i];
            digitCount++;
        }

        for (int i = 0; i < trailingZeros; i++)
        {
            if (digitCount == nextSeparatorAt && groupSizes.Length > 0 && digitCount > 0)
            {
                JsonReaderHelper.TryGetUtf8FromText(groupSeparator, buffer.Slice(pos), out int groupSeparatorLength);
                pos += groupSeparatorLength;

                int remainingDigits = integralDigits - digitCount;
                if (remainingDigits > 0)
                {
                    int groupIndex = Math.Min(totalSeparators - separatorsPlaced - 1, groupSizes.Length - 1);
                    int groupSize = groupSizes[groupIndex];
                    if (groupSize > 0)
                    {
                        nextSeparatorAt = digitCount + groupSize;
                        separatorsPlaced++;
                    }
                }
            }

            buffer[pos++] = (byte)'0';
            digitCount++;
        }
    }

    private static void WriteFractionalPartUnsafeFromBytes(
        ReadOnlySpan<byte> digits,
        int decimalPosition,
        int precision,
        Span<byte> buffer,
        ref int pos,
        string decimalSeparator)
    {
        JsonReaderHelper.TryGetUtf8FromText(decimalSeparator, buffer.Slice(pos), out int decimalSeparatorLength);
        pos += decimalSeparatorLength;

        int fractionalStart = decimalPosition;

        for (int i = 0; i < precision && fractionalStart + i < digits.Length; i++)
        {
            buffer[pos++] = digits[fractionalStart + i];
        }

        // Pad with zeros if needed
        for (int i = Math.Max(0, digits.Length - fractionalStart); i < precision; i++)
        {
            buffer[pos++] = (byte)'0';
        }
    }

    internal static bool TryFormatFixedPointWithSeparator(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        string numberDecimalSeparator,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        if (decimalPosition <= 0)
        {
            if (pos + 1 + (precision > 0 ? Encoding.UTF8.GetByteCount(numberDecimalSeparator) + precision : 0) > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            int integralPos = pos;
            destination[pos++] = (byte)'0';

            if (precision > 0)
            {
                if (!JsonReaderHelper.TryGetUtf8FromText(numberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += numberDecimalSeparatorLength;

                int leadingZeros = -decimalPosition;

                if (leadingZeros >= precision)
                {
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }
                }
                else
                {
                    for (int i = 0; i < leadingZeros; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }

                    int remainingPrecision = precision - leadingZeros;
                    int roundingPosition = remainingPrecision;

                    if (roundingPosition < totalLength)
                    {
                        byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                        int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                        for (int i = remainingPrecision - 1; i >= 0; i--)
                        {
                            byte digit = GetDigitAtPosition(integral, fractional, i);
                            digit += (byte)carry;
                            if (digit > (byte)'9')
                            {
                                digit = (byte)'0';
                                carry = 1;
                            }
                            else
                            {
                                carry = 0;
                            }

                            destination[pos + i] = digit;
                        }

                        pos += remainingPrecision;

                        if (carry > 0)
                        {
                            destination[integralPos] = (byte)'1';
                        }
                    }
                    else
                    {
                        for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                        {
                            destination[pos++] = GetDigitAtPosition(integral, fractional, i);
                        }

                        for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                        {
                            destination[pos++] = (byte)'0';
                        }
                    }
                }
            }

            bytesWritten = pos;
            return true;
        }

        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        if (pos + integralDigits + (precision > 0 ? Encoding.UTF8.GetByteCount(numberDecimalSeparator) + precision : 0) > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < Math.Min(integralDigits, totalLength); i++)
        {
            destination[pos++] = GetDigitAtPosition(integral, fractional, i);
        }

        for (int i = 0; i < trailingZeros; i++)
        {
            destination[pos++] = (byte)'0';
        }

        if (precision > 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(numberDecimalSeparator, destination.Slice(pos), out _))
            {
                bytesWritten = 0;
                return false;
            }

            pos += numberDecimalSeparator.Length;

            int fractionalStart = decimalPosition;

            if (fractionalStart >= totalLength)
            {
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = (byte)'0';
                }
            }
            else
            {
                int roundingPosition = fractionalStart + precision;

                if (roundingPosition >= totalLength)
                {
                    for (int i = fractionalStart; i < totalLength; i++)
                    {
                        destination[pos++] = GetDigitAtPosition(integral, fractional, i);
                    }

                    for (int i = totalLength - fractionalStart; i < precision; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }
                }
                else
                {
                    byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                    int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                    for (int i = precision - 1; i >= 0; i--)
                    {
                        byte digit = GetDigitAtPosition(integral, fractional, fractionalStart + i);
                        digit += (byte)carry;
                        if (digit > (byte)'9')
                        {
                            digit = (byte)'0';
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }

                        destination[pos + i] = digit;
                    }

                    pos += precision;

                    if (carry > 0)
                    {
                        int backPos = pos - precision - numberDecimalSeparator.Length - 1;
                        while (backPos >= (isNegative ? formatInfo.NegativeSign.Length : 0) && destination[backPos] == '9')
                        {
                            destination[backPos] = (byte)'0';
                            backPos--;
                        }

                        if (backPos >= (isNegative ? formatInfo.NegativeSign.Length : 0))
                        {
                            destination[backPos]++;
                        }
                        else
                        {
                            for (int i = pos; i > (isNegative ? formatInfo.NegativeSign.Length : 0); i--)
                            {
                                destination[i] = destination[i - 1];
                            }

                            destination[isNegative ? formatInfo.NegativeSign.Length : 0] = (byte)'1';
                            pos++;
                        }
                    }
                }
            }
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatGeneral(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, Span<byte> destination, out int bytesWritten, int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        int totalLength = integral.Length + fractional.Length;

        // Default precision for General format
        // When not specified, use a reasonable default (15 for double precision)
        int effectivePrecision = precision >= 0 ? precision : 15;

        // Calculate what the exponent would be in scientific notation
        // (position of most significant digit relative to decimal point)
        int scientificExponent = totalLength - 1 + exponent;

        // Check if rounding would cause a carry that increases the exponent
        if (effectivePrecision < totalLength)
        {
            byte roundingDigit = GetDigitAtPosition(integral, fractional, effectivePrecision);
            if (roundingDigit >= (byte)'5')
            {
                // Check if rounding would propagate all the way up
                int carry = 1;
                for (int i = effectivePrecision - 1; i >= 0 && carry > 0; i--)
                {
                    byte digit = GetDigitAtPosition(integral, fractional, i);
                    if (digit != (byte)'9')
                    {
                        carry = 0;
                        break;
                    }
                }

                if (carry > 0)
                {
                    scientificExponent++;
                }
            }
        }

        // Decide between fixed-point and scientific notation
        // Use scientific if exponent < -1 or >= precision (after considering significant digits)
        bool useScientific = scientificExponent < -1 || scientificExponent >= effectivePrecision;

        if (useScientific)
        {
            // Use scientific notation - format as "d.ddd...e±nn"
            return FormatGeneralScientific(isNegative, integral, fractional, exponent, destination, out bytesWritten, effectivePrecision, exponentChar, formatInfo);
        }
        else
        {
            // Use fixed-point notation - format as "ddd.ddd"
            return FormatGeneralFixedPoint(isNegative, integral, fractional, exponent, destination, out bytesWritten, effectivePrecision, formatInfo);
        }
    }

    private static bool FormatGeneralScientific(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        char exponentChar,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
        }

        int totalLength = integral.Length + fractional.Length;
        int significandStart = pos;

        // Calculate scientific exponent
        int scientificExponent = totalLength - 1 + exponent;

        // Round to precision significant figures
        if (precision > 0 && precision < totalLength)
        {
            byte roundingDigit = GetDigitAtPosition(integral, fractional, precision);
            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            for (int i = precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                destination[pos + i] = digit;
            }

            if (carry > 0)
            {
                // Shift digits right
                for (int i = precision; i > 0; i--)
                {
                    destination[pos + i] = destination[pos + i - 1];
                }

                destination[pos] = (byte)'1';
                pos += precision + 1;
                scientificExponent++;
            }
            else
            {
                pos += precision;
            }
        }
        else
        {
            // Output all digits
            for (int i = 0; i < totalLength; i++)
            {
                destination[pos++] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Remove trailing zeros
        for (int trailingZeros = 0; pos > significandStart + 1 && destination[pos - 1] == '0'; trailingZeros++)
        {
            pos--;
        }

        // Insert decimal point after first digit (if there are more digits)
        if (pos > significandStart + 1)
        {
            // Get decimal separator as UTF-8
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, stackalloc byte[10], out int decimalSepLength))
            {
                bytesWritten = 0;
                return false;
            }

            byte decimalSep = formatInfo.NumberDecimalSeparator.Length > 0 ? (byte)formatInfo.NumberDecimalSeparator[0] : (byte)'.';

            // Shift digits right to make room for decimal point
            for (int i = pos; i > significandStart + 1; i--)
            {
                destination[i] = destination[i - 1];
            }

            destination[significandStart + 1] = decimalSep;
            pos++;

            // Remove trailing zeros after decimal point
            while (pos > significandStart + 2 && destination[pos - 1] == '0')
            {
                pos--;
            }

            // Remove decimal point if no fractional digits remain
            if (destination[pos - 1] == decimalSep)
            {
                pos--;
            }
        }
        else if (pos == significandStart)
        {
            // If we removed all digits, write "0"
            destination[pos++] = (byte)'0';
            bytesWritten = pos;
            return true;
        }

        // Write exponent
        destination[pos++] = (byte)exponentChar;

        if (scientificExponent >= 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.PositiveSign, destination.Slice(pos), out int positiveSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += positiveSignLength;
        }
        else
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
            scientificExponent = -scientificExponent;
        }

        if (!Utf8Formatter.TryFormat(scientificExponent, destination.Slice(pos), out int expChars))
        {
            bytesWritten = 0;
            return false;
        }

        pos += expChars;
        bytesWritten = pos;
        return true;
    }

    private static bool FormatGeneralFixedPoint(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        // Apply rounding to precision significant figures
        Span<byte> roundedDigits = stackalloc byte[Math.Max(totalLength, precision + 1)];
        int roundedLength = totalLength;

        if (precision < totalLength)
        {
            // Need to round
            byte roundingDigit = GetDigitAtPosition(integral, fractional, precision);
            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            for (int i = precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            if (carry > 0)
            {
                // Shift digits right and add 1 at front
                for (int i = precision; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength = precision + 1;
                decimalPosition++;
            }
            else
            {
                roundedLength = precision;
            }
        }
        else
        {
            // Copy all digits
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Now format the rounded digits
        if (decimalPosition <= 0)
        {
            // Number less than 1: 0.xxx
            int leadingZeros = -decimalPosition;
            if (pos + 1 > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            destination[pos++] = (byte)'0';

            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int decimalSeparatorLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += decimalSeparatorLength;

            // Leading zeros after decimal
            for (int i = 0; i < leadingZeros; i++)
            {
                destination[pos++] = (byte)'0';
            }

            // Significant digits
            for (int i = 0; i < roundedLength; i++)
            {
                destination[pos++] = roundedDigits[i];
            }
        }
        else if (decimalPosition >= roundedLength)
        {
            // All digits before decimal, possibly with trailing zeros
            int trailingZeros = decimalPosition - roundedLength;
            int totalCharsNeeded = roundedLength + trailingZeros;
            if (pos + totalCharsNeeded > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            for (int i = 0; i < roundedLength; i++)
            {
                destination[pos++] = roundedDigits[i];
            }

            // Trailing zeros to reach decimal position
            for (int i = 0; i < trailingZeros; i++)
            {
                destination[pos++] = (byte)'0';
            }
        }
        else
        {
            // Some digits before and some after decimal
            int fractionalDigits = roundedLength - decimalPosition;
            int totalCharsNeeded = decimalPosition + Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + fractionalDigits; // integral + "." + fractional
            if (pos + totalCharsNeeded > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            for (int i = 0; i < decimalPosition; i++)
            {
                destination[pos++] = roundedDigits[i];
            }

            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int decimalSeparatorLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += decimalSeparatorLength;

            for (int i = decimalPosition; i < roundedLength; i++)
            {
                destination[pos++] = roundedDigits[i];
            }
        }

        // Remove trailing zeros after decimal point
        int decimalPointPos = -1;
        for (int i = 0; i < pos; i++)
        {
            if (destination[i] == formatInfo.NumberDecimalSeparator[0])
            {
                decimalPointPos = i;
                break;
            }
        }

        if (decimalPointPos >= 0)
        {
            while (pos > decimalPointPos + 1 && destination[pos - 1] == '0')
            {
                pos--;
            }

            // Remove decimal point if no fractional digits remain
            if (pos == decimalPointPos + 1)
            {
                pos--;
            }
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatNumber(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
        }

        int totalLength = integral.Length + fractional.Length;
        int decimalPosition = totalLength + exponent;

        if (decimalPosition <= 0)
        {
            if (pos + 1 + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + precision : 0) > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            int integralPos = pos;
            destination[pos++] = (byte)'0';

            if (precision > 0)
            {
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += numberDecimalSeparatorLength;

                int leadingZeros = -decimalPosition;

                if (leadingZeros >= precision)
                {
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }
                }
                else
                {
                    for (int i = 0; i < leadingZeros; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }

                    int remainingPrecision = precision - leadingZeros;
                    int roundingPosition = remainingPrecision;

                    if (roundingPosition < totalLength)
                    {
                        byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                        int carry = roundingDigit >= (byte)'5' ? 1 : 0;

                        for (int i = remainingPrecision - 1; i >= 0; i--)
                        {
                            byte digit = GetDigitAtPosition(integral, fractional, i);
                            digit += (byte)carry;
                            if (digit > (byte)'9')
                            {
                                digit = (byte)'0';
                                carry = 1;
                            }
                            else
                            {
                                carry = 0;
                            }

                            destination[pos + i] = digit;
                        }

                        pos += remainingPrecision;

                        if (carry > 0)
                        {
                            destination[integralPos] = (byte)'1';
                        }
                    }
                    else
                    {
                        for (int i = 0; i < remainingPrecision && i < totalLength; i++)
                        {
                            destination[pos++] = GetDigitAtPosition(integral, fractional, i);
                        }

                        for (int i = Math.Min(remainingPrecision, totalLength); i < remainingPrecision; i++)
                        {
                            destination[pos++] = (byte)'0';
                        }
                    }
                }
            }

            bytesWritten = pos;
            return true;
        }

        int integralDigits = decimalPosition;
        int trailingZeros = integralDigits > totalLength ? integralDigits - totalLength : 0;

        // Check if rounding the fractional part will carry into the integral part
        bool needsRounding = false;
        int fractionalStart = decimalPosition;
        if (fractionalStart < totalLength)
        {
            int roundingPosition = fractionalStart + precision;
            if (roundingPosition < totalLength)
            {
                byte roundingDigit = GetDigitAtPosition(integral, fractional, roundingPosition);
                if (roundingDigit >= (byte)'5')
                {
                    needsRounding = true;
                }
            }
        }

        Span<byte> roundedDigits = stackalloc byte[totalLength + 1];
        int roundedLength = totalLength;
        bool hasCarry = false;

        if (needsRounding)
        {
            int carry = 1;
            for (int i = fractionalStart + precision - 1; i >= 0; i--)
            {
                byte digit = GetDigitAtPosition(integral, fractional, i);
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            hasCarry = carry > 0;

            if (hasCarry)
            {
                for (int i = totalLength; i > 0; i--)
                {
                    roundedDigits[i] = roundedDigits[i - 1];
                }

                roundedDigits[0] = (byte)'1';
                roundedLength++;
                integralDigits++;
                if (integralDigits > totalLength + 1)
                {
                    trailingZeros++;
                }
            }
            else
            {
                for (int i = fractionalStart + precision; i < totalLength; i++)
                {
                    roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
                }
            }
        }
        else
        {
            for (int i = 0; i < totalLength; i++)
            {
                roundedDigits[i] = GetDigitAtPosition(integral, fractional, i);
            }
        }

        // Calculate space needed including group separators
        int[] groupSizes = formatInfo.NumberGroupSizes;
        int groupSeparatorCount = 0;
        if (groupSizes.Length > 0 && formatInfo.NumberGroupSeparator.Length > 0)
        {
            int digitsRemaining = integralDigits;
            int groupIndex = 0;
            while (digitsRemaining > 0 && groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)] > 0)
            {
                int groupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];
                if (digitsRemaining > groupSize)
                {
                    groupSeparatorCount++;
                    digitsRemaining -= groupSize;
                    groupIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        int totalGroupSeparatorLength = groupSeparatorCount * Encoding.UTF8.GetByteCount(formatInfo.NumberGroupSeparator);

        if (pos + integralDigits + totalGroupSeparatorLength + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + precision : 0) > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        // Write integral part with group separators (using the rounded digits)
        int newDecimalPosition = hasCarry ? decimalPosition + 1 : decimalPosition;
        int digitCount = 0;
        WriteIntegralWithGroupSeparatorsUnsafeFromBytes(roundedDigits.Slice(0, roundedLength), integralDigits, trailingZeros,
            destination.Slice(pos), ref digitCount, formatInfo.NumberGroupSeparator, groupSizes);
        pos += digitCount;

        if (precision > 0)
        {
            WriteFractionalPartUnsafeFromBytes(roundedDigits.Slice(0, roundedLength), newDecimalPosition, precision,
                destination, ref pos, formatInfo.NumberDecimalSeparator);
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatExponential(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        Span<byte> destination,
        out int bytesWritten,
        int precision,
        char exponentChar,
        NumberFormatInfo formatInfo)
    {
        int pos = 0;

        if (isNegative)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
        }

        int totalLength = integral.Length + fractional.Length;

        if (totalLength == 0)
        {
            // Special case for zero
            if (pos + Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + (precision > 0 ? 1 + precision : 0) + Encoding.UTF8.GetByteCount(formatInfo.PositiveSign) + 4 > destination.Length) // "0.000e+000"
            {
                bytesWritten = 0;
                return false;
            }

            destination[pos++] = (byte)'0';

            if (precision > 0)
            {
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += numberDecimalSeparatorLength;
                for (int i = 0; i < precision; i++)
                {
                    destination[pos++] = (byte)'0';
                }
            }

            destination[pos++] = (byte)exponentChar;
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.PositiveSign, destination.Slice(pos), out int positiveSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += positiveSignLength;
            destination[pos++] = (byte)'0';
            destination[pos++] = (byte)'0';
            destination[pos++] = (byte)'0';

            bytesWritten = pos;
            return true;
        }

        // Calculate the actual exponent for scientific notation
        // We want one digit before the decimal point
        int scientificExponent = totalLength - 1 + exponent;

        // Check buffer size
        if (pos + 1 + (precision > 0 ? 1 + precision : 0) + 1 + 1 + 3 > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        // Write first digit
        byte firstDigit = GetDigitAtPosition(integral, fractional, 0);

        // Handle rounding
        if (precision < totalLength - 1)
        {
            // Need to round
            int roundPos = precision + 1;
            byte roundingDigit = roundPos < totalLength ? GetDigitAtPosition(integral, fractional, roundPos) : (byte)'0';

            int carry = roundingDigit >= (byte)'5' ? 1 : 0;

            // Round the fractional digits
            Span<byte> roundedDigits = stackalloc byte[precision + 1];
            roundedDigits[0] = firstDigit;

            for (int i = precision; i >= 1; i--)
            {
                byte digit = i < totalLength ? GetDigitAtPosition(integral, fractional, i) : (byte)'0';
                digit += (byte)carry;
                if (digit > (byte)'9')
                {
                    digit = (byte)'0';
                    carry = 1;
                }
                else
                {
                    carry = 0;
                }

                roundedDigits[i] = digit;
            }

            // Handle carry into first digit
            firstDigit += (byte)carry;
            if (firstDigit > (byte)'9')
            {
                firstDigit = (byte)'0';
                carry = 1;
            }
            else
            {
                carry = 0;
            }

            roundedDigits[0] = firstDigit;

            // If carry overflowed, adjust
            if (carry > 0)
            {
                scientificExponent++;
                destination[pos++] = (byte)'1';

                if (precision > 0)
                {
                    if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberDecimalSeparatorLength;
                    for (int i = 0; i < precision; i++)
                    {
                        destination[pos++] = (byte)'0';
                    }
                }
            }
            else
            {
                destination[pos++] = roundedDigits[0];

                if (precision > 0)
                {
                    if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    pos += numberDecimalSeparatorLength;
                    for (int i = 1; i <= precision; i++)
                    {
                        destination[pos++] = roundedDigits[i];
                    }
                }
            }
        }
        else
        {
            // No rounding needed
            destination[pos++] = firstDigit;

            if (precision > 0)
            {
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += numberDecimalSeparatorLength;

                for (int i = 1; i <= precision && i < totalLength; i++)
                {
                    destination[pos++] = GetDigitAtPosition(integral, fractional, i);
                }

                // Pad with zeros if needed
                for (int i = totalLength; i <= precision; i++)
                {
                    destination[pos++] = (byte)'0';
                }
            }
        }

        // Write exponent
        destination[pos++] = (byte)exponentChar;

        if (scientificExponent >= 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.PositiveSign, destination.Slice(pos), out int positiveSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += positiveSignLength;
        }
        else
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NegativeSign, destination.Slice(pos), out int negativeSignLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += negativeSignLength;
            scientificExponent = -scientificExponent;
        }

        // Format exponent with at least 3 digits
        if (scientificExponent >= 100)
        {
            if (!Utf8Formatter.TryFormat(scientificExponent, destination.Slice(pos), out int expChars, D3Format))
            {
                bytesWritten = 0;
                return false;
            }

            pos += expChars;
        }
        else
        {
            destination[pos++] = (byte)('0' + (scientificExponent / 100));
            destination[pos++] = (byte)('0' + ((scientificExponent / 10) % 10));
            destination[pos++] = (byte)('0' + (scientificExponent % 10));
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatZero(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (format.IsEmpty)
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

        var formatInfo = NumberFormatInfo.GetInstance(provider);
        char formatType = char.ToUpperInvariant(format[0]);
        int precision = -1;

        if (format.Length > 1 && !TryParseInt32(format.Slice(1), out precision))
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

        return formatType switch
        {
            'G' => WriteByte(destination, out bytesWritten, (byte)'0'),
            'F' or 'N' => TryFormatZeroFixedPoint(destination, out bytesWritten, precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'E' => TryFormatZeroExponential(destination, out bytesWritten, precision >= 0 ? precision : 6, format[0], formatInfo),
            'C' => TryFormatZeroCurrency(destination, out bytesWritten, precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => TryFormatZeroPercent(destination, out bytesWritten, precision >= 0 ? precision : formatInfo.PercentDecimalDigits, formatInfo),
            'X' => TryFormatZeroHexOrBinary(destination, out bytesWritten, precision),
            'B' => TryFormatZeroHexOrBinary(destination, out bytesWritten, precision),
            _ => WriteByte(destination, out bytesWritten, (byte)'0'),
        };
    }

    internal static bool TryFormatZeroFixedPoint(Span<byte> destination, out int bytesWritten, int precision, NumberFormatInfo formatInfo)
    {
        int requiredLength = 1 + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + precision : 0);

        if (requiredLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        int pos = 0;
        destination[pos++] = (byte)'0';

        if (precision > 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += numberDecimalSeparatorLength;

            for (int i = 0; i < precision; i++)
            {
                destination[pos++] = (byte)'0';
            }
        }

        bytesWritten = pos;
        return true;
    }

    internal static bool TryFormatZeroExponential(Span<byte> destination, out int bytesWritten, int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        int requiredLength = 1 + (precision > 0 ? Encoding.UTF8.GetByteCount(formatInfo.NumberDecimalSeparator) + precision : 0) + Encoding.UTF8.GetByteCount(formatInfo.PositiveSign) + 4; // E+000

        if (requiredLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        int pos = 0;
        destination[pos++] = (byte)'0';

        if (precision > 0)
        {
            if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.NumberDecimalSeparator, destination.Slice(pos), out int numberDecimalSeparatorLength))
            {
                bytesWritten = 0;
                return false;
            }

            pos += numberDecimalSeparatorLength;

            for (int i = 0; i < precision; i++)
            {
                destination[pos++] = (byte)'0';
            }
        }

        destination[pos++] = (byte)exponentChar;
        if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.PositiveSign, destination.Slice(pos), out int positiveSignLength))
        {
            bytesWritten = 0;
            return false;
        }

        pos += positiveSignLength;
        destination[pos++] = (byte)'0';
        destination[pos++] = (byte)'0';
        destination[pos++] = (byte)'0';

        bytesWritten = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryFormatZeroCurrency(Span<byte> destination, out int bytesWritten, int precision, NumberFormatInfo formatInfo)
    {
        // Format the zero number part first
        Span<byte> tempDest = stackalloc byte[100];
        if (!TryFormatZeroFixedPoint(tempDest, out int numberBytes, precision, formatInfo))
        {
            bytesWritten = 0;
            return false;
        }

        // Apply positive currency pattern (zero is not negative)
        int pattern = formatInfo.CurrencyPositivePattern;
        int pos = 0;

        switch (pattern)
        {
            case 0: // $n
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.CurrencySymbol, destination.Slice(pos), out int currencyLength0))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += currencyLength0;
                if (pos + numberBytes > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                tempDest.Slice(0, numberBytes).CopyTo(destination.Slice(pos));
                pos += numberBytes;
                break;

            case 1: // n$
                if (pos + numberBytes > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                tempDest.Slice(0, numberBytes).CopyTo(destination.Slice(pos));
                pos += numberBytes;
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.CurrencySymbol, destination.Slice(pos), out int currencyLength1))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += currencyLength1;
                break;

            case 2: // $ n
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.CurrencySymbol, destination.Slice(pos), out int currencyLength2))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += currencyLength2;
                if (pos + 1 + numberBytes > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                destination[pos++] = (byte)' ';
                tempDest.Slice(0, numberBytes).CopyTo(destination.Slice(pos));
                pos += numberBytes;
                break;

            case 3: // n $
                if (pos + numberBytes + 1 > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                tempDest.Slice(0, numberBytes).CopyTo(destination.Slice(pos));
                pos += numberBytes;
                destination[pos++] = (byte)' ';
                if (!JsonReaderHelper.TryGetUtf8FromText(formatInfo.CurrencySymbol, destination.Slice(pos), out int currencyLength3))
                {
                    bytesWritten = 0;
                    return false;
                }

                pos += currencyLength3;
                break;

            default:
                bytesWritten = 0;
                return false;
        }

        bytesWritten = pos;
        return true;
    }

    private static bool TryFormatZeroPercent(Span<byte> destination, out int bytesWritten, int precision, NumberFormatInfo formatInfo)
    {
        if (!TryFormatZeroFixedPoint(destination, out int pos, precision, formatInfo))
        {
            bytesWritten = 0;
            return false;
        }

        int percentSymbolLength = Encoding.UTF8.GetByteCount(formatInfo.PercentSymbol);
        if (pos + 1 + percentSymbolLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        destination[pos++] = (byte)' ';
        JsonReaderHelper.TryGetUtf8FromText(formatInfo.PercentSymbol, destination.Slice(pos), out int written);

        bytesWritten = pos + written;
        return true;
    }

    private static bool TryFormatZeroHexOrBinary(Span<byte> destination, out int bytesWritten, int precision)
    {
        int requiredLength = precision > 0 ? precision : 1;
        if (destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < requiredLength; i++)
        {
            destination[i] = (byte)'0';
        }

        bytesWritten = requiredLength;
        return true;
    }
}