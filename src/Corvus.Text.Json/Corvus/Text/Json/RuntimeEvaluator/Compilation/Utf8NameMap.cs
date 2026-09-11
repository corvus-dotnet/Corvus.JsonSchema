// <copyright file="Utf8NameMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
    // Candidates sharing a byte chain through `next`.
    private readonly int[] positionByLength;
    private readonly int[][] tableByLength;
    private readonly int[] next;
    private readonly int minLength;
    private readonly int maxLength;

    public Utf8NameMap(IReadOnlyList<KeyValuePair<byte[], T>> entries)
    {
        int n = entries.Count;
        this.keys = new byte[n][];
        this.values = new T[n];
        this.next = new int[n];
        this.minLength = int.MaxValue;
        this.maxLength = 0;
        for (int i = 0; i < n; i++)
        {
            this.keys[i] = entries[i].Key;
            this.values[i] = entries[i].Value;
            this.minLength = Math.Min(this.minLength, this.keys[i].Length);
            this.maxLength = Math.Max(this.maxLength, this.keys[i].Length);
        }

        if (n == 0)
        {
            this.minLength = 0;
            this.maxLength = -1;
        }

        int lengths = Math.Max(0, this.maxLength + 1);
        this.positionByLength = new int[lengths];
        this.tableByLength = new int[lengths][];
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
            this.positionByLength[length] = bestPosition;
            for (int g = group.Count - 1; g >= 0; g--)
            {
                int i = group[g];
                int b = length == 0 ? 0 : this.keys[i][bestPosition];
                this.next[i] = table[b];
                table[b] = i + 1;
            }

            this.tableByLength[length] = table;
        }
    }

    public int Count => this.keys.Length;

    public byte[][] Keys => this.keys;

    public ReadOnlySpan<T> Values => this.values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(ReadOnlySpan<byte> key, [NotNullWhen(true)] out T? value)
    {
        int length = key.Length;
        if (length < this.minLength || length > this.maxLength)
        {
            value = null;
            return false;
        }

        int[]? table = this.tableByLength[length];
        if (table is null)
        {
            value = null;
            return false;
        }

        int slot = table[length == 0 ? 0 : key[this.positionByLength[length]]];
        while (slot != 0)
        {
            int i = slot - 1;
            if (key.SequenceEqual(this.keys[i]))
            {
                value = this.values[i];
                return true;
            }

            slot = this.next[i];
        }

        value = null;
        return false;
    }
}