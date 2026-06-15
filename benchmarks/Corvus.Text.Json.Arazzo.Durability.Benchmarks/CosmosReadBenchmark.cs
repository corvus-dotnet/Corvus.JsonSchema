// <copyright file="CosmosReadBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures draining a Cosmos response content stream (a seekable <see cref="MemoryStream"/>, as the SDK hands back)
/// into the UTF-8 payload the store parses — the former <c>CosmosJson.ReadAllAsync</c> (a fresh
/// <see cref="MemoryStream"/> + <c>CopyTo</c> + <c>ToArray</c>) versus the new pooled read-loop that sizes from the
/// stream length and rents from <see cref="ArrayPool{T}"/>. Both fire on every Cosmos read and every query page; the
/// payload is consumed transiently by the caller (parse → clone), so the pooled buffer is returned, not retained.
/// </summary>
public class CosmosReadBenchmark
{
    private byte[] payload = null!;
    private MemoryStream source = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A representative run/catalog document payload (~1 KB of JSON).
        this.payload = new byte[1024];
        for (int i = 0; i < this.payload.Length; i++)
        {
            this.payload[i] = (byte)('a' + (i % 26));
        }

        this.source = new MemoryStream(this.payload, writable: false);
    }

    /// <summary>Old: buffer the response into a fresh MemoryStream, then ToArray an owned copy.</summary>
    /// <returns>The payload length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Old_MemoryStream_ToArray()
    {
        this.source.Position = 0;
        using var buffer = new MemoryStream();
        this.source.CopyTo(buffer);
        byte[] owned = buffer.ToArray();
        return owned.Length;
    }

    /// <summary>New: size from the seekable length and drain into one pooled buffer, returned after use.</summary>
    /// <returns>The payload length.</returns>
    [Benchmark]
    public int New_PooledReadLoop()
    {
        this.source.Position = 0;
        int capacity = (int)this.source.Length;
        byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);
        int total = 0;
        try
        {
            while (true)
            {
                if (total == rented.Length)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(rented.Length * 2);
                    Array.Copy(rented, larger, total);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = larger;
                }

                int read = this.source.Read(rented.AsSpan(total, rented.Length - total));
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}