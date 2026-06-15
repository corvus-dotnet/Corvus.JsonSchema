// <copyright file="TagSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A deferred holder over a run's (or catalog version's) free-form tags, kept as the persisted JSON array of
/// strings (e.g. <c>["nightly","eu"]</c>) and never materialized into a <see cref="List{T}"/> of strings on the
/// read/filter/output paths. The only operations the durability layer performs on tags are membership-filtering
/// and re-serialization, both of which run directly over the persisted UTF-8 via <see cref="Utf8JsonReader"/> /
/// <see cref="Utf8JsonWriter.WriteRawValue(ReadOnlySpan{byte}, bool)"/> — so a read that never inspects tags
/// pays at most one small owned <see cref="byte"/> array (the persisted bytes), not a list plus a backing array
/// plus N tag strings.
/// </summary>
/// <remarks>
/// <para>
/// <c>default(TagSet)</c> is the empty set. Producers persist <see langword="null"/> (never <c>"[]"</c>) for an
/// empty set, and every constructor here returns <c>default</c> for empty input, so <see cref="IsEmpty"/> is a
/// cheap span check rather than a parse.
/// </para>
/// <para>
/// The canonical internal form is a bare JSON array of strings. JSON-native backends (Cosmos, NATS) embed it
/// with <see cref="WriteTo(Utf8JsonWriter)"/>; the string-field backends (Redis, Azure Table) round-trip it
/// with <see cref="ToJsonStringOrNull"/> / <see cref="FromJsonStringOrEmpty"/>; the SQL backends round-trip the
/// separator-delimited decoded form with <see cref="ToDelimitedOrNull"/> / <see cref="FromDelimited"/>.
/// Materializing a managed <see cref="string"/> (<see cref="ToList"/> / <see cref="FromTags"/>) is reserved for
/// a genuine leaf — a persistence driver that requires a string, or test/interop code.
/// </para>
/// </remarks>
public readonly struct TagSet
{
    private const int StackallocByteThreshold = 256;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    // The canonical persisted form: a bare JSON array of strings. Empty memory == the empty set.
    private readonly ReadOnlyMemory<byte> json;

    private TagSet(ReadOnlyMemory<byte> json) => this.json = json;

    /// <summary>Gets the empty set.</summary>
    public static TagSet Empty => default;

    /// <summary>Gets a value indicating whether the set holds no tags.</summary>
    public bool IsEmpty => this.json.IsEmpty;

    /// <summary>Gets the persisted JSON-array bytes (empty when the set is empty). Backends that embed tags inline persist these directly.</summary>
    public ReadOnlySpan<byte> RawJson => this.json.Span;

    /// <summary>Gets the number of tags. Walks the persisted bytes; prefer <see cref="IsEmpty"/> for presence checks.</summary>
    public int Count
    {
        get
        {
            if (this.json.IsEmpty)
            {
                return 0;
            }

            int count = 0;
            var reader = new Utf8JsonReader(this.json.Span);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Wraps already-owned JSON-array bytes without copying. The caller transfers ownership.</summary>
    /// <param name="jsonArray">A JSON array of strings the holder may keep, or empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static TagSet FromOwnedJsonArray(ReadOnlyMemory<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new TagSet(jsonArray);

    /// <summary>Copies a borrowed JSON-array span into one owned <see cref="byte"/> array (the read-escape boundary).</summary>
    /// <param name="jsonArray">A JSON array of strings borrowed from a just-read document, or empty.</param>
    /// <returns>The set.</returns>
    public static TagSet CopyFromJsonArray(ReadOnlySpan<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new TagSet(jsonArray.ToArray());

    /// <summary>Builds the set from the persisted JSON-array string of a string-field backend (Redis, Azure Table).</summary>
    /// <param name="json">The JSON array text, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static TagSet FromJsonStringOrEmpty(string? json)
        => string.IsNullOrEmpty(json) ? default : new TagSet(Encoding.UTF8.GetBytes(json));

    /// <summary>Builds the set by copying a parsed JSON-array element's canonical UTF-8 into one owned array (the holder must outlive the document the element points into).</summary>
    /// <typeparam name="T">The Corvus.Text.Json element type (a <see cref="JsonElement"/> or a generated array value).</typeparam>
    /// <param name="arrayElement">The tags array element from a parsed checkpoint/run document; a non-array yields the empty set.</param>
    /// <returns>The set.</returns>
    public static TagSet CopyFrom<T>(in T arrayElement)
        where T : struct, IJsonElement<T>
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
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

    /// <summary>Builds the set from the separator-delimited decoded form of a SQL backend.</summary>
    /// <param name="encoded">The delimited column value (separator-bracketed decoded tags), or <see langword="null"/>/empty.</param>
    /// <param name="separator">The delimiter character.</param>
    /// <returns>The set.</returns>
    public static TagSet FromDelimited(string? encoded, char separator)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return default;
        }

        ReadOnlySpan<char> remaining = encoded.AsSpan().Trim(separator);
        if (remaining.IsEmpty)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            bool any = false;
            while (!remaining.IsEmpty)
            {
                int next = remaining.IndexOf(separator);
                ReadOnlySpan<char> segment = next < 0 ? remaining : remaining[..next];
                if (!segment.IsEmpty)
                {
                    writer.WriteStringValue(segment);
                    any = true;
                }

                remaining = next < 0 ? default : remaining[(next + 1)..];
            }

            writer.WriteEndArray();
            writer.Flush();
            return any ? new TagSet(buffer.WrittenSpan.ToArray()) : default;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Builds the set from a sequence of decoded tag strings (HTTP ingest / interop / test leaf).</summary>
    /// <param name="tags">The tags, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static TagSet FromTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            bool any = false;
            foreach (string tag in tags)
            {
                writer.WriteStringValue(tag);
                any = true;
            }

            writer.WriteEndArray();
            writer.Flush();
            return any ? new TagSet(buffer.WrittenSpan.ToArray()) : default;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Determines whether the set contains a tag equal to the given decoded UTF-8 value.</summary>
    /// <param name="wantedUtf8">The decoded UTF-8 tag value to find.</param>
    /// <returns><see langword="true"/> if present.</returns>
    public bool Contains(ReadOnlySpan<byte> wantedUtf8)
    {
        if (this.json.IsEmpty)
        {
            return false;
        }

        var reader = new Utf8JsonReader(this.json.Span);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(wantedUtf8))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the set contains a tag equal to the given value.</summary>
    /// <param name="wanted">The tag value to find.</param>
    /// <returns><see langword="true"/> if present.</returns>
    public bool Contains(string? wanted)
    {
        if (this.json.IsEmpty || string.IsNullOrEmpty(wanted))
        {
            return false;
        }

        int max = Encoding.UTF8.GetMaxByteCount(wanted.Length);
        byte[]? rented = max > StackallocByteThreshold ? ArrayPool<byte>.Shared.Rent(max) : null;
        try
        {
            Span<byte> buffer = rented ?? stackalloc byte[StackallocByteThreshold];
            int written = Encoding.UTF8.GetBytes(wanted, buffer);
            return this.Contains(buffer[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Determines whether every tag in this (needle) set is present in <paramref name="haystack"/> (the row's tags). The in-memory tag-filter primitive (AND semantics): an empty needle matches anything.</summary>
    /// <param name="haystack">The row's tag set to test against.</param>
    /// <returns><see langword="true"/> if every tag in this set is contained in <paramref name="haystack"/>.</returns>
    public bool AllContainedIn(in TagSet haystack)
    {
        if (this.json.IsEmpty)
        {
            return true;
        }

        if (haystack.json.IsEmpty)
        {
            return false;
        }

        Span<byte> scratch = stackalloc byte[StackallocByteThreshold];
        var reader = new Utf8JsonReader(this.json.Span);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                continue;
            }

            // Compare decoded value against decoded value: an unescaped token's ValueSpan is already decoded;
            // an escaped token is decoded once into scratch (pooled if it overflows the stack buffer).
            bool found;
            if (!reader.ValueIsEscaped)
            {
                found = haystack.Contains(reader.ValueSpan);
            }
            else
            {
                int length = reader.ValueSpan.Length;
                if (length > StackallocByteThreshold)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        int written = reader.CopyString(rented);
                        found = haystack.Contains(rented.AsSpan(0, written));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
                else
                {
                    int written = reader.CopyString(scratch);
                    found = haystack.Contains(scratch[..written]);
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes the tags as a JSON array value to <paramref name="writer"/> (empty set writes <c>[]</c>).</summary>
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

    /// <summary>Serializes the set to its persisted JSON-array string (string-field backends), or <see langword="null"/> when empty.</summary>
    /// <returns>The JSON array text, or <see langword="null"/>.</returns>
    public string? ToJsonStringOrNull()
        => this.json.IsEmpty ? null : Encoding.UTF8.GetString(this.json.Span);

    /// <summary>Serializes the set to its separator-delimited decoded form (SQL backends), or <see langword="null"/> when empty.</summary>
    /// <param name="separator">The delimiter character (bracketing the value so an exact-match LIKE works).</param>
    /// <returns>The delimited column value, or <see langword="null"/>.</returns>
    public string? ToDelimitedOrNull(char separator)
    {
        if (this.json.IsEmpty)
        {
            return null;
        }

        // Decode straight into one pooled char buffer (the unescaped form is never longer than the encoded UTF-8
        // bytes, and the separators number one more than the tags), then allocate the result string once.
        int capacity = (this.json.Length * 2) + 2;
        char[] buffer = ArrayPool<char>.Shared.Rent(capacity);
        try
        {
            int position = 0;
            buffer[position++] = separator;
            var reader = new Utf8JsonReader(this.json.Span);
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    continue;
                }

                position += reader.CopyString(buffer.AsSpan(position));
                buffer[position++] = separator;
            }

            return new string(buffer, 0, position);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Materializes the tags to a list of strings. A genuine leaf — interop and test code only; never the read/filter/output paths.</summary>
    /// <returns>The tag list (empty when the set is empty).</returns>
    public List<string> ToList()
    {
        var list = new List<string>();
        if (this.json.IsEmpty)
        {
            return list;
        }

        var reader = new Utf8JsonReader(this.json.Span);
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