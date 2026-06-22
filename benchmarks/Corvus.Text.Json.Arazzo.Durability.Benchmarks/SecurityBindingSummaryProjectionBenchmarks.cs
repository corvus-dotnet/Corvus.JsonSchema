// <copyright file="SecurityBindingSummaryProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the security-binding warm read path (§14.2): turning a stored <see cref="SecurityBindingDocument"/> into its
/// <c>SecurityBindingSummary</c> response body (GET /security/bindings and GET /security/bindings/{id}). A whole-document
/// <c>From()</c> is impossible — the stored binding carries <c>scopes</c>/<c>expiresAt</c>/<c>eligibleOnly</c> that the
/// open summary must NOT leak — so the handler field-copies a selected subset. The fix (#3) carries each selected leaf
/// bytes-native: the scalar strings via <c>Models.JsonString.From(binding.X)</c> and the three verb grants via
/// <c>Models.VerbGrant.From(binding.Read/.Write/.Purge)</c> (the stored <c>VerbGrantInfo</c> is congruent with the summary
/// <c>VerbGrant</c> — same optional props), rather than realising a managed <see cref="string"/> per scalar and rebuilding
/// each grant (a managed string per rule name).
/// </summary>
/// <remarks>
/// Isolates the convertible part: the selected string scalars (id, claimType, claimValue, createdBy, description,
/// lastUpdatedBy, etag) and the read/write/purge grants (rule-name arrays). The bytes-native arm writes each via
/// <c>WriteTo</c> (the writer analog of the element <c>From()</c> the handler uses — copies the backing verbatim, no
/// managed string), the baseline realises strings (the projection the handler performs today). Hand-rolled writer (the
/// blessed conservative-lower-bound shape of <see cref="SecurityRuleSummaryProjectionBenchmarks"/>); the generated result
/// builder sits on top in the real handler. Order/dates are written identically in both arms (value types, no string), so
/// the reported delta is the per-request string realisation the bridge removes.
/// </remarks>
[MemoryDiagnoser]
public class SecurityBindingSummaryProjectionBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    // A representative stored binding: a groups→rules binding with a multi-rule read grant, full write, none purge, plus
    // the hidden scopes/expiresAt/eligibleOnly the summary must not project, and every optional summary field populated.
    private static readonly byte[] StoredJson =
        """
        {
          "id": "binding-42",
          "claimType": "groups",
          "claimValue": "platform-engineers",
          "read": { "ruleNames": [ "team-scope", "env-prod" ], "unrestricted": false },
          "write": { "unrestricted": true },
          "purge": { "unrestricted": false },
          "scopes": [ "runs:read" ],
          "expiresAt": "1970-01-01T08:00:00+00:00",
          "eligibleOnly": true,
          "order": 10,
          "description": "Platform engineers binding.",
          "createdBy": "alice",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "lastUpdatedBy": "bob",
          "lastUpdatedAt": "1970-01-01T08:00:00+00:00",
          "etag": "etag-1"
        }
        """u8.ToArray();

    private ParsedJsonDocument<SecurityBindingDocument> document = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<SecurityBindingDocument>.Parse(StoredJson);
        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The selected fields realised via <c>(string)</c> casts + rebuilt verb grants — a managed string per scalar
    /// and per rule name (the projection the handler performs today).</summary>
    /// <returns>The serialized body length (returned to defeat dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public long Materialize_fieldByField()
    {
        SecurityBindingDocument b = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("id", (string)b.Id);
            writer.WriteString("claimType", (string)b.ClaimType);
            if (b.ClaimValue.IsNotUndefined())
            {
                writer.WriteString("claimValue", (string)b.ClaimValue);
            }

            WriteGrantMaterialized(writer, "read", b.Read);
            WriteGrantMaterialized(writer, "write", b.Write);
            WriteGrantMaterialized(writer, "purge", b.Purge);
            writer.WriteNumber("order", (int)b.Order);
            if (b.Description.IsNotUndefined())
            {
                writer.WriteString("description", (string)b.Description);
            }

            writer.WriteString("createdBy", (string)b.CreatedBy);
            writer.WritePropertyName("createdAt");
            b.CreatedAt.WriteTo(writer);
            writer.WriteString("etag", (string)b.Etag);
            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>The same selected fields carried bytes-native via <c>WriteTo</c> (the writer analog of the element
    /// <c>From()</c> the fix applies) — no per-field managed string, the grant objects copied verbatim.</summary>
    /// <returns>The serialized body length.</returns>
    [Benchmark]
    public long BytesNative_From()
    {
        SecurityBindingDocument b = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 512, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            WriteValue(writer, "id", b.Id);
            WriteValue(writer, "claimType", b.ClaimType);
            if (b.ClaimValue.IsNotUndefined())
            {
                WriteValue(writer, "claimValue", b.ClaimValue);
            }

            WriteGrantNative(writer, "read", b.Read);
            WriteGrantNative(writer, "write", b.Write);
            WriteGrantNative(writer, "purge", b.Purge);
            writer.WriteNumber("order", (int)b.Order);
            if (b.Description.IsNotUndefined())
            {
                WriteValue(writer, "description", b.Description);
            }

            WriteValue(writer, "createdBy", b.CreatedBy);
            writer.WritePropertyName("createdAt");
            b.CreatedAt.WriteTo(writer);
            WriteValue(writer, "etag", b.Etag);
            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, in JsonString value)
    {
        writer.WritePropertyName(name);
        value.WriteTo(writer);
    }

    // The grant projection the handler performs today: realise a managed string per rule name, then write the object.
    private static void WriteGrantMaterialized(Utf8JsonWriter writer, string name, in SecurityBindingDocument.VerbGrantInfo grant)
    {
        writer.WriteStartObject(name);
        if (grant.HasRuleNames)
        {
            writer.WriteStartArray("ruleNames");
            foreach (JsonString ruleName in grant.RuleNames.EnumerateArray())
            {
                writer.WriteStringValue((string)ruleName);
            }

            writer.WriteEndArray();
        }

        writer.WriteBoolean("unrestricted", grant.IsUnrestrictedValue);
        writer.WriteEndObject();
    }

    // The bytes-native grant projection the fix applies: the stored grant object copied verbatim (Models.VerbGrant.From).
    private static void WriteGrantNative(Utf8JsonWriter writer, string name, in SecurityBindingDocument.VerbGrantInfo grant)
    {
        writer.WritePropertyName(name);
        grant.WriteTo(writer);
    }
}
