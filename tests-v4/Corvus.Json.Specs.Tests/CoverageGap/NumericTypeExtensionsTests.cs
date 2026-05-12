// <copyright file="NumericTypeExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text.Json;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class NumericTypeExtensionsTests
{
    [TestMethod]
    public void TryGetInt32WithFallbacks_DirectInt32_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.IsTrue(doc.RootElement.TryGetInt32WithFallbacks(out int result));
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void TryGetInt32WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("42.0");
        Assert.IsTrue(doc.RootElement.TryGetInt32WithFallbacks(out int result));
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void TryGetInt32WithFallbacks_FractionalDouble_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("42.5");
        Assert.IsFalse(doc.RootElement.TryGetInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetInt32WithFallbacks_OverflowPositive_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("3000000000");
        Assert.IsFalse(doc.RootElement.TryGetInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetInt32WithFallbacks_OverflowNegative_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("-3000000000");
        Assert.IsFalse(doc.RootElement.TryGetInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt32_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.AreEqual(42, doc.RootElement.SafeGetInt32());
    }

    [TestMethod]
    public void SafeGetInt32_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("42.5");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetInt32());
    }

    [TestMethod]
    public void TryGetInt16WithFallbacks_DirectInt16_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100");
        Assert.IsTrue(doc.RootElement.TryGetInt16WithFallbacks(out short result));
        Assert.AreEqual((short)100, result);
    }

    [TestMethod]
    public void TryGetInt16WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetInt16WithFallbacks(out short result));
        Assert.AreEqual((short)100, result);
    }

    [TestMethod]
    public void TryGetInt16WithFallbacks_Overflow_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("40000");
        Assert.IsFalse(doc.RootElement.TryGetInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt16_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("100");
        Assert.AreEqual((short)100, doc.RootElement.SafeGetInt16());
    }

    [TestMethod]
    public void SafeGetInt16_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("40000.5");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetInt16());
    }

    [TestMethod]
    public void TryGetInt64WithFallbacks_DirectInt64_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("1234567890");
        Assert.IsTrue(doc.RootElement.TryGetInt64WithFallbacks(out long result));
        Assert.AreEqual(1234567890L, result);
    }

    [TestMethod]
    public void TryGetInt64WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetInt64WithFallbacks(out long result));
        Assert.AreEqual(100L, result);
    }

    [TestMethod]
    public void TryGetInt64WithFallbacks_FractionalDouble_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("1.5");
        Assert.IsFalse(doc.RootElement.TryGetInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt64_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("1234567890");
        Assert.AreEqual(1234567890L, doc.RootElement.SafeGetInt64());
    }

    [TestMethod]
    public void SafeGetInt64_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("1.5");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetInt64());
    }

    [TestMethod]
    public void TryGetUInt32WithFallbacks_DirectUInt32_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("3000000000");
        Assert.IsTrue(doc.RootElement.TryGetUInt32WithFallbacks(out uint result));
        Assert.AreEqual(3000000000u, result);
    }

    [TestMethod]
    public void TryGetUInt32WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetUInt32WithFallbacks(out uint result));
        Assert.AreEqual(100u, result);
    }

    [TestMethod]
    public void TryGetUInt32WithFallbacks_Negative_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.IsFalse(doc.RootElement.TryGetUInt32WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt32_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("100");
        Assert.AreEqual(100u, doc.RootElement.SafeGetUInt32());
    }

    [TestMethod]
    public void SafeGetUInt32_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetUInt32());
    }

    [TestMethod]
    public void TryGetUInt16WithFallbacks_DirectUInt16_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("60000");
        Assert.IsTrue(doc.RootElement.TryGetUInt16WithFallbacks(out ushort result));
        Assert.AreEqual((ushort)60000, result);
    }

    [TestMethod]
    public void TryGetUInt16WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetUInt16WithFallbacks(out ushort result));
        Assert.AreEqual((ushort)100, result);
    }

    [TestMethod]
    public void TryGetUInt16WithFallbacks_Negative_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.IsFalse(doc.RootElement.TryGetUInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetUInt16WithFallbacks_Overflow_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("70000.0");
        Assert.IsFalse(doc.RootElement.TryGetUInt16WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt16_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("100");
        Assert.AreEqual((ushort)100, doc.RootElement.SafeGetUInt16());
    }

    [TestMethod]
    public void SafeGetUInt16_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetUInt16());
    }

    [TestMethod]
    public void TryGetUInt64WithFallbacks_DirectUInt64_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("10000000000");
        Assert.IsTrue(doc.RootElement.TryGetUInt64WithFallbacks(out ulong result));
        Assert.AreEqual(10000000000UL, result);
    }

    [TestMethod]
    public void TryGetUInt64WithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetUInt64WithFallbacks(out ulong result));
        Assert.AreEqual(100UL, result);
    }

    [TestMethod]
    public void TryGetUInt64WithFallbacks_Negative_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.IsFalse(doc.RootElement.TryGetUInt64WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt64_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("10000000000");
        Assert.AreEqual(10000000000UL, doc.RootElement.SafeGetUInt64());
    }

    [TestMethod]
    public void SafeGetUInt64_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetUInt64());
    }

    [TestMethod]
    public void TryGetByteWithFallbacks_DirectByte_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("200");
        Assert.IsTrue(doc.RootElement.TryGetByteWithFallbacks(out byte result));
        Assert.AreEqual((byte)200, result);
    }

    [TestMethod]
    public void TryGetByteWithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("100.0");
        Assert.IsTrue(doc.RootElement.TryGetByteWithFallbacks(out byte result));
        Assert.AreEqual((byte)100, result);
    }

    [TestMethod]
    public void TryGetByteWithFallbacks_Overflow_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("300.0");
        Assert.IsFalse(doc.RootElement.TryGetByteWithFallbacks(out _));
    }

    [TestMethod]
    public void TryGetByteWithFallbacks_Negative_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("-1.0");
        Assert.IsFalse(doc.RootElement.TryGetByteWithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetByte_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("200");
        Assert.AreEqual((byte)200, doc.RootElement.SafeGetByte());
    }

    [TestMethod]
    public void SafeGetByte_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("300.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetByte());
    }

    [TestMethod]
    public void TryGetSByteWithFallbacks_DirectSByte_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("-100");
        Assert.IsTrue(doc.RootElement.TryGetSByteWithFallbacks(out sbyte result));
        Assert.AreEqual((sbyte)-100, result);
    }

    [TestMethod]
    public void TryGetSByteWithFallbacks_WholeDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("-50.0");
        Assert.IsTrue(doc.RootElement.TryGetSByteWithFallbacks(out sbyte result));
        Assert.AreEqual((sbyte)-50, result);
    }

    [TestMethod]
    public void TryGetSByteWithFallbacks_Overflow_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("200.0");
        Assert.IsFalse(doc.RootElement.TryGetSByteWithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetSByte_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("-100");
        Assert.AreEqual((sbyte)-100, doc.RootElement.SafeGetSByte());
    }

    [TestMethod]
    public void SafeGetSByte_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("200.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetSByte());
    }

    [TestMethod]
    public void TryGetSingleWithFallbacks_DirectSingle_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("3.14");
        Assert.IsTrue(doc.RootElement.TryGetSingleWithFallbacks(out float result));
        Assert.AreEqual(3.14f, result, 0.001f);
    }

    [TestMethod]
    public void SafeGetSingle_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("3.14");
        float result = doc.RootElement.SafeGetSingle();
        Assert.AreEqual(3.14f, result, 0.001f);
    }

    [TestMethod]
    public void SafeGetDouble_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("3.14159");
        Assert.AreEqual(3.14159, doc.RootElement.SafeGetDouble(), 0.00001);
    }

    [TestMethod]
    public void SafeGetDouble_NonNumeric_ThrowsInvalidOperationException()
    {
        using var doc = JsonDocument.Parse("\"not-a-number\"");
        Assert.ThrowsExactly<InvalidOperationException>(() => doc.RootElement.SafeGetDouble());
    }

    [TestMethod]
    public void SafeGetDecimal_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("123.456");
        Assert.AreEqual(123.456m, doc.RootElement.SafeGetDecimal());
    }

    [TestMethod]
    public void SafeGetDecimal_NonNumeric_ThrowsInvalidOperationException()
    {
        using var doc = JsonDocument.Parse("\"not-a-number\"");
        Assert.ThrowsExactly<InvalidOperationException>(() => doc.RootElement.SafeGetDecimal());
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public void TryGetHalfWithFallbacks_ValidDouble_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("1.5");
        Assert.IsTrue(doc.RootElement.TryGetHalfWithFallbacks(out Half result));
        Assert.AreEqual((Half)1.5f, result);
    }

    [TestMethod]
    public void TryGetHalfWithFallbacks_Overflow_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("100000.0");
        Assert.IsFalse(doc.RootElement.TryGetHalfWithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetHalf_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("1.5");
        Assert.AreEqual((Half)1.5f, doc.RootElement.SafeGetHalf());
    }

    [TestMethod]
    public void SafeGetHalf_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("100000.0");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetHalf());
    }

    [TestMethod]
    public void TryGetInt128WithFallbacks_ValidInt_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.IsTrue(doc.RootElement.TryGetInt128WithFallbacks(out Int128 result));
        Assert.AreEqual((Int128)42, result);
    }

    [TestMethod]
    public void TryGetInt128WithFallbacks_FloatingPoint_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("3.14");
        Assert.IsFalse(doc.RootElement.TryGetInt128WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetInt128_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.AreEqual((Int128)42, doc.RootElement.SafeGetInt128());
    }

    [TestMethod]
    public void SafeGetInt128_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("3.14");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetInt128());
    }

    [TestMethod]
    public void TryGetUInt128WithFallbacks_ValidUInt_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.IsTrue(doc.RootElement.TryGetUInt128WithFallbacks(out UInt128 result));
        Assert.AreEqual((UInt128)42, result);
    }

    [TestMethod]
    public void TryGetUInt128WithFallbacks_FloatingPoint_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("3.14");
        Assert.IsFalse(doc.RootElement.TryGetUInt128WithFallbacks(out _));
    }

    [TestMethod]
    public void SafeGetUInt128_Valid_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.AreEqual((UInt128)42, doc.RootElement.SafeGetUInt128());
    }

    [TestMethod]
    public void SafeGetUInt128_Invalid_ThrowsFormatException()
    {
        using var doc = JsonDocument.Parse("3.14");
        Assert.ThrowsExactly<FormatException>(() => doc.RootElement.SafeGetUInt128());
    }
#endif
}