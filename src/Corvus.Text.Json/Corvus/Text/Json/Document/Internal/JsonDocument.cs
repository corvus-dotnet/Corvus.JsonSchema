// <copyright file="JsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Corvus.Numerics;
using NodaTime;

#if NET

using System.Globalization;

#endif

using System.Threading;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Base class for JSON document implementations providing common functionality
/// for parsing and accessing JSON data.
/// </summary>
public abstract partial class JsonDocument
{
    /// <summary>
    /// Mask used for extracting hash values from stored metadata.
    /// </summary>
    [CLSCompliant(false)]
    protected const ulong HashMask = 0xFFUL << 56;

    /// <summary>
    /// Length in bytes of hash values stored in metadata.
    /// </summary>
    protected const int HashLength = 8;

    /// <summary>
    /// Backing array for the property map data.
    /// </summary>
    [CLSCompliant(false)]
    protected byte[]? _propertyMapBacking;

    /// <summary>
    /// Backing array for the hash buckets used in property lookups.
    /// </summary>
    [CLSCompliant(false)]
    protected int[]? _bucketsBacking;

    /// <summary>
    /// Backing array for the hash table entries used in property lookups.
    /// </summary>
    [CLSCompliant(false)]
    protected byte[]? _entriesBacking;

    /// <summary>
    /// Backing array for storing dynamic values created during document manipulation.
    /// </summary>
    [CLSCompliant(false)]
    protected byte[]? _valueBacking;

    /// <summary>
    /// Current offset into the property map backing array.
    /// </summary>
    [CLSCompliant(false)]
    protected int _propertyMapOffset;

    /// <summary>
    /// Current offset into the buckets backing array.
    /// </summary>
    [CLSCompliant(false)]
    protected int _bucketOffset;

    /// <summary>
    /// Current offset into the entries backing array.
    /// </summary>
    [CLSCompliant(false)]
    protected int _entryOffset;

    /// <summary>
    /// Current offset into the value backing array.
    /// </summary>
    [CLSCompliant(false)]
    protected int _valueOffset;

    /// <summary>
    /// The metadata database containing all parsed JSON structure information.
    /// </summary>
    [CLSCompliant(false)]
    protected MetadataDb _parsedData;

    /// <summary>
    /// Indicates whether this document instance is immutable and cannot be modified.
    /// </summary>
    [CLSCompliant(false)]
    protected bool _isImmutable;

    // These are the indices of the one-and-only instances of the "null", "true", and "false" text in this document.
    private int _nullIndex = -1;

    private int _trueIndex = -1;

    private int _falseIndex = -1;

    /// <summary>
    /// Gets a value indicating whether the document is immutable.
    /// </summary>
    public bool IsImmutable
    {
        get => _isImmutable;
    }

    /// <summary>
    /// Makes the document immutable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You can only create a new document from this document once it is frozen.
    /// </para>
    /// <para>
    /// Immutable documents (like <see cref="ParsedJsonDocument{T}"/> are frozen once they
    /// are created, and there is no need to call freeze on them.
    /// </para>
    /// <para>
    /// Mutable documents (like <see cref="JsonDocumentBuilder{T}"/> must be frozen before
    /// you can create a child document from one of its elements. Once a mutable document is
    /// frozen, any methods that would modify the document will throw.
    /// </para>
    /// </remarks>
    public void Freeze()
    {
        _isImmutable = true;
    }

