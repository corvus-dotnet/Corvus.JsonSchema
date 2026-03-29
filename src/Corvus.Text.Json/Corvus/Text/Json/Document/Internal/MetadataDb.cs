// <copyright file="MetadataDb.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

// We need to target netstandard2.0, so keep using ref for MemoryMarshal.Write
// CS9191: The 'ref' modifier for argument 2 corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
#pragma warning disable CS9191

namespace Corvus.Text.Json.Internal;

// The database for the parsed structure of a JSON document.
// Every token from the document gets a row, which has one of the following forms:
// Number
// * First int
// * Top bit is 0 if this is the local token offset, 1 if it is an external document
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is set if the number uses scientific notation
// * 31 bits for the token length
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the index of the workspace document in the workspace for this row
// String, PropertyName
// * First int
// * Top bit is 0 if this is the local token offset, 1 if it is an external document
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is set if the string requires unescaping
// * 31 bits for the token length
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the index of the workspace document in the workspace for this row
// Other value types (True, False, Null)
// * First int
// * Top bit is 0 if this is the local token offset, 1 if it is an external document
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is unassigned / always clear
// * 31 bits for the token length
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the index of the workspace document in the workspace for this row
// EndObject
// * First int
// * Top bit is unassigned / always clear
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is 1 if this object has a property map, otherwise 0
// * 31 bits - index into the property map buffer if this has a property map backing, otherwise the length of the token
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the number of rows until the previous value (never 0)
// EndArray
// * First int
// * Top bit is unassigned / always clear
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Unassigned / always clear
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the number of rows until the previous value (never 0)
// StartObject
// * First int
// * Top bit is 0 if this is the local token offset, 1 if it is an external document
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is unassigned / always clear
// * 31 bits for the number of properties in this object
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the number of rows until the next value (never 0) if this is a local value,
// or the index of the workspace document in the workspace for this row if this is an external value.
// StartArray
// * First int
// * Top bit is 0 if this is the local token offset, 1 if it is an external document
// * 31 bits for token offset if the top bit is zero, or the external document index if
// the top bit is 1
// * Second int
// * Top bit is set if the array contains other arrays or objects ("complex" types)
// * 31 bits for the number of elements in this array
// * Third int
// * 4 bits JsonTokenType
// * 28 bits for the number of rows until the next value (never 0) if this is a local value,
// or the index of the workspace document in the workspace for this row if this is an external value.

/// <summary>
/// Database storing metadata for parsed JSON document structure, including token information
/// and structural relationships between JSON elements.
/// </summary>
public struct MetadataDb : IDisposable
{
    private const int SizeOrLengthOffset = 4;

    private const int NumberOfRowsOffset = 8;

    internal int Length { get; private set; }

    private byte[] _data;

    private bool _convertToAlloc; // Convert the rented data to an alloc when complete.
    private bool _isLocked; // Is the array the correct fixed size.

    // _isLocked _convertToAlloc truth table:
    // false     false  Standard flow. Size is not known and renting used throughout lifetime.
    // true      false  Used by JsonElement.ParseValue() for primitives and JsonDocument.Clone(). Size is known and no renting.
    // false     true   Used by JsonElement.ParseValue() for arrays and objects. Renting used until size is known.
    // true      true   not valid
    private MetadataDb(byte[] initialDb, bool isLocked, bool convertToAlloc, int length = 0)
    {
        _data = initialDb;
        _isLocked = isLocked;
        _convertToAlloc = convertToAlloc;
        Length = length;
    }

    internal MetadataDb(byte[] completeDb)
    {
        _data = completeDb;
        _isLocked = true;
        _convertToAlloc = false;
        Length = completeDb.Length;
    }

    /// <summary>
    /// Gets a value indicating whether the metadata database has been initialized with data.
    /// </summary>
    // If the instance is "default", _data can be null
    internal bool IsInitialized => _data is not null;

    /// <summary>
    /// Creates a metadata database using rented array pool memory with the specified data and length.
    /// </summary>
    /// <param name="data">The rented byte array to use as backing storage.</param>
    /// <param name="length">The initial length of data in the array.</param>
    /// <param name="convertToAlloc">Whether to convert to allocated memory when complete.</param>
    /// <returns>A new MetadataDb instance using the provided rented memory.</returns>
    internal static MetadataDb CreateRented(byte[] data, int length, bool convertToAlloc)
    {
        return new MetadataDb(data, isLocked: false, convertToAlloc, length);
    }

