// <copyright file="BinaryJsonNumberComparisonLessThanGreaterThan.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
internal class BinaryJsonNumberComparisonLessThanGreaterThan
{
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsSByte =
    [
        new object[] { (sbyte)3, (sbyte)4, Expectation.False },
        new object[] { (sbyte)3, (short)4, Expectation.False },
        new object[] { (sbyte)3, 4, Expectation.False },
        new object[] { (sbyte)3, 4L, Expectation.False },
        new object[] { (sbyte)3, new Int128(0, 4), Expectation.False },
        new object[] { (sbyte)3, (byte)4, Expectation.False },
        new object[] { (sbyte)3, (ushort)4, Expectation.False },
        new object[] { (sbyte)3, 4U, Expectation.False },
        new object[] { (sbyte)3, 4UL, Expectation.False },
        new object[] { (sbyte)3, new UInt128(0, 4), Expectation.False },
        new object[] { (sbyte)3, (Half)4, Expectation.False },
        new object[] { (sbyte)3, 4F, Expectation.False },
        new object[] { (sbyte)3, 4M, Expectation.False },
        new object[] { (sbyte)3, 4D, Expectation.False },
        new object[] { (sbyte)3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsInt16 =
    [
        new object[] { (short)3, (sbyte)4, Expectation.False },
        new object[] { (short)3, (short)4, Expectation.False },
        new object[] { (short)3, 4, Expectation.False },
        new object[] { (short)3, 4L, Expectation.False },
        new object[] { (short)3, new Int128(0, 4), Expectation.False },
        new object[] { (short)3, (byte)4, Expectation.False },
        new object[] { (short)3, (ushort)4, Expectation.False },
        new object[] { (short)3, 4U, Expectation.False },
        new object[] { (short)3, 4UL, Expectation.False },
        new object[] { (short)3, new UInt128(0, 4), Expectation.False },
        new object[] { (short)3, (Half)4, Expectation.False },
        new object[] { (short)3, 4F, Expectation.False },
        new object[] { (short)3, 4M, Expectation.False },
        new object[] { (short)3, 4D, Expectation.False },
        new object[] { (short)3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsInt32 =
    [
        new object[] { 3, (sbyte)4, Expectation.False },
        new object[] { 3, (short)4, Expectation.False },
        new object[] { 3, 4, Expectation.False },
        new object[] { 3, 4L, Expectation.False },
        new object[] { 3, new Int128(0, 4), Expectation.False },
        new object[] { 3, (byte)4, Expectation.False },
        new object[] { 3, (ushort)4, Expectation.False },
        new object[] { 3, 4U, Expectation.False },
        new object[] { 3, 4UL, Expectation.False },
        new object[] { 3, new UInt128(0, 4), Expectation.False },
        new object[] { 3, (Half)4, Expectation.False },
        new object[] { 3, 4F, Expectation.False },
        new object[] { 3, 4M, Expectation.False },
        new object[] { 3, 4D, Expectation.False },
        new object[] { 3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsInt64 =
    [
        new object[] { 3L, (sbyte)4, Expectation.False },
        new object[] { 3L, (short)4, Expectation.False },
        new object[] { 3L, 4, Expectation.False },
        new object[] { 3L, 4L, Expectation.False },
        new object[] { 3L, new Int128(0, 4), Expectation.False },
        new object[] { 3L, (byte)4, Expectation.False },
        new object[] { 3L, (ushort)4, Expectation.False },
        new object[] { 3L, 4U, Expectation.False },
        new object[] { 3L, 4UL, Expectation.False },
        new object[] { 3L, new UInt128(0, 4), Expectation.False },
        new object[] { 3L, (Half)4, Expectation.False },
        new object[] { 3L, 4F, Expectation.False },
        new object[] { 3L, 4M, Expectation.False },
        new object[] { 3L, 4D, Expectation.False },
        new object[] { 3L, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsInt128 =
    [
        new object[] { new Int128(0, 3), (sbyte)4, Expectation.False },
        new object[] { new Int128(0, 3), (short)4, Expectation.False },
        new object[] { new Int128(0, 3), 4, Expectation.False },
        new object[] { new Int128(0, 3), 4L, Expectation.False },
        new object[] { new Int128(0, 3), new Int128(0, 4), Expectation.False },
        new object[] { new Int128(0, 3), (byte)4, Expectation.False },
        new object[] { new Int128(0, 3), (ushort)4, Expectation.False },
        new object[] { new Int128(0, 3), 4U, Expectation.False },
        new object[] { new Int128(0, 3), 4UL, Expectation.False },
        new object[] { new Int128(0, 3), new UInt128(0, 4), Expectation.False },
        new object[] { new Int128(0, 3), (Half)4, Expectation.False },
        new object[] { new Int128(0, 3), 4F, Expectation.False },
        new object[] { new Int128(0, 3), 4M, Expectation.False },
        new object[] { new Int128(0, 3), 4D, Expectation.False },
        new object[] { new Int128(0, 3), GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsByte =
    [
        new object[] { (byte)3, (sbyte)4, Expectation.False },
        new object[] { (byte)3, (short)4, Expectation.False },
        new object[] { (byte)3, 4, Expectation.False },
        new object[] { (byte)3, 4L, Expectation.False },
        new object[] { (byte)3, new Int128(0, 4), Expectation.False },
        new object[] { (byte)3, (byte)4, Expectation.False },
        new object[] { (byte)3, (ushort)4, Expectation.False },
        new object[] { (byte)3, 4U, Expectation.False },
        new object[] { (byte)3, 4UL, Expectation.False },
        new object[] { (byte)3, new UInt128(0, 4), Expectation.False },
        new object[] { (byte)3, (Half)4, Expectation.False },
        new object[] { (byte)3, 4F, Expectation.False },
        new object[] { (byte)3, 4M, Expectation.False },
        new object[] { (byte)3, 4D, Expectation.False },
        new object[] { (byte)3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsUInt16 =
    [
        new object[] { (ushort)3, (sbyte)4, Expectation.False },
        new object[] { (ushort)3, (short)4, Expectation.False },
        new object[] { (ushort)3, 4, Expectation.False },
        new object[] { (ushort)3, 4L, Expectation.False },
        new object[] { (ushort)3, new Int128(0, 4), Expectation.False },
        new object[] { (ushort)3, (byte)4, Expectation.False },
        new object[] { (ushort)3, (ushort)4, Expectation.False },
        new object[] { (ushort)3, 4U, Expectation.False },
        new object[] { (ushort)3, 4UL, Expectation.False },
        new object[] { (ushort)3, new UInt128(0, 4), Expectation.False },
        new object[] { (ushort)3, (Half)4, Expectation.False },
        new object[] { (ushort)3, 4F, Expectation.False },
        new object[] { (ushort)3, 4M, Expectation.False },
        new object[] { (ushort)3, 4D, Expectation.False },
        new object[] { (ushort)3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsUInt32 =
    [
        new object[] { 3U, (sbyte)4, Expectation.False },
        new object[] { 3U, (short)4, Expectation.False },
        new object[] { 3U, 4, Expectation.False },
        new object[] { 3U, 4L, Expectation.False },
        new object[] { 3U, new Int128(0, 4), Expectation.False },
        new object[] { 3U, (byte)4, Expectation.False },
        new object[] { 3U, (ushort)4, Expectation.False },
        new object[] { 3U, 4U, Expectation.False },
        new object[] { 3U, 4UL, Expectation.False },
        new object[] { 3U, new UInt128(0, 4), Expectation.False },
        new object[] { 3U, (Half)4, Expectation.False },
        new object[] { 3U, 4F, Expectation.False },
        new object[] { 3U, 4M, Expectation.False },
        new object[] { 3U, 4D, Expectation.False },
        new object[] { 3U, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsUInt64 =
    [
        new object[] { 3UL, (sbyte)4, Expectation.False },
        new object[] { 3UL, (short)4, Expectation.False },
        new object[] { 3UL, 4, Expectation.False },
        new object[] { 3UL, 4L, Expectation.False },
        new object[] { 3UL, new Int128(0, 4), Expectation.False },
        new object[] { 3UL, (byte)4, Expectation.False },
        new object[] { 3UL, (ushort)4, Expectation.False },
        new object[] { 3UL, 4U, Expectation.False },
        new object[] { 3UL, 4UL, Expectation.False },
        new object[] { 3UL, new UInt128(0, 4), Expectation.False },
        new object[] { 3UL, (Half)4, Expectation.False },
        new object[] { 3UL, 4F, Expectation.False },
        new object[] { 3UL, 4M, Expectation.False },
        new object[] { 3UL, 4D, Expectation.False },
        new object[] { 3UL, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsUInt128 =
    [
        new object[] { new UInt128(0, 3), (sbyte)4, Expectation.False },
        new object[] { new UInt128(0, 3), (short)4, Expectation.False },
        new object[] { new UInt128(0, 3), 4, Expectation.False },
        new object[] { new UInt128(0, 3), 4L, Expectation.False },
        new object[] { new UInt128(0, 3), new Int128(0, 4), Expectation.False },
        new object[] { new UInt128(0, 3), (byte)4, Expectation.False },
        new object[] { new UInt128(0, 3), (ushort)4, Expectation.False },
        new object[] { new UInt128(0, 3), 4U, Expectation.False },
        new object[] { new UInt128(0, 3), 4UL, Expectation.False },
        new object[] { new UInt128(0, 3), new UInt128(0, 4), Expectation.False },
        new object[] { new UInt128(0, 3), (Half)4, Expectation.False },
        new object[] { new UInt128(0, 3), 4F, Expectation.False },
        new object[] { new UInt128(0, 3), 4M, Expectation.False },
        new object[] { new UInt128(0, 3), 4D, Expectation.False },
        new object[] { new UInt128(0, 3), GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsHalf =
    [
        new object[] { (Half)3, (sbyte)4, Expectation.False },
        new object[] { (Half)3, (short)4, Expectation.False },
        new object[] { (Half)3, 4, Expectation.False },
        new object[] { (Half)3, 4L, Expectation.False },
        new object[] { (Half)3, new Int128(0, 4), Expectation.False },
        new object[] { (Half)3, (byte)4, Expectation.False },
        new object[] { (Half)3, (ushort)4, Expectation.False },
        new object[] { (Half)3, 4U, Expectation.False },
        new object[] { (Half)3, 4UL, Expectation.False },
        new object[] { (Half)3, new UInt128(0, 4), Expectation.False },
        new object[] { (Half)3, (Half)4, Expectation.False },
        new object[] { (Half)3, 4F, Expectation.False },
        new object[] { (Half)3, 4M, Expectation.False },
        new object[] { (Half)3, 4D, Expectation.False },
        new object[] { (Half)3, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsSingle =
    [
        new object[] { 3F, (sbyte)4, Expectation.False },
        new object[] { 3F, (short)4, Expectation.False },
        new object[] { 3F, 4, Expectation.False },
        new object[] { 3F, 4L, Expectation.False },
        new object[] { 3F, new Int128(0, 4), Expectation.False },
        new object[] { 3F, (byte)4, Expectation.False },
        new object[] { 3F, (ushort)4, Expectation.False },
        new object[] { 3F, 4U, Expectation.False },
        new object[] { 3F, 4UL, Expectation.False },
        new object[] { 3F, new UInt128(0, 4), Expectation.False },
        new object[] { 3F, (Half)4, Expectation.False },
        new object[] { 3F, 4F, Expectation.False },
        new object[] { 3F, 4M, Expectation.False },
        new object[] { 3F, 4D, Expectation.False },
        new object[] { 3F, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsDecimal =
    [
        new object[] { 3M, (sbyte)4, Expectation.False },
        new object[] { 3M, (short)4, Expectation.False },
        new object[] { 3M, 4, Expectation.False },
        new object[] { 3M, 4L, Expectation.False },
        new object[] { 3M, new Int128(0, 4), Expectation.False },
        new object[] { 3M, (byte)4, Expectation.False },
        new object[] { 3M, (ushort)4, Expectation.False },
        new object[] { 3M, 4U, Expectation.False },
        new object[] { 3M, 4UL, Expectation.False },
        new object[] { 3M, new UInt128(0, 4), Expectation.False },
        new object[] { 3M, (Half)4, Expectation.False },
        new object[] { 3M, 4F, Expectation.False },
        new object[] { 3M, 4M, Expectation.False },
        new object[] { 3M, 4D, Expectation.False },
        new object[] { 3M, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] GreaterThanExpectationsDouble =
    [
        new object[] { 3D, (sbyte)4, Expectation.False },
        new object[] { 3D, (short)4, Expectation.False },
        new object[] { 3D, 4, Expectation.False },
        new object[] { 3D, 4L, Expectation.False },
        new object[] { 3D, new Int128(0, 4), Expectation.False },
        new object[] { 3D, (byte)4, Expectation.False },
        new object[] { 3D, (ushort)4, Expectation.False },
        new object[] { 3D, 4U, Expectation.False },
        new object[] { 3D, 4UL, Expectation.False },
        new object[] { 3D, new UInt128(0, 4), Expectation.False },
        new object[] { 3D, (Half)4, Expectation.False },
        new object[] { 3D, 4F, Expectation.False },
        new object[] { 3D, 4M, Expectation.False },
        new object[] { 3D, 4D, Expectation.False },
        new object[] { 3D, GetJsonElement(4D), Expectation.False },
    ];

    /// <summary>
    /// The expectation from the comparison.
    /// </summary>
    public enum Expectation
    {
        /// <summary>
        /// False expectation.
        /// </summary>
        False,

        /// <summary>
        /// True expectation.
        /// </summary>
        True,

        /// <summary>
        /// Exception expected.
        /// </summary>
        Exception,
    }

    [TestCaseSource(nameof(GreaterThanExpectationsSByte))]
    [TestCaseSource(nameof(GreaterThanExpectationsInt16))]
    [TestCaseSource(nameof(GreaterThanExpectationsInt32))]
    [TestCaseSource(nameof(GreaterThanExpectationsInt64))]
    [TestCaseSource(nameof(GreaterThanExpectationsInt128))]
    [TestCaseSource(nameof(GreaterThanExpectationsByte))]
    [TestCaseSource(nameof(GreaterThanExpectationsUInt16))]
    [TestCaseSource(nameof(GreaterThanExpectationsUInt32))]
    [TestCaseSource(nameof(GreaterThanExpectationsUInt64))]
    [TestCaseSource(nameof(GreaterThanExpectationsUInt128))]
    [TestCaseSource(nameof(GreaterThanExpectationsHalf))]
    [TestCaseSource(nameof(GreaterThanExpectationsSingle))]
    [TestCaseSource(nameof(GreaterThanExpectationsDecimal))]
    [TestCaseSource(nameof(GreaterThanExpectationsDouble))]
    public void ValuesCompare(object lhs, object rhs, Expectation expected)
    {
        BinaryJsonNumber number1 = GetBinaryJsonNumberFor(lhs);
        if (rhs is JsonElement jsonElement)
        {
            switch (expected)
            {
                case Expectation.False:
                    Assert.IsFalse(number1 > jsonElement);
                    Assert.IsTrue(number1 < jsonElement);
                    Assert.IsTrue(jsonElement > number1);
                    Assert.IsFalse(jsonElement < number1);
                    break;
                case Expectation.True:
                    Assert.IsTrue(number1 > jsonElement);
                    Assert.IsFalse(number1 < jsonElement);
                    Assert.IsFalse(jsonElement > number1);
                    Assert.IsTrue(jsonElement < number1);
                    break;
                case Expectation.Exception:
                    Assert.Catch(() => _ = number1 > jsonElement);
                    Assert.Catch(() => _ = number1 < jsonElement);
                    Assert.Catch(() => _ = jsonElement > number1);
                    Assert.Catch(() => _ = jsonElement < number1);
                    break;
            }
        }
        else
        {
            BinaryJsonNumber number2 = GetBinaryJsonNumberFor(rhs);

            switch (expected)
            {
                case Expectation.False:
                    Assert.IsFalse(number1 > number2);
                    Assert.IsTrue(number1 < number2);
                    break;
                case Expectation.True:
                    Assert.IsTrue(number1 > number2);
                    Assert.IsFalse(number1 < number2);
                    break;
                case Expectation.Exception:
                    Assert.Catch(() => _ = number1 > number2);
                    Assert.Catch(() => _ = number1 < number2);
                    break;
            }
        }
    }

    private static BinaryJsonNumber GetBinaryJsonNumberFor(object value)
    {
        if (value is sbyte sb)
        {
            return new(sb);
        }

        if (value is short int16)
        {
            return new(int16);
        }

        if (value is int int32)
        {
            return new(int32);
        }

        if (value is long int64)
        {
            return new(int64);
        }

        if (value is Int128 int128)
        {
            return new(int128);
        }

        if (value is byte b)
        {
            return new(b);
        }

        if (value is ushort uint16)
        {
            return new(uint16);
        }

        if (value is uint uint32)
        {
            return new(uint32);
        }

        if (value is ulong uint64)
        {
            return new(uint64);
        }

        if (value is UInt128 uint128)
        {
            return new(uint128);
        }

        if (value is Half half)
        {
            return new(half);
        }

        if (value is float singleValue)
        {
            return new(singleValue);
        }

        if (value is double doubleValue)
        {
            return new(doubleValue);
        }

        if (value is decimal decimalValue)
        {
            return new(decimalValue);
        }

        throw new InvalidOperationException("Unsupported value kind");
    }

    private static JsonElement GetJsonElement(double v)
    {
        using var doc = JsonDocument.Parse(v.ToString());
        return doc.RootElement.Clone();
    }

    private static JsonElement GetJsonElement(float v)
    {
        using var doc = JsonDocument.Parse(v.ToString());
        return doc.RootElement.Clone();
    }

    private static JsonElement GetJsonElement(decimal v)
    {
        using var doc = JsonDocument.Parse(v.ToString());
        return doc.RootElement.Clone();
    }

    private static JsonElement GetJsonElement(int v)
    {
        using var doc = JsonDocument.Parse(v.ToString());
        return doc.RootElement.Clone();
    }
}