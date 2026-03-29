// <copyright file="UniqueItemsHashSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

#pragma warning disable CS9191 // This is the warning about in/ref params; we disable it because we target netstandard2.0

/// <summary>
/// A map that can be built
/// </summary>
/// <remarks>
/// This class uses a hash-based approach to enable O(1) average-case lookups of property matchers based on property names, while minimizing memory usage through array pooling and efficient data layout. The implementation includes a custom hash function, separate chaining for collision resolution, and optimized key comparison strategies to ensure fast lookups even in the presence of hash collisions.
/// </remarks>
public ref struct UniqueItemsHashSet
#if NET
    : IDisposable
#endif
{
    /// <summary>
    /// Represents a property map structure for efficient property lookup in JSON objects.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JsonItemIndexHashSet
    {
        /// <summary>
        /// The number of buckets in the bucket set.
        /// </summary>
        public int BucketCount; // The BucketCount of our bucket set.

        /// <summary>
        /// The number of entries in the map.
        /// </summary>
        public int Count; // The number of entries in the map.

        /// <summary>
        /// The size in bytes of the structure.
        /// </summary>
        internal const int Size = 8;

#if DEBUG

        static unsafe JsonItemIndexHashSet()
        {
            Debug.Assert(sizeof(JsonItemIndexHashSet) == Size, "Size");
        }

#endif

        /// <summary>
        /// Writes a PropertyMap structure to the specified destination span.
        /// </summary>
        /// <param name="bucketCount">The number of buckets.</param>
        /// <param name="count">The number of entries.</param>
        internal static JsonItemIndexHashSet Create(int bucketCount)
        {
            return new JsonItemIndexHashSet() { BucketCount = bucketCount, Count = 0 };
        }

        /// <summary>
        /// Gets a reference to the bucket for the specified hash code and size.
        /// </summary>
        /// <param name="buckets">The buckets span.</param>
        /// <param name="hashCode">The hash code to find the bucket for.</param>
        /// <param name="size">The size of the bucket array.</param>
        /// <returns>A reference to the appropriate bucket.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref int GetBucket(Span<int> buckets, int hashCode, int size)
        {
            return ref buckets[(int)((uint)hashCode % (uint)size)];
        }

        /// <summary>
        /// Represents an entry in the property map containing hash information and key/value indices.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Entry
        {
            /// <summary>
            /// The size in bytes of an Entry structure.
            /// </summary>
            public const int Size = 12;

            /// <summary>
            /// The index of the next entry in the chain.
            /// </summary>
            public int Next;

            /// <summary>
            /// The index of the value for this entry.
            /// </summary>
            public int ValueIndex;

            /// <summary>
            /// The hash code for this entry.
            /// </summary>
            public int HashCode;

#if DEBUG

            static unsafe Entry()
            {
                Debug.Assert(sizeof(Entry) == Size, "Size");
            }

#endif

            /// <summary>
            /// This write is used when the entry does not require unescaping
            /// and the key is in the raw JSON data (the most common case).
            /// </summary>
            /// <param name="destination">The destination span to write to.</param>
            /// <param name="hashCode">The hash code of the entry.</param>
            /// <param name="next">The next index in the bucket.</param>
            /// <param name="valueIndex">The value index in the source meta db.</param>
            public static void Write(
                Span<byte> destination,
                int hashCode,
                int next,
                int valueIndex)
            {
                var entry = new Entry
                {
                    HashCode = hashCode,
                    Next = next,
                    ValueIndex = valueIndex,
                };

                MemoryMarshal.Write(destination, ref entry);
            }
        }
    }

    /// <summary>
    /// Backing for the property map data.
    /// </summary>
    private JsonItemIndexHashSet _propertyMap;

    /// <summary>
    /// Backing array for the hash buckets used in property lookups.
    /// </summary>
    private int[]? _bucketsBacking;

    private Span<int> _buckets;

    /// <summary>
    /// Backing array for the hash table entries used in property lookups.
    /// </summary>
    private byte[]? _entriesBacking;

    private Span<byte> _entries;

    private int _propertyIndex = 0;

    private IJsonDocument _parentDocument;

    private int _size;

    /// <summary>
    /// The recommended size for a stack allocated bucket buffer.
    /// </summary>
    public const int StackAllocBucketSize = 256;

    /// <summary>
    /// The recommended size for a stack allocated entries buffer.
    /// </summary>
    public const int StackAllocEntrySize = 1024;

    /// <summary>
    /// A delegate that provides the unescaped name for a property.
    /// </summary>
    /// <returns></returns>
    public delegate ReadOnlySpan<byte> UnescapedNameProvider();

    /// <summary>
    /// Creates a validator map for efficient property lookup based on the provided matchers.
    /// </summary>
    /// <param name="parentDocument">The parent document for the unique items map.</param>
    /// <param name="itemsCount">The number of items to be added to the map.</param>
    /// <param name="entries">A working buffer for the buckets.</param>
    /// <param name="entries">A working buffer for the entries.</param>
    [CLSCompliant(false)]
    public UniqueItemsHashSet(IJsonDocument parentDocument, int itemsCount, Span<int> buckets, Span<byte> entries)
    {
        _parentDocument = parentDocument;
        CreateMap(itemsCount, buckets, entries);
    }

    /// <summary>
    /// Creates a property map for efficient property lookup in a JSON object.
    /// </summary>
    /// <param name="startObjectIndex">The index of the start object token.</param>
    /// <param name="endIndex">The index of the end object token.</param>
    /// <remarks>
    /// This method creates a hash-based property map for fast property matcher lookups
    /// The process involves several steps:
    ///
    /// 1. **Initialization**: Calculates the number of properties and determines an optimal hash table size using prime numbers.
    ///
    /// 2. **Memory Allocation**: Ensures sufficient space in three backing arrays:
    /// - Buckets array: Contains hash bucket indices for the hash table
    /// - Property map array: Stores the PropertyMap structure metadata
    /// - Entries array: Contains the actual hash table entries with property information
    ///
    /// 3. **Property Processing**: Iterates through all properties in the JSON object:
    /// - For properties with complex children (requiring unescaping): Unescapes the property name,
    /// stores it in the dynamic value buffer, and creates an entry with the dynamic offset
    /// - For simple properties: Uses the raw property name directly from the JSON data
    /// - Calculates hash codes and manages hash collisions using chaining
    ///
    /// 4. **Hash Table Construction**: Uses separate chaining for collision resolution where:
    /// - Each bucket contains a 1-based index to the first entry in the chain
    /// - Entries link to the next entry in the chain via the Next field
    /// - Hash codes are computed using an optimized algorithm based on property name length
    ///
    /// 5. **Finalization**: Writes the PropertyMap header structure and updates offset pointers
    /// for future property map allocations.
    ///
    /// The resulting property map enables O(1) average-case property lookups with efficient
    /// memory usage and minimal allocations through array pooling.
    /// </remarks>
    private void CreateMap(int itemCount, Span<int> buckets, Span<byte> entries)
    {
        _size = HashHelpers.GetPrime(itemCount);
        int entriesSize = _size * JsonItemIndexHashSet.Entry.Size;

        if (itemCount > buckets.Length)
        {
            // Make sure we have space for the buckets
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

        _buckets.Clear();
        _entries.Clear();

        _propertyMap = JsonItemIndexHashSet.Create(itemCount);
    }

    /// <summary>
    /// Adds the item identified by the parent document index to the map if it does not already exist,
    /// returning true if it was added and false if it already existed.
    /// </summary>
    /// <param name="parentDocumentIndex">The index of the value in the document.</param>
    /// <returns></returns>
    public bool AddItemIfNotExists(int parentDocumentIndex)
    {
        int hashCode = GetHashCode(parentDocumentIndex);

        ref int bucket = ref JsonItemIndexHashSet.GetBucket(_buckets, hashCode, _size);

        /* Find existing entry */
        uint collisionCount = 0;
        JsonItemIndexHashSet.Entry entry;
        int i = bucket - 1; // Value in _buckets is 1-based; subtract 1 from i
        do
        {
            int offset = i * JsonItemIndexHashSet.Entry.Size;

            // Test in if to drop range check for following array access
            if ((uint)offset >= (uint)_entries.Length)
            {
                goto ReturnNotFound;
            }

            entry = MemoryMarshal.Read<JsonItemIndexHashSet.Entry>(_entries.Slice(offset));
            if (entry.HashCode == hashCode && ValueEquals(entry.ValueIndex, parentDocumentIndex))
            {
                goto ReturnFound;
            }

            i = entry.Next;

            collisionCount++;
        }
        while (collisionCount <= _propertyMap.Count);

        Debug.Fail("Possible infinite loop in JsonItemIndexHashSet.FindValue.");

    ReturnFound:
        return false;
    ReturnNotFound:

        // Wasn't found, so add it and return
        int entryIndex = _propertyIndex * JsonItemIndexHashSet.Entry.Size;
        JsonItemIndexHashSet.Entry.Write(_entries.Slice(entryIndex, JsonItemIndexHashSet.Entry.Size), hashCode, bucket - 1, parentDocumentIndex);
        _propertyIndex++;
        _propertyMap.Count++;
        bucket = _propertyIndex; // Value in buckets is 1-based
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetHashCode(int documentIndex)
    {
        return _parentDocument.GetHashCode(documentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ValueEquals(int leftIndex, int rightIndex)
    {
        return JsonElementHelpers.DeepEqualsNoParentDocumentCheck(_parentDocument, leftIndex, _parentDocument, rightIndex);
    }

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
    }
}