    /// <summary>
    /// Creates a metadata database using rented array pool memory with an estimated initial size.
    /// </summary>
    /// <param name="payloadLength">The length of the JSON payload to estimate buffer size.</param>
    /// <param name="convertToAlloc">Whether to convert to allocated memory when complete.</param>
    /// <returns>A new MetadataDb instance using rented memory.</returns>
    internal static MetadataDb CreateRented(int payloadLength, bool convertToAlloc)
    {
        int initialSize = CalculateInitialSize(payloadLength);

        byte[] data = ArrayPool<byte>.Shared.Rent(initialSize);
        return new MetadataDb(data, isLocked: false, convertToAlloc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateInitialSize(int payloadLength)
    {
        // Assume that a token happens approximately every 12 bytes.
        // int estimatedTokens = payloadLength / 12
        // now acknowledge that the number of bytes we need per token is 12.
        // So that's just the payload length.
        // Add one row worth of data since we need at least one row for a primitive type.
        int initialSize = payloadLength + DbRow.Size;

        // Stick with ArrayPool's rent/return range if it looks feasible.
        // If it's wrong, we'll just grow and copy as we would if the tokens
        // were more frequent anyways.
        const int OneMegabyte = 1024 * 1024;

        // Use unsigned comparison for efficient size range check
        if ((uint)initialSize > (uint)OneMegabyte && (uint)initialSize <= (uint)(4 * OneMegabyte))
        {
            initialSize = OneMegabyte;
        }

        return initialSize;
    }

    /// <summary>
    /// Creates a metadata database with a fixed-size allocated array.
    /// </summary>
    /// <param name="payloadLength">The length of the JSON payload to size the buffer.</param>
    /// <param name="convertToAlloc">Whether to convert to allocated memory when complete.</param>
    /// <returns>A new MetadataDb instance with locked allocated memory.</returns>
    internal static MetadataDb CreateLocked(int payloadLength, bool convertToAlloc = false)
    {
        // Add one row worth of data since we need at least one row for a primitive type.
        int size = payloadLength + DbRow.Size;

        byte[] data = new byte[size];
        return new MetadataDb(data, isLocked: true, convertToAlloc: convertToAlloc);
    }

    /// <summary>
    /// Releases resources used by the metadata database, returning rented arrays to the pool.
    /// </summary>
    public void Dispose()
    {
        byte[]? data = Interlocked.Exchange(ref _data, null!);
        if (data == null)
        {
            return;
        }

        Debug.Assert(!_isLocked, "Dispose called on a locked database");

        // The data in this rented buffer only conveys the positions and
        // lengths of tokens in a document, but no content; so it does not
        // need to be cleared.
        ArrayPool<byte>.Shared.Return(data);
        Length = 0;
    }

    /// <summary>
    /// Completes allocations by either converting rented memory to allocated memory or trimming excess capacity.
    /// </summary>
    /// <remarks>
    /// If using array pools, trims excess if necessary. If not using array pools, releases the temporary array pool and allocates.
    /// </remarks>
    internal void CompleteAllocations()
    {
        if (!_isLocked)
        {
            if (_convertToAlloc)
            {
                Debug.Assert(_data != null);
                byte[] returnBuf = _data;
                _data = _data.AsSpan(0, Length).ToArray();
                _isLocked = true;
                _convertToAlloc = false;

                // The data in this rented buffer only conveys the positions and
                // lengths of tokens in a document, but no content; so it does not
                // need to be cleared.
                ArrayPool<byte>.Shared.Return(returnBuf);
            }
            else
            {
                // There's a chance that the size we have is the size we'd get for this
                // amount of usage (particularly if Enlarge ever got called); and there's
                // the small copy-cost associated with trimming anyways. "Is half-empty" is
                // just a rough metric for "is trimming worth it?".
                if (Length <= (_data.Length / 2))
                {
                    byte[] newRent = ArrayPool<byte>.Shared.Rent(Length);
                    byte[] returnBuf = newRent;

                    if (newRent.Length < _data.Length)
                    {
                        Buffer.BlockCopy(_data, 0, newRent, 0, Length);
                        returnBuf = _data;
                        _data = newRent;
                    }

                    // The data in this rented buffer only conveys the positions and
                    // lengths of tokens in a document, but no content; so it does not
                    // need to be cleared.
                    ArrayPool<byte>.Shared.Return(returnBuf);
                }
            }
        }
    }

    /// <summary>
    /// Appends a new token entry to the metadata database.
    /// </summary>
    /// <param name="tokenType">The JSON token type.</param>
    /// <param name="startLocation">The start location of the token in the source.</param>
    /// <param name="length">The length of the token, or -1 for containers.</param>
    internal void Append(JsonTokenType tokenType, int startLocation, int length)
    {
        // StartArray or StartObject should have length -1, otherwise the length should not be -1.
        Debug.Assert(
            (tokenType == JsonTokenType.StartArray || tokenType == JsonTokenType.StartObject) ==
            (length == DbRow.UnknownSize));

        if (Length >= (_data.Length - DbRow.Size))
        {
            Enlarge();
        }

        var row = new DbRow(tokenType, startLocation, length);
        MemoryMarshal.Write(_data.AsSpan(Length), ref row);
        Length += DbRow.Size;
    }

    /// <summary>
    /// Appends a dynamic simple value (string, number, boolean, null) to the metadata database.
    /// </summary>
    /// <param name="tokenType">The JSON token type (must be PropertyName or higher).</param>
    /// <param name="location">The location of the token in the source.</param>
    /// <param name="requiresUnescapingOrHasExponent">Whether the value requires unescaping or has an exponent.</param>
    internal void AppendDynamicSimpleValue(JsonTokenType tokenType, int location, bool requiresUnescapingOrHasExponent)
    {
        Debug.Assert(tokenType >= JsonTokenType.PropertyName);

        // Use unsigned comparison for efficient bounds checking
        if (Length >= (_data.Length - DbRow.Size))
        {
            Enlarge();
        }

        var row = new DbRow(tokenType, location, requiresUnescapingOrHasExponent ? -1 : 1);
        MemoryMarshal.Write(_data.AsSpan(Length), ref row);
        Length += DbRow.Size;
    }

    /// <summary>
    /// Replaces a range of rows in a complex object with new rows, updating parent object counts.
    /// </summary>
    /// <param name="parentDocument">The parent mutable JSON document.</param>
    /// <param name="complexObjectStartIndex">The start index of the complex object containing the rows.</param>
    /// <param name="startIndex">The start index of the range to replace.</param>
    /// <param name="endIndex">The end index of the range to replace.</param>
    /// <param name="memberCountToReplace">The number of members being replaced.</param>
    /// <param name="rowCountToInsert">The number of rows to insert.</param>
    /// <param name="memberCountToInsert">The number of members to insert.</param>
    internal void ReplaceRowsInComplexObject(IMutableJsonDocument parentDocument, int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, int rowCountToInsert, int memberCountToInsert)
    {
        // First, we need to figure out how many rows we are replacing.
        int rowCountToAddOrRemove = rowCountToInsert - ((endIndex - startIndex) / DbRow.Size);
        int memberCountToAddOrRemove = memberCountToInsert - memberCountToReplace;

        InsertOrRemoveRowsInComplexObject(parentDocument, complexObjectStartIndex, startIndex, endIndex, rowCountToAddOrRemove, memberCountToAddOrRemove);
    }

    /// <summary>
    /// Inserts new rows in a complex object, updating parent object counts.
    /// </summary>
    /// <param name="parentDocument">The parent mutable JSON document.</param>
    /// <param name="complexObjectStartIndex">The start index of the complex object containing the insertion point.</param>
    /// <param name="startIndex">The index where rows should be inserted.</param>
    /// <param name="rowCountToInsert">The number of rows to insert.</param>
    /// <param name="memberCountToInsert">The number of members to insert.</param>
    internal void InsertRowsInComplexObject(IMutableJsonDocument parentDocument, int complexObjectStartIndex, int startIndex, int rowCountToInsert, int memberCountToInsert)
    {
        InsertOrRemoveRowsInComplexObject(parentDocument, complexObjectStartIndex, startIndex, startIndex, rowCountToInsert, memberCountToInsert);
    }

    /// <summary>
    /// Inserts or removes rows in a complex object, updating all parent container counts and managing memory allocation.
    /// </summary>
    /// <param name="parentDocument">The parent mutable JSON document.</param>
    /// <param name="complexObjectStartIndex">The start index of the complex object containing the modification point.</param>
    /// <param name="startIndex">The start index of the range where rows are being inserted or removed.</param>
    /// <param name="endIndex">The end index of the range. Equal to startIndex for insertions, or the end of the removal range for deletions.</param>
    /// <param name="rowCountToInsert">The number of rows to insert (positive) or remove (negative).</param>
    /// <param name="memberCountToInsert">The number of members to insert (positive) or remove (negative).</param>
    /// <remarks>
    /// This method performs a complex operation to maintain the structural integrity of the JSON metadata database
    /// when rows are inserted or removed from within nested containers. The process involves several key steps:
    ///
    /// 1. **Parent Container Traversal**: Starting from the immediate parent container, traverses backwards through
    /// all containing objects and arrays to update their metadata. This ensures that parent containers maintain
    /// accurate counts of their contents.
    ///
    /// 2. **Count Updates**: For each containing structure encountered:
    /// - **StartObject**: Updates both row count and member count, then resets member count to 0 for outer containers
    /// - **StartArray**: Updates row count and member count, with special handling for complex children detection
    /// - **EndObject/EndArray**: Uses the end token to find the corresponding start token and continues traversal
    /// - **Other tokens**: Simply moves to the previous row without modification
    ///
    /// 3. **Memory Management**: Handles dynamic resizing of the backing array:
    /// - For insertions requiring more space: Rents a larger array from the pool, copies data with gaps, returns old array
    /// - For in-place operations: Uses efficient block copy operations to shift existing data
    /// - For removals: Compacts data by copying remaining elements over the removed section
    ///
    /// 4. **Data Integrity**: Maintains critical invariants:
    /// - Row counts in containers accurately reflect the number of child elements
    /// - Member counts distinguish between the logical item count and the actual row count
    /// - Complex children flags are properly set when arrays contain non-simple values
    /// - External document references are reset to local references when modified
    ///
    /// 5. **Performance Optimizations**:
    /// - Updates counts before memory operations to avoid cache invalidation during traversal
    /// - Uses block copy operations for efficient memory management
    /// - Leverages array pooling to minimize garbage collection pressure
    /// - Processes containers in reverse order to maintain data consistency during updates
    ///
    /// The method is designed to handle both simple insertions/deletions and complex scenarios involving
    /// nested containers, ensuring that the metadata database remains consistent and all parent containers
    /// maintain accurate structural information.
    /// </remarks>
    private void InsertOrRemoveRowsInComplexObject(IMutableJsonDocument parentDocument, int complexObjectStartIndex, int startIndex, int endIndex, int rowCountToInsert, int memberCountToInsert)
    {
        AssertValidIndex(startIndex);
        AssertValidIndex(complexObjectStartIndex);

        Debug.Assert(!_isLocked, "Appending to a locked database");
        Debug.Assert(GetJsonTokenType(complexObjectStartIndex) is JsonTokenType.StartArray or JsonTokenType.StartObject);

        // First, fix up the counts, then block copy
        // If we do it in that order, we can just step through the data "as is"
        // with existing offsets
        int currentIndex = complexObjectStartIndex;
        while (currentIndex >= 0)
        {
            JsonTokenType tokenType = GetJsonTokenType(currentIndex);
            switch (tokenType)
            {
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:

                    // Skip past the start object of this end object, and into the previous entry
                    currentIndex = GetStartIndex(currentIndex) - DbRow.Size;
                    break;

                case JsonTokenType.StartObject:

                    // This was not skipped by hitting an EndObject/Array,
                    // so it must be the start of a containing object/array
                    // which will need to have its row count updated
                    SetRowAndMemberCount(currentIndex, currentIndex + parentDocument.GetDbSize(currentIndex, false), rowCountToInsert, memberCountToInsert);

                    // No more members to insert once we move out of our object, just rows.
                    memberCountToInsert = 0;
                    currentIndex -= DbRow.Size;
                    break;

                case JsonTokenType.StartArray:

                    // This was not skipped by hitting an EndObject/Array,
                    // so it must be the start of a containing object/array
                    // which will need to have its row count updated
                    SetRowAndMemberCount(currentIndex, currentIndex + parentDocument.GetDbSize(currentIndex, false), rowCountToInsert, memberCountToInsert, isArray: true);

                    // No more members to insert once we move out of our array, just rows.
                    memberCountToInsert = 0;
                    currentIndex -= DbRow.Size;
                    break;

                default:
                    currentIndex -= DbRow.Size;
                    break;
            }
        }

        int lengthToInsert = DbRow.Size * rowCountToInsert;

        if (lengthToInsert == 0)
        {
            return;
        }

        // Use unsigned comparison for efficient capacity check
        if (lengthToInsert > (_data.Length - Length))
        {
            // We will need to reallocate
            byte[] toReturn = _data;

            // Allow the data to grow up to maximum possible capacity (~2G bytes) before encountering overflow.
            // Note: Array.MaxLength exists only on .NET 6 or greater,
            // so for the other versions value is hardcoded
            const int MaxArrayLength = 0x7FFFFFC7;
#if NET
            Debug.Assert(MaxArrayLength == Array.MaxLength);
#endif

            int newCapacity = toReturn.Length * 2;

            // Note that this check works even when newCapacity overflowed thanks to the (uint) cast
            if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;

            // If the maximum capacity has already been reached,
            // then set the new capacity to be larger than what is possible
            // so that ArrayPool.Rent throws an OutOfMemoryException for us.
            if (newCapacity == toReturn.Length) newCapacity = int.MaxValue;

            _data = ArrayPool<byte>.Shared.Rent(newCapacity);

            // Block copy up to index
            Buffer.BlockCopy(toReturn, 0, _data, 0, startIndex);

            // Then copy the rest of the data with the extra space
            Buffer.BlockCopy(toReturn, endIndex, _data, endIndex + lengthToInsert, Length - endIndex);

            // The data in this rented buffer only conveys the positions and
            // lengths of tokens in a document, but no content; so it does not
            // need to be cleared.
            ArrayPool<byte>.Shared.Return(toReturn);
        }
        else
        {
            // We don't need to reallocate, so just copy the data up
            // This is also the code path if lengthToInsert is negative. We will be
            // copying the data down.
            Buffer.BlockCopy(_data, endIndex, _data, endIndex + lengthToInsert, Length - endIndex);
        }

        Length += lengthToInsert;
    }

    private void SetRowAndMemberCount(int startIndex, int endIndex, int rowCountToInsertOrRemove, int memberCountToInsertOrRemove, bool isArray = false)
    {
        AssertValidIndex(startIndex);
        AssertValidIndex(endIndex);

        Span<byte> endNumberOfRowsUnionPos = _data.AsSpan(endIndex + NumberOfRowsOffset);
        uint endNumberOfRowsUnion = MemoryMarshal.Read<uint>(endNumberOfRowsUnionPos);

        // Start and end row count are the same value, so we only need to calculate this once
        int numberOfRows = (int)(endNumberOfRowsUnion & 0x0FFFFFFFU) + rowCountToInsertOrRemove;

        Debug.Assert(numberOfRows >= 0);
        Debug.Assert(numberOfRows <= 0x0FFFFFFF);

        // Now update the end row.
        uint endTokenType = endNumberOfRowsUnion & 0xF0000000U;
        uint updatedValue = endTokenType | unchecked((uint)numberOfRows);
        MemoryMarshal.Write(endNumberOfRowsUnionPos, ref updatedValue);

        // And we aren't in an external document
        Span<byte> target = _data.AsSpan(endIndex, sizeof(int));
        MemoryMarshal.Cast<byte, int>(target)[0] = 0;

        // Now we will do the start positions
        // We have reversed the order from the usual so we update end first, then start
        // because we need the info from the end tgo calculate the number of rows
        // and this should help avoid busting the cache so often
        // Set the token offset to 0 - this makes it a local item in the builder document
        // as we no longer directly apply the target backing
        target = _data.AsSpan(startIndex, sizeof(int));
        MemoryMarshal.Cast<byte, int>(target)[0] = 0;

        // Persist the most significant nybble and the new row count
        Span<byte> startNumberOfRowsUnionPos = _data.AsSpan(startIndex + NumberOfRowsOffset);
        uint startNumberOfRowsUnion = MemoryMarshal.Read<uint>(startNumberOfRowsUnionPos);
        uint startTokenType = startNumberOfRowsUnion & 0xF0000000U;
        updatedValue = startTokenType | unchecked((uint)numberOfRows);
        MemoryMarshal.Write(startNumberOfRowsUnionPos, ref updatedValue);

        // Now do the item counts and complex children for the start. We do this now to try and do
        // all the local updates first, to avoid busting the cache.
        Span<byte> startSizeOrLengthUnion = _data.AsSpan(startIndex + SizeOrLengthOffset);
        int currentLength = (int)(MemoryMarshal.Read<uint>(startSizeOrLengthUnion) & 0x7FFF_FFFFU);
        currentLength += memberCountToInsertOrRemove;

        if (isArray)
        {
            // If the array item count is (e.g.) 12 and the number of rows is (e.g.) 13
            // then the extra row is just the EndArray item, so the array was made up
            // of simple values.
            // If the off-by-one relationship does not hold, then one of the values was
            // more than one row, making it a complex object. This is indicated by setting
            // the top bit of currentLength (which, handily, is just negating the value).
            // The current length must be greater than zero, as we must have at least
            // one row for this condition to hold.
            if (currentLength + 1 != numberOfRows)
            {
                currentLength = (int)((uint)currentLength | 0x8000_0000U);
            }
        }

        MemoryMarshal.Write(startSizeOrLengthUnion, ref currentLength);
    }

    /// <summary>
    /// Appends an external token entry to the metadata database for tokens from external documents.
    /// </summary>
    /// <param name="tokenType">The JSON token type.</param>
    /// <param name="externalIndex">The index in the external document.</param>
    /// <param name="sizeOrLength">The size or length of the token.</param>
    /// <param name="workspaceDocumentIndexOrNumberOfRows">The workspace document index or number of rows.</param>
    internal void AppendExternal(JsonTokenType tokenType, int externalIndex, int sizeOrLength, int workspaceDocumentIndexOrNumberOfRows)
    {
        // Use unsigned comparison for efficient bounds checking
        if (Length >= (_data.Length - DbRow.Size))
        {
            Enlarge();
        }

        var row = new DbRow(tokenType, externalIndex, sizeOrLength, workspaceDocumentIndexOrNumberOfRows);
        MemoryMarshal.Write(_data.AsSpan(Length), ref row);
        Length += DbRow.Size;
    }

    /// <summary>
    /// Enlarges the internal data array by doubling its capacity when more space is needed.
    /// </summary>
    private void Enlarge()
    {
        Debug.Assert(!_isLocked, "Appending to a locked database");

        byte[] toReturn = _data;

        // Allow the data to grow up to maximum possible capacity (~2G bytes) before encountering overflow.
        // Note: Array.MaxLength exists only on .NET 6 or greater,
        // so for the other versions value is hardcoded
        const int MaxArrayLength = 0x7FFFFFC7;
#if NET
        Debug.Assert(MaxArrayLength == Array.MaxLength);
#endif

        int newCapacity = toReturn.Length * 2;

        // Note that this check works even when newCapacity overflowed thanks to the (uint) cast
        if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;

        // If the maximum capacity has already been reached,
        // then set the new capacity to be larger than what is possible
        // so that ArrayPool.Rent throws an OutOfMemoryException for us.
        if (newCapacity == toReturn.Length) newCapacity = int.MaxValue;

        _data = ArrayPool<byte>.Shared.Rent(newCapacity);
        Buffer.BlockCopy(toReturn, 0, _data, 0, toReturn.Length);

        // The data in this rented buffer only conveys the positions and
        // lengths of tokens in a document, but no content; so it does not
        // need to be cleared.
        ArrayPool<byte>.Shared.Return(toReturn);
    }

    [Conditional("DEBUG")]
    private void AssertValidIndex(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index <= Length - DbRow.Size, $"startIndex {index} is out of bounds");
        Debug.Assert(index % DbRow.Size == 0, $"startIndex {index} is not at a record start position");
    }

    /// <summary>
    /// Sets the length value for the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row.</param>
    /// <param name="length">The length value to set.</param>
    internal void SetLength(int index, int length)
    {
        AssertValidIndex(index);
        Debug.Assert(length >= 0);
        Span<byte> destination = _data.AsSpan(index + SizeOrLengthOffset);
        MemoryMarshal.Write(destination, ref length);
    }

    /// <summary>
    /// Sets the number of rows value for the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row.</param>
    /// <param name="numberOfRows">The number of rows value to set (must be between 1 and 0x0FFFFFFF).</param>
    internal void SetNumberOfRows(int index, int numberOfRows)
    {
        AssertValidIndex(index);
        Debug.Assert(numberOfRows >= 1 && numberOfRows <= 0x0FFFFFFF);

        Span<byte> dataPos = _data.AsSpan(index + NumberOfRowsOffset);
        int current = MemoryMarshal.Read<int>(dataPos);

        // Persist the most significant nybble
        int value = (current & unchecked((int)0xF0000000)) | numberOfRows;
        MemoryMarshal.Write(dataPos, ref value);
    }

    /// <summary>
    /// Sets the property map index for the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row.</param>
    /// <param name="propertyMapIndex">The property map index to set.</param>
    internal void SetPropertyMapIndex(int index, int propertyMapIndex)
    {
        AssertValidIndex(index);
        uint pmi = (uint)propertyMapIndex | 0x8000_0000U;

        Span<byte> destination = _data.AsSpan(index + SizeOrLengthOffset);

        MemoryMarshal.Write(destination, ref pmi);
    }

    /// <summary>
    /// Sets the HasComplexChildren flag for the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row.</param>
    internal void SetHasComplexChildren(int index)
    {
        AssertValidIndex(index);

        // The HasComplexChildren bit is the most significant bit of "SizeOrLength"
        Span<byte> dataPos = _data.AsSpan(index + SizeOrLengthOffset);
        int current = MemoryMarshal.Read<int>(dataPos);

        int value = current | unchecked((int)0x80000000);
        MemoryMarshal.Write(dataPos, ref value);
    }

    /// <summary>
    /// Removes rows from the database, copying up the remaining rows to fill the gap.
    /// </summary>
    /// <param name="startIndex">The start index at which to remove rows.</param>
    /// <param name="length">The number of rows to remove.</param>
    /// <remarks>
    /// Note that this does *not* modify any data in complex objects as a result
    /// of the row removal. It is intended
    /// for use during complex object build.
    /// </remarks>
    internal void RemoveRows(int startIndex, int length)
    {
        AssertValidIndex(startIndex);
        Debug.Assert(length > 0, "length must be greater than 0");

        int lengthToRemove = (length * DbRow.Size);
        Debug.Assert(startIndex + lengthToRemove <= Length, $"Length {lengthToRemove} is out of bounds of the array.");
        int sourceIndex = startIndex + lengthToRemove;

        // Use unsigned comparison for efficient positive check
        if ((uint)lengthToRemove > 0)
        {
            // We are removing rows, so we need to copy the data up
            // to fill the gap.
            Buffer.BlockCopy(_data, sourceIndex, _data, startIndex, Length - sourceIndex);
        }

        Length -= lengthToRemove;
    }

    /// <summary>
    /// Finds the index of the first unset size or length entry for the specified token type.
    /// </summary>
    /// <param name="lookupType">The token type to search for (must be StartObject or StartArray).</param>
    /// <returns>The index of the first unset entry, or -1 if not found.</returns>
    internal int FindIndexOfFirstUnsetSizeOrLength(JsonTokenType lookupType)
    {
        Debug.Assert(lookupType == JsonTokenType.StartObject || lookupType == JsonTokenType.StartArray);
        return FindOpenElement(lookupType);
    }

    /// <summary>
    /// Finds an open element of the specified token type by searching backwards through the database.
    /// </summary>
    /// <param name="lookupType">The token type to search for.</param>
    /// <returns>The index of the open element, or -1 if not found.</returns>
    internal int FindOpenElement(JsonTokenType lookupType)
    {
        Span<byte> data = _data.AsSpan(0, Length);

        for (int i = Length - DbRow.Size; i >= 0; i -= DbRow.Size)
        {
            DbRow row = MemoryMarshal.Read<DbRow>(data.Slice(i));

            if (row.IsUnknownSize && row.TokenType == lookupType)
            {
                return i;
            }
        }

        // We should never reach here.
        Debug.Fail($"Unable to find expected {lookupType} token");
        return -1;
    }

    /// <summary>
    /// Gets the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row to retrieve.</param>
    /// <returns>The DbRow at the specified index.</returns>
    internal DbRow Get(int index)
    {
        AssertValidIndex(index);
        return MemoryMarshal.Read<DbRow>(_data.AsSpan(index));
    }

    /// <summary>
    /// Gets the JSON token type for the database row at the specified index.
    /// </summary>
    /// <param name="index">The index of the database row.</param>
    /// <returns>The JsonTokenType for the row at the specified index.</returns>
    internal JsonTokenType GetJsonTokenType(int index)
    {
        AssertValidIndex(index);
        uint union = MemoryMarshal.Read<uint>(_data.AsSpan(index + NumberOfRowsOffset));

        return (JsonTokenType)(union >> 28);
    }

    /// <summary>
    /// Gets the start index of a container (object or array) given its end index.
    /// </summary>
    /// <param name="endIndex">The index of the EndObject or EndArray token.</param>
    /// <returns>The index of the corresponding StartObject or StartArray token.</returns>
    internal int GetStartIndex(int endIndex)
    {
        Debug.Assert(GetJsonTokenType(endIndex) is JsonTokenType.EndObject or JsonTokenType.EndArray);

        uint union = MemoryMarshal.Read<uint>(_data.AsSpan(endIndex + NumberOfRowsOffset));
        return endIndex - ((int)(union & 0x0FFFFFFFU) * DbRow.Size);
    }

    /// <summary>
    /// Creates a copy of a segment of the metadata database from startIndex to endIndex.
    /// </summary>
    /// <param name="startIndex">The starting index of the segment to copy.</param>
    /// <param name="endIndex">The ending index of the segment to copy.</param>
    /// <returns>A new MetadataDb containing the copied segment with adjusted location offsets.</returns>
    internal MetadataDb CopySegment(int startIndex, int endIndex)
    {
        Debug.Assert(
            endIndex > startIndex,
            $"endIndex={endIndex} was at or before startIndex={startIndex}");

        AssertValidIndex(startIndex);
        Debug.Assert(endIndex <= Length);

        DbRow start = Get(startIndex);
#if DEBUG
        DbRow end = Get(endIndex - DbRow.Size);

        if (start.TokenType == JsonTokenType.StartObject)
        {
            Debug.Assert(
                end.TokenType == JsonTokenType.EndObject,
                $"StartObject paired with {end.TokenType}");
        }
        else if (start.TokenType == JsonTokenType.StartArray)
        {
            Debug.Assert(
                end.TokenType == JsonTokenType.EndArray,
                $"StartArray paired with {end.TokenType}");
        }
        else
        {
            Debug.Assert(
                startIndex + DbRow.Size == endIndex,
                $"{start.TokenType} should have been one row");
        }
#endif

        int length = endIndex - startIndex;

        byte[] newDatabase = new byte[length];
        _data.AsSpan(startIndex, length).CopyTo(newDatabase);

        Span<int> newDbInts = MemoryMarshal.Cast<byte, int>(newDatabase.AsSpan());
        int locationOffset = newDbInts[0];

        // Need to nudge one forward to account for the hidden quote on the string.
        if (start.TokenType == JsonTokenType.String)
        {
            locationOffset--;
        }

        for (int i = (length - DbRow.Size) / sizeof(int); i >= 0; i -= DbRow.Size / sizeof(int))
        {
            Debug.Assert(newDbInts[i] >= locationOffset);
            newDbInts[i] -= locationOffset;
        }

        return new MetadataDb(newDatabase);
    }

    /// <summary>
    /// Takes ownership of the internal rented backing array, clearing the current instance.
    /// </summary>
    /// <param name="rentedBacking">Returns the rented backing array that was taken ownership of.</param>
    /// <returns>The length of data in the returned backing array.</returns>
    internal int TakeOwnership(out byte[] rentedBacking)
    {
        byte[]? data = Interlocked.Exchange(ref _data, null!);
        Debug.Assert(data != null);
        rentedBacking = data;
        int length = Length;
        Length = 0;
        return length;
    }

    /// <summary>
    /// Overwrites data in the destination metadata database starting at the specified target index.
    /// </summary>
    /// <param name="destination">The destination metadata database to write to.</param>
    /// <param name="targetIndex">The index in the destination where writing should begin.</param>
    internal void Overwrite(ref MetadataDb destination, int targetIndex)
    {
        Debug.Assert(Length <= destination.Length - targetIndex);
        Buffer.BlockCopy(_data, 0, destination._data, targetIndex, Length);
    }
}