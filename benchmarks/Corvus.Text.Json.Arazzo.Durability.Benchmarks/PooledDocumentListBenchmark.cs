// <copyright file="PooledDocumentListBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures building a read-only batch of documents the old way (a <see cref="List{T}"/> whose backing array grows by
/// doubling and is GC-collected) versus the pooled <see cref="PooledDocumentList{T}"/> (an <see cref="System.Buffers.ArrayPool{T}"/>-rented
/// backing returned on dispose). Both add the same number of (default, no-op-dispose) document handles to isolate the
/// container cost; the delta is the List's backing array, which the pooled batch no longer allocates per list query.
/// </summary>
public class PooledDocumentListBenchmark
{
    [Params(20)]
    public int Count { get; set; }

    /// <summary>Old: a List whose backing array is GC-allocated and grown by doubling.</summary>
    /// <returns>The element count.</returns>
    [Benchmark(Baseline = true)]
    public int Old_ListBuild()
    {
        var list = new List<ParsedJsonDocument<SecurityRuleDocument>>();
        for (int i = 0; i < this.Count; i++)
        {
            list.Add(default!);
        }

        return list.Count;
    }

    /// <summary>New: a pooled batch whose rented backing array is returned to the pool on dispose.</summary>
    /// <returns>The element count.</returns>
    [Benchmark]
    public int New_PooledBuild()
    {
        using var batch = new PooledDocumentList<SecurityRuleDocument>();
        for (int i = 0; i < this.Count; i++)
        {
            batch.Add(default!);
        }

        return batch.Count;
    }
}