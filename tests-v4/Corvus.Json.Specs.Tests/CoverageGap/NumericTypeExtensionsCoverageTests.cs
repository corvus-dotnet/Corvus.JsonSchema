// <copyright file="NumericTypeExtensionsCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text.Json;
using Corvus.Json;

namespace CoverageGap;

/// <summary>
/// Tests for <see cref="NumericTypeExtensions"/> targeting SafeGet failure branches
/// and TryGet fallback paths that are uncovered.
/// </summary>
[TestClass]
public class NumericTypeExtensionsCoverageTests
{
    private static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }

    [TestMethod]
    public void SafeGetInt32_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetInt32());
    }

    [TestMethod]
    public void TryGetInt32_FractionalDouble_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1.5").TryGetInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetInt32_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("3000000000").TryGetInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt16_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetInt16());
    }

    [TestMethod]
    public void TryGetInt16_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("40000").TryGetInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetInt16_Fractional_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1.5").TryGetInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetSingle_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetSingle());
    }

    [TestMethod]
    public void SafeGetDouble_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetDouble());
    }

    [TestMethod]
    public void SafeGetDecimal_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetDecimal());
    }

    [TestMethod]
    public void SafeGetInt64_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetInt64());
    }

    [TestMethod]
    public void TryGetInt64_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1e+20").TryGetInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetInt64_Fractional_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1.5").TryGetInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt32_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetUInt32());
    }

    [TestMethod]
    public void TryGetUInt32_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("5000000000").TryGetUInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetUInt32_Negative_ReturnsFalse()
    {
        Assert.IsFalse(Parse("-1").TryGetUInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt16_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetUInt16());
    }

    [TestMethod]
    public void TryGetUInt16_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("70000").TryGetUInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetUInt16_Negative_ReturnsFalse()
    {
        Assert.IsFalse(Parse("-1").TryGetUInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt64_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetUInt64());
    }

    [TestMethod]
    public void TryGetUInt64_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1e+20").TryGetUInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetUInt64_Negative_ReturnsFalse()
    {
        Assert.IsFalse(Parse("-1").TryGetUInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetByte_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetByte());
    }

    [TestMethod]
    public void TryGetByte_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("300").TryGetByteWithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetByte_Negative_ReturnsFalse()
    {
        Assert.IsFalse(Parse("-1").TryGetByteWithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetSByte_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetSByte());
    }

    [TestMethod]
    public void TryGetSByte_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("200").TryGetSByteWithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetSByte_Fractional_ReturnsFalse()
    {
        Assert.IsFalse(Parse("1.5").TryGetSByteWithFallbacks(out _));
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public void SafeGetHalf_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetHalf());
    }

    [TestMethod]
    public void TryGetHalf_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(Parse("70000").TryGetHalfWithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt128_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetInt128());
    }

    [TestMethod]
    public void TryGetInt128_InvalidString_ReturnsFalse()
    {
        Assert.IsFalse(Parse("\"not_a_number\"").TryGetInt128WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt128_NonNumeric_Throws()
    {
        Assert.Throws<Exception>(() => Parse("\"text\"").SafeGetUInt128());
    }

    [TestMethod]
    public void TryGetUInt128_InvalidString_ReturnsFalse()
    {
        Assert.IsFalse(Parse("\"not_a_number\"").TryGetUInt128WithFallbacks(out _));
    }
#endif

    [TestMethod]
    public void TryGetInt32_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("3.0").TryGetInt32WithFallbacks(out int result));
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void TryGetInt64_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("7.0").TryGetInt64WithFallbacks(out long result));
        Assert.AreEqual(7L, result);
    }

    [TestMethod]
    public void TryGetByte_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("100.0").TryGetByteWithFallbacks(out byte result));
        Assert.AreEqual((byte)100, result);
    }

    [TestMethod]
    public void TryGetSByte_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("42.0").TryGetSByteWithFallbacks(out sbyte result));
        Assert.AreEqual((sbyte)42, result);
    }

    [TestMethod]
    public void TryGetUInt16_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("500.0").TryGetUInt16WithFallbacks(out ushort result));
        Assert.AreEqual((ushort)500, result);
    }

    [TestMethod]
    public void TryGetUInt32_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("1000.0").TryGetUInt32WithFallbacks(out uint result));
        Assert.AreEqual(1000u, result);
    }

    [TestMethod]
    public void TryGetUInt64_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("2000.0").TryGetUInt64WithFallbacks(out ulong result));
        Assert.AreEqual(2000UL, result);
    }

    [TestMethod]
    public void TryGetInt16_ViaDoubleFallback_Succeeds()
    {
        Assert.IsTrue(Parse("42.0").TryGetInt16WithFallbacks(out short result));
        Assert.AreEqual((short)42, result);
    }
}