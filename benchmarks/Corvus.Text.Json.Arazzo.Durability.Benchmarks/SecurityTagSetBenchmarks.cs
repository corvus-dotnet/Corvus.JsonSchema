// <copyright file="SecurityTagSetBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the deferred <see cref="SecurityTagSet"/> holder against the <see cref="List{T}"/>-of-<see cref="SecurityTag"/>
/// shape it replaces, op by op. The baselines are the code each op displaced: enumerating a parsed element into a list
/// (the former checkpoint/catalog read), serializing a list to the string field, and the StringBuilder/Split codec the
/// SQL catalog used for its denormalized column. The realized win is the preserve / string-field paths; the in-process
/// filter still materializes (<see cref="SecurityTagSet.ToList"/>), measured here for completeness.
/// </summary>
public class SecurityTagSetBenchmarks
{
    private const char PairSeparator = (char)0x1F;
    private const char KeyValueSeparator = (char)0x1E;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private SecurityTag[] tags = null!;
    private SecurityTagSet holder;
    private string jsonString = null!;
    private string delimited = null!;
    private ParsedJsonDocument<JsonElement> document = null!;
    private JsonElement tagsElement;

    [GlobalSetup]
    public void Setup()
    {
        this.tags = [new("tenant", "acme"), new("team", "payments")];
        this.holder = SecurityTagSet.FromTags(this.tags);
        this.jsonString = this.holder.ToJsonStringOrNull()!;
        this.delimited = this.holder.ToSecurityDelimitedOrNull(PairSeparator, KeyValueSeparator)!;
        this.document = ParsedJsonDocument<JsonElement>.Parse(("{\"securityTags\":" + this.jsonString + "}").AsMemory());
        this.tagsElement = this.document.RootElement.GetProperty("securityTags"u8);
    }

    [GlobalCleanup]
    public void Cleanup() => this.document.Dispose();

    // ---- read leaf: checkpoint resume / catalog read (the primary win path) ----

    /// <summary>Old: enumerate the parsed security-tags element into a list of <see cref="SecurityTag"/>.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark(Baseline = true)]
    public int Old_Element_Enumerate()
    {
        var list = new List<SecurityTag>();
        foreach (JsonElement tag in this.tagsElement.EnumerateArray())
        {
            if (tag.TryGetProperty("key"u8, out JsonElement k) && k.GetString() is { } key
                && tag.TryGetProperty("value"u8, out JsonElement v) && v.GetString() is { } value)
            {
                list.Add(new SecurityTag(key, value));
            }
        }

        return list.Count;
    }

    /// <summary>New: copy the parsed element's canonical bytes into the holder (one owned array).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_CopyFrom() => SecurityTagSet.CopyFrom(this.tagsElement).Count;

    // ---- write leaf: checkpoint / catalog carry ----

    /// <summary>Old: write the security-tags array from a list of <see cref="SecurityTag"/>.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int Old_List_WriteTo()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            foreach (SecurityTag tag in this.tags)
            {
                writer.WriteStartObject();
                writer.WriteString("key"u8, tag.Key);
                writer.WriteString("value"u8, tag.Value);
                writer.WriteEndObject();
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

    /// <summary>New: write the holder's bytes verbatim (no per-tag strings).</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int New_WriteTo()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            this.holder.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // ---- string-field leaf: Redis hash field / Azure Table property ----

    /// <summary>New: build the holder from the persisted string field (one owned array).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_FromJsonString() => SecurityTagSet.FromJsonStringOrEmpty(this.jsonString).Count;

    /// <summary>New: serialize the holder to its persisted string field.</summary>
    /// <returns>The JSON string.</returns>
    [Benchmark]
    public string? New_ToJsonString() => this.holder.ToJsonStringOrNull();

    // ---- SQL denormalized column: pair-delimited codec ----

    /// <summary>Old: encode a list to the denormalized column with a StringBuilder.</summary>
    /// <returns>The encoded string.</returns>
    [Benchmark]
    public string Old_StringBuilder_Encode()
    {
        var builder = new StringBuilder();
        builder.Append(PairSeparator);
        foreach (SecurityTag tag in this.tags)
        {
            builder.Append(tag.Key).Append(KeyValueSeparator).Append(tag.Value).Append(PairSeparator);
        }

        return builder.ToString();
    }

    /// <summary>New: encode the holder to the denormalized column from a pooled char buffer.</summary>
    /// <returns>The encoded string.</returns>
    [Benchmark]
    public string? New_ToSecurityDelimited() => this.holder.ToSecurityDelimitedOrNull(PairSeparator, KeyValueSeparator);

    /// <summary>Old: decode the denormalized column into a list via String.Split.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Old_Split_Decode()
    {
        string[] parts = this.delimited.Trim(PairSeparator).Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<SecurityTag>(parts.Length);
        foreach (string part in parts)
        {
            int split = part.IndexOf(KeyValueSeparator);
            if (split >= 0)
            {
                list.Add(new SecurityTag(part[..split], part[(split + 1)..]));
            }
        }

        return list.Count;
    }

    /// <summary>New: decode the denormalized column into the holder (one owned array).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_FromSecurityDelimited() => SecurityTagSet.FromSecurityDelimited(this.delimited, PairSeparator, KeyValueSeparator).Count;

    // ---- filter leaf (retained per design Q0) ----

    /// <summary>New: materialize the holder to a list for the in-process reach filter.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int New_ToList() => this.holder.ToList().Count;
}