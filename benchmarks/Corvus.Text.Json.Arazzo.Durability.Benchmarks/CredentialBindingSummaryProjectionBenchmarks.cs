// <copyright file="CredentialBindingSummaryProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the source-credential warm read path (§13): turning a stored <see cref="SourceCredentialBinding"/> into its
/// <c>CredentialBindingSummary</c> response body (GET /credentials and GET /credentials/{s}/{e}). Unlike the rule/access-
/// request summaries, this projection is <b>not</b> congruent — the summary <em>requires</em> a derived
/// <c>credentialStatus</c> (computed from <c>expiresAt</c> vs now, never persisted) and exposes operator-facing
/// <c>usageGrants</c> in place of the internal <c>usageTags</c> (the raw tags are deliberately hidden) — so <c>From()</c>
/// cannot be used and the handler must field-copy. The fix (#1) is narrower: the directly-copied scalar and array fields
/// are carried as UTF-8 spans (<see cref="UnescapedUtf8JsonString"/>, a pooled buffer — no per-field managed
/// <see cref="string"/>) rather than realised via <c>(string)</c> casts.
/// </summary>
/// <remarks>
/// This benchmark isolates exactly the part the fix changes — the directly-copied fields: the scalars (id, sourceName,
/// environment, createdBy, description) and the <c>secretRefs</c>/<c>config</c> arrays (name/ref, key/value). The two
/// genuine floors — the derived <c>credentialStatus</c> and the inverse-mapped <c>usageGrants</c>
/// (<c>DescribeUsageScope</c> returns a materialised list, the shared policy-seam leaf) — are identical in both arms and
/// excluded, so the reported delta is the per-request string realisation the bytes bridge removes. Hand-rolled writer
/// (the blessed conservative-lower-bound shape of <see cref="AccessRequestViewProjectionBenchmarks"/> /
/// <see cref="SecurityRuleSummaryProjectionBenchmarks"/>); the generated result builder sits on top of this in the real
/// handler.
/// </remarks>
[MemoryDiagnoser]
public class CredentialBindingSummaryProjectionBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    // A representative stored binding (one secretRef, one config entry, description + last-updated populated).
    private static readonly byte[] StoredJson =
        """
        {
          "id": "petstore@production",
          "sourceName": "petstore",
          "environment": "production",
          "authKind": "apiKey",
          "secretRefs": [ { "name": "value", "ref": "keyvault://petstore-apikey#3" } ],
          "config": [ { "key": "parameterName", "value": "X-Api-Key" } ],
          "description": "Pet store API key.",
          "createdBy": "alice",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "lastUpdatedBy": "bob",
          "lastUpdatedAt": "1970-01-01T08:00:00+00:00",
          "etag": "etag-1"
        }
        """u8.ToArray();

    private ParsedJsonDocument<SourceCredentialBinding> document = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<SourceCredentialBinding>.Parse(StoredJson);
        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The directly-copied fields realised via <c>(string)</c> casts — a managed string per scalar and per array
    /// leaf (the projection the handler performs today).</summary>
    /// <returns>The serialized body length (returned to defeat dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public long Materialize_fieldByField()
    {
        SourceCredentialBinding b = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("id", (string)b.Id);
            writer.WriteString("sourceName", (string)b.SourceName);
            writer.WriteString("environment", (string)b.Environment);
            writer.WriteString("createdBy", (string)b.CreatedBy);
            if (b.Description.IsNotUndefined())
            {
                writer.WriteString("description", (string)b.Description);
            }

            writer.WriteStartArray("secretRefs");
            foreach (SourceCredentialBinding.SecretReference reference in b.SecretRefs.EnumerateArray())
            {
                writer.WriteStartObject();
                writer.WriteString("name", (string)reference.Name);
                writer.WriteString("ref", (string)reference.Ref);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (b.Config.IsNotUndefined() && b.Config.GetArrayLength() > 0)
            {
                writer.WriteStartArray("config");
                foreach (SourceCredentialBinding.CredentialConfigEntry entry in b.Config.EnumerateArray())
                {
                    writer.WriteStartObject();
                    writer.WriteString("key", (string)entry.Key);
                    writer.WriteString("value", (string)entry.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>The same fields carried as UTF-8 spans (<see cref="UnescapedUtf8JsonString"/>, a pooled buffer) — no
    /// per-field managed string (the bytes bridge the fix applies via <c>(Models.JsonString.Source)span</c>).</summary>
    /// <returns>The serialized body length.</returns>
    [Benchmark]
    public long BytesBridge_utf8()
    {
        SourceCredentialBinding b = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            WriteUtf8(writer, "id"u8, b.Id);
            WriteUtf8(writer, "sourceName"u8, b.SourceName);
            WriteUtf8(writer, "environment"u8, b.Environment);
            WriteUtf8(writer, "createdBy"u8, b.CreatedBy);
            if (b.Description.IsNotUndefined())
            {
                WriteUtf8(writer, "description"u8, b.Description);
            }

            writer.WriteStartArray("secretRefs");
            foreach (SourceCredentialBinding.SecretReference reference in b.SecretRefs.EnumerateArray())
            {
                writer.WriteStartObject();
                WriteUtf8(writer, "name"u8, reference.Name);
                WriteUtf8(writer, "ref"u8, reference.Ref);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (b.Config.IsNotUndefined() && b.Config.GetArrayLength() > 0)
            {
                writer.WriteStartArray("config");
                foreach (SourceCredentialBinding.CredentialConfigEntry entry in b.Config.EnumerateArray())
                {
                    writer.WriteStartObject();
                    WriteUtf8(writer, "key"u8, entry.Key);
                    WriteUtf8(writer, "value"u8, entry.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private static void WriteUtf8(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonString value)
    {
        using UnescapedUtf8JsonString utf8 = value.GetUtf8String();
        writer.WriteString(name, utf8.Span);
    }
}
