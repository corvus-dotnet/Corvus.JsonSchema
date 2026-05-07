// <copyright file="BinaryJsonNumberFormat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable

#if NET8_0_OR_GREATER

using System.Globalization;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.BinaryJsonNumberTests;

public class BinaryJsonNumberTryFormatTests
{
    [Fact]
    public void TestTryFormat_Success()
    {
        var number = new BinaryJsonNumber(123.45);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(6, charsWritten);
        Assert.Equal("123.45", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_InsufficientBuffer()
    {
        var number = new BinaryJsonNumber(123.45);
        Span<char> destination = stackalloc char[5]; // Insufficient buffer size
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TestTryFormat_NegativeNumber()
    {
        var number = new BinaryJsonNumber(-123.45);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(7, charsWritten);
        Assert.Equal("-123.45", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Zero()
    {
        var number = new BinaryJsonNumber(0);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(1, charsWritten);
        Assert.Equal("0", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_LargeNumber()
    {
        var number = new BinaryJsonNumber(1234567890.12345);
        Span<char> destination = stackalloc char[20];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(16, charsWritten);
        Assert.Equal("1234567890.12345", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Byte()
    {
        var number = new BinaryJsonNumber((byte)123);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(3, charsWritten);
        Assert.Equal("123", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Decimal()
    {
        var number = new BinaryJsonNumber(123.45m);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(6, charsWritten);
        Assert.Equal("123.45", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Double()
    {
        var number = new BinaryJsonNumber(123.45);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(6, charsWritten);
        Assert.Equal("123.45", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Half()
    {
        var number = new BinaryJsonNumber((Half)123.44);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(6, charsWritten);
        Assert.Equal("123.44", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Int16()
    {
        var number = new BinaryJsonNumber((short)12345);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(5, charsWritten);
        Assert.Equal("12345", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Int32()
    {
        var number = new BinaryJsonNumber(12345);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(5, charsWritten);
        Assert.Equal("12345", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Int64()
    {
        var number = new BinaryJsonNumber(1234567890L);
        Span<char> destination = stackalloc char[20];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(10, charsWritten);
        Assert.Equal("1234567890", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Int128()
    {
        var number = new BinaryJsonNumber((Int128)12345678901234567890);
        Span<char> destination = stackalloc char[40];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(20, charsWritten);
        Assert.Equal("12345678901234567890", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_SByte()
    {
        var number = new BinaryJsonNumber((sbyte)123);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(3, charsWritten);
        Assert.Equal("123", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_Single()
    {
        var number = new BinaryJsonNumber(123.45f);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(6, charsWritten);
        Assert.Equal("123.45", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_UInt16()
    {
        var number = new BinaryJsonNumber((ushort)12345);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(5, charsWritten);
        Assert.Equal("12345", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_UInt32()
    {
        var number = new BinaryJsonNumber(12345U);
        Span<char> destination = stackalloc char[10];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(5, charsWritten);
        Assert.Equal("12345", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_UInt64()
    {
        var number = new BinaryJsonNumber(1234567890UL);
        Span<char> destination = stackalloc char[20];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(10, charsWritten);
        Assert.Equal("1234567890", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TestTryFormat_UInt128()
    {
        var number = new BinaryJsonNumber((UInt128)12345678901234567890);
        Span<char> destination = stackalloc char[40];
        bool result = number.TryFormat(destination, out int charsWritten, default, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(20, charsWritten);
        Assert.Equal("12345678901234567890", destination.Slice(0, charsWritten).ToString());
    }
}
#endif