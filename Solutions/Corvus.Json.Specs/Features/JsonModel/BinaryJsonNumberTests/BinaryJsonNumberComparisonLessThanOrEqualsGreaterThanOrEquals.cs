// <copyright file="BinaryJsonNumberComparisonLessThanOrEqualsGreaterThanOrEquals.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
internal class BinaryJsonNumberComparisonLessThanOrEqualsGreaterThanOrEquals
{
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsSByte =
    [
        new object[] { (sbyte)3, (sbyte)4, Expectation.True },
        new object[] { (sbyte)3, (short)4, Expectation.True },
        new object[] { (sbyte)3, 4, Expectation.True },
        new object[] { (sbyte)3, 4L, Expectation.True },
        new object[] { (sbyte)3, new Int128(0, 4), Expectation.True },
        new object[] { (sbyte)3, (byte)4, Expectation.True },
        new object[] { (sbyte)3, (ushort)4, Expectation.True },
        new object[] { (sbyte)3, 4U, Expectation.True },
        new object[] { (sbyte)3, 4UL, Expectation.True },
        new object[] { (sbyte)3, new UInt128(0, 4), Expectation.True },
        new object[] { (sbyte)3, (Half)4, Expectation.True },
        new object[] { (sbyte)3, 4F, Expectation.True },
        new object[] { (sbyte)3, 4M, Expectation.True },
        new object[] { (sbyte)3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsInt16 =
    [
        new object[] { (short)3, (sbyte)4, Expectation.True },
        new object[] { (short)3, (short)4, Expectation.True },
        new object[] { (short)3, 4, Expectation.True },
        new object[] { (short)3, 4L, Expectation.True },
        new object[] { (short)3, new Int128(0, 4), Expectation.True },
        new object[] { (short)3, (byte)4, Expectation.True },
        new object[] { (short)3, (ushort)4, Expectation.True },
        new object[] { (short)3, 4U, Expectation.True },
        new object[] { (short)3, 4UL, Expectation.True },
        new object[] { (short)3, new UInt128(0, 4), Expectation.True },
        new object[] { (short)3, (Half)4, Expectation.True },
        new object[] { (short)3, 4F, Expectation.True },
        new object[] { (short)3, 4M, Expectation.True },
        new object[] { (short)3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsInt32 =
    [
        new object[] { 3, (sbyte)4, Expectation.True },
        new object[] { 3, (short)4, Expectation.True },
        new object[] { 3, 4, Expectation.True },
        new object[] { 3, 4L, Expectation.True },
        new object[] { 3, new Int128(0, 4), Expectation.True },
        new object[] { 3, (byte)4, Expectation.True },
        new object[] { 3, (ushort)4, Expectation.True },
        new object[] { 3, 4U, Expectation.True },
        new object[] { 3, 4UL, Expectation.True },
        new object[] { 3, new UInt128(0, 4), Expectation.True },
        new object[] { 3, (Half)4, Expectation.True },
        new object[] { 3, 4F, Expectation.True },
        new object[] { 3, 4M, Expectation.True },
        new object[] { 3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsInt64 =
    [
        new object[] { 3L, (sbyte)4, Expectation.True },
        new object[] { 3L, (short)4, Expectation.True },
        new object[] { 3L, 4, Expectation.True },
        new object[] { 3L, 4L, Expectation.True },
        new object[] { 3L, new Int128(0, 4), Expectation.True },
        new object[] { 3L, (byte)4, Expectation.True },
        new object[] { 3L, (ushort)4, Expectation.True },
        new object[] { 3L, 4U, Expectation.True },
        new object[] { 3L, 4UL, Expectation.True },
        new object[] { 3L, new UInt128(0, 4), Expectation.True },
        new object[] { 3L, (Half)4, Expectation.True },
        new object[] { 3L, 4F, Expectation.True },
        new object[] { 3L, 4M, Expectation.True },
        new object[] { 3L, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsInt128 =
    [
        new object[] { new Int128(0, 3), (sbyte)4, Expectation.True },
        new object[] { new Int128(0, 3), (short)4, Expectation.True },
        new object[] { new Int128(0, 3), 4, Expectation.True },
        new object[] { new Int128(0, 3), 4L, Expectation.True },
        new object[] { new Int128(0, 3), new Int128(0, 4), Expectation.True },
        new object[] { new Int128(0, 3), (byte)4, Expectation.True },
        new object[] { new Int128(0, 3), (ushort)4, Expectation.True },
        new object[] { new Int128(0, 3), 4U, Expectation.True },
        new object[] { new Int128(0, 3), 4UL, Expectation.True },
        new object[] { new Int128(0, 3), new UInt128(0, 4), Expectation.True },
        new object[] { new Int128(0, 3), (Half)4, Expectation.True },
        new object[] { new Int128(0, 3), 4F, Expectation.True },
        new object[] { new Int128(0, 3), 4M, Expectation.True },
        new object[] { new Int128(0, 3), 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsByte =
    [
        new object[] { (byte)3, (sbyte)4, Expectation.True },
        new object[] { (byte)3, (short)4, Expectation.True },
        new object[] { (byte)3, 4, Expectation.True },
        new object[] { (byte)3, 4L, Expectation.True },
        new object[] { (byte)3, new Int128(0, 4), Expectation.True },
        new object[] { (byte)3, (byte)4, Expectation.True },
        new object[] { (byte)3, (ushort)4, Expectation.True },
        new object[] { (byte)3, 4U, Expectation.True },
        new object[] { (byte)3, 4UL, Expectation.True },
        new object[] { (byte)3, new UInt128(0, 4), Expectation.True },
        new object[] { (byte)3, (Half)4, Expectation.True },
        new object[] { (byte)3, 4F, Expectation.True },
        new object[] { (byte)3, 4M, Expectation.True },
        new object[] { (byte)3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsUInt16 =
    [
        new object[] { (ushort)3, (sbyte)4, Expectation.True },
        new object[] { (ushort)3, (short)4, Expectation.True },
        new object[] { (ushort)3, 4, Expectation.True },
        new object[] { (ushort)3, 4L, Expectation.True },
        new object[] { (ushort)3, new Int128(0, 4), Expectation.True },
        new object[] { (ushort)3, (byte)4, Expectation.True },
        new object[] { (ushort)3, (ushort)4, Expectation.True },
        new object[] { (ushort)3, 4U, Expectation.True },
        new object[] { (ushort)3, 4UL, Expectation.True },
        new object[] { (ushort)3, new UInt128(0, 4), Expectation.True },
        new object[] { (ushort)3, (Half)4, Expectation.True },
        new object[] { (ushort)3, 4F, Expectation.True },
        new object[] { (ushort)3, 4M, Expectation.True },
        new object[] { (ushort)3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsUInt32 =
    [
        new object[] { 3U, (sbyte)4, Expectation.True },
        new object[] { 3U, (short)4, Expectation.True },
        new object[] { 3U, 4, Expectation.True },
        new object[] { 3U, 4L, Expectation.True },
        new object[] { 3U, new Int128(0, 4), Expectation.True },
        new object[] { 3U, (byte)4, Expectation.True },
        new object[] { 3U, (ushort)4, Expectation.True },
        new object[] { 3U, 4U, Expectation.True },
        new object[] { 3U, 4UL, Expectation.True },
        new object[] { 3U, new UInt128(0, 4), Expectation.True },
        new object[] { 3U, (Half)4, Expectation.True },
        new object[] { 3U, 4F, Expectation.True },
        new object[] { 3U, 4M, Expectation.True },
        new object[] { 3U, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsUInt64 =
    [
        new object[] { 3UL, (sbyte)4, Expectation.True },
        new object[] { 3UL, (short)4, Expectation.True },
        new object[] { 3UL, 4, Expectation.True },
        new object[] { 3UL, 4L, Expectation.True },
        new object[] { 3UL, new Int128(0, 4), Expectation.True },
        new object[] { 3UL, (byte)4, Expectation.True },
        new object[] { 3UL, (ushort)4, Expectation.True },
        new object[] { 3UL, 4U, Expectation.True },
        new object[] { 3UL, 4UL, Expectation.True },
        new object[] { 3UL, new UInt128(0, 4), Expectation.True },
        new object[] { 3UL, (Half)4, Expectation.True },
        new object[] { 3UL, 4F, Expectation.True },
        new object[] { 3UL, 4M, Expectation.True },
        new object[] { 3UL, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsUInt128 =
    [
        new object[] { new UInt128(0, 3), (sbyte)4, Expectation.True },
        new object[] { new UInt128(0, 3), (short)4, Expectation.True },
        new object[] { new UInt128(0, 3), 4, Expectation.True },
        new object[] { new UInt128(0, 3), 4L, Expectation.True },
        new object[] { new UInt128(0, 3), new Int128(0, 4), Expectation.True },
        new object[] { new UInt128(0, 3), (byte)4, Expectation.True },
        new object[] { new UInt128(0, 3), (ushort)4, Expectation.True },
        new object[] { new UInt128(0, 3), 4U, Expectation.True },
        new object[] { new UInt128(0, 3), 4UL, Expectation.True },
        new object[] { new UInt128(0, 3), new UInt128(0, 4), Expectation.True },
        new object[] { new UInt128(0, 3), (Half)4, Expectation.True },
        new object[] { new UInt128(0, 3), 4F, Expectation.True },
        new object[] { new UInt128(0, 3), 4M, Expectation.True },
        new object[] { new UInt128(0, 3), 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsHalf =
    [
        new object[] { (Half)3, (sbyte)4, Expectation.True },
        new object[] { (Half)3, (short)4, Expectation.True },
        new object[] { (Half)3, 4, Expectation.True },
        new object[] { (Half)3, 4L, Expectation.True },
        new object[] { (Half)3, new Int128(0, 4), Expectation.True },
        new object[] { (Half)3, (byte)4, Expectation.True },
        new object[] { (Half)3, (ushort)4, Expectation.True },
        new object[] { (Half)3, 4U, Expectation.True },
        new object[] { (Half)3, 4UL, Expectation.True },
        new object[] { (Half)3, new UInt128(0, 4), Expectation.True },
        new object[] { (Half)3, (Half)4, Expectation.True },
        new object[] { (Half)3, 4F, Expectation.True },
        new object[] { (Half)3, 4M, Expectation.True },
        new object[] { (Half)3, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsSingle =
    [
        new object[] { 3F, (sbyte)4, Expectation.True },
        new object[] { 3F, (short)4, Expectation.True },
        new object[] { 3F, 4, Expectation.True },
        new object[] { 3F, 4L, Expectation.True },
        new object[] { 3F, new Int128(0, 4), Expectation.True },
        new object[] { 3F, (byte)4, Expectation.True },
        new object[] { 3F, (ushort)4, Expectation.True },
        new object[] { 3F, 4U, Expectation.True },
        new object[] { 3F, 4UL, Expectation.True },
        new object[] { 3F, new UInt128(0, 4), Expectation.True },
        new object[] { 3F, (Half)4, Expectation.True },
        new object[] { 3F, 4F, Expectation.True },
        new object[] { 3F, 4M, Expectation.True },
        new object[] { 3F, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsDecimal =
    [
        new object[] { 3M, (sbyte)4, Expectation.True },
        new object[] { 3M, (short)4, Expectation.True },
        new object[] { 3M, 4, Expectation.True },
        new object[] { 3M, 4L, Expectation.True },
        new object[] { 3M, new Int128(0, 4), Expectation.True },
        new object[] { 3M, (byte)4, Expectation.True },
        new object[] { 3M, (ushort)4, Expectation.True },
        new object[] { 3M, 4U, Expectation.True },
        new object[] { 3M, 4UL, Expectation.True },
        new object[] { 3M, new UInt128(0, 4), Expectation.True },
        new object[] { 3M, (Half)4, Expectation.True },
        new object[] { 3M, 4F, Expectation.True },
        new object[] { 3M, 4M, Expectation.True },
        new object[] { 3M, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] LessThanExpectationsDouble =
    [
        new object[] { 3D, (sbyte)4, Expectation.True },
        new object[] { 3D, (short)4, Expectation.True },
        new object[] { 3D, 4, Expectation.True },
        new object[] { 3D, 4L, Expectation.True },
        new object[] { 3D, new Int128(0, 4), Expectation.True },
        new object[] { 3D, (byte)4, Expectation.True },
        new object[] { 3D, (ushort)4, Expectation.True },
        new object[] { 3D, 4U, Expectation.True },
        new object[] { 3D, 4UL, Expectation.True },
        new object[] { 3D, new UInt128(0, 4), Expectation.True },
        new object[] { 3D, (Half)4, Expectation.True },
        new object[] { 3D, 4F, Expectation.True },
        new object[] { 3D, 4M, Expectation.True },
        new object[] { 3D, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsSByte =
    [
        new object[] { (sbyte)4, (sbyte)4, Expectation.True },
        new object[] { (sbyte)4, (short)4, Expectation.True },
        new object[] { (sbyte)4, 4, Expectation.True },
        new object[] { (sbyte)4, 4L, Expectation.True },
        new object[] { (sbyte)4, new Int128(0, 4), Expectation.True },
        new object[] { (sbyte)4, (byte)4, Expectation.True },
        new object[] { (sbyte)4, (ushort)4, Expectation.True },
        new object[] { (sbyte)4, 4U, Expectation.True },
        new object[] { (sbyte)4, 4UL, Expectation.True },
        new object[] { (sbyte)4, new UInt128(0, 4), Expectation.True },
        new object[] { (sbyte)4, (Half)4, Expectation.True },
        new object[] { (sbyte)4, 4F, Expectation.True },
        new object[] { (sbyte)4, 4M, Expectation.True },
        new object[] { (sbyte)4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsInt16 =
    [
        new object[] { (short)4, (sbyte)4, Expectation.True },
        new object[] { (short)4, (short)4, Expectation.True },
        new object[] { (short)4, 4, Expectation.True },
        new object[] { (short)4, 4L, Expectation.True },
        new object[] { (short)4, new Int128(0, 4), Expectation.True },
        new object[] { (short)4, (byte)4, Expectation.True },
        new object[] { (short)4, (ushort)4, Expectation.True },
        new object[] { (short)4, 4U, Expectation.True },
        new object[] { (short)4, 4UL, Expectation.True },
        new object[] { (short)4, new UInt128(0, 4), Expectation.True },
        new object[] { (short)4, (Half)4, Expectation.True },
        new object[] { (short)4, 4F, Expectation.True },
        new object[] { (short)4, 4M, Expectation.True },
        new object[] { (short)4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsInt32 =
    [
        new object[] { 4, (sbyte)4, Expectation.True },
        new object[] { 4, (short)4, Expectation.True },
        new object[] { 4, 4, Expectation.True },
        new object[] { 4, 4L, Expectation.True },
        new object[] { 4, new Int128(0, 4), Expectation.True },
        new object[] { 4, (byte)4, Expectation.True },
        new object[] { 4, (ushort)4, Expectation.True },
        new object[] { 4, 4U, Expectation.True },
        new object[] { 4, 4UL, Expectation.True },
        new object[] { 4, new UInt128(0, 4), Expectation.True },
        new object[] { 4, (Half)4, Expectation.True },
        new object[] { 4, 4F, Expectation.True },
        new object[] { 4, 4M, Expectation.True },
        new object[] { 4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsInt64 =
    [
        new object[] { 4L, (sbyte)4, Expectation.True },
        new object[] { 4L, (short)4, Expectation.True },
        new object[] { 4L, 4, Expectation.True },
        new object[] { 4L, 4L, Expectation.True },
        new object[] { 4L, new Int128(0, 4), Expectation.True },
        new object[] { 4L, (byte)4, Expectation.True },
        new object[] { 4L, (ushort)4, Expectation.True },
        new object[] { 4L, 4U, Expectation.True },
        new object[] { 4L, 4UL, Expectation.True },
        new object[] { 4L, new UInt128(0, 4), Expectation.True },
        new object[] { 4L, (Half)4, Expectation.True },
        new object[] { 4L, 4F, Expectation.True },
        new object[] { 4L, 4M, Expectation.True },
        new object[] { 4L, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsInt128 =
    [
        new object[] { new Int128(0, 4), (sbyte)4, Expectation.True },
        new object[] { new Int128(0, 4), (short)4, Expectation.True },
        new object[] { new Int128(0, 4), 4, Expectation.True },
        new object[] { new Int128(0, 4), 4L, Expectation.True },
        new object[] { new Int128(0, 4), new Int128(0, 4), Expectation.True },
        new object[] { new Int128(0, 4), (byte)4, Expectation.True },
        new object[] { new Int128(0, 4), (ushort)4, Expectation.True },
        new object[] { new Int128(0, 4), 4U, Expectation.True },
        new object[] { new Int128(0, 4), 4UL, Expectation.True },
        new object[] { new Int128(0, 4), new UInt128(0, 4), Expectation.True },
        new object[] { new Int128(0, 4), (Half)4, Expectation.True },
        new object[] { new Int128(0, 4), 4F, Expectation.True },
        new object[] { new Int128(0, 4), 4M, Expectation.True },
        new object[] { new Int128(0, 4), 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsByte =
    [
        new object[] { (byte)4, (sbyte)4, Expectation.True },
        new object[] { (byte)4, (short)4, Expectation.True },
        new object[] { (byte)4, 4, Expectation.True },
        new object[] { (byte)4, 4L, Expectation.True },
        new object[] { (byte)4, new Int128(0, 4), Expectation.True },
        new object[] { (byte)4, (byte)4, Expectation.True },
        new object[] { (byte)4, (ushort)4, Expectation.True },
        new object[] { (byte)4, 4U, Expectation.True },
        new object[] { (byte)4, 4UL, Expectation.True },
        new object[] { (byte)4, new UInt128(0, 4), Expectation.True },
        new object[] { (byte)4, (Half)4, Expectation.True },
        new object[] { (byte)4, 4F, Expectation.True },
        new object[] { (byte)4, 4M, Expectation.True },
        new object[] { (byte)4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsUInt16 =
    [
        new object[] { (ushort)4, (sbyte)4, Expectation.True },
        new object[] { (ushort)4, (short)4, Expectation.True },
        new object[] { (ushort)4, 4, Expectation.True },
        new object[] { (ushort)4, 4L, Expectation.True },
        new object[] { (ushort)4, new Int128(0, 4), Expectation.True },
        new object[] { (ushort)4, (byte)4, Expectation.True },
        new object[] { (ushort)4, (ushort)4, Expectation.True },
        new object[] { (ushort)4, 4U, Expectation.True },
        new object[] { (ushort)4, 4UL, Expectation.True },
        new object[] { (ushort)4, new UInt128(0, 4), Expectation.True },
        new object[] { (ushort)4, (Half)4, Expectation.True },
        new object[] { (ushort)4, 4F, Expectation.True },
        new object[] { (ushort)4, 4M, Expectation.True },
        new object[] { (ushort)4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsUInt32 =
    [
        new object[] { 4U, (sbyte)4, Expectation.True },
        new object[] { 4U, (short)4, Expectation.True },
        new object[] { 4U, 4, Expectation.True },
        new object[] { 4U, 4L, Expectation.True },
        new object[] { 4U, new Int128(0, 4), Expectation.True },
        new object[] { 4U, (byte)4, Expectation.True },
        new object[] { 4U, (ushort)4, Expectation.True },
        new object[] { 4U, 4U, Expectation.True },
        new object[] { 4U, 4UL, Expectation.True },
        new object[] { 4U, new UInt128(0, 4), Expectation.True },
        new object[] { 4U, (Half)4, Expectation.True },
        new object[] { 4U, 4F, Expectation.True },
        new object[] { 4U, 4M, Expectation.True },
        new object[] { 4U, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsUInt64 =
    [
        new object[] { 4UL, (sbyte)4, Expectation.True },
        new object[] { 4UL, (short)4, Expectation.True },
        new object[] { 4UL, 4, Expectation.True },
        new object[] { 4UL, 4L, Expectation.True },
        new object[] { 4UL, new Int128(0, 4), Expectation.True },
        new object[] { 4UL, (byte)4, Expectation.True },
        new object[] { 4UL, (ushort)4, Expectation.True },
        new object[] { 4UL, 4U, Expectation.True },
        new object[] { 4UL, 4UL, Expectation.True },
        new object[] { 4UL, new UInt128(0, 4), Expectation.True },
        new object[] { 4UL, (Half)4, Expectation.True },
        new object[] { 4UL, 4F, Expectation.True },
        new object[] { 4UL, 4M, Expectation.True },
        new object[] { 4UL, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsUInt128 =
    [
        new object[] { new UInt128(0, 4), (sbyte)4, Expectation.True },
        new object[] { new UInt128(0, 4), (short)4, Expectation.True },
        new object[] { new UInt128(0, 4), 4, Expectation.True },
        new object[] { new UInt128(0, 4), 4L, Expectation.True },
        new object[] { new UInt128(0, 4), new Int128(0, 4), Expectation.True },
        new object[] { new UInt128(0, 4), (byte)4, Expectation.True },
        new object[] { new UInt128(0, 4), (ushort)4, Expectation.True },
        new object[] { new UInt128(0, 4), 4U, Expectation.True },
        new object[] { new UInt128(0, 4), 4UL, Expectation.True },
        new object[] { new UInt128(0, 4), new UInt128(0, 4), Expectation.True },
        new object[] { new UInt128(0, 4), (Half)4, Expectation.True },
        new object[] { new UInt128(0, 4), 4F, Expectation.True },
        new object[] { new UInt128(0, 4), 4M, Expectation.True },
        new object[] { new UInt128(0, 4), 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsHalf =
    [
        new object[] { (Half)4, (sbyte)4, Expectation.True },
        new object[] { (Half)4, (short)4, Expectation.True },
        new object[] { (Half)4, 4, Expectation.True },
        new object[] { (Half)4, 4L, Expectation.True },
        new object[] { (Half)4, new Int128(0, 4), Expectation.True },
        new object[] { (Half)4, (byte)4, Expectation.True },
        new object[] { (Half)4, (ushort)4, Expectation.True },
        new object[] { (Half)4, 4U, Expectation.True },
        new object[] { (Half)4, 4UL, Expectation.True },
        new object[] { (Half)4, new UInt128(0, 4), Expectation.True },
        new object[] { (Half)4, (Half)4, Expectation.True },
        new object[] { (Half)4, 4F, Expectation.True },
        new object[] { (Half)4, 4M, Expectation.True },
        new object[] { (Half)4, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsSingle =
    [
        new object[] { 4F, (sbyte)4, Expectation.True },
        new object[] { 4F, (short)4, Expectation.True },
        new object[] { 4F, 4, Expectation.True },
        new object[] { 4F, 4L, Expectation.True },
        new object[] { 4F, new Int128(0, 4), Expectation.True },
        new object[] { 4F, (byte)4, Expectation.True },
        new object[] { 4F, (ushort)4, Expectation.True },
        new object[] { 4F, 4U, Expectation.True },
        new object[] { 4F, 4UL, Expectation.True },
        new object[] { 4F, new UInt128(0, 4), Expectation.True },
        new object[] { 4F, (Half)4, Expectation.True },
        new object[] { 4F, 4F, Expectation.True },
        new object[] { 4F, 4M, Expectation.True },
        new object[] { 4F, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsDecimal =
    [
        new object[] { 4M, (sbyte)4, Expectation.True },
        new object[] { 4M, (short)4, Expectation.True },
        new object[] { 4M, 4, Expectation.True },
        new object[] { 4M, 4L, Expectation.True },
        new object[] { 4M, new Int128(0, 4), Expectation.True },
        new object[] { 4M, (byte)4, Expectation.True },
        new object[] { 4M, (ushort)4, Expectation.True },
        new object[] { 4M, 4U, Expectation.True },
        new object[] { 4M, 4UL, Expectation.True },
        new object[] { 4M, new UInt128(0, 4), Expectation.True },
        new object[] { 4M, (Half)4, Expectation.True },
        new object[] { 4M, 4F, Expectation.True },
        new object[] { 4M, 4M, Expectation.True },
        new object[] { 4M, 4D, Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] EqualsExpectationsDouble =
    [
        new object[] { 4D, (sbyte)4, Expectation.True },
        new object[] { 4D, (short)4, Expectation.True },
        new object[] { 4D, 4, Expectation.True },
        new object[] { 4D, 4L, Expectation.True },
        new object[] { 4D, new Int128(0, 4), Expectation.True },
        new object[] { 4D, (byte)4, Expectation.True },
        new object[] { 4D, (ushort)4, Expectation.True },
        new object[] { 4D, 4U, Expectation.True },
        new object[] { 4D, 4UL, Expectation.True },
        new object[] { 4D, new UInt128(0, 4), Expectation.True },
        new object[] { 4D, (Half)4, Expectation.True },
        new object[] { 4D, 4F, Expectation.True },
        new object[] { 4D, 4M, Expectation.True },
        new object[] { 4D, 4D, Expectation.True },
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

    [TestCaseSource(nameof(LessThanExpectationsSByte))]
    [TestCaseSource(nameof(LessThanExpectationsInt16))]
    [TestCaseSource(nameof(LessThanExpectationsInt32))]
    [TestCaseSource(nameof(LessThanExpectationsInt64))]
    [TestCaseSource(nameof(LessThanExpectationsInt128))]
    [TestCaseSource(nameof(LessThanExpectationsByte))]
    [TestCaseSource(nameof(LessThanExpectationsUInt16))]
    [TestCaseSource(nameof(LessThanExpectationsUInt32))]
    [TestCaseSource(nameof(LessThanExpectationsUInt64))]
    [TestCaseSource(nameof(LessThanExpectationsUInt128))]
    [TestCaseSource(nameof(LessThanExpectationsHalf))]
    [TestCaseSource(nameof(LessThanExpectationsSingle))]
    [TestCaseSource(nameof(LessThanExpectationsDecimal))]
    [TestCaseSource(nameof(LessThanExpectationsDouble))]
    public void ValuesCompare(object lhs, object rhs, Expectation expected)
    {
        BinaryJsonNumber number1 = GetBinaryJsonNumberFor(lhs);
        BinaryJsonNumber number2 = GetBinaryJsonNumberFor(rhs);

        switch (expected)
        {
            case Expectation.False:
                Assert.IsFalse(number1 <= number2);
                Assert.IsTrue(number1 >= number2);
                break;
            case Expectation.True:
                Assert.IsTrue(number1 <= number2);
                Assert.IsFalse(number1 >= number2);
                break;
            case Expectation.Exception:
                Assert.Catch(() => _ = number1 <= number2);
                Assert.Catch(() => _ = number1 >= number2);
                break;
        }
    }

    [TestCaseSource(nameof(EqualsExpectationsSByte))]
    [TestCaseSource(nameof(EqualsExpectationsInt16))]
    [TestCaseSource(nameof(EqualsExpectationsInt32))]
    [TestCaseSource(nameof(EqualsExpectationsInt64))]
    [TestCaseSource(nameof(EqualsExpectationsInt128))]
    [TestCaseSource(nameof(EqualsExpectationsByte))]
    [TestCaseSource(nameof(EqualsExpectationsUInt16))]
    [TestCaseSource(nameof(EqualsExpectationsUInt32))]
    [TestCaseSource(nameof(EqualsExpectationsUInt64))]
    [TestCaseSource(nameof(EqualsExpectationsUInt128))]
    [TestCaseSource(nameof(EqualsExpectationsHalf))]
    [TestCaseSource(nameof(EqualsExpectationsSingle))]
    [TestCaseSource(nameof(EqualsExpectationsDecimal))]
    [TestCaseSource(nameof(EqualsExpectationsDouble))]
    public void ValuesCompareEquals(object lhs, object rhs, Expectation expected)
    {
        BinaryJsonNumber number1 = GetBinaryJsonNumberFor(lhs);
        BinaryJsonNumber number2 = GetBinaryJsonNumberFor(rhs);

        switch (expected)
        {
            case Expectation.False:
                Assert.IsFalse(number1 <= number2);
                Assert.IsFalse(number1 >= number2);
                break;
            case Expectation.True:
                Assert.IsTrue(number1 <= number2);
                Assert.IsTrue(number1 >= number2);
                break;
            case Expectation.Exception:
                Assert.Catch(() => _ = number1 <= number2);
                Assert.Catch(() => _ = number1 >= number2);
                break;
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
}