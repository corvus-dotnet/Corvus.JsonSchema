// <copyright file="BinaryJsonNumberOperatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1139 // Use literal suffix notation instead of casting

#if NET8_0_OR_GREATER

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests for BinaryJsonNumber arithmetic operators, unary operators, and mixed-kind dispatch.
/// Targets uncovered lines in BinaryJsonNumber.Net8.cs (same-kind and mixed-kind arithmetic,
/// increment, decrement, unary negation, unary plus, equality/inequality).
/// </summary>
[TestClass]
public class BinaryJsonNumberOperatorTests
{
    // =========================
    // Unary negation operator -
    // =========================
    [TestMethod]
    public void UnaryMinus_Byte()
    {
        BinaryJsonNumber v = new((byte)5);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-5), neg);
    }

    [TestMethod]
    public void UnaryMinus_SByte()
    {
        BinaryJsonNumber v = new((sbyte)3);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)-3), neg);
    }

    [TestMethod]
    public void UnaryMinus_Int16()
    {
        BinaryJsonNumber v = new((short)100);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber((short)-100), neg);
    }

    [TestMethod]
    public void UnaryMinus_Int32()
    {
        BinaryJsonNumber v = new(42);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-42), neg);
    }

    [TestMethod]
    public void UnaryMinus_Int64()
    {
        BinaryJsonNumber v = new(999L);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-999L), neg);
    }

    [TestMethod]
    public void UnaryMinus_Int128()
    {
        BinaryJsonNumber v = new((Int128)42);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber((Int128)(-42)), neg);
    }

    [TestMethod]
    public void UnaryMinus_UInt16()
    {
        BinaryJsonNumber v = new((ushort)7);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-7), neg);
    }

    [TestMethod]
    public void UnaryMinus_UInt32()
    {
        BinaryJsonNumber v = new((uint)10);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-10L), neg);
    }

    [TestMethod]
    public void UnaryMinus_UInt64()
    {
        // This was a bug: UInt64 negation used to return the value unchanged.
        BinaryJsonNumber v = new((ulong)5);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber((Int128)(-5)), neg);
    }

    [TestMethod]
    public void UnaryMinus_Double()
    {
        BinaryJsonNumber v = new(3.14);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-3.14), neg);
    }

    [TestMethod]
    public void UnaryMinus_Single()
    {
        BinaryJsonNumber v = new(2.5f);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-2.5f), neg);
    }

    [TestMethod]
    public void UnaryMinus_Half()
    {
        BinaryJsonNumber v = new((Half)1.5);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-(Half)1.5), neg);
    }

    [TestMethod]
    public void UnaryMinus_Decimal()
    {
        BinaryJsonNumber v = new(7.77m);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-7.77m), neg);
    }

    [TestMethod]
    public void UnaryMinus_UInt128()
    {
        BinaryJsonNumber v = new((UInt128)42);
        BinaryJsonNumber neg = -v;
        Assert.AreEqual(new BinaryJsonNumber(-(UInt128)42), neg);
    }

    // =========================
    // Unary plus operator +
    // =========================
    [TestMethod]
    public void UnaryPlus_Int32()
    {
        BinaryJsonNumber v = new(42);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber(42), pos);
    }

    [TestMethod]
    public void UnaryPlus_Half()
    {
        BinaryJsonNumber v = new((Half)1.5);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((Half)1.5), pos);
    }

    [TestMethod]
    public void UnaryPlus_Int128()
    {
        BinaryJsonNumber v = new((Int128)99);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((Int128)99), pos);
    }

    [TestMethod]
    public void UnaryPlus_UInt128()
    {
        BinaryJsonNumber v = new((UInt128)99);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((UInt128)99), pos);
    }

    [TestMethod]
    public void UnaryPlus_SByte()
    {
        BinaryJsonNumber v = new((sbyte)-3);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)-3), pos);
    }

    [TestMethod]
    public void UnaryPlus_Byte()
    {
        BinaryJsonNumber v = new((byte)5);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((byte)5), pos);
    }

    [TestMethod]
    public void UnaryPlus_UInt16()
    {
        BinaryJsonNumber v = new((ushort)7);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((ushort)7), pos);
    }

    [TestMethod]
    public void UnaryPlus_UInt32()
    {
        BinaryJsonNumber v = new((uint)10);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((uint)10), pos);
    }

    [TestMethod]
    public void UnaryPlus_UInt64()
    {
        BinaryJsonNumber v = new((ulong)5);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((ulong)5), pos);
    }

    [TestMethod]
    public void UnaryPlus_Int16()
    {
        BinaryJsonNumber v = new((short)100);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber((short)100), pos);
    }

    [TestMethod]
    public void UnaryPlus_Single()
    {
        BinaryJsonNumber v = new(2.5f);
        BinaryJsonNumber pos = +v;
        Assert.AreEqual(new BinaryJsonNumber(2.5f), pos);
    }

    // =========================
    // Increment operator ++
    // =========================
    [TestMethod]
    public void Increment_Byte()
    {
        BinaryJsonNumber v = new((byte)5);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((byte)6), v);
    }

    [TestMethod]
    public void Increment_SByte()
    {
        BinaryJsonNumber v = new((sbyte)3);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)4), v);
    }

    [TestMethod]
    public void Increment_Int16()
    {
        BinaryJsonNumber v = new((short)99);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((short)100), v);
    }

    [TestMethod]
    public void Increment_Int64()
    {
        BinaryJsonNumber v = new(999L);
        v++;
        Assert.AreEqual(new BinaryJsonNumber(1000L), v);
    }

    [TestMethod]
    public void Increment_Int128()
    {
        BinaryJsonNumber v = new((Int128)42);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((Int128)43), v);
    }

    [TestMethod]
    public void Increment_UInt16()
    {
        BinaryJsonNumber v = new((ushort)7);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((ushort)8), v);
    }

    [TestMethod]
    public void Increment_UInt32()
    {
        BinaryJsonNumber v = new((uint)10);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((uint)11), v);
    }

    [TestMethod]
    public void Increment_UInt64()
    {
        BinaryJsonNumber v = new((ulong)5);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((ulong)6), v);
    }

    [TestMethod]
    public void Increment_UInt128()
    {
        BinaryJsonNumber v = new((UInt128)99);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((UInt128)100), v);
    }

    [TestMethod]
    public void Increment_Half()
    {
        BinaryJsonNumber v = new((Half)1.0);
        v++;
        Assert.AreEqual(new BinaryJsonNumber((Half)2.0), v);
    }

    [TestMethod]
    public void Increment_Single()
    {
        BinaryJsonNumber v = new(2.0f);
        v++;
        Assert.AreEqual(new BinaryJsonNumber(3.0f), v);
    }

    [TestMethod]
    public void Increment_Double()
    {
        BinaryJsonNumber v = new(3.0);
        v++;
        Assert.AreEqual(new BinaryJsonNumber(4.0), v);
    }

    [TestMethod]
    public void Increment_Decimal()
    {
        BinaryJsonNumber v = new(7.5m);
        v++;
        Assert.AreEqual(new BinaryJsonNumber(8.5m), v);
    }

    // =========================
    // Decrement operator --
    // =========================
    [TestMethod]
    public void Decrement_Byte()
    {
        BinaryJsonNumber v = new((byte)5);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((byte)4), v);
    }

    [TestMethod]
    public void Decrement_SByte()
    {
        BinaryJsonNumber v = new((sbyte)3);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)2), v);
    }

    [TestMethod]
    public void Decrement_Int16()
    {
        BinaryJsonNumber v = new((short)100);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((short)99), v);
    }

    [TestMethod]
    public void Decrement_Int64()
    {
        BinaryJsonNumber v = new(1000L);
        v--;
        Assert.AreEqual(new BinaryJsonNumber(999L), v);
    }

    [TestMethod]
    public void Decrement_Int128()
    {
        BinaryJsonNumber v = new((Int128)43);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((Int128)42), v);
    }

    [TestMethod]
    public void Decrement_UInt16()
    {
        BinaryJsonNumber v = new((ushort)8);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((ushort)7), v);
    }

    [TestMethod]
    public void Decrement_UInt32()
    {
        BinaryJsonNumber v = new((uint)11);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((uint)10), v);
    }

    [TestMethod]
    public void Decrement_UInt64()
    {
        BinaryJsonNumber v = new((ulong)6);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((ulong)5), v);
    }

    [TestMethod]
    public void Decrement_UInt128()
    {
        BinaryJsonNumber v = new((UInt128)100);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((UInt128)99), v);
    }

    [TestMethod]
    public void Decrement_Half()
    {
        BinaryJsonNumber v = new((Half)2.0);
        v--;
        Assert.AreEqual(new BinaryJsonNumber((Half)1.0), v);
    }

    [TestMethod]
    public void Decrement_Single()
    {
        BinaryJsonNumber v = new(3.0f);
        v--;
        Assert.AreEqual(new BinaryJsonNumber(2.0f), v);
    }

    [TestMethod]
    public void Decrement_Decimal()
    {
        BinaryJsonNumber v = new(8.5m);
        v--;
        Assert.AreEqual(new BinaryJsonNumber(7.5m), v);
    }

    // =========================
    // Equality and inequality
    // =========================
    [TestMethod]
    public void Equality_SameKind_Int128()
    {
        BinaryJsonNumber a = new((Int128)42);
        BinaryJsonNumber b = new((Int128)42);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Inequality_SameKind_UInt128()
    {
        BinaryJsonNumber a = new((UInt128)1);
        BinaryJsonNumber b = new((UInt128)2);
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    // ===================================
    // Same-kind addition (the fast path)
    // ===================================
    [TestMethod]
    public void Add_SameKind_Half()
    {
        BinaryJsonNumber a = new((Half)1.5);
        BinaryJsonNumber b = new((Half)2.5);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((Half)4.0), result);
    }

    [TestMethod]
    public void Add_SameKind_SByte()
    {
        BinaryJsonNumber a = new((sbyte)3);
        BinaryJsonNumber b = new((sbyte)4);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((sbyte)7), result);
    }

    [TestMethod]
    public void Add_SameKind_Byte()
    {
        BinaryJsonNumber a = new((byte)10);
        BinaryJsonNumber b = new((byte)20);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((byte)30), result);
    }

    [TestMethod]
    public void Add_SameKind_Int16()
    {
        BinaryJsonNumber a = new((short)100);
        BinaryJsonNumber b = new((short)200);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((short)300), result);
    }

    [TestMethod]
    public void Add_SameKind_UInt16()
    {
        BinaryJsonNumber a = new((ushort)1000);
        BinaryJsonNumber b = new((ushort)2000);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((ushort)3000), result);
    }

    [TestMethod]
    public void Add_SameKind_UInt32()
    {
        BinaryJsonNumber a = new((uint)100);
        BinaryJsonNumber b = new((uint)200);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((uint)300), result);
    }

    [TestMethod]
    public void Add_SameKind_Int128()
    {
        BinaryJsonNumber a = new((Int128)1000);
        BinaryJsonNumber b = new((Int128)2000);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((Int128)3000), result);
    }

    [TestMethod]
    public void Add_SameKind_UInt128()
    {
        BinaryJsonNumber a = new((UInt128)50);
        BinaryJsonNumber b = new((UInt128)75);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber((UInt128)125), result);
    }

    [TestMethod]
    public void Add_SameKind_Single()
    {
        BinaryJsonNumber a = new(1.5f);
        BinaryJsonNumber b = new(2.5f);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(4.0f), result);
    }

    // ===================================
    // Mixed-kind addition (fallback path)
    // ===================================
    [TestMethod]
    public void Add_MixedKind_Int32_Double()
    {
        BinaryJsonNumber a = new(10);
        BinaryJsonNumber b = new(2.5);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(12.5), result);
    }

    [TestMethod]
    public void Add_MixedKind_Half_Int32()
    {
        BinaryJsonNumber a = new((Half)1.0);
        BinaryJsonNumber b = new(5);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(6.0), result);
    }

    [TestMethod]
    public void Add_MixedKind_Int128_Double()
    {
        BinaryJsonNumber a = new((Int128)100);
        BinaryJsonNumber b = new(0.5);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(100.5), result);
    }

    [TestMethod]
    public void Add_MixedKind_UInt128_Int32()
    {
        BinaryJsonNumber a = new((UInt128)50);
        BinaryJsonNumber b = new(25);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(75.0), result);
    }

    [TestMethod]
    public void Add_MixedKind_SByte_UInt16()
    {
        BinaryJsonNumber a = new((sbyte)3);
        BinaryJsonNumber b = new((ushort)10);
        BinaryJsonNumber result = a + b;
        Assert.AreEqual(new BinaryJsonNumber(13.0), result);
    }

    // ============================================
    // Same-kind subtraction
    // ============================================
    [TestMethod]
    public void Subtract_SameKind_Half()
    {
        BinaryJsonNumber a = new((Half)5.0);
        BinaryJsonNumber b = new((Half)2.0);
        Assert.AreEqual(new BinaryJsonNumber((Half)3.0), a - b);
    }

    [TestMethod]
    public void Subtract_SameKind_SByte()
    {
        BinaryJsonNumber a = new((sbyte)10);
        BinaryJsonNumber b = new((sbyte)3);
        Assert.AreEqual(new BinaryJsonNumber((sbyte)7), a - b);
    }

    [TestMethod]
    public void Subtract_SameKind_Int128()
    {
        BinaryJsonNumber a = new((Int128)1000);
        BinaryJsonNumber b = new((Int128)500);
        Assert.AreEqual(new BinaryJsonNumber((Int128)500), a - b);
    }

    [TestMethod]
    public void Subtract_SameKind_UInt128()
    {
        BinaryJsonNumber a = new((UInt128)100);
        BinaryJsonNumber b = new((UInt128)30);
        Assert.AreEqual(new BinaryJsonNumber((UInt128)70), a - b);
    }

    // Mixed-kind subtraction
    [TestMethod]
    public void Subtract_MixedKind_Double_Int32()
    {
        BinaryJsonNumber a = new(10.5);
        BinaryJsonNumber b = new(3);
        Assert.AreEqual(new BinaryJsonNumber(7.5), a - b);
    }

    [TestMethod]
    public void Subtract_MixedKind_Int128_Half()
    {
        BinaryJsonNumber a = new((Int128)10);
        BinaryJsonNumber b = new((Half)2.0);
        Assert.AreEqual(new BinaryJsonNumber(8.0), a - b);
    }

    // ============================================
    // Same-kind multiplication
    // ============================================
    [TestMethod]
    public void Multiply_SameKind_Half()
    {
        BinaryJsonNumber a = new((Half)2.0);
        BinaryJsonNumber b = new((Half)3.0);
        Assert.AreEqual(new BinaryJsonNumber((Half)6.0), a * b);
    }

    [TestMethod]
    public void Multiply_SameKind_SByte()
    {
        BinaryJsonNumber a = new((sbyte)3);
        BinaryJsonNumber b = new((sbyte)4);
        Assert.AreEqual(new BinaryJsonNumber((sbyte)12), a * b);
    }

    [TestMethod]
    public void Multiply_SameKind_Int128()
    {
        BinaryJsonNumber a = new((Int128)100);
        BinaryJsonNumber b = new((Int128)20);
        Assert.AreEqual(new BinaryJsonNumber((Int128)2000), a * b);
    }

    [TestMethod]
    public void Multiply_SameKind_UInt128()
    {
        BinaryJsonNumber a = new((UInt128)10);
        BinaryJsonNumber b = new((UInt128)5);
        Assert.AreEqual(new BinaryJsonNumber((UInt128)50), a * b);
    }

    // Mixed-kind multiplication
    [TestMethod]
    public void Multiply_MixedKind_Int32_Half()
    {
        BinaryJsonNumber a = new(4);
        BinaryJsonNumber b = new((Half)2.5);
        Assert.AreEqual(new BinaryJsonNumber(10.0), a * b);
    }

    // ============================================
    // Same-kind division
    // ============================================
    [TestMethod]
    public void Divide_SameKind_Half()
    {
        BinaryJsonNumber a = new((Half)6.0);
        BinaryJsonNumber b = new((Half)2.0);
        Assert.AreEqual(new BinaryJsonNumber((Half)3.0), a / b);
    }

    [TestMethod]
    public void Divide_SameKind_SByte()
    {
        BinaryJsonNumber a = new((sbyte)12);
        BinaryJsonNumber b = new((sbyte)4);
        Assert.AreEqual(new BinaryJsonNumber((sbyte)3), a / b);
    }

    [TestMethod]
    public void Divide_SameKind_Int128()
    {
        BinaryJsonNumber a = new((Int128)100);
        BinaryJsonNumber b = new((Int128)5);
        Assert.AreEqual(new BinaryJsonNumber((Int128)20), a / b);
    }

    [TestMethod]
    public void Divide_SameKind_UInt128()
    {
        BinaryJsonNumber a = new((UInt128)100);
        BinaryJsonNumber b = new((UInt128)4);
        Assert.AreEqual(new BinaryJsonNumber((UInt128)25), a / b);
    }

    // Mixed-kind division
    [TestMethod]
    public void Divide_MixedKind_Double_Int32()
    {
        BinaryJsonNumber a = new(10.0);
        BinaryJsonNumber b = new(4);
        Assert.AreEqual(new BinaryJsonNumber(2.5), a / b);
    }

    // ============================================
    // AdditiveIdentity and MultiplicativeIdentity
    // ============================================
    [TestMethod]
    public void AdditiveIdentity_IsZero()
    {
        Assert.AreEqual(BinaryJsonNumber.Zero, BinaryJsonNumber.AdditiveIdentity);
    }

    [TestMethod]
    public void MultiplicativeIdentity_IsOne()
    {
        Assert.AreEqual(BinaryJsonNumber.One, BinaryJsonNumber.MultiplicativeIdentity);
    }
}

#endif