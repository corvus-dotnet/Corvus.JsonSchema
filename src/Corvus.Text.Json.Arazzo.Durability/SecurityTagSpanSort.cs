// <copyright file="SecurityTagSpanSort.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Shared low-allocation parse-and-sort of a <see cref="SecurityTagSet"/>'s persisted tags into order-independent
/// canonical slices — the common core behind content-determined keys and digests (an identity digest, a binding
/// discriminator) that must be built without materialising a <see cref="List{T}"/> or per-tag strings.
/// </summary>
/// <remarks>
/// The caller owns the buffer lifetimes: it rents the scratch byte buffer (sized to the source JSON, since unescaping
/// never grows the byte count) and the slice table (a <c>stackalloc</c> of <see cref="StackTagCapacity"/>, with a pooled
/// fallback only for a pathological many-tag set), calls <see cref="Parse"/> then <see cref="Sort"/>, and then assembles
/// its own result from the sorted slices over the scratch bytes — so only the final result (a hash string, a delimited
/// key) escapes to the heap. Any consistent total order makes the canonical form order-independent.
/// </remarks>
internal static class SecurityTagSpanSort
{
    /// <summary>Tag sets carry a handful of tags; only a pathological set spills the slice table from the stack to the pool.</summary>
    internal const int StackTagCapacity = 32;

    /// <summary>
    /// Copies each tag's unescaped key and value into <paramref name="scratch"/>, filling <paramref name="slices"/>
    /// (whose length is the tag count), and returns the total bytes written. The persisted form is an array of
    /// <c>{ "key", "value" }</c> objects; either property order is tolerated.
    /// </summary>
    /// <param name="json">The tag set's persisted JSON-array bytes.</param>
    /// <param name="scratch">A scratch buffer at least as long as <paramref name="json"/>.</param>
    /// <param name="slices">A slice table whose length equals the tag count.</param>
    /// <returns>The number of bytes written to <paramref name="scratch"/>.</returns>
    internal static int Parse(ReadOnlySpan<byte> json, Span<byte> scratch, Span<TagSlice> slices)
    {
        int written = 0;
        int count = 0;
        var reader = new Utf8JsonReader(json);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                continue;
            }

            int keyOffset = 0, keyLength = 0, valueOffset = 0, valueLength = 0;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isKey = reader.ValueTextEquals(KeyUtf8);
                reader.Read();
                int n = reader.CopyString(scratch[written..]);
                if (isKey)
                {
                    keyOffset = written;
                    keyLength = n;
                }
                else
                {
                    valueOffset = written;
                    valueLength = n;
                }

                written += n;
            }

            slices[count++] = new TagSlice(keyOffset, keyLength, valueOffset, valueLength);
        }

        return written;
    }

    /// <summary>
    /// Insertion-sorts the slices by (key, value) ordinal byte comparison over <paramref name="scratch"/>. Insertion sort
    /// suits the small tag counts and needs no comparison delegate (no closure allocation).
    /// </summary>
    /// <param name="slices">The slices to sort in place.</param>
    /// <param name="scratch">The scratch buffer the slices index into.</param>
    internal static void Sort(Span<TagSlice> slices, ReadOnlySpan<byte> scratch)
    {
        for (int i = 1; i < slices.Length; i++)
        {
            TagSlice current = slices[i];
            int j = i - 1;
            while (j >= 0 && Compare(slices[j], current, scratch) > 0)
            {
                slices[j + 1] = slices[j];
                j--;
            }

            slices[j + 1] = current;
        }
    }

    private static int Compare(in TagSlice a, in TagSlice b, ReadOnlySpan<byte> scratch)
    {
        int byKey = scratch.Slice(a.KeyOffset, a.KeyLength).SequenceCompareTo(scratch.Slice(b.KeyOffset, b.KeyLength));
        return byKey != 0
            ? byKey
            : scratch.Slice(a.ValueOffset, a.ValueLength).SequenceCompareTo(scratch.Slice(b.ValueOffset, b.ValueLength));
    }

    private static ReadOnlySpan<byte> KeyUtf8 => "key"u8;

    /// <summary>An unescaped tag's key and value as offsets/lengths into a shared scratch buffer.</summary>
    /// <param name="KeyOffset">The key's start offset in the scratch buffer.</param>
    /// <param name="KeyLength">The key's length in bytes.</param>
    /// <param name="ValueOffset">The value's start offset in the scratch buffer.</param>
    /// <param name="ValueLength">The value's length in bytes.</param>
    internal readonly record struct TagSlice(int KeyOffset, int KeyLength, int ValueOffset, int ValueLength);
}