// <copyright file="EnvelopeBuildBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Reproduces the Cosmos security-store write path — build the <c>{id, pk, doc}</c> base64 envelope stream AND hand back
/// a pooled document over the same bytes — both ways, to measure the allocation cost the streaming fix removes:
/// <list type="bullet">
/// <item><see cref="OwnedByteArray"/>: the regressed shape — materialize the document as an <em>owned</em>
/// <see cref="byte"/> array, base64 it into the envelope, then copy it again into the pooled return document (two
/// materializations of the document bytes).</item>
/// <item><see cref="PooledScratch"/>: the fixed shape — serialize the document <em>once</em> into a pooled scratch
/// buffer, base64 it straight from that span into the envelope, and build the pooled return document from the same span
/// (no owned <see cref="byte"/> array).</item>
/// </list>
/// Both build the identical envelope <see cref="MemoryStream"/> and dispose an equivalent pooled return document, so the
/// reported delta is exactly the owned document array the fix eliminates.
/// </summary>
public class EnvelopeBuildBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private SecurityRuleDefinition definition;

    [GlobalSetup]
    public void Setup() => this.definition = new SecurityRuleDefinition("sys:tenant == $claim.tenant", "Tenant isolation.");

    /// <summary>Regressed: owned byte[] for the doc, base64'd into the envelope, then copied again into the pooled return doc.</summary>
    /// <returns>A value derived from the built artifacts (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int OwnedByteArray()
    {
        byte[] json = SecurityPolicySerialization.SerializeNewRule("tenant-scoped", this.definition, "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag("etag-1"));
        using MemoryStream stream = Envelope("tenant-scoped", "rule", json);
        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
        return (int)stream.Length + (int)document.RootElement.ValueKind;
    }

    /// <summary>Fixed: serialize the doc once into pooled scratch; base64 from the span; build the pooled return doc from the same span.</summary>
    /// <returns>A value derived from the built artifacts (prevents dead-code elimination).</returns>
    [Benchmark]
    public int PooledScratch()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter docWriter = workspace.RentWriterAndBuffer(512, out IByteBufferWriter docBuffer);
        try
        {
            SecurityRuleDocument.WriteNew(docWriter, "tenant-scoped", this.definition, "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag("etag-1"));
            docWriter.Flush();
            ReadOnlySpan<byte> docBytes = docBuffer.WrittenSpan;
            using MemoryStream stream = Envelope("tenant-scoped", "rule", docBytes);
            using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(docBytes);
            return (int)stream.Length + (int)document.RootElement.ValueKind;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(docWriter, docBuffer);
        }
    }

    private static MemoryStream Envelope(string id, string partition, ReadOnlySpan<byte> document)
    {
        var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("id"u8, id);
            writer.WriteString("pk"u8, partition);
            writer.WriteBase64String("doc"u8, document);
            writer.WriteEndObject();
        }

        stream.Position = 0;
        return stream;
    }
}