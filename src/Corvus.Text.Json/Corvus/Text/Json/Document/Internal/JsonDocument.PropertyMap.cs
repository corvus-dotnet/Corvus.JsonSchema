// <copyright file="JsonDocument.PropertyMap.cs" company="Endjin Limited">
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

public abstract partial class JsonDocument
{
    /// <summary>
    /// Represents a property map structure for efficient property lookup in JSON objects.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyMap
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
        /// The offset into the buckets buffer where our buckets start.
        /// </summary>
        public int BucketOffset; // The offset into the buckets buffer where our buckets start.

        /// <summary>
        /// The offset into the entries buffer where our entries start.
        /// </summary>
        public int EntryOffset; // The offset into the entries buffer where our entries start.

        /// <summary>
        /// The length of the end token.
        /// </summary>
        public int LengthOfEndToken; // The length of the end token

        /// <summary>
        /// The offset of the length of the end token in the property map.
        /// </summary>
        internal const int LengthOfEndTokenOffset = 16; // The offset of the length of the end token in the property map

        /// <summary>
        /// The size in bytes of a PropertyMap structure.
        /// </summary>
        internal const int Size = 20;

#if DEBUG

        static unsafe PropertyMap()
        {
            Debug.Assert(sizeof(PropertyMap) == Size, "Size");
        }

#endif

        /// <summary>
        /// Writes a PropertyMap structure to the specified destination span.
        /// </summary>
        /// <param name="bucketOffset">The offset into the buckets buffer.</param>
        /// <param name="entryOffset">The offset into the entries buffer.</param>
        /// <param name="bucketCount">The number of buckets.</param>
        /// <param name="count">The number of entries.</param>
        /// <param name="destination">The destination span to write to.</param>
        /// <param name="lengthOfEndToken">The length of the end token.</param>
        internal static void Write(int bucketOffset, int entryOffset, int bucketCount, int count, Span<byte> destination, int lengthOfEndToken)
        {
            var propertyMap = new PropertyMap() { BucketCount = bucketCount, Count = count, BucketOffset = bucketOffset, EntryOffset = entryOffset, LengthOfEndToken = lengthOfEndToken };
            MemoryMarshal.Write(destination, ref propertyMap);
        }

        /// <summary>
        /// Gets the length of the end token from the property map.
        /// </summary>
        /// <param name="propertyMap">The property map span to read from.</param>
        /// <returns>The length of the end token.</returns>
        internal static int GetLengthOfEndToken(Span<byte> propertyMap)
        {
            return MemoryMarshal.Read<int>(propertyMap.Slice(LengthOfEndTokenOffset));
        }

        /// <summary>
        /// Calculates a hash code for the specified byte span key.
        /// </summary>
        /// <param name="key">The key to calculate the hash code for.</param>
        /// <returns>The calculated hash code.</returns>
        internal static ulong GetHashCode(in ReadOnlySpan<byte> key)
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

        /// <summary>
        /// Gets a reference to the bucket for the specified hash code and size.
        /// </summary>
        /// <param name="buckets">The buckets span.</param>
        /// <param name="hashCode">The hash code to find the bucket for.</param>
        /// <param name="size">The size of the bucket array.</param>
        /// <returns>A reference to the appropriate bucket.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref int GetBucket(Span<int> buckets, ulong hashCode, int size)
        {
            return ref buckets[(int)(hashCode % (ulong)size)];
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
            public const int Size = 24;

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
            public ulong HashCode;

            /// <summary>
            /// The key offset for dynamic unescaped keys. Top bit indicates if the value is present.
            /// </summary>
            private int keyOffsetForDynamicUnescapedKey; // Top bit is 1 if the value, and the rest == offset into the key buffer if the name is escaped; otherwise all 0

#if DEBUG

            static unsafe Entry()
            {
                Debug.Assert(sizeof(Entry) == Size, "Size");
            }

#endif

            /// <summary>
            /// Gets a value indicating whether this entry has a dynamic unescaped key.
            /// </summary>
            public readonly bool HasDynamicUnescapedKey => keyOffsetForDynamicUnescapedKey >= 0;

            /// <summary>
            /// Gets the key offset for this entry.
            /// </summary>
            public readonly int KeyOffset => keyOffsetForDynamicUnescapedKey & int.MaxValue;

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
                ulong hashCode,
                int next,
                int valueIndex)
            {
                var entry = new Entry
                {
                    HashCode = hashCode,
                    Next = next,
                    ValueIndex = valueIndex,
                    keyOffsetForDynamicUnescapedKey = -1,
                };

                MemoryMarshal.Write(destination, ref entry);
            }

