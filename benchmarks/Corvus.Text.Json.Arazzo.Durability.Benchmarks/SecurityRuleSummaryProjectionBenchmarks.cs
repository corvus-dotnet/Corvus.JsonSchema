// <copyright file="SecurityRuleSummaryProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the security-rule warm read path (§14.2): turning a stored <see cref="SecurityRuleDocument"/> into its
/// <c>SecurityRuleSummary</c> response body for the single-document responses (<c>GET /security/rules/{name}</c> and
/// the <c>POST</c>/<c>PUT</c> rule responses). The summary is an exact, congruent projection of the persisted rule
/// (identical property names/types, same required set — <c>name</c>, <c>expression</c>, <c>createdBy</c>,
/// <c>createdAt</c>, <c>etag</c>; the summary is merely more permissive on <c>additionalProperties</c>), so the
/// handler can wrap the stored element (<c>SecurityRuleSummary.From</c>, a cross-assembly pointer reinterpret —
/// the <see cref="SecurityRuleDocument"/> root <em>is</em> a Corvus.Text.Json value) and serialize its backing
/// verbatim — versus the field-by-field rebuild it replaced, which realised a managed value for every scalar
/// (name, expression, description, createdBy, createdAt, lastUpdatedBy, lastUpdatedAt, etag). The headline result:
/// the <c>From()</c> projection allocates <b>zero</b> bytes on the warm path (the reused pooled writer copies the
/// backing), while the materialising projection pays O(fields) per request.
/// </summary>
/// <remarks>
/// The baseline reproduces the dominant cost of the field-by-field projection — realising every leaf accessor — by
/// writing those realised values into the same pooled writer the wrap path uses, so the reported delta is the
/// per-request materialisation the change removed (the generated builder the old handler also ran sits on top of
/// this, so the baseline is a conservative lower bound on the old cost). Mirrors
/// <see cref="AccessRequestViewProjectionBenchmarks"/>, the blessed projection template. The list response
/// (<c>GET /security/rules</c>) cannot use <c>From()</c> — its items reference a pooled batch disposed before the
/// array serializes — so it stays materialised and is out of scope here.
/// </remarks>
[MemoryDiagnoser]
public class SecurityRuleSummaryProjectionBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private ParsedJsonDocument<SecurityRuleDocument> document = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A rule that has been created and then updated — every optional field populated (description + lastUpdatedBy/At),
        // the largest realistic projection.
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft("team == $claim.team", "Team scope.");
        byte[] created = SecurityPolicySerialization.SerializeNewRule("team-scope", draft.RootElement, "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag("etag-1"));
        using ParsedJsonDocument<SecurityRuleDocument> createdDoc = ParsedJsonDocument<SecurityRuleDocument>.Parse(created.AsMemory());
        byte[] updated = SecurityPolicySerialization.SerializeUpdatedRule(createdDoc.RootElement, "rule", "team-scope", WorkflowEtag.None, draft.RootElement, "bob", DateTimeOffset.UnixEpoch.AddHours(8), new WorkflowEtag("etag-2"));

        this.document = ParsedJsonDocument<SecurityRuleDocument>.Parse(updated);
        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The field-by-field projection the handler replaced — realises a managed value per scalar field.</summary>
    /// <returns>The serialized body length (returned to defeat dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public long Materialize_fieldByField()
    {
        SecurityRuleDocument rule = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            WriteMaterialized(writer, rule);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>The element-wrap projection in use — <c>From()</c> reinterprets the backing, <c>WriteTo</c> copies it verbatim.</summary>
    /// <returns>The serialized body length.</returns>
    [Benchmark]
    public long ElementWrap_From()
    {
        Models.SecurityRuleSummary summary = Models.SecurityRuleSummary.From(this.document.RootElement);
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            summary.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // The projection the handler used to perform: realise every leaf as a managed value, then write the summary object.
    private static void WriteMaterialized(Utf8JsonWriter writer, SecurityRuleDocument rule)
    {
        string name = rule.NameValue;
        string expression = rule.ExpressionValue;
        string? description = rule.DescriptionOrNull;
        string createdBy = rule.CreatedByValue;
        DateTimeOffset createdAt = rule.CreatedAtValue;
        string? lastUpdatedBy = rule.UpdatedByOrNull;
        DateTimeOffset? lastUpdatedAt = rule.UpdatedAtValue;
        string etag = rule.EtagValue.Value ?? string.Empty;

        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("expression", expression);
        if (description is not null)
        {
            writer.WriteString("description", description);
        }

        writer.WriteString("createdBy", createdBy);
        writer.WriteString("createdAt", createdAt);
        if (lastUpdatedBy is not null)
        {
            writer.WriteString("lastUpdatedBy", lastUpdatedBy);
        }

        if (lastUpdatedAt is { } lastUpdatedAtValue)
        {
            writer.WriteString("lastUpdatedAt", lastUpdatedAtValue);
        }

        writer.WriteString("etag", etag);
        writer.WriteEndObject();
    }
}
