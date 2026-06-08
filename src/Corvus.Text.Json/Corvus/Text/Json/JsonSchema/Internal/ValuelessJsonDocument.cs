// <copyright file="ValuelessJsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents one of the three "valueless" JSON literals — <c>true</c>, <c>false</c>, and
/// <c>null</c> — whose value is fully determined by its token type and which therefore need no backing
/// buffer at all.
/// </summary>
/// <typeparam name="T">The type of the root element in the document.</typeparam>
/// <remarks>
/// <para>
/// There is exactly one possible <c>true</c>, <c>false</c>, and <c>null</c> value, so this type exposes
/// them as shared <see cref="BooleanTrue"/>, <see cref="BooleanFalse"/>, and <see cref="Null"/>
/// singletons. Each is immutable and allocation-free — safe to hand out from a <c>static readonly</c>
/// field and never dispose.
/// </para>
/// <para>
/// Unlike <see cref="FixedJsonValueDocument{T}"/> this is <em>not</em> an
/// <see cref="IWorkspaceManagedDocument"/> and is not pooled: a workspace will never dispose it on
/// reset, so the same singleton can be shared across many workspaces and runs. <see cref="IDisposable.Dispose"/>
/// is a no-op.
/// </para>
/// </remarks>
[CLSCompliant(false)]
public sealed class ValuelessJsonDocument<T> : IJsonDocument
    where T : struct, IJsonElement<T>
{
    /// <summary>The shared <c>true</c> singleton.</summary>
    public static readonly ValuelessJsonDocument<T> BooleanTrue = new(JsonTokenType.True);

    /// <summary>The shared <c>false</c> singleton.</summary>
    public static readonly ValuelessJsonDocument<T> BooleanFalse = new(JsonTokenType.False);

    /// <summary>The shared <c>null</c> singleton.</summary>
    public static readonly ValuelessJsonDocument<T> Null = new(JsonTokenType.Null);

    private static readonly byte[] TrueBytes = "true"u8.ToArray();
    private static readonly byte[] FalseBytes = "false"u8.ToArray();
    private static readonly byte[] NullBytes = "null"u8.ToArray();

    private readonly JsonTokenType _tokenType;

    private ValuelessJsonDocument(JsonTokenType tokenType) => _tokenType = tokenType;

    JsonWorkspace? IJsonDocument.CachedWorkspace { get; set; }

    int IJsonDocument.CachedWorkspaceDocumentIndex { get; set; }

    int IJsonDocument.CachedWorkspaceGeneration { get; set; }

    bool IJsonDocument.IsDisposable => false;

    bool IJsonDocument.IsImmutable => true;

#if NET
    /// <summary>Gets the root element of the document.</summary>
    public T RootElement => T.CreateInstance(this, 0);
#else
    /// <summary>Gets the root element of the document.</summary>
    public T RootElement => JsonElementHelpers.CreateInstance<T>(this, 0);
#endif

    private ReadOnlyMemory<byte> RawValue => _tokenType switch
    {
        JsonTokenType.True => TrueBytes,
        JsonTokenType.False => FalseBytes,
        _ => NullBytes,
    };

    void IDisposable.Dispose()
    {
        // Singletons are never disposed.
    }

    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
    {
        Debug.Assert(index == 0);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.AppendExternal(_tokenType, 0, RawValue.Length, workspaceDocumentIndex);
    }

    int IJsonDocument.WriteElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db, int writePosition)
    {
        Debug.Assert(index == 0);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.WriteRowAt(writePosition, new DbRow(_tokenType, 0, RawValue.Length, workspaceDocumentIndex));
        return 1;
    }

    int IJsonDocument.BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking)
    {
        var db = MetadataDb.CreateRented(DbRow.Size, false);
        int workspaceDocumentIndex = workspace.GetDocumentIndex(this);
        db.AppendExternal(_tokenType, 0, RawValue.Length, workspaceDocumentIndex);
        return db.TakeOwnership(out rentedBacking);
    }

    JsonElement IJsonDocument.CloneElement(int index)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return JsonElement.ParseValue(RawValue.Span);
#pragma warning restore CS0618
    }

    JsonDocumentBuilder<JsonElement.Mutable> IJsonDocument.CloneElementAsBuilder(int index, JsonWorkspace workspace) => JsonDocumentCloning.CloneElementAsBuilderBySerialization(this, index, workspace);

    TElement IJsonDocument.CloneElement<TElement>(int index)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return JsonElementHelpers.ParseValue<TElement>(RawValue.Span);
