// <copyright file="Utf8NameMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// An allocation-free lookup from UTF-8 keys to values, built once at compile time.
/// </summary>
/// <remarks>
/// A lookup is a length test, one byte read at a position chosen (per length) to separate the keys of that length,
/// and a byte comparison against the one or two candidates that share it: no hash over the name, which is what the
/// generated code's switch-on-length does. Keys of a length with no better position fall back to comparing the few
/// keys of that length in turn.
/// </remarks>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class Utf8NameMap<T>
    where T : class
{
    private readonly byte[][] keys;
    private readonly T[] values;

    // Per length: the byte position that splits its keys, and a 256-entry table of first candidate + 1 (0 = none).
    // Candidates sharing a byte chain through `next`. One array of buckets so that a lookup loads one element for
    // its length: no separate range test, position array and table array.
    private readonly Bucket[] buckets;
    private readonly int[] next;

    public Utf8NameMap(IReadOnlyList<KeyValuePair<byte[], T>> entries)
    {
        int n = entries.Count;
        this.keys = new byte[n][];
        this.values = new T[n];
        this.next = new int[n];
        int maxLength = 0;
        for (int i = 0; i < n; i++)
        {
            this.keys[i] = entries[i].Key;
            this.values[i] = entries[i].Value;
            maxLength = Math.Max(maxLength, this.keys[i].Length);
        }

        int lengths = n == 0 ? 0 : maxLength + 1;
        this.buckets = new Bucket[lengths];
        var byLength = new List<int>?[lengths];
        for (int i = 0; i < n; i++)
        {
            (byLength[this.keys[i].Length] ??= []).Add(i);
        }

        Span<int> counts = stackalloc int[256];
        for (int length = 0; length < lengths; length++)
        {
            List<int>? group = byLength[length];
            if (group is null)
            {
                continue;
            }

            // The position whose byte values spread the group over the most distinct entries.
            int bestPosition = 0;
            int bestDistinct = -1;
            for (int position = 0; position < length; position++)
            {
                counts.Clear();
                int distinct = 0;
                foreach (int i in group)
                {
                    if (counts[this.keys[i][position]]++ == 0)
                    {
                        distinct++;
                    }
                }

                if (distinct > bestDistinct)
                {
                    bestDistinct = distinct;
                    bestPosition = position;
                    if (distinct == group.Count)
                    {
                        break;
                    }
                }
            }

            int[] table = new int[256];
            for (int g = group.Count - 1; g >= 0; g--)
            {
                int i = group[g];
                int b = length == 0 ? 0 : this.keys[i][bestPosition];
                this.next[i] = table[b];
                table[b] = i + 1;
            }

            this.buckets[length] = new Bucket(bestPosition, table);
        }
    }

    public int Count => this.keys.Length;

    public byte[][] Keys => this.keys;

    public ReadOnlySpan<T> Values => this.values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(ReadOnlySpan<byte> key, [NotNullWhen(true)] out T? value)
    {
        int length = key.Length;
        Bucket[] buckets = this.buckets;
        if ((uint)length >= (uint)buckets.Length)
        {
            value = null;
            return false;
        }

        Bucket bucket = buckets[length];
        int[]? table = bucket.Table;
        if (table is null)
        {
            value = null;
            return false;
        }

        int slot = table[length == 0 ? 0 : key[bucket.Position]];
        while (slot != 0)
        {
            int i = slot - 1;
            if (KeyEquals(key, this.keys[i]))
            {
                value = this.values[i];
                return true;
            }

            slot = this.next[i];
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Looks up a value under a one-byte tag without building the tagged key: the tag stands for the key's first byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(byte tag, ReadOnlySpan<byte> value, [NotNullWhen(true)] out T? result)
    {
        int length = value.Length + 1;
        Bucket[] buckets = this.buckets;
        if ((uint)length >= (uint)buckets.Length)
        {
            result = null;
            return false;
        }

        Bucket bucket = buckets[length];
        int[]? table = bucket.Table;
        if (table is null)
        {
            result = null;
            return false;
        }

        int position = bucket.Position;
        int slot = table[position == 0 ? tag : value[position - 1]];
        while (slot != 0)
        {
            int i = slot - 1;
            byte[] key = this.keys[i];
            if (key[0] == tag && KeyEquals(value, key.AsSpan(1)))
            {
                result = this.values[i];
                return true;
            }

            slot = this.next[i];
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Equality of a name and a key of the same length (the bucket guarantees it): up to sixteen bytes as one or two
    /// overlapping word loads, longer keys through the vectorised comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool KeyEquals(ReadOnlySpan<byte> name, ReadOnlySpan<byte> k)
    {
        int n = name.Length;
        if (n >= 8)
        {
            if (n > 16)
            {
                return name.SequenceEqual(k);
            }

            return MemoryMarshal.Read<ulong>(name) == MemoryMarshal.Read<ulong>(k)
                && MemoryMarshal.Read<ulong>(name.Slice(n - 8)) == MemoryMarshal.Read<ulong>(k.Slice(n - 8));
        }

        if (n >= 4)
        {
            return MemoryMarshal.Read<uint>(name) == MemoryMarshal.Read<uint>(k)
                && MemoryMarshal.Read<uint>(name.Slice(n - 4)) == MemoryMarshal.Read<uint>(k.Slice(n - 4));
        }

        for (int b = 0; b < n; b++)
        {
            if (name[b] != k[b])
            {
                return false;
            }
        }

        return true;
    }

    private readonly struct Bucket(int position, int[] table)
    {
        public readonly int Position = position;
        public readonly int[] Table = table;
    }
}