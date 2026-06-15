// <copyright file="PooledUtf8Map.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A pooled, UTF-8-keyed map used for a run's working registers (retry counters, step outputs) over the lifetime of
/// one execution slice. Unlike a <see cref="Dictionary{TKey,TValue}"/> of <see cref="string"/> keys it allocates
/// nothing per entry: keys are copied into a single <c>ArrayPool</c>-rented arena, the hash buckets/entries are
/// pooled <see cref="int"/> arrays, and values live in a pooled <typeparamref name="TValue"/> array. Restoring N
/// completed steps from a checkpoint therefore costs one map object instead of N key strings plus a growing GC
/// dictionary (see <c>WorkingStateMaterializationBenchmarks</c>).
/// </summary>
/// <typeparam name="TValue">The value type (e.g. <see cref="int"/> retry counts, or borrowed <c>JsonElement</c> step-output views).</typeparam>
/// <remarks>
/// <para>
/// It is a class (not a readonly-struct holder like <see cref="TagSet"/>): it owns pooled buffers, is stored on the
/// checkpoint state / run and held across <c>await</c> boundaries, and must dispose idempotently — value semantics
/// would double-return the pooled arrays (cf. <c>PooledDocumentList</c> vs <c>RentedJson</c>). It is not thread-safe;
/// a run executes its steps sequentially.
/// </para>
/// <para>
/// The string-keyed members transcode to a stack buffer for the lookup (zero GC) so the generated executor can keep
/// calling the run with its interned step-id literals unchanged. Hot paths that enumerate (the checkpoint serializer)
/// MUST use the span-native <see cref="GetEnumerator"/> — never a per-key <see cref="string"/> projection.
/// </para>
/// </remarks>
public sealed class PooledUtf8Map<TValue> : IDisposable
{
    private const int StackallocByteThreshold = 256;
    private const int EntryInts = 4; // [0]=hashCode, [1]=next(1-based, 0=end), [2]=keyOffset, [3]=keyLength

    private int[]? buckets;
    private int[]? entries;
    private byte[]? keyArena;
    private TValue[]? values;
    private int bucketCount;
    private int capacity;
    private int keyArenaUsed;

    private PooledUtf8Map()
    {
    }

    /// <summary>Gets the number of entries.</summary>
    public int Count { get; private set; }

    /// <summary>Rents an empty map sized for an expected entry count.</summary>
    /// <param name="expectedCapacity">The expected number of entries (e.g. a checkpoint's <c>GetPropertyCount</c>); pre-sizing avoids rehash growth on restore.</param>
    /// <returns>The rented map.</returns>
    public static PooledUtf8Map<TValue> Rent(int expectedCapacity)
    {
        int cap = expectedCapacity < 4 ? 4 : expectedCapacity;
        int prime = NextPrime(cap);
        var map = new PooledUtf8Map<TValue>();
        int[] rentedBuckets = ArrayPool<int>.Shared.Rent(prime);
        Array.Clear(rentedBuckets, 0, prime);
        map.buckets = rentedBuckets;
        map.bucketCount = prime;
        map.entries = ArrayPool<int>.Shared.Rent(cap * EntryInts);
        map.values = ArrayPool<TValue>.Shared.Rent(cap);
        map.keyArena = ArrayPool<byte>.Shared.Rent(cap * 16);
        map.capacity = Math.Min(map.values.Length, map.entries.Length / EntryInts);
        return map;
    }

