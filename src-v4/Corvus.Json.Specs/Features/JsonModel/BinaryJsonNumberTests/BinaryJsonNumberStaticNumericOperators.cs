// <copyright file="BinaryJsonNumberStaticNumericOperators.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;

namespace Features.JsonModel.BinaryJsonNumberTests;

[TestFixture]
public class BinaryJsonNumberStaticNumericOperators
{
    [Test]
    public void TestOperator_Addition_Byte()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((byte)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_Decimal()
    {
        var num1 = new BinaryJsonNumber(5m);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15m), result);
    }

    [Test]
    public void TestOperator_Addition_Double()
    {
        var num1 = new BinaryJsonNumber(5.0);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15.0), result);
    }

    [Test]
    public void TestOperator_Addition_Int16()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((short)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_Int32()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15), result);
    }

    [Test]
    public void TestOperator_Addition_Int64()
    {
        var num1 = new BinaryJsonNumber(5L);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15L), result);
    }

    [Test]
    public void TestOperator_Addition_SByte()
    {
        var num1 = new BinaryJsonNumber((sbyte)5);
        var num2 = new BinaryJsonNumber((sbyte)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((sbyte)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_Single()
    {
        var num1 = new BinaryJsonNumber(5f);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15f), result);
    }

    [Test]
    public void TestOperator_Addition_UInt16()
    {
        var num1 = new BinaryJsonNumber((ushort)5);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((ushort)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_UInt32()
    {
        var num1 = new BinaryJsonNumber(5U);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((uint)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_UInt64()
    {
        var num1 = new BinaryJsonNumber(5UL);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((ulong)(5 + 10)), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Addition_Int128()
    {
        var num1 = new BinaryJsonNumber((Int128)5);
        var num2 = new BinaryJsonNumber((Int128)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_UInt128()
    {
        var num1 = new BinaryJsonNumber((UInt128)5);
        var num2 = new BinaryJsonNumber((UInt128)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((UInt128)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_Half()
    {
        var num1 = new BinaryJsonNumber((Half)5);
        var num2 = new BinaryJsonNumber((Half)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((Half)(5 + 10)), result);
    }
#endif

    [Test]
    public void TestOperator_Addition_ByteAndInt32()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15), result);
    }

    [Test]
    public void TestOperator_Addition_ByteAndDouble()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15.0), result);
    }

    [Test]
    public void TestOperator_Addition_Int16AndDouble()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15.0), result);
    }

    [Test]
    public void TestOperator_Addition_Int16AndDecimal()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15m), result);
    }

    [Test]
    public void TestOperator_Addition_Int32AndSingle()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15f), result);
    }

    [Test]
    public void TestOperator_Addition_Int32AndUInt64()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15UL), result);
    }

    [Test]
    public void TestOperator_Addition_Int64AndUInt32()
    {
        var num1 = new BinaryJsonNumber(5L);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15L), result);
    }

    [Test]
    public void TestOperator_Addition_SByteAndUInt16()
    {
        var num1 = new BinaryJsonNumber((sbyte)5);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15), result);
    }

    [Test]
    public void TestOperator_Addition_SingleAndDecimal()
    {
        var num1 = new BinaryJsonNumber(5f);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15m), result);
    }

    [Test]
    public void TestOperator_Addition_UInt16AndInt64()
    {
        var num1 = new BinaryJsonNumber((ushort)5);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15L), result);
    }

    [Test]
    public void TestOperator_Addition_UInt32AndInt16()
    {
        var num1 = new BinaryJsonNumber(5U);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15), result);
    }

    [Test]
    public void TestOperator_Addition_UInt64AndByte()
    {
        var num1 = new BinaryJsonNumber(5UL);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15UL), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Addition_Int128AndUInt64()
    {
        var num1 = new BinaryJsonNumber((Int128)5);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(5 + 10)), result);
    }

    [Test]
    public void TestOperator_Addition_HalfAndDecimal()
    {
        var num1 = new BinaryJsonNumber((Half)5);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 + num2;

        Assert.AreEqual(new BinaryJsonNumber(15m), result);
    }
