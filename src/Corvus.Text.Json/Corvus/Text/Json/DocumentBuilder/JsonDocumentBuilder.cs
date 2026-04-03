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

    // When > 0, _valueBacking[0.._rawJsonLength) contains the raw UTF-8 JSON input bytes.
    // MetadataDb rows created by ParseTokens store offsets into this region (ParsedJsonDocument-style).
    // Mutated values go into _valueBacking at _valueOffset (>= _rawJsonLength) using DynamicValue headers.
    private int _rawJsonLength;

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

    /// <summary>
    /// Creates a snapshot of this builder's current state. The snapshot can be used to
    /// cheaply restore the builder to this state via <see cref="Restore"/>
    /// without re-traversing the source document.
    /// </summary>
    /// <returns>
    /// A <see cref="JsonDocumentBuilderSnapshot{T}"/> that holds rented copies of this
    /// builder's backing data. The caller must dispose the snapshot when it is no longer needed.
    /// </returns>
    public JsonDocumentBuilderSnapshot<T> CreateSnapshot()
    {
        CheckNotDisposed();

        // Snapshot the metadata
        int metadataLength = _parsedData.Length;
        byte[] metadataBytes = ArrayPool<byte>.Shared.Rent(metadataLength);
        _parsedData.CopyDataTo(metadataBytes);

        // Snapshot the value backing
        byte[]? valueBacking = null;
        if (_valueBacking is not null && _valueOffset > 0)
        {
            valueBacking = ArrayPool<byte>.Shared.Rent(_valueOffset);
            Buffer.BlockCopy(_valueBacking, 0, valueBacking, 0, _valueOffset);
        }

        // Snapshot the property map
        byte[]? propertyMapBacking = null;
        if (_propertyMapBacking is not null && _propertyMapOffset > 0)
        {
            propertyMapBacking = ArrayPool<byte>.Shared.Rent(_propertyMapOffset);
            Buffer.BlockCopy(_propertyMapBacking, 0, propertyMapBacking, 0, _propertyMapOffset);
        }

        // Snapshot the buckets
        int[]? bucketsBacking = null;
        if (_bucketsBacking is not null && _bucketOffset > 0)
        {
            bucketsBacking = ArrayPool<int>.Shared.Rent(_bucketOffset);
            Buffer.BlockCopy(_bucketsBacking, 0, bucketsBacking, 0, _bucketOffset * sizeof(int));
        }

        // Snapshot the entries
        byte[]? entriesBacking = null;
        if (_entriesBacking is not null && _entryOffset > 0)
        {
            entriesBacking = ArrayPool<byte>.Shared.Rent(_entryOffset);
            Buffer.BlockCopy(_entriesBacking, 0, entriesBacking, 0, _entryOffset);
        }

        return new JsonDocumentBuilderSnapshot<T>(
            metadataBytes,
            metadataLength,
            valueBacking,
            _valueOffset,
            propertyMapBacking,
            _propertyMapOffset,
            bucketsBacking,
            _bucketOffset,
            entriesBacking,
            _entryOffset);
    }

    /// <summary>
    /// Restores this builder to the state captured in <paramref name="snapshot"/>.
    /// The existing backing buffers are reused (they can only grow, never shrink),
    /// so this is a pure memcpy with no allocations.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore from.</param>
    /// <returns>This builder instance.</returns>
    public JsonDocumentBuilder<T> Restore(JsonDocumentBuilderSnapshot<T> snapshot)
    {
        CheckNotDisposed();

        // Restore metadata into the existing buffer
        _parsedData.RestoreFrom(snapshot.MetadataBytes, snapshot.MetadataLength);

        // Restore value backing
        _valueOffset = snapshot.ValueOffset;
        if (snapshot.ValueBacking is not null && snapshot.ValueOffset > 0)
        {
            Buffer.BlockCopy(snapshot.ValueBacking, 0, _valueBacking!, 0, snapshot.ValueOffset);
        }

        // Restore property map
        _propertyMapOffset = snapshot.PropertyMapOffset;
        if (snapshot.PropertyMapBacking is not null && snapshot.PropertyMapOffset > 0)
        {
            Buffer.BlockCopy(snapshot.PropertyMapBacking, 0, _propertyMapBacking!, 0, snapshot.PropertyMapOffset);
        }

        // Restore buckets
        _bucketOffset = snapshot.BucketOffset;
        if (snapshot.BucketsBacking is not null && snapshot.BucketOffset > 0)
        {
            Buffer.BlockCopy(snapshot.BucketsBacking, 0, _bucketsBacking!, 0, snapshot.BucketOffset * sizeof(int));
        }

        // Restore entries
        _entryOffset = snapshot.EntryOffset;
        if (snapshot.EntriesBacking is not null && snapshot.EntryOffset > 0)
        {
            Buffer.BlockCopy(snapshot.EntriesBacking, 0, _entriesBacking!, 0, snapshot.EntryOffset);
        }

        // Invalidate any stale element references
        _version++;

        return this;
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
    void IMutableJsonDocument.ReplaceRootAndDispose(ref ComplexValueBuilder cvb)
    {
        CheckNotImmutable();
        _version++;
        _parsedData.Dispose();
        _parsedData = default;
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.InsertSimpleValue(int complexObjectStartIndex, int targetIndex, int memberCount, JsonTokenType tokenType, int location, int sizeOrLength)
    {
        CheckNotImmutable();
        _version++;
        _parsedData.InsertRowsInComplexObject(this, complexObjectStartIndex, targetIndex, 1, memberCount);
        _parsedData.WriteRowAt(targetIndex, new DbRow(tokenType, location, sizeOrLength));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IMutableJsonDocument.OverwriteSimpleValue(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, JsonTokenType tokenType, int location, int sizeOrLength)
    {
        CheckNotImmutable();
        _version++;
        _parsedData.ReplaceRowsInComplexObject(this, complexObjectStartIndex, startIndex, endIndex, memberCountToReplace, 1, 1);
        _parsedData.WriteRowAt(startIndex, new DbRow(tokenType, location, sizeOrLength));
    }

    /// <inheritdoc />
    void IMutableJsonDocument.InsertSimpleProperty(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, JsonTokenType valueTokenType, int valueLocation, int valueSizeOrLength)
    {
        CheckNotImmutable();
        _version++;
        _parsedData.InsertRowsInComplexObject(this, complexObjectStartIndex, targetIndex, 2, memberCount);
        int nameLoc = ((IMutableJsonDocument)this).EscapeAndStoreRawStringValue(propertyName, out bool nameReqEscaping);
        _parsedData.WriteRowAt(targetIndex, new DbRow(JsonTokenType.PropertyName, nameLoc, nameReqEscaping ? -1 : 1));
        _parsedData.WriteRowAt(targetIndex + DbRow.Size, new DbRow(valueTokenType, valueLocation, valueSizeOrLength));
    }

    /// <inheritdoc />
    void IMutableJsonDocument.InsertFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, IJsonDocument sourceDocument, int sourceIndex)
    {
        CheckNotImmutable();
        _version++;
        int rowCount = sourceDocument.GetDbSize(sourceIndex, true) / DbRow.Size;
        _parsedData.InsertRowsInComplexObject(this, complexObjectStartIndex, targetIndex, rowCount, memberCount);
        sourceDocument.WriteElementToMetadataDb(sourceIndex, _workspace, ref _parsedData, targetIndex);
    }

    /// <inheritdoc />
    void IMutableJsonDocument.OverwriteFromDocument(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, IJsonDocument sourceDocument, int sourceIndex)
    {
        CheckNotImmutable();
        _version++;
        int rowCount = sourceDocument.GetDbSize(sourceIndex, true) / DbRow.Size;
        _parsedData.ReplaceRowsInComplexObject(this, complexObjectStartIndex, startIndex, endIndex, memberCountToReplace, rowCount, 1);
        sourceDocument.WriteElementToMetadataDb(sourceIndex, _workspace, ref _parsedData, startIndex);
    }

    /// <inheritdoc />
    void IMutableJsonDocument.InsertPropertyFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex)
    {
        CheckNotImmutable();
        _version++;
        int valueRowCount = sourceDocument.GetDbSize(sourceIndex, true) / DbRow.Size;
        _parsedData.InsertRowsInComplexObject(this, complexObjectStartIndex, targetIndex, valueRowCount + 1, memberCount);
        int nameLoc = ((IMutableJsonDocument)this).EscapeAndStoreRawStringValue(propertyName, out bool nameReqEscaping);
        _parsedData.WriteRowAt(targetIndex, new DbRow(JsonTokenType.PropertyName, nameLoc, nameReqEscaping ? -1 : 1));
        sourceDocument.WriteElementToMetadataDb(sourceIndex, _workspace, ref _parsedData, targetIndex + DbRow.Size);
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryReplacePropertyValue(int objectIndex, ReadOnlySpan<byte> propertyName, JsonTokenType tokenType, int location, int sizeOrLength)
    {
        CheckNotImmutable();

        if (!TryGetNamedPropertyValueIndexUnsafe(objectIndex, propertyName, out int existingValueIndex))
        {
            return false;
        }

        int existingDbSize = GetDbSizeUnsafe(existingValueIndex, true);
        _version++;
        _parsedData.ReplaceRowsInComplexObject(this, objectIndex, existingValueIndex, existingValueIndex + existingDbSize, 1, 1, 1);
        _parsedData.WriteRowAt(existingValueIndex, new DbRow(tokenType, location, sizeOrLength));
        return true;
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryReplacePropertyFromDocument(int objectIndex, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex)
    {
        CheckNotImmutable();

        if (!TryGetNamedPropertyValueIndexUnsafe(objectIndex, propertyName, out int existingValueIndex))
        {
            return false;
        }

        int existingDbSize = GetDbSizeUnsafe(existingValueIndex, true);
        int srcRowCount = sourceDocument.GetDbSize(sourceIndex, true) / DbRow.Size;
        _version++;
        _parsedData.ReplaceRowsInComplexObject(this, objectIndex, existingValueIndex, existingValueIndex + existingDbSize, 1, srcRowCount, 1);
        sourceDocument.WriteElementToMetadataDb(sourceIndex, _workspace, ref _parsedData, existingValueIndex);
        return true;
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

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToProperty(int srcValueIndex, int dstObjectIndex, ReadOnlySpan<byte> propertyName)
    {
        CheckNotImmutable();

        int srcDbSize = GetDbSizeUnsafe(srcValueIndex, true);

        if (TryGetNamedPropertyValueIndexUnsafe(dstObjectIndex, propertyName, out int existingValueIndex))
        {
            // Replace existing property value.
            int existingDbSize = GetDbSizeUnsafe(existingValueIndex, true);

            // Check for overlap between source and existing value region.
            if (srcValueIndex < existingValueIndex + existingDbSize && srcValueIndex + srcDbSize > existingValueIndex)
            {
                // Source overlaps with the region being replaced — fall back to CVB.
                var cvb = ComplexValueBuilder.Create(this, 1);
                cvb.AddItem(new JsonElement(this, srcValueIndex));
                _version++;
                cvb.OverwriteAndDispose(dstObjectIndex, existingValueIndex, existingValueIndex + existingDbSize, 1, ref _parsedData);
            }
            else
            {
                int srcRowCount = srcDbSize / DbRow.Size;
                int delta = srcDbSize - existingDbSize;
                _version++;
                _parsedData.ReplaceRowsInComplexObject(this, dstObjectIndex, existingValueIndex, existingValueIndex + existingDbSize, 1, srcRowCount, 1);

                // Adjust source index if it was shifted by the replacement.
                int adjustedSrc = srcValueIndex >= existingValueIndex + existingDbSize ? srcValueIndex + delta : srcValueIndex;
                _parsedData.CopyRowRange(adjustedSrc, existingValueIndex, srcDbSize);
            }
        }
        else
        {
            // Insert new property at object end.
            int endIndex = dstObjectIndex + GetDbSizeUnsafe(dstObjectIndex, false);
            int valueRowCount = srcDbSize / DbRow.Size;

            // Check for overlap: source straddles insertion point.
            if (srcValueIndex < endIndex && srcValueIndex + srcDbSize > endIndex)
            {
                var cvb = ComplexValueBuilder.Create(this, 1);
                cvb.AddProperty(propertyName, new JsonElement(this, srcValueIndex));
                _version++;
                cvb.InsertAndDispose(dstObjectIndex, endIndex, ref _parsedData);
            }
            else
            {
                int totalRowCount = valueRowCount + 1; // +1 for property name row
                int totalByteSize = totalRowCount * DbRow.Size;
                _version++;
                _parsedData.InsertRowsInComplexObject(this, dstObjectIndex, endIndex, totalRowCount, 1);

                // Write property name row.
                int nameLoc = ((IMutableJsonDocument)this).EscapeAndStoreRawStringValue(propertyName, out bool nameReqEscaping);
                _parsedData.WriteRowAt(endIndex, new DbRow(JsonTokenType.PropertyName, nameLoc, nameReqEscaping ? -1 : 1));

                // Adjust source index if it was shifted by the insertion.
                int adjustedSrc = srcValueIndex >= endIndex ? srcValueIndex + totalByteSize : srcValueIndex;
                _parsedData.CopyRowRange(adjustedSrc, endIndex + DbRow.Size, srcDbSize);
            }
        }
    }

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToArrayIndex(int srcValueIndex, int dstArrayIndex, int itemIndex)
    {
        CheckNotImmutable();

        int srcDbSize = GetDbSizeUnsafe(srcValueIndex, true);
        int rowCount = srcDbSize / DbRow.Size;

        int insertionIndex;
        if (itemIndex == GetArrayLengthUnsafe(dstArrayIndex))
        {
            insertionIndex = dstArrayIndex + GetDbSizeUnsafe(dstArrayIndex, false);
        }
        else
        {
            insertionIndex = GetArrayIndexElementUnsafe(dstArrayIndex, itemIndex);
        }

        // Check for overlap: source straddles insertion point.
        if (srcValueIndex < insertionIndex && srcValueIndex + srcDbSize > insertionIndex)
        {
            var cvb = ComplexValueBuilder.Create(this, 1);
            cvb.AddItem(new JsonElement(this, srcValueIndex));
            _version++;
            cvb.InsertAndDispose(dstArrayIndex, insertionIndex, ref _parsedData);
        }
        else
        {
            _version++;
            _parsedData.InsertRowsInComplexObject(this, dstArrayIndex, insertionIndex, rowCount, 1);

            // Adjust source index if it was shifted by the insertion.
            int adjustedSrc = srcValueIndex >= insertionIndex ? srcValueIndex + srcDbSize : srcValueIndex;
            _parsedData.CopyRowRange(adjustedSrc, insertionIndex, srcDbSize);
        }
    }

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToArrayEnd(int srcValueIndex, int dstArrayIndex)
    {
        CheckNotImmutable();

        int srcDbSize = GetDbSizeUnsafe(srcValueIndex, true);
        int rowCount = srcDbSize / DbRow.Size;
        int endIndex = dstArrayIndex + GetDbSizeUnsafe(dstArrayIndex, false);

        // Check for overlap: source straddles insertion point.
        if (srcValueIndex < endIndex && srcValueIndex + srcDbSize > endIndex)
        {
            var cvb = ComplexValueBuilder.Create(this, 1);
            cvb.AddItem(new JsonElement(this, srcValueIndex));
            _version++;
            cvb.InsertAndDispose(dstArrayIndex, endIndex, ref _parsedData);
        }
        else
        {
            _version++;
            _parsedData.InsertRowsInComplexObject(this, dstArrayIndex, endIndex, rowCount, 1);

            // Adjust source index if it was shifted by the insertion.
            int adjustedSrc = srcValueIndex >= endIndex ? srcValueIndex + srcDbSize : srcValueIndex;
            _parsedData.CopyRowRange(adjustedSrc, endIndex, srcDbSize);
        }
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToProperty(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstObjectIndex, ReadOnlySpan<byte> dstPropertyName)
    {
        CheckNotImmutable();

        if (!TryGetNamedPropertyValueIndexUnsafe(srcObjectIndex, srcPropertyName, out int srcValueIndex))
        {
            return false;
        }

        // Same object, same property name is a no-op.
        if (srcObjectIndex == dstObjectIndex && srcPropertyName.SequenceEqual(dstPropertyName))
        {
            return true;
        }

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);
        int srcRemoveStart = srcValueIndex - DbRow.Size; // Include PropertyName row
        int srcRemoveEnd = srcValueIndex + srcValueByteLength;
        int removeByteLength = srcRemoveEnd - srcRemoveStart;

        // Check destination before mutating, so we know whether to replace or insert.
        bool destExists = TryGetNamedPropertyValueIndexUnsafe(dstObjectIndex, dstPropertyName, out _);

        var cvb = ComplexValueBuilder.Create(this, 1);
        if (destExists)
        {
            cvb.AddItem(srcElement);
        }
        else
        {
            cvb.AddProperty(dstPropertyName, srcElement);
        }

        _version++;

        // Remove source property.
        _parsedData.ReplaceRowsInComplexObject(this, srcObjectIndex, srcRemoveStart, srcRemoveEnd, 1, 0, 0);

        // Adjust destination object index for the removal.
        int adjustedDstObjectIndex = dstObjectIndex;
        if (srcRemoveStart < dstObjectIndex)
        {
            adjustedDstObjectIndex -= removeByteLength;
        }

        if (destExists)
        {
            // Re-resolve existing property at adjusted position.
            TryGetNamedPropertyValueIndexUnsafe(adjustedDstObjectIndex, dstPropertyName, out int existingValueIndex);
            int existingByteLength = GetDbSizeUnsafe(existingValueIndex, true);
            cvb.OverwriteAndDispose(adjustedDstObjectIndex, existingValueIndex, existingValueIndex + existingByteLength, 1, ref _parsedData);
        }
        else
        {
            int endIndex = adjustedDstObjectIndex + GetDbSizeUnsafe(adjustedDstObjectIndex, false);
            cvb.InsertAndDispose(adjustedDstObjectIndex, endIndex, ref _parsedData);
        }

        return true;
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToArray(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex, int destIndex)
    {
        CheckNotImmutable();

        if (!TryGetNamedPropertyValueIndexUnsafe(srcObjectIndex, srcPropertyName, out int srcValueIndex))
        {
            return false;
        }

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        var cvb = ComplexValueBuilder.Create(this, 1);
        cvb.AddItem(srcElement);

        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);
        int srcRemoveStart = srcValueIndex - DbRow.Size;
        int srcRemoveEnd = srcValueIndex + srcValueByteLength;
        int removeByteLength = srcRemoveEnd - srcRemoveStart;

        _version++;

        // Remove source property.
        _parsedData.ReplaceRowsInComplexObject(this, srcObjectIndex, srcRemoveStart, srcRemoveEnd, 1, 0, 0);

        // Adjust destination array index for the removal.
        int adjustedDstArrayIndex = dstArrayIndex;
        if (srcRemoveStart < dstArrayIndex)
        {
            adjustedDstArrayIndex -= removeByteLength;
        }

        // Find insertion point in the post-removal array.
        int insertionIndex;
        if (destIndex == GetArrayLengthUnsafe(adjustedDstArrayIndex))
        {
            insertionIndex = adjustedDstArrayIndex + GetDbSizeUnsafe(adjustedDstArrayIndex, false);
        }
        else
        {
            insertionIndex = GetArrayIndexElementUnsafe(adjustedDstArrayIndex, destIndex);
        }

        cvb.InsertAndDispose(adjustedDstArrayIndex, insertionIndex, ref _parsedData);
        return true;
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToArrayEnd(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex)
    {
        CheckNotImmutable();

        if (!TryGetNamedPropertyValueIndexUnsafe(srcObjectIndex, srcPropertyName, out int srcValueIndex))
        {
            return false;
        }

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        var cvb = ComplexValueBuilder.Create(this, 1);
        cvb.AddItem(srcElement);

        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);
        int srcRemoveStart = srcValueIndex - DbRow.Size;
        int srcRemoveEnd = srcValueIndex + srcValueByteLength;
        int removeByteLength = srcRemoveEnd - srcRemoveStart;

        _version++;

        // Remove source property.
        _parsedData.ReplaceRowsInComplexObject(this, srcObjectIndex, srcRemoveStart, srcRemoveEnd, 1, 0, 0);

        // Adjust destination array index for the removal.
        int adjustedDstArrayIndex = dstArrayIndex;
        if (srcRemoveStart < dstArrayIndex)
        {
            adjustedDstArrayIndex -= removeByteLength;
        }

        int endIndex = adjustedDstArrayIndex + GetDbSizeUnsafe(adjustedDstArrayIndex, false);
        cvb.InsertAndDispose(adjustedDstArrayIndex, endIndex, ref _parsedData);
        return true;
    }

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToArray(int srcArrayIndex, int srcIndex, int dstArrayIndex, int destIndex)
    {
        CheckNotImmutable();

        // Resolve source item position.
        int srcValueIndex = GetArrayIndexElementUnsafe(srcArrayIndex, srcIndex);

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        var cvb = ComplexValueBuilder.Create(this, 1);
        cvb.AddItem(srcElement);

        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);

        _version++;

        // Remove source item (no PropertyName row for array items).
        _parsedData.ReplaceRowsInComplexObject(this, srcArrayIndex, srcValueIndex, srcValueIndex + srcValueByteLength, 1, 0, 0);

        // Adjust destination array index for the removal.
        int adjustedDstArrayIndex = dstArrayIndex;
        if (srcValueIndex < dstArrayIndex)
        {
            adjustedDstArrayIndex -= srcValueByteLength;
        }

        // destIndex uses post-removal semantics, which is naturally correct
        // since we already removed the source.
        int insertionIndex;
        if (destIndex == GetArrayLengthUnsafe(adjustedDstArrayIndex))
        {
            insertionIndex = adjustedDstArrayIndex + GetDbSizeUnsafe(adjustedDstArrayIndex, false);
        }
        else
        {
            insertionIndex = GetArrayIndexElementUnsafe(adjustedDstArrayIndex, destIndex);
        }

        cvb.InsertAndDispose(adjustedDstArrayIndex, insertionIndex, ref _parsedData);
    }

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToArrayEnd(int srcArrayIndex, int srcIndex, int dstArrayIndex)
    {
        CheckNotImmutable();

        // Resolve source item position.
        int srcValueIndex = GetArrayIndexElementUnsafe(srcArrayIndex, srcIndex);

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        var cvb = ComplexValueBuilder.Create(this, 1);
        cvb.AddItem(srcElement);

        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);

        _version++;

        // Remove source item.
        _parsedData.ReplaceRowsInComplexObject(this, srcArrayIndex, srcValueIndex, srcValueIndex + srcValueByteLength, 1, 0, 0);

        // Adjust destination array index for the removal.
        int adjustedDstArrayIndex = dstArrayIndex;
        if (srcValueIndex < dstArrayIndex)
        {
            adjustedDstArrayIndex -= srcValueByteLength;
        }

        int endIndex = adjustedDstArrayIndex + GetDbSizeUnsafe(adjustedDstArrayIndex, false);
        cvb.InsertAndDispose(adjustedDstArrayIndex, endIndex, ref _parsedData);
    }

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToProperty(int srcArrayIndex, int srcIndex, int dstObjectIndex, ReadOnlySpan<byte> destPropertyName)
    {
        CheckNotImmutable();

        // Resolve source item position.
        int srcValueIndex = GetArrayIndexElementUnsafe(srcArrayIndex, srcIndex);

        // Capture source value into CVB before any mutations.
        var srcElement = new JsonElement(this, srcValueIndex);
        int srcValueByteLength = GetDbSizeUnsafe(srcValueIndex, true);

        // Check destination before mutating.
        bool destExists = TryGetNamedPropertyValueIndexUnsafe(dstObjectIndex, destPropertyName, out _);

        var cvb = ComplexValueBuilder.Create(this, 1);
        if (destExists)
        {
            cvb.AddItem(srcElement);
        }
        else
        {
            cvb.AddProperty(destPropertyName, srcElement);
        }

        _version++;

        // Remove source item.
        _parsedData.ReplaceRowsInComplexObject(this, srcArrayIndex, srcValueIndex, srcValueIndex + srcValueByteLength, 1, 0, 0);

        // Adjust destination object index for the removal.
        int adjustedDstObjectIndex = dstObjectIndex;
        if (srcValueIndex < dstObjectIndex)
        {
            adjustedDstObjectIndex -= srcValueByteLength;
        }

        if (destExists)
        {
            // Re-resolve existing property at adjusted position.
            TryGetNamedPropertyValueIndexUnsafe(adjustedDstObjectIndex, destPropertyName, out int existingValueIndex);
            int existingByteLength = GetDbSizeUnsafe(existingValueIndex, true);
            cvb.OverwriteAndDispose(adjustedDstObjectIndex, existingValueIndex, existingValueIndex + existingByteLength, 1, ref _parsedData);
        }
        else
        {
            int endIndex = adjustedDstObjectIndex + GetDbSizeUnsafe(adjustedDstObjectIndex, false);
            cvb.InsertAndDispose(adjustedDstObjectIndex, endIndex, ref _parsedData);
        }
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
            int offset = row.LocationOrIndex;
            if (offset < _rawJsonLength)
            {
                return new(ReadRawJsonBackingValue(offset, row.SizeOrLengthOrPropertyMapIndex, row.TokenType, includeQuotes));
            }

            return new(ReadRawSimpleDynamicValue(offset, includeQuotes));
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

        int offset = row.LocationOrIndex;
        if (offset < _rawJsonLength)
        {
            return ReadRawJsonBackingValue(offset, row.SizeOrLengthOrPropertyMapIndex, row.TokenType, includeQuotes);
        }

        return ReadRawSimpleDynamicValue(offset, includeQuotes);
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

        int offset = row.LocationOrIndex;
        if (offset < _rawJsonLength)
        {
            // No-includeQuotes overload: return content without quotes (same as ParsedJsonDocument convention).
            return _valueBacking.AsMemory(offset, row.SizeOrLengthOrPropertyMapIndex);
        }

        return ReadRawSimpleDynamicValue(offset);
    }

    /// <summary>
    /// Reads a simple value directly from the raw JSON backing region of <c>_valueBacking</c>.
    /// </summary>
    /// <remarks>
    /// When a builder is created via <see cref="Parse(JsonWorkspace, ReadOnlyMemory{byte}, JsonDocumentOptions)"/>,
    /// the raw UTF-8 JSON bytes occupy <c>_valueBacking[0.._rawJsonLength)</c> and MetadataDb rows store
    /// offsets into that region (ParsedJsonDocument-style). This method reads values directly from those
    /// raw bytes, avoiding the DynamicValue header overhead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlyMemory<byte> ReadRawJsonBackingValue(int offset, int length, JsonTokenType tokenType, bool includeQuotes)
    {
        // length must have the HasComplexChildren sign bit masked off.
        Debug.Assert(length >= 0);

        if (includeQuotes && (tokenType == JsonTokenType.String || tokenType == JsonTokenType.PropertyName))
        {
            // offset points past the opening quote; step back 1 byte to include both quotes.
            return _valueBacking.AsMemory(offset - 1, length + 2);
        }

        return _valueBacking.AsMemory(offset, length);
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

    /// <inheritdoc />
    TElement IMutableJsonDocument.FreezeElement<TElement>(int index)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);

        // If the row is from an external mutable document, we must recursively
        // freeze to preserve immutability. External references to immutable
        // documents are safe to copy as-is — they will continue to defer to
        // the immutable document via the workspace.
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);

            if (document is IMutableJsonDocument mutableDocument)
            {
                return mutableDocument.FreezeElement<TElement>(row.LocationOrIndex);
            }
        }

        int byteSize = GetDbSizeUnsafe(index, true);

        // Create a frozen builder in the same workspace, using the same
        // mutable type parameter T to satisfy the builder's constraint.
        JsonDocumentBuilder<T> frozen = new(_workspace);
        int parentWorkspaceIndex = _workspace.GetDocumentIndex(frozen);
        frozen.InitializeFrozen(this, index, byteSize, parentWorkspaceIndex);

        // Construct an immutable TElement pointing to the frozen builder.
