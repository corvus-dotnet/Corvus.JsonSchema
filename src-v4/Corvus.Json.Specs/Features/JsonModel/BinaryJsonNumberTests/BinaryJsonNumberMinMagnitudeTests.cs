// <copyright file="BinaryJsonNumberMinMagnitudeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER

using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
internal class BinaryJsonNumberMinMagnitudeTests
{
    [Test]
    public void TestMinMagnitude_Byte()
    {
        var number1 = new BinaryJsonNumber((byte)123);
        var number2 = new BinaryJsonNumber((byte)45);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Decimal()
    {
        var number1 = new BinaryJsonNumber(123.45m);
        var number2 = new BinaryJsonNumber(67.89m);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Double()
    {
        var number1 = new BinaryJsonNumber(123.45);
        var number2 = new BinaryJsonNumber(67.89);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Half()
    {
        var number1 = new BinaryJsonNumber((Half)123.45);
        var number2 = new BinaryJsonNumber((Half)67.89);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int16()
    {
        var number1 = new BinaryJsonNumber((short)12345);
        var number2 = new BinaryJsonNumber((short)6789);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int32()
    {
        var number1 = new BinaryJsonNumber(12345);
        var number2 = new BinaryJsonNumber(6789);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int64()
    {
        var number1 = new BinaryJsonNumber(1234567890L);
        var number2 = new BinaryJsonNumber(678901234L);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int128()
    {
        var number1 = new BinaryJsonNumber((Int128)12345678901234567890);
        var number2 = new BinaryJsonNumber((Int128)6789012345678901234);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_SByte()
    {
        var number1 = new BinaryJsonNumber((sbyte)123);
        var number2 = new BinaryJsonNumber((sbyte)45);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Single()
    {
        var number1 = new BinaryJsonNumber(123.45f);
        var number2 = new BinaryJsonNumber(67.89f);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt16()
    {
        var number1 = new BinaryJsonNumber((ushort)12345);
        var number2 = new BinaryJsonNumber((ushort)6789);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt32()
    {
        var number1 = new BinaryJsonNumber(12345U);
        var number2 = new BinaryJsonNumber(6789U);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt64()
    {
        var number1 = new BinaryJsonNumber(1234567890UL);
        var number2 = new BinaryJsonNumber(678901234UL);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)12345678901234567890);
        var number2 = new BinaryJsonNumber((UInt128)6789012345678901234);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_ByteAndInt32()
    {
        var number1 = new BinaryJsonNumber((byte)123);
        var number2 = new BinaryJsonNumber(45);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_DecimalAndDouble()
    {
        var number1 = new BinaryJsonNumber(123.45m);
        var number2 = new BinaryJsonNumber(67.89);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_HalfAndSingle()
    {
        var number1 = new BinaryJsonNumber((Half)123.45);
        var number2 = new BinaryJsonNumber(67.89f);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int16AndInt64()
    {
        var number1 = new BinaryJsonNumber((short)12345);
        var number2 = new BinaryJsonNumber(678901234L);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number1, result);
    }

    [Test]
    public void TestMinMagnitude_Int32AndUInt32()
    {
        var number1 = new BinaryJsonNumber(12345);
        var number2 = new BinaryJsonNumber(67890U);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number1, result);
    }

    [Test]
    public void TestMinMagnitude_Int64AndUInt64()
    {
        var number1 = new BinaryJsonNumber(1234567890L);
        var number2 = new BinaryJsonNumber(678901234UL);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_Int128AndUInt128()
    {
        var number1 = new BinaryJsonNumber((Int128)12345678901234567890);
        var number2 = new BinaryJsonNumber((UInt128)6789012345678901234);

        Assert.Throws<NotSupportedException>(() => BinaryJsonNumber.MinMagnitude(number1, number2));
    }

    [Test]
    public void TestMinMagnitude_SByteAndUInt16()
    {
        var number1 = new BinaryJsonNumber((sbyte)123);
        var number2 = new BinaryJsonNumber((ushort)6789);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number1, result);
    }

    [Test]
    public void TestMinMagnitude_SingleAndDecimal()
    {
        var number1 = new BinaryJsonNumber(123.45f);
        var number2 = new BinaryJsonNumber(67.89m);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt16AndInt32()
    {
        var number1 = new BinaryJsonNumber((ushort)12345);
        var number2 = new BinaryJsonNumber(6789);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number2, result);
    }

    [Test]
    public void TestMinMagnitude_UInt32AndInt64()
    {
        var number1 = new BinaryJsonNumber(12345U);
        var number2 = new BinaryJsonNumber(678901234L);
        var result = BinaryJsonNumber.MinMagnitude(number1, number2);

        Assert.AreEqual(number1, result);
    }

    [Test]
    public void TestMinMagnitude_UInt64AndInt128()
    {
        var number1 = new BinaryJsonNumber(1234567890UL);
        var number2 = new BinaryJsonNumber((Int128)6789012345678901234);

        Assert.Throws<NotSupportedException>(() => BinaryJsonNumber.MinMagnitude(number1, number2));
    }
}
#endif