// <copyright file="BigNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Corvus.Numerics;

/// <summary>
/// Represents an arbitrary-precision decimal number using a significand and exponent.
/// </summary>
/// <remarks>
/// <para>
/// Internally represented as: value = significand × 10^exponent where significand is a <see cref="BigInteger"/>
/// and exponent is a <see cref="int"/>.
/// </para>
/// <para>
/// This type provides equivalent functionality to <see cref="decimal"/> but with arbitrary precision,
/// similar to how <see cref="BigInteger"/> extends integer arithmetic beyond fixed sizes.
/// </para>
/// </remarks>
public readonly partial struct BigNumber :
    IEquatable<BigNumber>,
    IComparable<BigNumber>,
    IComparable,
#if NET
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    INumber<BigNumber>,
    ISignedNumber<BigNumber>
#else
    IFormattable
#endif
{
#if NET
    /// <summary>
    /// The maximum format length for a UInt32
    /// </summary>
    private const int MaximumFormatUInt32Length = 10;  // i.e. 4294967295
#endif

    /// <summary>
    /// Maximum size in characters for stack allocation. Larger buffers will use ArrayPool.
    /// </summary>
    private const int StackAllocThreshold = 256;

    /// <summary>
    /// Maximum input length for parsing to prevent DoS attacks.
    /// </summary>
    private const int MaxInputLength = 10_000;

    /// <summary>
    /// Cached powers of 10 for optimization. Covers exponents 0-255.
    /// </summary>
    private static readonly BigInteger[] PowersOf10 = InitializePowersOf10();

    private readonly bool _normalized;

    /// <summary>
    /// Lazy-initialized cache for larger powers of 10 (256-1023).
    /// </summary>
    private static readonly Lazy<System.Collections.Concurrent.ConcurrentDictionary<int, BigInteger>> LargePowersOf10 =
        new(
            () => new System.Collections.Concurrent.ConcurrentDictionary<int, BigInteger>());

    private static BigInteger[] InitializePowersOf10()
    {
        var powers = new BigInteger[256];
        powers[0] = BigInteger.One;
        for (int i = 1; i < powers.Length; i++)
        {
            powers[i] = powers[i - 1] * 10;
        }

        return powers;
    }

    /// <summary>
    /// Gets a cached power of 10, or computes it if outside the cache range.
    /// </summary>
    /// <param name="exponent">The exponent (must be non-negative).</param>
    /// <returns>10 raised to the specified power.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when exponent is negative or too large.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BigInteger GetPowerOf10(int exponent)
    {
        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative.");
        }

        // Primary cache: 10^0 to 10^255
        if ((uint)exponent < (uint)PowersOf10.Length)
        {
            return PowersOf10[exponent];
        }

        // Secondary cache: 10^256 to 10^1023
        if (exponent < 1024)
        {
            return LargePowersOf10.Value.GetOrAdd(exponent, exp => BigInteger.Pow(10, exp));
        }

        // Very large: compute on demand
        return BigInteger.Pow(10, exponent);
    }

    /// <summary>
    /// Safely scales a BigInteger by a power of 10, validating that the exponent difference is within safe range.
    /// </summary>
    /// <param name="value">The value to scale.</param>
    /// <param name="exponentDiff">The exponent difference (must be non-negative and within int.MaxValue).</param>
    /// <returns>The scaled value.</returns>
    /// <exception cref="OverflowException">Thrown when exponent difference exceeds safe range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BigInteger SafeScaleByPowerOf10(BigInteger value, int exponentDiff)
    {
        if (exponentDiff < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponentDiff), "Exponent difference must be non-negative.");
        }

        if (exponentDiff > int.MaxValue)
        {
            throw new OverflowException($"Exponent difference {exponentDiff} exceeds maximum supported range ({int.MaxValue}).");
        }

        return value * GetPowerOf10((int)exponentDiff);
    }

    private BigNumber(BigInteger significand, int exponent, bool normalized)
    {
        Significand = significand;
        Exponent = exponent;
        _normalized = normalized;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BigNumber"/> struct.
    /// </summary>
    /// <param name="significand">The significand.</param>
    /// <param name="exponent">The exponent (power of 10).</param>
    public BigNumber(BigInteger significand, int exponent)
    {
        Significand = significand;
        Exponent = exponent;
    }

    /// <summary>
    /// Gets the significand of the number.
    /// </summary>
    public BigInteger Significand { get; }

    /// <summary>
    /// Gets the exponent (power of 10) of the number.
    /// </summary>
    public int Exponent { get; }

    /// <summary>
    /// Gets a value representing zero.
    /// </summary>
    public static BigNumber Zero { get; } = new(BigInteger.Zero, 0);

    /// <summary>
    /// Gets a value representing one.
    /// </summary>
    public static BigNumber One { get; } = new(BigInteger.One, 0);

    /// <summary>
    /// Gets a value representing minus one.
    /// </summary>
    public static BigNumber MinusOne { get; } = new(BigInteger.MinusOne, 0);

    /// <summary>
    /// Gets the radix (base) of the number system.
    /// </summary>
    public static int Radix => 10;

#if NET
    /// <summary>
    /// Gets the additive identity (zero).
    /// </summary>
    static BigNumber IAdditiveIdentity<BigNumber, BigNumber>.AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the multiplicative identity (one).
    /// </summary>
    static BigNumber IMultiplicativeIdentity<BigNumber, BigNumber>.MultiplicativeIdentity => One;

    static BigNumber INumberBase<BigNumber>.One => One;

    static int INumberBase<BigNumber>.Radix => Radix;

    static BigNumber INumberBase<BigNumber>.Zero => Zero;

    static BigNumber ISignedNumber<BigNumber>.NegativeOne => MinusOne;
#endif

    /// <summary>
    /// Returns a normalized copy of this number with trailing zeros removed from the significand.
    /// </summary>
    /// <returns>A normalized <see cref="BigNumber"/>.</returns>
    public BigNumber Normalize()
    {
        if (_normalized)
        {
            return this;
        }

        if (Significand.IsZero)
        {
            return Zero;
        }

        BigInteger significand = Significand;
        int exponent = Exponent;

        while (!significand.IsZero && significand % 10 == 0)
        {
            significand /= 10;
            exponent++;
        }

        return new BigNumber(significand, exponent, true);
    }

    /// <summary>
    /// Determines whether this instance represents an integer value.
    /// </summary>
    /// <returns><c>true</c> if the value is an integer; otherwise, <c>false</c>.</returns>
    public bool IsInteger()
    {
        if (Exponent >= 0)
        {
            return true;
        }

        BigNumber normalized = Normalize();
        return normalized.Exponent >= 0;
    }

    /// <summary>
    /// Determines whether the specified <see cref="BigNumber"/> is equal to this instance.
    /// </summary>
    /// <param name="other">The <see cref="BigNumber"/> to compare with this instance.</param>
    /// <returns><c>true</c> if the specified value is equal to this instance; otherwise, <c>false</c>.</returns>
    public bool Equals(BigNumber other)
    {
        // Fast path: Same exponent - compare significands directly
        if (Exponent == other.Exponent)
        {
            return Significand == other.Significand;
        }

        // Standard path: Normalize and compare
        BigNumber left = Normalize();
        BigNumber right = other.Normalize();
        return left.Significand == right.Significand && left.Exponent == right.Exponent;
    }

    /// <summary>
    /// Determines whether the specified object is equal to this instance.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns><c>true</c> if the specified object is a <see cref="BigNumber"/> equal to this instance; otherwise, <c>false</c>.</returns>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is BigNumber other && Equals(other);

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this <see cref="BigNumber"/> value.</returns>
    public override int GetHashCode()
    {
        BigNumber normalized = Normalize();
#if NET
        return HashCode.Combine(normalized.Significand, normalized.Exponent);
#else
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + normalized.Significand.GetHashCode();
            hash = (hash * 31) + normalized.Exponent.GetHashCode();
            return hash;
        }
#endif
    }

    /// <summary>
    /// Compares this instance with another <see cref="BigNumber"/> value.
    /// </summary>
    /// <param name="other">The <see cref="BigNumber"/> to compare with this instance.</param>
    /// <returns>A value that indicates the relative order of the values being compared.</returns>
    public int CompareTo(BigNumber other)
    {
        // Fast path: Exact same values (including denormalized)
        if (Significand == other.Significand && Exponent == other.Exponent)
            return 0;

        // Fast path: Zero checks (don't need normalization)
        if (Significand.IsZero)
            return other.Significand.IsZero ? 0 : -other.Significand.Sign;

        if (other.Significand.IsZero)
            return Significand.Sign;

        // Fast path: Different signs
        if (Significand.Sign != other.Significand.Sign)
            return Significand.Sign.CompareTo(other.Significand.Sign);

        // Normalize for accurate comparison
        BigNumber left = Normalize();
        BigNumber right = other.Normalize();

        int exponentDiff = left.Exponent - right.Exponent;

        if (exponentDiff == 0)
        {
            return left.Significand.CompareTo(right.Significand);
        }

        long leftDigits = (long)BigInteger.Log10(BigInteger.Abs(left.Significand)) + 1;
        long rightDigits = (long)BigInteger.Log10(BigInteger.Abs(right.Significand)) + 1;

        long leftEffectiveDigits = leftDigits + left.Exponent;
        long rightEffectiveDigits = rightDigits + right.Exponent;

        if (leftEffectiveDigits != rightEffectiveDigits)
        {
            int comparison = leftEffectiveDigits.CompareTo(rightEffectiveDigits);
            return left.Significand.Sign > 0 ? comparison : -comparison;
        }

        BigInteger leftAdjusted = left.Significand;
        BigInteger rightAdjusted = right.Significand;

        if (exponentDiff > 0)
        {
            leftAdjusted = SafeScaleByPowerOf10(left.Significand, exponentDiff);
        }
        else
        {
            rightAdjusted = SafeScaleByPowerOf10(right.Significand, -exponentDiff);
        }

        return leftAdjusted.CompareTo(rightAdjusted);
    }

    /// <summary>
    /// Compares this instance with a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>A value that indicates the relative order of the values being compared.</returns>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is BigNumber other)
        {
            return CompareTo(other);
        }

        throw new ArgumentException($"Object must be of type {nameof(BigNumber)}.", nameof(obj));
    }

    /// <summary>
    /// Returns the string representation of this <see cref="BigNumber"/> value.
    /// </summary>
    /// <returns>The string representation of this instance.</returns>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats this <see cref="BigNumber"/> value using the specified format string and format provider.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <returns>The formatted string representation of this instance.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (Significand.IsZero)
        {
            return string.IsNullOrEmpty(format) ? "0" : FormatZero(format, formatProvider);
        }

        // If no format specified, use raw format (SignificandEExponent)
        if (string.IsNullOrEmpty(format))
        {
            BigNumber normalized = Normalize();
            if (normalized.Exponent == 0)
            {
                return normalized.Significand.ToString(formatProvider);
            }

            return $"{normalized.Significand.ToString(formatProvider)}E{normalized.Exponent}";
        }

        var formatInfo = NumberFormatInfo.GetInstance(formatProvider);

        // Parse format string
        char formatType = char.ToUpperInvariant(format![0]);
        char originalFormatChar = format[0];
        int precision = -1;

        if (format.Length > 1)
        {
#if NET
            if (!int.TryParse(format.AsSpan(1), out precision))
#else
            if (!ParsingPolyfills.TryParseInt32Invariant(format.AsSpan(1), out precision))
#endif
            {
                throw new FormatException($"Invalid format string: {format}");
            }
        }

        return formatType switch
        {
            'G' => FormatGeneral(precision >= 0 ? precision : -1, char.IsLower(originalFormatChar) ? 'e' : 'E', formatInfo),
            'F' => FormatFixedPoint(precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'N' => FormatNumber(precision >= 0 ? precision : formatInfo.NumberDecimalDigits, formatInfo),
            'E' => FormatExponential(precision >= 0 ? precision : 6, char.IsLower(format![0]) ? 'e' : 'E', formatInfo),
            'C' => FormatCurrency(precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => FormatPercent(precision >= 0 ? precision : 2, formatInfo),
            _ => throw new FormatException($"Format specifier '{formatType}' is not supported.")
        };
    }

    private static string FormatZero(string? format, IFormatProvider? formatProvider)
    {
        var formatInfo = NumberFormatInfo.GetInstance(formatProvider);

        if (string.IsNullOrEmpty(format))
        {
            return "0";
        }

        char formatType = char.ToUpperInvariant(format![0]);
        char originalFormatChar = format[0];
        int precision = -1;

#if NET
        if (format.Length > 1 && !int.TryParse(format.AsSpan(1), out precision))
#else
        if (format.Length > 1 && !ParsingPolyfills.TryParseInt32Invariant(format.AsSpan(1), out precision))
#endif
        {
            return "0";
        }

        return formatType switch
        {
            'G' => "0",
            'F' => "0" + (precision > 0 ? formatInfo.NumberDecimalSeparator + new string('0', precision) : ""),
            'N' => "0" + (precision > 0 ? formatInfo.NumberDecimalSeparator + new string('0', precision) : ""),
            'E' => "0" + (precision > 0 ? formatInfo.NumberDecimalSeparator + new string('0', precision) : "") + originalFormatChar + "+000",
            'C' => FormatCurrencyZero(precision >= 0 ? precision : formatInfo.CurrencyDecimalDigits, formatInfo),
            'P' => FormatPercentZero(precision >= 0 ? precision : formatInfo.PercentDecimalDigits, formatInfo),
            _ => "0"
        };
    }

    private static string FormatPercentZero(int precision, NumberFormatInfo formatInfo)
    {
        string value = "0" + (precision > 0 ? formatInfo.PercentDecimalSeparator + new string('0', precision) : "");

        return formatInfo.PercentPositivePattern switch
        {
            0 => value + " " + formatInfo.PercentSymbol,
            1 => value + formatInfo.PercentSymbol,
            2 => formatInfo.PercentSymbol + value,
            3 => formatInfo.PercentSymbol + " " + value,
            _ => value + " " + formatInfo.PercentSymbol
        };
    }

    private static string FormatCurrencyZero(int precision, NumberFormatInfo formatInfo)
    {
        string value = "0" + (precision > 0 ? formatInfo.CurrencyDecimalSeparator + new string('0', precision) : "");

        // Apply currency pattern (for positive zero)
        return formatInfo.CurrencyPositivePattern switch
        {
            0 => formatInfo.CurrencySymbol + value,
            1 => value + formatInfo.CurrencySymbol,
            2 => formatInfo.CurrencySymbol + " " + value,
            3 => value + " " + formatInfo.CurrencySymbol,
            _ => formatInfo.CurrencySymbol + value
        };
    }

    private string FormatGeneral(int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        BigNumber normalized = Normalize();

        // If precision is specified, we need to round and potentially use exponential
        if (precision > 0)
        {
            string sigStr = BigInteger.Abs(normalized.Significand).ToString(formatInfo);

            // If significand has more digits than precision, round
            if (sigStr.Length > precision)
            {
                // Round to precision significant digits
                BigInteger divisor = GetPowerOf10(sigStr.Length - precision);
                var quotient = BigInteger.DivRem(normalized.Significand, divisor, out BigInteger remainder);

                // Round half away from zero
                BigInteger halfDivisor = divisor / 2;
                if (BigInteger.Abs(remainder) > halfDivisor ||
                    (BigInteger.Abs(remainder) == halfDivisor && !quotient.IsEven))
                {
                    quotient += normalized.Significand >= 0 ? 1 : -1;
                }

                int newExponent = normalized.Exponent + (sigStr.Length - precision);
                normalized = new BigNumber(quotient, newExponent);
            }
        }

        // General format uses the compact representation
        // For values in normalized form, just return significand E exponent
        if (normalized.Exponent == 0)
        {
            return normalized.Significand.ToString(formatInfo);
        }

        // Use exponential notation - no sign for positive exponents in G format
        return $"{normalized.Significand.ToString(formatInfo)}{exponentChar}{normalized.Exponent}";
    }

    private string FormatFixedPoint(int precision, NumberFormatInfo formatInfo)
    {
        BigNumber scaled = RoundToPrecision(this, precision);

        string sigStr = BigInteger.Abs(scaled.Significand).ToString(formatInfo);

        // Split into integer and fractional parts
        string integerPart;
        string fractionalPart;

        if (sigStr.Length <= precision)
        {
            integerPart = "0";
            fractionalPart = new string('0', precision - sigStr.Length) + sigStr;
        }
        else
        {
            integerPart = sigStr.Substring(0, sigStr.Length - precision);
            fractionalPart = sigStr.Substring(sigStr.Length - precision);
        }

        string result = integerPart;
        if (precision > 0)
        {
            result += formatInfo.NumberDecimalSeparator + fractionalPart;
        }

        if (scaled.Significand < 0)
        {
            result = formatInfo.NegativeSign + result;
        }

        return result;
    }

    private static BigNumber RoundToPrecision(BigNumber value, int precision)
    {
        return RoundToPrecision(value, precision, MidpointRounding.AwayFromZero);
    }

    private static BigNumber RoundToPrecision(BigNumber value, int precision, MidpointRounding mode)
    {
        // Fast path: Already at desired precision or better
        if (value.Exponent >= -precision)
        {
            if (value.Exponent == -precision)
                return value;

            // Scale up to exact precision
            int diff = value.Exponent + precision;
            BigInteger scaledSig = SafeScaleByPowerOf10(value.Significand, diff);
            return new BigNumber(scaledSig, -precision);
        }

        // Need to round
        int roundDiff = -precision - value.Exponent;

        BigInteger divisor = GetPowerOf10((int)roundDiff);
        var quotient = BigInteger.DivRem(value.Significand, divisor, out BigInteger remainder);

        if (remainder.IsZero)
        {
            return new BigNumber(quotient, -precision);
        }

        // Apply rounding mode based on the remainder
        BigInteger halfDivisor = divisor / 2;
        var absRemainder = BigInteger.Abs(remainder);
        bool isPositive = value.Significand >= 0;

        bool shouldIncrementQuotient = mode switch
        {
            // Round to even (banker's rounding)
            MidpointRounding.ToEven => absRemainder > halfDivisor ||
                                       (absRemainder == halfDivisor && !quotient.IsEven),

            // Round away from zero (traditional rounding)
            MidpointRounding.AwayFromZero => absRemainder >= halfDivisor,

#if NET
            // Round toward zero (truncate)
            MidpointRounding.ToZero => false,

            // Round toward negative infinity (floor)
            MidpointRounding.ToNegativeInfinity => !isPositive,

            // Round toward positive infinity (ceiling)
            MidpointRounding.ToPositiveInfinity => isPositive,
#else
            // netstandard2.0 fallback: Treat special values as AwayFromZero
            (MidpointRounding)4 => false,  // ToZero
            (MidpointRounding)5 => !isPositive,  // ToNegativeInfinity
            (MidpointRounding)6 => isPositive,  // ToPositiveInfinity
#endif

            _ => absRemainder >= halfDivisor
        };

        if (shouldIncrementQuotient)
        {
            quotient += isPositive ? 1 : -1;
        }

        return new BigNumber(quotient, -precision);
    }

    private string FormatNumber(int precision, NumberFormatInfo formatInfo)
    {
        BigNumber scaled = RoundToPrecision(this, precision);

        string sigStr = BigInteger.Abs(scaled.Significand).ToString(formatInfo);

        // Split into integer and fractional parts
        string integerPart;
        string fractionalPart;

        if (sigStr.Length <= precision)
        {
            integerPart = "0";
            fractionalPart = new string('0', precision - sigStr.Length) + sigStr;
        }
        else
        {
            integerPart = sigStr.Substring(0, sigStr.Length - precision);
            fractionalPart = sigStr.Substring(sigStr.Length - precision);
        }

        // Add thousands separators to integer part
        string formattedInteger = AddThousandsSeparators(integerPart, formatInfo.NumberGroupSeparator, formatInfo.NumberGroupSizes);

        string result = formattedInteger;
        if (precision > 0)
        {
            result += formatInfo.NumberDecimalSeparator + fractionalPart;
        }

        if (scaled.Significand < 0)
        {
            result = formatInfo.NegativeSign + result;
        }

        return result;
    }

    private string FormatExponential(int precision, char exponentChar, NumberFormatInfo formatInfo)
    {
        if (Significand.IsZero)
        {
            string zeros = precision > 0 ? formatInfo.NumberDecimalSeparator + new string('0', precision) : "";
            return "0" + zeros + exponentChar + "+000";
        }

        BigNumber normalized = Normalize();
        string sigStr = BigInteger.Abs(normalized.Significand).ToString(formatInfo);

        // Calculate the actual exponent (position of first significant digit)
        int actualExponent = normalized.Exponent + sigStr.Length - 1;

        // Round to precision + 1 significant digits if needed
        var mantissaValue = BigInteger.Abs(normalized.Significand);
        if (sigStr.Length > precision + 1)
        {
            // Need to round
            int digitsToRemove = sigStr.Length - precision - 1;
            var divisor = BigInteger.Pow(10, digitsToRemove);
            var quotient = BigInteger.DivRem(mantissaValue, divisor, out BigInteger remainder);

            // Round half away from zero
            BigInteger halfDivisor = divisor / 2;
            if (BigInteger.Abs(remainder) > halfDivisor ||
                (BigInteger.Abs(remainder) == halfDivisor && !quotient.IsEven))
            {
                quotient++;

                // Check if rounding caused overflow (e.g., 9.999 -> 10.00)
                string quotientStr = quotient.ToString(formatInfo);
                if (quotientStr.Length > precision + 1)
                {
                    quotient /= 10;
                    actualExponent++;
                }
            }

            mantissaValue = quotient;
            sigStr = mantissaValue.ToString(formatInfo);
        }

        // Format: d.ddd...E±xxx (exponent uses at least 3 digits)
        string mantissa;
        if (precision == 0)
        {
            mantissa = sigStr[0].ToString();
        }
        else if (sigStr.Length > 1)
        {
            // We have enough digits
            int availableDecimals = sigStr.Length - 1;
            string decimals = sigStr.Substring(1, Math.Min(availableDecimals, precision));
            if (decimals.Length < precision)
            {
                decimals += new string('0', precision - decimals.Length);
            }

            mantissa = sigStr[0] + formatInfo.NumberDecimalSeparator + decimals;
        }
        else
        {
            // Pad with zeros
            mantissa = sigStr[0] + formatInfo.NumberDecimalSeparator + new string('0', precision);
        }

        string sign = normalized.Significand < 0 ? formatInfo.NegativeSign : "";
        string expSign = actualExponent >= 0 ? "+" : formatInfo.NegativeSign;
        string exp = Math.Abs(actualExponent).ToString("D3", formatInfo);

        return sign + mantissa + exponentChar + expSign + exp;
    }

    private string FormatCurrency(int precision, NumberFormatInfo formatInfo)
    {
        BigNumber scaled = RoundToPrecision(this, precision);

        string sigStr = BigInteger.Abs(scaled.Significand).ToString(formatInfo);

        // Split into integer and fractional parts
        string integerPart;
        string fractionalPart;

        if (sigStr.Length <= precision)
        {
            integerPart = "0";
            fractionalPart = new string('0', precision - sigStr.Length) + sigStr;
        }
        else
        {
            integerPart = sigStr.Substring(0, sigStr.Length - precision);
            fractionalPart = sigStr.Substring(sigStr.Length - precision);
        }

        // Add thousands separators
        string formattedInteger = AddThousandsSeparators(integerPart, formatInfo.CurrencyGroupSeparator, formatInfo.CurrencyGroupSizes);

        string value = formattedInteger;
        if (precision > 0)
        {
            value += formatInfo.CurrencyDecimalSeparator + fractionalPart;
        }

        // Apply currency pattern
        bool isNegative = scaled.Significand < 0;

        if (isNegative)
        {
            return formatInfo.CurrencyNegativePattern switch
            {
                0 => "(" + formatInfo.CurrencySymbol + value + ")",
                1 => formatInfo.NegativeSign + formatInfo.CurrencySymbol + value,
                2 => formatInfo.CurrencySymbol + formatInfo.NegativeSign + value,
                3 => formatInfo.CurrencySymbol + value + formatInfo.NegativeSign,
                4 => "(" + value + formatInfo.CurrencySymbol + ")",
                5 => formatInfo.NegativeSign + value + formatInfo.CurrencySymbol,
                6 => value + formatInfo.NegativeSign + formatInfo.CurrencySymbol,
                7 => value + formatInfo.CurrencySymbol + formatInfo.NegativeSign,
                8 => formatInfo.NegativeSign + value + " " + formatInfo.CurrencySymbol,
                9 => formatInfo.NegativeSign + formatInfo.CurrencySymbol + " " + value,
                10 => value + " " + formatInfo.CurrencySymbol + formatInfo.NegativeSign,
                11 => formatInfo.CurrencySymbol + " " + value + formatInfo.NegativeSign,
                12 => formatInfo.CurrencySymbol + " " + formatInfo.NegativeSign + value,
                13 => value + formatInfo.NegativeSign + " " + formatInfo.CurrencySymbol,
                14 => "(" + formatInfo.CurrencySymbol + " " + value + ")",
                15 => "(" + value + " " + formatInfo.CurrencySymbol + ")",
                _ => formatInfo.NegativeSign + formatInfo.CurrencySymbol + value
            };
        }
        else
        {
            return formatInfo.CurrencyPositivePattern switch
            {
                0 => formatInfo.CurrencySymbol + value,
                1 => value + formatInfo.CurrencySymbol,
                2 => formatInfo.CurrencySymbol + " " + value,
                3 => value + " " + formatInfo.CurrencySymbol,
                _ => formatInfo.CurrencySymbol + value
            };
        }
    }

    private string FormatPercent(int precision, NumberFormatInfo formatInfo)
    {
        // Multiply by 100 for percent
        BigNumber percentValue = this * new BigNumber(100, 0);

        BigNumber scaled = RoundToPrecision(percentValue, precision);

        string sigStr = BigInteger.Abs(scaled.Significand).ToString(formatInfo);

        // Split into integer and fractional parts
        string integerPart;
        string fractionalPart;

        if (sigStr.Length <= precision)
        {
            integerPart = "0";
            fractionalPart = new string('0', precision - sigStr.Length) + sigStr;
        }
        else
        {
            integerPart = sigStr.Substring(0, sigStr.Length - precision);
            fractionalPart = sigStr.Substring(sigStr.Length - precision);
        }

        // Add thousands separators
        string formattedInteger = AddThousandsSeparators(integerPart, formatInfo.PercentGroupSeparator, formatInfo.PercentGroupSizes);

        string value = formattedInteger;
        if (precision > 0)
        {
            value += formatInfo.PercentDecimalSeparator + fractionalPart;
        }

        // Apply percent pattern
        bool isNegative = scaled.Significand < 0;

        if (isNegative)
        {
            return formatInfo.PercentNegativePattern switch
            {
                0 => formatInfo.NegativeSign + value + " " + formatInfo.PercentSymbol,
                1 => formatInfo.NegativeSign + value + formatInfo.PercentSymbol,
                2 => formatInfo.NegativeSign + formatInfo.PercentSymbol + value,
                3 => formatInfo.PercentSymbol + value + formatInfo.NegativeSign,
                4 => formatInfo.PercentSymbol + formatInfo.NegativeSign + value,
                5 => value + formatInfo.NegativeSign + formatInfo.PercentSymbol,
                6 => value + formatInfo.PercentSymbol + formatInfo.NegativeSign,
                7 => formatInfo.NegativeSign + formatInfo.PercentSymbol + " " + value,
                8 => value + " " + formatInfo.PercentSymbol + formatInfo.NegativeSign,
                9 => formatInfo.PercentSymbol + " " + value + formatInfo.NegativeSign,
                10 => formatInfo.PercentSymbol + " " + formatInfo.NegativeSign + value,
                11 => value + formatInfo.NegativeSign + " " + formatInfo.PercentSymbol,
                _ => formatInfo.NegativeSign + value + " " + formatInfo.PercentSymbol
            };
        }
        else
        {
            return formatInfo.PercentPositivePattern switch
            {
                0 => value + " " + formatInfo.PercentSymbol,
                1 => value + formatInfo.PercentSymbol,
                2 => formatInfo.PercentSymbol + value,
                3 => formatInfo.PercentSymbol + " " + value,
                _ => value + " " + formatInfo.PercentSymbol
            };
        }
    }

    private static string AddThousandsSeparators(string number, string separator, int[] groupSizes)
    {
        if (number.Length <= 3 || groupSizes.Length == 0 || groupSizes[0] == 0)
        {
            return number;
        }

        var result = new StringBuilder();
        int currentPos = number.Length;
        int groupIndex = 0;
        int currentGroupSize = groupSizes[Math.Min(groupIndex, groupSizes.Length - 1)];

        while (currentPos > 0)
        {
            int takeCount = Math.Min(currentGroupSize, currentPos);

            if (result.Length > 0)
            {
                result.Insert(0, separator);
            }

            result.Insert(0, number.Substring(currentPos - takeCount, takeCount));
            currentPos -= takeCount;

            if (groupIndex < groupSizes.Length - 1)
            {
                groupIndex++;
                currentGroupSize = groupSizes[groupIndex];
            }
        }

        return result.ToString();
    }

#if NET
    /// <summary>
    /// Gets the minimum format buffer length.
    /// </summary>
    /// <param name="minimumLength">The minimum length for a text buffer to format the number.</param>
    /// <returns><see langword="true"/> if the buffer length required for the number can be safely allocated.</returns>
    public bool TryGetMinimumFormatBufferLength(out int minimumLength)
    {
        int value = (int)BigInteger.Log10(Significand) + 1;

        if (Significand.Sign < 0)
        {
            // One for the sign
            value++;
        }

        if (Exponent != 0)
        {
            // One for E and then the exponent
            value += 1 + NumberOfDigits(Exponent);
        }

        if (value > Array.MaxLength)
        {
            minimumLength = 0;
            return false;
        }

        minimumLength = (int)value;
        return true;
    }

    /// <summary>
    /// Tries to format this <see cref="BigNumber"/> value into the provided character span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="charsWritten">The number of characters written to the destination.</param>
    /// <param name="format">The format string.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // Use the optimized zero-allocation implementation
        return TryFormatOptimized(destination, out charsWritten, format, provider);
    }

    /// <summary>
    /// Tries to format this <see cref="BigNumber"/> value into the provided UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">The number of bytes written to the destination.</param>
    /// <param name="format">The format string.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // Use the optimized zero-allocation implementation
        return TryFormatUtf8Optimized(utf8Destination, out bytesWritten, format, provider);
    }
