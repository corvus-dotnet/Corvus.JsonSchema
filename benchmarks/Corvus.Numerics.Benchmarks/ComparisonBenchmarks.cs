// <copyright file="ComparisonBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks comparing BigNumber performance with System.Decimal.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class ComparisonBenchmarks
{
    private BigNumber _bigNumberA = default;
    private BigNumber _bigNumberB = default;
    private decimal _decimalA;
    private decimal _decimalB;

    [GlobalSetup]
    public void Setup()
    {
        this._bigNumberA = BigNumber.Parse("123.456");
        this._bigNumberB = BigNumber.Parse("789.012");
        this._decimalA = 123.456m;
        this._decimalB = 789.012m;
    }

    #region Addition Comparison

    [Benchmark(Baseline = true)]
    public decimal Decimal_Addition()
    {
        return this._decimalA + this._decimalB;
    }

    [Benchmark]
    public BigNumber BigNumber_Addition()
    {
        return this._bigNumberA + this._bigNumberB;
    }

    #endregion

    #region Subtraction Comparison

    [Benchmark]
    public decimal Decimal_Subtraction()
    {
        return this._decimalA - this._decimalB;
    }

    [Benchmark]
    public BigNumber BigNumber_Subtraction()
    {
        return this._bigNumberA - this._bigNumberB;
    }

    #endregion

    #region Multiplication Comparison

    [Benchmark]
    public decimal Decimal_Multiplication()
    {
        return this._decimalA * this._decimalB;
    }

    [Benchmark]
    public BigNumber BigNumber_Multiplication()
    {
        return this._bigNumberA * this._bigNumberB;
    }

    #endregion

    #region Division Comparison

    [Benchmark]
    public decimal Decimal_Division()
    {
        return this._decimalA / this._decimalB;
    }

    [Benchmark]
    public BigNumber BigNumber_Division()
    {
        return this._bigNumberA / this._bigNumberB;
    }

    #endregion

    #region Comparison Operations

    [Benchmark]
    public bool Decimal_Comparison()
    {
        return this._decimalA < this._decimalB;
    }

    [Benchmark]
    public bool BigNumber_Comparison()
    {
        return this._bigNumberA < this._bigNumberB;
    }

    #endregion

    #region Formatting Comparison

    [Benchmark]
    public string Decimal_ToString()
    {
        return this._decimalA.ToString("F2");
    }

    [Benchmark]
    public string BigNumber_ToString()
    {
        return this._bigNumberA.ToString("F2", null);
    }

#if NET
    [Benchmark]
    public int Decimal_TryFormat()
    {
        Span<char> buffer = stackalloc char[64];
        this._decimalA.TryFormat(buffer, out int charsWritten, "F2", null);
        return charsWritten;
    }

    [Benchmark]
    public int BigNumber_TryFormat()
    {
        Span<char> buffer = stackalloc char[64];
        this._bigNumberA.TryFormat(buffer, out int charsWritten, "F2", null);
        return charsWritten;
    }
#endif

    #endregion

    #region Parsing Comparison

    [Benchmark]
    public decimal Decimal_Parse()
    {
        return decimal.Parse("123.456");
    }

    [Benchmark]
    public BigNumber BigNumber_Parse()
    {
        return BigNumber.Parse("123.456");
    }

    #endregion
}