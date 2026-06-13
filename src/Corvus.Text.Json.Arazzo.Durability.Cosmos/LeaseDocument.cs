// <copyright file="LeaseDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a single-owner workflow lease, generated from
/// <c>Schemas/LeaseDocument.json</c>. Round-tripped through Corvus.Text.Json — never a reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/LeaseDocument.json")]
public readonly partial struct LeaseDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the lease owner.</summary>
    public string OwnerValue => (string)this.Owner;

    /// <summary>Gets the lease token.</summary>
    public string TokenValue => (string)this.Token;

    /// <summary>Gets the lease expiry in Unix milliseconds.</summary>
    public long ExpiresAtValue => (long)this.ExpiresAt;

    /// <summary>Builds a lease document, detached and ready to persist.</summary>
    /// <param name="id">The run id the lease guards.</param>
    /// <param name="owner">The lease owner.</param>
    /// <param name="token">The lease token.</param>
    /// <param name="expiresAtUnixMs">The lease expiry, Unix milliseconds.</param>
    /// <returns>The document.</returns>
    public static LeaseDocument Create(string id, string owner, string token, long expiresAtUnixMs)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, id);
            writer.WriteString(JsonPropertyNames.OwnerUtf8, owner);
            writer.WriteString(JsonPropertyNames.TokenUtf8, token);
            writer.WriteNumber(JsonPropertyNames.ExpiresAtUtf8, expiresAtUnixMs);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<LeaseDocument> doc = ParsedJsonDocument<LeaseDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a lease document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static LeaseDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<LeaseDocument> doc = ParsedJsonDocument<LeaseDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this lease document to its persisted JSON form.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }
}