// <copyright file="JsonSchemaEvaluation.Number.cs" company="Endjin Limited">
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
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    public static readonly JsonSchemaMessageProvider IgnoredNotTypeNumber = static (buffer, out written) => IgnoredNotType("number"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider IgnoredNotTypeInteger = static (buffer, out written) => IgnoredNotType("integer"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedTypeNumber = static (buffer, out written) => ExpectedType("number"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedTypeInteger = static (buffer, out written) => ExpectedType("integer"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedMultipleOf = static (divisor, buffer, out written) => ExpectedMultipleOfDivisor(divisor, buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedEquals = static (value, buffer, out written) => ExpectedEqualsValue(value, buffer, out written);

    private const int MaximumByteExponent = 0;

    private const bool MaximumByteIsNegative = false;

    private const int MaximumDecimalExponent = 0;

    private const bool MaximumDecimalIsNegative = false;

    private const int MaximumDoubleExponent = 291;

    private const bool MaximumDoubleIsNegative = false;

    private const int MaximumHalfExponent = 0;

    private const bool MaximumHalfIsNegative = false;

    private const int MaximumInt128Exponent = 0;

    private const bool MaximumInt128IsNegative = false;

    private const int MaximumInt16Exponent = 0;

    private const bool MaximumInt16IsNegative = false;

    private const int MaximumInt32Exponent = 0;

    private const bool MaximumInt32IsNegative = false;

    private const int MaximumInt64Exponent = 0;

    private const bool MaximumInt64IsNegative = false;

    private const int MaximumSByteExponent = 0;

    private const bool MaximumSByteIsNegative = false;

    private const int MaximumSingleExponent = 20;

    private const bool MaximumSingleIsNegative = false;

    private const int MaximumUInt128Exponent = 0;

    private const bool MaximumUInt128IsNegative = false;

    private const int MaximumUInt16Exponent = 0;

    private const bool MaximumUInt16IsNegative = false;

    private const int MaximumUInt32Exponent = 0;

    private const bool MaximumUInt32IsNegative = false;

    private const int MaximumUInt64Exponent = 0;

    private const bool MaximumUInt64IsNegative = false;

    private const int MinimumByteExponent = 0;

    private const bool MinimumByteIsNegative = false;

    private const int MinimumDecimalExponent = 0;

    private const bool MinimumDecimalIsNegative = true;

    private const int MinimumDoubleExponent = 291;

    private const bool MinimumDoubleIsNegative = true;

    private const int MinimumHalfExponent = 0;

    private const bool MinimumHalfIsNegative = true;

    private const int MinimumInt128Exponent = 0;

    private const bool MinimumInt128IsNegative = true;

    private const int MinimumInt16Exponent = 0;

    private const bool MinimumInt16IsNegative = true;

    private const int MinimumInt32Exponent = 0;

    private const bool MinimumInt32IsNegative = true;

    private const int MinimumInt64Exponent = 0;

    private const bool MinimumInt64IsNegative = true;

    private const int MinimumSByteExponent = 0;

    private const bool MinimumSByteIsNegative = true;

    private const int MinimumSingleExponent = 20;

    private const bool MinimumSingleIsNegative = true;

    private const int MinimumUInt128Exponent = 0;

    private const bool MinimumUInt128IsNegative = false;

    private const int MinimumUInt16Exponent = 0;

    private const bool MinimumUInt16IsNegative = false;

    private const int MinimumUInt32Exponent = 0;

    private const bool MinimumUInt32IsNegative = false;

    private const int MinimumUInt64Exponent = 0;

    private const bool MinimumUInt64IsNegative = false;

    private static readonly JsonSchemaMessageProvider ExpectedByte = static (buffer, out written) => ExpectedNumberFormat("byte"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedDecimal = static (buffer, out written) => ExpectedNumberFormat("decimal"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedDouble = static (buffer, out written) => ExpectedNumberFormat("double"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedHalf = static (buffer, out written) => ExpectedNumberFormat("half"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedInt128 = static (buffer, out written) => ExpectedNumberFormat("int128"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedInt16 = static (buffer, out written) => ExpectedNumberFormat("int16"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedInt32 = static (buffer, out written) => ExpectedNumberFormat("int32"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedInt64 = static (buffer, out written) => ExpectedNumberFormat("int64"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedSByte = static (buffer, out written) => ExpectedNumberFormat("sbyte"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedSingle = static (buffer, out written) => ExpectedNumberFormat("single"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUInt128 = static (buffer, out written) => ExpectedNumberFormat("uint128"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUInt16 = static (buffer, out written) => ExpectedNumberFormat("uint16"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUInt32 = static (buffer, out written) => ExpectedNumberFormat("uint32"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUInt64 = static (buffer, out written) => ExpectedNumberFormat("uint64"u8, buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedNotEquals = static (value, buffer, out written) => ExpectedNotEqualsValue(value, buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedLessThanOrEquals = static (value, buffer, out written) => ExpectedLessThanOrEqualsValue(value, buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedLessThan = static (value, buffer, out written) => ExpectedLessThanValue(value, buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedGreaterThanOrEquals = static (value, buffer, out written) => ExpectedGreaterThanOrEqualsValue(value, buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedGreaterThan = static (value, buffer, out written) => ExpectedGreaterThanValue(value, buffer, out written);

    private static ReadOnlySpan<byte> MaximumByteFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumByteIntegral => "255"u8;

    private static ReadOnlySpan<byte> MaximumDecimalFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumDecimalIntegral => "79228162514264337593543950335"u8;

    private static ReadOnlySpan<byte> MaximumDoubleFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumDoubleIntegral => "17976931348623157"u8;

    private static ReadOnlySpan<byte> MaximumHalfFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumHalfIntegral => "65504"u8;

    private static ReadOnlySpan<byte> MaximumInt128Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumInt128Integral => "170141183460469231731687303715884105727"u8;

    private static ReadOnlySpan<byte> MaximumInt16Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumInt16Integral => "32767"u8;

    private static ReadOnlySpan<byte> MaximumInt32Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumInt32Integral => "2147483647"u8;

    private static ReadOnlySpan<byte> MaximumInt64Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumInt64Integral => "9223372036854775807"u8;

    private static ReadOnlySpan<byte> MaximumSByteFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumSByteIntegral => "127"u8;

    private static ReadOnlySpan<byte> MaximumSingleFractional => ""u8;

    private static ReadOnlySpan<byte> MaximumSingleIntegral => "340282346638528859"u8;

    private static ReadOnlySpan<byte> MaximumUInt128Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumUInt128Integral => "340282366920938463463374607431768211455"u8;

    private static ReadOnlySpan<byte> MaximumUInt16Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumUInt16Integral => "65535"u8;

    private static ReadOnlySpan<byte> MaximumUInt32Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumUInt32Integral => "4294967295"u8;

    private static ReadOnlySpan<byte> MaximumUInt64Fractional => ""u8;

    private static ReadOnlySpan<byte> MaximumUInt64Integral => "18446744073709551615"u8;

    private static ReadOnlySpan<byte> MinimumByteFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumByteIntegral => ""u8;

    private static ReadOnlySpan<byte> MinimumDecimalFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumDecimalIntegral => "79228162514264337593543950335"u8;

    private static ReadOnlySpan<byte> MinimumDoubleFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumDoubleIntegral => "17976931348623157"u8;

    private static ReadOnlySpan<byte> MinimumHalfFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumHalfIntegral => "65504"u8;

    private static ReadOnlySpan<byte> MinimumInt128Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumInt128Integral => "170141183460469231731687303715884105728"u8;

    private static ReadOnlySpan<byte> MinimumInt16Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumInt16Integral => "32768"u8;

    private static ReadOnlySpan<byte> MinimumInt32Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumInt32Integral => "2147483648"u8;

    private static ReadOnlySpan<byte> MinimumInt64Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumInt64Integral => "9223372036854775808"u8;

    private static ReadOnlySpan<byte> MinimumSByteFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumSByteIntegral => "128"u8;

    private static ReadOnlySpan<byte> MinimumSingleFractional => ""u8;

    private static ReadOnlySpan<byte> MinimumSingleIntegral => "340282346638528859"u8;

    private static ReadOnlySpan<byte> MinimumUInt128Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt128Integral => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt16Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt16Integral => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt32Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt32Integral => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt64Fractional => ""u8;

    private static ReadOnlySpan<byte> MinimumUInt64Integral => ""u8;

    /// <summary>
    /// Matches a JSON number as a multiple of the given divisor.
    /// </summary>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="divisor">The significand of the divisor represented as a <see cref="ulong"/>.</param>
    /// <param name="divisorExponent">The exponent of the divisor. This will be non-zero if the divisor had a fractional component.</param>
    /// <param name="divisorValue">The string representation of the divisor.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the normalized JSON number is a multiple of the divisor (i.e. <c>n mod D == 0</c>).</returns>
    /// <remarks>
    /// <para>
    /// We do not need to pass the sign of the JSON number as it is irrelevant to the calculation.
    /// </para>
    /// <para>
    /// The divisor is normalized so it provides the integral form of the divisor, with an appropriate exponent. Normalization means
    /// the exponent is the minmax value for the divisor, and the divisor will not be divisible by 10.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public static bool MatchMultipleOf(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ulong divisor, int divisorExponent, string divisorValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        Debug.Assert(divisor % 10 != 0);

        if (!JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, divisor, divisorExponent))
        {
            context.EvaluatedKeyword(false, divisorValue, ExpectedMultipleOf, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, divisorValue, ExpectedMultipleOf, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number equals.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side equals the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchEquals(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) != 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedEquals, keyword);

            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedEquals, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number not equals.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side does not equal the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchNotEquals(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) == 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedNotEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedNotEquals, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number less than.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side is less than the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchLessThan(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) >= 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedLessThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedLessThan, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number less than or equals.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side is less than or equal to the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchLessThanOrEquals(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) > 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedLessThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedLessThanOrEquals, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number greater than.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side is less than the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchGreaterThan(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) <= 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedGreaterThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedGreaterThan, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number greater than or equals.
    /// </summary>
    /// <param name="leftIsNegative">Indicates whether the left hand side is negative.</param>
    /// <param name="leftIntegral">The integral part of the left hand side.</param>
    /// <param name="leftFractional">The fractional part of the left hand side.</param>
    /// <param name="leftExponent">The exponent of the left hand side.</param>
    /// <param name="rightIsNegative">Indicates whether the right hand side is negative.</param>
    /// <param name="rightIntegral">The integral part of the right hand side.</param>
    /// <param name="rightFractional">The fractional part of the right hand side.</param>
    /// <param name="rightExponent">The exponent of the right hand side.</param>
    /// <param name="rightValue">The string representation of the right hand side.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the left hand side is less than or equal to the right hand side.</returns>
    [CLSCompliant(false)]
    public static bool MatchGreaterThanOrEquals(
        bool leftIsNegative, ReadOnlySpan<byte> leftIntegral, ReadOnlySpan<byte> leftFractional, int leftExponent,
        bool rightIsNegative, ReadOnlySpan<byte> rightIntegral, ReadOnlySpan<byte> rightFractional, int rightExponent,
        string rightValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative, leftIntegral, leftFractional, leftExponent,
            rightIsNegative, rightIntegral, rightFractional, rightExponent) < 0)
        {
            context.EvaluatedKeyword(false, rightValue, ExpectedGreaterThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, rightValue, ExpectedGreaterThanOrEquals, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number as a multiple of the given divisor.
    /// </summary>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="divisor">The significand of the divisor represented as a <see cref="ulong"/>.</param>
    /// <param name="divisorExponent">The exponent of the divisor. This will be non-zero if the divisor had a fractional component.</param>
    /// <param name="divisorValue">The string representation of the divisor.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns>True if the normalized JSON number is a multiple of the divisor (i.e. <c>n mod D == 0</c>).</returns>
    /// <remarks>
    /// <para>
    /// We do not need to pass the sign of the JSON number as it is irrelevant to the calculation.
    /// </para>
    /// <para>
    /// The divisor is normalized so it provides the integral form of the divisor, with an appropriate exponent. Normalization means
    /// the exponent is the minmax value for the divisor, and the divisor will not be divisible by 10.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public static bool MatchMultipleOf(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, BigInteger divisor, int divisorExponent, string divisorValue, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        Debug.Assert(divisor % 10 != 0);

        if (!JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, divisor, divisorExponent))
        {
            context.EvaluatedKeyword(false, divisorValue, ExpectedMultipleOf, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, divisorValue, ExpectedMultipleOf, keyword);

        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Byte type constraint, validating it as an 8-bit unsigned integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Byte; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchByte(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedByte, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumByteIsNegative,
            MinimumByteIntegral,
            MinimumByteFractional,
            MinimumByteExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedByte, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumByteIsNegative,
            MaximumByteIntegral,
            MaximumByteFractional,
            MaximumByteExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedByte, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedByte, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Decimal type constraint, validating it as a decimal floating-point number.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Decimal; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchDecimal(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumDecimalIsNegative,
            MinimumDecimalIntegral,
            MinimumDecimalFractional,
            MinimumDecimalExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDecimal, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumDecimalIsNegative,
            MaximumDecimalIntegral,
            MaximumDecimalFractional,
            MaximumDecimalExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDecimal, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedDecimal, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Double type constraint, validating it as a double-precision floating-point number.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Double; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchDouble(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumDoubleIsNegative,
            MinimumDoubleIntegral,
            MinimumDoubleFractional,
            MinimumDoubleExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDouble, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumDoubleIsNegative,
            MaximumDoubleIntegral,
            MaximumDoubleFractional,
            MaximumDoubleExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDouble, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedDouble, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Half type constraint, validating it as a half-precision floating-point number.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Half; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchHalf(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumHalfIsNegative,
            MinimumHalfIntegral,
            MinimumHalfFractional,
            MinimumHalfExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedHalf, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumHalfIsNegative,
            MaximumHalfIntegral,
            MaximumHalfFractional,
            MaximumHalfExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedHalf, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedHalf, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Int128 type constraint, validating it as a 128-bit signed integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Int128; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchInt128(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedInt128, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumInt128IsNegative,
            MinimumInt128Integral,
            MinimumInt128Fractional,
            MinimumInt128Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt128, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumInt128IsNegative,
            MaximumInt128Integral,
            MaximumInt128Fractional,
            MaximumInt128Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt128, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedInt128, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Int16 type constraint, validating it as a 16-bit signed integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Int16; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchInt16(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedInt16, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumInt16IsNegative,
            MinimumInt16Integral,
            MinimumInt16Fractional,
            MinimumInt16Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt16, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumInt16IsNegative,
            MaximumInt16Integral,
            MaximumInt16Fractional,
            MaximumInt16Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt16, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedInt16, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Int32 type constraint, validating it as a 32-bit signed integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Int32; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchInt32(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedInt32, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumInt32IsNegative,
            MinimumInt32Integral,
            MinimumInt32Fractional,
            MinimumInt32Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt32, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumInt32IsNegative,
            MaximumInt32Integral,
            MaximumInt32Fractional,
            MaximumInt32Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt32, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedInt32, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Int64 type constraint, validating it as a 64-bit signed integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Int64; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchInt64(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedInt64, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumInt64IsNegative,
            MinimumInt64Integral,
            MinimumInt64Fractional,
            MinimumInt64Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt64, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumInt64IsNegative,
            MaximumInt64Integral,
            MaximumInt64Fractional,
            MaximumInt64Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedInt64, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedInt64, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the SByte type constraint, validating it as an 8-bit signed integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid SByte; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchSByte(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedSByte, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumSByteIsNegative,
            MinimumSByteIntegral,
            MinimumSByteFractional,
            MinimumSByteExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedSByte, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumSByteIsNegative,
            MaximumSByteIntegral,
            MaximumSByteFractional,
            MaximumSByteExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedSByte, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedSByte, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the Single type constraint, validating it as a single-precision floating-point number.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid Single; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchSingle(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumSingleIsNegative,
            MinimumSingleIntegral,
            MinimumSingleFractional,
            MinimumSingleExponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedSingle, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumSingleIsNegative,
            MaximumSingleIntegral,
            MaximumSingleFractional,
            MaximumSingleExponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedSingle, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedSingle, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON token type against the "number" type constraint.
    /// </summary>
    /// <param name="tokenType">The JSON token type to validate.</param>
    /// <param name="typeKeyword">The type keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the token type is a number; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTypeNumber(JsonTokenType tokenType, ReadOnlySpan<byte> typeKeyword, ref JsonSchemaContext context)
    {
        if (tokenType != JsonTokenType.Number)
        {
            context.EvaluatedKeyword(false, ExpectedTypeNumber, typeKeyword);
            return false;
        }
        else
        {
            context.EvaluatedKeyword(true, ExpectedTypeNumber, typeKeyword);
        }

        return true;
    }

    /// <summary>
    /// Matches a JSON token type against the "number" type constraint.
    /// </summary>
    /// <param name="tokenType">The JSON token type to validate.</param>
    /// <param name="typeKeyword">The type keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <returns><see langword="true"/> if the token type is a number; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTypeInteger(JsonTokenType tokenType, ReadOnlySpan<byte> typeKeyword, int exponent, ref JsonSchemaContext context)
    {
        if (tokenType != JsonTokenType.Number)
        {
            context.EvaluatedKeyword(false, ExpectedTypeInteger, typeKeyword);
            return false;
        }
        else
        {
            if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
            {
                context.EvaluatedKeyword(false, ExpectedTypeInteger, typeKeyword);
            }
            else
            {
                context.EvaluatedKeyword(true, ExpectedTypeInteger, typeKeyword);
            }
        }

        return true;
    }

    /// <summary>
    /// Matches a JSON number against the UInt128 type constraint, validating it as a 128-bit unsigned integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid UInt128; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUInt128(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedUInt128, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumUInt128IsNegative,
            MinimumUInt128Integral,
            MinimumUInt128Fractional,
            MinimumUInt128Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt128, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumUInt128IsNegative,
            MaximumUInt128Integral,
            MaximumUInt128Fractional,
            MaximumUInt128Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt128, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUInt128, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the UInt16 type constraint, validating it as a 16-bit unsigned integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid UInt16; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUInt16(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedUInt16, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumUInt16IsNegative,
            MinimumUInt16Integral,
            MinimumUInt16Fractional,
            MinimumUInt16Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt16, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumUInt16IsNegative,
            MaximumUInt16Integral,
            MaximumUInt16Fractional,
            MaximumUInt16Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt16, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUInt16, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the UInt32 type constraint, validating it as a 32-bit unsigned integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid UInt32; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUInt32(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedUInt32, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumUInt32IsNegative,
            MinimumUInt32Integral,
            MinimumUInt32Fractional,
            MinimumUInt32Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt32, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumUInt32IsNegative,
            MaximumUInt32Integral,
            MaximumUInt32Fractional,
            MaximumUInt32Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt32, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUInt32, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON number against the UInt64 type constraint, validating it as a 64-bit unsigned integer.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid UInt64; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUInt64(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent))
        {
            context.EvaluatedKeyword(false, ExpectedUInt64, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumUInt64IsNegative,
            MinimumUInt64Integral,
            MinimumUInt64Fractional,
            MinimumUInt64Exponent) < 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt64, keyword);
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumUInt64IsNegative,
            MaximumUInt64Integral,
            MaximumUInt64Fractional,
            MaximumUInt64Exponent) > 0)
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUInt64, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUInt64, keyword);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedNumberFormat(ReadOnlySpan<byte> typeName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedNumberFormat.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(typeName, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedEqualsValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedNotEqualsValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedNotEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedGreaterThanValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedGreaterThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedGreaterThanOrEqualsValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedGreaterThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLessThanValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLessThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ExpectedLessThanOrEqualsValue(string value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedLessThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(value, buffer, ref written);
    }
}