#endif

    [Test]
    public void TestOperator_Subtraction_ByteAndInt32()
    {
        var num1 = new BinaryJsonNumber((byte)15);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Subtraction_ByteAndDouble()
    {
        var num1 = new BinaryJsonNumber((byte)15);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int16AndDouble()
    {
        var num1 = new BinaryJsonNumber((short)15);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int16AndDecimal()
    {
        var num1 = new BinaryJsonNumber((short)15);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int32AndSingle()
    {
        var num1 = new BinaryJsonNumber(15);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5f), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int32AndUInt64()
    {
        var num1 = new BinaryJsonNumber(15);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5UL), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int64AndUInt32()
    {
        var num1 = new BinaryJsonNumber(15L);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Subtraction_SByteAndUInt16()
    {
        var num1 = new BinaryJsonNumber((sbyte)15);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Subtraction_SingleAndDecimal()
    {
        var num1 = new BinaryJsonNumber(15f);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt16AndInt64()
    {
        var num1 = new BinaryJsonNumber((ushort)15);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt32AndInt16()
    {
        var num1 = new BinaryJsonNumber(15U);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt64AndByte()
    {
        var num1 = new BinaryJsonNumber(15UL);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5UL), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Subtraction_Int128AndUInt64()
    {
        var num1 = new BinaryJsonNumber((Int128)15);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_HalfAndDecimal()
    {
        var num1 = new BinaryJsonNumber((Half)15);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }
#endif

    [Test]
    public void TestOperator_Subtraction_Byte()
    {
        var num1 = new BinaryJsonNumber((byte)15);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((byte)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_Decimal()
    {
        var num1 = new BinaryJsonNumber(15m);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Subtraction_Double()
    {
        var num1 = new BinaryJsonNumber(15.0);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int16()
    {
        var num1 = new BinaryJsonNumber((short)15);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((short)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int32()
    {
        var num1 = new BinaryJsonNumber(15);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Subtraction_Int64()
    {
        var num1 = new BinaryJsonNumber(15L);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Subtraction_SByte()
    {
        var num1 = new BinaryJsonNumber((sbyte)15);
        var num2 = new BinaryJsonNumber((sbyte)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((sbyte)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_Single()
    {
        var num1 = new BinaryJsonNumber(15f);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber(5f), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt16()
    {
        var num1 = new BinaryJsonNumber((ushort)15);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((ushort)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt32()
    {
        var num1 = new BinaryJsonNumber(15U);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((uint)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt64()
    {
        var num1 = new BinaryJsonNumber(15UL);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((ulong)(15 - 10)), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Subtraction_Int128()
    {
        var num1 = new BinaryJsonNumber((Int128)15);
        var num2 = new BinaryJsonNumber((Int128)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_UInt128()
    {
        var num1 = new BinaryJsonNumber((UInt128)15);
        var num2 = new BinaryJsonNumber((UInt128)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((UInt128)(15 - 10)), result);
    }

    [Test]
    public void TestOperator_Subtraction_Half()
    {
        var num1 = new BinaryJsonNumber((Half)15);
        var num2 = new BinaryJsonNumber((Half)10);
        BinaryJsonNumber result = num1 - num2;

        Assert.AreEqual(new BinaryJsonNumber((Half)(15 - 10)), result);
    }
#endif

    [Test]
    public void TestOperator_Multiplication_Byte()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((byte)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_Decimal()
    {
        var num1 = new BinaryJsonNumber(5m);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50m), result);
    }

    [Test]
    public void TestOperator_Multiplication_Double()
    {
        var num1 = new BinaryJsonNumber(5.0);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50.0), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int16()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((short)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int32()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int64()
    {
        var num1 = new BinaryJsonNumber(5L);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50L), result);
    }

    [Test]
    public void TestOperator_Multiplication_SByte()
    {
        var num1 = new BinaryJsonNumber((sbyte)5);
        var num2 = new BinaryJsonNumber((sbyte)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((sbyte)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_Single()
    {
        var num1 = new BinaryJsonNumber(5f);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50f), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt16()
    {
        var num1 = new BinaryJsonNumber((ushort)5);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((ushort)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt32()
    {
        var num1 = new BinaryJsonNumber(5U);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((uint)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt64()
    {
        var num1 = new BinaryJsonNumber(5UL);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((ulong)(5 * 10)), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Multiplication_Int128()
    {
        var num1 = new BinaryJsonNumber((Int128)5);
        var num2 = new BinaryJsonNumber((Int128)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt128()
    {
        var num1 = new BinaryJsonNumber((UInt128)5);
        var num2 = new BinaryJsonNumber((UInt128)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((UInt128)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_Half()
    {
        var num1 = new BinaryJsonNumber((Half)5);
        var num2 = new BinaryJsonNumber((Half)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((Half)(5 * 10)), result);
    }
#endif

    [Test]
    public void TestOperator_Multiplication_ByteAndInt32()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber(10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50), result);
    }

    [Test]
    public void TestOperator_Multiplication_ByteAndDouble()
    {
        var num1 = new BinaryJsonNumber((byte)5);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50.0), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int16AndDouble()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber(10.0);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50.0), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int16AndDecimal()
    {
        var num1 = new BinaryJsonNumber((short)5);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50m), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int32AndSingle()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10f);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50f), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int32AndUInt64()
    {
        var num1 = new BinaryJsonNumber(5);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50UL), result);
    }

    [Test]
    public void TestOperator_Multiplication_Int64AndUInt32()
    {
        var num1 = new BinaryJsonNumber(5L);
        var num2 = new BinaryJsonNumber(10U);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50L), result);
    }

    [Test]
    public void TestOperator_Multiplication_SByteAndUInt16()
    {
        var num1 = new BinaryJsonNumber((sbyte)5);
        var num2 = new BinaryJsonNumber((ushort)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50), result);
    }

    [Test]
    public void TestOperator_Multiplication_SingleAndDecimal()
    {
        var num1 = new BinaryJsonNumber(5f);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50m), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt16AndInt64()
    {
        var num1 = new BinaryJsonNumber((ushort)5);
        var num2 = new BinaryJsonNumber(10L);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50L), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt32AndInt16()
    {
        var num1 = new BinaryJsonNumber(5U);
        var num2 = new BinaryJsonNumber((short)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50), result);
    }

    [Test]
    public void TestOperator_Multiplication_UInt64AndByte()
    {
        var num1 = new BinaryJsonNumber(5UL);
        var num2 = new BinaryJsonNumber((byte)10);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50UL), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Multiplication_Int128AndUInt64()
    {
        var num1 = new BinaryJsonNumber((Int128)5);
        var num2 = new BinaryJsonNumber(10UL);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(5 * 10)), result);
    }

    [Test]
    public void TestOperator_Multiplication_HalfAndDecimal()
    {
        var num1 = new BinaryJsonNumber((Half)5);
        var num2 = new BinaryJsonNumber(10m);
        BinaryJsonNumber result = num1 * num2;

        Assert.AreEqual(new BinaryJsonNumber(50m), result);
    }
#endif

    [Test]
    public void TestOperator_Division_Byte()
    {
        var num1 = new BinaryJsonNumber((byte)10);
        var num2 = new BinaryJsonNumber((byte)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((byte)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_Decimal()
    {
        var num1 = new BinaryJsonNumber(10m);
        var num2 = new BinaryJsonNumber(2m);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Division_Double()
    {
        var num1 = new BinaryJsonNumber(10.0);
        var num2 = new BinaryJsonNumber(2.0);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Division_Int16()
    {
        var num1 = new BinaryJsonNumber((short)10);
        var num2 = new BinaryJsonNumber((short)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((short)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_Int32()
    {
        var num1 = new BinaryJsonNumber(10);
        var num2 = new BinaryJsonNumber(2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Division_Int64()
    {
        var num1 = new BinaryJsonNumber(10L);
        var num2 = new BinaryJsonNumber(2L);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Division_SByte()
    {
        var num1 = new BinaryJsonNumber((sbyte)10);
        var num2 = new BinaryJsonNumber((sbyte)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((sbyte)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_Single()
    {
        var num1 = new BinaryJsonNumber(10f);
        var num2 = new BinaryJsonNumber(2f);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5f), result);
    }

    [Test]
    public void TestOperator_Division_UInt16()
    {
        var num1 = new BinaryJsonNumber((ushort)10);
        var num2 = new BinaryJsonNumber((ushort)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((ushort)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_UInt32()
    {
        var num1 = new BinaryJsonNumber(10U);
        var num2 = new BinaryJsonNumber(2U);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((uint)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_UInt64()
    {
        var num1 = new BinaryJsonNumber(10UL);
        var num2 = new BinaryJsonNumber(2UL);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((ulong)(10 / 2)), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Division_Int128()
    {
        var num1 = new BinaryJsonNumber((Int128)10);
        var num2 = new BinaryJsonNumber((Int128)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_UInt128()
    {
        var num1 = new BinaryJsonNumber((UInt128)10);
        var num2 = new BinaryJsonNumber((UInt128)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((UInt128)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_Half()
    {
        var num1 = new BinaryJsonNumber((Half)10);
        var num2 = new BinaryJsonNumber((Half)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((Half)(10 / 2)), result);
    }
#endif

    [Test]
    public void TestOperator_Division_ByteAndInt32()
    {
        var num1 = new BinaryJsonNumber((byte)10);
        var num2 = new BinaryJsonNumber(2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Division_ByteAndDouble()
    {
        var num1 = new BinaryJsonNumber((byte)10);
        var num2 = new BinaryJsonNumber(2.0);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Division_Int16AndDouble()
    {
        var num1 = new BinaryJsonNumber((short)10);
        var num2 = new BinaryJsonNumber(2.0);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5.0), result);
    }

    [Test]
    public void TestOperator_Division_Int16AndDecimal()
    {
        var num1 = new BinaryJsonNumber((short)10);
        var num2 = new BinaryJsonNumber(2m);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Division_Int32AndSingle()
    {
        var num1 = new BinaryJsonNumber(10);
        var num2 = new BinaryJsonNumber(2f);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5f), result);
    }

    [Test]
    public void TestOperator_Division_Int32AndUInt64()
    {
        var num1 = new BinaryJsonNumber(10);
        var num2 = new BinaryJsonNumber(2UL);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5UL), result);
    }

    [Test]
    public void TestOperator_Division_Int64AndUInt32()
    {
        var num1 = new BinaryJsonNumber(10L);
        var num2 = new BinaryJsonNumber(2U);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Division_SByteAndUInt16()
    {
        var num1 = new BinaryJsonNumber((sbyte)10);
        var num2 = new BinaryJsonNumber((ushort)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Division_SingleAndDecimal()
    {
        var num1 = new BinaryJsonNumber(10f);
        var num2 = new BinaryJsonNumber(2m);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }

    [Test]
    public void TestOperator_Division_UInt16AndInt64()
    {
        var num1 = new BinaryJsonNumber((ushort)10);
        var num2 = new BinaryJsonNumber(2L);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5L), result);
    }

    [Test]
    public void TestOperator_Division_UInt32AndInt16()
    {
        var num1 = new BinaryJsonNumber(10U);
        var num2 = new BinaryJsonNumber((short)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5), result);
    }

    [Test]
    public void TestOperator_Division_UInt64AndByte()
    {
        var num1 = new BinaryJsonNumber(10UL);
        var num2 = new BinaryJsonNumber((byte)2);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5UL), result);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Division_Int128AndUInt64()
    {
        var num1 = new BinaryJsonNumber((Int128)10);
        var num2 = new BinaryJsonNumber(2UL);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber((Int128)(10 / 2)), result);
    }

    [Test]
    public void TestOperator_Division_HalfAndDecimal()
    {
        var num1 = new BinaryJsonNumber((Half)10);
        var num2 = new BinaryJsonNumber(2m);
        BinaryJsonNumber result = num1 / num2;

        Assert.AreEqual(new BinaryJsonNumber(5m), result);
    }
#endif

    [Test]
    public void TestOperator_Decrement_Byte()
    {
        var num = new BinaryJsonNumber((byte)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((byte)9), num);
    }

    [Test]
    public void TestOperator_Decrement_Decimal()
    {
        var num = new BinaryJsonNumber(10m);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9m), num);
    }

    [Test]
    public void TestOperator_Decrement_Double()
    {
        var num = new BinaryJsonNumber(10.0);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9.0), num);
    }

    [Test]
    public void TestOperator_Decrement_Int16()
    {
        var num = new BinaryJsonNumber((short)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((short)9), num);
    }

    [Test]
    public void TestOperator_Decrement_Int32()
    {
        var num = new BinaryJsonNumber(10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9), num);
    }

    [Test]
    public void TestOperator_Decrement_Int64()
    {
        var num = new BinaryJsonNumber(10L);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9L), num);
    }

    [Test]
    public void TestOperator_Decrement_SByte()
    {
        var num = new BinaryJsonNumber((sbyte)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)9), num);
    }

    [Test]
    public void TestOperator_Decrement_Single()
    {
        var num = new BinaryJsonNumber(10f);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9f), num);
    }

    [Test]
    public void TestOperator_Decrement_UInt16()
    {
        var num = new BinaryJsonNumber((ushort)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((ushort)9), num);
    }

    [Test]
    public void TestOperator_Decrement_UInt32()
    {
        var num = new BinaryJsonNumber(10U);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9U), num);
    }

    [Test]
    public void TestOperator_Decrement_UInt64()
    {
        var num = new BinaryJsonNumber(10UL);
        num--;
        Assert.AreEqual(new BinaryJsonNumber(9UL), num);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void TestOperator_Decrement_Int128()
    {
        var num = new BinaryJsonNumber((Int128)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((Int128)9), num);
    }

    [Test]
    public void TestOperator_Decrement_UInt128()
    {
        var num = new BinaryJsonNumber((UInt128)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((UInt128)9), num);
    }

    [Test]
    public void TestOperator_Decrement_Half()
    {
        var num = new BinaryJsonNumber((Half)10);
        num--;
        Assert.AreEqual(new BinaryJsonNumber((Half)9), num);
    }
#endif
}