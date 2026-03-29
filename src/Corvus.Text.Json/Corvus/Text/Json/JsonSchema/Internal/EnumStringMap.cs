// <copyright file="EnumStringMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

#pragma warning disable CS9191 // This is the warning about in/ref params; we disable it because we target netstandard2.0

/// <summary>
/// A hash map from UTF-8 string keys to integer values for efficient O(1) average-case lookup.
/// </summary>
/// <remarks>
/// <para>
/// This type is used by generated code for oneOf discriminator dispatch when the number of branches
/// exceeds a threshold. It uses the same hash algorithm as <see cref="EnumStringSet"/>
/// and <see cref="PropertySchemaMatchers{T}"/>.
/// </para>
/// </remarks>
public class EnumStringMap
{
    private const ulong HashMask = 0xFFUL << 56;
    private const int HashLength = 8;
    private const int EntrySize = 16;

    private readonly int _bucketCount;
    private readonly int _count;
    private readonly int[] _bucketsBacking;
    private readonly byte[] _entriesBacking;
    private readonly List<EnumStringSet.Utf8ValueProvider> _keyProviders;

    /// <summary>
    /// Creates an enum string map for efficient lookup of integer values by UTF-8 string keys.
    /// </summary>
    /// <param name="keyProviders">The list of delegates providing the UTF-8 bytes for each key.</param>
    /// <remarks>
    /// The integer value associated with each key is its index in the <paramref name="keyProviders"/> list.
    /// </remarks>
    [CLSCompliant(false)]
    public EnumStringMap(List<EnumStringSet.Utf8ValueProvider> keyProviders)
    {
        _keyProviders = keyProviders;
        _count = keyProviders.Count;
        _bucketCount = HashHelpers.GetPrime(_count);
        int entriesSize = _bucketCount * EntrySize;

        _bucketsBacking = ArrayPool<int>.Shared.Rent(_bucketCount);
        _entriesBacking = ArrayPool<byte>.Shared.Rent(entriesSize);

        Span<int> buckets = _bucketsBacking.AsSpan(0, _bucketCount);
        Span<byte> entries = _entriesBacking.AsSpan(0, entriesSize);
        buckets.Clear();
        entries.Clear();

        int index = 0;

        foreach (EnumStringSet.Utf8ValueProvider provider in keyProviders)
        {
            ReadOnlySpan<byte> key = provider();
            ulong hashCode = GetHashCode(key);
            ref int bucket = ref GetBucket(buckets, hashCode);
            int entryOffset = index * EntrySize;
            WriteEntry(entries.Slice(entryOffset, EntrySize), hashCode, bucket - 1, index);
            index++;
            bucket = index; // 1-based
        }
    }

    /// <summary>
    /// Attempts to find the integer value associated with the specified UTF-8 string key.
    /// </summary>
    /// <param name="utf8Key">The UTF-8 bytes of the key to look up.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the integer value (the index of the key in the original list).</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public bool TryGetValue(ReadOnlySpan<byte> utf8Key, out int value)
    {
        Span<int> buckets = _bucketsBacking.AsSpan(0, _bucketCount);
        Span<byte> entries = _entriesBacking.AsSpan(0, _count * EntrySize);

        ulong hashCode = GetHashCode(utf8Key);
        int i = GetBucket(buckets, hashCode) - 1;
        uint collisionCount = 0;

        while (true)
        {
            int offset = i * EntrySize;

            if ((uint)offset >= (uint)entries.Length)
            {
                value = -1;
                return false;
            }

            ReadEntry(entries.Slice(offset, EntrySize), out ulong entryHash, out int next, out int valueIndex);

            if (entryHash == hashCode &&
                    (((utf8Key.Length < HashLength) &&
                        ((hashCode & HashMask) == 0)) ||
                    _keyProviders[valueIndex]().SequenceEqual(utf8Key)))
            {
                value = valueIndex;
                return true;
            }

            i = next;
            collisionCount++;

            if (collisionCount > (uint)_count)
            {
                Debug.Fail("Possible infinite loop in EnumStringMap.TryGetValue.");
                value = -1;
                return false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetHashCode(in ReadOnlySpan<byte> key)
    {
        int length = key.Length;

        return length switch
        {
            7 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
            6 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40),
            5 => MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32),
            4 => MemoryMarshal.Read<uint>(key.Slice(0, 4)),
            3 => ((ulong)key[2] << 16)
                    + ((ulong)key[1] << 8)
                    + key[0],
            2 => ((ulong)key[1] << 8)
                    + key[0],
            1 => key[0],
            0 => 0,
            _ => ((ulong)((length + key[7] + key[key.Length - 1]) % 256) << 56)
                    + MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int GetBucket(Span<int> buckets, ulong hashCode)
    {
        return ref buckets[(int)(hashCode % (ulong)_bucketCount)];
    }

    private static void WriteEntry(Span<byte> destination, ulong hashCode, int next, int valueIndex)
    {
        MemoryMarshal.Write(destination, ref next);
        MemoryMarshal.Write(destination.Slice(4), ref valueIndex);
        MemoryMarshal.Write(destination.Slice(8), ref hashCode);
    }

    private static void ReadEntry(ReadOnlySpan<byte> source, out ulong hashCode, out int next, out int valueIndex)
    {
        next = MemoryMarshal.Read<int>(source);
        valueIndex = MemoryMarshal.Read<int>(source.Slice(4));
        hashCode = MemoryMarshal.Read<ulong>(source.Slice(8));
    }
}