    /// <summary>Gets the value for a UTF-8 key.</summary>
    /// <param name="key">The UTF-8 key.</param>
    /// <param name="value">The value, if found.</param>
    /// <returns><see langword="true"/> if the key was present.</returns>
    public bool TryGetValue(ReadOnlySpan<byte> key, out TValue value)
    {
        int index = this.FindEntry(key, out _);
        if (index >= 0)
        {
            value = this.values![index];
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Gets the value for a string key (transcoded to UTF-8 for the lookup; zero GC).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, if found.</param>
    /// <returns><see langword="true"/> if the key was present.</returns>
    public bool TryGetValue(string key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        int max = Encoding.UTF8.GetMaxByteCount(key.Length);
        byte[]? rented = max > StackallocByteThreshold ? ArrayPool<byte>.Shared.Rent(max) : null;
        try
        {
            Span<byte> buffer = rented ?? stackalloc byte[StackallocByteThreshold];
            int written = Encoding.UTF8.GetBytes(key, buffer);
            return this.TryGetValue(buffer[..written], out value);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Adds or updates an entry by UTF-8 key (the key is copied into the pooled arena on insert).</summary>
    /// <param name="key">The UTF-8 key.</param>
    /// <param name="value">The value.</param>
    public void Set(ReadOnlySpan<byte> key, TValue value)
    {
        int index = this.FindEntry(key, out int hash);
        if (index >= 0)
        {
            this.values![index] = value;
            return;
        }

        if (this.Count == this.capacity)
        {
            this.Grow();
        }

        int keyOffset = this.AppendKey(key);
        int entry = this.Count;
        int eb = entry * EntryInts;
        int bucket = (int)((uint)hash % (uint)this.bucketCount);
        this.entries![eb] = hash;
        this.entries[eb + 1] = this.buckets![bucket];
        this.entries[eb + 2] = keyOffset;
        this.entries[eb + 3] = key.Length;
        this.values![entry] = value;
        this.buckets[bucket] = entry + 1;
        this.Count = entry + 1;
    }

    /// <summary>Adds or updates an entry by string key (transcoded to UTF-8; zero GC).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Set(string key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        int max = Encoding.UTF8.GetMaxByteCount(key.Length);
        byte[]? rented = max > StackallocByteThreshold ? ArrayPool<byte>.Shared.Rent(max) : null;
        try
        {
            Span<byte> buffer = rented ?? stackalloc byte[StackallocByteThreshold];
            int written = Encoding.UTF8.GetBytes(key, buffer);
            this.Set(buffer[..written], value);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Gets a forward-only, allocation-free enumerator over the entries in insertion order.</summary>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    public void Dispose()
    {
        int[]? b = Interlocked.Exchange(ref this.buckets, null);
        if (b is not null)
        {
            ArrayPool<int>.Shared.Return(b);
        }

        int[]? e = Interlocked.Exchange(ref this.entries, null);
        if (e is not null)
        {
            ArrayPool<int>.Shared.Return(e);
        }

        byte[]? k = Interlocked.Exchange(ref this.keyArena, null);
        if (k is not null)
        {
            ArrayPool<byte>.Shared.Return(k);
        }

        TValue[]? v = Interlocked.Exchange(ref this.values, null);
        if (v is not null)
        {
            // Clear when the value carries a managed reference (e.g. a JsonElement view) so the pool does not pin it.
            ArrayPool<TValue>.Shared.Return(v, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
        }
    }

    private static int NextPrime(int min)
    {
        for (int candidate = min | 1; candidate < int.MaxValue; candidate += 2)
        {
            if (IsPrime(candidate))
            {
                return candidate;
            }
        }

        return min;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2)
        {
            return false;
        }

        if ((n & 1) == 0)
        {
            return n == 2;
        }

        for (int i = 3; (long)i * i <= n; i += 2)
        {
            if (n % i == 0)
            {
                return false;
            }
        }

        return true;
    }

    private int FindEntry(ReadOnlySpan<byte> key, out int hash)
    {
        hash = Hash(key);
        if (this.Count == 0)
        {
            return -1;
        }

        int bucket = (int)((uint)hash % (uint)this.bucketCount);
        int i = this.buckets![bucket] - 1;
        int[] e = this.entries!;
        byte[] arena = this.keyArena!;
        while (i >= 0)
        {
            int eb = i * EntryInts;
            if (e[eb] == hash && key.SequenceEqual(arena.AsSpan(e[eb + 2], e[eb + 3])))
            {
                return i;
            }

            i = e[eb + 1] - 1;
        }

        return -1;
    }

    private static int Hash(ReadOnlySpan<byte> key)
    {
        // FNV-1a 32-bit: stable within a run, sufficient for short step-id keys.
        uint hash = 2166136261u;
        foreach (byte b in key)
        {
            hash ^= b;
            hash *= 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    private int AppendKey(ReadOnlySpan<byte> key)
    {
        byte[] arena = this.keyArena!;
        if (this.keyArenaUsed + key.Length > arena.Length)
        {
            int newSize = Math.Max(arena.Length * 2, this.keyArenaUsed + key.Length);
            byte[] grown = ArrayPool<byte>.Shared.Rent(newSize);
            Array.Copy(arena, grown, this.keyArenaUsed);
            ArrayPool<byte>.Shared.Return(arena);
            this.keyArena = grown;
            arena = grown;
        }

        int offset = this.keyArenaUsed;
        key.CopyTo(arena.AsSpan(offset));
        this.keyArenaUsed += key.Length;
        return offset;
    }

    private void Grow()
    {
        int newCapacity = this.capacity * 2;
        int[] newEntries = ArrayPool<int>.Shared.Rent(newCapacity * EntryInts);
        TValue[] newValues = ArrayPool<TValue>.Shared.Rent(newCapacity);
        Array.Copy(this.entries!, newEntries, this.Count * EntryInts);
        Array.Copy(this.values!, newValues, this.Count);

        int[] oldEntries = this.entries!;
        TValue[] oldValues = this.values!;
        this.entries = newEntries;
        this.values = newValues;
        this.capacity = Math.Min(newValues.Length, newEntries.Length / EntryInts);
        ArrayPool<int>.Shared.Return(oldEntries);
        ArrayPool<TValue>.Shared.Return(oldValues, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());

        // Rehash into a larger bucket table.
        int prime = NextPrime(this.capacity);
        int[] oldBuckets = this.buckets!;
        int[] newBuckets = ArrayPool<int>.Shared.Rent(prime);
        Array.Clear(newBuckets, 0, prime);
        this.buckets = newBuckets;
        this.bucketCount = prime;
        ArrayPool<int>.Shared.Return(oldBuckets);
        for (int i = 0; i < this.Count; i++)
        {
            int eb = i * EntryInts;
            int bucket = (int)((uint)newEntries[eb] % (uint)prime);
            newEntries[eb + 1] = newBuckets[bucket];
            newBuckets[bucket] = i + 1;
        }
    }

    /// <summary>A forward-only, allocation-free enumerator over a <see cref="PooledUtf8Map{TValue}"/>.</summary>
    public ref struct Enumerator
    {
        private readonly PooledUtf8Map<TValue> map;
        private int index;

        internal Enumerator(PooledUtf8Map<TValue> map)
        {
            this.map = map;
            this.index = -1;
        }

        /// <summary>Gets the current entry's UTF-8 key (a view into the pooled arena).</summary>
        public readonly ReadOnlySpan<byte> CurrentKey
        {
            get
            {
                int eb = this.index * EntryInts;
                return this.map.keyArena!.AsSpan(this.map.entries![eb + 2], this.map.entries[eb + 3]);
            }
        }

        /// <summary>Gets the current entry's value.</summary>
        public readonly TValue CurrentValue => this.map.values![this.index];

        /// <summary>Advances to the next entry.</summary>
        /// <returns><see langword="true"/> if there was another entry.</returns>
        public bool MoveNext() => ++this.index < this.map.Count;
    }
}