// <copyright file="TagSetBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the deferred <see cref="TagSet"/> holder against the <see cref="List{T}"/>-of-strings shape it replaces,
/// op by op. The baseline for the read/filter paths is "materialize a <see cref="List{T}"/> from the persisted JSON
/// then operate on it" — exactly what the durability layer did on every indexed read. The holder operates over the
/// persisted UTF-8 directly: membership and subset checks allocate nothing, a read copies one small owned array, and
/// re-serialization is a raw byte copy.
/// </summary>
public class TagSetBenchmarks
{
    private const char Sep = (char)0x1F;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private byte[] tagsJson = null!;
    private byte[] needleUtf8 = null!;
    private string needle = null!;
    private string[] sourceTags = null!;
    private string delimited = null!;
    private string jsonString = null!;
    private TagSet haystack;
    private TagSet needleSet;
    private List<string> tagList = null!;
    private ParsedJsonDocument<JsonElement> document;
    private JsonElement tagsElement;

    [GlobalSetup]
    public void Setup()
    {
        this.tagsJson = "[\"nightly\",\"eu\",\"priority\",\"release-2026\"]"u8.ToArray();
        this.needle = "priority";
        this.needleUtf8 = "priority"u8.ToArray();
        this.sourceTags = ["nightly", "eu", "priority", "release-2026"];
        this.delimited = Sep + string.Join(Sep, this.sourceTags) + Sep;
        this.jsonString = System.Text.Encoding.UTF8.GetString(this.tagsJson);
        this.haystack = TagSet.FromOwnedJsonArray(this.tagsJson);
        this.needleSet = TagSet.FromTags(["priority"]);
        this.tagList = MaterializeList(this.tagsJson);
        this.document = ParsedJsonDocument<JsonElement>.Parse(("{\"tags\":" + this.jsonString + "}").AsMemory());
        this.tagsElement = this.document.RootElement.GetProperty("tags"u8);
    }

    [GlobalCleanup]
    public void Cleanup() => this.document.Dispose();

    // ---- membership filter (the proven case) ----

    /// <summary>Old: materialize a List from the persisted JSON, then Contains.</summary>
    /// <returns>Whether the tag is present.</returns>
    [Benchmark(Baseline = true)]
    public bool Old_ListMaterialize_Contains() => MaterializeList(this.tagsJson).Contains(this.needle);

    /// <summary>New: membership directly over the persisted bytes.</summary>
    /// <returns>Whether the tag is present.</returns>
    [Benchmark]
    public bool New_TagSet_Contains() => this.haystack.Contains(this.needleUtf8);

    /// <summary>New: AND-semantics subset filter (needle ⊆ row) directly over the bytes — the in-memory backend filter.</summary>
    /// <returns>Whether every needle tag is present.</returns>
    [Benchmark]
    public bool New_TagSet_AllContainedIn() => this.needleSet.AllContainedIn(this.haystack);

    // ---- read materialization ----

    /// <summary>Old: every indexed read materialized the tags into a List of strings.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Old_ListMaterialize_Read() => MaterializeList(this.tagsJson).Count;

    /// <summary>New: a read copies the persisted JSON into one small owned array.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_TagSet_CopyFromJsonArray() => TagSet.CopyFromJsonArray(this.tagsJson).Count;

    // ---- re-serialization (the output path) ----

    /// <summary>Old: re-serialize tags by iterating the materialized strings into the array writer.</summary>
    /// <returns>The written length.</returns>
    [Benchmark]
    public int Old_WriteTags_FromList()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            foreach (string tag in this.tagList)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>New: re-serialize tags as a raw byte copy of the persisted array.</summary>
    /// <returns>The written length.</returns>
    [Benchmark]
    public int New_WriteTags_Raw()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            this.haystack.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // ---- write-leaf construction (HTTP ingest) ----

    /// <summary>New: build the holder from ingest strings — one owned array, the write boundary.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_TagSet_FromTags() => TagSet.FromTags(this.sourceTags).Count;

    // ---- SQL backends: separator-delimited round-trip ----

    /// <summary>Old: decode the delimited column into a string array (the former DecodeTags).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Old_Delimited_Decode() => this.delimited.Trim(Sep).Split(Sep, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>New: build the holder from the delimited column (one owned array).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_TagSet_FromDelimited() => TagSet.FromDelimited(this.delimited, Sep).Count;

    /// <summary>Old: encode a tag list to the delimited column (the former EncodeTags).</summary>
    /// <returns>The delimited value.</returns>
    [Benchmark]
    public string Old_Delimited_Encode() => Sep + string.Join(Sep, this.tagList) + Sep;

    /// <summary>New: serialize the holder to the delimited column.</summary>
    /// <returns>The delimited value.</returns>
    [Benchmark]
    public string? New_TagSet_ToDelimited() => this.haystack.ToDelimitedOrNull(Sep);

    // ---- string-field backends (Redis, Azure Table): the dropped STJ round-trip ----

    /// <summary>Old: deserialize the JSON-array string field with System.Text.Json.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Old_Stj_Deserialize() => System.Text.Json.JsonSerializer.Deserialize<List<string>>(this.jsonString)!.Count;

    /// <summary>New: build the holder from the JSON-array string field (one owned array, no STJ).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_TagSet_FromJsonString() => TagSet.FromJsonStringOrEmpty(this.jsonString).Count;

    /// <summary>Old: serialize a tag list to the JSON-array string field with System.Text.Json.</summary>
    /// <returns>The JSON array string.</returns>
    [Benchmark]
    public string Old_Stj_Serialize() => System.Text.Json.JsonSerializer.Serialize(this.tagList);

    /// <summary>New: serialize the holder to the JSON-array string field (no STJ).</summary>
    /// <returns>The JSON array string.</returns>
    [Benchmark]
    public string? New_TagSet_ToJsonString() => this.haystack.ToJsonStringOrNull();

    // ---- document-embedded backends (NATS, Cosmos, checkpoint): read from a parsed element ----

    /// <summary>Old: enumerate the parsed tags element into a string list.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Old_Element_Enumerate()
    {
        var list = new List<string>();
        foreach (JsonElement tag in this.tagsElement.EnumerateArray())
        {
            list.Add(tag.GetString()!);
        }

        return list.Count;
    }

    /// <summary>New: copy the parsed tags element's canonical bytes into the holder (one owned array).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_TagSet_CopyFrom() => TagSet.CopyFrom(this.tagsElement).Count;

    private static List<string> MaterializeList(byte[] json)
    {
        var list = new List<string>();
        var reader = new Utf8JsonReader(json);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(reader.GetString()!);
            }
        }

        return list;
    }
}