#endif

    /// <summary>
    /// Tries to format this <see cref="BigNumber"/> value into the provided character span using default formatting.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="charsWritten">The number of characters written to the destination.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten)
    {
        // Use the optimized zero-allocation implementation
        return TryFormatOptimized(destination, out charsWritten, default, null);
    }

    /// <summary>
    /// Tries to format this <see cref="BigNumber"/> value into the provided UTF-8 byte span using default formatting.
    /// </summary>
    /// <param name="destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">The number of bytes written to the destination.</param>
    /// <returns><c>true</c> if formatting succeeded; otherwise, <c>false</c>.</returns>
    public bool TryFormat(Span<byte> destination, out int bytesWritten)
    {
        // Use the optimized zero-allocation implementation
        return TryFormatUtf8Optimized(destination, out bytesWritten, default, null);
    }

    /// <summary>
    /// Parses a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    public static BigNumber Parse(string s, IFormatProvider? provider = null)
    {
#if NET
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
#else
        if (s == null)
        {
            throw new ArgumentNullException(nameof(s));
        }

        return Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
#endif
    }

    /// <summary>
    /// Parses a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    public static BigNumber Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
    {
        if (TryParse(s, style, provider, out BigNumber result))
        {
            return result;
        }

        throw new FormatException($"Unable to parse '{s.ToString()}' as a BigNumber.");
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out BigNumber result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), NumberStyles.Float | NumberStyles.AllowThousands, null, out result);
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigNumber result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
    }

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigNumber result)
    {
        return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
    }

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out BigNumber result)
    {
        return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, null, out result);
    }

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> s, IFormatProvider? provider, out BigNumber result)
    {
        return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
    }

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> s, out BigNumber result)
    {
        return TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, null, out result);
    }

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out BigNumber result)
    {
        // Validate NumberStyles - HexNumber (specifically AllowHexSpecifier) is not supported
        if ((style & NumberStyles.AllowHexSpecifier) != 0)
        {
            throw new ArgumentException("NumberStyles.AllowHexSpecifier is not supported for BigNumber.", nameof(style));
        }

        // Protect against DoS with very long inputs
        if (s.Length > MaxInputLength)
        {
            result = default;
            return false;
        }

        // Fast path 1: Common single-character values
        if (s.Length == 1)
        {
            char c = s[0];
            if (c == '0') { result = Zero; return true; }
            if (c == '1') { result = One; return true; }
            if (c >= '0' && c <= '9')
            {
                result = new BigNumber(c - '0', 0);
                return true;
            }
        }

        // Fast path 2: Simple integers (no decimal point, no exponent, no special symbols)
        // Only for standard Float style with InvariantCulture
        if ((style == NumberStyles.Float || style == (NumberStyles.Float | NumberStyles.AllowThousands)) &&
            (provider == null || provider == CultureInfo.InvariantCulture))
        {
            bool hasDecimalPoint = s.Contains('.');
            bool hasExponent = s.Contains('E') || s.Contains('e');
            bool hasSign = s.Length > 0 && (s[0] == '-' || s[0] == '+');

            if (!hasDecimalPoint && !hasExponent)
            {
                // Try parsing as long for small integers
#if NET
                if (s.Length < 19 && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
#else
                if (s.Length < 19 && ParsingPolyfills.TryParseInt64Invariant(s, out long longValue))
#endif
                {
                    result = new BigNumber(longValue, 0).Normalize();
                    return true;
                }
            }
        }

        // Standard path: complex parsing with full NumberStyles support
        var formatInfo = NumberFormatInfo.GetInstance(provider);

        // Handle leading whitespace
        if ((style & NumberStyles.AllowLeadingWhite) != 0)
        {
            s = s.TrimStart();
        }
        else if (s.Length > 0 && char.IsWhiteSpace(s[0]))
        {
            result = default;
            return false;
        }

        if (s.IsEmpty)
        {
            result = default;
            return false;
        }

        // Handle trailing whitespace
        if ((style & NumberStyles.AllowTrailingWhite) != 0)
        {
            s = s.TrimEnd();
        }
        else if (s.Length > 0 && char.IsWhiteSpace(s[^1]))
        {
            result = default;
            return false;
        }

        if (s.IsEmpty)
        {
            result = default;
            return false;
        }

        bool isNegative = false;

        // Handle parentheses (negative)
        if ((style & NumberStyles.AllowParentheses) != 0)
        {
            if (s.Length >= 2 && s[0] == '(' && s[^1] == ')')
            {
                isNegative = true;
                s = s.Slice(1, s.Length - 2);
            }
        }

        // Handle currency symbol (before signs since currency can have signs around it)
        if ((style & NumberStyles.AllowCurrencySymbol) != 0)
        {
            // Try leading currency with optional whitespace
            if (s.StartsWith(formatInfo.CurrencySymbol))
            {
                s = s.Slice(formatInfo.CurrencySymbol.Length);
                if (s.Length > 0 && char.IsWhiteSpace(s[0]))
                {
                    s = s.TrimStart();
                }
            }

            // Try trailing currency with optional whitespace
            else if (s.EndsWith(formatInfo.CurrencySymbol))
            {
                s = s.Slice(0, s.Length - formatInfo.CurrencySymbol.Length);
                if (s.Length > 0 && char.IsWhiteSpace(s[^1]))
                {
                    s = s.TrimEnd();
                }
            }
        }

        // Handle leading sign
        if ((style & NumberStyles.AllowLeadingSign) != 0 && s.Length > 0)
        {
            if (s.StartsWith(formatInfo.PositiveSign))
            {
                s = s.Slice(formatInfo.PositiveSign.Length);

                // After consuming sign, no whitespace allowed before digits
                if (s.Length > 0 && char.IsWhiteSpace(s[0]))
                {
                    result = default;
                    return false;
                }
            }
            else if (s.StartsWith(formatInfo.NegativeSign))
            {
                isNegative = true;
                s = s.Slice(formatInfo.NegativeSign.Length);

                // After consuming sign, no whitespace allowed before digits
                if (s.Length > 0 && char.IsWhiteSpace(s[0]))
                {
                    result = default;
                    return false;
                }
            }
        }
        else if (s.Length > 0 && (s.StartsWith(formatInfo.PositiveSign) || s.StartsWith(formatInfo.NegativeSign)))
        {
            // Sign present but not allowed
            result = default;
            return false;
        }

        // Handle trailing sign
        if ((style & NumberStyles.AllowTrailingSign) != 0 && s.Length > 0)
        {
            if (s.EndsWith(formatInfo.PositiveSign))
            {
                s = s.Slice(0, s.Length - formatInfo.PositiveSign.Length);
            }
            else if (s.EndsWith(formatInfo.NegativeSign))
            {
                isNegative = true;
                s = s.Slice(0, s.Length - formatInfo.NegativeSign.Length);
            }
        }
        else if (s.Length > 0 && (s.EndsWith(formatInfo.PositiveSign) || s.EndsWith(formatInfo.NegativeSign)))
        {
            // Sign present but not allowed
            result = default;
            return false;
        }

        if (s.IsEmpty)
        {
            result = default;
            return false;
        }

        // Get the appropriate decimal separator
        string decimalSeparator = (style & NumberStyles.AllowCurrencySymbol) != 0
            ? formatInfo.CurrencyDecimalSeparator
            : formatInfo.NumberDecimalSeparator;

        string groupSeparator = (style & NumberStyles.AllowCurrencySymbol) != 0
            ? formatInfo.CurrencyGroupSeparator
            : formatInfo.NumberGroupSeparator;

        // When AllowCurrencySymbol is set, implicitly allow decimal points
        NumberStyles effectiveStyle = style;
        if ((style & NumberStyles.AllowCurrencySymbol) != 0)
        {
            effectiveStyle |= NumberStyles.AllowDecimalPoint;
        }

        // Parse the number components
        int eIndex = -1;
        if ((style & NumberStyles.AllowExponent) != 0)
        {
            eIndex = s.IndexOfAny('e', 'E');
        }

        ReadOnlySpan<char> significandPart = eIndex >= 0 ? s.Slice(0, eIndex) : s;
        ReadOnlySpan<char> exponentPart = eIndex >= 0 ? s.Slice(eIndex + 1) : ReadOnlySpan<char>.Empty;

        // Validate exponent part is not empty if 'e'/'E' was present
        if (eIndex >= 0 && exponentPart.IsEmpty)
        {
            result = default;
            return false;
        }

        // Remove thousands separators
        // Use stack allocation for small buffers, rent from pool for large ones
        char[]? rentedBuffer = null;
        Span<char> cleanedSignificand = significandPart.Length <= StackAllocThreshold
            ? stackalloc char[significandPart.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(significandPart.Length)).AsSpan(0, significandPart.Length);

        int cleanedLength = 0;

        bool foundDecimal = false;
        int decimalPosition = -1;

        try
        {
            for (int i = 0; i < significandPart.Length; i++)
            {
                // Check for decimal separator
                if (i + decimalSeparator.Length <= significandPart.Length &&
                    significandPart.Slice(i, decimalSeparator.Length).SequenceEqual(decimalSeparator))
                {
                    if ((effectiveStyle & NumberStyles.AllowDecimalPoint) == 0)
                    {
                        result = default;
                        return false;
                    }

                    if (foundDecimal)
                    {
                        // Multiple decimal points
                        result = default;
                        return false;
                    }

                    foundDecimal = true;
                    decimalPosition = cleanedLength;
                    i += decimalSeparator.Length - 1;
                    continue;
                }

                // Check for thousands separator
                if ((effectiveStyle & NumberStyles.AllowThousands) != 0 &&
                    i + groupSeparator.Length <= significandPart.Length &&
                    significandPart.Slice(i, groupSeparator.Length).SequenceEqual(groupSeparator))
                {
                    i += groupSeparator.Length - 1;
                    continue;
                }

                cleanedSignificand[cleanedLength++] = significandPart[i];
            }

            if (cleanedLength == 0)
            {
                result = default;
                return false;
            }

            // Parse the cleaned significand as BigInteger
#if NET
            if (!BigInteger.TryParse(cleanedSignificand.Slice(0, cleanedLength), NumberStyles.Integer, formatInfo, out BigInteger significand))
#else
            if (!ParsingPolyfills.TryParseBigInteger(cleanedSignificand.Slice(0, cleanedLength), NumberStyles.Integer, formatInfo, out BigInteger significand))
#endif
            {
                result = default;
                return false;
            }

            if (isNegative)
            {
                significand = -significand;
            }

            // Calculate exponent from decimal position
            int exponent = 0;
            if (foundDecimal && decimalPosition >= 0)
            {
                int fractionalDigits = cleanedLength - decimalPosition;
                exponent = -fractionalDigits;
            }

            // Parse explicit exponent if present
            if (!exponentPart.IsEmpty)
            {
#if NET
                if (!int.TryParse(exponentPart, NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, formatInfo, out int additionalExponent))
#else
                if (!ParsingPolyfills.TryParseInt32(exponentPart, NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, formatInfo, out int additionalExponent))
#endif
                {
                    result = default;
                    return false;
                }

                exponent += additionalExponent;
            }

            result = new BigNumber(significand, exponent).Normalize();
            return true;
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Parses UTF-8 bytes into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 bytes to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    public static BigNumber Parse(ReadOnlySpan<byte> utf8Text, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
    {
        if (TryParse(utf8Text, style, provider, out BigNumber result))
        {
            return result;
        }

        throw new FormatException("Unable to parse UTF-8 text as a BigNumber.");
    }

    /// <summary>
    /// Attempts to parse UTF-8 bytes into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 bytes to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider, out BigNumber result)
    {
        char[]? rentedArray = null;

        try
        {
            Span<char> chars = utf8Text.Length <= StackAllocThreshold
                ? stackalloc char[utf8Text.Length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(utf8Text.Length)).AsSpan(0, utf8Text.Length);

            int charCount = Encoding.UTF8.GetChars(utf8Text, chars);

            return TryParse(chars.Slice(0, charCount), style, provider, out result);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }

#if NET
    /// <summary>
    /// Parses a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    static BigNumber IParsable<BigNumber>.Parse(string s, IFormatProvider? provider) =>
        Parse(s, provider);

    /// <summary>
    /// Attempts to parse a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    static bool IParsable<BigNumber>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigNumber result) =>
        TryParse(s, provider, out result);

    /// <summary>
    /// Parses a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    static BigNumber ISpanParsable<BigNumber>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);

    /// <summary>
    /// Attempts to parse a span of characters into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    static bool ISpanParsable<BigNumber>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigNumber result) =>
        TryParse(s, provider, out result);

    /// <summary>
    /// Parses a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The parsed number.</returns>
    static BigNumber INumberBase<BigNumber>.Parse(string s, NumberStyles style, IFormatProvider? provider) =>
        Parse(s.AsSpan(), style, provider);

    /// <summary>
    /// Attempts to parse a string into a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="style">Number styles.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="result">The parsed number.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    static bool INumberBase<BigNumber>.TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out BigNumber result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), style, provider, out result);
    }
#endif

    /// <summary>
    /// Determines whether two <see cref="BigNumber"/> values are equal.
    /// </summary>
    public static bool operator ==(BigNumber left, BigNumber right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="BigNumber"/> values are not equal.
    /// </summary>
    public static bool operator !=(BigNumber left, BigNumber right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one value is less than another.
    /// </summary>
    public static bool operator <(BigNumber left, BigNumber right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one value is less than or equal to another.
    /// </summary>
    public static bool operator <=(BigNumber left, BigNumber right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one value is greater than another.
    /// </summary>
    public static bool operator >(BigNumber left, BigNumber right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one value is greater than or equal to another.
    /// </summary>
    public static bool operator >=(BigNumber left, BigNumber right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Adds two <see cref="BigNumber"/> values.
    /// </summary>
    public static BigNumber operator +(BigNumber left, BigNumber right)
    {
        // Fast path: One operand is zero
        if (left.Significand.IsZero) return right;
        if (right.Significand.IsZero) return left;

        if (left.Exponent == right.Exponent)
        {
            return new BigNumber(left.Significand + right.Significand, left.Exponent).Normalize();
        }

        if (left.Exponent > right.Exponent)
        {
            int diff = left.Exponent - right.Exponent;
            BigInteger leftScaled = SafeScaleByPowerOf10(left.Significand, diff);
            return new BigNumber(leftScaled + right.Significand, right.Exponent).Normalize();
        }
        else
        {
            int diff = right.Exponent - left.Exponent;
            BigInteger rightScaled = SafeScaleByPowerOf10(right.Significand, diff);
            return new BigNumber(left.Significand + rightScaled, left.Exponent).Normalize();
        }
    }

    /// <summary>
    /// Subtracts one <see cref="BigNumber"/> from another.
    /// </summary>
    public static BigNumber operator -(BigNumber left, BigNumber right)
    {
        // Fast path: Right operand is zero
        if (right.Significand.IsZero) return left;

        // Fast path: Left operand is zero
        if (left.Significand.IsZero) return -right;

        if (left.Exponent == right.Exponent)
        {
            return new BigNumber(left.Significand - right.Significand, left.Exponent).Normalize();
        }

        if (left.Exponent > right.Exponent)
        {
            int diff = left.Exponent - right.Exponent;
            BigInteger leftScaled = SafeScaleByPowerOf10(left.Significand, diff);
            return new BigNumber(leftScaled - right.Significand, right.Exponent).Normalize();
        }
        else
        {
            int diff = right.Exponent - left.Exponent;
            BigInteger rightScaled = SafeScaleByPowerOf10(right.Significand, diff);
            return new BigNumber(left.Significand - rightScaled, left.Exponent).Normalize();
        }
    }

    /// <summary>
    /// Multiplies two <see cref="BigNumber"/> values.
    /// </summary>
    public static BigNumber operator *(BigNumber left, BigNumber right)
    {
        // Fast path 1: Small integer multiplication (both operands ≤ 100 with exponent 0)
        bool leftIsSmallInt = left.Exponent == 0 && BigInteger.Abs(left.Significand) <= 100;
        bool rightIsSmallInt = right.Exponent == 0 && BigInteger.Abs(right.Significand) <= 100;

        if (leftIsSmallInt && rightIsSmallInt)
        {
            // Both are small integers - use long multiplication
            long product = (long)left.Significand * (long)right.Significand;
            return new BigNumber(product, 0);
        }

        if (leftIsSmallInt)
        {
            // Left is small integer - optimize multiplication
            long leftLong = (long)left.Significand;
            return new BigNumber(right.Significand * leftLong, right.Exponent).Normalize();
        }

        if (rightIsSmallInt)
        {
            // Right is small integer - optimize multiplication
            long rightLong = (long)right.Significand;
            return new BigNumber(left.Significand * rightLong, left.Exponent).Normalize();
        }

        // Fast path 2: Multiplication by common small values (2, 5, 10)
        if (left.Significand == 2 || left.Significand == 5 || left.Significand == 10)
        {
            int multiplier = (int)left.Significand;
            return new BigNumber(right.Significand * multiplier, right.Exponent + left.Exponent).Normalize();
        }

        if (right.Significand == 2 || right.Significand == 5 || right.Significand == 10)
        {
            int multiplier = (int)right.Significand;
            return new BigNumber(left.Significand * multiplier, left.Exponent + right.Exponent).Normalize();
        }

        // Standard path: BigInteger multiplication
        return new BigNumber(left.Significand * right.Significand, left.Exponent + right.Exponent).Normalize();
    }

    /// <summary>
    /// Divides one <see cref="BigNumber"/> by another with default precision.
    /// </summary>
    public static BigNumber operator /(BigNumber dividend, BigNumber divisor)
    {
        return Divide(dividend, divisor, 50);
    }

    /// <summary>
    /// Divides one <see cref="BigNumber"/> by another with specified precision.
    /// </summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <param name="precision">The number of decimal places of precision.</param>
    /// <returns>The quotient.</returns>
    /// <exception cref="DivideByZeroException">Thrown when divisor is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when precision is negative.</exception>
    public static BigNumber Divide(BigNumber dividend, BigNumber divisor, int precision)
    {
        if (precision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be non-negative.");
        }

        if (divisor.Significand.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (dividend.Significand.IsZero)
        {
            return Zero;
        }

        // Fast path 1: Division by power of 10 (significand = ±1)
        if (divisor.Significand == 1)
        {
            // Division by 10^n = just adjust exponent
            return new BigNumber(dividend.Significand, dividend.Exponent - divisor.Exponent);
        }

        if (divisor.Significand == -1)
        {
            // Division by -10^n = negate and adjust exponent
            return new BigNumber(-dividend.Significand, dividend.Exponent - divisor.Exponent);
        }

        // Fast path 2: Small integer divisor (exponent = 0, |significand| <= 1000)
        // Use capped precision to stay within the primary cache (0-255) for optimal performance
        if (divisor.Exponent == 0 && BigInteger.Abs(divisor.Significand) <= 1000)
        {
            int smallDivisor = (int)divisor.Significand;

            // Precision must be within the primary power-of-10 cache range (0-255)
            if (precision < 0 || precision > 255)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_PrecisionMustBeBetween0And255();
            }

            int cappedPrecision = precision;

            BigInteger scaledDividend = dividend.Significand * GetPowerOf10(cappedPrecision);
            BigInteger quotient = scaledDividend / smallDivisor;
            int newExponent = dividend.Exponent - cappedPrecision;
            return new BigNumber(quotient, newExponent).Normalize();
        }

        // Fast path 3: Same exponent optimization
        // Cap precision to stay within cache for better performance
        if (dividend.Exponent == divisor.Exponent)
        {
            // Precision must be within the primary power-of-10 cache range (0-255)
            if (precision < 0 || precision > 255)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_PrecisionMustBeBetween0And255();
            }

            int cappedPrecision = precision;

            BigInteger scaledDividend = dividend.Significand * GetPowerOf10(cappedPrecision);
            BigInteger quotient = scaledDividend / divisor.Significand;
            return new BigNumber(quotient, -cappedPrecision).Normalize();
        }

        // Standard path: full BigInteger division
        // Precision must be within the primary power-of-10 cache range (0-255)
        if (precision < 0 || precision > 255)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException_PrecisionMustBeBetween0And255();
        }

        int standardPrecision = precision;
        BigInteger standardScaledDividend = dividend.Significand * GetPowerOf10(standardPrecision);
        BigInteger standardQuotient = standardScaledDividend / divisor.Significand;
        int standardNewExponent = dividend.Exponent - divisor.Exponent - standardPrecision;

        return new BigNumber(standardQuotient, standardNewExponent).Normalize();
    }

    /// <summary>
    /// Computes the remainder of division.
    /// </summary>
    public static BigNumber operator %(BigNumber left, BigNumber right)
    {
        if (right.Significand.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (left.Significand.IsZero)
        {
            return Zero;
        }

        // Use modular arithmetic to avoid expanding unnecessarily
        // For left = s1 * 10^e1 and right = s2 * 10^e2:
        // If e1 >= e2: (s1 * 10^e1) % (s2 * 10^e2) = ((s1 * 10^(e1-e2)) % s2) * 10^e2
        // If e1 < e2: (s1 * 10^e1) % (s2 * 10^e2) = s1 * 10^e1 (since left < right in magnitude)
        if (left.Exponent >= right.Exponent)
        {
            int exponentDiff = left.Exponent - right.Exponent;
            BigInteger scaledLeft = exponentDiff == 0 ? left.Significand : left.Significand * GetPowerOf10(exponentDiff);
            BigInteger remainder = scaledLeft % right.Significand;
            return new BigNumber(remainder, right.Exponent).Normalize();
        }
        else
        {
            // left has smaller exponent, so if |left| < |right * 10^(e2-e1)|, result is just left
            // But we need to check this properly
            int exponentDiff = right.Exponent - left.Exponent;
            BigInteger scaledRight = right.Significand * GetPowerOf10(exponentDiff);
            BigInteger remainder = left.Significand % scaledRight;
            return new BigNumber(remainder, left.Exponent).Normalize();
        }
    }

    /// <summary>
    /// Negates a value.
    /// </summary>
    public static BigNumber operator -(BigNumber value) => new(-value.Significand, value.Exponent);

    /// <summary>
    /// Returns the value unchanged (unary plus).
    /// </summary>
    public static BigNumber operator +(BigNumber value) => value;

    /// <summary>
    /// Increments a value by one.
    /// </summary>
    public static BigNumber operator ++(BigNumber value) => value + One;

    /// <summary>
    /// Decrements a value by one.
    /// </summary>
    public static BigNumber operator --(BigNumber value) => value - One;

    /// <summary>
    /// Converts an <see cref="int"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static implicit operator BigNumber(int value) => new(value, 0);

    /// <summary>
    /// Converts a <see cref="long"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static implicit operator BigNumber(long value) => new(value, 0);

    /// <summary>
    /// Converts a <see cref="long"/> to a <see cref="BigNumber"/>.
    /// </summary>
    [CLSCompliant(false)]
    public static implicit operator BigNumber(ulong value) => new(value, 0);

    /// <summary>
    /// Converts a <see cref="BigInteger"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static implicit operator BigNumber(BigInteger value) => new(value, 0);

    /// <summary>
    /// Converts a <see cref="decimal"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static implicit operator BigNumber(decimal value)
    {
        string str = value.ToString("G29", CultureInfo.InvariantCulture);
        return Parse(str, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts a <see cref="double"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static implicit operator BigNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException("Cannot convert NaN or Infinity to BigNumber.", nameof(value));
        }

        string str = value.ToString("G17", CultureInfo.InvariantCulture);
        return Parse(str, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Explicitly converts a <see cref="BigNumber"/> to a <see cref="decimal"/>.
    /// </summary>
    public static explicit operator decimal(BigNumber value)
    {
        BigInteger significand = value.Significand;
        int exponent = value.Exponent;

        // Fast path for zero
        if (significand.IsZero)
        {
            return decimal.Zero;
        }

        // Adjust exponent to be within a manageable range for decimal
        // This might lose precision for very large or small exponents, which is acceptable for an explicit conversion.
        const int maxDecimalExponent = 28;

        const int minDecimalExponent = -28;

        if (exponent > maxDecimalExponent)
        {
            significand *= BigInteger.Pow(10, exponent - maxDecimalExponent);
            exponent = maxDecimalExponent;
        }
        else if (exponent < minDecimalExponent)
        {
            significand /= BigInteger.Pow(10, -exponent + minDecimalExponent);
            exponent = minDecimalExponent;
        }

        decimal result = (decimal)significand;

        if (exponent > 0)
        {
            result *= (decimal)Math.Pow(10, exponent);
        }
        else if (exponent < 0)
        {
            result /= (decimal)Math.Pow(10, -exponent);
        }

        return result;
    }

    /// <summary>
    /// Explicitly converts a <see cref="BigNumber"/> to a <see cref="double"/>.
    /// </summary>
    public static explicit operator double(BigNumber value)
    {
        return (double)value.Significand * Math.Pow(10, value.Exponent);
    }

    /// <summary>
    /// Explicitly converts a <see cref="BigNumber"/> to a <see cref="float"/>.
    /// </summary>
    public static explicit operator float(BigNumber value)
    {
        return (float)((double)value.Significand * Math.Pow(10, value.Exponent));
    }

    /// <summary>
    /// Explicitly converts a <see cref="BigNumber"/> to a <see cref="long"/>.
    /// </summary>
    public static explicit operator long(BigNumber value)
    {
        BigInteger intValue = value.Significand;

        if (value.Exponent > 0)
        {
            if (value.Exponent > int.MaxValue)
            {
                throw new OverflowException($"Exponent {value.Exponent} is too large for conversion to long.");
            }

            intValue = SafeScaleByPowerOf10(intValue, value.Exponent);
        }
        else if (value.Exponent < 0)
        {
            int absExponent = -value.Exponent;
            if (absExponent > int.MaxValue)
            {
                // Very small number, will truncate to zero
                return 0;
            }

            intValue /= GetPowerOf10(absExponent);
        }

        return (long)intValue;
    }

    /// <summary>
    /// Explicitly converts a <see cref="BigNumber"/> to a <see cref="ulong"/>.
    /// </summary>
    [CLSCompliant(false)]
    public static explicit operator ulong(BigNumber value)
    {
        BigInteger intValue = value.Significand;

        if (value.Exponent > 0)
        {
            if (value.Exponent > int.MaxValue)
            {
                throw new OverflowException($"Exponent {value.Exponent} is too large for conversion to long.");
            }

            intValue = SafeScaleByPowerOf10(intValue, value.Exponent);
        }
        else if (value.Exponent < 0)
        {
            int absExponent = -value.Exponent;
            if (absExponent > int.MaxValue)
            {
                // Very small number, will truncate to zero
                return 0;
            }

            intValue /= GetPowerOf10(absExponent);
        }

        return (ulong)intValue;
    }

    /// <summary>
    /// Returns the absolute value.
    /// </summary>
    public static BigNumber Abs(BigNumber value) => new(BigInteger.Abs(value.Significand), value.Exponent);

    /// <summary>
    /// Returns the sign of the number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative, 0 for zero, 1 for positive.</returns>
    public static int Sign(BigNumber value) => value.Significand.Sign;

    /// <summary>
    /// Raises a BigNumber to an integer power.
    /// </summary>
    /// <param name="value">The base value.</param>
    /// <param name="exponent">The integer exponent.</param>
    /// <returns>The value raised to the specified power.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when exponent is negative.</exception>
    public static BigNumber Pow(BigNumber value, int exponent)
    {
        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative. Use Divide for negative powers.");
        }

        if (exponent == 0)
        {
            return One;
        }

        if (exponent == 1)
        {
            return value;
        }

        if (value.Significand.IsZero)
        {
            return Zero;
        }

        // Use exponentiation by squaring for efficiency
        var resultSignificand = BigInteger.Pow(value.Significand, exponent);
        int resultExponent = value.Exponent * exponent;

        return new BigNumber(resultSignificand, resultExponent).Normalize();
    }

    /// <summary>
    /// Computes the square root of a BigNumber using Newton's method.
    /// </summary>
    /// <param name="value">The value to find the square root of.</param>
    /// <param name="precision">The number of decimal places of precision.</param>
    /// <returns>The square root.</returns>
    /// <exception cref="ArgumentException">Thrown when value is negative.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when precision is negative.</exception>
    public static BigNumber Sqrt(BigNumber value, int precision)
    {
        if (precision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be non-negative.");
        }

        if (value.Significand < 0)
        {
            throw new ArgumentException("Cannot compute square root of negative number.", nameof(value));
        }

        if (value.Significand.IsZero)
        {
            return Zero;
        }

        // For odd exponents, adjust significand to make exponent even
        BigNumber adjusted = value;
        if (value.Exponent % 2 != 0)
        {
            adjusted = new BigNumber(value.Significand * 10, value.Exponent - 1);
        }

        // Scale up for precision
        int scalePower = precision * 2;
        BigInteger scaledValue = SafeScaleByPowerOf10(adjusted.Significand, scalePower);

        // Newton's method: x_{n+1} = (x_n + S/x_n) / 2
        BigInteger x = scaledValue / 2;
        BigInteger previous;
        int iterations = 0;

        const int maxIterations = 100;

        do
        {
            previous = x;
            x = (x + (scaledValue / x)) / 2;
            iterations++;
        }
        while (x != previous && iterations < maxIterations);

        int resultExponent = (adjusted.Exponent / 2) - precision;
        return new BigNumber(x, resultExponent).Normalize();
    }

    /// <summary>
    /// Rounds a value to a specified number of decimal places.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <param name="decimals">The number of decimal places.</param>
    /// <param name="mode">The rounding mode.</param>
    /// <returns>The rounded value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when decimals is negative.</exception>
    public static BigNumber Round(BigNumber value, int decimals, MidpointRounding mode = MidpointRounding.ToEven)
    {
        if (decimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), "Decimals must be non-negative.");
        }

        return RoundToPrecision(value, decimals, mode);
    }

    /// <summary>
    /// Returns the largest integer less than or equal to the specified number.
    /// </summary>
    /// <param name="value">The value to floor.</param>
    /// <returns>The floor of the value.</returns>
    public static BigNumber Floor(BigNumber value)
    {
        if (value.Exponent >= 0)
        {
            return value;
        }

        BigNumber normalized = value.Normalize();
        if (normalized.Exponent >= 0)
        {
            return normalized;
        }

        int absExponent = -normalized.Exponent;
        if (absExponent > int.MaxValue)
        {
            return Zero;
        }

        BigInteger divisor = GetPowerOf10(absExponent);
        var quotient = BigInteger.DivRem(normalized.Significand, divisor, out BigInteger remainder);

        // If negative and has remainder, subtract 1
        if (normalized.Significand < 0 && remainder != 0)
        {
            quotient -= 1;
        }

        return new BigNumber(quotient, 0);
    }

    /// <summary>
    /// Returns the smallest integer greater than or equal to the specified number.
    /// </summary>
    /// <param name="value">The value to ceiling.</param>
    /// <returns>The ceiling of the value.</returns>
    public static BigNumber Ceiling(BigNumber value)
    {
        if (value.Exponent >= 0)
        {
            return value;
        }

        BigNumber normalized = value.Normalize();
        if (normalized.Exponent >= 0)
        {
            return normalized;
        }

        int absExponent = -normalized.Exponent;
        if (absExponent > int.MaxValue)
        {
            return value.Significand < 0 ? Zero : One;
        }

        BigInteger divisor = GetPowerOf10(absExponent);
        var quotient = BigInteger.DivRem(normalized.Significand, divisor, out BigInteger remainder);

        // If positive and has remainder, add 1
        if (normalized.Significand > 0 && remainder != 0)
        {
            quotient += 1;
        }

        return new BigNumber(quotient, 0);
    }

    /// <summary>
    /// Truncates a value to an integer by removing the fractional part.
    /// </summary>
    /// <param name="value">The value to truncate.</param>
    /// <returns>The truncated value.</returns>
    public static BigNumber Truncate(BigNumber value)
    {
        if (value.Exponent >= 0)
        {
            return value;
        }

        BigNumber normalized = value.Normalize();
        if (normalized.Exponent >= 0)
        {
            return normalized;
        }

        int absExponent = -normalized.Exponent;
        if (absExponent > int.MaxValue)
        {
            return Zero;
        }

        BigInteger divisor = GetPowerOf10(absExponent);
        BigInteger quotient = normalized.Significand / divisor;

        return new BigNumber(quotient, 0);
    }

#if NET
    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsCanonical(BigNumber value) => true;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsComplexNumber(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsEvenInteger(BigNumber value) => value.IsInteger() && value.Significand.IsEven;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsFinite(BigNumber value) => true;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsImaginaryNumber(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsInfinity(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsInteger(BigNumber value) => value.IsInteger();

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsNaN(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsNegative(BigNumber value) => value.Significand.Sign < 0;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsNegativeInfinity(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsNormal(BigNumber value) => !value.Significand.IsZero;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsOddInteger(BigNumber value) => value.IsInteger() && !value.Significand.IsEven;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsPositive(BigNumber value) => value.Significand.Sign > 0;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsPositiveInfinity(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsRealNumber(BigNumber value) => true;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsSubnormal(BigNumber value) => false;

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.IsZero(BigNumber value) => value.Significand.IsZero;

    /// <inheritdoc/>
    static BigNumber INumberBase<BigNumber>.MaxMagnitude(BigNumber x, BigNumber y) => MaxMagnitude(x, y);

    /// <inheritdoc/>
    static BigNumber INumberBase<BigNumber>.MaxMagnitudeNumber(BigNumber x, BigNumber y) => MaxMagnitude(x, y);

    /// <inheritdoc/>
    static BigNumber INumberBase<BigNumber>.MinMagnitude(BigNumber x, BigNumber y) => MinMagnitude(x, y);

    /// <inheritdoc/>
    static BigNumber INumberBase<BigNumber>.MinMagnitudeNumber(BigNumber x, BigNumber y) => MinMagnitude(x, y);

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertFromChecked<TOther>(TOther value, out BigNumber result)
    {
        return TryConvertFrom(value, out result);
    }

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertFromSaturating<TOther>(TOther value, out BigNumber result)
    {
        return TryConvertFrom(value, out result);
    }

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertFromTruncating<TOther>(TOther value, out BigNumber result)
    {
        return TryConvertFrom(value, out result);
    }

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertToChecked<TOther>(BigNumber value, [MaybeNullWhen(false)] out TOther result)
    {
        return TryConvertToChecked(value, out result);
    }

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertToSaturating<TOther>(BigNumber value, [MaybeNullWhen(false)] out TOther result)
    {
        return TryConvertToChecked(value, out result);
    }

    /// <inheritdoc/>
    static bool INumberBase<BigNumber>.TryConvertToTruncating<TOther>(BigNumber value, [MaybeNullWhen(false)] out TOther result)
    {
        return TryConvertToChecked(value, out result);
    }

    /// <inheritdoc/>
    static BigNumber INumber<BigNumber>.Max(BigNumber x, BigNumber y) => x > y ? x : y;

    /// <inheritdoc/>
    static BigNumber INumber<BigNumber>.Min(BigNumber x, BigNumber y) => x < y ? x : y;

    /// <inheritdoc/>
    static BigNumber INumber<BigNumber>.Clamp(BigNumber value, BigNumber min, BigNumber max) =>
        value < min ? min : value > max ? max : value;

    private static bool TryConvertFrom<TOther>(TOther value, out BigNumber result)
    where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(int))
        {
            result = (int)(object)value!;
            return true;
        }

        if (typeof(TOther) == typeof(long))
        {
            result = (long)(object)value!;
            return true;
        }

        if (typeof(TOther) == typeof(decimal))
        {
            result = (decimal)(object)value!;
            return true;
        }

        if (typeof(TOther) == typeof(double))
        {
            result = (double)(object)value!;
            return true;
        }

        if (typeof(TOther) == typeof(BigInteger))
        {
            result = (BigInteger)(object)value!;
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryConvertToChecked<TOther>(BigNumber value, [MaybeNullWhen(false)] out TOther result)
    {
        if (typeof(TOther) == typeof(decimal))
        {
            result = (TOther)(object)(decimal)value;
            return true;
        }

        if (typeof(TOther) == typeof(double))
        {
            result = (TOther)(object)(double)value;
            return true;
        }

        if (typeof(TOther) == typeof(long))
        {
            result = (TOther)(object)(long)value;
            return true;
        }

        result = default;
        return false;
    }
#endif

    private static BigNumber MaxMagnitude(BigNumber x, BigNumber y)
    {
        var xAbs = BigInteger.Abs(x.Significand);
        var yAbs = BigInteger.Abs(y.Significand);
        return xAbs > yAbs ? x : y;
    }

    private static BigNumber MinMagnitude(BigNumber x, BigNumber y)
    {
        var xAbs = BigInteger.Abs(x.Significand);
        var yAbs = BigInteger.Abs(y.Significand);
        return xAbs < yAbs ? x : y;
    }
}