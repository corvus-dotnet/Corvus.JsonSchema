// <copyright file="SecurityRuleDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The persisted JSON form of a <see cref="SecurityRuleRecord"/> (design §14.2), generated from
/// <c>Schemas/SecurityRuleDocument.json</c>. Backends round-trip a rule through this Corvus.Text.Json schema type
/// (<see cref="From"/> → <see cref="ToJsonBytes"/> to persist; <see cref="FromJson"/> → <see cref="ToRecord"/> to
/// read) — never a reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/SecurityRuleDocument.json")]
public readonly partial struct SecurityRuleDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Builds the document from a rule record, detached and ready to persist.</summary>
    /// <param name="record">The rule record.</param>
    /// <returns>The document.</returns>
    public static SecurityRuleDocument From(SecurityRuleRecord record)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.NameUtf8, record.Name);
            writer.WriteString(JsonPropertyNames.ExpressionUtf8, record.Expression);
            if (record.Description is { } description)
            {
                writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
            }

            writer.WriteString(JsonPropertyNames.CreatedByUtf8, record.CreatedBy);
            writer.WriteString(JsonPropertyNames.CreatedAtUtf8, record.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            if (record.UpdatedBy is { } updatedBy)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, updatedBy);
            }

            if (record.UpdatedAt is { } updatedAt)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt.ToString("O", CultureInfo.InvariantCulture));
            }

            writer.WriteString(JsonPropertyNames.EtagUtf8, record.Etag.Value ?? string.Empty);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<SecurityRuleDocument> doc = ParsedJsonDocument<SecurityRuleDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static SecurityRuleDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<SecurityRuleDocument> doc = ParsedJsonDocument<SecurityRuleDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this document to its persisted JSON form.</summary>
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

    /// <summary>Projects this document back to a <see cref="SecurityRuleRecord"/>.</summary>
    /// <returns>The rule record.</returns>
    public SecurityRuleRecord ToRecord()
        => new(
            (string)this.Name,
            (string)this.Expression,
            this.Description.IsNotUndefined() ? (string)this.Description : null,
            (string)this.CreatedBy,
            DateTimeOffset.Parse((string)this.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null,
            this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.Parse((string)this.LastUpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            new WorkflowEtag((string)this.Etag));
}