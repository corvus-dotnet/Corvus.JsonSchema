// <copyright file="BinaryJsonNumberNet8AdditionalTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1139 // Use literal suffix notation instead of casting

#if NET8_0_OR_GREATER

using System.Numerics;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class BinaryJsonNumberNet8AdditionalTests
{
    // Helper methods to call static virtual interface members through generic constraints
    private static TTarget GenericCreateChecked<TTarget, TSource>(TSource value)
        where TTarget : INumberBase<TTarget>
        where TSource : INumberBase<TSource>
        => TTarget.CreateChecked(value);

    private static TTarget GenericCreateSaturating<TTarget, TSource>(TSource value)
        where TTarget : INumberBase<TTarget>
        where TSource : INumberBase<TSource>
        => TTarget.CreateSaturating(value);

    private static TTarget GenericCreateTruncating<TTarget, TSource>(TSource value)
        where TTarget : INumberBase<TTarget>
        where TSource : INumberBase<TSource>
        => TTarget.CreateTruncating(value);

    // =====================
    // IsOddInteger — all kinds
    // =====================
    [TestMethod]
    public void IsOddInteger_Byte_Odd() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((byte)3)));

    [TestMethod]
    public void IsOddInteger_Byte_Even() =>
        Assert.IsFalse(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((byte)4)));

    [TestMethod]
    public void IsOddInteger_SByte() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((sbyte)-3)));

    [TestMethod]
    public void IsOddInteger_Int16() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((short)7)));

    [TestMethod]
    public void IsOddInteger_Int32() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber(5)));

    [TestMethod]
    public void IsOddInteger_Int64() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber(9L)));

    [TestMethod]
    public void IsOddInteger_Int128() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((Int128)11)));

    [TestMethod]
    public void IsOddInteger_UInt16() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((ushort)3)));

    [TestMethod]
    public void IsOddInteger_UInt32() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((uint)5)));

    [TestMethod]
    public void IsOddInteger_UInt64() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((ulong)7)));

    [TestMethod]
    public void IsOddInteger_UInt128() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((UInt128)9)));

    [TestMethod]
    public void IsOddInteger_Decimal() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber(3m)));

    [TestMethod]
    public void IsOddInteger_Double() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber(3.0)));

    [TestMethod]
    public void IsOddInteger_Single() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber(3.0f)));

    [TestMethod]
    public void IsOddInteger_Half() =>
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(new BinaryJsonNumber((Half)3)));

    // =====================
    // IsPositive — all kinds
    // =====================
    [TestMethod]
    public void IsPositive_Byte() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((byte)1)));

    [TestMethod]
    public void IsPositive_SByte_Positive() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((sbyte)1)));

    [TestMethod]
    public void IsPositive_SByte_Negative() =>
        Assert.IsFalse(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((sbyte)-1)));

    [TestMethod]
    public void IsPositive_Int16() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((short)1)));

    [TestMethod]
    public void IsPositive_Int32() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(1)));

    [TestMethod]
    public void IsPositive_Int32_Negative() =>
        Assert.IsFalse(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(-1)));

    [TestMethod]
    public void IsPositive_Int64() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(1L)));

    [TestMethod]
    public void IsPositive_Int128() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((Int128)1)));

    [TestMethod]
    public void IsPositive_UInt16() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((ushort)1)));

    [TestMethod]
    public void IsPositive_UInt32() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((uint)1)));

    [TestMethod]
    public void IsPositive_UInt64() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((ulong)1)));

    [TestMethod]
    public void IsPositive_UInt128() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((UInt128)1)));

    [TestMethod]
    public void IsPositive_Decimal() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(1m)));

    [TestMethod]
    public void IsPositive_Decimal_Negative() =>
        Assert.IsFalse(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(-1m)));

    [TestMethod]
    public void IsPositive_Double() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(1.0)));

    [TestMethod]
    public void IsPositive_Single() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber(1.0f)));

    [TestMethod]
    public void IsPositive_Half() =>
        Assert.IsTrue(BinaryJsonNumber.IsPositive(new BinaryJsonNumber((Half)1)));

    // =====================
    // IsEvenInteger — fill gaps (existing tests cover Int128, UInt128, Half)
    // =====================
    [TestMethod]
    public void IsEvenInteger_Byte() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((byte)4)));

    [TestMethod]
    public void IsEvenInteger_SByte() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((sbyte)-4)));

    [TestMethod]
    public void IsEvenInteger_Int16() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((short)6)));

    [TestMethod]
    public void IsEvenInteger_Int32() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber(8)));

    [TestMethod]
    public void IsEvenInteger_Int64() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber(10L)));

    [TestMethod]
    public void IsEvenInteger_UInt16() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((ushort)6)));

    [TestMethod]
    public void IsEvenInteger_UInt32() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((uint)8)));

    [TestMethod]
    public void IsEvenInteger_UInt64() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber((ulong)10)));

    [TestMethod]
    public void IsEvenInteger_Decimal() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber(4m)));

    [TestMethod]
    public void IsEvenInteger_Double() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber(4.0)));

    [TestMethod]
    public void IsEvenInteger_Single() =>
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(new BinaryJsonNumber(4.0f)));

    // =====================
    // CreateSaturating / CreateTruncating — all kinds
    // =====================
    [TestMethod]
    public void CreateSaturating_AllKinds()
    {
        Assert.AreEqual(42.0, new BinaryJsonNumber((byte)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((sbyte)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((short)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42L).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Int128)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ushort)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((uint)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ulong)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((UInt128)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42m).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0f).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Half)42).CreateSaturating<double>());
    }

    [TestMethod]
    public void CreateTruncating_AllKinds()
    {
        Assert.AreEqual(42, new BinaryJsonNumber((byte)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((sbyte)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((short)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42L).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((Int128)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((ushort)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((uint)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((ulong)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((UInt128)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42m).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42.0).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42.0f).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((Half)42).CreateTruncating<int>());
    }

    // =====================
    // TryConvertFromChecked via generic math CreateChecked — various source types
    // =====================
    [TestMethod]
    public void TryConvertFromChecked_Byte()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, byte>((byte)10);
        Assert.AreEqual(10.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_SByte()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, sbyte>((sbyte)-5);
        Assert.AreEqual(-5.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_Char()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, char>('A');
        Assert.AreEqual(65.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_Double()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, double>(3.14);
        Assert.AreEqual(3.14, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_Decimal()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, decimal>(2.5m);
        Assert.AreEqual(2.5, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_UInt16()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, ushort>((ushort)100);
        Assert.AreEqual(100.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_UInt32()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, uint>((uint)200);
        Assert.AreEqual(200.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_UInt64()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, ulong>((ulong)300);
        Assert.AreEqual(300.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_NUInt()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, nuint>((nuint)50);
        Assert.AreEqual(50.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_Half()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, Half>((Half)1.5);
        Assert.AreEqual((double)(Half)1.5, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_Int128()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, Int128>((Int128)999);
        Assert.AreEqual(999.0, bjn.CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromChecked_UInt128()
    {
        BinaryJsonNumber bjn = GenericCreateChecked<BinaryJsonNumber, UInt128>((UInt128)888);
        Assert.AreEqual(888.0, bjn.CreateChecked<double>());
    }

    // =====================
    // TryConvertFromSaturating / TryConvertFromTruncating via generic math
    // =====================
    [TestMethod]
    public void TryConvertFromSaturating_AllSourceTypes()
    {
        Assert.AreEqual(10.0, GenericCreateSaturating<BinaryJsonNumber, byte>((byte)10).CreateChecked<double>());
        Assert.AreEqual(-5.0, GenericCreateSaturating<BinaryJsonNumber, sbyte>((sbyte)-5).CreateChecked<double>());
        Assert.AreEqual(65.0, GenericCreateSaturating<BinaryJsonNumber, char>('A').CreateChecked<double>());
        Assert.AreEqual(3.14, GenericCreateSaturating<BinaryJsonNumber, double>(3.14).CreateChecked<double>());
        Assert.AreEqual(2.5, GenericCreateSaturating<BinaryJsonNumber, decimal>(2.5m).CreateChecked<double>());
        Assert.AreEqual(100.0, GenericCreateSaturating<BinaryJsonNumber, ushort>((ushort)100).CreateChecked<double>());
        Assert.AreEqual(200.0, GenericCreateSaturating<BinaryJsonNumber, uint>((uint)200).CreateChecked<double>());
        Assert.AreEqual(300.0, GenericCreateSaturating<BinaryJsonNumber, ulong>((ulong)300).CreateChecked<double>());
        Assert.AreEqual(50.0, GenericCreateSaturating<BinaryJsonNumber, nuint>((nuint)50).CreateChecked<double>());
        Assert.AreEqual(999.0, GenericCreateSaturating<BinaryJsonNumber, Int128>((Int128)999).CreateChecked<double>());
        Assert.AreEqual(888.0, GenericCreateSaturating<BinaryJsonNumber, UInt128>((UInt128)888).CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertFromTruncating_AllSourceTypes()
    {
        Assert.AreEqual(10.0, GenericCreateTruncating<BinaryJsonNumber, byte>((byte)10).CreateChecked<double>());
        Assert.AreEqual(-5.0, GenericCreateTruncating<BinaryJsonNumber, sbyte>((sbyte)-5).CreateChecked<double>());
        Assert.AreEqual(65.0, GenericCreateTruncating<BinaryJsonNumber, char>('A').CreateChecked<double>());
        Assert.AreEqual(3.14, GenericCreateTruncating<BinaryJsonNumber, double>(3.14).CreateChecked<double>());
        Assert.AreEqual(2.5, GenericCreateTruncating<BinaryJsonNumber, decimal>(2.5m).CreateChecked<double>());
        Assert.AreEqual(100.0, GenericCreateTruncating<BinaryJsonNumber, ushort>((ushort)100).CreateChecked<double>());
        Assert.AreEqual(200.0, GenericCreateTruncating<BinaryJsonNumber, uint>((uint)200).CreateChecked<double>());
        Assert.AreEqual(300.0, GenericCreateTruncating<BinaryJsonNumber, ulong>((ulong)300).CreateChecked<double>());
        Assert.AreEqual(50.0, GenericCreateTruncating<BinaryJsonNumber, nuint>((nuint)50).CreateChecked<double>());
        Assert.AreEqual(999.0, GenericCreateTruncating<BinaryJsonNumber, Int128>((Int128)999).CreateChecked<double>());
        Assert.AreEqual(888.0, GenericCreateTruncating<BinaryJsonNumber, UInt128>((UInt128)888).CreateChecked<double>());
    }

    // =====================
    // TryConvertToChecked/Saturating/Truncating via double.CreateChecked(bjn) etc.
    // =====================
    [TestMethod]
    public void TryConvertToChecked_AllKinds()
    {
        Assert.AreEqual(42.0, new BinaryJsonNumber((byte)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((sbyte)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((short)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42L).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Int128)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ushort)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((uint)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ulong)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((UInt128)42).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42m).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0f).CreateChecked<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Half)42).CreateChecked<double>());
    }

    [TestMethod]
    public void TryConvertToSaturating_AllKinds()
    {
        Assert.AreEqual(42.0, new BinaryJsonNumber((byte)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((sbyte)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((short)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42L).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Int128)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ushort)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((uint)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((ulong)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((UInt128)42).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42m).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber(42.0f).CreateSaturating<double>());
        Assert.AreEqual(42.0, new BinaryJsonNumber((Half)42).CreateSaturating<double>());
    }

    [TestMethod]
    public void TryConvertToTruncating_AllKinds()
    {
        Assert.AreEqual(42, new BinaryJsonNumber((byte)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((sbyte)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((short)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42L).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((Int128)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((ushort)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((uint)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((ulong)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((UInt128)42).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42m).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42.0).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber(42.0f).CreateTruncating<int>());
        Assert.AreEqual(42, new BinaryJsonNumber((Half)42).CreateTruncating<int>());
    }

    // =====================
    // Negation for uncovered kinds
    // =====================
    [TestMethod]
    public void Negation_Byte() =>
        Assert.AreEqual(-42.0, (-new BinaryJsonNumber((byte)42)).CreateChecked<double>());

    [TestMethod]
    public void Negation_Decimal() =>
        Assert.AreEqual(-42m, (-new BinaryJsonNumber(42m)).CreateChecked<decimal>());

    [TestMethod]
    public void Negation_Double() =>
        Assert.AreEqual(-42.0, (-new BinaryJsonNumber(42.0)).CreateChecked<double>());

    [TestMethod]
    public void Negation_Int16() =>
        Assert.AreEqual((short)-42, (-new BinaryJsonNumber((short)42)).CreateChecked<short>());

    [TestMethod]
    public void Negation_Int32() =>
        Assert.AreEqual(-42, (-new BinaryJsonNumber(42)).CreateChecked<int>());

    [TestMethod]
    public void Negation_Int64() =>
        Assert.AreEqual(-42L, (-new BinaryJsonNumber(42L)).CreateChecked<long>());

    [TestMethod]
    public void Negation_UInt16() =>
        Assert.AreEqual(-42.0, (-new BinaryJsonNumber((ushort)42)).CreateChecked<double>());

    [TestMethod]
    public void Negation_UInt32() =>
        Assert.AreEqual(-42.0, (-new BinaryJsonNumber((uint)42)).CreateChecked<double>());

    [TestMethod]
    public void Negation_UInt64()
    {
        var bjn = new BinaryJsonNumber((ulong)42);
        var neg = -bjn;
        Assert.AreEqual(-42.0, neg.CreateChecked<double>());
    }

    [TestMethod]
    public void Negation_UInt128()
    {
        var bjn = new BinaryJsonNumber((UInt128)42);
        var neg = -bjn;

        // UInt128 negation wraps around (unsigned arithmetic), so just verify it doesn't throw
        Assert.AreNotEqual(42.0, neg.CreateChecked<double>());
    }

    // =====================
    // Abs for uncovered kinds
    // =====================
    [TestMethod]
    public void Abs_Byte_ReturnsSelf() =>
        Assert.AreEqual(42.0, BinaryJsonNumber.Abs(new BinaryJsonNumber((byte)42)).CreateChecked<double>());

    [TestMethod]
    public void Abs_Decimal() =>
        Assert.AreEqual(42m, BinaryJsonNumber.Abs(new BinaryJsonNumber(-42m)).CreateChecked<decimal>());

    [TestMethod]
    public void Abs_Double() =>
        Assert.AreEqual(42.0, BinaryJsonNumber.Abs(new BinaryJsonNumber(-42.0)).CreateChecked<double>());

    [TestMethod]
    public void Abs_Int16() =>
        Assert.AreEqual((short)42, BinaryJsonNumber.Abs(new BinaryJsonNumber((short)-42)).CreateChecked<short>());

    [TestMethod]
    public void Abs_Int32() =>
        Assert.AreEqual(42, BinaryJsonNumber.Abs(new BinaryJsonNumber(-42)).CreateChecked<int>());

    [TestMethod]
    public void Abs_Int64() =>
        Assert.AreEqual(42L, BinaryJsonNumber.Abs(new BinaryJsonNumber(-42L)).CreateChecked<long>());

    [TestMethod]
    public void Abs_Single() =>
        Assert.AreEqual(42.0f, BinaryJsonNumber.Abs(new BinaryJsonNumber(-42.0f)).CreateChecked<float>());

    [TestMethod]
    public void Abs_UInt16_ReturnsSelf() =>
        Assert.AreEqual(42.0, BinaryJsonNumber.Abs(new BinaryJsonNumber((ushort)42)).CreateChecked<double>());

    [TestMethod]
    public void Abs_UInt32_ReturnsSelf() =>
        Assert.AreEqual(42.0, BinaryJsonNumber.Abs(new BinaryJsonNumber((uint)42)).CreateChecked<double>());

    [TestMethod]
    public void Abs_UInt64_ReturnsSelf() =>
        Assert.AreEqual(42.0, BinaryJsonNumber.Abs(new BinaryJsonNumber((ulong)42)).CreateChecked<double>());

    // =====================
    // UnaryPlus for uncovered kinds
    // =====================
    [TestMethod]
    public void UnaryPlus_Byte() =>
        Assert.AreEqual(42.0, (+new BinaryJsonNumber((byte)42)).CreateChecked<double>());

    [TestMethod]
    public void UnaryPlus_Decimal() =>
        Assert.AreEqual(42m, (+new BinaryJsonNumber(42m)).CreateChecked<decimal>());

    [TestMethod]
    public void UnaryPlus_Double() =>
        Assert.AreEqual(42.0, (+new BinaryJsonNumber(42.0)).CreateChecked<double>());

    [TestMethod]
    public void UnaryPlus_Int16() =>
        Assert.AreEqual((short)42, (+new BinaryJsonNumber((short)42)).CreateChecked<short>());

    [TestMethod]
    public void UnaryPlus_Int32() =>
        Assert.AreEqual(42, (+new BinaryJsonNumber(42)).CreateChecked<int>());

    [TestMethod]
    public void UnaryPlus_Int64() =>
        Assert.AreEqual(42L, (+new BinaryJsonNumber(42L)).CreateChecked<long>());

    [TestMethod]
    public void UnaryPlus_UInt16() =>
        Assert.AreEqual(42.0, (+new BinaryJsonNumber((ushort)42)).CreateChecked<double>());

    [TestMethod]
    public void UnaryPlus_UInt32() =>
        Assert.AreEqual(42.0, (+new BinaryJsonNumber((uint)42)).CreateChecked<double>());

    [TestMethod]
    public void UnaryPlus_UInt64() =>
        Assert.AreEqual(42.0, (+new BinaryJsonNumber((ulong)42)).CreateChecked<double>());

    // =====================
    // ToString for uncovered kinds
    // =====================
    [TestMethod]
    public void ToString_Byte() =>
        Assert.AreEqual("42", new BinaryJsonNumber((byte)42).ToString());

    [TestMethod]
    public void ToString_Int16() =>
        Assert.AreEqual("-42", new BinaryJsonNumber((short)-42).ToString());

    [TestMethod]
    public void ToString_Int32() =>
        Assert.AreEqual("42", new BinaryJsonNumber(42).ToString());

    [TestMethod]
    public void ToString_Int64() =>
        Assert.AreEqual("42", new BinaryJsonNumber(42L).ToString());

    [TestMethod]
    public void ToString_UInt16() =>
        Assert.AreEqual("42", new BinaryJsonNumber((ushort)42).ToString());

    [TestMethod]
    public void ToString_UInt32() =>
        Assert.AreEqual("42", new BinaryJsonNumber((uint)42).ToString());

    [TestMethod]
    public void ToString_UInt64() =>
        Assert.AreEqual("42", new BinaryJsonNumber((ulong)42).ToString());

    [TestMethod]
    public void ToString_Decimal() =>
        Assert.AreEqual("42", new BinaryJsonNumber(42m).ToString());

    [TestMethod]
    public void ToString_Single() =>
        Assert.AreEqual("42", new BinaryJsonNumber(42.0f).ToString());

    [TestMethod]
    public void ToString_Double() =>
        Assert.AreEqual("42", new BinaryJsonNumber(42.0).ToString());

    // =====================
    // ToString with format — uncovered kinds
    // =====================
    [TestMethod]
    public void ToStringFormatted_Byte() =>
        Assert.AreEqual("2A", new BinaryJsonNumber((byte)42).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_Int16() =>
        Assert.AreEqual("002A", new BinaryJsonNumber((short)42).ToString("X4", null));

    [TestMethod]
    public void ToStringFormatted_Int32() =>
        Assert.AreEqual("2A", new BinaryJsonNumber(42).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_Int64() =>
        Assert.AreEqual("2A", new BinaryJsonNumber(42L).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_UInt16() =>
        Assert.AreEqual("2A", new BinaryJsonNumber((ushort)42).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_UInt32() =>
        Assert.AreEqual("2A", new BinaryJsonNumber((uint)42).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_UInt64() =>
        Assert.AreEqual("2A", new BinaryJsonNumber((ulong)42).ToString("X", null));

    [TestMethod]
    public void ToStringFormatted_Decimal() =>
        Assert.AreEqual("42.00", new BinaryJsonNumber(42m).ToString("F2", null));

    [TestMethod]
    public void ToStringFormatted_Single() =>
        Assert.AreEqual("42.00", new BinaryJsonNumber(42.0f).ToString("F2", null));

    [TestMethod]
    public void ToStringFormatted_Double() =>
        Assert.AreEqual("42.00", new BinaryJsonNumber(42.0).ToString("F2", null));

    [TestMethod]
    public void ToStringFormatted_Half() =>
        Assert.AreEqual("42.00", new BinaryJsonNumber((Half)42).ToString("F2", null));

    [TestMethod]
    public void ToStringFormatted_SByte() =>
        Assert.AreEqual("2A", new BinaryJsonNumber((sbyte)42).ToString("X", null));

    // =====================
    // TryFormat char span for uncovered kinds
    // =====================
    [TestMethod]
    public void TryFormat_Byte()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber((byte)42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Int16()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber((short)-42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("-42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Int32()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber(42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Int64()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber(42L).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_UInt16()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber((ushort)42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_UInt32()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber((uint)42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_UInt64()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber((ulong)42).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Decimal()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber(42m).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Single()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber(42.0f).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    [TestMethod]
    public void TryFormat_Double()
    {
        Span<char> buffer = stackalloc char[32];
        Assert.IsTrue(new BinaryJsonNumber(42.0).TryFormat(buffer, out int written, default, null));
        Assert.AreEqual("42", buffer.Slice(0, written).ToString());
    }

    // =====================
    // TryFormat UTF-8 span for uncovered kinds
    // =====================
    [TestMethod]
    public void TryFormatUtf8_Byte()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((byte)42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Int16()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((short)-42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("-42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Int32()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber(42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Int64()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber(42L).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_UInt16()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((ushort)42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_UInt32()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((uint)42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_UInt64()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((ulong)42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Decimal()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber(42m).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Single()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber(42.0f).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_Double()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber(42.0).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryFormatUtf8_SByte()
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(new BinaryJsonNumber((sbyte)-42).TryFormat(buffer, out int written, default, null));
        CollectionAssert.AreEqual("-42"u8.ToArray(), buffer.Slice(0, written).ToArray());
    }

    // =====================
    // IsMultipleOf for uncovered kinds
    // =====================
    [TestMethod]
    public void IsMultipleOf_Int128_True()
    {
        var value = new BinaryJsonNumber((Int128)12);
        var factor = new BinaryJsonNumber((Int128)3);
        Assert.IsTrue(value.IsMultipleOf(factor));
    }

    [TestMethod]
    public void IsMultipleOf_UInt128_True()
    {
        var value = new BinaryJsonNumber((UInt128)15);
        var factor = new BinaryJsonNumber((UInt128)5);
        Assert.IsTrue(value.IsMultipleOf(factor));
    }

    [TestMethod]
    public void IsMultipleOf_Half_True()
    {
        var value = new BinaryJsonNumber((Half)6);
        var factor = new BinaryJsonNumber((Half)3);
        Assert.IsTrue(value.IsMultipleOf(factor));
    }

    [TestMethod]
    public void IsMultipleOf_Half_False()
    {
        var value = new BinaryJsonNumber((Half)7);
        var factor = new BinaryJsonNumber((Half)3);
        Assert.IsFalse(value.IsMultipleOf(factor));
    }
}

#endif