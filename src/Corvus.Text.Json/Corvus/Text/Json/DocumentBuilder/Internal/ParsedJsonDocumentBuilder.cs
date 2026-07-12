// <copyright file="ParsedJsonDocumentBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// A single-use, poolable <see cref="IMutableJsonDocument"/> that supports document construction
/// via <see cref="ComplexValueBuilder"/>, and materializes the result as a self-contained
/// <see cref="ParsedJsonDocument{T}"/> without a serialize-and-reparse round trip.
/// </summary>
/// <remarks>
/// <para>
/// This type backs the generated <c>Create()</c> factory methods, which are the
/// <see cref="ParsedJsonDocument{T}"/>-producing analogue of the generated <c>CreateBuilder()</c>
/// methods. Values are accumulated in the standard dynamic value store, exactly as they would be for a
/// <see cref="JsonDocumentBuilder{T}"/>; <see cref="ToParsedJsonDocument{TElement}"/> then makes a single
/// linear pass over the metadata table, writing the UTF-8 JSON text and the parsed-format metadata rows
/// together, and hands both off to a new <see cref="ParsedJsonDocument{T}"/>. External document values
/// are blitted into the backing during that pass, so the resulting document is fully self-contained.
/// </para>
/// <para>
/// Instances are rented from a thread-local cache via <see cref="Rent"/> to avoid hot-path allocation.
/// <see cref="Dispose"/> is idempotent and returns the instance to the cache; it is safe to call it
/// after <see cref="ToParsedJsonDocument{TElement}"/> (which disposes the builder as part of the
/// handoff), for example from a <see langword="finally"/> block in a fault-handling path.
/// </para>
/// <para>
/// The builder supports document construction only. Element access, mutation of an existing document,
/// and the other <see cref="IJsonDocument"/> read operations are not supported and throw
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
[CLSCompliant(false)]
public sealed class ParsedJsonDocumentBuilder : JsonDocument, IMutableJsonDocument
{
    private const int FrameInts = 4;

    private JsonWorkspace? _workspace;

    private bool _isRented;

    private ParsedJsonDocumentBuilder()
    {
    }

    /// <inheritdoc />
    JsonWorkspace? IJsonDocument.CachedWorkspace { get; set; }

    /// <inheritdoc />
    int IJsonDocument.CachedWorkspaceDocumentIndex { get; set; }

    /// <inheritdoc />
    int IJsonDocument.CachedWorkspaceGeneration { get; set; }

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    bool IJsonDocument.IsDisposable => true;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ulong IMutableJsonDocument.Version => 0;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int IMutableJsonDocument.ParentWorkspaceIndex => 0;

    /// <inheritdoc />
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonWorkspace IMutableJsonDocument.Workspace
    {
        get
        {
            CheckNotDisposed();
            return _workspace!;
        }
    }

