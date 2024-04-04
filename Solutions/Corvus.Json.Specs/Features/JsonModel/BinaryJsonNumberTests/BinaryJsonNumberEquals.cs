// <copyright file="BinaryJsonNumberEquals.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
internal class BinaryJsonNumberEquals
{
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsSByte =
    [
        new object[] { (sbyte)3, (sbyte)4, Expectation.False },
        new object[] { (sbyte)3, (short)4, Expectation.False },
        new object[] { (sbyte)3, 4, Expectation.False },
        new object[] { (sbyte)3, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)3, new Int128(0, 4), Expectation.False },
#endif
        new object[] { (sbyte)3, (byte)4, Expectation.False },
        new object[] { (sbyte)3, (ushort)4, Expectation.False },
        new object[] { (sbyte)3, 4U, Expectation.False },
        new object[] { (sbyte)3, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)3, new UInt128(0, 4), Expectation.False },
        new object[] { (sbyte)3, (Half)4, Expectation.False },
#endif
        new object[] { (sbyte)3, 4F, Expectation.False },
        new object[] { (sbyte)3, 4M, Expectation.False },
        new object[] { (sbyte)3, 4D, Expectation.False },
        new object[] { (sbyte)3, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsInt16 =
    [
        new object[] { (short)3, (sbyte)4, Expectation.False },
        new object[] { (short)3, (short)4, Expectation.False },
        new object[] { (short)3, 4, Expectation.False },
        new object[] { (short)3, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (short)3, new Int128(0, 4), Expectation.False },
#endif
        new object[] { (short)3, (byte)4, Expectation.False },
        new object[] { (short)3, (ushort)4, Expectation.False },
        new object[] { (short)3, 4U, Expectation.False },
        new object[] { (short)3, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (short)3, new UInt128(0, 4), Expectation.False },
        new object[] { (short)3, (Half)4, Expectation.False },
#endif
        new object[] { (short)3, 4F, Expectation.False },
        new object[] { (short)3, 4M, Expectation.False },
        new object[] { (short)3, 4D, Expectation.False },
        new object[] { (short)3, GetJsonElement(4), Expectation.False },

    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsInt32 =
    [
        new object[] { 3, (sbyte)4, Expectation.False },
        new object[] { 3, (short)4, Expectation.False },
        new object[] { 3, 4, Expectation.False },
        new object[] { 3, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3, (byte)4, Expectation.False },
        new object[] { 3, (ushort)4, Expectation.False },
        new object[] { 3, 4U, Expectation.False },
        new object[] { 3, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3, new UInt128(0, 4), Expectation.False },
        new object[] { 3, (Half)4, Expectation.False },
#endif
        new object[] { 3, 4F, Expectation.False },
        new object[] { 3, 4M, Expectation.False },
        new object[] { 3, 4D, Expectation.False },
        new object[] { 3, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsInt64 =
    [
        new object[] { 3L, (sbyte)4, Expectation.False },
        new object[] { 3L, (short)4, Expectation.False },
        new object[] { 3L, 4, Expectation.False },
        new object[] { 3L, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3L, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3L, (byte)4, Expectation.False },
        new object[] { 3L, (ushort)4, Expectation.False },
        new object[] { 3L, 4U, Expectation.False },
        new object[] { 3L, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3L, new UInt128(0, 4), Expectation.False },
        new object[] { 3L, (Half)4, Expectation.False },
#endif
        new object[] { 3L, 4F, Expectation.False },
        new object[] { 3L, 4M, Expectation.False },
        new object[] { 3L, 4D, Expectation.False },
        new object[] { 3L, GetJsonElement(4), Expectation.False },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsInt128 =
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
        new object[] { new Int128(0, 3), GetJsonElement(4), Expectation.False },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsByte =
    [
        new object[] { (byte)3, (sbyte)4, Expectation.False },
        new object[] { (byte)3, (short)4, Expectation.False },
        new object[] { (byte)3, 4, Expectation.False },
        new object[] { (byte)3, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (byte)3, new Int128(0, 4), Expectation.False },
#endif
        new object[] { (byte)3, (byte)4, Expectation.False },
        new object[] { (byte)3, (ushort)4, Expectation.False },
        new object[] { (byte)3, 4U, Expectation.False },
        new object[] { (byte)3, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (byte)3, new UInt128(0, 4), Expectation.False },
        new object[] { (byte)3, (Half)4, Expectation.False },
#endif
        new object[] { (byte)3, 4F, Expectation.False },
        new object[] { (byte)3, 4M, Expectation.False },
        new object[] { (byte)3, 4D, Expectation.False },
        new object[] { (byte)3, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsUInt16 =
    [
        new object[] { (ushort)3, (sbyte)4, Expectation.False },
        new object[] { (ushort)3, (short)4, Expectation.False },
        new object[] { (ushort)3, 4, Expectation.False },
        new object[] { (ushort)3, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (ushort)3, new Int128(0, 4), Expectation.False },
#endif
        new object[] { (ushort)3, (byte)4, Expectation.False },
        new object[] { (ushort)3, (ushort)4, Expectation.False },
        new object[] { (ushort)3, 4U, Expectation.False },
        new object[] { (ushort)3, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { (ushort)3, new UInt128(0, 4), Expectation.False },
        new object[] { (ushort)3, (Half)4, Expectation.False },
#endif
        new object[] { (ushort)3, 4F, Expectation.False },
        new object[] { (ushort)3, 4M, Expectation.False },
        new object[] { (ushort)3, 4D, Expectation.False },
        new object[] { (ushort)3, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsUInt32 =
    [
        new object[] { 3U, (sbyte)4, Expectation.False },
        new object[] { 3U, (short)4, Expectation.False },
        new object[] { 3U, 4, Expectation.False },
        new object[] { 3U, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3U, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3U, (byte)4, Expectation.False },
        new object[] { 3U, (ushort)4, Expectation.False },
        new object[] { 3U, 4U, Expectation.False },
        new object[] { 3U, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3U, new UInt128(0, 4), Expectation.False },
        new object[] { 3U, (Half)4, Expectation.False },
#endif
        new object[] { 3U, 4F, Expectation.False },
        new object[] { 3U, 4M, Expectation.False },
        new object[] { 3U, 4D, Expectation.False },
        new object[] { 3U, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsUInt64 =
    [
        new object[] { 3UL, (sbyte)4, Expectation.False },
        new object[] { 3UL, (short)4, Expectation.False },
        new object[] { 3UL, 4, Expectation.False },
        new object[] { 3UL, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3UL, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3UL, (byte)4, Expectation.False },
        new object[] { 3UL, (ushort)4, Expectation.False },
        new object[] { 3UL, 4U, Expectation.False },
        new object[] { 3UL, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3UL, new UInt128(0, 4), Expectation.False },
        new object[] { 3UL, (Half)4, Expectation.False },
#endif
        new object[] { 3UL, 4F, Expectation.False },
        new object[] { 3UL, 4M, Expectation.False },
        new object[] { 3UL, 4D, Expectation.False },
        new object[] { 3UL, GetJsonElement(4), Expectation.False },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsUInt128 =
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
        new object[] { new UInt128(0, 3), GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsHalf =
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
        new object[] { (Half)3, GetJsonElement(4), Expectation.False },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsSingle =
    [
        new object[] { 3F, (sbyte)4, Expectation.False },
        new object[] { 3F, (short)4, Expectation.False },
        new object[] { 3F, 4, Expectation.False },
        new object[] { 3F, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3F, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3F, (byte)4, Expectation.False },
        new object[] { 3F, (ushort)4, Expectation.False },
        new object[] { 3F, 4U, Expectation.False },
        new object[] { 3F, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3F, new UInt128(0, 4), Expectation.False },
        new object[] { 3F, (Half)4, Expectation.False },
#endif
        new object[] { 3F, 4F, Expectation.False },
        new object[] { 3F, 4M, Expectation.False },
        new object[] { 3F, 4D, Expectation.False },
        new object[] { 3F, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsDecimal =
    [
        new object[] { 3M, (sbyte)4, Expectation.False },
        new object[] { 3M, (short)4, Expectation.False },
        new object[] { 3M, 4, Expectation.False },
        new object[] { 3M, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3M, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3M, (byte)4, Expectation.False },
        new object[] { 3M, (ushort)4, Expectation.False },
        new object[] { 3M, 4U, Expectation.False },
        new object[] { 3M, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3M, new UInt128(0, 4), Expectation.False },
        new object[] { 3M, (Half)4, Expectation.False },
#endif
        new object[] { 3M, 4F, Expectation.False },
        new object[] { 3M, 4M, Expectation.False },
        new object[] { 3M, 4D, Expectation.False },
        new object[] { 3M, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] DifferentNotEqualsExpectationsDouble =
    [
        new object[] { 3D, (sbyte)4, Expectation.False },
        new object[] { 3D, (short)4, Expectation.False },
        new object[] { 3D, 4, Expectation.False },
        new object[] { 3D, 4L, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3D, new Int128(0, 4), Expectation.False },
#endif
        new object[] { 3D, (byte)4, Expectation.False },
        new object[] { 3D, (ushort)4, Expectation.False },
        new object[] { 3D, 4U, Expectation.False },
        new object[] { 3D, 4UL, Expectation.False },
#if NET8_0_OR_GREATER
        new object[] { 3D, new UInt128(0, 4), Expectation.False },
        new object[] { 3D, (Half)4, Expectation.False },
#endif
        new object[] { 3D, 4F, Expectation.False },
        new object[] { 3D, 4M, Expectation.False },
        new object[] { 3D, 4D, Expectation.False },
        new object[] { 3D, GetJsonElement(4), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsSByte =
    [
        new object[] { (sbyte)4, (sbyte)4, Expectation.True },
        new object[] { (sbyte)4, (short)4, Expectation.True },
        new object[] { (sbyte)4, 4, Expectation.True },
        new object[] { (sbyte)4, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)4, new Int128(0, 4), Expectation.True },
#endif
        new object[] { (sbyte)4, (byte)4, Expectation.True },
        new object[] { (sbyte)4, (ushort)4, Expectation.True },
        new object[] { (sbyte)4, 4U, Expectation.True },
        new object[] { (sbyte)4, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)4, new UInt128(0, 4), Expectation.True },
        new object[] { (sbyte)4, (Half)4, Expectation.True },
#endif
        new object[] { (sbyte)4, 4F, Expectation.True },
        new object[] { (sbyte)4, 4M, Expectation.True },
        new object[] { (sbyte)4, 4D, Expectation.True },
        new object[] { (sbyte)4, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsInt16 =
    [
        new object[] { (short)4, (sbyte)4, Expectation.True },
        new object[] { (short)4, (short)4, Expectation.True },
        new object[] { (short)4, 4, Expectation.True },
        new object[] { (short)4, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (short)4, new Int128(0, 4), Expectation.True },
#endif
        new object[] { (short)4, (byte)4, Expectation.True },
        new object[] { (short)4, (ushort)4, Expectation.True },
        new object[] { (short)4, 4U, Expectation.True },
        new object[] { (short)4, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (short)4, new UInt128(0, 4), Expectation.True },
        new object[] { (short)4, (Half)4, Expectation.True },
#endif
        new object[] { (short)4, 4F, Expectation.True },
        new object[] { (short)4, 4M, Expectation.True },
        new object[] { (short)4, 4D, Expectation.True },
        new object[] { (short)4, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsInt32 =
    [
        new object[] { 4, (sbyte)4, Expectation.True },
        new object[] { 4, (short)4, Expectation.True },
        new object[] { 4, 4, Expectation.True },
        new object[] { 4, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4, (byte)4, Expectation.True },
        new object[] { 4, (ushort)4, Expectation.True },
        new object[] { 4, 4U, Expectation.True },
        new object[] { 4, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4, new UInt128(0, 4), Expectation.True },
        new object[] { 4, (Half)4, Expectation.True },
#endif
        new object[] { 4, 4F, Expectation.True },
        new object[] { 4, 4M, Expectation.True },
        new object[] { 4, 4D, Expectation.True },
        new object[] { 4, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsInt64 =
    [
        new object[] { 4L, (sbyte)4, Expectation.True },
        new object[] { 4L, (short)4, Expectation.True },
        new object[] { 4L, 4, Expectation.True },
        new object[] { 4L, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4L, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4L, (byte)4, Expectation.True },
        new object[] { 4L, (ushort)4, Expectation.True },
        new object[] { 4L, 4U, Expectation.True },
        new object[] { 4L, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4L, new UInt128(0, 4), Expectation.True },
        new object[] { 4L, (Half)4, Expectation.True },
#endif
        new object[] { 4L, 4F, Expectation.True },
        new object[] { 4L, 4M, Expectation.True },
        new object[] { 4L, 4D, Expectation.True },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsInt128 =
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
        new object[] { new Int128(0, 4), GetJsonElement(4), Expectation.True },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsByte =
    [
        new object[] { (byte)4, (sbyte)4, Expectation.True },
        new object[] { (byte)4, (short)4, Expectation.True },
        new object[] { (byte)4, 4, Expectation.True },
        new object[] { (byte)4, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (byte)4, new Int128(0, 4), Expectation.True },
#endif
        new object[] { (byte)4, (byte)4, Expectation.True },
        new object[] { (byte)4, (ushort)4, Expectation.True },
        new object[] { (byte)4, 4U, Expectation.True },
        new object[] { (byte)4, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (byte)4, new UInt128(0, 4), Expectation.True },
        new object[] { (byte)4, (Half)4, Expectation.True },
#endif
        new object[] { (byte)4, 4F, Expectation.True },
        new object[] { (byte)4, 4M, Expectation.True },
        new object[] { (byte)4, 4D, Expectation.True },
        new object[] { (byte)4, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsUInt16 =
    [
        new object[] { (ushort)4, (sbyte)4, Expectation.True },
        new object[] { (ushort)4, (short)4, Expectation.True },
        new object[] { (ushort)4, 4, Expectation.True },
        new object[] { (ushort)4, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (ushort)4, new Int128(0, 4), Expectation.True },
#endif
        new object[] { (ushort)4, (byte)4, Expectation.True },
        new object[] { (ushort)4, (ushort)4, Expectation.True },
        new object[] { (ushort)4, 4U, Expectation.True },
        new object[] { (ushort)4, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (ushort)4, new UInt128(0, 4), Expectation.True },
        new object[] { (ushort)4, (Half)4, Expectation.True },
#endif
        new object[] { (ushort)4, 4F, Expectation.True },
        new object[] { (ushort)4, 4M, Expectation.True },
        new object[] { (ushort)4, 4D, Expectation.True },
        new object[] { (ushort)4, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsUInt32 =
    [
        new object[] { 4U, (sbyte)4, Expectation.True },
        new object[] { 4U, (short)4, Expectation.True },
        new object[] { 4U, 4, Expectation.True },
        new object[] { 4U, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4U, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4U, (byte)4, Expectation.True },
        new object[] { 4U, (ushort)4, Expectation.True },
        new object[] { 4U, 4U, Expectation.True },
        new object[] { 4U, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4U, new UInt128(0, 4), Expectation.True },
        new object[] { 4U, (Half)4, Expectation.True },
#endif
        new object[] { 4U, 4F, Expectation.True },
        new object[] { 4U, 4M, Expectation.True },
        new object[] { 4U, 4D, Expectation.True },
        new object[] { 4U, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsUInt64 =
    [
        new object[] { 4UL, (sbyte)4, Expectation.True },
        new object[] { 4UL, (short)4, Expectation.True },
        new object[] { 4UL, 4, Expectation.True },
        new object[] { 4UL, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4UL, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4UL, (byte)4, Expectation.True },
        new object[] { 4UL, (ushort)4, Expectation.True },
        new object[] { 4UL, 4U, Expectation.True },
        new object[] { 4UL, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4UL, new UInt128(0, 4), Expectation.True },
        new object[] { 4UL, (Half)4, Expectation.True },
#endif
        new object[] { 4UL, 4F, Expectation.True },
        new object[] { 4UL, 4M, Expectation.True },
        new object[] { 4UL, 4D, Expectation.True },
        new object[] { 4UL, GetJsonElement(4), Expectation.True },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsUInt128 =
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
        new object[] { new UInt128(0, 4), GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsHalf =
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
        new object[] { (Half)4, GetJsonElement(4), Expectation.True },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsSingle =
    [
        new object[] { 4F, (sbyte)4, Expectation.True },
        new object[] { 4F, (short)4, Expectation.True },
        new object[] { 4F, 4, Expectation.True },
        new object[] { 4F, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4F, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4F, (byte)4, Expectation.True },
        new object[] { 4F, (ushort)4, Expectation.True },
        new object[] { 4F, 4U, Expectation.True },
        new object[] { 4F, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4F, new UInt128(0, 4), Expectation.True },
        new object[] { 4F, (Half)4, Expectation.True },
#endif
        new object[] { 4F, 4F, Expectation.True },
        new object[] { 4F, 4M, Expectation.True },
        new object[] { 4F, 4D, Expectation.True },
        new object[] { 4F, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsDecimal =
    [
        new object[] { 4M, (sbyte)4, Expectation.True },
        new object[] { 4M, (short)4, Expectation.True },
        new object[] { 4M, 4, Expectation.True },
        new object[] { 4M, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4M, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4M, (byte)4, Expectation.True },
        new object[] { 4M, (ushort)4, Expectation.True },
        new object[] { 4M, 4U, Expectation.True },
        new object[] { 4M, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4M, new UInt128(0, 4), Expectation.True },
        new object[] { 4M, (Half)4, Expectation.True },
#endif
        new object[] { 4M, 4F, Expectation.True },
        new object[] { 4M, 4M, Expectation.True },
        new object[] { 4M, 4D, Expectation.True },
        new object[] { 4M, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] SameEqualsExpectationsDouble =
    [
        new object[] { 4D, (sbyte)4, Expectation.True },
        new object[] { 4D, (short)4, Expectation.True },
        new object[] { 4D, 4, Expectation.True },
        new object[] { 4D, 4L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4D, new Int128(0, 4), Expectation.True },
#endif
        new object[] { 4D, (byte)4, Expectation.True },
        new object[] { 4D, (ushort)4, Expectation.True },
        new object[] { 4D, 4U, Expectation.True },
        new object[] { 4D, 4UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 4D, new UInt128(0, 4), Expectation.True },
        new object[] { 4D, (Half)4, Expectation.True },
#endif
        new object[] { 4D, 4F, Expectation.True },
        new object[] { 4D, 4M, Expectation.True },
        new object[] { 4D, 4D, Expectation.True },
        new object[] { 4D, GetJsonElement(4), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] AwkwardDecimalExpectations =
    [
        new object[] { 1234567890.1234567891M, 1E29, Expectation.Exception },
        new object[] { 1E29, 1234567890.1234567891M, Expectation.Exception },
        new object[] { 1234567890.1234567891M, 1E29D, Expectation.Exception },
        new object[] { 1E29D, 1234567890.1234567891M, Expectation.Exception },

        new object[] { 1234567890.1234567891M, GetJsonElement(1E29), Expectation.Exception },

        // Because of https://github.com/dotnet/runtime/issues/92631, the JSON element succeeds in converting to double
        new object[] { 1E29, GetJsonElement(1234567890.1234567891M), Expectation.False },
        new object[] { 1234567890.1234567891M, GetJsonElement(1234567890.1234567891M), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] AwkwardFloatExpectations =
    [
#if NET8_0_OR_GREATER
        new object[] { 0.3F, (Half)0.3F, Expectation.False },
#endif
        new object[] { 0.3F, 0.3F, Expectation.True },
        new object[] { 0.3F, 0.3M, Expectation.True },
        new object[] { 0.3F, 0.3D, Expectation.False },
        new object[] { 0.3F, GetJsonElement(0.3F), Expectation.False }, // These are false because JsonElement behaves as a double even if it was created via a float; it can't tell.
        new object[] { 0.3F, GetJsonElement(0.3M), Expectation.False },
        new object[] { 0.3F, GetJsonElement(0.3D), Expectation.False },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] AwkwardDoubleExpectations =
    [
#if NET8_0_OR_GREATER
        new object[] { 0.3D, (Half)0.3F, Expectation.False },
#endif
        new object[] { 0.3D, 0.3F, Expectation.False },
        new object[] { 0.3D, 0.3M, Expectation.True },
        new object[] { 0.3D, 0.3D, Expectation.True },
        new object[] { 0.3D, GetJsonElement(0.3F), Expectation.True }, // These are true because JsonElement behaves as a double even if it was created via a float; it can't tell.
        new object[] { 0.3D, GetJsonElement(0.3M), Expectation.True },
        new object[] { 0.3D, GetJsonElement(0.3D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsSByte =
    [
        new object[] { (sbyte)0, (sbyte)0, Expectation.True },
        new object[] { (sbyte)0, (short)0, Expectation.True },
        new object[] { (sbyte)0, 0, Expectation.True },
        new object[] { (sbyte)0, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)0, Int128.Zero, Expectation.True },
#endif
        new object[] { (sbyte)0, (byte)0, Expectation.True },
        new object[] { (sbyte)0, (ushort)0, Expectation.True },
        new object[] { (sbyte)0, 0U, Expectation.True },
        new object[] { (sbyte)0, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (sbyte)0, UInt128.Zero, Expectation.True },
        new object[] { (sbyte)0, Half.Zero, Expectation.True },
#endif
        new object[] { (sbyte)0, 0F, Expectation.True },
        new object[] { (sbyte)0, 0M, Expectation.True },
        new object[] { (sbyte)0, 0D, Expectation.True },
        new object[] { (sbyte)0, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsInt16 =
    [
        new object[] { (short)0, (sbyte)0, Expectation.True },
        new object[] { (short)0, (short)0, Expectation.True },
        new object[] { (short)0, 0, Expectation.True },
        new object[] { (short)0, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (short)0, Int128.Zero, Expectation.True },
#endif
        new object[] { (short)0, (byte)0, Expectation.True },
        new object[] { (short)0, (ushort)0, Expectation.True },
        new object[] { (short)0, 0U, Expectation.True },
        new object[] { (short)0, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (short)0, UInt128.Zero, Expectation.True },
        new object[] { (short)0, Half.Zero, Expectation.True },
#endif
        new object[] { (short)0, 0F, Expectation.True },
        new object[] { (short)0, 0M, Expectation.True },
        new object[] { (short)0, 0D, Expectation.True },
        new object[] { (short)0, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsInt32 =
    [
        new object[] { 0, (sbyte)0, Expectation.True },
        new object[] { 0, (short)0, Expectation.True },
        new object[] { 0, 0, Expectation.True },
        new object[] { 0, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0, Int128.Zero, Expectation.True },
#endif
        new object[] { 0, (byte)0, Expectation.True },
        new object[] { 0, (ushort)0, Expectation.True },
        new object[] { 0, 0U, Expectation.True },
        new object[] { 0, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0, UInt128.Zero, Expectation.True },
        new object[] { 0, Half.Zero, Expectation.True },
#endif
        new object[] { 0, 0F, Expectation.True },
        new object[] { 0, 0M, Expectation.True },
        new object[] { 0, 0D, Expectation.True },
        new object[] { 0, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsInt64 =
    [
        new object[] { 0L, (sbyte)0, Expectation.True },
        new object[] { 0L, (short)0, Expectation.True },
        new object[] { 0L, 0, Expectation.True },
        new object[] { 0L, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0L, Int128.Zero, Expectation.True },
#endif
        new object[] { 0L, (byte)0, Expectation.True },
        new object[] { 0L, (ushort)0, Expectation.True },
        new object[] { 0L, 0U, Expectation.True },
        new object[] { 0L, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0L, UInt128.Zero, Expectation.True },
        new object[] { 0L, Half.Zero, Expectation.True },
#endif
        new object[] { 0L, 0F, Expectation.True },
        new object[] { 0L, 0M, Expectation.True },
        new object[] { 0L, 0D, Expectation.True },
        new object[] { 0L, GetJsonElement(0D), Expectation.True },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsInt128 =
    [
        new object[] { Int128.Zero, (sbyte)0, Expectation.True },
        new object[] { Int128.Zero, (short)0, Expectation.True },
        new object[] { Int128.Zero, 0, Expectation.True },
        new object[] { Int128.Zero, 0L, Expectation.True },
        new object[] { Int128.Zero, Int128.Zero, Expectation.True },
        new object[] { Int128.Zero, (byte)0, Expectation.True },
        new object[] { Int128.Zero, (ushort)0, Expectation.True },
        new object[] { Int128.Zero, 0U, Expectation.True },
        new object[] { Int128.Zero, 0UL, Expectation.True },
        new object[] { Int128.Zero, UInt128.Zero, Expectation.True },
        new object[] { Int128.Zero, Half.Zero, Expectation.True },
        new object[] { Int128.Zero, 0F, Expectation.True },
        new object[] { Int128.Zero, 0M, Expectation.True },
        new object[] { Int128.Zero, 0D, Expectation.True },
        new object[] { Int128.Zero, GetJsonElement(0D), Expectation.True },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsByte =
    [
        new object[] { (byte)0, (sbyte)0, Expectation.True },
        new object[] { (byte)0, (short)0, Expectation.True },
        new object[] { (byte)0, 0, Expectation.True },
        new object[] { (byte)0, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (byte)0, Int128.Zero, Expectation.True },
#endif
        new object[] { (byte)0, (byte)0, Expectation.True },
        new object[] { (byte)0, (ushort)0, Expectation.True },
        new object[] { (byte)0, 0U, Expectation.True },
        new object[] { (byte)0, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (byte)0, UInt128.Zero, Expectation.True },
        new object[] { (byte)0, Half.Zero, Expectation.True },
#endif
        new object[] { (byte)0, 0F, Expectation.True },
        new object[] { (byte)0, 0M, Expectation.True },
        new object[] { (byte)0, 0D, Expectation.True },
        new object[] { (byte)0, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsUInt16 =
    [
        new object[] { (ushort)0, (sbyte)0, Expectation.True },
        new object[] { (ushort)0, (short)0, Expectation.True },
        new object[] { (ushort)0, 0, Expectation.True },
        new object[] { (ushort)0, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (ushort)0, Int128.Zero, Expectation.True },
#endif
        new object[] { (ushort)0, (byte)0, Expectation.True },
        new object[] { (ushort)0, (ushort)0, Expectation.True },
        new object[] { (ushort)0, 0U, Expectation.True },
        new object[] { (ushort)0, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { (ushort)0, UInt128.Zero, Expectation.True },
        new object[] { (ushort)0, Half.Zero, Expectation.True },
#endif
        new object[] { (ushort)0, 0F, Expectation.True },
        new object[] { (ushort)0, 0M, Expectation.True },
        new object[] { (ushort)0, 0D, Expectation.True },
        new object[] { (ushort)0, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsUInt32 =
    [
        new object[] { 0U, (sbyte)0, Expectation.True },
        new object[] { 0U, (short)0, Expectation.True },
        new object[] { 0U, 0, Expectation.True },
        new object[] { 0U, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0U, Int128.Zero, Expectation.True },
#endif
        new object[] { 0U, (byte)0, Expectation.True },
        new object[] { 0U, (ushort)0, Expectation.True },
        new object[] { 0U, 0U, Expectation.True },
        new object[] { 0U, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0U, UInt128.Zero, Expectation.True },
        new object[] { 0U, Half.Zero, Expectation.True },
#endif
        new object[] { 0U, 0F, Expectation.True },
        new object[] { 0U, 0M, Expectation.True },
        new object[] { 0U, 0D, Expectation.True },
        new object[] { 0U, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsUInt64 =
    [
        new object[] { 0UL, (sbyte)0, Expectation.True },
        new object[] { 0UL, (short)0, Expectation.True },
        new object[] { 0UL, 0, Expectation.True },
        new object[] { 0UL, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0UL, Int128.Zero, Expectation.True },
#endif
        new object[] { 0UL, (byte)0, Expectation.True },
        new object[] { 0UL, (ushort)0, Expectation.True },
        new object[] { 0UL, 0U, Expectation.True },
        new object[] { 0UL, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0UL, UInt128.Zero, Expectation.True },
        new object[] { 0UL, Half.Zero, Expectation.True },
#endif
        new object[] { 0UL, 0F, Expectation.True },
        new object[] { 0UL, 0M, Expectation.True },
        new object[] { 0UL, 0D, Expectation.True },
        new object[] { 0UL, GetJsonElement(0D), Expectation.True },
    ];

#if NET8_0_OR_GREATER
    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsUInt128 =
    [
        new object[] { UInt128.Zero, (sbyte)0, Expectation.True },
        new object[] { UInt128.Zero, (short)0, Expectation.True },
        new object[] { UInt128.Zero, 0, Expectation.True },
        new object[] { UInt128.Zero, 0L, Expectation.True },
        new object[] { UInt128.Zero, Int128.Zero, Expectation.True },
        new object[] { UInt128.Zero, (byte)0, Expectation.True },
        new object[] { UInt128.Zero, (ushort)0, Expectation.True },
        new object[] { UInt128.Zero, 0U, Expectation.True },
        new object[] { UInt128.Zero, 0UL, Expectation.True },
        new object[] { UInt128.Zero, UInt128.Zero, Expectation.True },
        new object[] { UInt128.Zero, Half.Zero, Expectation.True },
        new object[] { UInt128.Zero, 0F, Expectation.True },
        new object[] { UInt128.Zero, 0M, Expectation.True },
        new object[] { UInt128.Zero, 0D, Expectation.True },
        new object[] { UInt128.Zero, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsHalf =
    [
        new object[] { Half.Zero, (sbyte)0, Expectation.True },
        new object[] { Half.Zero, (short)0, Expectation.True },
        new object[] { Half.Zero, 0, Expectation.True },
        new object[] { Half.Zero, 0L, Expectation.True },
        new object[] { Half.Zero, Int128.Zero, Expectation.True },
        new object[] { Half.Zero, (byte)0, Expectation.True },
        new object[] { Half.Zero, (ushort)0, Expectation.True },
        new object[] { Half.Zero, 0U, Expectation.True },
        new object[] { Half.Zero, 0UL, Expectation.True },
        new object[] { Half.Zero, UInt128.Zero, Expectation.True },
        new object[] { Half.Zero, Half.Zero, Expectation.True },
        new object[] { Half.Zero, 0F, Expectation.True },
        new object[] { Half.Zero, 0M, Expectation.True },
        new object[] { Half.Zero, 0D, Expectation.True },
        new object[] { Half.Zero, GetJsonElement(0D), Expectation.True },
    ];
#endif

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsSingle =
    [
        new object[] { 0F, (sbyte)0, Expectation.True },
        new object[] { 0F, (short)0, Expectation.True },
        new object[] { 0F, 0, Expectation.True },
        new object[] { 0F, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0F, Int128.Zero, Expectation.True },
#endif
        new object[] { 0F, (byte)0, Expectation.True },
        new object[] { 0F, (ushort)0, Expectation.True },
        new object[] { 0F, 0U, Expectation.True },
        new object[] { 0F, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0F, UInt128.Zero, Expectation.True },
        new object[] { 0F, Half.Zero, Expectation.True },
#endif
        new object[] { 0F, 0F, Expectation.True },
        new object[] { 0F, 0M, Expectation.True },
        new object[] { 0F, 0D, Expectation.True },
        new object[] { 0F, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsDecimal =
    [
        new object[] { 0M, (sbyte)0, Expectation.True },
        new object[] { 0M, (short)0, Expectation.True },
        new object[] { 0M, 0, Expectation.True },
        new object[] { 0M, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0M, Int128.Zero, Expectation.True },
#endif
        new object[] { 0M, (byte)0, Expectation.True },
        new object[] { 0M, (ushort)0, Expectation.True },
        new object[] { 0M, 0U, Expectation.True },
        new object[] { 0M, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0M, UInt128.Zero, Expectation.True },
        new object[] { 0M, Half.Zero, Expectation.True },
#endif
        new object[] { 0M, 0F, Expectation.True },
        new object[] { 0M, 0M, Expectation.True },
        new object[] { 0M, 0D, Expectation.True },
        new object[] { 0M, GetJsonElement(0D), Expectation.True },
    ];

    /// <summary>
    /// Expectations for Equals.
    /// </summary>
    public static readonly object[] ZeroEqualsExpectationsDouble =
    [
        new object[] { 0D, (sbyte)0, Expectation.True },
        new object[] { 0D, (short)0, Expectation.True },
        new object[] { 0D, 0, Expectation.True },
        new object[] { 0D, 0L, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0D, Int128.Zero, Expectation.True },
#endif
        new object[] { 0D, (byte)0, Expectation.True },
        new object[] { 0D, (ushort)0, Expectation.True },
        new object[] { 0D, 0U, Expectation.True },
        new object[] { 0D, 0UL, Expectation.True },
#if NET8_0_OR_GREATER
        new object[] { 0D, UInt128.Zero, Expectation.True },
        new object[] { 0D, Half.Zero, Expectation.True },
#endif
        new object[] { 0D, 0F, Expectation.True },
        new object[] { 0D, 0M, Expectation.True },
        new object[] { 0D, 0D, Expectation.True },
        new object[] { 0D, GetJsonElement(0D), Expectation.True },
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

    [TestCaseSource(nameof(ZeroEqualsExpectationsSByte))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsInt16))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsInt32))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(ZeroEqualsExpectationsInt128))]
#endif
    [TestCaseSource(nameof(ZeroEqualsExpectationsByte))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsUInt16))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsUInt32))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsUInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(ZeroEqualsExpectationsUInt128))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsHalf))]
#endif
    [TestCaseSource(nameof(ZeroEqualsExpectationsSingle))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsDecimal))]
    [TestCaseSource(nameof(ZeroEqualsExpectationsDouble))]

    [TestCaseSource(nameof(SameEqualsExpectationsSByte))]
    [TestCaseSource(nameof(SameEqualsExpectationsInt16))]
    [TestCaseSource(nameof(SameEqualsExpectationsInt32))]
    [TestCaseSource(nameof(SameEqualsExpectationsInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(SameEqualsExpectationsInt128))]
#endif
    [TestCaseSource(nameof(SameEqualsExpectationsByte))]
    [TestCaseSource(nameof(SameEqualsExpectationsUInt16))]
    [TestCaseSource(nameof(SameEqualsExpectationsUInt32))]
    [TestCaseSource(nameof(SameEqualsExpectationsUInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(SameEqualsExpectationsUInt128))]
    [TestCaseSource(nameof(SameEqualsExpectationsHalf))]
#endif
    [TestCaseSource(nameof(SameEqualsExpectationsSingle))]
    [TestCaseSource(nameof(SameEqualsExpectationsDecimal))]
    [TestCaseSource(nameof(SameEqualsExpectationsDouble))]

    [TestCaseSource(nameof(DifferentNotEqualsExpectationsSByte))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsInt16))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsInt32))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsInt128))]
#endif
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsByte))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsUInt16))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsUInt32))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsUInt64))]
#if NET8_0_OR_GREATER
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsUInt128))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsHalf))]
#endif
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsSingle))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsDecimal))]
    [TestCaseSource(nameof(DifferentNotEqualsExpectationsDouble))]

    [TestCaseSource(nameof(AwkwardDecimalExpectations))]
    [TestCaseSource(nameof(AwkwardFloatExpectations))]
    [TestCaseSource(nameof(AwkwardDoubleExpectations))]
    public void ValuesEqual(object lhs, object rhs, Expectation expected)
    {
        BinaryJsonNumber number1 = GetBinaryJsonNumberFor(lhs);

        if (rhs is JsonElement jsonElement)
        {
            switch (expected)
            {
                case Expectation.False:
                    Assert.IsFalse(number1.Equals(jsonElement));
                    Assert.IsFalse(number1 == jsonElement);
                    Assert.IsTrue(number1 != jsonElement);
                    Assert.IsFalse(jsonElement == number1);
                    Assert.IsTrue(jsonElement != number1);
                    break;
                case Expectation.True:
                    Assert.IsTrue(number1.Equals(jsonElement));
                    Assert.IsTrue(number1 == jsonElement);
                    Assert.IsFalse(number1 != jsonElement);
                    Assert.IsTrue(jsonElement == number1);
                    Assert.IsFalse(jsonElement != number1);
                    break;
                case Expectation.Exception:
                    Assert.Catch(() => number1.Equals(jsonElement));
                    Assert.Catch(() => _ = number1 == jsonElement);
                    Assert.Catch(() => _ = number1 != jsonElement);
                    Assert.Catch(() => _ = jsonElement == number1);
                    Assert.Catch(() => _ = jsonElement != number1);
                    break;
            }
        }
        else
        {
            BinaryJsonNumber number2 = GetBinaryJsonNumberFor(rhs);

            switch (expected)
            {
                case Expectation.False:
                    Assert.IsFalse(number1.Equals(number2));
                    Assert.IsFalse(number1 == number2);
                    Assert.IsTrue(number1 != number2);
                    break;
                case Expectation.True:
                    Assert.IsTrue(number1.Equals(number2));
                    Assert.IsTrue(number1 == number2);
                    Assert.IsFalse(number1 != number2);
                    break;
                case Expectation.Exception:
                    Assert.Catch(() => number1.Equals(number2));
                    Assert.Catch(() => _ = number1 == number2);
                    Assert.Catch(() => _ = number1 != number2);
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

#if NET8_0_OR_GREATER
        if (value is Int128 int128)
        {
            return new(int128);
        }
#endif

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

#if NET8_0_OR_GREATER
        if (value is UInt128 uint128)
        {
            return new(uint128);
        }

        if (value is Half half)
        {
            return new(half);
        }
#endif
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