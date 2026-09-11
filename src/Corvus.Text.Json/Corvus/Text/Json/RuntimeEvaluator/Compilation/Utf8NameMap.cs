// <copyright file="Utf8NameMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// An allocation-free lookup from UTF-8 keys to values, built once at compile time.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class Utf8NameMap<T>
    where T : class
{
    private const int LinearThreshold = 8;

    private readonly byte[][] keys;
    private readonly int[] lengths;
    private readonly T[] values;
    private readonly int[]? buckets; // index + 1, 0 = empty
    private readonly ulong[]? hashes;
    private readonly int mask;
    private readonly int minLength;
    private readonly int maxLength;

    public Utf8NameMap(IReadOnlyList<KeyValuePair<byte[], T>> entries)
    {
        int n = entries.Count;
        this.keys = new byte[n][];
        this.lengths = new int[n];
        this.values = new T[n];
        this.minLength = int.MaxValue;
        this.maxLength = 0;
        for (int i = 0; i < n; i++)
        {
            this.keys[i] = entries[i].Key;
            this.lengths[i] = entries[i].Key.Length;
            this.values[i] = entries[i].Value;
            this.minLength = Math.Min(this.minLength, this.keys[i].Length);
            this.maxLength = Math.Max(this.maxLength, this.keys[i].Length);
        }

        if (n > LinearThreshold)
        {
            int size = 16;
            while (size < n * 2)
            {
                size <<= 1;
            }

            this.mask = size - 1;
            this.buckets = new int[size];
            this.hashes = new ulong[n];
            for (int i = 0; i < n; i++)
            {
                ulong h = Utf8Hash.GetHashCode(this.keys[i]);
                this.hashes[i] = h;
                int b = (int)(h & (ulong)this.mask);
                while (this.buckets[b] != 0)
                {
                    b = (b + 1) & this.mask;
                }

                this.buckets[b] = i + 1;
            }
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

        if (this.buckets is null)
        {
            byte[][] ks = this.keys;
            for (int i = 0; i < ks.Length; i++)
            {
                byte[] k = ks[i];
                if (k.Length == length && key.SequenceEqual(k))
                {
                    value = this.values[i];
                    return true;
                }
            }

            value = null;
            return false;
        }

        return this.TryGetHashed(key, out value);
    }

    private bool TryGetHashed(ReadOnlySpan<byte> key, out T? value)
    {
        ulong h = Utf8Hash.GetHashCode(key);
        int b = (int)(h & (ulong)this.mask);
        int[] bs = this.buckets!;
        while (true)
        {
            int slot = bs[b];
            if (slot == 0)
            {
                value = null;
                return false;
            }

            int i = slot - 1;
            if (this.hashes![i] == h && key.SequenceEqual(this.keys[i]))
            {
                value = this.values[i];
                return true;
            }

            b = (b + 1) & this.mask;
        }
    }
}