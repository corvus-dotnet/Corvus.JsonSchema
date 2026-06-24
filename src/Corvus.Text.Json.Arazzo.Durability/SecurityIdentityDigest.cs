// <copyright file="SecurityIdentityDigest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A stable, order-independent digest of a <see cref="SecurityTagSet"/> identity — the index key that makes an identity
/// <em>collision probe</em> (design §16.5.4) a uniform indexed equality lookup across every store, rather than a
/// per-backend relational set-equality query. Two identities are set-equal (the authorization comparison) iff their
/// digests are equal, so a grantee whose resolved identity already belongs to a <em>different</em> grantee is found by a
/// single indexed seek.
/// </summary>
/// <remarks>
/// <para>The digest is the lower-case hex SHA-256 of the identity's tags in canonical (ordinal-sorted) order, so it is
/// independent of the tag order a producer happened to use and is a fixed 64 characters (index-safe on every backend,
/// unlike the variable-length canonical JSON). SHA-256 is collision-free in practice, so a digest match <em>is</em> a
/// set-equality match — the probe needs no second verification pass. The <strong>empty</strong> identity (the unscoped /
/// system sentinel) has no digest (<see langword="null"/>): it is not a real principal identity and never collides.</para>
/// <para><strong>Allocation ledger.</strong> <see cref="Compute"/> reads the tag set's persisted bytes directly via the
/// shared <see cref="SecurityTagSpanSort"/> (each tag's key and value copied unescaped into one pooled scratch buffer —
/// no per-tag string, no <see cref="List{T}"/> — and the slices insertion-sorted over a stack table), then assembles the
/// canonical length-prefixed bytes in a second pooled buffer and hashes into a stack span. Every working buffer is
/// pooled or on the stack, so the <strong>only</strong> heap allocation is the 64-character hex result the caller
/// indexes on. It runs on each sighting (the write path) as well as the probe, so this floor matters; the durability
/// benchmark (<c>Find_Conflict</c>) is its regression guard.</para>
/// </remarks>
public static class SecurityIdentityDigest
{
    /// <summary>The length, in UTF-8 bytes, of a formatted digest (the 32-byte SHA-256 as lower-case hex).</summary>
    public const int DigestUtf8Length = 64;

    /// <summary>Computes the collision-probe digest of an identity, or <see langword="null"/> for the empty identity.</summary>
    /// <param name="identity">The <c>sys:</c> identity to digest.</param>
    /// <returns>The 64-character lower-case hex SHA-256 of the canonical identity, or <see langword="null"/> when empty.</returns>
    /// <remarks>Allocates the hex result string; for the response-projection path that writes the digest straight into the
    /// JSON value, use <see cref="FormatUtf8"/> (no string).</remarks>
    public static string? Compute(SecurityTagSet identity)
    {
        Span<byte> hash = stackalloc byte[32];
        return TryComputeHash(identity, hash) ? Convert.ToHexStringLower(hash) : null;
    }

    /// <summary>Writes the collision-probe digest of an identity as lower-case hex UTF-8 into <paramref name="destination"/>
    /// (no string allocation), returning the bytes written (<see cref="DigestUtf8Length"/>), or 0 for the empty identity.</summary>
    /// <param name="identity">The <c>sys:</c> identity to digest.</param>
    /// <param name="destination">The destination span; must be at least <see cref="DigestUtf8Length"/> bytes.</param>
    /// <returns>The number of UTF-8 bytes written (<see cref="DigestUtf8Length"/>), or 0 for the empty identity.</returns>
    public static int FormatUtf8(SecurityTagSet identity, Span<byte> destination)
    {
        Span<byte> hash = stackalloc byte[32];
        if (!TryComputeHash(identity, hash))
        {
            return 0;
        }

        WriteHexUtf8(hash, destination);
        return DigestUtf8Length;
    }

    // Parses + ordinal-sorts the tags (pooled scratch + stack/pooled slice table, no per-tag string), assembles the
    // canonical length-prefixed bytes, and hashes into hash32. Returns false (no hash) for the empty identity, which is
    // not a real principal and never collides. Every working buffer is pooled or on the stack — nothing escapes.
    private static bool TryComputeHash(SecurityTagSet identity, Span<byte> hash32)
    {
        if (identity.IsEmpty)
        {
            return false;
        }

        ReadOnlySpan<byte> json = identity.RawJson;
        int tagCount = identity.Count;

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
            HashCanonical(slices, scratch, written, hash32);
            return true;
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

    // Assembles the sorted tags as length-prefixed [keyLen][key][valLen][value] runs in one pooled buffer — the length
    // prefixes make two distinct sets unambiguous without a delimiter — and hashes them into the caller's stack span.
    private static void HashCanonical(ReadOnlySpan<SecurityTagSpanSort.TagSlice> slices, ReadOnlySpan<byte> scratch, int written, Span<byte> hash32)
    {
        int length = written + (slices.Length * (2 * sizeof(int)));
        byte[] canonical = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            int position = 0;
            foreach (SecurityTagSpanSort.TagSlice slice in slices)
            {
                BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(position), slice.KeyLength);
                position += sizeof(int);
                scratch.Slice(slice.KeyOffset, slice.KeyLength).CopyTo(canonical.AsSpan(position));
                position += slice.KeyLength;
                BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(position), slice.ValueLength);
                position += sizeof(int);
                scratch.Slice(slice.ValueOffset, slice.ValueLength).CopyTo(canonical.AsSpan(position));
                position += slice.ValueLength;
            }

            SHA256.HashData(canonical.AsSpan(0, position), hash32);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(canonical);
        }
    }

    // Lower-case hex (UTF-8) of each byte, written straight into the destination — no intermediate string.
    private static void WriteHexUtf8(ReadOnlySpan<byte> hash, Span<byte> destination)
    {
        ReadOnlySpan<byte> hex = "0123456789abcdef"u8;
        for (int i = 0; i < hash.Length; i++)
        {
            destination[i * 2] = hex[hash[i] >> 4];
            destination[(i * 2) + 1] = hex[hash[i] & 0xF];
        }
    }
}