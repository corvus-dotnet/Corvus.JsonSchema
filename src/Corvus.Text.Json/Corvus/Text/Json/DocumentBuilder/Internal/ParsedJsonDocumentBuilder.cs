// <copyright file="ParsedJsonDocumentBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// methods. Unlike a <see cref="JsonDocumentBuilder{T}"/>, this builder writes the final UTF-8
/// document text directly during construction: the store operations format each value straight
/// into the text backing, the <see cref="IComplexValueConstructionCallbacks"/> hooks supply the
/// container boundaries, and values embedded from other documents are captured into the backing
/// at the point they are added. Property names are distinguished from string values by JSON's
/// strict name/value alternation within an object, so the ordinary store operations need no
/// extra signalling.
/// </para>
/// <para>
/// The metadata rows are those built by the <see cref="ComplexValueBuilder"/> in the usual way;
/// a side list records the text location and content length for each row as it is written, and
/// <see cref="ToParsedJsonDocument{TElement}"/> applies them in a single in-place patch pass —
/// no second metadata table is built and no content is copied. If a property is removed during
/// construction (for example via <see cref="ComplexValueBuilder.TryApply{T}"/>), the removed
/// value's text is stranded in the backing and the document is marked dirty; a dirty document
/// pays one compaction pass at handoff, rebuilding the text from the surviving rows.
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
public sealed class ParsedJsonDocumentBuilder : JsonDocument, IMutableJsonDocument, IComplexValueConstructionCallbacks
{
    // Frame flags for the container state machine.
    private const int IsObjectFlag = 1;
    private const int HasChildrenFlag = 2;
    private const int ExpectNameFlag = 4;

    // Side-list sizeOrLength sentinel: the row's sizeOrLength is owned by the
    // ComplexValueBuilder (container member counts and end-token lengths) — patch the location only.
    private const int PairKeepSize = int.MinValue;

    private byte[]? _text;

    private int _textPos;

    // Two ints per metadata row, in row order: text location, content length (or PairKeepSize).
    private int[]? _pairs;

    private int _pairCount;

    private int[]? _frames;

    private int _frameDepth;

    private bool _dirty;

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

    private bool NextIsPropertyName =>
        _frameDepth > 0 &&
        (_frames![_frameDepth - 1] & (IsObjectFlag | ExpectNameFlag)) == (IsObjectFlag | ExpectNameFlag);

