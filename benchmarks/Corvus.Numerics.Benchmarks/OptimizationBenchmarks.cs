// <copyright file="OptimizationBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks to measure the impact of Phase 1 optimizations.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class OptimizationBenchmarks
{
    private BigNumber _numberA = default;
    private BigNumber _numberB = default;
    private BigNumber _numberSameExponent = default;
    private BigNumber _numberZero = default;
    private BigNumber _simpleNumber = default;
    private BigNumber _decimalNumber = default;

    [GlobalSetup]
    public void Setup()
    {
        this._numberA = new BigNumber(12345, 5);
        this._numberB = new BigNumber(67890, 3);
        this._numberSameExponent = new BigNumber(54321, 5); // Same exponent as _numberA
        this._numberZero = BigNumber.Zero;
        this._simpleNumber = new BigNumber(12345, 0);
        this._decimalNumber = new BigNumber(12345, -2); // 123.45
    }

    #region Arithmetic with Same Exponent (Fast Path)

    [Benchmark]
    public BigNumber Addition_SameExponent()
    {
        return this._numberA + this._numberSameExponent;
    }

    [Benchmark]
    public BigNumber Addition_DifferentExponent()
    {
        return this._numberA + this._numberB;
    }

    [Benchmark]
    public BigNumber Subtraction_SameExponent()
    {
        return this._numberA - this._numberSameExponent;
    }

    [Benchmark]
    public BigNumber Subtraction_DifferentExponent()
    {
        return this._numberA - this._numberB;
    }

    #endregion

    #region Zero Operand Fast Path

    [Benchmark]
    public BigNumber Addition_WithZero()
    {
        return this._numberA + this._numberZero;
    }

    [Benchmark]
    public BigNumber Subtraction_WithZero()
    {
        return this._numberA - this._numberZero;
    }

    [Benchmark]
    public BigNumber Subtraction_ZeroMinusValue()
    {
        return this._numberZero - this._numberA;
    }

    #endregion

    #region Comparison Optimizations

    [Benchmark]
    public bool Comparison_ExactSame()
    {
        return this._numberA.CompareTo(this._numberA) == 0;
    }

    [Benchmark]
    public int Comparison_DifferentSigns()
    {
        BigNumber negative = -this._numberA;
        return this._numberA.CompareTo(negative);
    }

    [Benchmark]
    public int Comparison_WithZero()
    {
        return this._numberA.CompareTo(this._numberZero);
    }

    [Benchmark]
    public bool Comparison_ZeroWithZero()
    {
        return this._numberZero.CompareTo(BigNumber.Zero) == 0;
    }

    #endregion

    #region Format String Fast Paths

    [Benchmark]
    public int Format_EmptyFormat()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        return charsWritten;
    }

    [Benchmark]
    public int Format_SingleDigitPrecision_F2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    [Benchmark]
    public int Format_SingleDigitPrecision_E6()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "E6".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    [Benchmark]
    public int Format_SingleDigitPrecision_N2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "N2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    [Benchmark]
    public int Format_MultiDigitPrecision_F10()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F10".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region Power of 10 Cache Impact

    [Benchmark]
    public BigNumber Multiply_RequiresPowerOf10_Small()
    {
        // Requires 10^3 (in cache)
        var a = new BigNumber(123, 0);
        var b = new BigNumber(456, 3);
        return a + b;
    }

    [Benchmark]
    public BigNumber Multiply_RequiresPowerOf10_Large()
    {
        // Requires 10^50 (in cache)
        var a = new BigNumber(123, 0);
        var b = new BigNumber(456, 50);
        return a + b;
    }

    [Benchmark]
    public BigNumber Multiply_RequiresPowerOf10_VeryLarge()
    {
        // Requires 10^150 (outside cache)
        var a = new BigNumber(123, 0);
        var b = new BigNumber(456, 150);
        return a + b;
    }

    #endregion

    #region Batch Operations

    [Benchmark]
    public long BatchAddition_SameExponent()
    {
        long sum = 0;
        for (int i = 0; i < 100; i++)
        {
            BigNumber result = this._numberA + this._numberSameExponent;
            sum += result.Exponent;
        }
        return sum;
    }

    [Benchmark]
    public long BatchComparison()
    {
        long sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += this._numberA.CompareTo(this._numberB);
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormatting_F2()
    {
        long sum = 0;
        Span<char> buffer = stackalloc char[128];
        for (int i = 0; i < 100; i++)
        {
            this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
            sum += charsWritten;
        }
        return sum;
    }

    #endregion

    #region RoundToPrecision Fast Path

    [Benchmark]
    public string Format_AlreadyPrecise()
    {
        // Number already at exact precision (should hit fast path)
        var precise = new BigNumber(12345, -2); // 123.45, precision = 2
        return precise.ToString("F2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public string Format_NeedsRounding()
    {
        // Number needs rounding
        var imprecise = new BigNumber(123456, -3); // 123.456, round to 2
        return imprecise.ToString("F2", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Sign Handling Optimization

    [Benchmark]
    public string Format_NegativeNumber_InvariantCulture()
    {
        BigNumber negative = -this._decimalNumber;
        return negative.ToString("F2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public string Format_NegativeNumber_CustomCulture()
    {
        BigNumber negative = -this._decimalNumber;
        return negative.ToString("F2", new CultureInfo("fr-FR"));
    }

    #endregion
}