#if NET
        return TElement.CreateInstance(frozen, 0);
#else
        return JsonElementHelpers.CreateInstance<TElement>(frozen, 0);
#endif
    }

    /// <summary>
    /// Initializes this builder as a frozen (immutable) copy of a segment of the
    /// source builder's metadb and value backing.
    /// </summary>
    /// <param name="source">The source document to freeze from.</param>
    /// <param name="index">The starting metadb byte index in the source.</param>
    /// <param name="byteSize">The byte size of the metadb segment to copy.</param>
    /// <param name="parentWorkspaceIndex">The workspace index for this builder.</param>
    internal void InitializeFrozen(JsonDocument source, int index, int byteSize, int parentWorkspaceIndex)
    {
        _parentWorkspaceIndex = parentWorkspaceIndex;
        source.CopyFreezeState(this, index, byteSize);
        _isImmutable = true;
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

    /// <inheritdoc />
    int IJsonDocument.WriteElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db, int writePosition)
    {
        CheckNotDisposed();
        CheckNotImmutable();

        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        return WriteElementAt(index, workspace, ref db, workspaceDocumentIndex, writePosition);
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

    private int WriteElementAt(int index, JsonWorkspace workspace, ref MetadataDb db, int workspaceDocumentIndex, int writePosition)
    {
        DbRow row = _parsedData.Get(index);

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace.GetDocument(row.WorkspaceDocumentId);
            return document.WriteElementToMetadataDb(row.LocationOrIndex, workspace, ref db, writePosition);
        }

        switch (row.TokenType)
        {
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
            case JsonTokenType.Number:
            case JsonTokenType.String:
            case JsonTokenType.PropertyName:
                db.WriteRowAt(writePosition, new DbRow(row.TokenType, index, row.RawSizeOrLength, workspaceDocumentIndex));
                return 1;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                return WriteComplexObjectAt(index, workspace, ref db, workspaceDocumentIndex, writePosition);
        }

        Debug.Fail($"Unexpected encounter with JsonTokenType {_parsedData.GetJsonTokenType(index)}");
        return -1;
    }

    private int WriteComplexObjectAt(int index, JsonWorkspace workspace, ref MetadataDb db, int workspaceDocumentIndex, int writePosition)
    {
        int count = 2;
        DbRow complexObjectRow = _parsedData.Get(index);
        db.WriteRowAt(writePosition, new DbRow(complexObjectRow.TokenType, index, complexObjectRow.RawSizeOrLength, workspaceDocumentIndex));

        int endIndex = index + GetDbSizeUnsafe(index, false);
        int currentWritePos = writePosition + DbRow.Size;

        for (int i = index + DbRow.Size; (uint)i < (uint)endIndex; i += DbRow.Size)
        {
            int rowsWritten = WriteElementAt(i, workspace, ref db, workspaceDocumentIndex, currentWritePos);
            count += rowsWritten;
            currentWritePos += rowsWritten * DbRow.Size;
            i += (rowsWritten - 1) * DbRow.Size;
        }

        complexObjectRow = _parsedData.Get(endIndex);
        int entityLength = complexObjectRow.HasPropertyMap ? GetLengthOfEndToken(complexObjectRow.SizeOrLengthOrPropertyMapIndex) : complexObjectRow.RawSizeOrLength;
        db.WriteRowAt(currentWritePos, new DbRow(complexObjectRow.TokenType, index, entityLength, workspaceDocumentIndex));
        return count;
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