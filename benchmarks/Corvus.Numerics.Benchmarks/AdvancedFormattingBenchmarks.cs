// <copyright file="AdvancedFormattingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class AdvancedFormattingBenchmarks
{
    private BigNumber _simpleNumber = default;
    private BigNumber _decimalNumber = default;
    private BigNumber _largeNumber = default;

    [GlobalSetup]
    public void Setup()
    {
        this._simpleNumber = new BigNumber(12345, 0);
        this._decimalNumber = new BigNumber(12345, -2); // 123.45
        this._largeNumber = new BigNumber(BigInteger.Parse("123456789012345678901234567890"), 0);
    }

    #region Fixed-Point Format Benchmarks

    [Benchmark]
    public string FixedPoint_ToString_F2()
    {
        return this._decimalNumber.ToString("F2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int FixedPoint_TryFormatOptimized_F2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    [Benchmark]
    public string FixedPoint_ToString_F4()
    {
        return this._decimalNumber.ToString("F4", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int FixedPoint_TryFormatOptimized_F4()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F4".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region General Format Benchmarks

    [Benchmark]
    public string General_ToString_G()
    {
        return this._simpleNumber.ToString("G", null);
    }

    [Benchmark]
    public int General_TryFormatOptimized_G()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);
        return charsWritten;
    }

    [Benchmark]
    public string General_ToString_G5()
    {
        return this._simpleNumber.ToString("G5", null);
    }

    [Benchmark]
    public int General_TryFormatOptimized_G5()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "G5".AsSpan(), null);
        return charsWritten;
    }

    #endregion

    #region Exponential Format Benchmarks

    [Benchmark]
    public string Exponential_ToString_E2()
    {
        return this._simpleNumber.ToString("E2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int Exponential_TryFormatOptimized_E2()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    [Benchmark]
    public string Exponential_ToString_E6()
    {
        return this._simpleNumber.ToString("E6", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int Exponential_TryFormatOptimized_E6()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "E6".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region Percent Format Benchmarks

    [Benchmark]
    public string Percent_ToString_P2()
    {
        return this._decimalNumber.ToString("P2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int Percent_TryFormatOptimized_P2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "P2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region Number Format Benchmarks

    [Benchmark]
    public string Number_ToString_N2()
    {
        return this._decimalNumber.ToString("N2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int Number_TryFormatOptimized_N2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "N2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region Currency Format Benchmarks

    [Benchmark]
    public string Currency_ToString_C2()
    {
        return this._decimalNumber.ToString("C2", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int Currency_TryFormatOptimized_C2()
    {
        Span<char> buffer = stackalloc char[128];
        this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "C2".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region UTF-8 Benchmarks

    [Benchmark]
    public byte[] Utf8_ToString_ThenEncode_F2()
    {
        string str = this._decimalNumber.ToString("F2", CultureInfo.InvariantCulture);
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int Utf8_TryFormatUtf8Optimized_F2()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._decimalNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public byte[] Utf8_ToString_ThenEncode_E2()
    {
        string str = this._simpleNumber.ToString("E2", CultureInfo.InvariantCulture);
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int Utf8_TryFormatUtf8Optimized_E2()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._simpleNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    #endregion

    #region Large Number Benchmarks

    [Benchmark]
    public string LargeNumber_ToString_G()
    {
        return this._largeNumber.ToString("G", null);
    }

    [Benchmark]
    public int LargeNumber_TryFormatOptimized_G()
    {
        Span<char> buffer = stackalloc char[512];
        this._largeNumber.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);
        return charsWritten;
    }

    [Benchmark]
    public string LargeNumber_ToString_F10()
    {
        return this._largeNumber.ToString("F10", CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public int LargeNumber_TryFormatOptimized_F10()
    {
        Span<char> buffer = stackalloc char[512];
        this._largeNumber.TryFormatOptimized(buffer, out int charsWritten, "F10".AsSpan(), CultureInfo.InvariantCulture);
        return charsWritten;
    }

    #endregion

    #region Batch Processing Benchmarks

    [Benchmark]
    public long BatchFormatting_ToString_F2()
    {
        long sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            string result = this._decimalNumber.ToString("F2", CultureInfo.InvariantCulture);
            sum += result.Length;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormatting_TryFormatOptimized_F2()
    {
        long sum = 0;
        Span<char> buffer = stackalloc char[128];
        for (int i = 0; i < 1000; i++)
        {
            this._decimalNumber.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
            sum += charsWritten;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormatting_ToString_E2()
    {
        long sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            string result = this._simpleNumber.ToString("E2", CultureInfo.InvariantCulture);
            sum += result.Length;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormatting_TryFormatOptimized_E2()
    {
        long sum = 0;
        Span<char> buffer = stackalloc char[128];
        for (int i = 0; i < 1000; i++)
        {
            this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);
            sum += charsWritten;
        }
        return sum;
    }

    #endregion
}