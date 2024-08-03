// <copyright file="BinaryJsonNumberIsMultipleOfTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
internal class BinaryJsonNumberIsMultipleOfTests
{
    [Test]
    public void TestIsMultipleOf_Double()
    {
        var number1 = new BinaryJsonNumber(10.0);
        var number2 = new BinaryJsonNumber(2.5);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Decimal()
    {
        var number1 = new BinaryJsonNumber(10.0m);
        var number2 = new BinaryJsonNumber(2.5m);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_JsonElementAndDouble()
    {
        JsonElement number1 = JsonDocument.Parse("10.0").RootElement;
        var number2 = new BinaryJsonNumber(2.5);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_JsonElementAndDecimal()
    {
        JsonElement number1 = JsonDocument.Parse("10.0").RootElement;
        var number2 = new BinaryJsonNumber(2.5m);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Double()
    {
        var number1 = new BinaryJsonNumber(10.0);
        var number2 = new BinaryJsonNumber(3.0);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Decimal()
    {
        var number1 = new BinaryJsonNumber(10.0m);
        var number2 = new BinaryJsonNumber(3.0m);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_JsonElementAndDouble()
    {
        JsonElement number1 = JsonDocument.Parse("10.0").RootElement;
        var number2 = new BinaryJsonNumber(3.0);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_JsonElementAndDecimal()
    {
        JsonElement number1 = JsonDocument.Parse("10.0").RootElement;
        var number2 = new BinaryJsonNumber(3.0m);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsMultipleOf_Byte()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var number2 = new BinaryJsonNumber((byte)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Short()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var number2 = new BinaryJsonNumber((short)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_SByte()
    {
        var number1 = new BinaryJsonNumber((sbyte)10);
        var number2 = new BinaryJsonNumber((sbyte)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_UShort()
    {
        var number1 = new BinaryJsonNumber((ushort)10);
        var number2 = new BinaryJsonNumber((ushort)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_UInt()
    {
        var number1 = new BinaryJsonNumber(10u);
        var number2 = new BinaryJsonNumber(2u);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestIsMultipleOf_Half()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var number2 = new BinaryJsonNumber((Half)2.5);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_UInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var number2 = new BinaryJsonNumber((UInt128)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Int128()
    {
        var number1 = new BinaryJsonNumber((Int128)10);
        var number2 = new BinaryJsonNumber((Int128)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }
#endif

    [Test]
    public void TestIsNotMultipleOf_Byte()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var number2 = new BinaryJsonNumber((byte)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Short()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var number2 = new BinaryJsonNumber((short)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_SByte()
    {
        var number1 = new BinaryJsonNumber((sbyte)10);
        var number2 = new BinaryJsonNumber((sbyte)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_UShort()
    {
        var number1 = new BinaryJsonNumber((ushort)10);
        var number2 = new BinaryJsonNumber((ushort)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_UInt()
    {
        var number1 = new BinaryJsonNumber(10u);
        var number2 = new BinaryJsonNumber(3u);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestIsNotMultipleOf_Half()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var number2 = new BinaryJsonNumber((Half)3.0);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_UInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var number2 = new BinaryJsonNumber((UInt128)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Int128()
    {
        var number1 = new BinaryJsonNumber((Int128)10);
        var number2 = new BinaryJsonNumber((Int128)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }
#endif

#if NET8_0_OR_GREATER

    [Test]
    public void TestIsMultipleOf_Literal_Byte()
    {
        byte number1 = 10;
        var number2 = new BinaryJsonNumber((byte)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_Short()
    {
        short number1 = 10;
        var number2 = new BinaryJsonNumber((short)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_SByte()
    {
        sbyte number1 = 10;
        var number2 = new BinaryJsonNumber((sbyte)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_UShort()
    {
        ushort number1 = 10;
        var number2 = new BinaryJsonNumber((ushort)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_UInt()
    {
        uint number1 = 10u;
        var number2 = new BinaryJsonNumber(2u);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_Half()
    {
        var number1 = (Half)10.0;
        var number2 = new BinaryJsonNumber((Half)2.5);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_UInt128()
    {
        var number1 = (UInt128)10;
        var number2 = new BinaryJsonNumber((UInt128)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsMultipleOf_Literal_Int128()
    {
        var number1 = (Int128)10;
        var number2 = new BinaryJsonNumber((Int128)2);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_Byte()
    {
        byte number1 = 10;
        var number2 = new BinaryJsonNumber((byte)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_Short()
    {
        short number1 = 10;
        var number2 = new BinaryJsonNumber((short)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_SByte()
    {
        sbyte number1 = 10;
        var number2 = new BinaryJsonNumber((sbyte)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_UShort()
    {
        ushort number1 = 10;
        var number2 = new BinaryJsonNumber((ushort)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_UInt()
    {
        uint number1 = 10u;
        var number2 = new BinaryJsonNumber(3u);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_Half()
    {
        var number1 = (Half)10.0;
        var number2 = new BinaryJsonNumber((Half)3.0);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_UInt128()
    {
        var number1 = (UInt128)10;
        var number2 = new BinaryJsonNumber((UInt128)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestIsNotMultipleOf_Literal_Int128()
    {
        var number1 = (Int128)10;
        var number2 = new BinaryJsonNumber((Int128)3);
        bool result = BinaryJsonNumber.IsMultipleOf(number1, number2);

        Assert.IsFalse(result);
    }
#endif

    [Test]
    public void TestInstanceIsMultipleOf_Byte()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var multipleOf = new BinaryJsonNumber((byte)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_Short()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var multipleOf = new BinaryJsonNumber((short)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_SByte()
    {
        var number1 = new BinaryJsonNumber((sbyte)10);
        var multipleOf = new BinaryJsonNumber((sbyte)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_UShort()
    {
        var number1 = new BinaryJsonNumber((ushort)10);
        var multipleOf = new BinaryJsonNumber((ushort)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_UInt()
    {
        var number1 = new BinaryJsonNumber(10u);
        var multipleOf = new BinaryJsonNumber(2u);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestInstanceIsMultipleOf_Half()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var multipleOf = new BinaryJsonNumber((Half)2.5);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_UInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var multipleOf = new BinaryJsonNumber((UInt128)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_Int128()
    {
        var number1 = new BinaryJsonNumber((Int128)10);
        var multipleOf = new BinaryJsonNumber((Int128)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }
#endif

    [Test]
    public void TestInstanceIsNotMultipleOf_Byte()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var multipleOf = new BinaryJsonNumber((byte)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_Short()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var multipleOf = new BinaryJsonNumber((short)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_SByte()
    {
        var number1 = new BinaryJsonNumber((sbyte)10);
        var multipleOf = new BinaryJsonNumber((sbyte)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_UShort()
    {
        var number1 = new BinaryJsonNumber((ushort)10);
        var multipleOf = new BinaryJsonNumber((ushort)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_UInt()
    {
        var number1 = new BinaryJsonNumber(10u);
        var multipleOf = new BinaryJsonNumber(3u);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestInstanceIsNotMultipleOf_Half()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var multipleOf = new BinaryJsonNumber((Half)3.0);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_UInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var multipleOf = new BinaryJsonNumber((UInt128)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_Int128()
    {
        var number1 = new BinaryJsonNumber((Int128)10);
        var multipleOf = new BinaryJsonNumber((Int128)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }
#endif

    [Test]
    public void TestInstanceIsMultipleOf_DifferentKind_ByteToShort()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var multipleOf = new BinaryJsonNumber((short)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_DifferentKind_ShortToByte()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var multipleOf = new BinaryJsonNumber((byte)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_DifferentKind_UIntToUShort()
    {
        var number1 = new BinaryJsonNumber(10u);
        var multipleOf = new BinaryJsonNumber((ushort)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestInstanceIsMultipleOf_DifferentKind_HalfToUInt()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var multipleOf = new BinaryJsonNumber(2u);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }

    [Test]
    public void TestInstanceIsMultipleOf_DifferentKind_UInt128ToInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var multipleOf = new BinaryJsonNumber((Int128)2);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsTrue(result);
    }
#endif

    [Test]
    public void TestInstanceIsNotMultipleOf_DifferentKind_ByteToShort()
    {
        var number1 = new BinaryJsonNumber((byte)10);
        var multipleOf = new BinaryJsonNumber((short)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_DifferentKind_ShortToByte()
    {
        var number1 = new BinaryJsonNumber((short)10);
        var multipleOf = new BinaryJsonNumber((byte)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_DifferentKind_UIntToUShort()
    {
        var number1 = new BinaryJsonNumber(10u);
        var multipleOf = new BinaryJsonNumber((ushort)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestInstanceIsNotMultipleOf_DifferentKind_HalfToUInt()
    {
        var number1 = new BinaryJsonNumber((Half)10.0);
        var multipleOf = new BinaryJsonNumber(3u);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }

    [Test]
    public void TestInstanceIsNotMultipleOf_DifferentKind_UInt128ToInt128()
    {
        var number1 = new BinaryJsonNumber((UInt128)10);
        var multipleOf = new BinaryJsonNumber((Int128)3);
        bool result = number1.IsMultipleOf(multipleOf);

        Assert.IsFalse(result);
    }
#endif
}