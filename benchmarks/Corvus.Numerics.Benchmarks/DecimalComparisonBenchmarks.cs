// <copyright file="DecimalComparisonBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Comprehensive benchmarks comparing BigNumber with decimal across all standard operations.
/// Tests three magnitude ranges: typical, very small, and very large (within decimal range).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class DecimalComparisonBenchmarks
{
    // Typical values (everyday numbers)
    private BigNumber _typicalBigNumberA = default!;
    private BigNumber _typicalBigNumberB = default!;
    private decimal _typicalDecimalA;
    private decimal _typicalDecimalB;

    // Very small values (near decimal minimum)
    private BigNumber _smallBigNumberA = default!;
    private BigNumber _smallBigNumberB = default!;
    private decimal _smallDecimalA;
    private decimal _smallDecimalB;

    // Very large values (near decimal maximum)
    private BigNumber _largeBigNumberA = default!;
    private BigNumber _largeBigNumberB = default!;
    private decimal _largeDecimalA;
    private decimal _largeDecimalB;

    [GlobalSetup]
    public void Setup()
    {
        // Typical values: Everyday numbers
        _typicalBigNumberA = BigNumber.Parse("123.456");
        _typicalBigNumberB = BigNumber.Parse("789.012");
        _typicalDecimalA = 123.456m;
        _typicalDecimalB = 789.012m;

        // Very small values: Close to decimal's minimum precision
        _smallBigNumberA = BigNumber.Parse("0.00000000000000000001"); // 1E-20
        _smallBigNumberB = BigNumber.Parse("0.00000000000000000002"); // 2E-20
        _smallDecimalA = 0.00000000000000000001m;
        _smallDecimalB = 0.00000000000000000002m;

        // Very large values: Close to decimal's maximum
        _largeBigNumberA = BigNumber.Parse("79228162514264337593"); // Near decimal.MaxValue
        _largeBigNumberB = BigNumber.Parse("79228162514264337592");
        _largeDecimalA = 79228162514264337593m;
        _largeDecimalB = 79228162514264337592m;
    }

    #region Addition Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Addition", "Typical")]
    public decimal Decimal_Addition_Typical()
    {
        return _typicalDecimalA + _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Addition", "Typical")]
    public BigNumber BigNumber_Addition_Typical()
    {
        return _typicalBigNumberA + _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Addition", "Small")]
    public decimal Decimal_Addition_Small()
    {
        return _smallDecimalA + _smallDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Addition", "Small")]
    public BigNumber BigNumber_Addition_Small()
    {
        return _smallBigNumberA + _smallBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Addition", "Large")]
    public decimal Decimal_Addition_Large()
    {
        return _largeDecimalA + _largeDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Addition", "Large")]
    public BigNumber BigNumber_Addition_Large()
    {
        return _largeBigNumberA + _largeBigNumberB;
    }

    #endregion

    #region Subtraction Benchmarks

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Typical")]
    public decimal Decimal_Subtraction_Typical()
    {
        return _typicalDecimalA - _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Typical")]
    public BigNumber BigNumber_Subtraction_Typical()
    {
        return _typicalBigNumberA - _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Small")]
    public decimal Decimal_Subtraction_Small()
    {
        return _smallDecimalA - _smallDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Small")]
    public BigNumber BigNumber_Subtraction_Small()
    {
        return _smallBigNumberA - _smallBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Large")]
    public decimal Decimal_Subtraction_Large()
    {
        return _largeDecimalA - _largeDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Subtraction", "Large")]
    public BigNumber BigNumber_Subtraction_Large()
    {
        return _largeBigNumberA - _largeBigNumberB;
    }

    #endregion

    #region Multiplication Benchmarks

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Typical")]
    public decimal Decimal_Multiplication_Typical()
    {
        return _typicalDecimalA * _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Typical")]
    public BigNumber BigNumber_Multiplication_Typical()
    {
        return _typicalBigNumberA * _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Small")]
    public decimal Decimal_Multiplication_Small()
    {
        return _smallDecimalA * _smallDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Small")]
    public BigNumber BigNumber_Multiplication_Small()
    {
        return _smallBigNumberA * _smallBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Large")]
    public decimal Decimal_Multiplication_Large()
    {
        // Use smaller values to avoid overflow
        decimal a = 792281625.14m;
        decimal b = 792281625.13m;
        return a * b;
    }

    [Benchmark]
    [BenchmarkCategory("Multiplication", "Large")]
    public BigNumber BigNumber_Multiplication_Large()
    {
        var a = BigNumber.Parse("792281625.14");
        var b = BigNumber.Parse("792281625.13");
        return a * b;
    }

    #endregion

    #region Division Benchmarks

    [Benchmark]
    [BenchmarkCategory("Division", "Typical")]
    public decimal Decimal_Division_Typical()
    {
        return _typicalDecimalA / _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Division", "Typical")]
    public BigNumber BigNumber_Division_Typical()
    {
        return _typicalBigNumberA / _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Division", "Small")]
    public decimal Decimal_Division_Small()
    {
        return _smallDecimalA / _smallDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Division", "Small")]
    public BigNumber BigNumber_Division_Small()
    {
        return _smallBigNumberA / _smallBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Division", "Large")]
    public decimal Decimal_Division_Large()
    {
        return _largeDecimalA / _largeDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Division", "Large")]
    public BigNumber BigNumber_Division_Large()
    {
        return _largeBigNumberA / _largeBigNumberB;
    }

    #endregion

    #region Modulo Benchmarks

    [Benchmark]
    [BenchmarkCategory("Modulo", "Typical")]
    public decimal Decimal_Modulo_Typical()
    {
        return _typicalDecimalA % _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Modulo", "Typical")]
    public BigNumber BigNumber_Modulo_Typical()
    {
        return _typicalBigNumberA % _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Modulo", "Large")]
    public decimal Decimal_Modulo_Large()
    {
        return _largeDecimalA % _largeDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Modulo", "Large")]
    public BigNumber BigNumber_Modulo_Large()
    {
        return _largeBigNumberA % _largeBigNumberB;
    }

    #endregion

    #region Comparison Benchmarks

    [Benchmark]
    [BenchmarkCategory("Comparison", "Typical")]
    public bool Decimal_LessThan_Typical()
    {
        return _typicalDecimalA < _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison", "Typical")]
    public bool BigNumber_LessThan_Typical()
    {
        return _typicalBigNumberA < _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison", "Typical")]
    public bool Decimal_Equals_Typical()
    {
        return _typicalDecimalA == _typicalDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison", "Typical")]
    public bool BigNumber_Equals_Typical()
    {
        return _typicalBigNumberA == _typicalBigNumberB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison", "Large")]
    public bool Decimal_LessThan_Large()
    {
        return _largeDecimalA < _largeDecimalB;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison", "Large")]
    public bool BigNumber_LessThan_Large()
    {
        return _largeBigNumberA < _largeBigNumberB;
    }

    #endregion

    #region Mathematical Functions

    [Benchmark]
    [BenchmarkCategory("Math", "Round")]
    public decimal Decimal_Round_Typical()
    {
        return decimal.Round(_typicalDecimalA, 2);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Round")]
    public BigNumber BigNumber_Round_Typical()
    {
        return BigNumber.Round(_typicalBigNumberA, 2);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Floor")]
    public decimal Decimal_Floor_Typical()
    {
        return decimal.Floor(_typicalDecimalA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Floor")]
    public BigNumber BigNumber_Floor_Typical()
    {
        return BigNumber.Floor(_typicalBigNumberA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Ceiling")]
    public decimal Decimal_Ceiling_Typical()
    {
        return decimal.Ceiling(_typicalDecimalA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Ceiling")]
    public BigNumber BigNumber_Ceiling_Typical()
    {
        return BigNumber.Ceiling(_typicalBigNumberA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Truncate")]
    public decimal Decimal_Truncate_Typical()
    {
        return decimal.Truncate(_typicalDecimalA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Truncate")]
    public BigNumber BigNumber_Truncate_Typical()
    {
        return BigNumber.Truncate(_typicalBigNumberA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Abs")]
    public decimal Decimal_Abs_Typical()
    {
        return Math.Abs(-_typicalDecimalA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Abs")]
    public BigNumber BigNumber_Abs_Typical()
    {
        return BigNumber.Abs(-_typicalBigNumberA);
    }

    // Note: Decimal doesn't have Pow or Sqrt, so we compare with Math on doubles
    [Benchmark]
    [BenchmarkCategory("Math", "Sqrt")]
    public double Math_Sqrt_AsDouble()
    {
        return Math.Sqrt((double)_typicalDecimalA);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Sqrt")]
    public BigNumber BigNumber_Sqrt_Precision10()
    {
        return BigNumber.Sqrt(_typicalBigNumberA, 10);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Pow")]
    public double Math_Pow_AsDouble()
    {
        return Math.Pow((double)_typicalDecimalA, 2);
    }

    [Benchmark]
    [BenchmarkCategory("Math", "Pow")]
    public BigNumber BigNumber_Pow_Square()
    {
        return BigNumber.Pow(_typicalBigNumberA, 2);
    }

    #endregion

    #region Parsing Benchmarks

    [Benchmark]
    [BenchmarkCategory("Parsing", "Typical")]
    public decimal Decimal_Parse_Typical()
    {
        return decimal.Parse("123.456");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing", "Typical")]
    public BigNumber BigNumber_Parse_Typical()
    {
        return BigNumber.Parse("123.456");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing", "Scientific")]
    public decimal Decimal_Parse_Scientific()
    {
        return decimal.Parse("1.23456E+2");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing", "Scientific")]
    public BigNumber BigNumber_Parse_Scientific()
    {
        return BigNumber.Parse("1.23456E+2");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing", "Large")]
    public decimal Decimal_Parse_Large()
    {
        return decimal.Parse("79228162514264337593");
    }

    [Benchmark]
    [BenchmarkCategory("Parsing", "Large")]
    public BigNumber BigNumber_Parse_Large()
    {
        return BigNumber.Parse("79228162514264337593");
    }

    #endregion

    #region Formatting Benchmarks

    [Benchmark]
    [BenchmarkCategory("Formatting", "ToString")]
    public string Decimal_ToString_F2()
    {
        return _typicalDecimalA.ToString("F2");
    }

    [Benchmark]
    [BenchmarkCategory("Formatting", "ToString")]
    public string BigNumber_ToString_F2()
    {
        return _typicalBigNumberA.ToString("F2", null);
    }

#if NET
    [Benchmark]
    [BenchmarkCategory("Formatting", "TryFormat")]
    public bool Decimal_TryFormat_F2()
    {
        Span<char> buffer = stackalloc char[64];
        return _typicalDecimalA.TryFormat(buffer, out _, "F2", null);
    }

    [Benchmark]
    [BenchmarkCategory("Formatting", "TryFormat")]
    public bool BigNumber_TryFormat_F2()
    {
        Span<char> buffer = stackalloc char[64];
        return _typicalBigNumberA.TryFormat(buffer, out _, "F2", null);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Formatting", "TryFormatOptimized")]
    public bool BigNumber_TryFormatOptimized_F2()
    {
        Span<char> buffer = stackalloc char[64];
        return _typicalBigNumberA.TryFormatOptimized(buffer, out _, "F2", null);
    }

    #endregion

    #region Conversion Benchmarks

    [Benchmark]
    [BenchmarkCategory("Conversion")]
    public BigNumber Decimal_ToBigNumber()
    {
        return (BigNumber)_typicalDecimalA;
    }

    [Benchmark]
    [BenchmarkCategory("Conversion")]
    public decimal BigNumber_ToDecimal()
    {
        return (decimal)_typicalBigNumberA;
    }

    [Benchmark]
    [BenchmarkCategory("Conversion")]
    public double Decimal_ToDouble()
    {
        return (double)_typicalDecimalA;
    }

    [Benchmark]
    [BenchmarkCategory("Conversion")]
    public double BigNumber_ToDouble()
    {
        return (double)_typicalBigNumberA;
    }

    #endregion
}