    /// <summary>
    /// Rents a builder from the thread-local cache, ready to receive a value via
    /// <see cref="ComplexValueBuilder"/> and <see cref="IMutableJsonDocument.SetAndDispose"/>.
    /// </summary>
    /// <param name="initialValueBufferSize">The initial size in bytes of the value buffer.</param>
    /// <returns>A rented <see cref="ParsedJsonDocumentBuilder"/>. Always dispose the builder; disposal
    /// is idempotent, so this is safe whether or not <see cref="ToParsedJsonDocument{TElement}"/>
    /// completed the handoff.</returns>
    public static ParsedJsonDocumentBuilder Rent(int initialValueBufferSize = 8192)
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilderCache.RentBuilder();
        builder._workspace = JsonWorkspace.Create();
        builder._valueBacking = ArrayPool<byte>.Shared.Rent(initialValueBufferSize);
        builder._isRented = true;
        return builder;
    }

    /// <summary>
    /// Hands off the built value as a new, self-contained <see cref="ParsedJsonDocument{T}"/>, and
    /// disposes this builder (returning it to the pool).
    /// </summary>
    /// <typeparam name="TElement">The element type of the resulting document.</typeparam>
    /// <returns>A <see cref="ParsedJsonDocument{T}"/> owning pooled memory. The caller must dispose it.</returns>
    /// <exception cref="InvalidOperationException">The builder has not been initialized with a value.</exception>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public ParsedJsonDocument<TElement> ToParsedJsonDocument<TElement>()
        where TElement : struct, IJsonElement<TElement>
    {
        CheckNotDisposed();

        if (!_parsedData.IsInitialized || _parsedData.Length < DbRow.Size)
        {
            ThrowHelper.ThrowInvalidOperationException(SR.ParsedJsonDocumentBuilderHasNoValue);
        }

        int metadataLength = _parsedData.Length;

        // The output table has exactly one row per input row, so the exact size is known up front.
        MetadataDb db = MetadataDb.CreateRented(metadataLength, convertToAlloc: false);
        byte[]? text = null;
        int pos = 0;

        try
        {
            // The dynamic value store holds every local payload plus a 4-byte header per value; the
            // headers approximately cover the structural bytes (quotes, separators, braces) the text
            // needs. External content grows the buffer on demand.
            text = ArrayPool<byte>.Shared.Rent(Math.Max(JsonConstants.StackallocByteThreshold, _valueOffset + (metadataLength / DbRow.Size * 2)));
            pos = Flatten(ref db, ref text);
            db.CompleteAllocations();

            ParsedJsonDocument<TElement> result = ParsedJsonDocument<TElement>.CreateFromBuilder(text.AsMemory(0, pos), db, text);

            // Ownership of the text buffer and the metadata database has transferred to the document.
            text = null;
            Dispose();
            return result;
        }
        catch
        {
            db.Dispose();

            if (text is byte[] t)
            {
                // The buffer holds document content, so clear what was written before returning it.
                t.AsSpan(0, pos).Clear();
                ArrayPool<byte>.Shared.Return(t);
            }

            throw;
        }
    }

    /// <summary>
    /// Disposes the builder, returning its pooled resources and the instance itself to their pools.
    /// Disposal is idempotent.
    /// </summary>
    public void Dispose()
    {
        if (!_isRented)
        {
            return;
        }

        _isRented = false;

        DisposeCore();
        ResetCoreForReuse();

        JsonWorkspace? workspace = _workspace;
        _workspace = null;
        workspace?.Dispose();

        ParsedJsonDocumentBuilderCache.ReturnBuilder(this);
    }

    /// <summary>
    /// Creates an empty instance for caching purposes.
    /// </summary>
    /// <returns>An empty builder instance suitable for caching.</returns>
    internal static ParsedJsonDocumentBuilder CreateEmptyInstanceForCaching() => new();

    /// <inheritdoc />
    void IMutableJsonDocument.SetAndDispose(ref ComplexValueBuilder cvb)
    {
        CheckNotDisposed();
        cvb.SetAndDispose(ref _parsedData);
    }

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(ref MetadataDb parsedData, int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out int valueIndex)
    {
        CheckNotDisposed();
        return TryGetNamedPropertyValueIndexUnsafe(ref parsedData, startIndex, endIndex, propertyName, out valueIndex);
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreBooleanValue(bool value) => StoreBooleanValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreNullValue() => StoreNullValue();

    /// <inheritdoc />
    int IMutableJsonDocument.StoreRawNumberValue(ReadOnlySpan<byte> value) => StoreRawNumberValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<byte> value, out bool requiredEscaping) => EscapeAndStoreRawStringValue(value, out requiredEscaping, _workspace?.Options.Encoder);

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping) => EscapeAndStoreRawStringValue(value, out requiredEscaping, _workspace?.Options.Encoder);

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
    int IMutableJsonDocument.StoreValue(in BigInteger value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in BigNumber value) => StoreValue(value);

#if NET

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Int128 value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(UInt128 value) => StoreValue(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Half value) => StoreValue(value);