            /// <summary>
            /// This write is used when the entry requires unescaping
            /// in which case the keyOffset is the offset into the
            /// dynamic value buffer.
            /// </summary>
            /// <param name="destination">The destination span to write to.</param>
            /// <param name="hashCode">The hash code of the entry.</param>
            /// <param name="next">The next index in the bucket.</param>
            /// <param name="valueIndex">The value index in the source meta db.</param>
            /// <param name="keyOffset">The offset of the key name in the dynamic value backing in the workspace.</param>
            public static void Write(
                Span<byte> destination,
                ulong hashCode,
                int next,
                int valueIndex,
                int keyOffset)
            {
                var entry = new Entry
                {
                    HashCode = hashCode,
                    Next = next,
                    ValueIndex = valueIndex,
                    keyOffsetForDynamicUnescapedKey = keyOffset,
                };

                MemoryMarshal.Write(destination, ref entry);
            }
        }
    }

    /// <summary>
    /// Ensures that a property map exists for the JSON object at the specified index.
    /// </summary>
    /// <param name="index">The index of the JSON object start token.</param>
    protected void EnsurePropertyMapUnsafe(int index)
    {
        DbRow row = _parsedData.Get(index);
        Debug.Assert(row.TokenType == JsonTokenType.StartObject);
        int endIndex = index + GetDbSizeUnsafe(index, false);
        row = _parsedData.Get(endIndex);

        if (!row.HasPropertyMap)
        {
            int propertyMapIndex = CreatePropertyMap(index, endIndex);
            _parsedData.SetPropertyMapIndex(endIndex, propertyMapIndex);
        }
    }

    /// <summary>
    /// Creates a property map for efficient property lookup in a JSON object.
    /// </summary>
    /// <param name="startObjectIndex">The index of the start object token.</param>
    /// <param name="endIndex">The index of the end object token.</param>
    /// <returns>The index of the created property map in the property map buffer.</returns>
    /// <remarks>
    /// This method creates a hash-based property map for fast property lookups in JSON objects.
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
    private int CreatePropertyMap(int startObjectIndex, int endIndex)
    {
        DbRow startObjectRow = _parsedData.Get(startObjectIndex);
        DbRow endObjectRow = _parsedData.Get(endIndex);

        int lengthOfEnd = endObjectRow.SizeOrLengthOrPropertyMapIndex;
        int propertyCount = startObjectRow.SizeOrLengthOrPropertyMapIndex;
        int size = HashHelpers.GetPrime(propertyCount);
        int entriesSize = size * PropertyMap.Entry.Size;

        // Make sure we have space for the buckets
        if (_bucketsBacking is null)
        {
            _bucketsBacking = ArrayPool<int>.Shared.Rent(size);
        }
        else
        {
            JsonDocument.Enlarge(_bucketOffset + size, ref _bucketsBacking);
        }

        // Make sure we have space for the property map
        if (_propertyMapBacking is null)
        {
            // We will start with 10
            _propertyMapBacking = ArrayPool<byte>.Shared.Rent(PropertyMap.Size * 10);
        }
        else
        {
            Enlarge(_propertyMapOffset + PropertyMap.Size, ref _propertyMapBacking);
        }

        // Make sure we have space for the entries
        if (_entriesBacking is null)
        {
            _entriesBacking = ArrayPool<byte>.Shared.Rent(entriesSize);
        }
        else
        {
            Enlarge(_entryOffset + entriesSize, ref _entriesBacking);
        }

        Span<int> buckets = _bucketsBacking.AsSpan(_bucketOffset, size);
        Span<byte> entries = _entriesBacking.AsSpan(_entryOffset, entriesSize);
        buckets.Clear();
        entries.Clear();

        Span<byte> buffer = stackalloc byte[JsonConstants.StackallocByteThreshold];

        int propertyIndex = 0;

        int index = startObjectIndex + DbRow.Size;

        while (index < endIndex)
        {
            DbRow propertyRow = _parsedData.Get(index);
            int valueIndex = index + DbRow.Size;
            DbRow row = _parsedData.Get(valueIndex);
            Debug.Assert(propertyRow.TokenType == JsonTokenType.PropertyName, "The row must be a property name");

            if (propertyRow.HasComplexChildren)
            {
                Debug.Assert(propertyRow.LocationOrIndex >= 0, "The property must be local if it has complex children");

                ReadOnlyMemory<byte> rawName = GetRawSimpleValueUnsafe(index, false);
                ReadOnlySpan<byte> unescapedName = UnescapeAndStoreUnescapedStringValue(rawName.Span, out int dynamicValueOffset);
                ulong hashCode = PropertyMap.GetHashCode(unescapedName);
                ref int bucket = ref PropertyMap.GetBucket(buckets, hashCode, size);
                int entryIndex = propertyIndex * PropertyMap.Entry.Size;
                PropertyMap.Entry.Write(entries.Slice(entryIndex, PropertyMap.Entry.Size), hashCode, bucket - 1, valueIndex, dynamicValueOffset);
                propertyIndex++;
                bucket = propertyIndex; // Value in buckets is 1-based
            }
            else
            {
                ReadOnlyMemory<byte> rawName = GetRawSimpleValueUnsafe(index, false);
                ulong hashCode = PropertyMap.GetHashCode(rawName.Span);
                ref int bucket = ref PropertyMap.GetBucket(buckets, hashCode, size);
                int entryIndex = propertyIndex * PropertyMap.Entry.Size;
                PropertyMap.Entry.Write(entries.Slice(entryIndex, PropertyMap.Entry.Size), hashCode, bucket - 1, valueIndex);
                propertyIndex++;
                bucket = propertyIndex; // Value in buckets is 1-based
            }

            if (row.IsSimpleValue)
            {
                index = valueIndex + DbRow.Size;
            }
            else
            {
                int length = GetDbSizeUnsafe(valueIndex, true);
                Debug.Assert(length > 0, "There must be at least one row in a non-simple value.");
                index = valueIndex + length;
            }
        }

        PropertyMap.Write(_bucketOffset, _entryOffset, size, propertyCount, _propertyMapBacking.AsSpan(_propertyMapOffset), lengthOfEnd);

        int propertyMapIndex = _propertyMapOffset;

        // Move the pointers for the next property map.
        _propertyMapOffset += PropertyMap.Size;
        _bucketOffset += size;
        _entryOffset += entriesSize;

        return propertyMapIndex;
    }

    /// <summary>
    /// Gets the length of the end token from the property map at the specified buffer index.
    /// </summary>
    /// <param name="propertyMapBufferIndex">The index of the property map in the property map buffer.</param>
    /// <returns>The length of the end token.</returns>
    protected int GetLengthOfEndToken(int propertyMapBufferIndex)
    {
        return PropertyMap.GetLengthOfEndToken(_propertyMapBacking.AsSpan(propertyMapBufferIndex, PropertyMap.Size));
    }

    /// <summary>
    /// Attempts to find a named property value in the property map using efficient hash-based lookup.
    /// </summary>
    /// <param name="propertyMapBufferIndex">The index of the property map in the property map buffer.</param>
    /// <param name="unescapedUtf8Name">The unescaped UTF-8 property name to search for.</param>
    /// <param name="valueIndex">When this method returns, contains the index of the property value if found; otherwise, -1.</param>
    /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method implements an efficient hash table lookup algorithm for property names in JSON objects.
    /// The lookup process follows these steps:
    ///
    /// 1. **Property Map Loading**: Reads the PropertyMap structure from the backing buffer to get metadata
    /// including bucket and entry offsets, counts, and sizes.
    ///
    /// 2. **Hash Calculation**: Computes a hash code for the target property name using an optimized
    /// algorithm that varies based on the property name length for maximum distribution.
    ///
    /// 3. **Bucket Selection**: Uses modulo operation to map the hash code to a specific bucket
    /// in the hash table, providing O(1) initial access.
    ///
    /// 4. **Chain Traversal**: Follows the linked chain of entries in the selected bucket:
    /// - Bucket values are 1-based, so the initial index is decremented
    /// - Each entry contains a Next field pointing to the next entry in the collision chain
    /// - Bounds checking prevents array access violations
    ///
    /// 5. **Hash and Key Comparison**: For each entry in the chain:
    /// - First compares hash codes for fast rejection of non-matches
    /// - For hash matches, performs optimized key comparison:
    /// * Short keys (&lt; HashLength) with no hash collision bits can skip full comparison
    /// * Otherwise, retrieves the actual property name and performs byte-wise comparison
    ///
    /// 6. **Key Retrieval**: Property names are retrieved differently based on storage:
    /// - Simple properties: Read directly from the original JSON data
    /// - Escaped properties: Read from the dynamic value buffer after unescaping
    ///
    /// 7. **Collision Handling**: The algorithm includes safeguards against infinite loops
    /// by tracking collision count and ensuring it doesn't exceed the total entry count.
    ///
    /// This implementation provides O(1) average-case lookup performance with graceful handling
    /// of hash collisions through separate chaining, while minimizing memory allocations and
    /// cache misses through efficient data layout.
    /// </remarks>
    protected bool TryGetNamedPropertyValueFromPropertyMap(int propertyMapBufferIndex, ReadOnlySpan<byte> unescapedUtf8Name, out int valueIndex)
    {
        PropertyMap propertyMap = MemoryMarshal.Read<PropertyMap>(_propertyMapBacking.AsSpan(propertyMapBufferIndex, PropertyMap.Size));
        Span<int> buckets = _bucketsBacking.AsSpan(propertyMap.BucketOffset, propertyMap.BucketCount);
        Span<byte> entries = _entriesBacking.AsSpan(propertyMap.EntryOffset, propertyMap.Count * PropertyMap.Entry.Size);

        ulong hashCode = PropertyMap.GetHashCode(unescapedUtf8Name);
        int i = PropertyMap.GetBucket(buckets, hashCode, propertyMap.BucketCount);
        uint collisionCount = 0;
        PropertyMap.Entry entry;

        i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.
        do
        {
            int offset = i * PropertyMap.Entry.Size;

            // Test in if to drop range check for following array access
            if ((uint)offset >= (uint)entries.Length)
            {
                goto ReturnNotFound;
            }

            entry = MemoryMarshal.Read<PropertyMap.Entry>(entries.Slice(offset));
            if (entry.HashCode == hashCode &&
                    (((unescapedUtf8Name.Length < HashLength) &&
                        ((hashCode & HashMask) == 0)) ||
                    GetKey(ref entry).SequenceEqual(unescapedUtf8Name)))
            {
                goto ReturnFound;
            }

            i = entry.Next;

            collisionCount++;
        }
        while (collisionCount <= propertyMap.Count);

        Debug.Fail("Possible infinite loop in JsonItemIndexHashSet.FindValue.");

    ReturnFound:
        valueIndex = entry.ValueIndex;
        return true;
    ReturnNotFound:
        valueIndex = -1;
        return false;
    }

    /// <summary>
    /// Gets the key (property name) for the specified property map entry.
    /// </summary>
    /// <param name="entry">The property map entry to get the key for.</param>
    /// <returns>The property name as a read-only byte span.</returns>
    private ReadOnlySpan<byte> GetKey(ref PropertyMap.Entry entry)
    {
        if (entry.HasDynamicUnescapedKey)
        {
            return ReadDynamicUnescapedUtf8String(entry.KeyOffset).Span;
        }
        else
        {
            return GetRawSimpleValueUnsafe(entry.ValueIndex - DbRow.Size, false).Span;
        }
    }
}