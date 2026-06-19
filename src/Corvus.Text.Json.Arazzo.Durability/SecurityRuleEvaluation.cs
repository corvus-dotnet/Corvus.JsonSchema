// <copyright file="SecurityRuleEvaluation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A row's parsed security tags as unescaped UTF-8 spans over a shared scratch buffer (design §14.2) — the bytes-to-bytes
/// view a <see cref="SecurityRule"/> evaluates against, so a list/search scan checks each candidate row without
/// materialising a managed <see cref="SecurityTag"/> string or <see cref="List{T}"/>. A <see langword="ref"/> struct: it
/// cannot escape the pooled-buffer scope that owns the scratch.
/// </summary>
internal readonly ref struct RowTags
{
    private readonly ReadOnlySpan<byte> scratch;
    private readonly ReadOnlySpan<SecurityTagSpanSort.TagSlice> slices;

    internal RowTags(ReadOnlySpan<byte> scratch, ReadOnlySpan<SecurityTagSpanSort.TagSlice> slices)
    {
        this.scratch = scratch;
        this.slices = slices;
    }

    /// <summary>Gets the number of tags.</summary>
    internal int Count => this.slices.Length;

    /// <summary>Gets the <paramref name="index"/>-th tag's key as unescaped UTF-8.</summary>
    /// <param name="index">The tag index.</param>
    /// <returns>The key bytes.</returns>
    internal ReadOnlySpan<byte> Key(int index) => this.scratch.Slice(this.slices[index].KeyOffset, this.slices[index].KeyLength);

    /// <summary>Gets the <paramref name="index"/>-th tag's value as unescaped UTF-8.</summary>
    /// <param name="index">The tag index.</param>
    /// <returns>The value bytes.</returns>
    internal ReadOnlySpan<byte> Value(int index) => this.scratch.Slice(this.slices[index].ValueOffset, this.slices[index].ValueLength);
}

/// <summary>
/// A principal's claims encoded to UTF-8 once (design §14.2): the <c>$claim.*</c> / <c>$claims.*</c> side of rule
/// evaluation, so each per-row comparison against a row tag's unescaped UTF-8 is span-vs-span. Built once per request
/// (the claims are fixed for a <see cref="SecurityFilter"/>'s lifetime) and reused across every scanned row.
/// </summary>
/// <remarks>
/// The claim names and values come from the principal (a genuinely string-typed source — a <c>ClaimsPrincipal</c>); this
/// encodes them to UTF-8 so the comparison crosses to bytes once, on the small fixed claim set, instead of decoding every
/// (many) row tag to a string. Ordinal UTF-8 byte equality is exactly the ordinal string equality of the same code points.
/// </remarks>
internal readonly struct Utf8ClaimSet
{
    private readonly byte[][]? keys;
    private readonly byte[][][]? values;

    /// <summary>Initializes a new instance of the <see cref="Utf8ClaimSet"/> struct, encoding each claim name and value to UTF-8.</summary>
    /// <param name="claims">The principal's claims: claim name → its values.</param>
    internal Utf8ClaimSet(IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        int count = claims.Count;
        this.keys = new byte[count][];
        this.values = new byte[count][][];
        int i = 0;
        foreach (KeyValuePair<string, IReadOnlyList<string>> claim in claims)
        {
            this.keys[i] = Encoding.UTF8.GetBytes(claim.Key);
            var encoded = new byte[claim.Value.Count][];
            for (int j = 0; j < claim.Value.Count; j++)
            {
                encoded[j] = Encoding.UTF8.GetBytes(claim.Value[j]);
            }

            this.values[i] = encoded;
            i++;
        }
    }

    /// <summary>Gets the UTF-8 value list for the claim named <paramref name="name"/>.</summary>
    /// <param name="name">The claim name as unescaped UTF-8.</param>
    /// <param name="claimValues">The claim's values as UTF-8 on success; an empty array otherwise.</param>
    /// <returns><see langword="true"/> if the principal holds the named claim.</returns>
    internal bool TryGetValues(ReadOnlySpan<byte> name, out byte[][] claimValues)
    {
        if (this.keys is not null)
        {
            for (int i = 0; i < this.keys.Length; i++)
            {
                if (name.SequenceEqual(this.keys[i]))
                {
                    claimValues = this.values![i];
                    return true;
                }
            }
        }

        claimValues = [];
        return false;
    }

    /// <summary>Whether a row tag <c>(key, value)</c> is covered: the principal holds the claim named <paramref name="key"/> with <paramref name="value"/> among its values.</summary>
    /// <param name="key">The tag key as unescaped UTF-8.</param>
    /// <param name="value">The tag value as unescaped UTF-8.</param>
    /// <returns><see langword="true"/> if the claim set covers the tag.</returns>
    internal bool Covers(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        if (this.TryGetValues(key, out byte[][] claimValues))
        {
            foreach (byte[] claimValue in claimValues)
            {
                if (value.SequenceEqual(claimValue))
                {
                    return true;
                }
            }
        }

        return false;
    }
}