    /// <summary>
    /// Rents a builder from the thread-local cache, ready to receive a value via
    /// <see cref="ComplexValueBuilder"/> and <see cref="IMutableJsonDocument.SetAndDispose"/>.
    /// </summary>
    /// <param name="initialValueBufferSize">The initial size in bytes of the document text buffer.</param>
    /// <returns>A rented <see cref="ParsedJsonDocumentBuilder"/>. Always dispose the builder; disposal
    /// is idempotent, so this is safe whether or not <see cref="ToParsedJsonDocument{TElement}"/>
    /// completed the handoff.</returns>
    public static ParsedJsonDocumentBuilder Rent(int initialValueBufferSize = 8192)
    {
        ParsedJsonDocumentBuilder builder = ParsedJsonDocumentBuilderCache.RentBuilder();
        builder._workspace = JsonWorkspace.Create();
        builder._text = ArrayPool<byte>.Shared.Rent(Math.Max(JsonConstants.StackallocByteThreshold, initialValueBufferSize));
        builder._pairs = ArrayPool<int>.Shared.Rent(128);
        builder._frames = ArrayPool<int>.Shared.Rent(16);
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

        Debug.Assert(_pairCount * DbRow.Size == _parsedData.Length, "side list out of step with metadata rows");
        Debug.Assert(_frameDepth == 0, "unbalanced container rows in construction metadata");

        if (_dirty)
        {
            Compact();
        }

        PatchRows();

        _parsedData.CompleteAllocations();

        byte[] text = _text!;
        ParsedJsonDocument<TElement> result = ParsedJsonDocument<TElement>.CreateFromBuilder(text.AsMemory(0, _textPos), _parsedData, text);

        // Ownership of the text buffer and the metadata database has transferred to the document.
        _text = null;
        _parsedData = default;
        Dispose();
        return result;
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

        if (_text is byte[] text)
        {
            _text = null;

            // The buffer holds document content, so clear what was written before returning it.
            text.AsSpan(0, _textPos).Clear();
            ArrayPool<byte>.Shared.Return(text);
        }

        if (_pairs is int[] pairs)
        {
            _pairs = null;
            ArrayPool<int>.Shared.Return(pairs);
        }

        if (_frames is int[] frames)
        {
            _frames = null;
            ArrayPool<int>.Shared.Return(frames);
        }

        _textPos = 0;
        _pairCount = 0;
        _frameDepth = 0;
        _dirty = false;

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

    // ──────────────────────────────────────────────────────────────────────────
    // Construction callbacks: the ComplexValueBuilder surfaces container boundaries,
    // embedded elements, and row removal through these.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    void IComplexValueConstructionCallbacks.OnStartComplex(JsonTokenType tokenType)
    {
        CheckNotDisposed();
        WriteStartComplexCore(tokenType);
    }

    /// <inheritdoc />
    void IComplexValueConstructionCallbacks.OnEndComplex(JsonTokenType tokenType)
    {
        CheckNotDisposed();
        WriteEndComplexCore(tokenType);
    }

    /// <inheritdoc />
    void IComplexValueConstructionCallbacks.AppendExternalElement(IJsonDocument sourceDocument, int sourceIndex, ref MetadataDb db)
    {
        CheckNotDisposed();

        // Expand the element to external reference rows (the standard mechanism), then resolve
        // them immediately: content into the text backing, fully-local rows into the metadata.
        var scratch = MetadataDb.CreateRented(16 * DbRow.Size, convertToAlloc: false);
        try
        {
            sourceDocument.AppendElementToMetadataDb(sourceIndex, _workspace!, ref scratch);
            WriteResolvedExternalRows(ref scratch, ref db);
        }
        finally
        {
            scratch.Dispose();
        }
    }

    /// <inheritdoc />
    void IComplexValueConstructionCallbacks.OnRowsRemoved(int rowByteIndex, int rowCount)
    {
        CheckNotDisposed();

        int pairIndex = rowByteIndex / DbRow.Size;
        Debug.Assert(pairIndex + rowCount <= _pairCount);

        Array.Copy(_pairs!, (pairIndex + rowCount) * 2, _pairs!, pairIndex * 2, (_pairCount - pairIndex - rowCount) * 2);
        _pairCount -= rowCount;

        // The removed rows' text is stranded in the backing; compact at handoff.
        _dirty = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Store operations: format directly into the document text.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    int IMutableJsonDocument.StoreBooleanValue(bool value) => StoreLiteralText(value ? JsonConstants.TrueValue : JsonConstants.FalseValue);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreNullValue() => StoreLiteralText(JsonConstants.NullValue);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreRawNumberValue(ReadOnlySpan<byte> value)
    {
        CheckNotDisposed();
        JsonWriterHelper.ValidateNumber(value);
        BeginValueToken(value.Length);
        int start = _textPos;
        value.CopyTo(_text.AsSpan(_textPos));
        _textPos += value.Length;
        PushPair(start, value.Length);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<byte> value, out bool requiredEscaping)
    {
        CheckNotDisposed();

        System.Text.Encodings.Web.JavaScriptEncoder? encoder = _workspace!.Options.Encoder;
        int valueIdx = JsonWriterHelper.NeedsEscaping(value, encoder);

        Debug.Assert(valueIdx >= -1 && valueIdx < value.Length);

        int maxContent = valueIdx == -1 ? value.Length : JsonWriterHelper.GetMaxEscapedLength(value.Length, valueIdx);
        bool isName = BeginStringToken(maxContent);

        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;

        if (valueIdx != -1)
        {
            requiredEscaping = true;
            JsonWriterHelper.EscapeString(value, _text.AsSpan(_textPos), valueIdx, encoder, out int written);
            _textPos += written;
        }
        else
        {
            requiredEscaping = false;
            value.CopyTo(_text.AsSpan(_textPos));
            _textPos += value.Length;
        }

        CompleteStringToken(start, isName);
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping)
    {
        CheckNotDisposed();

        System.Text.Encodings.Web.JavaScriptEncoder? encoder = _workspace!.Options.Encoder;
        int valueIdx = JsonWriterHelper.NeedsEscaping(value, encoder);

        int valueLength = value.Length;
        Debug.Assert(valueIdx >= -1 && valueIdx < valueLength);

        // Escaped output is ASCII, so the UTF-8 length of the escaped form equals its char length;
        // an unescaped char transcodes to at most three UTF-8 bytes.
        int maxContent = valueIdx == -1 ? valueLength * 3 : JsonWriterHelper.GetMaxEscapedLength(valueLength, valueIdx) * 3;
        bool isName = BeginStringToken(maxContent);

        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;

        int written;
        if (valueIdx != -1)
        {
            requiredEscaping = true;

            char[]? buffer = null;
            int maxEscapedLength = JsonWriterHelper.GetMaxEscapedLength(valueLength, valueIdx);
            Span<char> escapedBuffer = maxEscapedLength <= JsonConstants.StackallocCharThreshold
                ? stackalloc char[JsonConstants.StackallocCharThreshold]
                : (buffer = ArrayPool<char>.Shared.Rent(maxEscapedLength)).AsSpan();

            try
            {
                JsonWriterHelper.EscapeString(value, escapedBuffer, valueIdx, encoder, out written);
                JsonWriterHelper.ToUtf8(escapedBuffer.Slice(0, written), _text.AsSpan(_textPos), out written);
                _textPos += written;
            }
            finally
            {
                if (buffer is char[] b)
                {
                    ArrayPool<char>.Shared.Return(b);
                }
            }
        }
        else
        {
            requiredEscaping = false;
            JsonWriterHelper.ToUtf8(value, _text.AsSpan(_textPos), out written);
            _textPos += written;
        }

        CompleteStringToken(start, isName);
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreRawStringValue(ReadOnlySpan<byte> value)
    {
        CheckNotDisposed();

        bool isName = BeginStringToken(value.Length);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        value.CopyTo(_text.AsSpan(_textPos));
        _textPos += value.Length;
        CompleteStringToken(start, isName);
        return start;
    }

    /// <inheritdoc cref="IMutableJsonDocument.StorePrebakedValue"/>
    public new int StorePrebakedValue(ReadOnlySpan<byte> prebakedValue)
    {
        CheckNotDisposed();

        // A pre-baked blob is a property name: [4-byte header][quote][escaped name][quote].
        Debug.Assert(NextIsPropertyName, "pre-baked values are property names");
        ReadOnlySpan<byte> quotedName = prebakedValue.Slice(4);

        BeginNameToken(quotedName.Length);
        int start = _textPos + 1;
        quotedName.CopyTo(_text.AsSpan(_textPos));
        _textPos += quotedName.Length;
        _text![_textPos++] = JsonConstants.KeyValueSeparator;
        PushPair(start, quotedName.Length - 2);
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Guid value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatGuidLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in DateTime value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatGuidLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        JsonWriterHelper.WriteDateTimeTrimmed(_text.AsSpan(_textPos), value, out int written);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in DateTimeOffset value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatGuidLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        JsonWriterHelper.WriteDateTimeOffsetTrimmed(_text.AsSpan(_textPos), value, out int written);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetDateTime value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatDateTimeOffsetLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = JsonElementHelpers.TryFormatOffsetDateTime(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetDate value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatOffsetDateLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = JsonElementHelpers.TryFormatOffsetDate(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in OffsetTime value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatOffsetTimeLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = JsonElementHelpers.TryFormatOffsetTime(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in LocalDate value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatOffsetTimeLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = JsonElementHelpers.TryFormatLocalDate(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in Period value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatOffsetTimeLength + 2);
        _text![_textPos++] = JsonConstants.Quote;
        int start = _textPos;
        bool success = JsonElementHelpers.TryFormatPeriod(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        _text[_textPos++] = JsonConstants.Quote;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(sbyte value) => StoreInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(byte value) => StoreUInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(int value) => StoreInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(uint value) => StoreUInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(long value) => StoreInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(ulong value) => StoreUInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(short value) => StoreInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(ushort value) => StoreUInt64Text(value);

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(float value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatSingleLength);
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(double value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatDoubleLength);
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(decimal value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatDecimalLength);
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in BigInteger value)
    {
        CheckNotDisposed();

#if NET
        if (!value.TryGetMinimumFormatBufferLength(out int required))
        {
            ThrowHelper.ThrowOutOfMemoryException((uint)required);
        }

        BeginValueToken(required);
        int start = _textPos;
        bool formatted = value.TryFormat(_text.AsSpan(_textPos), out int written);
        Debug.Assert(formatted);
#else
        BeginValueToken(JsonConstants.InitialFormatBigIntegerLength);
        int start = _textPos;
        int written;
        while (!value.TryFormat(_text.AsSpan(_textPos), out written))
        {
            EnsureText(_text!.Length + JsonConstants.InitialFormatBigIntegerLength);
        }
#endif
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(in BigNumber value)
    {
        CheckNotDisposed();

#if NET
        if (!value.TryGetMinimumFormatBufferLength(out int required))
        {
            ThrowHelper.ThrowOutOfMemoryException((uint)required);
        }

        BeginValueToken(required);
        int start = _textPos;
        bool formatted = value.TryFormat(_text.AsSpan(_textPos), out int written);
        Debug.Assert(formatted);
#else
        BeginValueToken(JsonConstants.InitialFormatBigNumberLength);
        int start = _textPos;
        int written;
        while (!value.Normalize().TryFormat(_text.AsSpan(_textPos), out written))
        {
            EnsureText(_text!.Length + JsonConstants.InitialFormatBigIntegerLength);
        }
#endif
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

#if NET

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Int128 value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatInt128Length);
        int start = _textPos;
        bool success = value.TryFormat(_text.AsSpan(_textPos), out int written, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(UInt128 value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatUInt128Length);
        int start = _textPos;
        bool success = value.TryFormat(_text.AsSpan(_textPos), out int written, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <inheritdoc />
    int IMutableJsonDocument.StoreValue(Half value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatHalfLength);
        int start = _textPos;
        bool success = value.TryFormat(_text.AsSpan(_textPos), out int written, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

#endif

    // ──────────────────────────────────────────────────────────────────────────
    // Raw value reads over the in-progress metadata, used by the property-name lookup
    // during construction (e.g. ComplexValueBuilder.RemoveProperty).
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index, bool includeQuotes) => GetRawSimpleValueUnsafe(ref _parsedData, index, includeQuotes);

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index) => GetRawSimpleValueUnsafe(ref _parsedData, index);

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index, bool includeQuotes)
    {
        DbRow row = parsedData.Get(index);

        Debug.Assert(row.IsSimpleValue);
        Debug.Assert(!row.FromExternalDocument, "external rows are resolved at add time");

        (int location, int length) = GetPair(index / DbRow.Size);

        if (includeQuotes && (row.TokenType == JsonTokenType.String || row.TokenType == JsonTokenType.PropertyName))
        {
            return _text.AsMemory(location - 1, length + 2);
        }

        return _text.AsMemory(location, length);
    }

    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(ref MetadataDb parsedData, int index)
    {
        DbRow row = parsedData.Get(index);

        Debug.Assert(row.IsSimpleValue);
        Debug.Assert(!row.FromExternalDocument, "external rows are resolved at add time");

        (int location, int length) = GetPair(index / DbRow.Size);
        return _text.AsMemory(location, length);
    }

    /// <inheritdoc />
    private protected override ReadOnlyMemory<byte> GetRawSimpleValueFromRowUnsafe(in DbRow row) => throw ConstructionOnly();

    // ──────────────────────────────────────────────────────────────────────────
    // Text emission core.
    // ──────────────────────────────────────────────────────────────────────────
    private int StoreLiteralText(ReadOnlySpan<byte> literal)
    {
        CheckNotDisposed();
        BeginValueToken(literal.Length);
        int start = _textPos;
        literal.CopyTo(_text.AsSpan(_textPos));
        _textPos += literal.Length;
        PushPair(start, literal.Length);
        CompleteValueToken();
        return start;
    }

    private int StoreInt64Text(long value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatInt64Length);
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    private int StoreUInt64Text(ulong value)
    {
        CheckNotDisposed();
        BeginValueToken(JsonConstants.MaximumFormatUInt64Length);
        int start = _textPos;
        bool success = Utf8Formatter.TryFormat(value, _text.AsSpan(_textPos), out int written);
        Debug.Assert(success);
        _textPos += written;
        PushPair(start, written);
        CompleteValueToken();
        return start;
    }

    /// <summary>
    /// Prepares to write a value token: ensures capacity for the content plus any separator, and
    /// writes the list separator when this is a subsequent array item.
    /// </summary>
    /// <param name="maxContentLength">The maximum content length about to be written.</param>
    private void BeginValueToken(int maxContentLength)
    {
        EnsureText(_textPos + maxContentLength + 3);

        if (_frameDepth > 0)
        {
            int frame = _frames![_frameDepth - 1];
            if ((frame & IsObjectFlag) != 0)
            {
                Debug.Assert((frame & ExpectNameFlag) == 0, "value written in a property-name slot");
            }
            else if ((frame & HasChildrenFlag) != 0)
            {
                _text![_textPos++] = JsonConstants.ListSeparator;
            }
        }
    }

    /// <summary>
    /// Marks the current value complete: the enclosing container has a child, and an enclosing
    /// object next expects a property name.
    /// </summary>
    private void CompleteValueToken()
    {
        if (_frameDepth > 0)
        {
            ref int frame = ref _frames![_frameDepth - 1];
            frame |= HasChildrenFlag;
            if ((frame & IsObjectFlag) != 0)
            {
                frame |= ExpectNameFlag;
            }
        }
    }

    /// <summary>
    /// Prepares to write a property name token: ensures capacity, writes the list separator when
    /// this is a subsequent member, and moves the frame into the value slot.
    /// </summary>
    /// <param name="maxContentLength">The maximum content length about to be written (including quotes).</param>
    private void BeginNameToken(int maxContentLength)
    {
        EnsureText(_textPos + maxContentLength + 4);

        ref int frame = ref _frames![_frameDepth - 1];
        Debug.Assert((frame & (IsObjectFlag | ExpectNameFlag)) == (IsObjectFlag | ExpectNameFlag));

        if ((frame & HasChildrenFlag) != 0)
        {
            _text![_textPos++] = JsonConstants.ListSeparator;
        }

        frame &= ~ExpectNameFlag;
    }

    /// <summary>
    /// Prepares to write a string token, classifying it as a property name or a value from the
    /// name/value alternation of the enclosing object frame.
    /// </summary>
    /// <param name="maxContentLength">The maximum content length about to be written.</param>
    /// <returns><see langword="true"/> if the token is a property name.</returns>
    private bool BeginStringToken(int maxContentLength)
    {
        if (NextIsPropertyName)
        {
            BeginNameToken(maxContentLength + 2);
            return true;
        }

        BeginValueToken(maxContentLength + 2);
        return false;
    }

    /// <summary>
    /// Completes a string token: writes the closing quote, then the key/value separator for a
    /// property name or the value-completion state transition for a value.
    /// </summary>
    /// <param name="contentStart">The text position of the first content byte.</param>
    /// <param name="isName">Whether the token is a property name.</param>
    private void CompleteStringToken(int contentStart, bool isName)
    {
        int length = _textPos - contentStart;
        _text![_textPos++] = JsonConstants.Quote;

        if (isName)
        {
            _text[_textPos++] = JsonConstants.KeyValueSeparator;
        }
        else
        {
            CompleteValueToken();
        }

        PushPair(contentStart, length);
    }

    private void WriteStartComplexCore(JsonTokenType tokenType)
    {
        BeginValueToken(1);
        PushPair(_textPos, PairKeepSize);
        _text![_textPos++] = tokenType == JsonTokenType.StartObject ? JsonConstants.OpenBrace : JsonConstants.OpenBracket;
        PushFrame(tokenType == JsonTokenType.StartObject ? IsObjectFlag | ExpectNameFlag : 0);
    }

    private void WriteEndComplexCore(JsonTokenType tokenType)
    {
        EnsureText(_textPos + 1);
        Debug.Assert(_frameDepth > 0);
        _frameDepth--;
        PushPair(_textPos, PairKeepSize);
        _text![_textPos++] = tokenType == JsonTokenType.EndObject ? JsonConstants.CloseBrace : JsonConstants.CloseBracket;
        CompleteValueToken();
    }

    /// <summary>
    /// Resolves the external reference rows in <paramref name="scratch"/>: writes their content
    /// into the text backing and appends fully-local, final rows to <paramref name="db"/>.
    /// </summary>
    /// <param name="scratch">The external reference rows produced by the source document.</param>
    /// <param name="db">The metadata database under construction.</param>
    private void WriteResolvedExternalRows(ref MetadataDb scratch, ref MetadataDb db)
    {
        JsonWorkspace workspace = _workspace!;

        // Tracks the output db byte index of each open container so the end row can fix up
        // NumberOfRows on both; reuses the frame machinery for separators and name slots.
        int[]? startStack = ArrayPool<int>.Shared.Rent(JsonDocumentOptions.DefaultMaxDepth);
        int stackDepth = 0;

        try
        {
            for (int i = 0; i < scratch.Length; i += DbRow.Size)
            {
                DbRow row = scratch.Get(i);

                switch (row.TokenType)
                {
                    case JsonTokenType.PropertyName:
                    case JsonTokenType.String:
                    {
                        IJsonDocument source = workspace.GetDocument(row.WorkspaceDocumentId);
                        ReadOnlySpan<byte> content = source.GetRawSimpleValue(row.LocationOrIndex, includeQuotes: false).Span;

                        bool isName = row.TokenType == JsonTokenType.PropertyName;
                        if (isName)
                        {
                            BeginNameToken(content.Length + 2);
                        }
                        else
                        {
                            BeginValueToken(content.Length + 2);
                        }

                        _text![_textPos++] = JsonConstants.Quote;
                        int start = _textPos;
                        content.CopyTo(_text.AsSpan(_textPos));
                        _textPos += content.Length;
                        _text[_textPos++] = JsonConstants.Quote;

                        if (isName)
                        {
                            _text[_textPos++] = JsonConstants.KeyValueSeparator;
                        }
                        else
                        {
                            CompleteValueToken();
                        }

                        db.AppendStringOrPropertyName(row.TokenType, start, content.Length, row.HasComplexChildren);
                        PushPair(start, content.Length);
                        break;
                    }

                    case JsonTokenType.Number:
                    {
                        IJsonDocument source = workspace.GetDocument(row.WorkspaceDocumentId);
                        ReadOnlySpan<byte> content = source.GetRawSimpleValue(row.LocationOrIndex, includeQuotes: false).Span;

                        BeginValueToken(content.Length);
                        int start = _textPos;
                        content.CopyTo(_text.AsSpan(_textPos));
                        _textPos += content.Length;
                        CompleteValueToken();

                        db.Append(JsonTokenType.Number, start, content.Length);
                        PushPair(start, content.Length);
                        break;
                    }

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                    {
                        ReadOnlySpan<byte> literal = row.TokenType switch
                        {
                            JsonTokenType.True => JsonConstants.TrueValue,
                            JsonTokenType.False => JsonConstants.FalseValue,
                            _ => JsonConstants.NullValue,
                        };

                        BeginValueToken(literal.Length);
                        int start = _textPos;
                        literal.CopyTo(_text.AsSpan(_textPos));
                        _textPos += literal.Length;
                        CompleteValueToken();

                        db.Append(row.TokenType, start, literal.Length);
                        PushPair(start, literal.Length);
                        break;
                    }

                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                    {
                        if (stackDepth == startStack!.Length)
                        {
                            GrowIntArray(ref startStack, stackDepth);
                        }

                        BeginValueToken(1);
                        int startRowIndex = db.Length;
                        startStack![stackDepth++] = startRowIndex;

                        PushPair(_textPos, PairKeepSize);
                        db.Append(row.TokenType, _textPos, DbRow.UnknownSize);
                        _text![_textPos++] = row.TokenType == JsonTokenType.StartObject ? JsonConstants.OpenBrace : JsonConstants.OpenBracket;
                        PushFrame(row.TokenType == JsonTokenType.StartObject ? IsObjectFlag | ExpectNameFlag : 0);

                        db.SetLength(startRowIndex, row.SizeOrLengthOrPropertyMapIndex);
                        if (row.HasComplexChildren)
                        {
                            db.SetHasComplexChildren(startRowIndex);
                        }

                        break;
                    }

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                    {
                        Debug.Assert(stackDepth > 0);
                        int startRowIndex = startStack![--stackDepth];
                        int endRowIndex = db.Length;

                        EnsureText(_textPos + 1);
                        Debug.Assert(_frameDepth > 0);
                        _frameDepth--;
                        PushPair(_textPos, PairKeepSize);
                        db.Append(row.TokenType, _textPos, 1);
                        _text![_textPos++] = row.TokenType == JsonTokenType.EndObject ? JsonConstants.CloseBrace : JsonConstants.CloseBracket;
                        CompleteValueToken();

                        int numberOfRows = (endRowIndex - startRowIndex) / DbRow.Size;
                        db.SetNumberOfRows(startRowIndex, numberOfRows);
                        db.SetNumberOfRows(endRowIndex, numberOfRows);
                        break;
                    }

                    default:
                    {
                        Debug.Fail($"Unexpected JsonTokenType {row.TokenType} in external element rows");
                        break;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(startStack!);
        }
    }

    /// <summary>
    /// Rebuilds the text backing from the surviving rows after one or more removals, dropping the
    /// stranded content and rewriting the structural bytes.
    /// </summary>
    private void Compact()
    {
        byte[] oldText = _text!;
        int oldPos = _textPos;

        _text = ArrayPool<byte>.Shared.Rent(oldPos);
        _textPos = 0;

        Debug.Assert(_frameDepth == 0);

        int metadataLength = _parsedData.Length;
        for (int rowIndex = 0, pairIndex = 0; rowIndex < metadataLength; rowIndex += DbRow.Size, pairIndex++)
        {
            JsonTokenType tokenType = _parsedData.GetJsonTokenType(rowIndex);
            (int oldLocation, int length) = GetPair(pairIndex);

            switch (tokenType)
            {
                case JsonTokenType.PropertyName:
                {
                    BeginNameToken(length + 2);
                    _text![_textPos++] = JsonConstants.Quote;
                    int start = _textPos;
                    oldText.AsSpan(oldLocation, length).CopyTo(_text.AsSpan(_textPos));
                    _textPos += length;
                    _text[_textPos++] = JsonConstants.Quote;
                    _text[_textPos++] = JsonConstants.KeyValueSeparator;
                    SetPair(pairIndex, start, length);
                    break;
                }

                case JsonTokenType.String:
                {
                    BeginValueToken(length + 2);
                    _text![_textPos++] = JsonConstants.Quote;
                    int start = _textPos;
                    oldText.AsSpan(oldLocation, length).CopyTo(_text.AsSpan(_textPos));
                    _textPos += length;
                    _text[_textPos++] = JsonConstants.Quote;
                    CompleteValueToken();
                    SetPair(pairIndex, start, length);
                    break;
                }

                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                {
                    BeginValueToken(length);
                    int start = _textPos;
                    oldText.AsSpan(oldLocation, length).CopyTo(_text.AsSpan(_textPos));
                    _textPos += length;
                    CompleteValueToken();
                    SetPair(pairIndex, start, length);
                    break;
                }

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                {
                    BeginValueToken(1);
                    SetPair(pairIndex, _textPos, PairKeepSize);
                    _text![_textPos++] = tokenType == JsonTokenType.StartObject ? JsonConstants.OpenBrace : JsonConstants.OpenBracket;
                    PushFrame(tokenType == JsonTokenType.StartObject ? IsObjectFlag | ExpectNameFlag : 0);
                    break;
                }

                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                {
                    EnsureText(_textPos + 1);
                    Debug.Assert(_frameDepth > 0);
                    _frameDepth--;
                    SetPair(pairIndex, _textPos, PairKeepSize);
                    _text![_textPos++] = tokenType == JsonTokenType.EndObject ? JsonConstants.CloseBrace : JsonConstants.CloseBracket;
                    CompleteValueToken();
                    break;
                }

                default:
                {
                    Debug.Fail($"Unexpected JsonTokenType {tokenType} in construction metadata");
                    break;
                }
            }
        }

        Debug.Assert(_frameDepth == 0, "unbalanced container rows in construction metadata");

        // The old buffer holds document content, so clear what was written before returning it.
        oldText.AsSpan(0, oldPos).Clear();
        ArrayPool<byte>.Shared.Return(oldText);
        _dirty = false;
    }

    /// <summary>
    /// Applies the side list to the metadata rows in place: every row receives its text location,
    /// and simple-value rows receive their content length (string and property-name rows keep
    /// their requires-unescaping flag; number rows are normalized to the parsed convention with
    /// no flag). Container rows keep the counts the <see cref="ComplexValueBuilder"/> maintained.
    /// </summary>
    private void PatchRows()
    {
        int metadataLength = _parsedData.Length;
        for (int rowIndex = 0, pairIndex = 0; rowIndex < metadataLength; rowIndex += DbRow.Size, pairIndex++)
        {
            (int location, int size) = GetPair(pairIndex);

            _parsedData.SetRowLocation(rowIndex, location);

            if (size != PairKeepSize)
            {
                DbRow row = _parsedData.Get(rowIndex);
                _parsedData.SetLength(rowIndex, size);

                if (row.HasComplexChildren &&
                    (row.TokenType == JsonTokenType.String || row.TokenType == JsonTokenType.PropertyName))
                {
                    _parsedData.SetHasComplexChildren(rowIndex);
                }
            }
        }
    }

    private void PushFrame(int frame)
    {
        if (_frameDepth == _frames!.Length)
        {
            GrowIntArray(ref _frames, _frameDepth);
        }

        _frames![_frameDepth++] = frame;
    }

    private void PushPair(int location, int size)
    {
        if ((_pairCount * 2) + 2 > _pairs!.Length)
        {
            GrowIntArray(ref _pairs, _pairCount * 2);
        }

        _pairs![_pairCount * 2] = location;
        _pairs[(_pairCount * 2) + 1] = size;
        _pairCount++;
    }

    private static void GrowIntArray(ref int[]? array, int copyCount)
    {
        int[] toReturn = array!;
        array = ArrayPool<int>.Shared.Rent(toReturn.Length * 2);
        toReturn.AsSpan(0, copyCount).CopyTo(array);
        ArrayPool<int>.Shared.Return(toReturn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (int Location, int Size) GetPair(int pairIndex)
    {
        Debug.Assert(pairIndex < _pairCount);
        return (_pairs![pairIndex * 2], _pairs[(pairIndex * 2) + 1]);
    }

    private void SetPair(int pairIndex, int location, int size)
    {
        Debug.Assert(pairIndex < _pairCount);
        _pairs![pairIndex * 2] = location;
        _pairs[(pairIndex * 2) + 1] = size;
    }

    /// <summary>
    /// Ensures the text buffer can hold at least <paramref name="required"/> bytes, growing it in a
    /// single step and preserving the bytes already produced.
    /// </summary>
    /// <param name="required">The required capacity in bytes.</param>
    private void EnsureText(int required)
    {
        if ((uint)_text!.Length >= (uint)required)
        {
            return;
        }

        byte[] toReturn = _text;
        _text = ArrayPool<byte>.Shared.Rent(Math.Max(toReturn.Length * 2, required));
        toReturn.AsSpan(0, _textPos).CopyTo(_text);

        // The buffer holds document content, so clear it before returning it to the pool.
        toReturn.AsSpan(0, _textPos).Clear();
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