// <copyright file="ParsedJsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;

namespace Corvus.Text.Json;

/// <summary>
/// Represents the structure of a JSON value in a lightweight, read-only form.
/// </summary>
/// <remarks>
/// This class utilizes resources from pooled memory to minimize the garbage collector (GC)
/// impact in high-usage scenarios. Failure to properly Dispose this object will result in
/// the memory not being returned to the pool, which will cause an increase in GC impact across
/// various parts of the framework.
/// </remarks>
public sealed partial class ParsedJsonDocument<T> : JsonDocument, IJsonDocument, IDisposable
    where T : struct, IJsonElement<T>
{
    private ReadOnlyMemory<byte> _utf8Json;

    private readonly bool _isDisposable;

    private byte[]? _extraRentedArrayPoolBytes;

    private PooledByteBufferWriter? _extraPooledByteBufferWriter;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool IJsonDocument.IsDisposable => _isDisposable;

    /// <summary>
    /// The <see cref="IJsonElement"/> representing the value of the document.
    /// </summary>

#if NET
    public T RootElement => T.CreateInstance(this, 0);
#else
    public T RootElement => JsonElementHelpers.CreateInstance<T>(this, 0);
#endif

    private ParsedJsonDocument(
        ReadOnlyMemory<byte> utf8Json,
        MetadataDb parsedData,
        byte[]? extraRentedArrayPoolBytes = null,
        PooledByteBufferWriter? extraPooledByteBufferWriter = null,
        bool isDisposable = true)
    {
        Debug.Assert(!utf8Json.IsEmpty);

        // Both rented values better be null if we're not disposable.
        Debug.Assert(isDisposable ||
            (extraRentedArrayPoolBytes == null && extraPooledByteBufferWriter == null));

        // Both rented values can't be specified.
        Debug.Assert(extraRentedArrayPoolBytes == null || extraPooledByteBufferWriter == null);

        _utf8Json = utf8Json;
        _parsedData = parsedData;
        _extraRentedArrayPoolBytes = extraRentedArrayPoolBytes;
        _extraPooledByteBufferWriter = extraPooledByteBufferWriter;
        _isDisposable = isDisposable;
        _isImmutable = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        int length = _utf8Json.Length;
        if ((uint)length == 0 || !_isDisposable)
        {
            return;
        }

        DisposeCore();

        _utf8Json = ReadOnlyMemory<byte>.Empty;

        if (_extraRentedArrayPoolBytes != null)
        {
            byte[]? extraRentedBytes = Interlocked.Exchange<byte[]?>(ref _extraRentedArrayPoolBytes, null);

            if (extraRentedBytes != null)
            {
                // When "extra rented bytes exist" it contains the document,
                // and thus needs to be cleared before being returned.
                extraRentedBytes.AsSpan(0, length).Clear();
                ArrayPool<byte>.Shared.Return(extraRentedBytes);
            }
        }
        else if (_extraPooledByteBufferWriter != null)
        {
            PooledByteBufferWriter? extraBufferWriter = Interlocked.Exchange<PooledByteBufferWriter?>(ref _extraPooledByteBufferWriter, null);
            extraBufferWriter?.Dispose();
        }
    }

    /// <summary>
    /// Write the document into the provided writer as a JSON value.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <exception cref="ArgumentNullException">
    /// The <paramref name="writer"/> parameter is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This <see cref="RootElement"/>'s <see cref="JsonElement.ValueKind"/> would result in an invalid JSON.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        RootElement.WriteTo(writer);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    JsonTokenType IJsonDocument.GetJsonTokenType(int index)
    {
        CheckNotDisposed();

        return _parsedData.GetJsonTokenType(index);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IJsonDocument.EnsurePropertyMap(int index)
    {
        if (_isDisposable)
        {
            CheckNotDisposed();

            CheckExpectedType(JsonTokenType.StartObject, _parsedData.GetJsonTokenType(index));

            EnsurePropertyMapUnsafe(index);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.ValueIsEscaped(int index, bool isPropertyName)
    {
        CheckNotDisposed();

        return ValueIsEscapedUnsafe(index, isPropertyName);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetArrayLength(int index)
    {
        CheckNotDisposed();

        return GetArrayLengthUnsafe(index);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetPropertyCount(int index)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.StartObject, row.TokenType);

        return row.SizeOrLengthOrPropertyMapIndex;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    JsonElement IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex)
    {
        CheckNotDisposed();

        return new JsonElement(this, GetArrayIndexElementUnsafe(currentIndex, arrayIndex));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    TElement IJsonDocument.GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex)
    {
        CheckNotDisposed();

#if NET
        return TElement.CreateInstance(this, GetArrayIndexElementUnsafe(currentIndex, arrayIndex));
#else
        return JsonElementHelpers.CreateInstance<TElement>(this, GetArrayIndexElementUnsafe(currentIndex, arrayIndex));
#endif
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex)
    {
        CheckNotDisposed();

        parentDocument = this;
        parentDocumentIndex = GetArrayIndexElementUnsafe(currentIndex, arrayIndex);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetArrayInsertionIndex(int currentIndex, int arrayIndex)
    {
        CheckNotDisposed();

        if (arrayIndex == GetArrayLengthUnsafe(currentIndex))
        {
            return currentIndex + GetDbSizeUnsafe(currentIndex, false);
        }

        return GetArrayIndexElementUnsafe(currentIndex, arrayIndex);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetDbSize(int index, bool includeEndElement)
    {
        CheckNotDisposed();
        return GetDbSizeUnsafe(index, includeEndElement);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetStartIndex(int endIndex)
    {
        CheckNotDisposed();
        return GetStartIndexUnsafe(endIndex);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes)
    {
        CheckNotDisposed();
        return new(GetRawValueUnsafe(index, includeQuotes));
    }

    private ReadOnlyMemory<byte> GetRawValueUnsafe(int index, bool includeQuotes)
    {
        DbRow row = _parsedData.Get(index);

        if (row.IsSimpleValue)
        {
            if (includeQuotes && row.TokenType == JsonTokenType.String)
            {
                // Start one character earlier than the value (the open quote)
                // End one character after the value (the close quote)
                return _utf8Json.Slice(row.LocationOrIndex - 1, row.SizeOrLengthOrPropertyMapIndex + 2);
            }

            return _utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);
        }

        int endElementIdx = index + GetDbSizeUnsafe(index, includeEndElement: false);
        int start = row.LocationOrIndex;
        row = _parsedData.Get(endElementIdx);
        return _utf8Json.Slice(start, row.LocationOrIndex - start + (row.HasPropertyMap ? GetLengthOfEndToken(row.SizeOrLengthOrPropertyMapIndex) : row.SizeOrLengthOrPropertyMapIndex));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index, bool includeQuotes)
    {
        CheckNotDisposed();

        return GetRawSimpleValueUnsafe(index, includeQuotes);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index)
    {
        CheckNotDisposed();

        return GetRawSimpleValueUnsafe(index);
    }

    /// <summary>
    /// Gets the raw simple value from the document without bounds checking.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes for string values.</param>
    /// <returns>The raw value as a read-only memory of bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index, bool includeQuotes)
    {
        return GetRawSimpleValueUnsafe(ref _parsedData, index, includeQuotes);
    }

    /// <summary>
    /// Gets the raw simple value from the document without bounds checking.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw value as a read-only memory of bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index)
    {
        return GetRawSimpleValueUnsafe(ref _parsedData, index);
    }

    /// <summary>
    /// Gets the raw simple value from the specified metadata database without bounds checking.
    /// </summary>
    /// <param name="parsedData">The metadata database to read from.</param>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes for string values.</param>
    /// <returns>The raw value as a read-only memory of bytes.</returns>
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index, bool includeQuotes)
    {
        DbRow row = parsedData.Get(index);
        Debug.Assert(row.IsSimpleValue);

        if (includeQuotes && row.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
        {
            // Start one character earlier than the value (the open quote)
            // End one character after the value (the close quote)
            return _utf8Json.Slice(row.LocationOrIndex - 1, row.SizeOrLengthOrPropertyMapIndex + 2);
        }

        return _utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);
    }

    /// <summary>
    /// Gets the raw simple value from the specified metadata database without bounds checking.
    /// </summary>
    /// <param name="parsedData">The metadata database to read from.</param>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw value as a read-only memory of bytes.</returns>
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index)
    {
        DbRow row = parsedData.Get(index);
        Debug.Assert(row.IsSimpleValue);

        return _utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string? IJsonDocument.GetString(int index, JsonTokenType expectedType)
    {
        CheckNotDisposed();
        return GetStringUnsafe(index, expectedType);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        JsonTokenType tokenType = row.TokenType;

        if (expectedType != tokenType)
        {
            result = null;
            return false;
        }

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        result = row.HasComplexChildren
            ? JsonReaderHelper.GetUnescapedString(segment)
            : JsonReaderHelper.TranscodeHelper(segment);
        return true;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    UnescapedUtf8JsonString IJsonDocument.GetUtf8JsonString(int index, JsonTokenType expectedType)
    {
        CheckNotDisposed();
        return GetUtf8JsonStringUnsafe(index, expectedType);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    UnescapedUtf16JsonString IJsonDocument.GetUtf16JsonString(int index, JsonTokenType expectedType)
    {
        CheckNotDisposed();
        return GetUtf16JsonStringUnsafe(index, expectedType);
    }

    private string? GetStringUnsafe(int index, JsonTokenType expectedType)
    {
        DbRow row = _parsedData.Get(index);

        JsonTokenType tokenType = row.TokenType;

        if (tokenType == JsonTokenType.Null)
        {
            return null;
        }

        CheckExpectedType(expectedType, tokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return row.HasComplexChildren
            ? JsonReaderHelper.GetUnescapedString(segment)
            : JsonReaderHelper.TranscodeHelper(segment);
    }

    private UnescapedUtf8JsonString GetUtf8JsonStringUnsafe(int index, JsonTokenType expectedType)
    {
        DbRow row = _parsedData.Get(index);

        JsonTokenType tokenType = row.TokenType;

        if (expectedType != JsonTokenType.None)
        {
            CheckExpectedType(expectedType, tokenType);
        }
        else
        {
            if (tokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, tokenType);
            }
        }

        ReadOnlyMemory<byte> segment = _utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (row.HasComplexChildren)
        {
            int segmentLength = segment.Length;
            byte[] rentedBytes = ArrayPool<byte>.Shared.Rent(segmentLength);
            try
            {
                JsonReaderHelper.Unescape(segment.Span, rentedBytes, out int written);
                return new UnescapedUtf8JsonString(rentedBytes.AsMemory(0, written), rentedBytes);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rentedBytes);
            }
        }

        return new UnescapedUtf8JsonString(segment);
    }

    private UnescapedUtf16JsonString GetUtf16JsonStringUnsafe(int index, JsonTokenType expectedType)
    {
        DbRow row = _parsedData.Get(index);

        JsonTokenType tokenType = row.TokenType;

        if (expectedType != JsonTokenType.None)
        {
            CheckExpectedType(expectedType, tokenType);
        }
        else
        {
            if (tokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, tokenType);
            }
        }

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (row.HasComplexChildren)
        {
            // Unescape from the escaped UTF-8 source first.
            int utf8Length = segment.Length;
            byte[]? rentedUtf8 = null;

            Span<byte> utf8Unescaped = utf8Length <= JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (rentedUtf8 = ArrayPool<byte>.Shared.Rent(utf8Length));

            try
            {
                JsonReaderHelper.Unescape(segment, utf8Unescaped, out int utf8Written);
                utf8Unescaped = utf8Unescaped.Slice(0, utf8Written);

                // Transcode the unescaped UTF-8 to UTF-16 chars.
                char[] rentedChars = ArrayPool<char>.Shared.Rent(utf8Written);
                try
                {
                    int charsWritten = JsonReaderHelper.TranscodeHelper(utf8Unescaped, rentedChars);
                    return new UnescapedUtf16JsonString(rentedChars.AsMemory(0, charsWritten), rentedChars);
                }
                catch
                {
                    ArrayPool<char>.Shared.Return(rentedChars);
                    throw;
                }
            }
            finally
            {
                if (rentedUtf8 != null)
                {
                    utf8Unescaped.Clear();
                    ArrayPool<byte>.Shared.Return(rentedUtf8);
                }
            }
        }

        // No escaping needed — just transcode UTF-8 to UTF-16.
        char[] rentedTranscoded = ArrayPool<char>.Shared.Rent(segment.Length);
        try
        {
            int charsWritten = JsonReaderHelper.TranscodeHelper(segment, rentedTranscoded);
            return new UnescapedUtf16JsonString(rentedTranscoded.AsMemory(0, charsWritten), rentedTranscoded);
        }
        catch
        {
            ArrayPool<char>.Shared.Return(rentedTranscoded);
            throw;
        }
    }

    private bool TryGetUnescapedUtf8JsonStringUnsafe(int index, Span<byte> destination, JsonTokenType expectedType, out int bytesWritten)
    {
        DbRow row = _parsedData.Get(index);

        JsonTokenType tokenType = row.TokenType;

        if (expectedType != JsonTokenType.None)
        {
            CheckExpectedType(expectedType, tokenType);
        }
        else
        {
            if (tokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, tokenType);
            }
        }

        ReadOnlyMemory<byte> segment = _utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (row.HasComplexChildren)
        {
            return JsonReaderHelper.TryUnescape(segment.Span, destination, out bytesWritten);
        }

        if (segment.Span.TryCopyTo(destination))
        {
            bytesWritten = segment.Length;
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
    {
        CheckNotDisposed();
        return TextEqualsUnsafe(index, otherText, isPropertyName);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
    {
        CheckNotDisposed();
        return TextEqualsUnsafe(index, otherUtf8Text, isPropertyName, shouldUnescape);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string IJsonDocument.GetNameOfPropertyValue(int index)
    {
        CheckNotDisposed();

        // The property name is one row before the property value
        return GetStringUnsafe(index - DbRow.Size, JsonTokenType.PropertyName)!;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlySpan<byte> IJsonDocument.GetPropertyNameRaw(int index)
    {
        CheckNotDisposed();
        Debug.Assert(_parsedData.Get(index - DbRow.Size).TokenType is JsonTokenType.PropertyName);

        return GetRawSimpleValueUnsafe(index - DbRow.Size, false).Span;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetPropertyNameRaw(int index, bool includeQuotes)
    {
        CheckNotDisposed();
        Debug.Assert(_parsedData.Get(index - DbRow.Size).TokenType is JsonTokenType.PropertyName);

        return GetRawSimpleValueUnsafe(index - DbRow.Size, includeQuotes);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    JsonElement IJsonDocument.GetPropertyName(int index)
    {
        CheckNotDisposed();
        Debug.Assert(_parsedData.Get(index - DbRow.Size).TokenType is JsonTokenType.PropertyName);

        return new JsonElement(this, index - DbRow.Size);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    UnescapedUtf8JsonString IJsonDocument.GetPropertyNameUnescaped(int index)
    {
        CheckNotDisposed();
        Debug.Assert(_parsedData.Get(index - DbRow.Size).TokenType is JsonTokenType.PropertyName);

        return GetUtf8JsonStringUnsafe(index - DbRow.Size, JsonTokenType.PropertyName);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, [NotNullWhen(true)] out byte[]? value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        // Segment needs to be unescaped
        if (row.HasComplexChildren)
        {
            return JsonReaderHelper.TryGetUnescapedBase64Bytes(segment, out value);
        }

        Debug.Assert(segment.IndexOf(JsonConstants.BackSlash) == -1);
        return JsonReaderHelper.TryDecodeBase64(segment, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out sbyte value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out byte value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out byte tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out short value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out short tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out ushort value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out ushort tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out int value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out int tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out uint value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out uint tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out long value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out long tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out ulong value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out ulong tmp, out int consumed) &&
            (uint)consumed == (uint)segment.Length)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out double value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed) &&
            (uint)segment.Length == (uint)bytesConsumed)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out float value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed) &&
            (uint)segment.Length == (uint)bytesConsumed)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out decimal value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if (Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed) &&
            (uint)segment.Length == (uint)bytesConsumed)
        {
            value = tmp;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out BigInteger value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return BigInteger.TryParse(segment, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out BigNumber value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return BigNumber.TryParse(segment, out value);
    }

#if NET

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Int128 value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Int128.TryParse(segment, out Int128 tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out UInt128 value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (UInt128.TryParse(segment, out UInt128 tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Half value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Half.TryParse(segment, out Half tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

#endif

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateTime value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDate value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        if ((uint)segment.Length > (uint)JsonConstants.MaximumEscapedDateTimeOffsetParseLength)
        {
            value = default;
            return false;
        }

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetTime value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out LocalDate value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Period value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

#if NET
    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateOnly value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out TimeOnly value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }
#endif

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Guid value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> data = _utf8Json.Span;
        ReadOnlySpan<byte> segment = data.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex);

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string IJsonDocument.GetRawValueAsString(int index)
    {
        CheckNotDisposed();

        return GetRawValueAsStringUnsafe(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetRawValueAsStringUnsafe(int index)
    {
        ReadOnlyMemory<byte> segment = GetRawValueUnsafe(index, includeQuotes: true);
        return JsonReaderHelper.TranscodeHelper(segment.Span);
    }

    /// <inheritdoc />
    string IJsonDocument.ToString(int index)
    {
        CheckNotDisposed();

        switch (_parsedData.GetJsonTokenType(index))
        {
            case JsonTokenType.None:
            case JsonTokenType.Null:
                return string.Empty;

            case JsonTokenType.True:
                return JsonConstants.ToStringTrueValueUtf16String;

            case JsonTokenType.False:
                return JsonConstants.ToStringFalseValueUtf16String;

            case JsonTokenType.Number:
            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
            {
                // null parent should have hit the None case
                return GetRawValueAsStringUnsafe(index);
            }

            case JsonTokenType.String:
                return GetStringUnsafe(index, JsonTokenType.String)!;

            case JsonTokenType.Comment:
            case JsonTokenType.EndArray:
            case JsonTokenType.EndObject:
            default:
                Debug.Fail($"No handler for {nameof(JsonTokenType)}.{_parsedData.GetJsonTokenType(index)}");
                return string.Empty;
        }
    }

    /// <inheritdoc />
    string IJsonDocument.ToString(int index, string? format, IFormatProvider? formatProvider)
    {
        if (_parsedData.GetJsonTokenType(index) == JsonTokenType.Number)
        {
            ReadOnlyMemory<byte> segment = GetRawSimpleValueUnsafe(index, includeQuotes: false);
            if (JsonElementHelpers.TryFormatNumberAsString(segment.Span, format, formatProvider, out string? result))
            {
                return result;
            }
        }

        return ((IJsonDocument)this).ToString(index);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryFormat(int index, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        CheckNotDisposed();

        switch (_parsedData.GetJsonTokenType(index))
        {
            case JsonTokenType.None:
            case JsonTokenType.Null:
                bytesWritten = 0;
                return true;

            case JsonTokenType.True:
                if (JsonConstants.ToStringTrueValue.TryCopyTo(destination))
                {
                    bytesWritten = JsonConstants.ToStringTrueValue.Length;
                    return true;
                }
                else
                {
                    bytesWritten = 0;
                    return false;
                }

            case JsonTokenType.False:
                if (JsonConstants.ToStringFalseValue.TryCopyTo(destination))
                {
                    bytesWritten = JsonConstants.ToStringFalseValue.Length;
                    return true;
                }
                else
                {
                    bytesWritten = 0;
                    return false;
                }

            case JsonTokenType.Number:
            {
                ReadOnlyMemory<byte> segment = GetRawSimpleValueUnsafe(index, includeQuotes: false);
                return JsonElementHelpers.TryFormatNumber(segment.Span, destination, out bytesWritten, format, formatProvider);
            }

            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
            {
                ReadOnlyMemory<byte> segment = GetRawValueUnsafe(index, includeQuotes: true);
                if (segment.Span.TryCopyTo(destination))
                {
                    bytesWritten = segment.Length;
                    return true;
                }

                bytesWritten = 0;
                return false;
            }

            case JsonTokenType.String:
            {
                return TryGetUnescapedUtf8JsonStringUnsafe(index, destination, JsonTokenType.String, out bytesWritten);
            }

            case JsonTokenType.Comment:
            case JsonTokenType.EndArray:
            case JsonTokenType.EndObject:
            default:
                Debug.Fail($"No handler for {nameof(JsonTokenType)}.{_parsedData.GetJsonTokenType(index)}");
                bytesWritten = 0;
                return false;
        }
    }

    /// <inheritdoc />
    bool IJsonDocument.TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        CheckNotDisposed();

        switch (_parsedData.GetJsonTokenType(index))
        {
            case JsonTokenType.None:
            case JsonTokenType.Null:
                charsWritten = 0;
                return true;

            case JsonTokenType.True:
                if (JsonConstants.ToStringTrueValueUtf16.TryCopyTo(destination))
                {
                    charsWritten = JsonConstants.ToStringTrueValueUtf16.Length;
                    return true;
                }
                else
                {
                    charsWritten = 0;
                    return false;
                }

            case JsonTokenType.False:
                if (JsonConstants.ToStringFalseValueUtf16.TryCopyTo(destination))
                {
                    charsWritten = JsonConstants.ToStringFalseValueUtf16.Length;
                    return true;
                }
                else
                {
                    charsWritten = 0;
                    return false;
                }

            case JsonTokenType.Number:
            {
                ReadOnlyMemory<byte> segment = GetRawSimpleValueUnsafe(index, includeQuotes: false);
                return JsonElementHelpers.TryFormatNumber(segment.Span, destination, out charsWritten, format, formatProvider);
            }

            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
            {
                ReadOnlyMemory<byte> segment = GetRawValueUnsafe(index, includeQuotes: true);
                return JsonReaderHelper.TryTranscode(segment.Span, destination, out charsWritten);
            }

            case JsonTokenType.String:
            {
                using UnescapedUtf8JsonString stringValue = GetUtf8JsonStringUnsafe(index, JsonTokenType.String);
                return JsonReaderHelper.TryTranscode(stringValue.Span, destination, out charsWritten);
            }

            case JsonTokenType.Comment:
            case JsonTokenType.EndArray:
            case JsonTokenType.EndObject:
            default:
                Debug.Fail($"No handler for {nameof(JsonTokenType)}.{_parsedData.GetJsonTokenType(index)}");
                charsWritten = 0;
                return false;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string IJsonDocument.GetPropertyRawValueAsString(int valueIndex)
    {
        CheckNotDisposed();

        // The property name is stored one row before the value
        DbRow row = _parsedData.Get(valueIndex - DbRow.Size);
        Debug.Assert(row.TokenType == JsonTokenType.PropertyName);

        // Subtract one for the open quote.
        int start = row.LocationOrIndex - 1;
        int end;

        row = _parsedData.Get(valueIndex);

        if (row.IsSimpleValue)
        {
            end = row.LocationOrIndex + row.SizeOrLengthOrPropertyMapIndex;

            // If the value was a string, pick up the terminating quote.
            if (row.TokenType == JsonTokenType.String)
            {
                end++;
            }

            return JsonReaderHelper.TranscodeHelper(_utf8Json.Slice(start, end - start).Span);
        }

        int endElementIdx = valueIndex + GetDbSizeUnsafe(valueIndex, includeEndElement: false);
        row = _parsedData.Get(endElementIdx);
        end = row.LocationOrIndex + row.SizeOrLengthOrPropertyMapIndex;
        return JsonReaderHelper.TranscodeHelper(_utf8Json.Slice(start, end - start).Span);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> fragment, int index, out TValue value)
    {
        CheckNotDisposed();

        if (TryResolveJsonPointerUnsafe(fragment, index, out int valueIndex))
        {
#if NET
            value = TValue.CreateInstance(this, valueIndex);
#else
            value = JsonElementHelpers.CreateInstance<TValue>(this, valueIndex);
#endif
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Creates a new parsed JSON document from UTF-8 JSON data without renting memory from pools.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON data to parse.</param>
    /// <param name="options">Optional reader options for parsing.</param>
    /// <returns>A new parsed JSON document instance.</returns>
    /// <remarks>
    /// This method creates a non-disposable document instance that does not use pooled memory,
    /// making it suitable for scenarios where the document needs to be retained long-term
    /// without memory pool pressure.
    /// </remarks>
    internal static ParsedJsonDocument<T> ParseUnrented(ReadOnlyMemory<byte> utf8Json, JsonReaderOptions? options = null)
    {
        var db = MetadataDb.CreateRented(utf8Json.Length, convertToAlloc: true);
        var stack = new StackRowStack(JsonDocumentOptions.DefaultMaxDepth * StackRow.Size);
        try
        {
            Parse(utf8Json.Span, options ?? default, ref db, ref stack);
        }
        finally
        {
            stack.Dispose();
        }

        db.CompleteAllocations();

        return new ParsedJsonDocument<T>(utf8Json, db, isDisposable: false);
    }

    /// <summary>
    /// Creates a deep clone of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element to clone.</param>
    /// <returns>A new element that is a deep copy of the original.</returns>
    /// <remarks>
    /// The cloned element creates its own document instance with a copy of the relevant
    /// metadata and JSON data, making it independent of the original document.
    /// </remarks>
    private T CloneElement(int index)
    {
        CheckNotDisposed();

        int endIndex = index + GetDbSizeUnsafe(index, true);
        MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
        ReadOnlyMemory<byte> segmentCopy = GetRawValueUnsafe(index, includeQuotes: true).ToArray();

        ParsedJsonDocument<T> newDocument =
            new(
                segmentCopy,
                newDb,
                extraRentedArrayPoolBytes: null,
                extraPooledByteBufferWriter: null,
                isDisposable: false);

        return newDocument.RootElement;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    JsonElement IJsonDocument.CloneElement(int index)
    {
        return JsonElement.From(CloneElement(index));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    TElement IJsonDocument.CloneElement<TElement>(int index)
    {
        T element = CloneElement(index);
#if NET
        return TElement.CreateInstance(element.ParentDocument, element.ParentDocumentIndex);
#else
        return JsonElementHelpers.CreateInstance<TElement>(element.ParentDocument, element.ParentDocumentIndex);
#endif
    }

    /// <inheritdoc />
    void IJsonDocument.WriteElementTo(
        int index,
        Utf8JsonWriter writer)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        switch (row.TokenType)
        {
            case JsonTokenType.StartObject:
                writer.WriteStartObject();
                WriteComplexElement(index, writer);
                return;

            case JsonTokenType.StartArray:
                writer.WriteStartArray();
                WriteComplexElement(index, writer);
                return;

            case JsonTokenType.String:
            {
                using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(index, JsonTokenType.String);
                writer.WriteStringValue(unescaped.Span);
            }

            return;

            case JsonTokenType.Number:
                writer.WriteNumberValue(_utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex).Span);
                return;

            case JsonTokenType.True:
                writer.WriteBooleanValue(value: true);
                return;

            case JsonTokenType.False:
                writer.WriteBooleanValue(value: false);
                return;

            case JsonTokenType.Null:
                writer.WriteNullValue();
                return;
        }

        Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
    }

    private void WriteComplexElement(int index, Utf8JsonWriter writer)
    {
        int endIndex = index + GetDbSizeUnsafe(index, true);

        for (int i = index + DbRow.Size; i < endIndex; i += DbRow.Size)
        {
            DbRow row = _parsedData.Get(i);

            // All of the types which don't need the value span
            switch (row.TokenType)
            {
                case JsonTokenType.String:
                {
                    using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(i, JsonTokenType.String);
                    writer.WriteStringValue(unescaped.Span);
                }

                continue;
                case JsonTokenType.Number:
                    writer.WriteNumberValue(_utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex).Span);
                    continue;
                case JsonTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    continue;
                case JsonTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    continue;
                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    continue;
                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    continue;
                case JsonTokenType.EndObject:
                    writer.WriteEndObject();
                    continue;
                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    continue;
                case JsonTokenType.EndArray:
                    writer.WriteEndArray();
                    continue;
                case JsonTokenType.PropertyName:
                {
                    using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(i, JsonTokenType.PropertyName);
                    writer.WritePropertyName(unescaped.Span);
                }

                continue;
            }

            Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
        }
    }

    /// <inheritdoc />
    void IJsonDocument.WritePropertyName(int index, Utf8JsonWriter writer)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index - DbRow.Size);
        Debug.Assert(row.TokenType == JsonTokenType.PropertyName);
        writer.WritePropertyName(_utf8Json.Slice(row.LocationOrIndex, row.SizeOrLengthOrPropertyMapIndex).Span);
    }

    /// <summary>
    /// Parses UTF-8 JSON data into a metadata database representation.
    /// </summary>
    /// <param name="utf8JsonSpan">The UTF-8 JSON data to parse.</param>
    /// <param name="readerOptions">Options for the JSON reader.</param>
    /// <param name="database">The metadata database to populate with parsing results.</param>
    /// <param name="stack">The stack for tracking nested JSON structures during parsing.</param>
    /// <remarks>
    /// This method performs the core JSON parsing logic, building up a metadata database
    /// that represents the structure of the JSON document. It tracks arrays, objects,
    /// and their nesting levels to create an efficient representation for element access.
    /// </remarks>
    internal static void Parse(
        ReadOnlySpan<byte> utf8JsonSpan,
        JsonReaderOptions readerOptions,
        ref MetadataDb database,
        ref StackRowStack stack)
    {
        bool inArray = false;
        int arrayItemsOrPropertyCount = 0;
        int numberOfRowsForMembers = 0;
        int numberOfRowsForValues = 0;

        Utf8JsonReader reader = new(
            utf8JsonSpan,
            isFinalBlock: true,
            new JsonReaderState(options: readerOptions));

        while (reader.Read())
        {
            JsonTokenType tokenType = reader.TokenType;

            // Since the input payload is contained within a Span,
            // token start index can never be larger than int.MaxValue (i.e. utf8JsonSpan.Length).
            Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
            int tokenStart = (int)reader.TokenStartIndex;

            if (tokenType == JsonTokenType.StartObject)
            {
                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                numberOfRowsForValues++;
                database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForMembers + 1);
                stack.Push(row);
                arrayItemsOrPropertyCount = 0;
                numberOfRowsForMembers = 0;
            }
            else if (tokenType == JsonTokenType.EndObject)
            {
                int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartObject);

                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                database.SetLength(rowIndex, arrayItemsOrPropertyCount);

                int newRowIndex = database.Length;
                database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                database.SetNumberOfRows(rowIndex, numberOfRowsForMembers);
                database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                StackRow row = stack.Pop();
                arrayItemsOrPropertyCount = row.SizeOrLength;
                numberOfRowsForMembers += row.NumberOfRows;
            }
            else if (tokenType == JsonTokenType.StartArray)
            {
                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                numberOfRowsForMembers++;
                database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1);
                stack.Push(row);
                arrayItemsOrPropertyCount = 0;
                numberOfRowsForValues = 0;
            }
            else if (tokenType == JsonTokenType.EndArray)
            {
                int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartArray);

                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                database.SetLength(rowIndex, arrayItemsOrPropertyCount);
                database.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                // If the array item count is (e.g.) 12 and the number of rows is (e.g.) 13
                // then the extra row is just this EndArray item, so the array was made up
                // of simple values.
                // If the off-by-one relationship does not hold, then one of the values was
                // more than one row, making it a complex object.
                // This check is similar to tracking the start array and painting it when
                // StartObject or StartArray is encountered, but avoids the mixed state
                // where "UnknownSize" implies "has complex children".
                if ((uint)(arrayItemsOrPropertyCount + 1) != (uint)numberOfRowsForValues)
                {
                    database.SetHasComplexChildren(rowIndex);
                }

                int newRowIndex = database.Length;
                database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                StackRow row = stack.Pop();
                arrayItemsOrPropertyCount = row.SizeOrLength;
                numberOfRowsForValues += row.NumberOfRows;
            }
            else if (tokenType == JsonTokenType.PropertyName)
            {
                numberOfRowsForValues++;
                numberOfRowsForMembers++;
                arrayItemsOrPropertyCount++;

                // Adding 1 to skip the start quote will never overflow
                Debug.Assert(tokenStart < int.MaxValue);

                database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                if (reader.ValueIsEscaped)
                {
                    database.SetHasComplexChildren(database.Length - DbRow.Size);
                }

                Debug.Assert(!inArray);
            }
            else
            {
                Debug.Assert(tokenType >= JsonTokenType.String && tokenType <= JsonTokenType.Null);
                numberOfRowsForValues++;
                numberOfRowsForMembers++;

                if (inArray)
                {
                    arrayItemsOrPropertyCount++;
                }

                if (tokenType == JsonTokenType.String)
                {
                    // Adding 1 to skip the start quote will never overflow
                    Debug.Assert(tokenStart < int.MaxValue);

                    database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                    if (reader.ValueIsEscaped)
                    {
                        database.SetHasComplexChildren(database.Length - DbRow.Size);
                    }
                }
                else
                {
                    database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                }
            }

            inArray = reader.IsInArray;
        }

        Debug.Assert(reader.BytesConsumed == utf8JsonSpan.Length);
        database.CompleteAllocations();
    }

    private void CheckNotDisposed()
    {
        if (_utf8Json.IsEmpty)
        {
            ThrowHelper.ThrowObjectDisposedException_JsonDocument();
        }
    }

    private static void CheckSupportedOptions(
        JsonReaderOptions readerOptions,
        string paramName)
    {
        // Since these are coming from a valid instance of Utf8JsonReader, the JsonReaderOptions must already be valid
        Debug.Assert(readerOptions.CommentHandling >= 0 && readerOptions.CommentHandling <= JsonCommentHandling.Allow);

        if (readerOptions.CommentHandling == JsonCommentHandling.Allow)
        {
            throw new ArgumentException(SR.JsonDocumentDoesNotSupportComments, paramName);
        }
    }

    /// <inheritdoc />
    int IJsonDocument.BuildRentedMetadataDb(int index, JsonWorkspace workspace, out byte[] rentedBacking)
    {
        CheckNotDisposed();

        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);

        DbRow row = _parsedData.Get(index);
        int estimatedRowCount;
        if (row.IsSimpleValue)
        {
            // Simple values are a single row.
            estimatedRowCount = 1;
        }
        else
        {
            // Number of rows + end row.
            estimatedRowCount = row.NumberOfRows + 1;
        }

        var db = MetadataDb.CreateRented(estimatedRowCount * DbRow.Size, false);
        AppendElement(index, ref db, workspaceDocumentIndex);

        // Note we just orphan this db instance, as we are passing the underlying
        // byte array off to the dynamically created document that wants it.
        return db.TakeOwnership(out rentedBacking);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
    {
        CheckNotDisposed();

        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        AppendElement(index, ref db, workspaceDocumentIndex);
    }

    /// <inheritdoc />
    int IJsonDocument.GetHashCode(int index)
    {
        CheckNotDisposed();
        return GetHashCodeUnsafe(index);
    }

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index) => GetRawSimpleValueUnsafe(index);

    private int AppendElement(int index, ref MetadataDb db, int workspaceDocumentIndex)
    {
        switch (_parsedData.GetJsonTokenType(index))
        {
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
            case JsonTokenType.Number:
            case JsonTokenType.String:
            case JsonTokenType.PropertyName:
                DbRow row = _parsedData.Get(index);
                db.AppendExternal(row.TokenType, index, row.RawSizeOrLength, workspaceDocumentIndex);
                return 1;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                return ProcessComplexObject(index, ref db, workspaceDocumentIndex);
        }

        Debug.Fail($"Unexpected encounter with JsonTokenType {_parsedData.GetJsonTokenType(index)}");
        return -1;
    }

    private int ProcessComplexObject(int index, ref MetadataDb db, int workspaceDocumentIndex)
    {
        int count = 2;
        DbRow complexObjectRow = _parsedData.Get(index);
        db.AppendExternal(complexObjectRow.TokenType, index, complexObjectRow.RawSizeOrLength, workspaceDocumentIndex);

        int endIndex = index + GetDbSizeUnsafe(index, false);

        for (int i = index + DbRow.Size; i < endIndex; i += DbRow.Size)
        {
            int rowsAdded = AppendElement(i, ref db, workspaceDocumentIndex);
            count += rowsAdded;
            i += (rowsAdded - 1) * DbRow.Size;
        }

        complexObjectRow = _parsedData.Get(endIndex);
        int entityLength = complexObjectRow.HasPropertyMap ? GetLengthOfEndToken(complexObjectRow.SizeOrLengthOrPropertyMapIndex) : complexObjectRow.RawSizeOrLength;
        db.AppendExternal(complexObjectRow.TokenType, index, entityLength, complexObjectRow.NumberOfRows);
        return count;
    }
}