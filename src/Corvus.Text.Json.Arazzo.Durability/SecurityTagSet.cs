// <copyright file="SecurityTagSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A deferred holder over a run's (or catalog version's) <see cref="SecurityTag"/> labels (design §14.2), kept as
/// the persisted JSON array of <c>{ "key", "value" }</c> objects and never materialized into a
/// <see cref="List{T}"/> of <see cref="SecurityTag"/> on the preserve / string-field paths. A checkpoint resume or
/// cancel carries the tags through unchanged (<see cref="WriteTo(Utf8JsonWriter)"/> / <see cref="CopyFrom{T}"/>),
/// and the Redis / Azure Table string-field backends round-trip them as a single JSON value
/// (<see cref="ToJsonStringOrNull"/> / <see cref="FromJsonStringOrEmpty"/>) — none of which needs the managed
/// objects — so a read that only carries or re-serializes the tags pays at most one small owned <see cref="byte"/>
/// array (the persisted bytes), not a list plus a backing array plus N tag strings.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="TagSet"/> / <see cref="SourceSet"/> for the security labels. The canonical
/// internal form is a bare JSON array of <c>{ "key", "value" }</c> objects. <c>default(SecurityTagSet)</c> is the
/// empty set; producers persist <see langword="null"/> (never <c>"[]"</c>) for an empty set, so <see cref="IsEmpty"/>
/// is a cheap span check.
/// </para>
/// <para>
/// Per the design's Q0 decision the evaluator (<see cref="SecurityRule"/> / <see cref="SecurityFilter"/>) keeps
/// consuming <see cref="IReadOnlyList{T}"/>, so the in-process per-row reach filter is fed
/// <see cref="ToList"/> <b>at the leaf</b> and still materializes per row; the realized allocation win is on the
/// preserve / string-field paths only. Backends that keep their own embedded shape (Cosmos / Mongo / NATS
/// <c>{ k, v }</c>, the SQL denormalized column) map at the leaf — <see cref="GetEnumerator"/> drives a write
/// without a list, and <see cref="FromSecurityDelimited"/> / <see cref="ToSecurityDelimitedOrNull"/> round-trip the
/// SQL form. Materializing a managed <see cref="SecurityTag"/> list (<see cref="ToList"/>) or a string is reserved
/// for a genuine leaf — the filter, a persistence driver, the control-plane response, or test/interop code.
/// </para>
/// </remarks>
public readonly struct SecurityTagSet
{
    private const int StackallocByteThreshold = 256;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    // The canonical persisted form: a bare JSON array of { key, value } objects. Empty memory == the empty set.
    private readonly ReadOnlyMemory<byte> json;

    private SecurityTagSet(ReadOnlyMemory<byte> json) => this.json = json;

    /// <summary>Gets the empty set.</summary>
    public static SecurityTagSet Empty => default;

    /// <summary>Gets a value indicating whether the set holds no security tags.</summary>
    public bool IsEmpty => this.json.IsEmpty;

    /// <summary>Gets the persisted JSON-array bytes (empty when the set is empty).</summary>
    public ReadOnlySpan<byte> RawJson => this.json.Span;

    /// <summary>Gets the number of security tags. Walks the persisted bytes; prefer <see cref="IsEmpty"/> for presence checks.</summary>
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
    /// <param name="jsonArray">A JSON array of <c>{ key, value }</c> objects the holder may keep, or empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet FromOwnedJsonArray(ReadOnlyMemory<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new SecurityTagSet(jsonArray);

    /// <summary>Copies a borrowed JSON-array span into one owned <see cref="byte"/> array (the read-escape boundary).</summary>
    /// <param name="jsonArray">A JSON array of <c>{ key, value }</c> objects borrowed from a just-read document, or empty.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet CopyFromJsonArray(ReadOnlySpan<byte> jsonArray)
        => jsonArray.IsEmpty ? default : new SecurityTagSet(jsonArray.ToArray());

    /// <summary>Builds the set from the persisted JSON-array string of a string-field backend (Redis, Azure Table).</summary>
    /// <param name="json">The JSON array text, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet FromJsonStringOrEmpty(string? json)
        => string.IsNullOrEmpty(json) ? default : new SecurityTagSet(Encoding.UTF8.GetBytes(json));

    /// <summary>Builds the set by copying a parsed JSON-array element's canonical UTF-8 into one owned array (the holder must outlive the document the element points into).</summary>
    /// <typeparam name="T">The Corvus.Text.Json element type (a <see cref="JsonElement"/> or a generated array value).</typeparam>
    /// <param name="arrayElement">The security-tags array element from a parsed checkpoint/catalog document; a non-array yields the empty set.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet CopyFrom<T>(in T arrayElement)
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

    /// <summary>Builds the set from the separator-delimited denormalized form of a SQL backend (pairs joined by <paramref name="pairSeparator"/>, key/value split by <paramref name="keyValueSeparator"/>).</summary>
    /// <param name="encoded">The delimited column value, or <see langword="null"/>/empty.</param>
    /// <param name="pairSeparator">The character separating (and bracketing) tag pairs.</param>
    /// <param name="keyValueSeparator">The character separating a key from its value.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet FromSecurityDelimited(string? encoded, char pairSeparator, char keyValueSeparator)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return default;
        }

        ReadOnlySpan<char> remaining = encoded.AsSpan().Trim(pairSeparator);
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
                int nextPair = remaining.IndexOf(pairSeparator);
                ReadOnlySpan<char> pair = nextPair < 0 ? remaining : remaining[..nextPair];
                remaining = nextPair < 0 ? default : remaining[(nextPair + 1)..];

                if (pair.IsEmpty)
                {
                    continue;
                }

                int split = pair.IndexOf(keyValueSeparator);
                if (split < 0)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString(KeyUtf8, pair[..split]);
                writer.WriteString(ValueUtf8, pair[(split + 1)..]);
                writer.WriteEndObject();
                any = true;
            }

            writer.WriteEndArray();
            writer.Flush();
            return any ? new SecurityTagSet(buffer.WrittenSpan.ToArray()) : default;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Builds the set from a list of security tags (the write leaf — run/version creation, HTTP ingest, interop, test).</summary>
    /// <param name="tags">The security tags, or <see langword="null"/>/empty for the empty set.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet FromTags(IReadOnlyList<SecurityTag>? tags)
    {
        if (tags is not { Count: > 0 })
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            foreach (SecurityTag tag in tags)
            {
                writer.WriteStartObject();
                writer.WriteString(KeyUtf8, tag.Key);
                writer.WriteString(ValueUtf8, tag.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();
            return new SecurityTagSet(buffer.WrittenSpan.ToArray());
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Builds the set by writing its tags straight into a pooled JSON buffer through an <see cref="IdentityBuilder"/> — the
    /// <strong>bytes-to-bytes</strong> write leaf for an identity assembled from another UTF-8 source (a directory
    /// response), so no managed <see cref="string"/> is materialized per key or value and no intermediate
    /// <see cref="SecurityTag"/> list is built (cf. <see cref="FromTags"/>, the string-sourced leaf). The only owned
    /// allocation is the one <see cref="byte"/> array the sealed set carries; the writer and buffer are pooled.
    /// </summary>
    /// <typeparam name="TState">The state threaded to <paramref name="build"/> (so it can be a <see langword="static"/> lambda — no capture).</typeparam>
    /// <param name="state">The state passed by reference to <paramref name="build"/>.</param>
    /// <param name="build">Adds the tags via <see cref="IdentityBuilder.Add(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>; adding none yields the empty set.</param>
    /// <returns>The set.</returns>
    public static SecurityTagSet Build<TState>(in TState state, SecurityTagBuildAction<TState> build)
        where TState : allows ref struct
    {
        ArgumentNullException.ThrowIfNull(build);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            var builder = new IdentityBuilder(writer);
            build(ref builder, in state);
            writer.WriteEndArray();
            writer.Flush();
            return builder.Count == 0 ? default : new SecurityTagSet(buffer.WrittenSpan.ToArray());
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Builds the set the same bytes-to-bytes way as <see cref="Build{TState}"/>, but lets the callback drop the record —
    /// returning <see langword="false"/> produces no identity (<paramref name="result"/> is the empty set and the method
    /// returns <see langword="false"/>), so a directory mapper can decline a record it does not recognise without first
    /// materializing anything.
    /// </summary>
    /// <typeparam name="TState">The state threaded to <paramref name="build"/>.</typeparam>
    /// <param name="state">The state passed by reference to <paramref name="build"/>.</param>
    /// <param name="build">Adds the tags and returns whether to keep the record.</param>
    /// <param name="result">The built set when kept; the empty set when dropped.</param>
    /// <returns><see langword="true"/> if the record was kept.</returns>
    public static bool TryBuild<TState>(in TState state, SecurityTagTryBuildAction<TState> build, out SecurityTagSet result)
        where TState : allows ref struct
    {
        ArgumentNullException.ThrowIfNull(build);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, StackallocByteThreshold, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            var builder = new IdentityBuilder(writer);
            if (!build(ref builder, in state))
            {
                result = default;
                return false;
            }

            writer.WriteEndArray();
            writer.Flush();
            result = builder.Count == 0 ? default : new SecurityTagSet(buffer.WrittenSpan.ToArray());
            return true;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Writes the security tags as a JSON array value to <paramref name="writer"/> (empty set writes <c>[]</c>).</summary>
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

    /// <summary>Serializes the set to its separator-delimited denormalized form (SQL backends), or <see langword="null"/> when empty.</summary>
    /// <param name="pairSeparator">The character separating (and bracketing) tag pairs.</param>
    /// <param name="keyValueSeparator">The character separating a key from its value.</param>
    /// <returns>The delimited column value, or <see langword="null"/>.</returns>
    public string? ToSecurityDelimitedOrNull(char pairSeparator, char keyValueSeparator)
    {
        if (this.json.IsEmpty)
        {
            return null;
        }

        // Decode straight into one pooled char buffer (the unescaped key/value chars are never longer than the
        // encoded UTF-8 bytes, and the separators number fewer than the JSON punctuation they replace), then
        // allocate the result string once. No intermediate per-tag strings.
        int capacity = this.json.Length + 2;
        char[] buffer = ArrayPool<char>.Shared.Rent(capacity);
        try
        {
            int position = 0;
            buffer[position++] = pairSeparator;
            var reader = new Utf8JsonReader(this.json.Span);
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (reader.ValueTextEquals(KeyUtf8))
                {
                    reader.Read();
                    position += reader.CopyString(buffer.AsSpan(position));
                    buffer[position++] = keyValueSeparator;
                }
                else if (reader.ValueTextEquals(ValueUtf8))
                {
                    reader.Read();
                    position += reader.CopyString(buffer.AsSpan(position));
                    buffer[position++] = pairSeparator;
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            return new string(buffer, 0, position);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Materializes the security tags to a list. A genuine leaf — the in-process reach filter, persistence drivers, the control-plane response, and test/interop code only.</summary>
    /// <returns>The tag list (empty when the set is empty).</returns>
    public List<SecurityTag> ToList()
    {
        var list = new List<SecurityTag>();
        if (this.json.IsEmpty)
        {
            return list;
        }

        foreach (SecurityTag tag in this)
        {
            list.Add(tag);
        }

        return list;
    }

    /// <summary>
    /// Whether this set contains exactly the same tags as <paramref name="other"/> (order-independent set equality), the
    /// administration/entitlement membership comparison — computed directly on the unescaped UTF-8 key/value bytes, so it
    /// materializes no managed string, no list, and nothing escapes to the heap.
    /// </summary>
    /// <param name="other">The set to compare against.</param>
    /// <returns><see langword="true"/> if the two sets contain exactly the same tags.</returns>
    public bool SetEquals(SecurityTagSet other)
    {
        if (this.json.IsEmpty || other.json.IsEmpty)
        {
            return this.json.IsEmpty && other.json.IsEmpty;
        }

        int count = this.Count;
        if (count != other.Count)
        {
            return false;
        }

        // Parse + sort both sets into pooled scratch + stack slice tables, then compare element-wise on the unescaped
        // UTF-8 spans — set equality without materializing a single tag string.
        byte[] scratchA = ArrayPool<byte>.Shared.Rent(this.json.Length);
        byte[] scratchB = ArrayPool<byte>.Shared.Rent(other.json.Length);
        SecurityTagSpanSort.TagSlice[]? rentedA = count > SecurityTagSpanSort.StackTagCapacity ? ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Rent(count) : null;
        SecurityTagSpanSort.TagSlice[]? rentedB = count > SecurityTagSpanSort.StackTagCapacity ? ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Rent(count) : null;
        try
        {
            scoped Span<SecurityTagSpanSort.TagSlice> tableA;
            scoped Span<SecurityTagSpanSort.TagSlice> tableB;
            if (rentedA is not null)
            {
                tableA = rentedA;
            }
            else
            {
                tableA = stackalloc SecurityTagSpanSort.TagSlice[SecurityTagSpanSort.StackTagCapacity];
            }

            if (rentedB is not null)
            {
                tableB = rentedB;
            }
            else
            {
                tableB = stackalloc SecurityTagSpanSort.TagSlice[SecurityTagSpanSort.StackTagCapacity];
            }

            Span<SecurityTagSpanSort.TagSlice> a = tableA[..count];
            Span<SecurityTagSpanSort.TagSlice> b = tableB[..count];
            SecurityTagSpanSort.Parse(this.json.Span, scratchA, a);
            SecurityTagSpanSort.Sort(a, scratchA);
            SecurityTagSpanSort.Parse(other.json.Span, scratchB, b);
            SecurityTagSpanSort.Sort(b, scratchB);

            for (int i = 0; i < count; i++)
            {
                if (!scratchA.AsSpan(a[i].KeyOffset, a[i].KeyLength).SequenceEqual(scratchB.AsSpan(b[i].KeyOffset, b[i].KeyLength))
                    || !scratchA.AsSpan(a[i].ValueOffset, a[i].ValueLength).SequenceEqual(scratchB.AsSpan(b[i].ValueOffset, b[i].ValueLength)))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratchA);
            ArrayPool<byte>.Shared.Return(scratchB);
            if (rentedA is not null)
            {
                ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Return(rentedA);
            }

            if (rentedB is not null)
            {
                ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Return(rentedB);
            }
        }
    }

    /// <summary>Gets an allocation-free enumerator over the security tags (each <see cref="SecurityTag.Key"/>/<see cref="SecurityTag.Value"/> is decoded once at the driver leaf). Lets a <c>{ k, v }</c> / BSON / SQL backend write its embedded form without a <see cref="List{T}"/>.</summary>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(this.json.Span);

    // The bare-array property names, matching the checkpoint and the generated CatalogVersion's SecurityTagInfo.
    private static ReadOnlySpan<byte> KeyUtf8 => "key"u8;

    private static ReadOnlySpan<byte> ValueUtf8 => "value"u8;

    /// <summary>A forward-only, allocation-free enumerator over a <see cref="SecurityTagSet"/>'s tags.</summary>
    public ref struct Enumerator
    {
        private readonly bool empty;
        private Utf8JsonReader reader;

        internal Enumerator(ReadOnlySpan<byte> json)
        {
            this.empty = json.IsEmpty;
            this.reader = this.empty ? default : new Utf8JsonReader(json);
            this.Current = default;
        }

        /// <summary>Gets the current security tag.</summary>
        public SecurityTag Current { get; private set; }

        /// <summary>Advances to the next security tag.</summary>
        /// <returns><see langword="true"/> if there was another tag.</returns>
        public bool MoveNext()
        {
            if (this.empty)
            {
                return false;
            }

            string? key = null;
            string? value = null;
            while (this.reader.Read())
            {
                switch (this.reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        key = null;
                        value = null;
                        break;

                    case JsonTokenType.PropertyName:
                        if (this.reader.ValueTextEquals(KeyUtf8))
                        {
                            this.reader.Read();
                            key = this.reader.GetString();
                        }
                        else if (this.reader.ValueTextEquals(ValueUtf8))
                        {
                            this.reader.Read();
                            value = this.reader.GetString();
                        }
                        else
                        {
                            this.reader.Read();
                            this.reader.Skip();
                        }

                        break;

                    case JsonTokenType.EndObject:
                        this.Current = new SecurityTag(key ?? string.Empty, value ?? string.Empty);
                        return true;
                }
            }

            return false;
        }
    }
}