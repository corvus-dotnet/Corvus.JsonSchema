// <copyright file="SourceSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A deferred holder over a catalog version's package source references, kept as the persisted JSON array of
/// <c>{ "name", "type"? }</c> objects and never materialized into a <see cref="List{T}"/> of
/// <see cref="CatalogSourceRef"/> on the read or carry-through paths. Sources are only ever read, re-serialized,
/// or carried unchanged across a metadata update — none of which needs the managed objects — so a read that does
/// not enumerate sources pays at most one small owned <see cref="byte"/> array (the persisted bytes).
/// </summary>
/// <remarks>
/// The counterpart of <see cref="TagSet"/> for the catalog's source list. It has no membership operation (sources
/// are never filtered). <c>default(SourceSet)</c> is the empty set; producers persist <see langword="null"/> (never
/// <c>"[]"</c>) for an empty set, so <see cref="IsEmpty"/> is a cheap span check. Materializing the managed
/// <see cref="CatalogSourceRef"/> list (<see cref="ToList"/>) is reserved for a genuine leaf — a driver that
/// requires the objects, or the control-plane response.
/// </remarks>
public readonly struct SourceSet
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    // The canonical persisted form: a JSON array of { name, type? } objects. Empty memory == the empty set.
    private readonly ReadOnlyMemory<byte> json;

    private SourceSet(ReadOnlyMemory<byte> json) => this.json = json;

    /// <summary>Gets the empty set.</summary>
    public static SourceSet Empty => default;

    /// <summary>Gets a value indicating whether the set holds no sources.</summary>
    public bool IsEmpty => this.json.IsEmpty;

    /// <summary>Gets the persisted JSON-array bytes (empty when the set is empty).</summary>
    public ReadOnlySpan<byte> RawJson => this.json.Span;

    /// <summary>Gets the number of sources. Walks the persisted bytes; prefer <see cref="IsEmpty"/> for presence checks.</summary>
    public int Count
    {
        get
        {
            if (this.json.IsEmpty)
            {
                return 0;
            }

            int depth = 0;
            int count = 0;
            var reader = new Utf8JsonReader(this.json.Span);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    if (depth == 0)
                    {
                        count++;
                    }

                    depth++;
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    depth--;
                }
            }

            return count;
        }
    }

    /// <summary>Wraps already-owned JSON-array bytes without copying. The caller transfers ownership.</summary>
    /// <param name="jsonArray">A JSON array of source objects the holder may keep, or empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SourceSet FromOwnedJsonArray(ReadOnlyMemory<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new SourceSet(jsonArray);

    /// <summary>Copies a borrowed JSON-array span into one owned <see cref="byte"/> array (the read-escape boundary).</summary>
    /// <param name="jsonArray">A JSON array of source objects borrowed from a just-read document, or empty.</param>
    /// <returns>The set.</returns>
    public static SourceSet CopyFromJsonArray(ReadOnlySpan<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new SourceSet(jsonArray.ToArray());

    /// <summary>Builds the set from the persisted JSON-array string of a column backend (SQL, Azure Table).</summary>
    /// <param name="json">The JSON array text, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SourceSet FromJsonStringOrEmpty(string? json)
        => string.IsNullOrEmpty(json) ? default : new SourceSet(Encoding.UTF8.GetBytes(json));

    /// <summary>Builds the set by copying a parsed JSON-array element's canonical UTF-8 into one owned array (the holder must outlive the document the element points into).</summary>
    /// <typeparam name="T">The Corvus.Text.Json element type (a <see cref="JsonElement"/> or a generated array value).</typeparam>
    /// <param name="arrayElement">The sources array element from a parsed catalog document; a non-array yields the empty set.</param>
    /// <returns>The set.</returns>
    public static SourceSet CopyFrom<T>(in T arrayElement)
        where T : struct, IJsonElement<T>
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            arrayElement.WriteTo(writer);
            writer.Flush();
            return CopyFromJsonArray(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Builds the set from a sequence of source references (the write leaf — package projection / interop / test).</summary>
    /// <param name="sources">The source references, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SourceSet FromSources(IEnumerable<CatalogSourceRef>? sources)
    {
        if (sources is null)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            bool any = false;
            foreach (CatalogSourceRef source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString("name"u8, source.Name);
                if (source.Type is { } type)
                {
                    writer.WriteString("type"u8, type);
                }

                writer.WriteEndObject();
                any = true;
            }

            writer.WriteEndArray();
            writer.Flush();
            return any ? new SourceSet(buffer.WrittenSpan.ToArray()) : default;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Writes the sources as a JSON array value to <paramref name="writer"/> (empty set writes <c>[]</c>).</summary>
    /// <param name="writer">The writer.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if (this.json.IsEmpty)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
            return;
        }

        writer.WriteRawValue(this.json.Span, skipInputValidation: true);
    }

    /// <summary>Serializes the set to its persisted JSON-array string (column backends), or <see langword="null"/> when empty.</summary>
    /// <returns>The JSON array text, or <see langword="null"/>.</returns>
    public string? ToJsonStringOrNull()
        => this.json.IsEmpty ? null : Encoding.UTF8.GetString(this.json.Span);

    /// <summary>Materializes the sources to a list of <see cref="CatalogSourceRef"/>. A genuine leaf — driver, response, and test code only.</summary>
    /// <returns>The source list (empty when the set is empty).</returns>
    public List<CatalogSourceRef> ToList()
    {
        var list = new List<CatalogSourceRef>();
        if (this.json.IsEmpty)
        {
            return list;
        }

        var reader = new Utf8JsonReader(this.json.Span);
        string name = string.Empty;
        string? type = null;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    name = string.Empty;
                    type = null;
                    break;
                case JsonTokenType.PropertyName:
                    if (reader.ValueTextEquals("name"u8))
                    {
                        reader.Read();
                        name = reader.GetString() ?? string.Empty;
                    }
                    else if (reader.ValueTextEquals("type"u8))
                    {
                        reader.Read();
                        type = reader.GetString();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }

                    break;
                case JsonTokenType.EndObject:
                    list.Add(new CatalogSourceRef(name, type));
                    break;
            }
        }

        return list;
    }
}