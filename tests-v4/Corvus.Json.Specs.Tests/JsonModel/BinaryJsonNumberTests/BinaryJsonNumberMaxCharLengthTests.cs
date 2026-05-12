// <copyright file="BinaryJsonNumberMaxCharLengthTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.BinaryJsonNumberTests;

[TestClass]
public class BinaryJsonNumberMaxCharLengthTests
{
    [TestMethod]
    public void TestGetMaxCharLength_Byte()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Byte);
        Assert.AreEqual(3, maxLength); // 255 is the maximum value for byte
    }

    [TestMethod]
    public void TestGetMaxCharLength_SByte()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.SByte);
        Assert.AreEqual(4, maxLength); // -128 is the minimum value for sbyte (includes '-' sign)
    }

    [TestMethod]
    public void TestGetMaxCharLength_Short()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int16);
        Assert.AreEqual(6, maxLength); // -32768 is the minimum value for short (includes '-' sign)
    }

    [TestMethod]
    public void TestGetMaxCharLength_UShort()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt16);
        Assert.AreEqual(5, maxLength); // 65535 is the maximum value for ushort
    }

    [TestMethod]
    public void TestGetMaxCharLength_Int()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int32);
        Assert.AreEqual(11, maxLength); // -2147483648 is the minimum value for int (includes '-' sign)
    }

    [TestMethod]
    public void TestGetMaxCharLength_UInt()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt32);
        Assert.AreEqual(10, maxLength); // 4294967295 is the maximum value for uint
    }

    [TestMethod]
    public void TestGetMaxCharLength_Long()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int64);
        Assert.AreEqual(20, maxLength); // -9223372036854775808 is the minimum value for long (includes '-' sign)
    }

    [TestMethod]
    public void TestGetMaxCharLength_ULong()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt64);
        Assert.AreEqual(20, maxLength); // 18446744073709551615 is the maximum value for ulong
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public void TestGetMaxCharLength_Int128()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int128);
        Assert.AreEqual(40, maxLength); // -170141183460469231731687303715884105728 is the minimum value for Int128 (includes '-' sign)
    }

    [TestMethod]
    public void TestGetMaxCharLength_UInt128()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt128);
        Assert.AreEqual(39, maxLength); // 340282366920938463463374607431768211455 is the maximum value for UInt128
    }

    [TestMethod]
    public void TestGetMaxCharLength_Half()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Half);
        Assert.AreEqual(7, maxLength); // 65504 is the maximum value for Half
    }
#endif

    [TestMethod]
    public void TestGetMaxCharLength_Float()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Single);
        Assert.AreEqual(47, maxLength); // 340282350000000000000000000000000000000
    }

    [TestMethod]
    public void TestGetMaxCharLength_Double()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Double);
        Assert.AreEqual(324, maxLength); // Max value for double
    }

    [TestMethod]
    public void TestGetMaxCharLength_Decimal()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Decimal);
        Assert.AreEqual(29, maxLength); // Max value for decimal
    }
}