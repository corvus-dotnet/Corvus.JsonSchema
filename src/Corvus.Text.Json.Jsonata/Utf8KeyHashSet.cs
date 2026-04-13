// <copyright file="Utf8KeyHashSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Jsonata;

#pragma warning disable CS9191 // The 'ref' modifier for argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

/// <summary>
/// A hash set for UTF-8 byte strings, modeled on <see cref="UniqueItemsHashSet"/>.
/// Uses <see cref="Utf8Hash"/> for hashing and <c>SequenceEqual</c> for collision resolution.
/// Keys are stored in a flat byte buffer; all backing storage is stack-allocated or rented
/// from <see cref="ArrayPool{T}"/>.
/// </summary>
internal ref struct Utf8KeyHashSet
#if NET
    : IDisposable
#endif
{
    private const int EntrySize = 20; // Next(4) + BufOffset(4) + Length(4) + HashCode(8)

    /// <summary>
    /// The recommended stack-alloc size for the bucket buffer (int count).
    /// </summary>
    public const int StackAllocBucketSize = 64;

    /// <summary>
    /// The recommended stack-alloc size for the entries buffer (byte count).
    /// </summary>
    public const int StackAllocEntrySize = 512;

    /// <summary>
    /// The recommended stack-alloc size for the key byte buffer.
    /// </summary>
    public const int StackAllocKeyBufferSize = 512;

    // Small prime table for sizing — avoids dependency on internal HashHelpers.
    private static ReadOnlySpan<int> Primes =>
    [
        3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521,
    ];

    private int[]? _bucketsBacking;
    private Span<int> _buckets;

    private byte[]? _entriesBacking;
    private Span<byte> _entries;

    private byte[]? _keyBufferBacking;
    private Span<byte> _keyBuffer;
    private int _keyBufferUsed;

    private int _count;
    private int _size;

    /// <summary>
    /// Creates a UTF-8 key hash set.
    /// </summary>
    /// <param name="estimatedCount">The estimated number of keys.</param>
    /// <param name="buckets">A working buffer for buckets.</param>
    /// <param name="entries">A working buffer for entries.</param>
    /// <param name="keyBuffer">A working buffer for concatenated key bytes.</param>
    public Utf8KeyHashSet(int estimatedCount, Span<int> buckets, Span<byte> entries, Span<byte> keyBuffer)
    {
        _size = GetPrime(estimatedCount);
        int entriesSize = _size * EntrySize;

        if (_size > buckets.Length)
        {
            _bucketsBacking = ArrayPool<int>.Shared.Rent(_size);
            _buckets = _bucketsBacking.AsSpan(0, _size);
        }
        else
        {
            _buckets = buckets.Slice(0, _size);
        }

        if (entriesSize > entries.Length)
        {
            _entriesBacking = ArrayPool<byte>.Shared.Rent(entriesSize);
            _entries = _entriesBacking.AsSpan(0, entriesSize);
        }
        else
        {
            _entries = entries.Slice(0, entriesSize);
        }

        _keyBuffer = keyBuffer;
        _keyBufferUsed = 0;
        _count = 0;

        _buckets.Clear();
        _entries.Clear();
    }

    /// <summary>
    /// Adds a UTF-8 key if it does not already exist in the set.
    /// </summary>
    /// <param name="key">The UTF-8 key bytes.</param>
    /// <returns><see langword="true"/> if the key was added (not seen before);
    /// <see langword="false"/> if it already existed.</returns>
    public bool AddIfNotExists(ReadOnlySpan<byte> key)
    {
        ulong hashCode = Utf8Hash.GetHashCode(key);
        ref int bucket = ref _buckets[(int)(hashCode % (ulong)_size)];

        // Search chain for existing entry
        uint collisionCount = 0;
        int i = bucket - 1; // 1-based
        while (true)
        {
            int offset = i * EntrySize;
            if ((uint)offset >= (uint)_entries.Length)
            {
                break;
            }

            ReadEntry(_entries.Slice(offset), out ulong entryHash, out int next, out int bufOffset, out int bufLen);

            if (entryHash == hashCode &&
                ((key.Length <= Utf8Hash.PerfectHashLength && (hashCode & Utf8Hash.HashMask) == 0) ||
                 _keyBuffer.Slice(bufOffset, bufLen).SequenceEqual(key)))
            {
                return false;
            }

            i = next;
            collisionCount++;

            if (collisionCount > (uint)_count)
            {
                Debug.Fail("Possible infinite loop in Utf8KeyHashSet.AddIfNotExists.");
                return false;
            }
        }

        // Not found — append key to buffer and add entry
        EnsureKeyBufferCapacity(key.Length);
        key.CopyTo(_keyBuffer.Slice(_keyBufferUsed));
        int keyOffset = _keyBufferUsed;
        _keyBufferUsed += key.Length;

        int entryIndex = _count * EntrySize;
        WriteEntry(_entries.Slice(entryIndex, EntrySize), hashCode, bucket - 1, keyOffset, key.Length);
        _count++;
        bucket = _count; // 1-based

        return true;
    }

    /// <summary>
    /// Gets the number of unique keys stored.
    /// </summary>
    public readonly int Count => _count;

    public void Dispose()
    {
        if (_bucketsBacking is int[] bb)
        {
            ArrayPool<int>.Shared.Return(bb);
        }

        if (_entriesBacking is byte[] eb)
        {
            ArrayPool<byte>.Shared.Return(eb);
        }

        if (_keyBufferBacking is byte[] kb)
        {
            ArrayPool<byte>.Shared.Return(kb);
        }
    }

    private static int GetPrime(int min)
    {
        foreach (int prime in Primes)
        {
            if (prime >= min)
            {
                return prime;
            }
        }

        // Beyond our small table, just use the value + 1 to make it odd
        return min | 1;
    }

    private void EnsureKeyBufferCapacity(int additionalBytes)
    {
        int required = _keyBufferUsed + additionalBytes;
        if (required <= _keyBuffer.Length)
        {
            return;
        }

        int newSize = Math.Max(required, _keyBuffer.Length * 2);
        byte[] newBacking = ArrayPool<byte>.Shared.Rent(newSize);
        _keyBuffer.Slice(0, _keyBufferUsed).CopyTo(newBacking);

        if (_keyBufferBacking is byte[] oldBacking)
        {
            ArrayPool<byte>.Shared.Return(oldBacking);
        }

        _keyBufferBacking = newBacking;
        _keyBuffer = newBacking.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteEntry(Span<byte> destination, ulong hashCode, int next, int bufOffset, int length)
    {
        MemoryMarshal.Write(destination, ref next);
        MemoryMarshal.Write(destination.Slice(4), ref bufOffset);
        MemoryMarshal.Write(destination.Slice(8), ref length);
        MemoryMarshal.Write(destination.Slice(12), ref hashCode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadEntry(ReadOnlySpan<byte> source, out ulong hashCode, out int next, out int bufOffset, out int length)
    {
        next = MemoryMarshal.Read<int>(source);
        bufOffset = MemoryMarshal.Read<int>(source.Slice(4));
        length = MemoryMarshal.Read<int>(source.Slice(8));
        hashCode = MemoryMarshal.Read<ulong>(source.Slice(12));
    }
}