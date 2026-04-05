// <copyright file="FixedJsonValueDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a JSON document backed by a fixed simple value (number or string).
/// </summary>
/// <typeparam name="T">The type of the root element in the document.</typeparam>
/// <remarks>
/// <para>
/// This type uses a thread-local pool to avoid allocations for
/// intermediate values produced during expression evaluation (e.g., arithmetic
/// results, string concatenation results). Each instance wraps the raw UTF-8
/// JSON representation of a single value.
/// </para>
/// <para>
/// For numbers, the raw value is the unquoted number text (e.g., <c>123.45</c>).
/// For strings, the raw value is the quoted string text (e.g., <c>"hello"</c>).
/// </para>
/// </remarks>
[CLSCompliant(false)]
public sealed class FixedJsonValueDocument<T> : IJsonDocument
    where T : struct, IJsonElement<T>
{
    private byte[]? _rentedBuffer;
    private ReadOnlyMemory<byte> _rawValue;
    private JsonTokenType _tokenType;

    private FixedJsonValueDocument(ReadOnlyMemory<byte> rawValue, JsonTokenType tokenType)
    {
        _rawValue = rawValue;
        _tokenType = tokenType;
    }

    bool IJsonDocument.IsDisposable => true;

    bool IJsonDocument.IsImmutable => true;

    /// <summary>
    /// Creates a <see cref="FixedJsonValueDocument{T}"/> for a number value, using the pool.
    /// </summary>
    /// <param name="rawUtf8Number">The raw UTF-8 number bytes (unquoted).</param>
    /// <returns>A pooled document instance wrapping the number value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedJsonValueDocument<T> ForNumber(ReadOnlyMemory<byte> rawUtf8Number)
    {
        return Pool.Rent(rawUtf8Number, JsonTokenType.Number);
    }

    /// <summary>
    /// Creates a <see cref="FixedJsonValueDocument{T}"/> for a string value, using the pool.
    /// </summary>
    /// <param name="rawQuotedUtf8String">The raw UTF-8 string bytes (including surrounding quotes).</param>
    /// <returns>A pooled document instance wrapping the string value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedJsonValueDocument<T> ForString(ReadOnlyMemory<byte> rawQuotedUtf8String)
    {
        return Pool.Rent(rawQuotedUtf8String, JsonTokenType.String);
    }

    /// <summary>
    /// Creates a <see cref="FixedJsonValueDocument{T}"/> for a number value from a span,
    /// copying the data into the document's internal buffer (zero heap allocation).
    /// </summary>
    /// <param name="rawUtf8Number">The raw UTF-8 number bytes (unquoted). Must be &lt;= 64 bytes.</param>
    /// <returns>A pooled document instance wrapping the number value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedJsonValueDocument<T> ForNumberFromSpan(ReadOnlySpan<byte> rawUtf8Number)
    {
        return Pool.RentFromSpan(rawUtf8Number, JsonTokenType.Number);
    }

    /// <summary>
    /// Creates a <see cref="FixedJsonValueDocument{T}"/> for a string value from a span,
    /// copying the data into the document's internal buffer (zero heap allocation).
    /// </summary>
    /// <param name="rawQuotedUtf8String">The raw UTF-8 string bytes (including surrounding quotes). Must be &lt;= 64 bytes.</param>
    /// <returns>A pooled document instance wrapping the string value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedJsonValueDocument<T> ForStringFromSpan(ReadOnlySpan<byte> rawQuotedUtf8String)
    {
        return Pool.RentFromSpan(rawQuotedUtf8String, JsonTokenType.String);
    }

#if NET
    /// <summary>
    /// Gets the root element of the document.
    /// </summary>
    public T RootElement => T.CreateInstance(this, 0);
#else
    /// <summary>
    /// Gets the root element of the document.
    /// </summary>
    public T RootElement => JsonElementHelpers.CreateInstance<T>(this, 0);
#endif

    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
    {
        Debug.Assert(index == 0);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.AppendExternal(_tokenType, 0, _rawValue.Length, workspaceDocumentIndex);
    }

    int IJsonDocument.WriteElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db, int writePosition)
    {
        Debug.Assert(index == 0);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.WriteRowAt(writePosition, new DbRow(_tokenType, 0, _rawValue.Length, workspaceDocumentIndex));
        return 1;
    }

    int IJsonDocument.BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking)
    {
        var db = MetadataDb.CreateRented(DbRow.Size, false);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.AppendExternal(_tokenType, 0, _rawValue.Length, workspaceDocumentIndex);
        return db.TakeOwnership(out rentedBacking);
    }

    JsonElement IJsonDocument.CloneElement(int index) => new(this, 0);

    TElement IJsonDocument.CloneElement<TElement>(int index)
    {
#if NET
        return TElement.CreateInstance(this, 0);
#else
        return JsonElementHelpers.CreateInstance<TElement>(this, 0);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IDisposable.Dispose() => Pool.ReturnDocument(this);

    void IJsonDocument.EnsurePropertyMap(int index) { }

    JsonElement IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, _tokenType);
        return default;
    }

    TElement IJsonDocument.GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, _tokenType);
        return default;
    }

    void IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, _tokenType);
        parentDocument = default;
        parentDocumentIndex = default;
    }

    int IJsonDocument.GetArrayInsertionIndex(int currentIndex, int arrayIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, _tokenType);
        return default;
    }

    int IJsonDocument.GetArrayLength(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, _tokenType);
        return default;
    }

    int IJsonDocument.GetDbSize(int index, bool includeEndElement)
    {
        Debug.Assert(index == 0);
        return DbRow.Size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetHashCode(int index)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            JsonElementHelpers.ParseNumber(_rawValue.Span, out bool isNeg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exp);
            HashCode code = default;
            code.Add(isNeg);
            code.AddBytes(integral);
            code.AddBytes(fractional);
            code.Add(exp);
            return code.ToHashCode();
        }

        return JsonDocument.GetHashCodeForString(_rawValue.Span[1..^1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    JsonTokenType IJsonDocument.GetJsonTokenType(int index)
    {
        Debug.Assert(index == 0);
        return _tokenType;
    }

    string IJsonDocument.GetNameOfPropertyValue(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    int IJsonDocument.GetPropertyCount(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    JsonElement IJsonDocument.GetPropertyName(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    ReadOnlySpan<byte> IJsonDocument.GetPropertyNameRaw(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    ReadOnlyMemory<byte> IJsonDocument.GetPropertyNameRaw(int index, bool includeQuotes)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    UnescapedUtf8JsonString IJsonDocument.GetPropertyNameUnescaped(int index)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    string IJsonDocument.GetPropertyRawValueAsString(int valueIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index, bool includeQuotes)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.String && !includeQuotes)
        {
            return _rawValue[1..^1];
        }

        return _rawValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index)
    {
        Debug.Assert(index == 0);
        return _rawValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index)
    {
        Debug.Assert(index == 0);
        return _rawValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.String && !includeQuotes)
        {
            return new(_rawValue[1..^1]);
        }

        return new(_rawValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string IJsonDocument.GetRawValueAsString(int index)
    {
        Debug.Assert(index == 0);
        return JsonReaderHelper.TranscodeHelper(_rawValue.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IJsonDocument.GetStartIndex(int endIndex)
    {
        Debug.Assert(endIndex == 0);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string? IJsonDocument.GetString(int index, JsonTokenType expectedType)
    {
        Debug.Assert(index == 0);

        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TranscodeHelper(_rawValue.Span[1..^1]);
        }

        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result)
    {
        Debug.Assert(index == 0);

        if (_tokenType == JsonTokenType.String)
        {
            result = JsonReaderHelper.TranscodeHelper(_rawValue.Span[1..^1]);
            return true;
        }

        result = default;
        return false;
    }

    UnescapedUtf8JsonString IJsonDocument.GetUtf8JsonString(int index, JsonTokenType expectedType)
    {
        Debug.Assert(index == 0 && expectedType == JsonTokenType.String);

        if (_tokenType != JsonTokenType.String)
        {
            ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        }

        return new UnescapedUtf8JsonString(_rawValue[1..^1]);
    }

    UnescapedUtf16JsonString IJsonDocument.GetUtf16JsonString(int index, JsonTokenType expectedType)
    {
        Debug.Assert(index == 0 && expectedType == JsonTokenType.String);

        if (_tokenType != JsonTokenType.String)
        {
            ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        }

        ReadOnlySpan<byte> utf8Source = _rawValue.Span[1..^1];
        char[] rentedTranscoded = ArrayPool<char>.Shared.Rent(utf8Source.Length);
        try
        {
            int charsWritten = JsonReaderHelper.TranscodeHelper(utf8Source, rentedTranscoded);
            return new UnescapedUtf16JsonString(rentedTranscoded.AsMemory(0, charsWritten), rentedTranscoded);
        }
        catch
        {
            ArrayPool<char>.Shared.Return(rentedTranscoded);
            throw;
        }
    }

    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
    {
        if (_tokenType != JsonTokenType.String)
        {
            return false;
        }

        byte[]? otherUtf8TextArray = null;
        int length = checked(otherText.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
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
            result = _rawValue.Span[1..^1].SequenceEqual(otherUtf8Text.Slice(0, written));
        }

        if (otherUtf8TextArray != null)
        {
            otherUtf8Text.Slice(0, written).Clear();
            ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
    {
        Debug.Assert(index == 0 && !isPropertyName);

        if (_tokenType != JsonTokenType.String)
        {
            return false;
        }

        return _rawValue.Span[1..^1].SequenceEqual(otherUtf8Text);
    }

    string IJsonDocument.ToString(int index)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TranscodeHelper(_rawValue.Span[1..^1]);
        }

        // For numbers, transcode the raw value directly
        return JsonReaderHelper.TranscodeHelper(_rawValue.Span);
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        elementParent = default;
        elementIndex = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, _tokenType);
        elementParent = default;
        elementIndex = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, [NotNullWhen(true)] out byte[]? value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out sbyte value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out byte value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out short value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out ushort value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out int value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out uint value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out long value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out ulong value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out double value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out float value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out decimal value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return Utf8Parser.TryParse(_rawValue.Span, out value, out int bytesConsumed) &&
                   (uint)_rawValue.Length == (uint)bytesConsumed;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out BigInteger value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return BigInteger.TryParse(_rawValue.Span, out value);
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out BigNumber value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number)
        {
            return BigNumber.TryParse(_rawValue.Span, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out DateTime value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out OffsetDate value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out OffsetTime value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out LocalDate value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out Period value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.TryGetValue(int index, out Guid value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

#if NET
    bool IJsonDocument.TryGetValue(int index, out DateOnly value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out TimeOnly value)
    {
        if (_tokenType == JsonTokenType.String)
        {
            return JsonReaderHelper.TryGetValue(_rawValue.Span[1..^1], false, out value);
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Int128 value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number && Int128.TryParse(_rawValue.Span, out Int128 tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out UInt128 value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number && UInt128.TryParse(_rawValue.Span, out UInt128 tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Half value)
    {
        Debug.Assert(index == 0);
        if (_tokenType == JsonTokenType.Number && Half.TryParse(_rawValue.Span, out Half tmp))
        {
            value = tmp;
            return true;
        }

        value = default;
        return false;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IJsonDocument.ValueIsEscaped(int index, bool isPropertyName)
    {
        Debug.Assert(index == 0);

        // Values produced by the VM never contain escape sequences
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IJsonDocument.WriteElementTo(int index, Utf8JsonWriter writer)
    {
        Debug.Assert(index == 0);

        if (_tokenType == JsonTokenType.String)
        {
            writer.WriteRawValue(_rawValue.Span);
        }
        else
        {
            writer.WriteRawValue(_rawValue.Span);
        }
    }

    void IJsonDocument.WritePropertyName(int index, Utf8JsonWriter writer) => Debug.Fail("");

    bool IJsonDocument.TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> jsonPointer, int index, out TValue value)
    {
        if (jsonPointer.Length > 2 ||
            (jsonPointer.Length > 1 && jsonPointer[1] != (byte)'/') ||
            (jsonPointer.Length == 1 && jsonPointer[0] is not ((byte)'#' or (byte)'/')))
        {
            value = default;
            return false;
        }

#if NET
        value = TValue.CreateInstance(this, 0);
#else
        value = JsonElementHelpers.CreateInstance<TValue>(this, 0);
#endif

        return true;
    }

    bool IJsonDocument.TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset)
    {
        line = 0;
        charOffset = 0;
        lineByteOffset = 0;
        return false;
    }

    bool IJsonDocument.TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset)
    {
        line = 0;
        charOffset = 0;
        lineByteOffset = 0;
        return false;
    }

    bool IJsonDocument.TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line)
    {
        line = default;
        return false;
    }

    bool IJsonDocument.TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line)
    {
        line = null;
        return false;
    }

    public bool TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        ReadOnlySpan<byte> content = GetContentSpan();
        return JsonReaderHelper.TryTranscode(content, destination, out charsWritten);
    }

    public bool TryFormat(int index, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        ReadOnlySpan<byte> content = GetContentSpan();
        if (content.TryCopyTo(destination))
        {
            bytesWritten = content.Length;
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    public string ToString(int index, string? format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        return ((IJsonDocument)this).ToString(index);
    }

    /// <summary>
    /// Gets the content span (for strings, strips quotes; for numbers, returns raw bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> GetContentSpan()
    {
        if (_tokenType == JsonTokenType.String)
        {
            return _rawValue.Span[1..^1];
        }

        return _rawValue.Span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset(ReadOnlyMemory<byte> rawValue, JsonTokenType tokenType)
    {
        _rawValue = rawValue;
        _tokenType = tokenType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetAllStateForCacheReuse()
    {
        if (_rentedBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
            _rentedBuffer = null;
        }

        _rawValue = ReadOnlyMemory<byte>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FixedJsonValueDocument<T> CreateEmptyInstanceForCaching()
    {
        return new FixedJsonValueDocument<T>(ReadOnlyMemory<byte>.Empty, JsonTokenType.None);
    }

    /// <summary>
    /// Defines a thread-local pool for reusable FixedJsonValue document instances.
    /// </summary>
    private static class Pool
    {
        private const int PoolSize = 8;

        [ThreadStatic]
        private static ThreadLocalState? t_threadLocalState;

        /// <summary>
        /// Rents a document from the thread-local pool or creates a new one.
        /// </summary>
        public static FixedJsonValueDocument<T> Rent(ReadOnlyMemory<byte> rawValue, JsonTokenType tokenType)
        {
            ThreadLocalState state = t_threadLocalState ??= new();

            if (state.RentedCount < PoolSize)
            {
                FixedJsonValueDocument<T> document = state.Documents[state.RentedCount];
                document.Reset(rawValue, tokenType);
                state.RentedCount++;
                return document;
            }

            // Pool exhausted — allocate a new instance
            return new FixedJsonValueDocument<T>(rawValue, tokenType);
        }

        /// <summary>
        /// Rents a document from the pool, copying span data into a rented buffer.
        /// The buffer is returned to <see cref="ArrayPool{T}"/> when the document is disposed.
        /// </summary>
        public static FixedJsonValueDocument<T> RentFromSpan(ReadOnlySpan<byte> rawValue, JsonTokenType tokenType)
        {
            ThreadLocalState state = t_threadLocalState ??= new();

            FixedJsonValueDocument<T> document;
            if (state.RentedCount < PoolSize)
            {
                document = state.Documents[state.RentedCount];
                state.RentedCount++;
            }
            else
            {
                document = new FixedJsonValueDocument<T>(ReadOnlyMemory<byte>.Empty, JsonTokenType.None);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(rawValue.Length);
            rawValue.CopyTo(buffer);
            document._rentedBuffer = buffer;
            document._rawValue = new ReadOnlyMemory<byte>(buffer, 0, rawValue.Length);
            document._tokenType = tokenType;
            return document;
        }

        /// <summary>
        /// Returns a document to the thread-local pool for reuse.
        /// </summary>
        public static void ReturnDocument(FixedJsonValueDocument<T> document)
        {
            ThreadLocalState? state = t_threadLocalState;

            if (state is { RentedCount: > 0 })
            {
                // Only return if it belongs to our pool
                for (int i = state.RentedCount - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(state.Documents[i], document))
                    {
                        document.ResetAllStateForCacheReuse();

                        // Swap with last rented to maintain compact rented region
                        if (i < state.RentedCount - 1)
                        {
                            state.Documents[i] = state.Documents[state.RentedCount - 1];
                            state.Documents[state.RentedCount - 1] = document;
                        }

                        state.RentedCount--;
                        return;
                    }
                }
            }

            // Not from our pool — just let GC handle it
            document.ResetAllStateForCacheReuse();
        }

        private sealed class ThreadLocalState
        {
            public readonly FixedJsonValueDocument<T>[] Documents;
            public int RentedCount;

            public ThreadLocalState()
            {
                Documents = new FixedJsonValueDocument<T>[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    Documents[i] = CreateEmptyInstanceForCaching();
                }
            }
        }
    }
}