#pragma warning restore CS0618
    }

    void IJsonDocument.EnsurePropertyMap(int index)
    {
    }

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

    bool IJsonDocument.TryFindNextDescendantPropertyValue(int elementIndex, ref int scanIndex, ReadOnlySpan<byte> utf8PropertyName, out int valueIndex)
    {
        valueIndex = -1;
        return false;
    }

    int IJsonDocument.GetHashCode(int index)
    {
        Debug.Assert(index == 0);
        return _tokenType switch
        {
            JsonTokenType.True => true.GetHashCode(),
            JsonTokenType.False => false.GetHashCode(),
            _ => JsonDocument.s_nullHashCode,
        };
    }

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

    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index, bool includeQuotes)
    {
        Debug.Assert(index == 0);
        return RawValue;
    }

    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index)
    {
        Debug.Assert(index == 0);
        return RawValue;
    }

    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index)
    {
        Debug.Assert(index == 0);
        return RawValue;
    }

    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes)
    {
        Debug.Assert(index == 0);
        return new(RawValue);
    }

    string IJsonDocument.GetRawValueAsString(int index)
    {
        Debug.Assert(index == 0);
        return JsonReaderHelper.TranscodeHelper(RawValue.Span);
    }

    int IJsonDocument.GetStartIndex(int endIndex)
    {
        Debug.Assert(endIndex == 0);
        return 0;
    }

    string? IJsonDocument.GetString(int index, JsonTokenType expectedType)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        return default;
    }

    bool IJsonDocument.TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result)
    {
        result = default;
        return false;
    }

    UnescapedUtf8JsonString IJsonDocument.GetUtf8JsonString(int index, JsonTokenType expectedType)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        return default;
    }

    UnescapedUtf16JsonString IJsonDocument.GetUtf16JsonString(int index, JsonTokenType expectedType)
    {
        ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.String, _tokenType);
        return default;
    }

    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName) => false;

    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape) => false;

    string IJsonDocument.ToString(int index)
    {
        Debug.Assert(index == 0);
        return JsonReaderHelper.TranscodeHelper(RawValue.Span);
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
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out byte value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out short value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out ushort value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out int value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out uint value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out long value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out ulong value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out double value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out float value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out decimal value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out BigInteger value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out BigNumber value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out DateTime value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out OffsetDate value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out OffsetTime value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out LocalDate value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Period value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Guid value)
    {
        value = default;
        return false;
    }

#if NET
    bool IJsonDocument.TryGetValue(int index, out DateOnly value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out TimeOnly value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Int128 value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out UInt128 value)
    {
        value = default;
        return false;
    }

    bool IJsonDocument.TryGetValue(int index, out Half value)
    {
        value = default;
        return false;
    }
#endif

    bool IJsonDocument.ValueIsEscaped(int index, bool isPropertyName)
    {
        Debug.Assert(index == 0);
        return false;
    }

    void IJsonDocument.WriteElementTo(int index, Utf8JsonWriter writer)
    {
        Debug.Assert(index == 0);

        switch (_tokenType)
        {
            case JsonTokenType.True:
                writer.WriteBooleanValue(value: true);
                break;

            case JsonTokenType.False:
                writer.WriteBooleanValue(value: false);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }

    void IJsonDocument.WritePropertyName(int index, Utf8JsonWriter writer) => Debug.Fail("A valueless literal is never a property name.");

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

    /// <summary>Tries to format the value into a character span.</summary>
    /// <param name="index">The element index (always 0).</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <param name="format">The format (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public bool TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        return JsonReaderHelper.TryTranscode(RawValue.Span, destination, out charsWritten);
    }

    /// <summary>Tries to format the value into a UTF-8 byte span.</summary>
    /// <param name="index">The element index (always 0).</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <param name="format">The format (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded.</returns>
    public bool TryFormat(int index, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        ReadOnlySpan<byte> content = RawValue.Span;
        if (content.TryCopyTo(destination))
        {
            bytesWritten = content.Length;
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <summary>Returns the JSON text of the value.</summary>
    /// <param name="index">The element index (always 0).</param>
    /// <param name="format">The format (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns>The JSON text.</returns>
    public string ToString(int index, string? format, IFormatProvider? formatProvider)
    {
        Debug.Assert(index == 0);
        return ((IJsonDocument)this).ToString(index);
    }
}