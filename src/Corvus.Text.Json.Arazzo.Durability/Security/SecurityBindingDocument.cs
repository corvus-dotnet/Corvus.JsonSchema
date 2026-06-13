// <copyright file="SecurityBindingDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The persisted JSON form of a <see cref="SecurityBinding"/> (design §14.2), generated from
/// <c>Schemas/SecurityBindingDocument.json</c>. Backends round-trip a binding through this Corvus.Text.Json schema
/// type (<see cref="From"/> → <see cref="ToJsonBytes"/> to persist; <see cref="FromJson"/> → <see cref="ToRecord"/>
/// to read) — never a reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/SecurityBindingDocument.json")]
public readonly partial struct SecurityBindingDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Builds the document from a binding, detached and ready to persist.</summary>
    /// <param name="binding">The binding.</param>
    /// <returns>The document.</returns>
    public static SecurityBindingDocument From(SecurityBinding binding)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, binding.Id);
            writer.WriteString(JsonPropertyNames.ClaimTypeUtf8, binding.ClaimType);
            if (binding.ClaimValue is { } claimValue)
            {
                writer.WriteString(JsonPropertyNames.ClaimValueUtf8, claimValue);
            }

            WriteGrant(writer, JsonPropertyNames.ReadUtf8, binding.Read);
            WriteGrant(writer, JsonPropertyNames.WriteUtf8, binding.Write);
            WriteGrant(writer, JsonPropertyNames.PurgeUtf8, binding.Purge);
            writer.WriteNumber(JsonPropertyNames.OrderUtf8, binding.Order);
            if (binding.Description is { } description)
            {
                writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
            }

            writer.WriteString(JsonPropertyNames.CreatedByUtf8, binding.CreatedBy);
            writer.WriteString(JsonPropertyNames.CreatedAtUtf8, binding.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            if (binding.UpdatedBy is { } updatedBy)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, updatedBy);
            }

            if (binding.UpdatedAt is { } updatedAt)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt.ToString("O", CultureInfo.InvariantCulture));
            }

            writer.WriteString(JsonPropertyNames.EtagUtf8, binding.Etag.Value ?? string.Empty);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<SecurityBindingDocument> doc = ParsedJsonDocument<SecurityBindingDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static SecurityBindingDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<SecurityBindingDocument> doc = ParsedJsonDocument<SecurityBindingDocument>.Parse(utf8);
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

    /// <summary>Projects this document back to a <see cref="SecurityBinding"/>.</summary>
    /// <returns>The binding.</returns>
    public SecurityBinding ToRecord()
        => new(
            (string)this.Id,
            (string)this.ClaimType,
            this.ClaimValue.IsNotUndefined() ? (string)this.ClaimValue : null,
            ReadGrant(this.Read),
            ReadGrant(this.Write),
            ReadGrant(this.Purge),
            this.Order,
            this.Description.IsNotUndefined() ? (string)this.Description : null,
            (string)this.CreatedBy,
            DateTimeOffset.Parse((string)this.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null,
            this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.Parse((string)this.LastUpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            new WorkflowEtag((string)this.Etag));

    private static void WriteGrant(Utf8JsonWriter writer, ReadOnlySpan<byte> propertyName, VerbGrant grant)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteBoolean(VerbGrantInfo.JsonPropertyNames.UnrestrictedUtf8, grant.Unrestricted);
        if (grant.RuleNames.Count > 0)
        {
            writer.WriteStartArray(VerbGrantInfo.JsonPropertyNames.RuleNamesUtf8);
            foreach (string ruleName in grant.RuleNames)
            {
                writer.WriteStringValue(ruleName);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static VerbGrant ReadGrant(in VerbGrantInfo grant)
    {
        if (grant.IsUndefined())
        {
            return VerbGrant.None;
        }

        bool unrestricted = grant.Unrestricted.IsNotUndefined() && (bool)grant.Unrestricted;
        var names = new List<string>();
        if (grant.RuleNames.IsNotUndefined())
        {
            foreach (JsonString ruleName in grant.RuleNames.EnumerateArray())
            {
                names.Add((string)ruleName);
            }
        }

        return new VerbGrant(unrestricted, names);
    }
}