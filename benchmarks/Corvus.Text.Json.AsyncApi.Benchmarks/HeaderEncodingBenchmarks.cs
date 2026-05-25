// <copyright file="HeaderEncodingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace AsyncApiBenchmark;

/// <summary>
/// Transport-specific header encoding/decoding overhead.
/// Measures the cost of encoding JSON headers to/from base64 (the pattern
/// used by NATS/MQTT transports that don't have native header maps).
/// </summary>
[MemoryDiagnoser]
public class HeaderEncodingBenchmarks
{
    private byte[] headersJson = null!;
    private byte[] headersBase64 = null!;

    [Params(1, 5, 10)]
    public int HeaderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Build a JSON object with N header pairs
        StringBuilder sb = new();
        sb.Append('{');
        for (int i = 0; i < this.HeaderCount; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"x-header-{i}\":\"value-{i}\"");
        }

        sb.Append('}');

        this.headersJson = Encoding.UTF8.GetBytes(sb.ToString());
        this.headersBase64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(this.headersJson));
    }

    [Benchmark(Description = "Encode: JSON → Base64 (NATS/MQTT pattern)", Baseline = true)]
    public int EncodeHeaders_Base64()
    {
        string base64 = Convert.ToBase64String(this.headersJson);
        return base64.Length;
    }

    [Benchmark(Description = "Decode: Base64 → JSON (NATS/MQTT pattern)")]
    public int DecodeHeaders_Base64()
    {
        byte[] decoded = Convert.FromBase64String(Encoding.UTF8.GetString(this.headersBase64));
        return decoded.Length;
    }

    [Benchmark(Description = "Encode: Corvus headers (WriteTo)")]
    public int EncodeHeaders_Corvus()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(this.headersJson);

        ArrayBufferWriter<byte> buffer = new(256);
        using Utf8JsonWriter writer = new(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark(Description = "Decode: Corvus headers (Parse)")]
    public int DecodeHeaders_Corvus()
    {
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(this.headersJson);

        int count = 0;
        foreach (JsonProperty<JsonElement> property in doc.RootElement.EnumerateObject())
        {
            count++;
        }

        return count;
    }
}