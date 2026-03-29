// <copyright file="JsonUtf8ParsingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Numerics;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Benchmarks for JSON UTF-8 parsing with direct UTF-8 parsing vs char conversion.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MultiRuntimeConfig))]
public class JsonUtf8ParsingBenchmarks
{
    private byte[] _zero = null!;
    private byte[] _simpleInteger = null!;
    private byte[] _negativeInteger = null!;
    private byte[] _withNegativeExp = null!;
    private byte[] _withPositiveExp = null!;
    private byte[] _largeNumber = null!;
    private byte[] _decimalNumber = null!;
    private byte[] _scientificNotation = null!;
    private byte[] _veryLargeNumber = null!;
    private byte[] _withWhitespace = null!;

    [GlobalSetup]
    public void Setup()
    {
        this._zero = "0"u8.ToArray();
        this._simpleInteger = "1234"u8.ToArray();
        this._negativeInteger = "-5678"u8.ToArray();
        this._withNegativeExp = "1234E-3"u8.ToArray();
        this._withPositiveExp = "1234E2"u8.ToArray();
        this._largeNumber = "123456789012345678"u8.ToArray();
        this._decimalNumber = "123.456"u8.ToArray();
        this._scientificNotation = "1.23E10"u8.ToArray();
        this._veryLargeNumber = Encoding.UTF8.GetBytes(new string('9', 50));
        this._withWhitespace = "  123.45  "u8.ToArray();
    }

    #region Direct UTF-8 Parsing (Current Implementation)

    [Benchmark]
    public BigNumber DirectUtf8_Zero()
    {
        BigNumber.TryParseJsonUtf8(this._zero, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_SimpleInteger()
    {
        BigNumber.TryParseJsonUtf8(this._simpleInteger, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_NegativeInteger()
    {
        BigNumber.TryParseJsonUtf8(this._negativeInteger, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_WithNegativeExponent()
    {
        BigNumber.TryParseJsonUtf8(this._withNegativeExp, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_WithPositiveExponent()
    {
        BigNumber.TryParseJsonUtf8(this._withPositiveExp, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_LargeNumber()
    {
        BigNumber.TryParseJsonUtf8(this._largeNumber, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_DecimalNumber()
    {
        BigNumber.TryParseJsonUtf8(this._decimalNumber, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_ScientificNotation()
    {
        BigNumber.TryParseJsonUtf8(this._scientificNotation, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_VeryLargeNumber()
    {
        BigNumber.TryParseJsonUtf8(this._veryLargeNumber, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber DirectUtf8_WithWhitespace()
    {
        BigNumber.TryParseJsonUtf8(this._withWhitespace, out BigNumber result);
        return result;
    }

    #endregion

    #region Comparison: Standard TryParse (UTF-8 → Char)

    [Benchmark]
    public BigNumber StandardParse_SimpleInteger()
    {
        BigNumber.TryParse(this._simpleInteger, NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber StandardParse_WithExponent()
    {
        BigNumber.TryParse(this._withNegativeExp, NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber StandardParse_DecimalNumber()
    {
        BigNumber.TryParse(this._decimalNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);
        return result;
    }

    [Benchmark]
    public BigNumber StandardParse_LargeNumber()
    {
        BigNumber.TryParse(this._largeNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);
        return result;
    }

    #endregion

    #region Batch Operations

    [Benchmark]
    public int BatchDirectUtf8_1000()
    {
        int count = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (BigNumber.TryParseJsonUtf8(this._withNegativeExp, out _))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    public int BatchStandardParse_1000()
    {
        int count = 0;

        for (int i = 0; i < 1000; i++)
        {
            if (BigNumber.TryParse(this._withNegativeExp, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    #region Roundtrip Scenarios

#if NET
    [Benchmark]
    public bool Roundtrip_DirectUtf8()
    {
        // Format
        BigNumber original = new(12345, -2);
        Span<byte> buffer = stackalloc byte[64];
        original.TryFormat(buffer, out int bytesWritten, "", null);
        
        // Parse
        BigNumber.TryParseJsonUtf8(buffer.Slice(0, bytesWritten), out BigNumber result);
        
        return result == original;
    }

    [Benchmark]
    public bool Roundtrip_StandardParse()
    {
        // Format
        BigNumber original = new(12345, -2);
        Span<byte> buffer = stackalloc byte[64];
        original.TryFormat(buffer, out int bytesWritten, "", null);
        
        // Parse
        BigNumber.TryParse(buffer.Slice(0, bytesWritten), NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);
        
        return result == original;
    }
#endif

    #endregion

    #region Real-World JSON Scenarios

    [Benchmark]
    public int ParseJsonArray_10Numbers()
    {
        // Simulate parsing: [123,456E-2,-789,0,1E5,234E-3,567,890E2,-123E-4,456]
        byte[] json = "[123,456E-2,-789,0,1E5,234E-3,567,890E2,-123E-4,456]"u8.ToArray();

        int count = 0;
        int position = 1; // Skip opening '['

        while (position < json.Length && json[position] != (byte)']')
        {
            // Skip comma
            if (json[position] == (byte)',')
            {
                position++;
            }

            // Find next comma or close bracket
            int start = position;
            while (position < json.Length && json[position] != (byte)',' && json[position] != (byte)']')
            {
                position++;
            }

            // Parse number
            if (BigNumber.TryParseJsonUtf8(json.AsSpan(start, position - start), out _))
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public int ParseMultipleValues_Mixed()
    {
        // Parse various number formats
        int successCount = 0;

        if (BigNumber.TryParseJsonUtf8("0"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("123"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("-456"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("789.123"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("1E5"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("-2.34E-10"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("999999999999999"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("0.000000001"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("123456E-6"u8, out _)) successCount++;
        if (BigNumber.TryParseJsonUtf8("-987E3"u8, out _)) successCount++;

        return successCount;
    }

    #endregion
}