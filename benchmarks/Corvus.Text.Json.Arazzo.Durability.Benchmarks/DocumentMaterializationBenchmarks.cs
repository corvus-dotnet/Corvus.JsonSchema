// <copyright file="DocumentMaterializationBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Compares the two ways a store hands a persisted document back to a consumer: the <em>detached</em>
/// <see cref="SecurityRuleDocument.FromJson"/> (owns a fresh GC <see cref="byte"/> array + metadata every call) versus
/// the <em>pooled</em> <see cref="PersistedJson.ToPooledDocument{T}"/> (rents its backing buffer + metadata from the
/// pool and returns them on dispose, so only the small document wrapper hits the GC heap).
/// </summary>
public class DocumentMaterializationBenchmarks
{
    private byte[] ruleJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        SecurityRuleDocument rule = SecurityRuleDocument.CreateRule(
            "tenant-scoped",
            new SecurityRuleDefinition("sys:tenant == $claim.tenant", "Tenant isolation."),
            "alice",
            DateTimeOffset.UnixEpoch,
            new WorkflowEtag("etag-1"));
        this.ruleJson = PersistedJson.ToArray(rule, static (Utf8JsonWriter writer, in SecurityRuleDocument r) => r.WriteTo(writer));
    }

    /// <summary>Pure materialization, detached: owns a fresh byte[] + metadata each call.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark(Baseline = true)]
    public JsonValueKind Detached_Materialize()
    {
        SecurityRuleDocument document = SecurityRuleDocument.FromJson(this.ruleJson);
        return document.ValueKind;
    }

    /// <summary>Pure materialization, pooled: rents + returns the backing buffer; only the wrapper is GC.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind Pooled_Materialize()
    {
        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.ruleJson);
        return document.RootElement.ValueKind;
    }

    /// <summary>Realistic read, detached: materialize then read a field (the field string is a leaf, common to both).</summary>
    /// <returns>The rule name.</returns>
    [Benchmark]
    public string Detached_ReadField()
    {
        SecurityRuleDocument document = SecurityRuleDocument.FromJson(this.ruleJson);
        return document.NameValue;
    }

    /// <summary>Realistic read, pooled: materialize, read a field, dispose (buffer recycled).</summary>
    /// <returns>The rule name.</returns>
    [Benchmark]
    public string Pooled_ReadField()
    {
        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.ruleJson);
        return document.RootElement.NameValue;
    }
}