// <copyright file="WriteToStreamBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Cosmos;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the Cosmos write-path serialization — turning a generated document into the <see cref="Stream"/> the Cosmos
/// stream API requires — both ways:
/// <list type="bullet">
/// <item><see cref="Unpooled_NewStreamNewWriter"/>: the regressing shape — a fresh growing <see cref="MemoryStream"/>
/// plus a fresh <see cref="Utf8JsonWriter"/> whose internal <see cref="System.Buffers.ArrayBufferWriter{T}"/> is
/// unpooled and grows by doubling — per write.</item>
/// <item><see cref="Pooled_WriteToStream"/>: <see cref="CosmosJson.WriteToStream{T}(in T)"/> — the thread-local pooled
/// writer cache (zero scratch GC) plus an <see cref="System.Buffers.ArrayPool{T}"/>-backed, non-growing stream, so the
/// only GC allocation per write is the small stream wrapper the SDK's <see cref="Stream"/> contract forces.</item>
/// </list>
/// This is the run-state hotpath write helper, so the per-write delta scales with run/lease/catalog write throughput.
/// </summary>
public class WriteToStreamBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private SecurityRuleDocument ruleDoc;

    [GlobalSetup]
    public void Setup()
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft("sys:tenant == $claim.tenant", "Tenant isolation.");
        byte[] json = SecurityPolicySerialization.SerializeNewRule(
            "tenant-scoped",
            draft.RootElement,
            "alice",
            DateTimeOffset.UnixEpoch,
            new WorkflowEtag("etag-1"));
        this.ruleDoc = SecurityRuleDocument.FromJson(json);
    }

    /// <summary>Regressed: fresh growing MemoryStream + fresh Utf8JsonWriter (unpooled internal buffer) per write.</summary>
    /// <returns>The written length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Unpooled_NewStreamNewWriter()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            this.ruleDoc.WriteTo(writer);
        }

        return (int)stream.Length;
    }

    /// <summary>Fixed: pooled writer cache + ArrayPool-backed non-growing stream (the production CosmosJson.WriteToStream).</summary>
    /// <returns>The written length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Pooled_WriteToStream()
    {
        using Stream stream = CosmosJson.WriteToStream(this.ruleDoc);
        return (int)stream.Length;
    }
}