    /// <summary>
    /// Gets the raw simple value as a memory span for the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes in the result.</param>
    /// <returns>The raw value as a memory span.</returns>
    protected abstract ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index, bool includeQuotes);

    /// <summary>
    /// Gets the raw simple value as a memory span for the specified index using external metadata.
    /// </summary>
    /// <param name="parsedData">The parsed data metadata database.</param>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes in the result.</param>
    /// <returns>The raw value as a memory span.</returns>
    protected abstract ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index, bool includeQuotes);

    /// Gets the raw simple value as a memory span for the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw value as a memory span.</returns>
    protected abstract ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index);

    /// <summary>
    /// Gets the raw simple value as a memory span for the specified index using external metadata.
    /// </summary>
    /// <param name="parsedData">The parsed data metadata database.</param>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes in the result.</param>
    /// <returns>The raw value as a memory span.</returns>
    protected abstract ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index);

    /// <summary>
    /// Checks that the actual token type matches the expected token type, throwing an exception if not.
    /// </summary>
    /// <param name="expected">The expected token type.</param>
    /// <param name="actual">The actual token type.</param>
    /// <exception cref="JsonException">Thrown when the types don't match.</exception>
    protected static void CheckExpectedType(JsonTokenType expected, JsonTokenType actual)
    {
        if (expected != actual)
        {
            ThrowHelper.ThrowJsonElementWrongTypeException(expected, actual);
        }
    }

    /// <summary>
    /// Gets the database size for the specified index, optionally including the end element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeEndElement">Whether to include the end element in the size calculation.</param>
    /// <returns>The database size in bytes.</returns>
    protected virtual int GetDbSizeUnsafe(int index, bool includeEndElement)
    {
        DbRow row = _parsedData.Get(index);

        if (row.IsSimpleValue)
        {
            return DbRow.Size;
        }

        int endIndex = DbRow.Size * row.NumberOfRows;

        if (includeEndElement)
        {
            endIndex += DbRow.Size;
        }

        return endIndex;
    }

    /// <summary>
    /// Gets the start index for the specified end index.
    /// </summary>
    /// <param name="endIndex">The end index of the element.</param>
    /// <returns>The start index of the element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int GetStartIndexUnsafe(int endIndex)
    {
        DbRow row = _parsedData.Get(endIndex);

        if (row.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            return endIndex - (DbRow.Size * row.NumberOfRows);
        }
        else
        {
            return endIndex;
        }
    }

    /// <summary>
    /// Compares the text at the specified index with the provided character span for equality.
    /// </summary>
    /// <param name="index">The index of the element to compare.</param>
    /// <param name="otherText">The text to compare against.</param>
    /// <param name="isPropertyName">Whether the element is a property name.</param>
    /// <returns><see langword="true"/> if the texts are equal; otherwise, <see langword="false"/>.</returns>
    protected bool TextEqualsUnsafe(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
    {
        byte[]? otherUtf8TextArray = null;

        int length = checked(otherText.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);

        // Use unsigned comparison for efficient stackalloc threshold check
        Span<byte> otherUtf8Text = (uint)length <= (uint)JsonConstants.StackallocByteThreshold ?
            stackalloc byte[JsonConstants.StackallocByteThreshold] :
            (otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length));

        OperationStatus status = JsonWriterHelper.ToUtf8(otherText, otherUtf8Text, out int written);
        Debug.Assert(status != OperationStatus.DestinationTooSmall);
        bool result;
        if (status == OperationStatus.InvalidData)
        {
            result = false;
        }
        else
        {
            Debug.Assert(status == OperationStatus.Done);
            result = TextEqualsUnsafe(index, otherUtf8Text.Slice(0, written), isPropertyName, shouldUnescape: true);
        }

        if (otherUtf8TextArray != null)
        {
            otherUtf8Text.Slice(0, written).Clear();
            ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
        }

        return result;
    }

    /// <summary>
    /// Compares the text at the specified index with the provided UTF-8 byte span for equality.
    /// </summary>
    /// <param name="index">The index of the element to compare.</param>
    /// <param name="otherUtf8Text">The UTF-8 encoded text to compare against.</param>
    /// <param name="isPropertyName">Whether the element is a property name.</param>
    /// <param name="shouldUnescape">Whether to unescape the text during comparison.</param>
    /// <returns><see langword="true"/> if the texts are equal; otherwise, <see langword="false"/>.</returns>
    protected bool TextEqualsUnsafe(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
    {
        int matchIndex = isPropertyName ? index - DbRow.Size : index;

        DbRow row = _parsedData.Get(matchIndex);

        CheckExpectedType(
            isPropertyName ? JsonTokenType.PropertyName : JsonTokenType.String,
            row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(matchIndex, includeQuotes: false).Span;

        // Use unsigned comparison to optimize length checks
        if ((uint)otherUtf8Text.Length > (uint)segment.Length || (!shouldUnescape && otherUtf8Text.Length != segment.Length))
        {
            return false;
        }

        if (row.HasComplexChildren && shouldUnescape)
        {
            // Use unsigned comparison for length validation
            if ((uint)otherUtf8Text.Length < (uint)(segment.Length / JsonConstants.MaxExpansionFactorWhileEscaping))
            {
                return false;
            }

            int idx = segment.IndexOf(JsonConstants.BackSlash);
            Debug.Assert(idx != -1);

            if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
            {
                return false;
            }

            return JsonReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
        }

        return segment.SequenceEqual(otherUtf8Text);
    }

    /// <summary>
    /// Gets the index of the element at the specified array index within an array element.
    /// </summary>
    /// <param name="currentIndex">The index of the array element.</param>
    /// <param name="arrayIndex">The index within the array to find.</param>
    /// <returns>The index of the element at the specified array position.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the array index is out of range.</exception>
    protected int GetArrayIndexElementUnsafe(int currentIndex, int arrayIndex)
    {
        DbRow row = _parsedData.Get(currentIndex);

        CheckExpectedType(JsonTokenType.StartArray, row.TokenType);

        int arrayLength = row.SizeOrLengthOrPropertyMapIndex;

        if ((uint)arrayIndex >= (uint)arrayLength)
        {
            throw new IndexOutOfRangeException();
        }

        if (!row.HasComplexChildren)
        {
            // Since we wouldn't be here without having completed the document parse, and we
            // already vetted the index against the length, this new index will always be
            // within the table.
            return currentIndex + ((arrayIndex + 1) * DbRow.Size);
        }

        int elementCount = 0;
        int objectOffset = currentIndex + DbRow.Size;

        // Use inlined unsigned comparison for optimized bounds checking
        while ((uint)objectOffset < (uint)_parsedData.Length)
        {
            if (arrayIndex == elementCount)
            {
                return objectOffset;
            }

            row = _parsedData.Get(objectOffset);

            if (!row.IsSimpleValue)
            {
                objectOffset += GetDbSizeUnsafe(objectOffset, includeEndElement: false);
            }

            elementCount++;
            objectOffset += DbRow.Size;
        }

        Debug.Fail(
            $"Ran out of database searching for array index {arrayIndex} from {currentIndex} when length was {arrayLength}");
        throw new IndexOutOfRangeException();
    }

    /// <summary>
    /// Determines whether the value at the specified index is escaped and requires unescaping.
    /// </summary>
    /// <param name="index">The index of the element to check.</param>
    /// <param name="isPropertyName">Whether the element is a property name.</param>
    /// <returns><see langword="true"/> if the value is escaped; otherwise, <see langword="false"/>.</returns>
    protected bool ValueIsEscapedUnsafe(int index, bool isPropertyName)
    {
        int matchIndex = isPropertyName ? index - DbRow.Size : index;
        DbRow row = _parsedData.Get(matchIndex);
        Debug.Assert(!isPropertyName || row.TokenType is JsonTokenType.PropertyName);

        return row.HasComplexChildren;
    }

    /// <summary>
    /// Enlarges a byte array to accommodate the specified minimum size, using array pool rental.
    /// </summary>
    /// <param name="v">The minimum size required.</param>
    /// <param name="byteArray">The byte array to enlarge (passed by reference).</param>
    protected static void Enlarge(int v, [NotNull] ref byte[]? byteArray)
    {
        if (byteArray is null)
        {
            byteArray = ArrayPool<byte>.Shared.Rent(Math.Max(16384, v));
            return;
        }

        // Use unsigned comparison to optimize length check
        if ((uint)byteArray.Length > (uint)v)
        {
            return;
        }

        byte[] toReturn = byteArray;

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

        byteArray = ArrayPool<byte>.Shared.Rent(newCapacity);
        Buffer.BlockCopy(toReturn, 0, byteArray, 0, toReturn.Length);

        // The data in this rented buffer only conveys the positions and
        // lengths of tokens in a document, but no content; so it does not
        // need to be cleared.
        ArrayPool<byte>.Shared.Return(toReturn);
    }

    /// <summary>
    /// Enlarges an integer array to accommodate the specified minimum size, using array pool rental.
    /// </summary>
    /// <param name="v">The minimum size required.</param>
    /// <param name="intArray">The integer array to enlarge (passed by reference).</param>
    protected static void Enlarge(int v, ref int[] intArray)
    {
        int[] toReturn = intArray;

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

        intArray = ArrayPool<int>.Shared.Rent(newCapacity);
        Buffer.BlockCopy(toReturn, 0, intArray, 0, toReturn.Length * sizeof(int));

        // The data in this rented buffer only conveys the positions and
        // lengths of tokens in a document, but no content; so it does not
        // need to be cleared.
        ArrayPool<int>.Shared.Return(toReturn);
    }

    /// <summary>
    /// Stores a boolean value in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The boolean value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreBooleanValue(bool value)
    {
        ref int valueIndex = ref _falseIndex;

        if (value)
        {
            valueIndex = ref _trueIndex;
        }

        if (valueIndex >= 0)
        {
            return valueIndex;
        }

        ReadOnlySpan<byte> valueUtf8 = value ? JsonConstants.TrueValue : JsonConstants.FalseValue;
        int offset = _valueOffset;
        int length = valueUtf8.Length;
        Enlarge(length, ref _valueBacking);

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Boolean);

        valueIndex = offset;

        offset += 4;
        valueUtf8.CopyTo(_valueBacking.AsSpan(offset));
        _valueOffset = offset + length;
        return valueIndex;
    }

    /// <summary>
    /// Stores a null value in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <returns>The offset of the stored null value in the value buffer.</returns>
    protected int StoreNullValue()
    {
        if (_nullIndex >= 0)
        {
            return _nullIndex;
        }

        int offset = _valueOffset;
        int length = JsonConstants.NullValue.Length;
        Enlarge(length, ref _valueBacking);

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Null);

        _nullIndex = offset;

        offset += 4;
        JsonConstants.NullValue.CopyTo(_valueBacking.AsSpan(offset));
        _valueOffset = offset + length;
        return _nullIndex;
    }

    /// <summary>
    /// Stores a GUID value as a quoted string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The GUID value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(Guid value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatGuidLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a DateTime value as a quoted ISO 8601 string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The DateTime value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in DateTime value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatGuidLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        JsonWriterHelper.WriteDateTimeTrimmed(_valueBacking.AsSpan(offset), value, out length);
        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a DateTimeOffset value as a quoted ISO 8601 string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The DateTimeOffset value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in DateTimeOffset value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatGuidLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        JsonWriterHelper.WriteDateTimeOffsetTrimmed(_valueBacking.AsSpan(offset), value, out length);
        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an OffsetDateTime value as a quoted ISO 8601 string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The OffsetDateTime value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in OffsetDateTime value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatDateTimeOffsetLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = JsonElementHelpers.TryFormatOffsetDateTime(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);

        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an OffsetDate value as a quoted ISO 8601 date string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The OffsetDate value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in OffsetDate value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatOffsetDateLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = JsonElementHelpers.TryFormatOffsetDate(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);

        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an OffsetTime value as a quoted ISO 8601 time string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The OffsetTime value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in OffsetTime value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatOffsetTimeLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = JsonElementHelpers.TryFormatOffsetTime(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);

        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a LocalDate value as a quoted ISO 8601 date string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The LocalDate value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in LocalDate value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatOffsetTimeLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = JsonElementHelpers.TryFormatLocalDate(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);

        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a Period value as a quoted string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The Period value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in Period value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatOffsetTimeLength + 2;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        _valueBacking[offset++] = JsonConstants.Quote;

        bool success = JsonElementHelpers.TryFormatPeriod(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);

        offset += length;

        _valueBacking[offset++] = JsonConstants.Quote;

        length += 2;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.QuotedUtf8String);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a signed byte value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The signed byte value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(sbyte value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a byte value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The byte value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(byte value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatUInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The integer value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(int value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an unsigned integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The unsigned integer value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(uint value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatUInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a long integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The long value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(long value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an unsigned long integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The unsigned long value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(ulong value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatUInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a short integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The short value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(short value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores an unsigned short integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The unsigned short value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(ushort value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatUInt64Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a single precision float value as a quoted string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The float value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(float value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatSingleLength;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a double value as a quoted string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The double value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(double value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatDoubleLength;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a decimal value as a quoted string in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The decimal value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(decimal value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatDecimalLength;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = Utf8Formatter.TryFormat(value, _valueBacking.AsSpan(offset), out length);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a BigInteger value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The BigInteger value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(in BigInteger value)
    {
        int offset = _valueOffset;
        int result = offset;

        offset += 4;

        int length;
#if NET
        if (!value.TryGetMinimumFormatBufferLength(out int enlarge))
        {
            ThrowHelper.ThrowOutOfMemoryException((uint)enlarge);
        }

        Enlarge(enlarge, ref _valueBacking);
        bool formatted = value.TryFormat(_valueBacking.AsSpan(offset), out length);
        Debug.Assert(formatted);
#else
        Enlarge(JsonConstants.InitialFormatBigIntegerLength, ref _valueBacking);

        while (!value.TryFormat(_valueBacking.AsSpan(offset), out length))
        {
            Enlarge(JsonConstants.InitialFormatBigIntegerLength, ref _valueBacking);
        }
#endif

        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a BigNumber value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The BigNumber value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(in BigNumber value)
    {
        int offset = _valueOffset;
        int result = offset;

        offset += 4;

        int length;
#if NET
        if (!value.TryGetMinimumFormatBufferLength(out int enlarge))
        {
            ThrowHelper.ThrowOutOfMemoryException((uint)enlarge);
        }

        Enlarge(enlarge, ref _valueBacking);
        bool formatted = value.TryFormat(_valueBacking.AsSpan(offset), out length);
        Debug.Assert(formatted);
#else
        Enlarge(JsonConstants.InitialFormatBigNumberLength, ref _valueBacking);

        while (!value.Normalize().TryFormat(_valueBacking.AsSpan(offset), out length))
        {
            Enlarge(JsonConstants.InitialFormatBigIntegerLength, ref _valueBacking);
        }
#endif
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

#if NET

    /// <summary>
    /// Stores a 128-bit signed integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The Int128 value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(Int128 value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatInt128Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = value.TryFormat(_valueBacking.AsSpan(offset), out length, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a 128-bit unsigned integer value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The UInt128 value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    [CLSCompliant(false)]
    protected int StoreValue(UInt128 value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatUInt128Length;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = value.TryFormat(_valueBacking.AsSpan(offset), out length, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

    /// <summary>
    /// Stores a half-precision floating point value as a number in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="value">The Half value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreValue(Half value)
    {
        int offset = _valueOffset;
        int result = offset;
        int length = JsonConstants.MaximumFormatHalfLength;
        Enlarge(length, ref _valueBacking);

        offset += 4;

        bool success = value.TryFormat(_valueBacking.AsSpan(offset), out length, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        offset += length;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(_valueOffset), (uint)(length << 4) | (uint)DynamicValueType.Number);

        _valueOffset = offset;
        return result;
    }

#endif

    /// <summary>
    /// Unescapes an escaped string value and stores the unescaped result in the dynamic value buffer.
    /// </summary>
    /// <param name="escapedPropertyName">The escaped string to unescape and store.</param>
    /// <param name="dynamicValueOffset">The offset of the stored unescaped value in the value buffer.</param>
    /// <returns>A span containing the unescaped string data.</returns>
    protected ReadOnlySpan<byte> UnescapeAndStoreUnescapedStringValue(ReadOnlySpan<byte> escapedPropertyName, out int dynamicValueOffset)
    {
        int index = escapedPropertyName.IndexOf(JsonConstants.BackSlash);
        Debug.Assert(index >= 0);
        int maxRequiredLength = escapedPropertyName.Length + 4;
        Enlarge(maxRequiredLength, ref _valueBacking);

        int offset = _valueOffset;
        int length = index;
        int valueOffset = offset + 4;
        if (index > 0)
        {
            // Copy the unescaped portion
            escapedPropertyName.CopyTo(_valueBacking.AsSpan(valueOffset));
        }

        // Unescape the rest into the destination
        JsonReaderHelper.Unescape(escapedPropertyName.Slice(index), _valueBacking.AsSpan(valueOffset + index), 0, out int written);
        length += written;

        if (length > 0x0FFFFFFF)
        {
            ThrowHelper.ThrowArgumentException_ValueTooLarge(length);
        }

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(), (uint)(length << 4) | (uint)DynamicValueType.UnescapedUtf8String);
        _valueOffset += length;
        dynamicValueOffset = offset;
        return _valueBacking.AsSpan(valueOffset, length);
    }

    /// <summary>
    /// Escapes and stores a UTF-8 string value in the dynamic value buffer, returning its offset and whether escaping was required.
    /// </summary>
    /// <param name="utf8Value">The UTF-8 string value to escape and store.</param>
    /// <param name="requiredEscaping">Indicates whether the string required escaping.</param>
    /// <param name="encoder">The optional JavaScript encoder to use for escaping.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int EscapeAndStoreRawStringValue(ReadOnlySpan<byte> utf8Value, out bool requiredEscaping, JavaScriptEncoder? encoder)
    {
        int offset = _valueOffset;

        int valueIdx = JsonWriterHelper.NeedsEscaping(utf8Value, encoder);

        Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length);

        int maxRequiredSize = valueIdx == -1 ? utf8Value.Length + 2 + 4 : JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, valueIdx) + 2 + 4;

        Enlarge(_valueOffset + maxRequiredSize, ref _valueBacking);

        int written;

        int index = offset + 4;

        _valueBacking[index++] = JsonConstants.Quote;

        if (valueIdx != -1)
        {
            requiredEscaping = true;
            JsonWriterHelper.EscapeString(utf8Value, _valueBacking.AsSpan(index), valueIdx, encoder, out written);
            index += written;
        }
        else
        {
            requiredEscaping = false;
            utf8Value.CopyTo(_valueBacking.AsSpan(index));
            written = utf8Value.Length;
            index += written;
        }

        _valueBacking[index++] = JsonConstants.Quote;

        // Then write the type information.
        uint length = (uint)(written + 2);
        if (length > 0x0FFFFFFF)
        {
            ThrowHelper.ThrowArgumentException_ValueTooLarge(length);
        }

        // Shift it and OR in the value type.
        length <<= 4;
        length |= (uint)DynamicValueType.QuotedUtf8String;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(offset), length);
        _valueOffset = index;
        return offset;
    }

    /// <summary>
    /// Escapes and stores a character string value in the dynamic value buffer, returning its offset and whether escaping was required.
    /// </summary>
    /// <param name="value">The character string value to escape and store.</param>
    /// <param name="requiredEscaping">Indicates whether the string required escaping.</param>
    /// <param name="encoder">The optional JavaScript encoder to use for escaping.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping, JavaScriptEncoder? encoder)
    {
        int offset = _valueOffset;

        int valueIdx = JsonWriterHelper.NeedsEscaping(value, encoder);

        int valueLength = value.Length;
        Debug.Assert(valueIdx >= -1 && valueIdx < valueLength);

        int maxRequiredSize = valueIdx == -1 ? valueLength + 2 + 4 : JsonWriterHelper.GetMaxEscapedLength(valueLength, valueIdx) + 2 + 4;

        Enlarge(_valueOffset + maxRequiredSize, ref _valueBacking);

        int written;

        int index = offset + 4;

        _valueBacking[index++] = JsonConstants.Quote;

        if (valueIdx != -1)
        {
            requiredEscaping = true;

            char[]? buffer = null;
            Span<char> escapedBuffer = valueLength <= JsonConstants.StackallocCharThreshold ? stackalloc char[JsonConstants.StackallocCharThreshold] : (buffer = ArrayPool<char>.Shared.Rent(valueLength)).AsSpan();

            JsonWriterHelper.EscapeString(value, escapedBuffer, valueIdx, encoder, out written);
            JsonWriterHelper.ToUtf8(escapedBuffer.Slice(0, written), _valueBacking.AsSpan(index), out written);
            index += written;
        }
        else
        {
            requiredEscaping = false;
            JsonWriterHelper.ToUtf8(value, _valueBacking.AsSpan(index), out written);
            written = valueLength;
            index += written;
        }

        _valueBacking[index++] = JsonConstants.Quote;

        // Then write the type information.
        uint length = (uint)(written + 2);
        if (length > 0x0FFFFFFF)
        {
            ThrowHelper.ThrowArgumentException_ValueTooLarge(length);
        }

        // Shift it and OR in the value type.
        length <<= 4;
        length |= (uint)DynamicValueType.QuotedUtf8String;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(offset), length);
        _valueOffset = index;
        return offset;
    }

    /// <summary>
    /// Stores a raw escaped string value in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="escapedString">The already-escaped string to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreRawStringValue(ReadOnlySpan<byte> escapedString)
    {
        int offset = _valueOffset;

        // We write the value buffer offset here, to save doing it again later.
        _valueOffset += escapedString.Length + 6;

        Enlarge(_valueOffset, ref _valueBacking);

        uint length = (uint)escapedString.Length + 2;

        // Use unsigned comparison for efficient bounds check
        if (length > 0x0FFFFFFF)
        {
            ThrowHelper.ThrowArgumentException_ValueTooLarge(length);
        }

        // Shift it and OR in the value type.
        length <<= 4;
        length |= (uint)DynamicValueType.QuotedUtf8String;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(offset), length);
        int index = offset + 4;
        _valueBacking[index++] = JsonConstants.Quote;
        escapedString.CopyTo(_valueBacking.AsSpan(index));
        index += escapedString.Length;
        _valueBacking[index++] = JsonConstants.Quote;
        return offset;
    }

    /// <summary>
    /// Stores a raw unescaped number value in the dynamic value buffer and returns its offset.
    /// </summary>
    /// <param name="unescapedNumberValue">The unescaped number value to store.</param>
    /// <returns>The offset of the stored value in the value buffer.</returns>
    protected int StoreRawNumberValue(ReadOnlySpan<byte> unescapedNumberValue)
    {
        JsonWriterHelper.ValidateNumber(unescapedNumberValue);
        int offset = _valueOffset;

        // We write the value buffer offset here, to save doing it again later.
        _valueOffset += unescapedNumberValue.Length + 4;

        Enlarge(_valueOffset, ref _valueBacking);

        uint length = (uint)unescapedNumberValue.Length;

        // Use unsigned comparison for efficient bounds check
        if (length > 0x0FFFFFFF)
        {
            ThrowHelper.ThrowArgumentException_ValueTooLarge(length);
        }

        // Shift it and OR in the value type.
        length <<= 4;
        length |= (uint)DynamicValueType.Number;

        BitConverter.TryWriteBytes(_valueBacking.AsSpan(offset), length);
        unescapedNumberValue.CopyTo(_valueBacking.AsSpan(offset + 4));

        return offset;
    }

    /// <summary>
    /// Reads an unescaped UTF-8 string from the dynamic value buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in the value buffer where the string is stored.</param>
    /// <returns>A memory span containing the unescaped UTF-8 string data.</returns>
    protected ReadOnlyMemory<byte> ReadDynamicUnescapedUtf8String(int offset)
    {
        // The first 4 bytes are the type and length
        uint length = BitConverter.ToUInt32(_valueBacking!, offset);

        Debug.Assert((DynamicValueType)(length & 0xF) == DynamicValueType.UnescapedUtf8String, $"Expected Unescaped UTF8 string at {offset}");

        length >>= 4;

        return _valueBacking.AsMemory(offset + 4, (int)length);
    }

    /// <summary>
    /// Reads a simple dynamic value (string, number, boolean, or null) from the dynamic value buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in the value buffer where the value is stored.</param>
    /// <param name="includeQuotes">Whether to include quotes in the result for string values.</param>
    /// <returns>A memory span containing the dynamic value data.</returns>
    protected ReadOnlyMemory<byte> ReadRawSimpleDynamicValue(int offset, bool includeQuotes)
    {
        // The first 4 bytes are the type and length
        uint length = BitConverter.ToUInt32(_valueBacking!, offset);

        var valueType = (DynamicValueType)(length & 0xF);
        Debug.Assert(valueType is DynamicValueType.QuotedUtf8String or DynamicValueType.Number or DynamicValueType.Boolean or DynamicValueType.Null, $"Expected simple value at {offset}");

        length >>= 4;

        int start;

        if (!includeQuotes && valueType == DynamicValueType.QuotedUtf8String)
        {
            start = offset + 5;
            length -= 2;
        }
        else
        {
            start = offset + 4;
        }

        return _valueBacking.AsMemory(start, (int)length);
    }

    /// <summary>
    /// Reads a simple dynamic value (string, number, boolean, or null) from the dynamic value buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in the value buffer where the value is stored.</param>
    /// <returns>A memory span containing the dynamic value data.</returns>
    protected ReadOnlyMemory<byte> ReadRawSimpleDynamicValue(int offset)
    {
        // The first 4 bytes are the type and length
        uint length = BitConverter.ToUInt32(_valueBacking!, offset);

        var valueType = (DynamicValueType)(length & 0xF);
        Debug.Assert(valueType is DynamicValueType.QuotedUtf8String or DynamicValueType.Number or DynamicValueType.Boolean or DynamicValueType.Null, $"Expected simple value at {offset}");

        length >>= 4;

        int start;

        if (valueType == DynamicValueType.QuotedUtf8String)
        {
            start = offset + 5;
            length -= 2;
        }
        else
        {
            start = offset + 4;
        }

        return _valueBacking.AsMemory(start, (int)length);
    }

    /// <summary>
    /// Attempts to get the index of a named property value within a JSON object using a character span property name.
    /// </summary>
    /// <param name="index">The index of the JSON object.</param>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <param name="valueIndex">The index of the property value if found.</param>
    /// <returns><see langword="true"/> if the property is found; otherwise, <see langword="false"/>.</returns>
    protected bool TryGetNamedPropertyValueIndexUnsafe(int index, ReadOnlySpan<char> propertyName, out int valueIndex)
    {
        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.StartObject, row.TokenType);

        // Only one row means it was EndObject.
        if (!row.FromExternalDocument && row.NumberOfRows == 1)
        {
            valueIndex = -1;
            return false;
        }

        int maxBytes = JsonReaderHelper.s_utf8Encoding.GetMaxByteCount(propertyName.Length);

        byte[]? byteBuffer = null;

        Span<byte> utf8Name =
            maxBytes < JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (byteBuffer = ArrayPool<byte>.Shared.Rent(maxBytes)).AsSpan();

        try
        {
            int len = JsonReaderHelper.GetUtf8FromText(propertyName, utf8Name);
            utf8Name = utf8Name.Slice(0, len);

            return TryGetNamedPropertyValueIndexUnsafe(
                index,
                utf8Name,
                out valueIndex);
        }
        finally
        {
            if (byteBuffer is byte[] b)
            {
                ArrayPool<byte>.Shared.Return(b, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Attempts to get the index of a named property value within a JSON object using a UTF-8 byte span property name.
    /// </summary>
    /// <param name="startIndex">The starting index of the JSON object.</param>
    /// <param name="propertyName">The UTF-8 encoded name of the property to find.</param>
    /// <param name="valueIndex">The index of the property value if found.</param>
    /// <returns><see langword="true"/> if the property is found; otherwise, <see langword="false"/>.</returns>
    protected bool TryGetNamedPropertyValueIndexUnsafe(
        int startIndex,
        ReadOnlySpan<byte> propertyName,
        out int valueIndex)
    {
        DbRow row = _parsedData.Get(startIndex);

        CheckExpectedType(JsonTokenType.StartObject, row.TokenType);

        // Only one row means it was EndObject.
        if (!row.FromExternalDocument && row.NumberOfRows == 1)
        {
            valueIndex = -1;
            return false;
        }

        int endIndex = startIndex + GetDbSizeUnsafe(startIndex, false); // checked(row.NumberOfRows * DbRow.Size + startIndex);

        DbRow endObjectRow = _parsedData.Get(endIndex);

        if (endObjectRow.HasPropertyMap)
        {
            return TryGetNamedPropertyValueFromPropertyMap(endObjectRow.SizeOrLengthOrPropertyMapIndex, propertyName, out valueIndex);
        }

        return TryGetNamedPropertyValueIndexUnsafe(ref _parsedData, startIndex, endIndex, propertyName, out valueIndex);
    }

    /// <summary>
    /// Try to resolve the given UTF-8 JSON pointer.
    /// </summary>
    /// <param name="jsonPointerUtf8">The UTF-8 JSON pointer to resolve.</param>
    /// <param name="startRowIndex">The index of the element.</param>
    /// <param name="resultIndex">The index of the result, if it could be resolved. Otherwise <c>-1</c>.</param>
    /// <returns><see langword="true"/> if the pointer could be resolved, otherwise <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    protected bool TryResolveJsonPointerUnsafe(ReadOnlySpan<byte> jsonPointerUtf8, int startRowIndex, out int resultIndex)
    {
        int currentRowIndex = startRowIndex;

        int index = 0;
        int startRun = 0;
        byte[]? decodedComponentBytes = null;
        Span<byte> decodedComponent =
            jsonPointerUtf8.Length < JsonConstants.StackallocByteThreshold
                ? stackalloc byte[jsonPointerUtf8.Length]
                : (decodedComponentBytes = ArrayPool<byte>.Shared.Rent(jsonPointerUtf8.Length));

        try
        {
            while (index < jsonPointerUtf8.Length)
            {
                if (index == 0 && jsonPointerUtf8[index] == (byte)'#')
                {
                    ++index;
                }

                if (index >= jsonPointerUtf8.Length)
                {
                    break;
                }

                if (jsonPointerUtf8[index] == (byte)'/')
                {
                    ++index;
                }

                startRun = index;

                if (index >= jsonPointerUtf8.Length)
                {
                    break;
                }

                while (index < jsonPointerUtf8.Length && jsonPointerUtf8[index] != (byte)'/')
                {
                    ++index;
                }

                // We've either reached the fragment.Length (so have to go 1 back from the end)
                // or we're sitting on the terminating '/'
                int endRun = index;
                ReadOnlySpan<byte> encodedComponent = jsonPointerUtf8[startRun..endRun];
                int decodedWritten = Utf8JsonPointerTools.DecodePointer(encodedComponent, decodedComponent);
                ReadOnlySpan<byte> component = decodedComponent[..decodedWritten];
                JsonTokenType currentTokenType = _parsedData.GetJsonTokenType(currentRowIndex);
                if (currentTokenType == JsonTokenType.StartObject)
                {
                    if (TryGetNamedPropertyValueIndexUnsafe(currentRowIndex, component, out int next))
                    {
                        currentRowIndex = next;
                    }
                    else
                    {
                        // We were unable to find the element at that location.
                        resultIndex = -1;
                        return false;
                    }
                }
                else if (currentTokenType == JsonTokenType.StartArray)
                {
                    if (Utf8Parser.TryParse(component, out int targetArrayIndex, out int bytesConsumed))
                    {
                        index += bytesConsumed;
                        if (targetArrayIndex >= GetArrayLengthUnsafe(currentRowIndex))
                        {
                            resultIndex = -1;
                            return false;
                        }

                        currentRowIndex = GetArrayIndexElementUnsafe(currentRowIndex, targetArrayIndex);
                    }
                    else
                    {
                        resultIndex = -1;
                        return false;
                    }
                }
            }

            resultIndex = currentRowIndex;
            return true;
        }
        finally
        {
            if (decodedComponentBytes is byte[] dcb)
            {
                ArrayPool<byte>.Shared.Return(dcb);
            }
        }
    }

    /// <summary>
    /// Gets the length of the JSON array at the specified index without performing any safety checks.
    /// </summary>
    /// <param name="index">The index of the start of the array.</param>
    /// <returns>The length of the array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int GetArrayLengthUnsafe(int index)
    {
        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.StartArray, row.TokenType);

        return row.SizeOrLengthOrPropertyMapIndex;
    }

    /// <summary>
    /// Gets the named property value from a specific <see cref="MetadataDb"/>.
    /// </summary>
    /// <param name="parsedData">The parsed data. This is used in place of the document's own MetadataDb.</param>
    /// <param name="startIndex">The index of the first property name.</param>
    /// <param name="endIndex">The index of the last property value.</param>
    /// <param name="propertyName">The unescaped property name to look up.</param>
    /// <param name="valueIndex">The index of the value corresponding to the given property name.</param>
    /// <returns><see langword="true"/> if the property with the given name is found.</returns>
    protected bool TryGetNamedPropertyValueIndexUnsafe(ref MetadataDb parsedData, int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out int valueIndex)
    {
        Span<byte> utf8UnescapedStack = stackalloc byte[JsonConstants.StackallocByteThreshold];
        int index = endIndex - DbRow.Size;
        while (index > startIndex)
        {
            DbRow row = parsedData.Get(index);
            Debug.Assert(row.TokenType != JsonTokenType.PropertyName);

            // Move before the value
            if (row.IsSimpleValue)
            {
                index -= DbRow.Size;
            }
            else
            {
                Debug.Assert(row.NumberOfRows > 0);
                index -= DbRow.Size * (row.NumberOfRows + 1);
            }

            row = parsedData.Get(index);

            Debug.Assert(row.TokenType == JsonTokenType.PropertyName);

            ReadOnlySpan<byte> currentPropertyName = GetRawSimpleValueUnsafe(ref parsedData, index, false).Span;

            if (row.HasComplexChildren)
            {
                // An escaped property name will be longer than an unescaped candidate, so only unescape
                // when the lengths are compatible. Use unsigned comparison for optimization.
                if ((uint)currentPropertyName.Length > (uint)propertyName.Length)
                {
                    int idx = currentPropertyName.IndexOf(JsonConstants.BackSlash);
                    Debug.Assert(idx >= 0);

                    // If everything up to where the property name has a backslash matches, keep going.
                    // Use unsigned comparison for index validation
                    if ((uint)propertyName.Length > (uint)idx &&
                        currentPropertyName.Slice(0, idx).SequenceEqual(propertyName.Slice(0, idx)))
                    {
                        int remaining = currentPropertyName.Length - idx;
                        int written = 0;
                        byte[]? rented = null;

                        try
                        {
                            Span<byte> utf8Unescaped = remaining <= utf8UnescapedStack.Length ?
                                utf8UnescapedStack :
                                (rented = ArrayPool<byte>.Shared.Rent(remaining));

                            // Only unescape the part we haven't processed.
                            JsonReaderHelper.Unescape(currentPropertyName.Slice(idx), utf8Unescaped, 0, out written);

                            // If the unescaped remainder matches the input remainder, it's a match.
                            if (utf8Unescaped.Slice(0, written).SequenceEqual(propertyName.Slice(idx)))
                            {
                                // If the property name is a match, the answer is the next element.
                                valueIndex = index + DbRow.Size;
                                return true;
                            }
                        }
                        finally
                        {
                            if (rented != null)
                            {
                                rented.AsSpan(0, written).Clear();
                                ArrayPool<byte>.Shared.Return(rented);
                            }
                        }
                    }
                }
            }
            else if (currentPropertyName.SequenceEqual(propertyName))
            {
                // If the property name is a match, the answer is the next element.
                valueIndex = index + DbRow.Size;
                return true;
            }

            // Move to the previous value
            index -= DbRow.Size;
        }

        valueIndex = -1;
        return false;
    }

    /// <summary>
    /// Disposes of the core resources used by the JSON document, including returning rented arrays to their pools.
    /// </summary>
    protected void DisposeCore()
    {
        _parsedData.Dispose();

        if (_propertyMapBacking != null)
        {
            // The property map is a rented array, so we need to return it to the pool.
            byte[]? propertyMapBacking = Interlocked.Exchange(ref _propertyMapBacking, null);
            if (propertyMapBacking != null)
            {
                // It does not need to be cleared as it contains no sensitive data
                ArrayPool<byte>.Shared.Return(propertyMapBacking);
            }
        }

        if (_bucketsBacking != null)
        {
            // The buckets are a rented array, so we need to return it to the pool.
            int[]? bucketsBacking = Interlocked.Exchange(ref _bucketsBacking, null);
            if (bucketsBacking != null)
            {
                // It does not need to be cleared as it contains no sensitive data
                ArrayPool<int>.Shared.Return(bucketsBacking);
            }
        }

        if (_entriesBacking != null)
        {
            byte[]? entriesBacking = Interlocked.Exchange(ref _entriesBacking, null);
            if (entriesBacking != null)
            {
                // It does not need to be cleared as it contains no sensitive data
                ArrayPool<byte>.Shared.Return(entriesBacking);
            }
        }

        if (_valueBacking != null)
        {
            byte[]? valueBacking = Interlocked.Exchange(ref _valueBacking, null);
            if (valueBacking != null)
            {
                valueBacking.AsSpan(0, _valueOffset).Clear();
                ArrayPool<byte>.Shared.Return(valueBacking);
            }
        }
    }

    /// <summary>
    /// Gets the hash code for the JSON element at the specified index using an unsafe method that doesn't validate input.
    /// </summary>
    /// <param name="index">The index of the element to get the hash code for.</param>
    /// <returns>The hash code of the element.</returns>
    protected int GetHashCodeUnsafe(int index)
    {
        return _parsedData.GetJsonTokenType(index) switch
        {
            JsonTokenType.StartArray => GetHashCodeForArray(index),
            JsonTokenType.StartObject => GetHashCodeForObject(index),
            JsonTokenType.Number => GetHashCodeForNumber(index),
            JsonTokenType.String => GetHashCodeForString(index),
            JsonTokenType.PropertyName => GetHashCodeForProperty(index),
            JsonTokenType.True => true.GetHashCode(),
            JsonTokenType.False => false.GetHashCode(),
            JsonTokenType.Null => s_nullHashCode,
            _ => s_undefinedHashCode,
        };
    }

    private int GetHashCodeForProperty(int index)
    {
        HashCode code = default;
        code.Add(GetHashCodeForString(index));
        code.Add(GetHashCodeUnsafe(index + DbRow.Size));
        return code.ToHashCode();
    }

    private int GetHashCodeForString(int index)
    {
        ReadOnlySpan<byte> value = GetRawSimpleValueUnsafe(index, false).Span;
        return GetHashCodeForString(value);
    }

    /// <summary>
    /// Gets the HashCode for a string value.
    /// </summary>
    /// <param name="value">The value for which to get the HashCode.</param>
    /// <returns>The HashCode for the specified value.</returns>
    internal static int GetHashCodeForString(ReadOnlySpan<byte> value)
    {
        // We need to unescape and convert to char, in order to get the same
        // hash code as the equivalent string.
        char[]? charBuffer = null;
        int length = Encoding.UTF8.GetMaxCharCount(value.Length);

        Span<char> buffer = length < JsonConstants.StackallocNonRecursiveCharThreshold ? stackalloc char[length]
            : (charBuffer = ArrayPool<char>.Shared.Rent(length)).AsSpan();

        int written = 0;

        try
        {
            written = JsonReaderHelper.TranscodeHelper(value, buffer);
            return string.GetHashCode(buffer.Slice(written));
        }
        finally
        {
            if (charBuffer is not null)
            {
                if (written > 0)
                {
                    // Clear the buffer to avoid leaking sensitive information.
                    Array.Clear(charBuffer, 0, written);
                }

                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
    }

    private int GetHashCodeForNumber(int index)
    {
        // We convert to a normalized JSON number, then get the hash code for that.
        ReadOnlySpan<byte> numberValue = GetRawSimpleValueUnsafe(index, false).Span;
        JsonElementHelpers.ParseNumber(numberValue, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        HashCode code = default;
        code.Add(isNegative);
        code.AddBytes(integral);
        code.AddBytes(fractional);
        code.Add(exponent);

        return code.ToHashCode();
    }

    private int GetHashCodeForObject(int index)
    {
        // We have to be a JSON document!
        Debug.Assert(this is IJsonDocument);

        ObjectEnumerator enumerator = new((IJsonDocument)this, index);

        DbRow row = _parsedData.Get(index);
        int arrayLength = row.SizeOrLengthOrPropertyMapIndex;

        // This is likely to be recursive, so we are conservative in our stack buffer
        // Build a list of hashes of all the properties (name/value pairs).
        using ValueListBuilder<int> valueListBuilder = new(stackalloc int[JsonConstants.StackallocByteThreshold / sizeof(int)]);

        while (enumerator.MoveNext())
        {
            valueListBuilder.Append(GetHashCodeUnsafe(enumerator.CurrentIndex));
        }

        // Then sort them by hash code for stability
        // (as we are property order independent)
        valueListBuilder.Sort();

        // Then build the hash from the ordered list
        HashCode hash = default;

        ReadOnlySpan<int> span = valueListBuilder.AsSpan();
        for (int i = 0; i < span.Length; ++i)
        {
            hash.Add(span[i]);
        }

        return hash.ToHashCode();
    }

    private int GetHashCodeForArray(int index)
    {
        HashCode hash = default;

        // We have to be a JSON document!
        Debug.Assert(this is IJsonDocument);

        ArrayEnumerator enumerator = new((IJsonDocument)this, index);
        while (enumerator.MoveNext())
        {
            hash.Add(GetHashCodeUnsafe(enumerator.CurrentIndex));
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Gets the null hash code.
    /// </summary>
    internal static readonly int s_nullHashCode = CreateNullHashCode();

    /// <summary>
    /// Gets the undefined hash code.
    /// </summary>
    internal static readonly int s_undefinedHashCode = CreateUndefinedHashCode();

    private static int CreateNullHashCode()
    {
        HashCode code = default;
        code.Add((object?)null);
        return code.ToHashCode();
    }

    private static int CreateUndefinedHashCode()
    {
        HashCode code = default;

        // We'll pick a random value and use it as our undefined hash code.
        code.Add(Guid.NewGuid());
        return code.ToHashCode();
    }
}