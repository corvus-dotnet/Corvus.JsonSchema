// <copyright file="EnvelopeWriteBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Cosmos;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures building a Cosmos <c>{id, pk, doc:base64}</c> envelope (the security/runner write shape) plus the pooled
/// return document, both ways: the old shape — a fresh growing <see cref="MemoryStream"/> + fresh
/// <see cref="Utf8JsonWriter"/> per write (the original ~5.6 KB cost) — versus
/// <see cref="CosmosJson.WriteToStream{TContext}(in TContext, PersistedJson.WriteCallback{TContext})"/>, the pooled
/// writer cache + ArrayPool-backed stream. (The embedded document bytes are pre-serialized in setup; in production they
/// come from the pooled <see cref="CosmosJson.RentJson{TContext}"/>.)
/// </summary>
public class EnvelopeWriteBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private byte[] docBytes = null!;

    [GlobalSetup]
    public void Setup()
        => this.docBytes = SecurityPolicySerialization.SerializeNewRule(
            "tenant-scoped",
            new SecurityRuleDefinition("sys:tenant == $claim.tenant", "Tenant isolation."),
            "alice",
            DateTimeOffset.UnixEpoch,
            new WorkflowEtag("etag-1"));

    /// <summary>Old: fresh growing MemoryStream + fresh Utf8JsonWriter for the envelope, plus the pooled return doc.</summary>
    /// <returns>A value derived from the artifacts (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Old_NewStreamNewWriter()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("id"u8, "tenant-scoped");
            writer.WriteString("pk"u8, "rule");
            writer.WriteBase64String("doc"u8, this.docBytes);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.docBytes);
        return (int)stream.Length + (int)document.RootElement.ValueKind;
    }

    /// <summary>New: pooled CosmosJson.WriteToStream for the envelope, plus the pooled return doc.</summary>
    /// <returns>A value derived from the artifacts (prevents dead-code elimination).</returns>
    [Benchmark]
    public int New_WriteToStream()
    {
        using MemoryStream stream = CosmosJson.WriteToStream(
            this.docBytes,
            static (Utf8JsonWriter writer, in byte[] doc) =>
            {
                writer.WriteStartObject();
                writer.WriteString("id"u8, "tenant-scoped");
                writer.WriteString("pk"u8, "rule");
                writer.WriteBase64String("doc"u8, doc);
                writer.WriteEndObject();
            });

        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.docBytes);
        return (int)stream.Length + (int)document.RootElement.ValueKind;
    }
}