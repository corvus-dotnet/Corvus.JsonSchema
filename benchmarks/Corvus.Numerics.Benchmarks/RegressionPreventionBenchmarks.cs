// <copyright file="RegressionPreventionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks to prevent performance regressions and verify optimizations.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class RegressionPreventionBenchmarks
{
    private readonly BigNumber _testNumber = new(12345, -2); // 123.45
    private readonly BigNumber _largeNumber = new(BigInteger.Parse("123456789012345678901234567890"), -15);

    /// <summary>
    /// Verifies power-of-10 cache effectiveness at various exponent ranges.
    /// </summary>
    [Benchmark]
    [Arguments(50)]   // Within original cache (0-127)
    [Arguments(127)]  // Original cache boundary
    [Arguments(128)]  // First element outside original cache
    [Arguments(200)]  // Within extended primary cache (0-255)
    [Arguments(255)]  // Extended primary cache boundary
    [Arguments(256)]  // First element in secondary lazy cache
    [Arguments(500)]  // Within secondary cache (256-1023)
    [Arguments(1000)] // Near secondary cache boundary
    public BigNumber MultiplyByPowerOf10(int exponent)
    {
        return _testNumber * new BigNumber(1, exponent);
    }

    /// <summary>
    /// Verifies InvariantCulture fast path performance for common formats.
    /// </summary>
    [Benchmark]
    public bool FormatInvariantCulture_F2()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Verifies InvariantCulture fast path performance for Number format.
    /// </summary>
    [Benchmark]
    public bool FormatInvariantCulture_N2()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "N2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Verifies InvariantCulture fast path performance for Exponential format.
    /// </summary>
    [Benchmark]
    public bool FormatInvariantCulture_E2()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "E2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Verifies InvariantCulture fast path performance for General format.
    /// </summary>
    [Benchmark]
    public bool FormatInvariantCulture_G()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "G", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Measures custom culture formatting performance (should use non-fast-path).
    /// </summary>
    [Benchmark]
    public bool FormatCustomCulture_F2_German()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "F2", new CultureInfo("de-DE"));
    }

    /// <summary>
    /// Measures custom culture formatting performance for French locale.
    /// </summary>
    [Benchmark]
    public bool FormatCustomCulture_N2_French()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "N2", new CultureInfo("fr-FR"));
    }

    /// <summary>
    /// Verifies zero allocation for null provider (should use InvariantCulture fast path).
    /// </summary>
    [Benchmark]
    public bool FormatNullProvider_F2()
    {
        Span<char> buffer = stackalloc char[64];
        return _testNumber.TryFormatOptimized(buffer, out _, "F2", null);
    }

    /// <summary>
    /// Verifies performance with high-precision formatting.
    /// </summary>
    [Benchmark]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(50)]
    public bool FormatHighPrecision_F(int precision)
    {
        Span<char> buffer = stackalloc char[256];
        return _largeNumber.TryFormatOptimized(buffer, out _, $"F{precision}", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compares InvariantCulture fast path vs explicit InvariantCulture formatting.
    /// </summary>
    [Benchmark]
    public bool CompareNullVsExplicitInvariant()
    {
        Span<char> buffer1 = stackalloc char[64];
        Span<char> buffer2 = stackalloc char[64];

        bool result1 = _testNumber.TryFormatOptimized(buffer1, out _, "F2", null);
        bool result2 = _testNumber.TryFormatOptimized(buffer2, out _, "F2", CultureInfo.InvariantCulture);

        return result1 && result2;
    }

    /// <summary>
    /// Measures UTF-8 formatting performance with InvariantCulture.
    /// </summary>
    [Benchmark]
    public bool FormatUtf8_InvariantCulture_F2()
    {
        Span<byte> buffer = stackalloc byte[64];
        return _testNumber.TryFormatUtf8Optimized(buffer, out _, "F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Measures UTF-8 JSON formatting (highly optimized path).
    /// </summary>
    [Benchmark]
    public bool FormatUtf8_Json_G()
    {
        Span<byte> buffer = stackalloc byte[64];
        return _testNumber.TryFormatUtf8Optimized(buffer, out _, "G", null);
    }

    /// <summary>
    /// Verifies batch formatting maintains zero allocations.
    /// </summary>
    [Benchmark]
    public int BatchFormat_100Numbers()
    {
        Span<char> buffer = stackalloc char[64];
        int successCount = 0;

        for (int i = 0; i < 100; i++)
        {
            var num = new BigNumber(i * 12345, -2);
            if (num.TryFormatOptimized(buffer, out _, "F2", CultureInfo.InvariantCulture))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Verifies various format strings with InvariantCulture.
    /// </summary>
    [Benchmark]
    public bool CycleThroughFormats()
    {
        Span<char> buffer = stackalloc char[128];

        return _testNumber.TryFormatOptimized(buffer, out _, "F2", CultureInfo.InvariantCulture) &&
               _testNumber.TryFormatOptimized(buffer, out _, "N2", CultureInfo.InvariantCulture) &&
               _testNumber.TryFormatOptimized(buffer, out _, "E6", CultureInfo.InvariantCulture) &&
               _testNumber.TryFormatOptimized(buffer, out _, "G", CultureInfo.InvariantCulture) &&
               _testNumber.TryFormatOptimized(buffer, out _, "C2", CultureInfo.InvariantCulture) &&
               _testNumber.TryFormatOptimized(buffer, out _, "P2", CultureInfo.InvariantCulture);
    }
}