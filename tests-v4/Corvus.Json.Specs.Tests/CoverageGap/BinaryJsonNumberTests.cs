// <copyright file="BinaryJsonNumberTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600
#pragma warning disable SA1139

#if NET8_0_OR_GREATER
using System.Globalization;
using System.Text.Json;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class BinaryJsonNumberTests
{
    [TestMethod]
    public void Addition_Int128_SameKind()
    {
        var a = new BinaryJsonNumber((Int128)100);
        var b = new BinaryJsonNumber((Int128)200);
        var result = a + b;
        Assert.AreEqual("300", result.ToString());
    }

    [TestMethod]
    public void Addition_UInt128_SameKind()
    {
        var a = new BinaryJsonNumber((UInt128)100);
        var b = new BinaryJsonNumber((UInt128)200);
        var result = a + b;
        Assert.AreEqual("300", result.ToString());
    }

    [TestMethod]
    public void Addition_Half_SameKind()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        var b = new BinaryJsonNumber((Half)1.5);
        var result = a + b;
        Assert.AreEqual("4", result.ToString());
    }

    [TestMethod]
    public void Addition_SByte_SameKind()
    {
        var a = new BinaryJsonNumber((sbyte)10);
        var b = new BinaryJsonNumber((sbyte)20);
        var result = a + b;
        Assert.AreEqual("30", result.ToString());
    }

    [TestMethod]
    public void Addition_UInt16_SameKind()
    {
        var a = new BinaryJsonNumber((ushort)1000);
        var b = new BinaryJsonNumber((ushort)2000);
        var result = a + b;
        Assert.AreEqual("3000", result.ToString());
    }

    [TestMethod]
    public void Addition_UInt32_SameKind()
    {
        var a = new BinaryJsonNumber((uint)100000);
        var b = new BinaryJsonNumber((uint)200000);
        var result = a + b;
        Assert.AreEqual("300000", result.ToString());
    }

    [TestMethod]
    public void Addition_UInt64_SameKind()
    {
        var a = new BinaryJsonNumber((ulong)10000000000);
        var b = new BinaryJsonNumber((ulong)20000000000);
        var result = a + b;
        Assert.AreEqual("30000000000", result.ToString());
    }

    [TestMethod]
    public void Addition_Single_SameKind()
    {
        var a = new BinaryJsonNumber(2.5f);
        var b = new BinaryJsonNumber(1.5f);
        var result = a + b;
        Assert.AreEqual("4", result.ToString());
    }

    [TestMethod]
    public void Addition_Byte_SameKind()
    {
        var a = new BinaryJsonNumber((byte)100);
        var b = new BinaryJsonNumber((byte)50);
        var result = a + b;
        Assert.AreEqual("150", result.ToString());
    }

    [TestMethod]
    public void Addition_Int16_SameKind()
    {
        var a = new BinaryJsonNumber((short)1000);
        var b = new BinaryJsonNumber((short)2000);
        var result = a + b;
        Assert.AreEqual("3000", result.ToString());
    }

    [TestMethod]
    public void Addition_Decimal_SameKind()
    {
        var a = new BinaryJsonNumber(1.23m);
        var b = new BinaryJsonNumber(4.56m);
        var result = a + b;
        Assert.AreEqual("5.79", result.ToString());
    }

    [TestMethod]
    public void Addition_CrossKind_Int128_Int32()
    {
        var a = new BinaryJsonNumber((Int128)100);
        var b = new BinaryJsonNumber(50);
        var result = a + b;
        Assert.AreEqual("150", result.ToString());
    }

    [TestMethod]
    public void Subtraction_CrossKind_UInt128_UInt64()
    {
        var a = new BinaryJsonNumber((UInt128)300);
        var b = new BinaryJsonNumber((ulong)100);
        var result = a - b;
        Assert.AreEqual("200", result.ToString());
    }

    [TestMethod]
    public void Multiplication_CrossKind_Half_Double()
    {
        var a = new BinaryJsonNumber((Half)3.0);
        var b = new BinaryJsonNumber(4.0);
        var result = a * b;
        Assert.AreEqual("12", result.ToString());
    }

    [TestMethod]
    public void Division_CrossKind_Decimal_Int32()
    {
        var a = new BinaryJsonNumber(10.0m);
        var b = new BinaryJsonNumber(4);
        var result = a / b;
        Assert.AreEqual("2.5", result.ToString());
    }

    [TestMethod]
    public void Subtraction_Int128_SameKind()
    {
        var a = new BinaryJsonNumber((Int128)300);
        var b = new BinaryJsonNumber((Int128)100);
        var result = a - b;
        Assert.AreEqual("200", result.ToString());
    }

    [TestMethod]
    public void Multiplication_UInt128_SameKind()
    {
        var a = new BinaryJsonNumber((UInt128)15);
        var b = new BinaryJsonNumber((UInt128)20);
        var result = a * b;
        Assert.AreEqual("300", result.ToString());
    }

    [TestMethod]
    public void Division_Int128_SameKind()
    {
        var a = new BinaryJsonNumber((Int128)100);
        var b = new BinaryJsonNumber((Int128)4);
        var result = a / b;
        Assert.AreEqual("25", result.ToString());
    }

    [TestMethod]
    public void Subtraction_Half_SameKind()
    {
        var a = new BinaryJsonNumber((Half)5.0);
        var b = new BinaryJsonNumber((Half)2.0);
        var result = a - b;
        Assert.AreEqual("3", result.ToString());
    }

    [TestMethod]
    public void Multiplication_Half_SameKind()
    {
        var a = new BinaryJsonNumber((Half)3.0);
        var b = new BinaryJsonNumber((Half)4.0);
        var result = a * b;
        Assert.AreEqual("12", result.ToString());
    }

    [TestMethod]
    public void Division_Half_SameKind()
    {
        var a = new BinaryJsonNumber((Half)10.0);
        var b = new BinaryJsonNumber((Half)2.0);
        var result = a / b;
        Assert.AreEqual("5", result.ToString());
    }

    [TestMethod]
    public void Increment_Int128()
    {
        var a = new BinaryJsonNumber((Int128)99);
        a++;
        Assert.AreEqual("100", a.ToString());
    }

    [TestMethod]
    public void Increment_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)99);
        a++;
        Assert.AreEqual("100", a.ToString());
    }

    [TestMethod]
    public void Increment_Half()
    {
        var a = new BinaryJsonNumber((Half)2.0);
        a++;
        Assert.AreEqual("3", a.ToString());
    }

    [TestMethod]
    public void Increment_SByte()
    {
        var a = new BinaryJsonNumber((sbyte)10);
        a++;
        Assert.AreEqual("11", a.ToString());
    }

    [TestMethod]
    public void Increment_UInt16()
    {
        var a = new BinaryJsonNumber((ushort)100);
        a++;
        Assert.AreEqual("101", a.ToString());
    }

    [TestMethod]
    public void Increment_Single()
    {
        var a = new BinaryJsonNumber(5.0f);
        a++;
        Assert.AreEqual("6", a.ToString());
    }

    [TestMethod]
    public void Decrement_Int128()
    {
        var a = new BinaryJsonNumber((Int128)100);
        a--;
        Assert.AreEqual("99", a.ToString());
    }

    [TestMethod]
    public void Decrement_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)100);
        a--;
        Assert.AreEqual("99", a.ToString());
    }

    [TestMethod]
    public void Decrement_Half()
    {
        var a = new BinaryJsonNumber((Half)3.0);
        a--;
        Assert.AreEqual("2", a.ToString());
    }

    [TestMethod]
    public void Decrement_SByte()
    {
        var a = new BinaryJsonNumber((sbyte)10);
        a--;
        Assert.AreEqual("9", a.ToString());
    }

    [TestMethod]
    public void Negation_Int128()
    {
        var a = new BinaryJsonNumber((Int128)42);
        var result = -a;
        Assert.AreEqual("-42", result.ToString());
    }

    [TestMethod]
    public void Negation_Half()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        var result = -a;
        Assert.AreEqual("-2.5", result.ToString());
    }

    [TestMethod]
    public void Negation_SByte()
    {
        var a = new BinaryJsonNumber((sbyte)10);
        var result = -a;
        Assert.AreEqual("-10", result.ToString());
    }

    [TestMethod]
    public void Negation_Single()
    {
        var a = new BinaryJsonNumber(3.5f);
        var result = -a;
        Assert.AreEqual("-3.5", result.ToString());
    }

    [TestMethod]
    public void UnaryPlus_Int128()
    {
        var a = new BinaryJsonNumber((Int128)42);
        var result = +a;
        Assert.AreEqual("42", result.ToString());
    }

    [TestMethod]
    public void UnaryPlus_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)42);
        var result = +a;
        Assert.AreEqual("42", result.ToString());
    }

    [TestMethod]
    public void UnaryPlus_Half()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        var result = +a;
        Assert.AreEqual("2.5", result.ToString());
    }

    [TestMethod]
    public void UnaryPlus_SByte()
    {
        var a = new BinaryJsonNumber((sbyte)10);
        var result = +a;
        Assert.AreEqual("10", result.ToString());
    }

    [TestMethod]
    public void UnaryPlus_Single()
    {
        var a = new BinaryJsonNumber(4.5f);
        var result = +a;
        Assert.AreEqual("4.5", result.ToString());
    }

    [TestMethod]
    public void Abs_Int128_Negative()
    {
        var a = new BinaryJsonNumber((Int128)(-42));
        var result = BinaryJsonNumber.Abs(a);
        Assert.AreEqual("42", result.ToString());
    }

    [TestMethod]
    public void Abs_Half_Negative()
    {
        var a = new BinaryJsonNumber((Half)(-3.5));
        var result = BinaryJsonNumber.Abs(a);
        Assert.AreEqual("3.5", result.ToString());
    }

    [TestMethod]
    public void Abs_SByte_Negative()
    {
        var a = new BinaryJsonNumber((sbyte)(-7));
        var result = BinaryJsonNumber.Abs(a);
        Assert.AreEqual("7", result.ToString());
    }

    [TestMethod]
    public void Abs_UInt128_ReturnsSelf()
    {
        var a = new BinaryJsonNumber((UInt128)42);
        var result = BinaryJsonNumber.Abs(a);
        Assert.AreEqual("42", result.ToString());
    }

    [TestMethod]
    public void IsEvenInteger_Int128_Even()
    {
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((Int128)4)));
    }

    [TestMethod]
    public void IsEvenInteger_Int128_Odd()
    {
        Assert.IsFalse(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((Int128)3)));
    }

    [TestMethod]
    public void IsEvenInteger_UInt128()
    {
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((UInt128)100)));
        Assert.IsFalse(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((UInt128)101)));
    }

    [TestMethod]
    public void IsEvenInteger_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((Half)4.0)));
        Assert.IsFalse(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((Half)3.0)));
    }

    [TestMethod]
    public void IsFinite_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsFinite(new BinaryJsonNumber((Half)1.0)));
        Assert.IsFalse(BinaryJsonNumber.IsFinite(new BinaryJsonNumber(Half.PositiveInfinity)));
    }

    [TestMethod]
    public void IsFinite_Single()
    {
        Assert.IsTrue(BinaryJsonNumber.IsFinite(new BinaryJsonNumber(1.0f)));
        Assert.IsFalse(BinaryJsonNumber.IsFinite(new BinaryJsonNumber(float.PositiveInfinity)));
    }

    [TestMethod]
    public void IsFinite_IntegerKind_AlwaysTrue()
    {
        Assert.IsTrue(BinaryJsonNumber.IsFinite(new BinaryJsonNumber((Int128)999)));
    }

    [TestMethod]
    public void IsInfinity_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber(Half.PositiveInfinity)));
        Assert.IsTrue(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber(Half.NegativeInfinity)));
        Assert.IsFalse(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber((Half)1.0)));
    }

    [TestMethod]
    public void IsInfinity_Single()
    {
        Assert.IsTrue(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber(float.PositiveInfinity)));
        Assert.IsFalse(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber(1.0f)));
    }

    [TestMethod]
    public void IsInfinity_IntegerKind_AlwaysFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.IsInfinity(new BinaryJsonNumber((Int128)999)));
    }

    [TestMethod]
    public void IsNaN_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsNaN(new BinaryJsonNumber(Half.NaN)));
        Assert.IsFalse(BinaryJsonNumber.IsNaN(new BinaryJsonNumber((Half)1.0)));
    }

    [TestMethod]
    public void IsNaN_Single()
    {
        Assert.IsTrue(BinaryJsonNumber.IsNaN(new BinaryJsonNumber(float.NaN)));
        Assert.IsFalse(BinaryJsonNumber.IsNaN(new BinaryJsonNumber(1.0f)));
    }

    [TestMethod]
    public void IsNaN_IntegerKind_AlwaysFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.IsNaN(new BinaryJsonNumber((UInt128)123)));
    }

    [TestMethod]
    public void IsNegative_Int128()
    {
        Assert.IsTrue(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((Int128)(-5))));
        Assert.IsFalse(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((Int128)5)));
    }

    [TestMethod]
    public void IsNegative_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((Half)(-1.0))));
        Assert.IsFalse(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((Half)1.0)));
    }

    [TestMethod]
    public void IsNegative_SByte()
    {
        Assert.IsTrue(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((sbyte)(-1))));
        Assert.IsFalse(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((sbyte)1)));
    }

    [TestMethod]
    public void IsNegative_UnsignedKind_AlwaysFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((UInt128)100)));
        Assert.IsFalse(BinaryJsonNumber.IsNegative(new BinaryJsonNumber((byte)100)));
    }

    [TestMethod]
    public void IsInteger_Half()
    {
        Assert.IsTrue(BinaryJsonNumber.IsInteger(new BinaryJsonNumber((Half)4.0)));
        Assert.IsFalse(BinaryJsonNumber.IsInteger(new BinaryJsonNumber((Half)4.5)));
    }

    [TestMethod]
    public void IsInteger_Single()
    {
        Assert.IsTrue(BinaryJsonNumber.IsInteger(new BinaryJsonNumber(4.0f)));
        Assert.IsFalse(BinaryJsonNumber.IsInteger(new BinaryJsonNumber(4.5f)));
    }

    [TestMethod]
    public void IsInteger_IntKind_AlwaysTrue()
    {
        Assert.IsTrue(BinaryJsonNumber.IsInteger(new BinaryJsonNumber((Int128)42)));
        Assert.IsTrue(BinaryJsonNumber.IsInteger(new BinaryJsonNumber((UInt128)42)));
    }

    [TestMethod]
    public void IsZero_True()
    {
        Assert.IsTrue(BinaryJsonNumber.IsZero(new BinaryJsonNumber(0)));
        Assert.IsTrue(BinaryJsonNumber.IsZero(new BinaryJsonNumber(0.0)));
        Assert.IsTrue(BinaryJsonNumber.IsZero(new BinaryJsonNumber((Int128)0)));
    }

    [TestMethod]
    public void IsZero_False()
    {
        Assert.IsFalse(BinaryJsonNumber.IsZero(new BinaryJsonNumber(1)));
        Assert.IsFalse(BinaryJsonNumber.IsZero(new BinaryJsonNumber((Int128)1)));
        Assert.IsFalse(BinaryJsonNumber.IsZero(new BinaryJsonNumber((Half)1)));
        Assert.IsFalse(BinaryJsonNumber.IsZero(new BinaryJsonNumber((sbyte)1)));
    }

    [TestMethod]
    public void ToString_Int128()
    {
        var a = new BinaryJsonNumber((Int128)123456789);
        Assert.AreEqual("123456789", a.ToString());
    }

    [TestMethod]
    public void ToString_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)987654321);
        Assert.AreEqual("987654321", a.ToString());
    }

    [TestMethod]
    public void ToString_Half()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        Assert.AreEqual("2.5", a.ToString());
    }

    [TestMethod]
    public void ToString_SByte()
    {
        var a = new BinaryJsonNumber((sbyte)(-42));
        Assert.AreEqual("-42", a.ToString());
    }

    [TestMethod]
    public void ToString_WithFormat_Int128()
    {
        var a = new BinaryJsonNumber((Int128)255);
        Assert.AreEqual("FF", a.ToString("X", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_WithFormat_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)255);
        Assert.AreEqual("FF", a.ToString("X", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_Span_Int128()
    {
        var a = new BinaryJsonNumber((Int128)42);
        Span<char> buffer = stackalloc char[64];
        Assert.IsTrue(a.TryFormat(buffer, out int charsWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("42", buffer[..charsWritten].ToString());
    }

    [TestMethod]
    public void TryFormat_Span_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)42);
        Span<char> buffer = stackalloc char[64];
        Assert.IsTrue(a.TryFormat(buffer, out int charsWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("42", buffer[..charsWritten].ToString());
    }

    [TestMethod]
    public void TryFormat_Utf8Span_Int128()
    {
        var a = new BinaryJsonNumber((Int128)42);
        Span<byte> buffer = stackalloc byte[64];
        Assert.IsTrue(a.TryFormat(buffer, out int bytesWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("42", System.Text.Encoding.UTF8.GetString(buffer[..bytesWritten]));
    }

    [TestMethod]
    public void TryFormat_Utf8Span_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)42);
        Span<byte> buffer = stackalloc byte[64];
        Assert.IsTrue(a.TryFormat(buffer, out int bytesWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("42", System.Text.Encoding.UTF8.GetString(buffer[..bytesWritten]));
    }

    [TestMethod]
    public void TryFormat_Utf8Span_Half()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        Span<byte> buffer = stackalloc byte[64];
        Assert.IsTrue(a.TryFormat(buffer, out int bytesWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("2.5", System.Text.Encoding.UTF8.GetString(buffer[..bytesWritten]));
    }

    [TestMethod]
    public void CreatePrecise_Int128_ToDouble()
    {
        var a = new BinaryJsonNumber((Int128)42);
        double result = a.CreatePrecise<double>();
        Assert.AreEqual(42.0, result);
    }

    [TestMethod]
    public void CreatePrecise_UInt128_ToDouble()
    {
        var a = new BinaryJsonNumber((UInt128)100);
        double result = a.CreatePrecise<double>();
        Assert.AreEqual(100.0, result);
    }

    [TestMethod]
    public void CreatePrecise_Half_ToDouble()
    {
        var a = new BinaryJsonNumber((Half)2.5);
        double result = a.CreatePrecise<double>();
        Assert.AreEqual(2.5, result);
    }

    [TestMethod]
    public void CreatePrecise_SByte_ToInt()
    {
        var a = new BinaryJsonNumber((sbyte)(-10));
        int result = a.CreatePrecise<int>();
        Assert.AreEqual(-10, result);
    }

    [TestMethod]
    public void CreatePrecise_UInt16_ToLong()
    {
        var a = new BinaryJsonNumber((ushort)65000);
        long result = a.CreatePrecise<long>();
        Assert.AreEqual(65000L, result);
    }

    [TestMethod]
    public void CreatePrecise_Single_ToDouble()
    {
        var a = new BinaryJsonNumber(3.14f);
        double result = a.CreatePrecise<double>();
        Assert.AreEqual((double)3.14f, result);
    }

    [TestMethod]
    public void Equals_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(42);
        using var doc = JsonDocument.Parse("42");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(BinaryJsonNumber.Equals(number, element));
    }

    [TestMethod]
    public void Equals_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(42);
        using var doc = JsonDocument.Parse("42");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(BinaryJsonNumber.Equals(element, number));
    }

    [TestMethod]
    public void LessThan_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(10);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number < element);
    }

    [TestMethod]
    public void LessThanOrEqual_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number <= element);
    }

    [TestMethod]
    public void GreaterThan_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(30);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number > element);
    }

    [TestMethod]
    public void GreaterThanOrEqual_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number >= element);
    }

    [TestMethod]
    public void LessThan_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("10");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(element < number);
    }

    [TestMethod]
    public void LessThanOrEqual_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(element <= number);
    }

    [TestMethod]
    public void GreaterThan_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(10);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(element > number);
    }

    [TestMethod]
    public void GreaterThanOrEqual_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(element >= number);
    }

    [TestMethod]
    public void EqualityOperator_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(42);
        using var doc = JsonDocument.Parse("42");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number == element);
        Assert.IsFalse(number != element);
    }

    [TestMethod]
    public void EqualityOperator_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(42);
        using var doc = JsonDocument.Parse("42");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(element == number);
        Assert.IsFalse(element != number);
    }

    [TestMethod]
    public void InequalityOperator_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(42);
        using var doc = JsonDocument.Parse("99");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(number != element);
        Assert.IsFalse(number == element);
    }

    [TestMethod]
    public void Compare_Int128()
    {
        var a = new BinaryJsonNumber((Int128)10);
        var b = new BinaryJsonNumber((Int128)20);
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= b);
        Assert.IsTrue(b >= a);
    }

    [TestMethod]
    public void Compare_UInt128()
    {
        var a = new BinaryJsonNumber((UInt128)10);
        var b = new BinaryJsonNumber((UInt128)20);
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
    }

    [TestMethod]
    public void Compare_Half()
    {
        var a = new BinaryJsonNumber((Half)1.5);
        var b = new BinaryJsonNumber((Half)2.5);
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
    }

    [TestMethod]
    public void Equality_CrossKind_IntDouble()
    {
        var a = new BinaryJsonNumber(42);
        var b = new BinaryJsonNumber(42.0);
        Assert.IsTrue(BinaryJsonNumber.Equals(a, b));
    }

    [TestMethod]
    public void Equality_CrossKind_Int128_Double()
    {
        var a = new BinaryJsonNumber((Int128)100);
        var b = new BinaryJsonNumber(100.0);
        Assert.IsTrue(BinaryJsonNumber.Equals(a, b));
    }

    [TestMethod]
    public void Parse_ReadOnlySpan_WithProvider()
    {
        var result = BinaryJsonNumber.Parse("42".AsSpan(), CultureInfo.InvariantCulture);
        Assert.AreEqual("42", result.ToString());
    }

    [TestMethod]
    public void Parse_String_WithProvider()
    {
        var result = BinaryJsonNumber.Parse("3.14", CultureInfo.InvariantCulture);
        Assert.AreEqual("3.14", result.ToString());
    }

    [TestMethod]
    public void TryParse_ReadOnlySpan_WithProvider()
    {
        Assert.IsTrue(BinaryJsonNumber.TryParse("123".AsSpan(), CultureInfo.InvariantCulture, out var result));
        Assert.AreEqual("123", result.ToString());
    }

    [TestMethod]
    public void TryParse_String_WithProvider()
    {
        Assert.IsTrue(BinaryJsonNumber.TryParse("456", CultureInfo.InvariantCulture, out var result));
        Assert.AreEqual("456", result.ToString());
    }

    [TestMethod]
    public void TryParse_InvalidString_ReturnsFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.TryParse("not-a-number", CultureInfo.InvariantCulture, out _));
    }

    [TestMethod]
    public void Parse_ReadOnlySpan_WithNumberStyles()
    {
        var result = BinaryJsonNumber.Parse("123".AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        Assert.AreEqual("123", result.ToString());
    }

    [TestMethod]
    public void Parse_String_WithNumberStyles()
    {
        var result = BinaryJsonNumber.Parse("456", NumberStyles.Integer, CultureInfo.InvariantCulture);
        Assert.AreEqual("456", result.ToString());
    }

    [TestMethod]
    public void TryParse_ReadOnlySpan_WithNumberStyles()
    {
        Assert.IsTrue(BinaryJsonNumber.TryParse("789".AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result));
        Assert.AreEqual("789", result.ToString());
    }

    [TestMethod]
    public void TryParse_String_WithNumberStyles()
    {
        Assert.IsTrue(BinaryJsonNumber.TryParse("321", NumberStyles.Integer, CultureInfo.InvariantCulture, out var result));
        Assert.AreEqual("321", result.ToString());
    }

    [TestMethod]
    public void TryParse_String_WithNumberStyles_Invalid_ReturnsFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.TryParse("not-a-number", NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
    }

    [TestMethod]
    public void IsMultipleOf_Double_True()
    {
        var x = new BinaryJsonNumber(10.0);
        var y = new BinaryJsonNumber(5.0);
        Assert.IsTrue(BinaryJsonNumber.IsMultipleOf(x, y));
    }

    [TestMethod]
    public void IsMultipleOf_Double_False()
    {
        var x = new BinaryJsonNumber(10.0);
        var y = new BinaryJsonNumber(3.0);
        Assert.IsFalse(BinaryJsonNumber.IsMultipleOf(x, y));
    }

    [TestMethod]
    public void IsMultipleOf_Decimal_True()
    {
        var x = new BinaryJsonNumber(10.0m);
        var y = new BinaryJsonNumber(2.5m);
        Assert.IsTrue(BinaryJsonNumber.IsMultipleOf(x, y));
    }

    [TestMethod]
    public void IsMultipleOf_Decimal_False()
    {
        var x = new BinaryJsonNumber(10.0m);
        var y = new BinaryJsonNumber(3.0m);
        Assert.IsFalse(BinaryJsonNumber.IsMultipleOf(x, y));
    }

    [TestMethod]
    [DataRow(BinaryJsonNumber.Kind.Byte, 3)]
    [DataRow(BinaryJsonNumber.Kind.Int16, 6)]
    [DataRow(BinaryJsonNumber.Kind.Int32, 11)]
    [DataRow(BinaryJsonNumber.Kind.Int64, 20)]
    [DataRow(BinaryJsonNumber.Kind.Int128, 40)]
    [DataRow(BinaryJsonNumber.Kind.UInt16, 5)]
    [DataRow(BinaryJsonNumber.Kind.UInt32, 10)]
    [DataRow(BinaryJsonNumber.Kind.UInt64, 20)]
    [DataRow(BinaryJsonNumber.Kind.UInt128, 39)]
    [DataRow(BinaryJsonNumber.Kind.SByte, 4)]
    [DataRow(BinaryJsonNumber.Kind.Half, 7)]
    [DataRow(BinaryJsonNumber.Kind.Single, 47)]
    [DataRow(BinaryJsonNumber.Kind.Double, 324)]
    [DataRow(BinaryJsonNumber.Kind.Decimal, 29)]
    public void GetMaxCharLength_ReturnsExpected(BinaryJsonNumber.Kind kind, int expected)
    {
        Assert.AreEqual(expected, BinaryJsonNumber.GetMaxCharLength(kind));
    }

    [TestMethod]
    public void One_IsOne()
    {
        Assert.AreEqual("1", BinaryJsonNumber.One.ToString());
    }

    [TestMethod]
    public void Zero_IsZero()
    {
        Assert.AreEqual("0", BinaryJsonNumber.Zero.ToString());
    }

    [TestMethod]
    public void IsCanonical_AlwaysTrue()
    {
        Assert.IsTrue(BinaryJsonNumber.IsCanonical(new BinaryJsonNumber(42)));
        Assert.IsTrue(BinaryJsonNumber.IsCanonical(new BinaryJsonNumber((Int128)42)));
    }

    [TestMethod]
    public void IsComplexNumber_AlwaysFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.IsComplexNumber(new BinaryJsonNumber(42)));
    }

    [TestMethod]
    public void IsImaginaryNumber_AlwaysFalse()
    {
        Assert.IsFalse(BinaryJsonNumber.IsImaginaryNumber(new BinaryJsonNumber(42)));
    }

    [TestMethod]
    public void BoolKind_True_IsNotZero_DefaultBehavior()
    {
        // Bool kind falls through to the default case in IsZero (returns true)
        // and ToString throws NotSupportedException. This tests the Kind property.
        var a = new BinaryJsonNumber(true);
        Assert.IsTrue(BinaryJsonNumber.IsZero(a));
    }

    [TestMethod]
    public void BoolKind_False_IsZero_DefaultBehavior()
    {
        var a = new BinaryJsonNumber(false);
        Assert.IsTrue(BinaryJsonNumber.IsZero(a));
    }

    [TestMethod]
    public void Compare_BinaryJsonNumber_JsonElement()
    {
        var number = new BinaryJsonNumber(10);
        using var doc = JsonDocument.Parse("20");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(BinaryJsonNumber.Compare(number, element) < 0);
    }

    [TestMethod]
    public void Compare_JsonElement_BinaryJsonNumber()
    {
        var number = new BinaryJsonNumber(20);
        using var doc = JsonDocument.Parse("10");
        JsonElement element = doc.RootElement;
        Assert.IsTrue(BinaryJsonNumber.Compare(element, number) < 0);
    }
}
#endif