#endif

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index, bool includeQuotes) => GetRawSimpleValueUnsafe(ref _parsedData, index, includeQuotes);

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index) => GetRawSimpleValueUnsafe(ref _parsedData, index);

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index, bool includeQuotes)
    {
        DbRow row = parsedData.Get(index);

        Debug.Assert(row.IsSimpleValue);

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace!.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex, includeQuotes);
        }

        // There is never a raw-JSON backing region in this builder; every local value is dynamic.
        return ReadRawSimpleDynamicValue(row.LocationOrIndex, includeQuotes);
    }

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index)
    {
        DbRow row = parsedData.Get(index);

        Debug.Assert(row.IsSimpleValue);

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace!.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex, includeQuotes: false);
        }

        return ReadRawSimpleDynamicValue(row.LocationOrIndex);
    }

    /// <inheritdoc />
    private protected override ReadOnlyMemory<byte> GetRawSimpleValueFromRowUnsafe(in DbRow row)
    {
        Debug.Assert(row.IsSimpleValue);

        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace!.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex, includeQuotes: false);
        }

        return ReadRawSimpleDynamicValue(row.LocationOrIndex);
    }

    /// <summary>
    /// Makes a single linear pass over the construction metadata, writing the UTF-8 JSON text and the
    /// parsed-format metadata rows together. This mirrors the row-emission conventions of the parse
    /// loop (see <c>JsonDocumentBuilder.ParseTokens</c>), driven by the built rows instead of a reader.
    /// </summary>
    /// <param name="db">The output metadata database receiving parsed-format rows.</param>
    /// <param name="text">The output UTF-8 text buffer; grown on demand.</param>
    /// <returns>The number of bytes written to <paramref name="text"/>.</returns>
    private int Flatten(ref MetadataDb db, ref byte[] text)
    {
        int metadataLength = _parsedData.Length;
        int pos = 0;

        bool inArray = false;
        int arrayItemsOrPropertyCount = 0;
        int numberOfRowsForMembers = 0;
        int numberOfRowsForValues = 0;

        // Frames of FrameInts ints: saved item/property count, saved row count (already including the
        // parser's +1), the output db index of the matching Start row, and whether the enclosing
        // container was an array.
        int[] frames = ArrayPool<int>.Shared.Rent(FrameInts * 16);
        int frameTop = 0;

        try
        {
            for (int i = 0; i < metadataLength; i += DbRow.Size)
            {
                DbRow row = _parsedData.Get(i);
                JsonTokenType tokenType = row.TokenType;

                switch (tokenType)
                {
                    case JsonTokenType.PropertyName:
                    {
                        Debug.Assert(!inArray);
                        numberOfRowsForValues++;
                        numberOfRowsForMembers++;

                        ReadOnlySpan<byte> name = GetSimpleContent(in row);
                        EnsureCapacity(pos + name.Length + 4, pos, ref text);

                        if (arrayItemsOrPropertyCount > 0)
                        {
                            text[pos++] = JsonConstants.ListSeparator;
                        }

                        arrayItemsOrPropertyCount++;

                        text[pos++] = JsonConstants.Quote;
                        int location = pos;
                        name.CopyTo(text.AsSpan(pos));
                        pos += name.Length;
                        text[pos++] = JsonConstants.Quote;
                        text[pos++] = JsonConstants.KeyValueSeparator;

                        db.AppendStringOrPropertyName(tokenType, location, name.Length, row.HasComplexChildren);
                        break;
                    }

                    case JsonTokenType.String:
                    {
                        numberOfRowsForValues++;
                        numberOfRowsForMembers++;

                        ReadOnlySpan<byte> content = GetSimpleContent(in row);
                        EnsureCapacity(pos + content.Length + 3, pos, ref text);

                        if (inArray)
                        {
                            if (arrayItemsOrPropertyCount > 0)
                            {
                                text[pos++] = JsonConstants.ListSeparator;
                            }

                            arrayItemsOrPropertyCount++;
                        }

                        text[pos++] = JsonConstants.Quote;
                        int location = pos;
                        content.CopyTo(text.AsSpan(pos));
                        pos += content.Length;
                        text[pos++] = JsonConstants.Quote;

                        db.AppendStringOrPropertyName(tokenType, location, content.Length, row.HasComplexChildren);
                        break;
                    }

                    case JsonTokenType.Number:
                    {
                        numberOfRowsForValues++;
                        numberOfRowsForMembers++;

                        ReadOnlySpan<byte> content = GetSimpleContent(in row);
                        EnsureCapacity(pos + content.Length + 1, pos, ref text);

                        if (inArray)
                        {
                            if (arrayItemsOrPropertyCount > 0)
                            {
                                text[pos++] = JsonConstants.ListSeparator;
                            }

                            arrayItemsOrPropertyCount++;
                        }

                        int location = pos;
                        content.CopyTo(text.AsSpan(pos));
                        pos += content.Length;

                        db.Append(tokenType, location, content.Length);
                        break;
                    }

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                    {
                        numberOfRowsForValues++;
                        numberOfRowsForMembers++;

                        ReadOnlySpan<byte> content = tokenType switch
                        {
                            JsonTokenType.True => JsonConstants.TrueValue,
                            JsonTokenType.False => JsonConstants.FalseValue,
                            _ => JsonConstants.NullValue,
                        };

                        EnsureCapacity(pos + content.Length + 1, pos, ref text);

                        if (inArray)
                        {
                            if (arrayItemsOrPropertyCount > 0)
                            {
                                text[pos++] = JsonConstants.ListSeparator;
                            }

                            arrayItemsOrPropertyCount++;
                        }

                        int location = pos;
                        content.CopyTo(text.AsSpan(pos));
                        pos += content.Length;

                        db.Append(tokenType, location, content.Length);
                        break;
                    }

                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                    {
                        bool isObject = tokenType == JsonTokenType.StartObject;

                        EnsureCapacity(pos + 2, pos, ref text);

                        if (inArray)
                        {
                            if (arrayItemsOrPropertyCount > 0)
                            {
                                text[pos++] = JsonConstants.ListSeparator;
                            }

                            arrayItemsOrPropertyCount++;
                        }

                        int savedRowCount;
                        if (isObject)
                        {
                            numberOfRowsForValues++;
                            savedRowCount = numberOfRowsForMembers + 1;
                        }
                        else
                        {
                            numberOfRowsForMembers++;
                            savedRowCount = numberOfRowsForValues + 1;
                        }

                        int startIndex = db.Length;
                        db.Append(tokenType, pos, DbRow.UnknownSize);
                        text[pos++] = isObject ? JsonConstants.OpenBrace : JsonConstants.OpenBracket;

                        if (frameTop == frames.Length)
                        {
                            Enlarge(frameTop, ref frames);
                        }

                        frames[frameTop] = arrayItemsOrPropertyCount;
                        frames[frameTop + 1] = savedRowCount;
                        frames[frameTop + 2] = startIndex;
                        frames[frameTop + 3] = inArray ? 1 : 0;
                        frameTop += FrameInts;

                        arrayItemsOrPropertyCount = 0;
                        if (isObject)
                        {
                            numberOfRowsForMembers = 0;
                        }
                        else
                        {
                            numberOfRowsForValues = 0;
                        }

                        inArray = !isObject;
                        break;
                    }

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                    {
                        bool isObject = tokenType == JsonTokenType.EndObject;

                        Debug.Assert(frameTop >= FrameInts);
                        frameTop -= FrameInts;
                        int savedCount = frames[frameTop];
                        int savedRowCount = frames[frameTop + 1];
                        int startRowIndex = frames[frameTop + 2];
                        bool wasInArray = frames[frameTop + 3] != 0;

                        numberOfRowsForValues++;
                        numberOfRowsForMembers++;
                        db.SetLength(startRowIndex, arrayItemsOrPropertyCount);

                        EnsureCapacity(pos + 1, pos, ref text);

                        if (isObject)
                        {
                            int newRowIndex = db.Length;
                            db.Append(tokenType, pos, 1);
                            text[pos++] = JsonConstants.CloseBrace;
                            db.SetNumberOfRows(startRowIndex, numberOfRowsForMembers);
                            db.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                            arrayItemsOrPropertyCount = savedCount;
                            numberOfRowsForMembers += savedRowCount;
                        }
                        else
                        {
                            db.SetNumberOfRows(startRowIndex, numberOfRowsForValues);

                            if ((uint)(arrayItemsOrPropertyCount + 1) != (uint)numberOfRowsForValues)
                            {
                                db.SetHasComplexChildren(startRowIndex);
                            }

                            int newRowIndex = db.Length;
                            db.Append(tokenType, pos, 1);
                            text[pos++] = JsonConstants.CloseBracket;
                            db.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                            arrayItemsOrPropertyCount = savedCount;
                            numberOfRowsForValues += savedRowCount;
                        }

                        inArray = wasInArray;
                        break;
                    }

                    default:
                    {
                        Debug.Fail($"Unexpected JsonTokenType {tokenType} in construction metadata");
                        break;
                    }
                }
            }

            Debug.Assert(frameTop == 0, "Unbalanced container rows in construction metadata");
        }
        finally
        {
            ArrayPool<int>.Shared.Return(frames);
        }

        return pos;
    }

    /// <summary>
    /// Gets the content bytes for a simple value row. For strings and property names the content
    /// excludes the surrounding quotes but retains its escaping, which is normalized for
    /// <see cref="System.Text.Encodings.Web.JavaScriptEncoder.Default"/> by the store operations.
    /// </summary>
    /// <param name="row">The row to read.</param>
    /// <returns>The content bytes.</returns>
    private ReadOnlySpan<byte> GetSimpleContent(in DbRow row)
    {
        if (row.FromExternalDocument)
        {
            IJsonDocument document = _workspace!.GetDocument(row.WorkspaceDocumentId);
            return document.GetRawSimpleValue(row.LocationOrIndex, includeQuotes: false).Span;
        }

        return ReadRawSimpleDynamicValue(row.LocationOrIndex).Span;
    }

    /// <summary>
    /// Ensures the text buffer can hold at least <paramref name="required"/> bytes, growing it in a
    /// single step and preserving the <paramref name="written"/> bytes already produced.
    /// </summary>
    /// <param name="required">The required capacity in bytes.</param>
    /// <param name="written">The number of bytes already written to the buffer.</param>
    /// <param name="text">The buffer to grow.</param>
    private static void EnsureCapacity(int required, int written, ref byte[] text)
    {
        if ((uint)text.Length >= (uint)required)
        {
            return;
        }

        byte[] toReturn = text;
        text = ArrayPool<byte>.Shared.Rent(Math.Max(toReturn.Length * 2, required));
        toReturn.AsSpan(0, written).CopyTo(text);

        // The buffer holds document content, so clear it before returning it to the pool.
        toReturn.AsSpan(0, written).Clear();
        ArrayPool<byte>.Shared.Return(toReturn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckNotDisposed()
    {
        if (!_isRented)
        {
            ThrowHelper.ThrowObjectDisposedException_JsonDocument();
        }
    }

    private static InvalidOperationException ConstructionOnly() => new(SR.ParsedJsonDocumentBuilderIsConstructionOnly);

    // ──────────────────────────────────────────────────────────────────────────
    // The members below are not used during document construction. The builder never hands out
    // elements over its in-progress state, so every element-level read or mutation entry point
    // throws to signal misuse.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    TElement IMutableJsonDocument.FreezeElement<TElement>(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    TElement IMutableJsonDocument.RefreshElementUnsafe<TElement>(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    JsonElement.Mutable IMutableJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IMutableJsonDocument parentDocument, out int parentDocumentIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<char> propertyName, out int valueIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<byte> propertyName, out int valueIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement.Mutable value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement.Mutable value) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.RemoveRange(int complexObjectStartIndex, int startIndex, int endIndex, int membersToRemove) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.ReplaceRootAndDispose(ref ComplexValueBuilder cvb) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.InsertAndDispose(int complexObjectStartIndex, int index, ref ComplexValueBuilder cvb) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.OverwriteAndDispose(int complexObjectStartIndex, int startIndex, int endIndex, int membersToOverwrite, ref ComplexValueBuilder cvb) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.InsertSimpleValue(int complexObjectStartIndex, int targetIndex, int memberCount, JsonTokenType tokenType, int location, int sizeOrLength) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.OverwriteSimpleValue(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, JsonTokenType tokenType, int location, int sizeOrLength) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.InsertSimpleProperty(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, JsonTokenType valueTokenType, int valueLocation, int valueSizeOrLength) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.InsertFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, IJsonDocument sourceDocument, int sourceIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.OverwriteFromDocument(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, IJsonDocument sourceDocument, int sourceIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.InsertPropertyFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryReplacePropertyValue(int objectIndex, ReadOnlySpan<byte> propertyName, JsonTokenType tokenType, int location, int sizeOrLength) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.TryReplacePropertyFromDocument(int objectIndex, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToProperty(int srcValueIndex, int dstObjectIndex, ReadOnlySpan<byte> propertyName) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToArrayIndex(int srcValueIndex, int dstArrayIndex, int itemIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.CopyValueToArrayEnd(int srcValueIndex, int dstArrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToProperty(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstObjectIndex, ReadOnlySpan<byte> dstPropertyName) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToArray(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex, int destIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IMutableJsonDocument.MovePropertyToArrayEnd(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToArray(int srcArrayIndex, int srcIndex, int dstArrayIndex, int destIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToArrayEnd(int srcArrayIndex, int srcIndex, int dstArrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IMutableJsonDocument.MoveItemToProperty(int srcArrayIndex, int srcIndex, int dstObjectIndex, ReadOnlySpan<byte> destPropertyName) => throw ConstructionOnly();

    /// <inheritdoc />
    void IJsonDocument.EnsurePropertyMap(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetHashCode(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    string IJsonDocument.ToString(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    JsonTokenType IJsonDocument.GetJsonTokenType(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    JsonElement IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    TElement IJsonDocument.GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    void IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetArrayInsertionIndex(int currentIndex, int arrayIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetArrayLength(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetPropertyCount(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    string? IJsonDocument.GetString(int index, JsonTokenType expectedType) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result) => throw ConstructionOnly();

    /// <inheritdoc />
    UnescapedUtf8JsonString IJsonDocument.GetUtf8JsonString(int index, JsonTokenType expectedType) => throw ConstructionOnly();

    /// <inheritdoc />
    UnescapedUtf16JsonString IJsonDocument.GetUtf16JsonString(int index, JsonTokenType expectedType) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, [NotNullWhen(true)] out byte[]? value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out sbyte value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out byte value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out short value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out ushort value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out int value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out uint value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out long value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out ulong value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out double value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out float value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out decimal value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out BigInteger value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out BigNumber value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateTime value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetDate value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out OffsetTime value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out LocalDate value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Period value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Guid value) => throw ConstructionOnly();

#if NET

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Int128 value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out UInt128 value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out Half value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out DateOnly value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetValue(int index, out TimeOnly value) => throw ConstructionOnly();

#endif

    /// <inheritdoc />
    string IJsonDocument.GetNameOfPropertyValue(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    ReadOnlySpan<byte> IJsonDocument.GetPropertyNameRaw(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetPropertyNameRaw(int index, bool includeQuotes) => throw ConstructionOnly();

    /// <inheritdoc />
    JsonElement IJsonDocument.GetPropertyName(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    UnescapedUtf8JsonString IJsonDocument.GetPropertyNameUnescaped(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    string IJsonDocument.GetRawValueAsString(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    string IJsonDocument.GetPropertyRawValueAsString(int valueIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes) => throw ConstructionOnly();

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index, bool includeQuotes)
    {
        CheckNotDisposed();
        return GetRawSimpleValueUnsafe(index, includeQuotes);
    }

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index)
    {
        CheckNotDisposed();
        return GetRawSimpleValueUnsafe(index);
    }

    /// <inheritdoc />
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index) => GetRawSimpleValueUnsafe(index);

    /// <inheritdoc />
    bool IJsonDocument.ValueIsEscaped(int index, bool isPropertyName) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape) => throw ConstructionOnly();

    /// <inheritdoc />
    void IJsonDocument.WriteElementTo(int index, Utf8JsonWriter writer) => throw ConstructionOnly();

    /// <inheritdoc />
    void IJsonDocument.WritePropertyName(int index, Utf8JsonWriter writer) => throw ConstructionOnly();

    /// <inheritdoc />
    JsonElement IJsonDocument.CloneElement(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    TElement IJsonDocument.CloneElement<TElement>(int index) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetDbSize(int index, bool includeEndElement) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryFindNextDescendantPropertyValue(int elementIndex, ref int scanIndex, ReadOnlySpan<byte> utf8PropertyName, out int valueIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.GetStartIndex(int endIndex) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking) => throw ConstructionOnly();

    /// <inheritdoc />
    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db) => throw ConstructionOnly();

    /// <inheritdoc />
    int IJsonDocument.WriteElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db, int writePosition) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> jsonPointer, int index, out TValue value) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryFormat(int index, Span<byte> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider) => throw ConstructionOnly();

    /// <inheritdoc />
    string IJsonDocument.ToString(int index, string? format, IFormatProvider? formatProvider) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line) => throw ConstructionOnly();

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line) => throw ConstructionOnly();
}