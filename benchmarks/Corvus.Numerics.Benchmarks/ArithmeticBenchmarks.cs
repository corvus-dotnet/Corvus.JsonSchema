// <copyright file="ArithmeticBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class ArithmeticBenchmarks
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
}