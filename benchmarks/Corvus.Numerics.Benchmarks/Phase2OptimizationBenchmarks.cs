// <copyright file="Phase2OptimizationBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks to verify Phase 2 optimizations are effective.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class Phase2OptimizationBenchmarks
{
    private BigNumber _number = default!;
    private BigNumber _powerOf10 = default!;
    private BigNumber _smallInteger = default!;
    private BigNumber _sameExponentA = default!;
    private BigNumber _sameExponentB = default!;

    [GlobalSetup]
    public void Setup()
    {
        _number = BigNumber.Parse("123.456");
        _powerOf10 = new BigNumber(1, 3);  // 1000
        _smallInteger = new BigNumber(25, 0);  // 25
        _sameExponentA = new BigNumber(12345, -2);  // 123.45
        _sameExponentB = new BigNumber(67890, -2);  // 678.90
    }

    #region Division Fast Paths

    [Benchmark]
    [BenchmarkCategory("Division")]
    public BigNumber Division_ByPowerOf10()
    {
        return _number / _powerOf10;
    }

    [Benchmark]
    [BenchmarkCategory("Division")]
    public BigNumber Division_BySmallInteger()
    {
        return _number / _smallInteger;
    }

    [Benchmark]
    [BenchmarkCategory("Division")]
    public BigNumber Division_SameExponent()
    {
        return _sameExponentA / _sameExponentB;
    }

    #endregion

    #region Multiplication Fast Paths

    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public BigNumber Multiplication_BySmallInteger()
    {
        return _number * _smallInteger;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public BigNumber Multiplication_ByTwo()
    {
        return _number * new BigNumber(2, 0);
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication")]
    public BigNumber Multiplication_SmallBySmall()
    {
        BigNumber a = new(42, 0);
        BigNumber b = new(17, 0);
        return a * b;
    }

    #endregion

    #region Comparison Fast Paths

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public bool Comparison_SameExponent_LessThan()
    {
        return _sameExponentA < _sameExponentB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public bool Comparison_SameExponent_Equals()
    {
        BigNumber copy = new(_sameExponentA.Significand, _sameExponentA.Exponent);
        return _sameExponentA == copy;
    }

    #endregion

    #region Parsing Fast Paths

    [Benchmark]
    [BenchmarkCategory("Parsing")]
    public BigNumber Parsing_SingleDigit()
    {
        return BigNumber.Parse("5");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing")]
    public BigNumber Parsing_SmallInteger()
    {
        return BigNumber.Parse("12345");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing")]
    public BigNumber Parsing_Zero()
    {
        return BigNumber.Parse("0");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing")]
    public BigNumber Parsing_One()
    {
        return BigNumber.Parse("1");
    }

    #endregion
}