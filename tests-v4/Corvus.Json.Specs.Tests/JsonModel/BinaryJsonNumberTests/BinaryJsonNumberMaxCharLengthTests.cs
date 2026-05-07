// <copyright file="BinaryJsonNumberMaxCharLengthTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.BinaryJsonNumberTests;

public class BinaryJsonNumberMaxCharLengthTests
{
    [Fact]
    public void TestGetMaxCharLength_Byte()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Byte);
        Assert.Equal(3, maxLength); // 255 is the maximum value for byte
    }

    [Fact]
    public void TestGetMaxCharLength_SByte()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.SByte);
        Assert.Equal(4, maxLength); // -128 is the minimum value for sbyte (includes '-' sign)
    }

    [Fact]
    public void TestGetMaxCharLength_Short()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int16);
        Assert.Equal(6, maxLength); // -32768 is the minimum value for short (includes '-' sign)
    }

    [Fact]
    public void TestGetMaxCharLength_UShort()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt16);
        Assert.Equal(5, maxLength); // 65535 is the maximum value for ushort
    }

    [Fact]
    public void TestGetMaxCharLength_Int()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int32);
        Assert.Equal(11, maxLength); // -2147483648 is the minimum value for int (includes '-' sign)
    }

    [Fact]
    public void TestGetMaxCharLength_UInt()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt32);
        Assert.Equal(10, maxLength); // 4294967295 is the maximum value for uint
    }

    [Fact]
    public void TestGetMaxCharLength_Long()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int64);
        Assert.Equal(20, maxLength); // -9223372036854775808 is the minimum value for long (includes '-' sign)
    }

    [Fact]
    public void TestGetMaxCharLength_ULong()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt64);
        Assert.Equal(20, maxLength); // 18446744073709551615 is the maximum value for ulong
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void TestGetMaxCharLength_Int128()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Int128);
        Assert.Equal(40, maxLength); // -170141183460469231731687303715884105728 is the minimum value for Int128 (includes '-' sign)
    }

    [Fact]
    public void TestGetMaxCharLength_UInt128()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.UInt128);
        Assert.Equal(39, maxLength); // 340282366920938463463374607431768211455 is the maximum value for UInt128
    }

    [Fact]
    public void TestGetMaxCharLength_Half()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Half);
        Assert.Equal(7, maxLength); // 65504 is the maximum value for Half
    }
#endif

    [Fact]
    public void TestGetMaxCharLength_Float()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Single);
        Assert.Equal(47, maxLength); // 340282350000000000000000000000000000000
    }

    [Fact]
    public void TestGetMaxCharLength_Double()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Double);
        Assert.Equal(324, maxLength); // Max value for double
    }

    [Fact]
    public void TestGetMaxCharLength_Decimal()
    {
        int maxLength = BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Decimal);
        Assert.Equal(29, maxLength); // Max value for decimal
    }
}