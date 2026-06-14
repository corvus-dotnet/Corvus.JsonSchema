// <copyright file="LeaseDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a single-owner workflow lease, generated from
/// <c>Schemas/LeaseDocument.json</c>. Round-tripped through Corvus.Text.Json — never a reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/LeaseDocument.json")]
public readonly partial struct LeaseDocument
{
    /// <summary>Gets the lease owner.</summary>
    public string OwnerValue => (string)this.Owner;

    /// <summary>Gets the lease token.</summary>
    public string TokenValue => (string)this.Token;

    /// <summary>Gets the lease expiry in Unix milliseconds.</summary>
    public long ExpiresAtValue => (long)this.ExpiresAt;

    /// <summary>
    /// Writes a lease document's persisted JSON straight to <paramref name="writer"/> — no intermediate
    /// <see cref="LeaseDocument"/> value and no re-serialization (the store hands this to <c>CosmosJson.WriteToStream</c>
    /// so the lease is serialized exactly once, into a pooled stream).
    /// </summary>
    /// <param name="writer">The writer to write the document to.</param>
    /// <param name="id">The run id the lease guards.</param>
    /// <param name="owner">The lease owner.</param>
    /// <param name="token">The lease token.</param>
    /// <param name="expiresAtUnixMs">The lease expiry, Unix milliseconds.</param>
    public static void WriteJson(Utf8JsonWriter writer, string id, string owner, string token, long expiresAtUnixMs)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        writer.WriteString(JsonPropertyNames.OwnerUtf8, owner);
        writer.WriteString(JsonPropertyNames.TokenUtf8, token);
        writer.WriteNumber(JsonPropertyNames.ExpiresAtUtf8, expiresAtUnixMs);
        writer.WriteEndObject();
    }

    /// <summary>Parses a lease document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static LeaseDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<LeaseDocument> doc = ParsedJsonDocument<LeaseDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }
}