// <copyright file="DirectoryRecordView.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>One captured attribute's position within a <see cref="DirectoryRecordView"/>'s scratch buffer (the key and value byte ranges).</summary>
/// <param name="KeyOffset">The key's start in the scratch buffer.</param>
/// <param name="KeyLength">The key's length.</param>
/// <param name="ValueOffset">The value's start in the scratch buffer.</param>
/// <param name="ValueLength">The value's length.</param>
public readonly record struct DirectoryAttributeSlice(int KeyOffset, int KeyLength, int ValueOffset, int ValueLength);

/// <summary>
/// A stack-only, zero-allocation view of one fetched directory record's <em>captured</em> attributes (design §16.5.4) —
/// the unescaped UTF-8 key/value bytes an adapter copied into a pooled scratch buffer for exactly the attributes the
/// mapper declared it reads (plus the grantee value), with no managed <see cref="string"/> per attribute. A
/// <see cref="IDirectoryIdentitySpanMapper"/> reads these as spans and writes the identity straight into an
/// <c>IdentityBuilder</c> — the bytes-to-bytes path. The view is valid only for the synchronous duration of the mapper
/// call (it borrows the adapter's pooled scratch), which is why it is a <see langword="ref"/> struct.
/// </summary>
public readonly ref struct DirectoryRecordView
{
    private readonly ReadOnlySpan<byte> scratch;
    private readonly ReadOnlySpan<DirectoryAttributeSlice> slices;

    /// <summary>Initializes a new instance of the <see cref="DirectoryRecordView"/> struct.</summary>
    /// <param name="kind">The grantee kind the adapter queried.</param>
    /// <param name="value">The grantee value as unescaped UTF-8 (the searchable id — e.g. the source of <c>sys:sub</c>).</param>
    /// <param name="scratch">The pooled buffer holding the captured key/value bytes.</param>
    /// <param name="slices">The captured attributes' positions within <paramref name="scratch"/>.</param>
    public DirectoryRecordView(GranteeKind kind, ReadOnlySpan<byte> value, ReadOnlySpan<byte> scratch, ReadOnlySpan<DirectoryAttributeSlice> slices)
    {
        this.Kind = kind;
        this.ValueUtf8 = value;
        this.scratch = scratch;
        this.slices = slices;
    }

    /// <summary>Gets the grantee kind the adapter queried for (person / team / role).</summary>
    public GranteeKind Kind { get; }

    /// <summary>Gets the grantee value as unescaped UTF-8 — the searchable id, the usual source of <c>sys:sub</c>.</summary>
    public ReadOnlySpan<byte> ValueUtf8 { get; }

    /// <summary>
    /// Gets a captured attribute's value as unescaped UTF-8, or an empty span if the adapter did not capture it (i.e. the
    /// mapper did not declare it in <see cref="IDirectoryIdentitySpanMapper.RequiredAttributes"/>). Match the attribute by a
    /// UTF-8 literal (<c>"department"u8</c>) so no key string is allocated.
    /// </summary>
    /// <param name="name">The attribute name as UTF-8, in the adapter's flattened-key notation.</param>
    /// <returns>The value bytes, or an empty span when absent.</returns>
    public readonly ReadOnlySpan<byte> AttributeUtf8(ReadOnlySpan<byte> name)
    {
        foreach (ref readonly DirectoryAttributeSlice slice in this.slices)
        {
            if (this.scratch.Slice(slice.KeyOffset, slice.KeyLength).SequenceEqual(name))
            {
                return this.scratch.Slice(slice.ValueOffset, slice.ValueLength);
            }
        }

        return default;
    }
}