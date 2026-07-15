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
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft("sys:tenant == $claim.tenant", "Tenant isolation.");
        this.ruleJson = SecurityPolicySerialization.SerializeNewRule(
            "tenant-scoped",
            draft.RootElement,
            "alice",
            DateTimeOffset.UnixEpoch,
            new WorkflowEtag("etag-1"));
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

    /// <summary>Create() adoption row 1.1 "before": the replaced draft construction — a writer callback serialized
    /// through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/> and reparsed into the pooled document. The old
    /// shape is preserved inline here so the row's delta stays measured, not asserted.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind RunnerAuthorizationDraft_SerializeReparse()
    {
        var state = ("production", "runner-01");
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft =
            PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization, (string Environment, string RunnerId)>(
                in state,
                static (Utf8JsonWriter writer, in (string Environment, string RunnerId) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("environment"u8, c.Environment);
                    writer.WriteString("runnerId"u8, c.RunnerId);
                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.1 "after": the production <see cref="EnvironmentRunnerAuthorization.Draft"/>,
    /// now the generated <c>Create()</c> — document text and parse metadata written in one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind RunnerAuthorizationDraft_Create()
    {
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft = EnvironmentRunnerAuthorization.Draft("production", "runner-01");
        return draft.RootElement.ValueKind;
    }
}