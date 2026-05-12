// <copyright file="FormattingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class FormattingBenchmarks
{
    private BigNumber _simpleNumber = default;
    private BigNumber _numberWithExponent = default;
    private BigNumber _largeNumber = default;
    private BigNumber _negativeNumber = default;

    [GlobalSetup]
    public void Setup()
    {
        this._simpleNumber = new BigNumber(12345, 0);
        this._numberWithExponent = new BigNumber(123, 10);
        this._largeNumber = new BigNumber(BigInteger.Parse("123456789012345678901234567890"), 0);
        this._negativeNumber = new BigNumber(-987654, -3);
    }

    #region UTF-16 Formatting Benchmarks

    [Benchmark(Baseline = true)]
    public string ToString_SimpleNumber_Allocating()
    {
        return this._simpleNumber.ToString();
    }

    [Benchmark]
    public int TryFormatOptimized_SimpleNumber_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, default, null);
        return charsWritten;
    }

    [Benchmark]
    public string ToString_NumberWithExponent_Allocating()
    {
        return this._numberWithExponent.ToString();
    }

    [Benchmark]
    public int TryFormatOptimized_NumberWithExponent_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[128];
        this._numberWithExponent.TryFormatOptimized(buffer, out int charsWritten, default, null);
        return charsWritten;
    }

    [Benchmark]
    public string ToString_LargeNumber_Allocating()
    {
        return this._largeNumber.ToString();
    }

    [Benchmark]
    public int TryFormatOptimized_LargeNumber_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[256];
        this._largeNumber.TryFormatOptimized(buffer, out int charsWritten, default, null);
        return charsWritten;
    }

    [Benchmark]
    public string ToString_NegativeNumber_Allocating()
    {
        return this._negativeNumber.ToString();
    }

    [Benchmark]
    public int TryFormatOptimized_NegativeNumber_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[128];
        this._negativeNumber.TryFormatOptimized(buffer, out int charsWritten, default, null);
        return charsWritten;
    }

    #endregion

    #region UTF-8 Formatting Benchmarks

    [Benchmark]
    public byte[] ToUtf8Bytes_SimpleNumber_Allocating()
    {
        string str = this._simpleNumber.ToString();
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int TryFormatUtf8Optimized_SimpleNumber_ZeroAllocation()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._simpleNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);
        return bytesWritten;
    }

    [Benchmark]
    public byte[] ToUtf8Bytes_NumberWithExponent_Allocating()
    {
        string str = this._numberWithExponent.ToString();
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int TryFormatUtf8Optimized_NumberWithExponent_ZeroAllocation()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._numberWithExponent.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);
        return bytesWritten;
    }

    [Benchmark]
    public byte[] ToUtf8Bytes_LargeNumber_Allocating()
    {
        string str = this._largeNumber.ToString();
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int TryFormatUtf8Optimized_LargeNumber_ZeroAllocation()
    {
        Span<byte> buffer = stackalloc byte[256];
        this._largeNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);
        return bytesWritten;
    }

    [Benchmark]
    public byte[] ToUtf8Bytes_NegativeNumber_Allocating()
    {
        string str = this._negativeNumber.ToString();
        return Encoding.UTF8.GetBytes(str);
    }

    [Benchmark]
    public int TryFormatUtf8Optimized_NegativeNumber_ZeroAllocation()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._negativeNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);
        return bytesWritten;
    }

    #endregion

    #region Format String Benchmarks

    [Benchmark]
    public string ToString_GeneralFormat_Allocating()
    {
        return this._simpleNumber.ToString("G", null);
    }

    [Benchmark]
    public int TryFormatOptimized_GeneralFormat_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);
        return charsWritten;
    }

    [Benchmark]
    public string ToString_ExponentialFormat_Allocating()
    {
        return this._simpleNumber.ToString("E2", null);
    }

    [Benchmark]
    public int TryFormatOptimized_ExponentialFormat_ZeroAllocation()
    {
        Span<char> buffer = stackalloc char[128];
        this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), null);
        return charsWritten;
    }

    #endregion

    #region High-Throughput Scenario Benchmarks

    [Benchmark]
    public long BatchFormatting_Allocating()
    {
        long sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            string result = this._simpleNumber.ToString();
            sum += result.Length;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormatting_ZeroAllocation()
    {
        long sum = 0;
        Span<char> buffer = stackalloc char[128];
        for (int i = 0; i < 1000; i++)
        {
            this._simpleNumber.TryFormatOptimized(buffer, out int charsWritten, default, null);
            sum += charsWritten;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormattingUtf8_Allocating()
    {
        long sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            string str = this._simpleNumber.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            sum += bytes.Length;
        }
        return sum;
    }

    [Benchmark]
    public long BatchFormattingUtf8_ZeroAllocation()
    {
        long sum = 0;
        Span<byte> buffer = stackalloc byte[128];
        for (int i = 0; i < 1000; i++)
        {
            this._simpleNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);
            sum += bytesWritten;
        }
        return sum;
    }

    #endregion
}