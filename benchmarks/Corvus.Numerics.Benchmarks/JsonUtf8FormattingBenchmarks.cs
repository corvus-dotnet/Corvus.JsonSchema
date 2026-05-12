// <copyright file="JsonUtf8FormattingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks for the highly-optimized JSON UTF-8 formatting fast path.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class JsonUtf8FormattingBenchmarks
{
    private BigNumber _zero;
    private BigNumber _simpleInteger;
    private BigNumber _negativeInteger;
    private BigNumber _withNegativeExp;
    private BigNumber _withPositiveExp;
    private BigNumber _largeNumber;
    private BigNumber _decimalNumber;

    [GlobalSetup]
    public void Setup()
    {
        this._zero = BigNumber.Zero;
        this._simpleInteger = new BigNumber(1234, 0);
        this._negativeInteger = new BigNumber(-5678, 0);
        this._withNegativeExp = new BigNumber(1234, -3);
        this._withPositiveExp = new BigNumber(1234, 2);
        this._largeNumber = new BigNumber(123456789012345678, 10);
        this._decimalNumber = BigNumber.Parse("123.456", CultureInfo.InvariantCulture);
    }

    #region JSON UTF-8 Fast Path

    [Benchmark]
    public int JsonUtf8_Zero()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._zero.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_SimpleInteger()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._simpleInteger.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_NegativeInteger()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._negativeInteger.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_WithNegativeExponent()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._withNegativeExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_WithPositiveExponent()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._withPositiveExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_LargeNumber()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._largeNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_DecimalNumber()
    {
        Span<byte> buffer = stackalloc byte[128];
        this._decimalNumber.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    #endregion

    #region Comparison: UTF-16 then Encode

#if NET
    [Benchmark]
    public int Utf16ThenEncode_SimpleInteger()
    {
        Span<char> charBuffer = stackalloc char[64];
        Span<byte> utf8Buffer = stackalloc byte[128];
        
        this._simpleInteger.TryFormatOptimized(charBuffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        int bytesWritten = Encoding.UTF8.GetBytes(charBuffer.Slice(0, charsWritten), utf8Buffer);
        
        return bytesWritten;
    }

    [Benchmark]
    public int Utf16ThenEncode_WithExponent()
    {
        Span<char> charBuffer = stackalloc char[64];
        Span<byte> utf8Buffer = stackalloc byte[128];
        
        this._withNegativeExp.TryFormatOptimized(charBuffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        int bytesWritten = Encoding.UTF8.GetBytes(charBuffer.Slice(0, charsWritten), utf8Buffer);
        
        return bytesWritten;
    }
#endif

    #endregion

    #region Batch Operations

    [Benchmark]
    public long BatchJsonUtf8_1000()
    {
        long sum = 0;
        Span<byte> buffer = stackalloc byte[128];

        for (int i = 0; i < 1000; i++)
        {
            this._withNegativeExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
            sum += bytesWritten;
        }

        return sum;
    }

#if NET
    [Benchmark]
    public long BatchUtf16ThenEncode_1000()
    {
        long sum = 0;
        Span<char> charBuffer = stackalloc char[64];
        Span<byte> utf8Buffer = stackalloc byte[128];
        
        for (int i = 0; i < 1000; i++)
        {
            this._withNegativeExp.TryFormatOptimized(charBuffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
            int bytesWritten = Encoding.UTF8.GetBytes(charBuffer.Slice(0, charsWritten), utf8Buffer);
            sum += bytesWritten;
        }
        
        return sum;
    }
#endif

    #endregion

    #region Format Variations

    [Benchmark]
    public int JsonUtf8_EmptyFormat()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._withNegativeExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_GFormat()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._withNegativeExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, "G".AsSpan(), CultureInfo.InvariantCulture);
        return bytesWritten;
    }

    [Benchmark]
    public int JsonUtf8_NullProvider()
    {
        Span<byte> buffer = stackalloc byte[64];
        this._withNegativeExp.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        return bytesWritten;
    }

    #endregion

    #region Real-World Scenarios

    [Benchmark]
    public int JsonArray_10Numbers()
    {
        Span<byte> buffer = stackalloc byte[512];
        int position = 0;

        buffer[position++] = (byte)'[';

        for (int i = 0; i < 10; i++)
        {
            if (i > 0)
            {
                buffer[position++] = (byte)',';
            }

            var num = new BigNumber(1234 + i, -3 + (i % 3));
            num.TryFormatUtf8Optimized(buffer.Slice(position), out int written, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
            position += written;
        }

        buffer[position++] = (byte)']';

        return position;
    }

    [Benchmark]
    public int JsonObject_5Properties()
    {
        Span<byte> buffer = stackalloc byte[512];
        int position = 0;

        // {"value1":1234E-3,"value2":-5678,"value3":9012E2,"value4":0,"value5":123}
        buffer[position++] = (byte)'{';

        string[] keys = { "value1", "value2", "value3", "value4", "value5" };
        BigNumber[] values =
        {
            new(1234, -3),
            new(-5678, 0),
            new(9012, 2),
            BigNumber.Zero,
            new(123, 0)
        };

        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0)
            {
                buffer[position++] = (byte)',';
            }

            // Write key
            buffer[position++] = (byte)'"';
            foreach (char c in keys[i])
            {
                buffer[position++] = (byte)c;
            }
            buffer[position++] = (byte)'"';
            buffer[position++] = (byte)':';

            // Write value
            values[i].TryFormatUtf8Optimized(buffer.Slice(position), out int written, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
            position += written;
        }

        buffer[position++] = (byte)'}';

        return position;
    }

    #endregion
}