// <copyright file="JsonDocumentBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;

namespace Corvus.Text.Json;

/// <summary>
/// A mutable JSON document builder that provides functionality to construct and modify JSON documents.
/// </summary>
/// <typeparam name="T">The type of mutable JSON element this builder works with.</typeparam>
[CLSCompliant(false)]
public sealed partial class JsonDocumentBuilder<T> : JsonDocument, IMutableJsonDocument
    where T : struct, IMutableJsonElement<T>
{
    private readonly JsonWorkspace _workspace;

    private int _parentWorkspaceIndex = -1;

    private ulong _version = 0;

    internal JsonDocumentBuilder(JsonWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool IJsonDocument.IsDisposable => true;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int IMutableJsonDocument.ParentWorkspaceIndex => _parentWorkspaceIndex;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ulong IMutableJsonDocument.Version => _version;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonWorkspace IMutableJsonDocument.Workspace => _workspace;

    /// <summary>
    /// Gets the root element of the JSON document.
    /// </summary>
    /// <value>The mutable root element of the document.</value>
    /// <exception cref="ObjectDisposedException">Thrown when the document has been disposed.</exception>
    public T RootElement
    {
        get
        {
            CheckNotDisposed();
#if NET
            return T.CreateInstance(this, 0);
#else
            return JsonElementHelpers.CreateInstance<T>(this, 0);
#endif
        }
    }

    /// <summary>
    /// Write the document into the provided writer as a JSON value.
    /// </summary>
    /// <param name="writer"></param>
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

    internal void Initialize<TElement>(TElement sourceElement, int parentWorkspaceIndex, bool convertToAlloc)
        where TElement : struct, IJsonElement<TElement>
    {
        _parentWorkspaceIndex = parentWorkspaceIndex;
        sourceElement.CheckValidInstance();
        int metadataDbLength = sourceElement.ParentDocument.BuildRentedMetadataDb(sourceElement.ParentDocumentIndex, _workspace, out byte[] metadataDbBytes);
        _parsedData = MetadataDb.CreateRented(metadataDbBytes, metadataDbLength, convertToAlloc);
    }

    internal void Initialize(int parentWorkspaceIndex, int initialElementCount, int initialValueBufferSize)
    {
        _parentWorkspaceIndex = parentWorkspaceIndex;

        // Use unsigned comparison for efficient parameter validation
        if ((uint)initialElementCount < int.MaxValue)
        {
            _parsedData = MetadataDb.CreateRented(initialElementCount * DbRow.Size, convertToAlloc: false);
        }

        // Use unsigned comparison for efficient parameter validation
        if ((uint)initialValueBufferSize > 0)
        {
            _valueBacking = ArrayPool<byte>.Shared.Rent(initialValueBufferSize);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_parentWorkspaceIndex == -1)
        {
            return;
        }

        DisposeCore();

        _parentWorkspaceIndex = -1;
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
        CheckNotDisposed();
        EnsurePropertyMapUnsafe(index);
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
    JsonElement.Mutable IMutableJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex)
    {
        CheckNotDisposed();

        return new JsonElement.Mutable(this, GetArrayIndexElementUnsafe(currentIndex, arrayIndex));
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
    void IMutableJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IMutableJsonDocument parentDocument, out int parentDocumentIndex)
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

    /// <summary>
    /// Gets the database size for an element without bounds checking.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeEndElement">Whether to include the end element in the size calculation.</param>
    /// <returns>The database size of the element.</returns>
    protected override int GetDbSizeUnsafe(int index, bool includeEndElement)
    {
        DbRow row = _parsedData.Get(index);

        // If the row is from an external document, we defer to that
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.GetDbSize(row.LocationOrIndex, includeEndElement);
        }

        return base.GetDbSizeUnsafe(index, includeEndElement);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes)
    {
        CheckNotDisposed();
        return GetRawValueUnsafe(index, includeQuotes);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.InsertAndDispose(int complexObjectStartIndex, int index, ref ComplexValueBuilder cvb)
    {
        CheckNotImmutable();
        _version++;
        cvb.InsertAndDispose(complexObjectStartIndex, index, ref _parsedData);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.SetAndDispose(ref ComplexValueBuilder cvb)
    {
        CheckNotImmutable();
        _version++;
        cvb.SetAndDispose(ref _parsedData);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.OverwriteAndDispose(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, ref ComplexValueBuilder cvb)
    {
        CheckNotImmutable();
        _version++;
        cvb.OverwriteAndDispose(complexObjectStartIndex, startIndex, endIndex, memberCountToReplace, ref _parsedData);
    }

    /// <summary>
    /// Removes a range of values from the document.
    /// </summary>
    /// <param name="complexObjectStartIndex">The start index of the complex object.</param>
    /// <param name="startIndex">The start index of the range to remove.</param>
    /// <param name="endIndex">The end index of the range to remove.</param>
    /// <param name="membersToRemove">The number of members to remove.</param>
    /// <remarks>
    /// This is similar to <see cref="OverwriteAndDispose"/>, but it does not replace the
    /// values that are removed. Instead, it simply removes the specified range of members
    /// from the document, effectively shifting subsequent members up.
    /// </remarks>
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.RemoveRange(int complexObjectStartIndex, int startIndex, int endIndex, int membersToRemove)
    {
        CheckNotImmutable();
        _version++;

        // Use ReplaceRowsInComplexObject with 0 insertions to effectively remove the range
        _parsedData.ReplaceRowsInComplexObject(this, complexObjectStartIndex, startIndex, endIndex, membersToRemove, 0, 0);
    }

    private RawUtf8JsonString GetRawValueUnsafe(int index, bool includeQuotes)
    {
        DbRow row = _parsedData.Get(index);

        // If the row is from an external document, we defer to that
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawValue(row.LocationOrIndex, includeQuotes);
        }

        // So we do have a dynamic value. If it is a simple value that means it's
        // a local dynamic value.
        if (row.IsSimpleValue)
        {
            return new(ReadRawSimpleDynamicValue(row.LocationOrIndex, includeQuotes));
        }

        // We have a complex value and it is not a simple slice of a parent
        // buffer somewhere, so we have to render it out to return it.
        // The length of our parsed data is a good guess at the initial size for the buffer (on the usual 12 bytes per token,
        // 12 bytes per row heuristic). It will reallocate if needs be, anyway.
        // In an ideal world, you are not doing this too often; in general you will be acquiring simple values
        // rather than complex ones - except for ToString() and so forth which are not intended to be high-performance
        // scenarios (not least because they allocate strings!)
        Utf8JsonWriter writer = _workspace.RentWriterAndBuffer(_parsedData.Length, out IByteBufferWriter bufferWriter);
        try
        {
            WriteComplexElementToUnsafe(index, writer);
            writer.Flush();
            int length = bufferWriter.WrittenSpan.Length;
            byte[] additionalRentedBytes = ArrayPool<byte>.Shared.Rent(length);
            bufferWriter.WrittenSpan.CopyTo(additionalRentedBytes.AsSpan());
            return new(additionalRentedBytes.AsMemory(0, length));
        }
        finally
        {
            _workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
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

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex, includeQuotes);
        }

        return ReadRawSimpleDynamicValue(row.LocationOrIndex, includeQuotes);
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

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex);
        }

        return ReadRawSimpleDynamicValue(row.LocationOrIndex);
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        result = row.HasComplexChildren
            ? JsonReaderHelper.GetUnescapedString(segment)
            : JsonReaderHelper.TranscodeHelper(segment);
        return true;
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        return row.HasComplexChildren
            ? JsonReaderHelper.GetUnescapedString(segment)
            : JsonReaderHelper.TranscodeHelper(segment);
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

        ReadOnlyMemory<byte> segment = GetRawSimpleValueUnsafe(index, false);

        if (row.HasComplexChildren)
        {
            byte[] rentedBytes = ArrayPool<byte>.Shared.Rent(segment.Length);
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (row.HasComplexChildren)
        {
            int utf8Length = segment.Length;
            byte[]? rentedUtf8 = null;

            Span<byte> utf8Unescaped = utf8Length <= JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (rentedUtf8 = ArrayPool<byte>.Shared.Rent(utf8Length));

            try
            {
                JsonReaderHelper.Unescape(segment, utf8Unescaped, out int utf8Written);
                utf8Unescaped = utf8Unescaped.Slice(0, utf8Written);

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

        ReadOnlyMemory<byte> segment = GetRawSimpleValueUnsafe(index, false);

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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out byte tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out short tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out ushort tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out int tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out uint tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out long tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out ulong tmp, out int consumed) &&
            consumed == segment.Length)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed) &&
            segment.Length == bytesConsumed)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed) &&
            segment.Length == bytesConsumed)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        if (Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed) &&
            segment.Length == bytesConsumed)
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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        return BigInteger.TryParse(segment, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out BigNumber value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.Number, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

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

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDate value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetTime value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out LocalDate value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Period value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

#if NET
    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateOnly value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out TimeOnly value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;
        return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
    }
