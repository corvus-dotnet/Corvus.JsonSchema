// <copyright file="Evaluator.UniqueItems.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// <c>uniqueItems</c>: an open-addressing set of item indices keyed by a structural hash computed over the raw
/// UTF-8 (unescaped strings, numbers by value, objects order-independently), sized to the array and cleared only as
/// far as it is used, so an array of objects costs one hash per item instead of a rented table zeroed in full and
/// a UTF-16 transcode of every string. The table is rented (not stack-allocated) so that the set may be passed
/// alongside the evaluation state without the ref-safety analysis seeing stack memory that could escape; arrays of
/// up to <see cref="PairwiseLimit"/> items are checked pairwise instead and rent nothing.
/// </summary>
internal static partial class Evaluator
{
    /// <summary>Arrays of up to this many items are checked pairwise (at most 28 comparisons) without a table.</summary>
    internal const int PairwiseLimit = 8;

    /// <summary>
    /// The set. Slots hold item index + 1 (0 is empty) with the item's hash alongside; linear probing. One rented
    /// array holds the slots followed by the hashes.
    /// </summary>
    internal struct UniqueItemSet
    {
        private int[]? table;
        private int mask;

        public UniqueItemSet(int itemCount)
        {
            int capacity = 16;
            while (capacity < itemCount * 2)
            {
                capacity <<= 1;
            }

            this.table = ArrayPool<int>.Shared.Rent(capacity * 2);
            this.table.AsSpan(0, capacity).Clear();
            this.mask = capacity - 1;
        }

        /// <summary>Adds the item; false when an equal item is already present.</summary>
        public bool TryAdd<TAccess>(ref EvaluationState state, IJsonDocument doc, int valueIndex)
            where TAccess : struct, IDocumentAccess
        {
            int[] table = this.table!;
            int capacity = this.mask + 1;
            int hash = HashValue<TAccess>(ref state, doc, valueIndex);
            int b = hash & this.mask;
            while (true)
            {
                int slot = table[b];
                if (slot == 0)
                {
                    table[b] = valueIndex + 1;
                    table[capacity + b] = hash;
                    return true;
                }

                if (table[capacity + b] == hash && ValuesEqual<TAccess>(ref state, doc, valueIndex, slot - 1))
                {
                    return false;
                }

                b = (b + 1) & this.mask;
            }
        }

        public void Dispose()
        {
            if (this.table is not null)
            {
                ArrayPool<int>.Shared.Return(this.table);
                this.table = null;
            }
        }
    }

    /// <summary>Whether every item of a small array differs from every other, by pairwise structural comparison.</summary>
    private static bool AllUniquePairwise<TAccess>(ref EvaluationState state, IJsonDocument doc, int index, int end)
        where TAccess : struct, IDocumentAccess
    {
        for (int a = index + RowSize; a < end; a = default(TAccess).NextIndex(ref state, doc, a))
        {
            for (int b = default(TAccess).NextIndex(ref state, doc, a); b < end; b = default(TAccess).NextIndex(ref state, doc, b))
            {
                if (ValuesEqual<TAccess>(ref state, doc, a, b))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// A hash that agrees with JSON equality: strings by their unescaped text, numbers by their value, arrays in
    /// order, objects as an order-independent sum over their properties.
    /// </summary>
    private static int HashValue<TAccess>(ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        switch (default(TAccess).TokenType(ref state, doc, index))
        {
            case JsonTokenType.String:
                if (default(TAccess).IsEscaped(ref state, doc, index))
                {
                    using UnescapedUtf8JsonString s = default(TAccess).GetString(ref state, doc, index);
                    return Fnv(s.Span, 0x5f3759df);
                }

                return Fnv(default(TAccess).RawValue(ref state, doc, index), 0x5f3759df);
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index);
                if (Utf8Parser.TryParse(raw, out double d, out int consumed) && consumed == raw.Length)
                {
                    if (d == 0)
                    {
                        d = 0; // -0 and 0 are equal.
                    }

                    long bits = BitConverter.DoubleToInt64Bits(d);
                    return (int)bits ^ (int)(bits >> 32) ^ 0x2545F491;
                }

                return 0x2545F491;
            }

            case JsonTokenType.True:
                return 0x0151_7a7e;
            case JsonTokenType.False:
                return 0x0151_7a7f;
            case JsonTokenType.Null:
                return 0x0151_7a80;
            case JsonTokenType.StartArray:
            {
                int h = 0x6a09e667;
                int end = default(TAccess).EndIndex(ref state, doc, index);
                for (int i = index + RowSize; i < end; i = default(TAccess).NextIndex(ref state, doc, i))
                {
                    h = (h * 31) + HashValue<TAccess>(ref state, doc, i);
                }

                return h;
            }

            case JsonTokenType.StartObject:
            {
                int h = 0x3c6ef372;
                int end = default(TAccess).EndIndex(ref state, doc, index);
                for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
                {
                    int nameHash;
                    using (UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex))
                    {
                        nameHash = Fnv(name.Span, 0x27d4eb2f);
                    }

                    h += (nameHash * 397) ^ HashValue<TAccess>(ref state, doc, valueIndex);
                }

                return h;
            }

            default:
                return 0;
        }
    }

    /// <summary>Hashes eight bytes at a time (the order only has to be stable within a process).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Fnv(ReadOnlySpan<byte> bytes, int seed)
    {
        const ulong Multiplier = 0x9E3779B97F4A7C15UL;
        ulong h = ((ulong)(uint)seed ^ 0x243F6A8885A308D3UL) * Multiplier;
        int i = 0;
        for (; i + sizeof(ulong) <= bytes.Length; i += sizeof(ulong))
        {
            h = (h ^ System.Runtime.InteropServices.MemoryMarshal.Read<ulong>(bytes.Slice(i, sizeof(ulong)))) * Multiplier;
            h ^= h >> 29;
        }

        if (i < bytes.Length)
        {
            ulong tail = (ulong)(bytes.Length - i) << 56;
            for (int j = 0; i + j < bytes.Length; j++)
            {
                tail |= (ulong)bytes[i + j] << (8 * j);
            }

            h = (h ^ tail) * Multiplier;
            h ^= h >> 29;
        }

        return (int)(h ^ (h >> 32));
    }

    /// <summary>JSON equality of two values in the same document, with cheap prefilters for scalars.</summary>
    private static bool ValuesEqual<TAccess>(ref EvaluationState state, IJsonDocument doc, int a, int b)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, a);
        if (default(TAccess).TokenType(ref state, doc, b) != tokenType)
        {
            return false;
        }

        switch (tokenType)
        {
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
                return true;
            case JsonTokenType.String:
                return default(TAccess).RawValue(ref state, doc, a).SequenceEqual(default(TAccess).RawValue(ref state, doc, b))
                    || ((default(TAccess).IsEscaped(ref state, doc, a) || default(TAccess).IsEscaped(ref state, doc, b)) && JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, a, doc, b));
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> ra = default(TAccess).RawValue(ref state, doc, a);
                ReadOnlySpan<byte> rb = default(TAccess).RawValue(ref state, doc, b);
                if (ra.SequenceEqual(rb))
                {
                    return true;
                }

                // Two canonical integer literals with different text are different numbers.
                return !(IsCanonicalInteger(ra) && IsCanonicalInteger(rb)) && JsonElementHelpers.AreEqualJsonNumbers(ra, rb);
            }

            default:
                return JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, a, doc, b);
        }
    }
}