// <copyright file="SourceCredentialKey.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The uniqueness key for a source credential binding, shared by every <see cref="ISourceCredentialStore"/> backend so
/// they agree on identity. A binding is identified by (sourceName, environment) plus a <em>discriminator</em> over its
/// immutable management and usage tag sets, so two bindings for the same source/environment that differ in either scope
/// coexist while an exact duplicate is rejected (design §13/§14.2).
/// </summary>
public static class SourceCredentialKey
{
    // A control character that cannot appear in a tag key or value, separating the two canonical tag sets so the
    // discriminator is unambiguous.
    private const char Separator = '\u0001';

    /// <summary>Computes the binding discriminator from its management and usage tag sets.</summary>
    /// <param name="managementTags">The binding's management tags.</param>
    /// <param name="usageTags">The binding's usage tags.</param>
    /// <returns>The canonical, order-independent discriminator string.</returns>
    public static string Discriminator(SecurityTagSet managementTags, SecurityTagSet usageTags)
        => $"{CanonicalTags(managementTags)}{Separator}{CanonicalTags(usageTags)}";

    /// <summary>Computes the canonical, order-independent string form of a single tag set — e.g. a run's tags, so a
    /// runner cache can key on it. Compute once (the run's tags are fixed at transport-bind time) and reuse, to keep
    /// the per-request warm path allocation-free.</summary>
    /// <param name="tags">The tag set.</param>
    /// <returns>The canonical <c>key=value;key=value</c> string, ordinal-sorted (empty for an empty set).</returns>
    /// <remarks>
    /// <para><strong>Allocation ledger.</strong> Reads the tag set's persisted bytes via the shared
    /// <see cref="SecurityTagSpanSort"/> (keys/values copied unescaped into one pooled scratch buffer, slices sorted over
    /// a stack table — no <see cref="List{T}"/>, no per-tag string), then assembles the <c>key=value;key=value</c> form
    /// into a second pooled buffer decoded once. Only the result string escapes to the heap — replacing an earlier
    /// <c>ToList()</c> + per-tag <c>$"{k}={v}"</c> interpolation + <c>string.Join</c>. Runs on every binding write /
    /// discriminator and per cache miss, so the floor matters; <c>SourceCredentialKeyBenchmarks</c> is its regression guard.</para>
    /// </remarks>
    public static string CanonicalTags(SecurityTagSet tags)
    {
        if (tags.IsEmpty)
        {
            return string.Empty;
        }

        ReadOnlySpan<byte> json = tags.RawJson;
        int tagCount = tags.Count;

        // Unescaping never grows the byte count, so one scratch buffer of the source length holds every tag's key+value.
        byte[] scratch = ArrayPool<byte>.Shared.Rent(json.Length);
        SecurityTagSpanSort.TagSlice[]? rentedSlices = tagCount > SecurityTagSpanSort.StackTagCapacity ? ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Rent(tagCount) : null;
        try
        {
            scoped Span<SecurityTagSpanSort.TagSlice> table;
            if (rentedSlices is not null)
            {
                table = rentedSlices;
            }
            else
            {
                table = stackalloc SecurityTagSpanSort.TagSlice[SecurityTagSpanSort.StackTagCapacity];
            }

            Span<SecurityTagSpanSort.TagSlice> slices = table[..tagCount];
            int written = SecurityTagSpanSort.Parse(json, scratch, slices);
            SecurityTagSpanSort.Sort(slices, scratch);
            return Assemble(slices, scratch, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
            if (rentedSlices is not null)
            {
                ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Return(rentedSlices);
            }
        }
    }

    // Assembles the sorted tags as "key=value;key=value" UTF-8 in one pooled buffer (one '=' per tag, one ';' between),
    // decoded to the result string once; no per-tag string. Length = total tag bytes + one '=' per tag + (N-1) separators.
    private static string Assemble(ReadOnlySpan<SecurityTagSpanSort.TagSlice> slices, ReadOnlySpan<byte> scratch, int written)
    {
        int length = written + slices.Length + (slices.Length - 1);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            int position = 0;
            for (int i = 0; i < slices.Length; i++)
            {
                if (i > 0)
                {
                    buffer[position++] = (byte)';';
                }

                SecurityTagSpanSort.TagSlice slice = slices[i];
                scratch.Slice(slice.KeyOffset, slice.KeyLength).CopyTo(buffer.AsSpan(position));
                position += slice.KeyLength;
                buffer[position++] = (byte)'=';
                scratch.Slice(slice.ValueOffset, slice.ValueLength).CopyTo(buffer.AsSpan(position));
                position += slice.ValueLength;
            }

            return Encoding.UTF8.GetString(buffer.AsSpan(0, position));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}