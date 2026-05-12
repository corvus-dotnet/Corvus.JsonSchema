// <copyright file="MathematicalFunctionsBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks for mathematical functions (Pow, Sqrt, Round, etc.).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class MathematicalFunctionsBenchmarks
{
    private BigNumber _number = default;
    private BigNumber _largeNumber = default;
    private BigNumber _decimalNumber = default;

    [GlobalSetup]
    public void Setup()
    {
        this._number = new BigNumber(10, 0);
        this._largeNumber = new BigNumber(123456789, 0);
        this._decimalNumber = BigNumber.Parse("123.456789");
    }

    #region Pow Benchmarks

    [Benchmark]
    public BigNumber Pow_Square()
    {
        return BigNumber.Pow(this._number, 2);
    }

    [Benchmark]
    public BigNumber Pow_Cube()
    {
        return BigNumber.Pow(this._number, 3);
    }

    [Benchmark]
    public BigNumber Pow_TenthPower()
    {
        return BigNumber.Pow(this._number, 10);
    }

    [Benchmark]
    public BigNumber Pow_LargeExponent()
    {
        return BigNumber.Pow(this._number, 50);
    }

    #endregion

    #region Sqrt Benchmarks

    [Benchmark]
    public BigNumber Sqrt_Precision10()
    {
        return BigNumber.Sqrt(this._number, 10);
    }

    [Benchmark]
    public BigNumber Sqrt_Precision50()
    {
        return BigNumber.Sqrt(this._number, 50);
    }

    [Benchmark]
    public BigNumber Sqrt_Precision100()
    {
        return BigNumber.Sqrt(this._number, 100);
    }

    [Benchmark]
    public BigNumber Sqrt_LargeNumber()
    {
        return BigNumber.Sqrt(this._largeNumber, 20);
    }

    #endregion

    #region Round Benchmarks

    [Benchmark]
    public BigNumber Round_ToZeroDecimals()
    {
        return BigNumber.Round(this._decimalNumber, 0);
    }

    [Benchmark]
    public BigNumber Round_ToTwoDecimals()
    {
        return BigNumber.Round(this._decimalNumber, 2);
    }

    [Benchmark]
    public BigNumber Round_ToFiveDecimals()
    {
        return BigNumber.Round(this._decimalNumber, 5);
    }

    [Benchmark]
    public BigNumber Round_ToEven()
    {
        return BigNumber.Round(BigNumber.Parse("2.5"), 0, MidpointRounding.ToEven);
    }

    [Benchmark]
    public BigNumber Round_AwayFromZero()
    {
        return BigNumber.Round(BigNumber.Parse("2.5"), 0, MidpointRounding.AwayFromZero);
    }

    #endregion

    #region Floor/Ceiling/Truncate Benchmarks

    [Benchmark]
    public BigNumber Floor_DecimalNumber()
    {
        return BigNumber.Floor(this._decimalNumber);
    }

    [Benchmark]
    public BigNumber Ceiling_DecimalNumber()
    {
        return BigNumber.Ceiling(this._decimalNumber);
    }

    [Benchmark]
    public BigNumber Truncate_DecimalNumber()
    {
        return BigNumber.Truncate(this._decimalNumber);
    }

    #endregion

    #region Batch Operations

    [Benchmark]
    public long BatchPow_SmallExponents()
    {
        long sum = 0;
        for (int i = 1; i <= 10; i++)
        {
            var result = BigNumber.Pow(this._number, i);
            sum += result.Exponent;
        }
        return sum;
    }

    [Benchmark]
    public long BatchSqrt_VaryingPrecisions()
    {
        long sum = 0;
        for (int precision = 5; precision <= 50; precision += 5)
        {
            var result = BigNumber.Sqrt(this._number, precision);
            sum += result.Exponent;
        }
        return sum;
    }

    [Benchmark]
    public long BatchRound_VaryingDecimals()
    {
        long sum = 0;
        for (int decimals = 0; decimals <= 6; decimals++)
        {
            var result = BigNumber.Round(this._decimalNumber, decimals);
            sum += (long)result.Significand;
        }
        return sum;
    }

    #endregion
}