#endif

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Guid value)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        CheckExpectedType(JsonTokenType.String, row.TokenType);

        ReadOnlySpan<byte> segment = GetRawSimpleValueUnsafe(index, false).Span;

        // Use unsigned comparison for efficient length check
        if ((uint)segment.Length > (uint)JsonConstants.MaximumEscapedGuidLength)
        {
            value = default;
            return false;
        }

        // Segment needs to be unescaped
        if (row.HasComplexChildren)
        {
            return JsonReaderHelper.TryGetEscapedGuid(segment, out value);
        }

        Debug.Assert(segment.IndexOf(JsonConstants.BackSlash) == -1);

        if (segment.Length == JsonConstants.MaximumFormatGuidLength
            && Utf8Parser.TryParse(segment, out Guid tmp, out _, 'D'))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    string IJsonDocument.GetRawValueAsString(int index)
    {
        CheckNotDisposed();

        return GetRawValueAsStringUnsafe(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetRawValueAsStringUnsafe(int index)
    {
        using RawUtf8JsonString segment = GetRawValueUnsafe(index, includeQuotes: true);
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
    string IJsonDocument.GetPropertyRawValueAsString(int valueIndex)
    {
        CheckNotDisposed();

        // The property name is stored one row before the value
        int propertyNameIndex = valueIndex - DbRow.Size;
        DbRow row = _parsedData.Get(propertyNameIndex);
        Debug.Assert(row.TokenType == JsonTokenType.PropertyName);

        DbRow valueRow = _parsedData.Get(valueIndex);

        // These are not values in our document,
        // but they are both in the same external document,
        // so we can just defer processing to that document
        if (row.FromExternalDocument && valueRow.FromExternalDocument &&
            row.WorkspaceDocumentId == valueRow.WorkspaceDocumentId)
        {
            IJsonDocument document = _workspace.GetDocument(valueRow.WorkspaceDocumentId);
            return document.GetPropertyRawValueAsString(valueRow.LocationOrIndex);
        }

        ReadOnlyMemory<byte> name = GetRawSimpleValueUnsafe(propertyNameIndex, includeQuotes: true);
        using RawUtf8JsonString value = GetRawValueUnsafe(valueIndex, includeQuotes: true);

        // quoted name, quoted value and a colon
        int length = name.Length + value.Span.Length + 1;
        byte[]? buffer = null;

        // Use unsigned comparison for efficient stackalloc threshold check
        Span<byte> propertySpan =
            (uint)length < (uint)JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (buffer = ArrayPool<byte>.Shared.Rent(length)).AsSpan();

        try
        {
            name.Span.CopyTo(propertySpan);
            propertySpan[name.Length] = JsonConstants.Colon; // colon between name and value
            value.Span.CopyTo(propertySpan.Slice(name.Length + 1));
            propertySpan = propertySpan.Slice(0, length);
            return JsonReaderHelper.TranscodeHelper(propertySpan);
        }
        finally
        {
            if (buffer is not null)
            {
                propertySpan.Clear();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <inheritdoc />
    JsonElement IJsonDocument.CloneElement(int index)
    {
        return CloneElement<JsonElement>(index);
    }

    /// <inheritdoc />
    TElement IJsonDocument.CloneElement<TElement>(int index)
    {
        return CloneElement<TElement>(index);
    }

    private TElement CloneElement<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        // If the row is from an external document, we defer to that
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.CloneElement<TElement>(row.LocationOrIndex);
        }

        using RawUtf8JsonString rawUtf8Json = GetRawValueUnsafe(index, includeQuotes: true);

        ReadOnlyMemory<byte> segmentCopy;
        segmentCopy = rawUtf8Json.Span.ToArray();

        var newDocument =
            ParsedJsonDocument<TElement>.ParseUnrented(segmentCopy);

        return newDocument.RootElement;
    }

    /// <inheritdoc />
    void IJsonDocument.WriteElementTo(
        int index,
        Utf8JsonWriter writer)
    {
        CheckNotDisposed();

        // Need to look at the element types/deferrals etc
        WriteElementToUnsafe(index, writer);
    }

    private void WriteElementToUnsafe(
        int index,
        Utf8JsonWriter writer)
    {
        DbRow row = _parsedData.Get(index);

        switch (row.TokenType)
        {
            case JsonTokenType.StartObject:
                WriteComplexElementToUnsafe(index, writer);
                return;

            case JsonTokenType.StartArray:
                WriteComplexElementToUnsafe(index, writer);
                return;

            case JsonTokenType.String:
                if (row.HasComplexChildren)
                {
                    using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(index, JsonTokenType.String);
                    writer.WriteStringValue(unescaped.Span);
                }
                else
                {
                    writer.WriteStringValue(GetRawSimpleValueUnsafe(index, false).Span);
                }

                return;

            case JsonTokenType.Number:
                writer.WriteNumberValue(GetRawSimpleValueUnsafe(index, includeQuotes: false).Span);
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

    private void WriteComplexElementToUnsafe(
        int index,
        Utf8JsonWriter writer)
    {
        int endIndex = index + GetDbSizeUnsafe(index, true);

        // Use unsigned comparison for optimized bounds checking
        for (int i = index; (uint)i < (uint)endIndex; i += DbRow.Size)
        {
            DbRow row = _parsedData.Get(i);

            // All of the types which don't need the value span
            switch (row.TokenType)
            {
                case JsonTokenType.String:
                    if (row.HasComplexChildren)
                    {
                        using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(i, JsonTokenType.String);
                        writer.WriteStringValue(unescaped.Span);
                    }
                    else
                    {
                        writer.WriteStringValue(GetRawSimpleValueUnsafe(i, false).Span);
                    }

                    continue;
                case JsonTokenType.Number:
                    writer.WriteNumberValue(GetRawSimpleValueUnsafe(i, includeQuotes: false).Span);
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
                    if (row.HasComplexChildren)
                    {
                        using UnescapedUtf8JsonString unescaped = GetUtf8JsonStringUnsafe(i, JsonTokenType.PropertyName);
                        writer.WritePropertyName(unescaped.Span);
                    }
                    else
                    {
                        writer.WritePropertyName(GetRawSimpleValueUnsafe(i, false).Span);
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

        Debug.Assert(_parsedData.Get(index - DbRow.Size).TokenType == JsonTokenType.PropertyName);
        WritePropertyNameUnsafe(index - DbRow.Size, writer);
    }

    private void WritePropertyNameUnsafe(int index, Utf8JsonWriter writer)
    {
        bool rowHasComplexChildren = _parsedData.Get(index - DbRow.Size).HasComplexChildren;
        if (rowHasComplexChildren)
        {
            ArraySegment<byte> rented = default;

            try
            {
                writer.WritePropertyName(UnescapeString(index, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }
        else
        {
            writer.WritePropertyName(GetRawSimpleValueUnsafe(index, false).Span);
        }
    }

    private ReadOnlySpan<byte> UnescapeString(int index, out ArraySegment<byte> rented)
    {
        Debug.Assert(_parsedData.Get(index).TokenType is JsonTokenType.String or JsonTokenType.PropertyName);
        ReadOnlySpan<byte> text = GetRawSimpleValueUnsafe(index, false).Span;
        int length = text.Length;
        byte[] rent = ArrayPool<byte>.Shared.Rent(length);
        JsonReaderHelper.Unescape(text, rent, out int written);
        rented = new ArraySegment<byte>(rent, 0, written);
        return rented.AsSpan();
    }

    private static void ClearAndReturn(ArraySegment<byte> rented)
    {
        if (rented.Array != null)
        {
            rented.AsSpan().Clear();
            ArrayPool<byte>.Shared.Return(rented.Array);
        }
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
#if NET
            value = TElement.CreateInstance(this, valueIndex);
#else
            value = JsonElementHelpers.CreateInstance<TElement>(this, valueIndex);
#endif
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement.Mutable value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement.Mutable(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement.Mutable value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement.Mutable(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIdx)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            elementParent = this;
            elementIdx = valueIndex;
            return true;
        }

        elementIdx = -1;
        elementParent = null;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIdx)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            elementParent = this;
            elementIdx = valueIndex;
            return true;
        }

        elementIdx = -1;
        elementParent = null;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
#if NET
            value = TElement.CreateInstance(this, valueIndex);
#else
            value = JsonElementHelpers.CreateInstance<TElement>(this, valueIndex);
#endif
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    int IJsonDocument.GetHashCode(int index)
    {
        CheckNotDisposed();
        return GetHashCodeUnsafe(index);
    }

    /// <inheritdoc />
    int IJsonDocument.BuildRentedMetadataDb(int index, JsonWorkspace workspace, out byte[] rentedBacking)
    {
        CheckNotDisposed();
        CheckImmutable();

        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);

        DbRow row = _parsedData.Get(index);

        // If the row is from an external document, we defer to that
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.BuildRentedMetadataDb(row.LocationOrIndex, workspace, out rentedBacking);
        }

        int estimatedRowCount;
        if (row.IsSimpleValue)
        {
            // Simple values are a single row.
            estimatedRowCount = 1;
        }
        else
        {
            // Number of rows + end row.
            estimatedRowCount = GetDbSizeUnsafe(index, true);
        }

        var db = MetadataDb.CreateRented(estimatedRowCount * DbRow.Size, false);
        AppendLocalElement(index, workspace, ref db, workspaceDocumentIndex);

        // Note we just orphan this db instance, as we are passing the underlying
        // byte array off to the dynamically created document that wants it.
        return db.TakeOwnership(out rentedBacking);
    }

    /// <inheritdoc />
    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
    {
        CheckNotDisposed();
        CheckNotImmutable();

        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        AppendElement(index, workspace, ref db, workspaceDocumentIndex);
    }

    private void AppendElement(int index, JsonWorkspace workspace, ref MetadataDb db, int workspaceDocumentIndex)
    {
        DbRow row = _parsedData.Get(index);

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            document.AppendElementToMetadataDb(row.LocationOrIndex, workspace, ref db);
            return;
        }

        switch (row.TokenType)
        {
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
            case JsonTokenType.Number:
            case JsonTokenType.String:
            case JsonTokenType.PropertyName:
                db.AppendExternal(row.TokenType, index, row.RawSizeOrLength, workspaceDocumentIndex);
                return;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                ProcessComplexObject(index, workspace, ref db, workspaceDocumentIndex);
                return;
        }

        Debug.Fail($"Unexpected encounter with JsonTokenType {_parsedData.GetJsonTokenType(index)}");
    }

    private void ProcessComplexObject(int index, JsonWorkspace workspace, ref MetadataDb db, int workspaceDocumentIndex)
    {
        DbRow complexObjectRow = _parsedData.Get(index);
        db.AppendExternal(complexObjectRow.TokenType, index, complexObjectRow.RawSizeOrLength, workspaceDocumentIndex);

        int endIndex = index + GetDbSizeUnsafe(index, false);

        // Use unsigned comparison for optimized bounds checking
        for (int i = index + DbRow.Size; (uint)i < (uint)endIndex; i += DbRow.Size)
        {
            int currentLength = db.Length;
            AppendElement(i, workspace, ref db, workspaceDocumentIndex);
            i += db.Length - currentLength - DbRow.Size;
        }

        complexObjectRow = _parsedData.Get(endIndex);
        int entityLength = complexObjectRow.HasPropertyMap ? GetLengthOfEndToken(complexObjectRow.SizeOrLengthOrPropertyMapIndex) : complexObjectRow.RawSizeOrLength;
        db.AppendExternal(complexObjectRow.TokenType, index, entityLength, workspaceDocumentIndex);
    }

    private void AppendLocalElement(int index, JsonWorkspace workspace, ref MetadataDb db, int workspaceDocumentIndex)
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
                return;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                ProcessComplexObject(index, workspace, ref db, workspaceDocumentIndex);
                return;
        }

        Debug.Fail($"Unexpected encounter with JsonTokenType {_parsedData.GetJsonTokenType(index)}");
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset)
    {
        line = 0;
        charOffset = 0;
        lineByteOffset = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset)
    {
        line = 0;
        charOffset = 0;
        lineByteOffset = 0;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line)
    {
        line = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line)
    {
        line = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckNotDisposed()
    {
        // Use unsigned comparison for efficient disposal check
        if ((uint)_parentWorkspaceIndex >= (uint)int.MaxValue)
        {
            ThrowHelper.ThrowObjectDisposedException_JsonDocument();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckNotImmutable()
    {
        if (_isImmutable)
        {
            ThrowHelper.ThrowInvalidOperationException(SR.CannotModifyAnImmutableDocument);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckImmutable()
    {
        if (!_isImmutable)
        {
            ThrowHelper.ThrowInvalidOperationException(SR.CannotCreateBuilderFromMutableDocument);
        }
    }

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index) => GetRawSimpleValueUnsafe(index);

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(ref MetadataDb parsedData, int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out int valueIndex)
    {
        CheckNotDisposed();
        return TryGetNamedPropertyValueIndexUnsafe(ref parsedData, startIndex, endIndex, propertyName, out valueIndex);
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<byte> propertyName, out int valueIndex)
    {
        CheckNotDisposed();
        return TryGetNamedPropertyValueIndexUnsafe(index, propertyName, out valueIndex);
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<char> propertyName, out int valueIndex)
    {
        CheckNotDisposed();
        return TryGetNamedPropertyValueIndexUnsafe(index, propertyName, out valueIndex);
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreBooleanValue(bool value) => StoreBooleanValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreNullValue() => StoreNullValue();

    /// <inheritdoc />
    int IMutableJsonDocument.StoreRawNumberValue(ReadOnlySpan<byte> value) => StoreRawNumberValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<byte> value, out bool requiredEscaping) => EscapeAndStoreRawStringValue(value, out requiredEscaping, _workspace.Options.Encoder);

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping) => EscapeAndStoreRawStringValue(value, out requiredEscaping, _workspace.Options.Encoder);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreRawStringValue(ReadOnlySpan<byte> value) => StoreRawStringValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Guid value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in DateTime value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in DateTimeOffset value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetDateTime value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetDate value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetTime value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in LocalDate value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in Period value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(sbyte value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(byte value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(int value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(uint value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(long value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(ulong value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(short value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(ushort value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(float value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(double value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(decimal value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in BigNumber value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in BigInteger value) => StoreValue(value);

#if NET

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Int128 value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(UInt128 value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Half value) => StoreValue(value);

#endif

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
                RawUtf8JsonString segment = GetRawValueUnsafe(index, includeQuotes: true);
                if (segment.Span.TryCopyTo(destination))
                {
                    bytesWritten = segment.Span.Length;
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
                using RawUtf8JsonString rawString = GetRawValueUnsafe(index, includeQuotes: true);
                return JsonReaderHelper.TryTranscode(rawString.Span, destination, out charsWritten);
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
}