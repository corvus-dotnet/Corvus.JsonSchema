// <copyright file="SecurityTagSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The persisted JSON form of a row's <see cref="SecurityTag"/> set (design §14.2), generated from
/// <c>Schemas/SecurityTagSet.json</c>. Backends that keep the tags as a single JSON value (a Redis hash field, an
/// Azure Table property) round-trip them through this Corvus.Text.Json schema type — never a reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/SecurityTagSet.json")]
public readonly partial struct SecurityTagSet
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Builds the set from a list of tags, detached and ready to persist.</summary>
    /// <param name="tags">The security tags.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet From(IReadOnlyList<SecurityTag> tags)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteStartArray(JsonPropertyNames.SecurityTagsUtf8);
            foreach (SecurityTag tag in tags)
            {
                writer.WriteStartObject();
                writer.WriteString(SecurityTagEntry.JsonPropertyNames.KeyUtf8, tag.Key);
                writer.WriteString(SecurityTagEntry.JsonPropertyNames.ValueUtf8, tag.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<SecurityTagSet> doc = ParsedJsonDocument<SecurityTagSet>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes a tag list to its persisted JSON string (or <see langword="null"/> when empty).</summary>
    /// <param name="tags">The security tags.</param>
    /// <returns>The JSON string, or <see langword="null"/> if <paramref name="tags"/> is null/empty.</returns>
    public static string? ToJsonStringOrNull(IReadOnlyList<SecurityTag>? tags)
        => tags is { Count: > 0 } ? From(tags).ToJsonString() : null;

    /// <summary>Parses a tag list from its persisted JSON string (or <see langword="null"/>/empty).</summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The tag list, or <see langword="null"/> if absent/empty.</returns>
    public static IReadOnlyList<SecurityTag>? FromJsonStringOrNull(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using ParsedJsonDocument<SecurityTagSet> doc = ParsedJsonDocument<SecurityTagSet>.Parse(json);
        IReadOnlyList<SecurityTag> list = doc.RootElement.ToList();
        return list.Count > 0 ? list : null;
    }

    /// <summary>Serializes this set to its persisted JSON string.</summary>
    /// <returns>The JSON string.</returns>
    public string ToJsonString()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Projects this set back to a list of <see cref="SecurityTag"/>.</summary>
    /// <returns>The tag list.</returns>
    public IReadOnlyList<SecurityTag> ToList()
    {
        var list = new List<SecurityTag>();
        if (this.SecurityTags.IsNotUndefined())
        {
            foreach (SecurityTagEntry entry in this.SecurityTags.EnumerateArray())
            {
                list.Add(new SecurityTag((string)entry.Key, (string)entry.Value));
            }
        }

        return list;
    }
}