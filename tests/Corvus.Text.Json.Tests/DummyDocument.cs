// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;

namespace Corvus.Text.Json.Tests;

internal class DummyDocument : IJsonDocument
{
    private readonly JsonTokenType _tokenType;

    public bool IsDisposable => false;
    public bool IsImmutable => true;

    bool IJsonDocument.IsDisposable { get; }
    bool IJsonDocument.IsImmutable { get; }

    public DummyDocument(JsonTokenType tokenType)
    {
        _tokenType = tokenType;
    }

    public void AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
    { }

    public int BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking)
    { rentedBacking = []; return 0; }

    public JsonElement CloneElement(int index)
    { return default; }

    public TElement CloneElement<TElement>(int index) where TElement : struct, IJsonElement<TElement>
    { return default; }

    public void Dispose()
    { }

    public void EnsurePropertyMap(int index)
    { }

    public JsonElement GetArrayIndexElement(int currentIndex, int arrayIndex)
    { return default; }

    public TElement GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex) where TElement : struct, IJsonElement<TElement>
    { return default; }

    void IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex)
    {
        parentDocument = this;
        parentDocumentIndex = 0;
    }

    public int GetArrayLength(int index)
    { return 0; }

    public int GetDbSize(int index, bool includeEndElement)
    { return 0; }

    public JsonTokenType GetJsonTokenType(int index)
    { return _tokenType; }

    public string GetNameOfPropertyValue(int index)
    { return string.Empty; }

    public int GetPropertyCount(int index)
    { return 0; }

    public ReadOnlySpan<byte> GetPropertyNameRaw(int index)
    { return default; }

    public string GetPropertyRawValueAsString(int valueIndex)
    { return string.Empty; }

    public ReadOnlyMemory<byte> GetRawSimpleValue(int index, bool includeQuotes)
    { return default; }

    public ReadOnlyMemory<byte> GetRawSimpleValue(int index)
    { return default; }

    public ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index)
    { return default; }

    public RawUtf8JsonString GetRawValue(int index, bool includeQuotes)
    { return default; }

    public string GetRawValueAsString(int index)
    { return string.Empty; }

    public string GetString(int index, JsonTokenType expectedType)
    { return string.Empty; }

    public UnescapedUtf8JsonString GetUtf8JsonString(int index, JsonTokenType expectedType)
    { return default; }

    public UnescapedUtf16JsonString GetUtf16JsonString(int index, JsonTokenType expectedType)
    { return default; }

    public bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
    { return false; }

    public bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
    { return false; }

    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
    { value = default; return false; }

    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
    { value = default; return false; }

    public bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value) where TElement : struct, IJsonElement<TElement>
    { value = default; return false; }

    public bool TryGetValue(int index, [NotNullWhen(true)] out byte[] value)
    { value = default; return false; }

    public bool TryGetValue(int index, out sbyte value)
    { value = default; return false; }

    public bool TryGetValue(int index, out byte value)
    { value = default; return false; }

    public bool TryGetValue(int index, out short value)
    { value = default; return false; }

    public bool TryGetValue(int index, out ushort value)
    { value = default; return false; }

    public bool TryGetValue(int index, out int value)
    { value = default; return false; }

    public bool TryGetValue(int index, out uint value)
    { value = default; return false; }

    public bool TryGetValue(int index, out long value)
    { value = default; return false; }

    public bool TryGetValue(int index, out ulong value)
    { value = default; return false; }

    public bool TryGetValue(int index, out double value)
    { value = default; return false; }

    public bool TryGetValue(int index, out float value)
    { value = default; return false; }

    public bool TryGetValue(int index, out decimal value)
    { value = default; return false; }

    public bool TryGetValue(int index, out BigInteger value)
    { value = default; return false; }

    public bool TryGetValue(int index, out BigNumber value)
    { value = default; return false; }

    public bool TryGetValue(int index, out DateTime value)
    { value = default; return false; }

    public bool TryGetValue(int index, out DateTimeOffset value)
    { value = default; return false; }

    public bool TryGetValue(int index, out OffsetDateTime value)
    { value = default; return false; }

    public bool TryGetValue(int index, out OffsetDate value)
    { value = default; return false; }

    public bool TryGetValue(int index, out OffsetTime value)
    { value = default; return false; }

    public bool TryGetValue(int index, out LocalDate value)
    { value = default; return false; }

    public bool TryGetValue(int index, out Period value)
    { value = default; return false; }

    public bool TryGetValue(int index, out Guid value)
    { value = default; return false; }

#if NET

    public bool TryGetValue(int index, out DateOnly value)
    { value = default; return false; }

    public bool TryGetValue(int index, out TimeOnly value)
    { value = default; return false; }

    public bool TryGetValue(int index, out Int128 value)
    { value = default; return false; }

    public bool TryGetValue(int index, out UInt128 value)
    { value = default; return false; }

    public bool TryGetValue(int index, out Half value)
    { value = default; return false; }

#endif


    public bool ValueIsEscaped(int index, bool isPropertyName)
    { return false; }

    public void WriteElementTo(int index, Utf8JsonWriter writer)
    { }

    public void WritePropertyName(int index, Utf8JsonWriter writer)
    { }

    public int GetHashCode(int index) => default;

    public string ToString(int index) => string.Empty;

    public bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
        where TElement : struct, IJsonElement<TElement>
    {
        value = default;
        return false;
    }

    public JsonElement GetPropertyName(int index) => default;
    void IJsonDocument.EnsurePropertyMap(int index) => throw new NotImplementedException();
    int IJsonDocument.GetHashCode(int index) => throw new NotImplementedException();
    string IJsonDocument.ToString(int index) => throw new NotImplementedException();
    JsonTokenType IJsonDocument.GetJsonTokenType(int index) => GetJsonTokenType(index);
    JsonElement IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex) => throw new NotImplementedException();
    TElement IJsonDocument.GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex) => throw new NotImplementedException();
    int IJsonDocument.GetArrayLength(int index) => throw new NotImplementedException();
    int IJsonDocument.GetPropertyCount(int index) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out IJsonDocument elementParent, out int elementIndex) => throw new NotImplementedException();
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out IJsonDocument elementParent, out int elementIndex) => throw new NotImplementedException();
    string IJsonDocument.GetString(int index, JsonTokenType expectedType) => throw new NotImplementedException();
    bool IJsonDocument.TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string result) => throw new NotImplementedException();
    UnescapedUtf8JsonString IJsonDocument.GetUtf8JsonString(int index, JsonTokenType expectedType) => throw new NotImplementedException();
    UnescapedUtf16JsonString IJsonDocument.GetUtf16JsonString(int index, JsonTokenType expectedType) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out byte[] value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out sbyte value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out byte value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out short value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out ushort value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out int value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out uint value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out long value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out ulong value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out double value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out float value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out decimal value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out BigInteger value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out BigNumber value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out DateTime value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out DateTimeOffset value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out OffsetDateTime value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out OffsetDate value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out OffsetTime value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out LocalDate value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out Period value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out Guid value) => throw new NotImplementedException();
#if NET
    bool IJsonDocument.TryGetValue(int index, out DateOnly value) => throw new NotImplementedException();
    bool IJsonDocument.TryGetValue(int index, out TimeOnly value) => throw new NotImplementedException();
#endif
    string IJsonDocument.GetNameOfPropertyValue(int index) => throw new NotImplementedException();
    ReadOnlySpan<byte> IJsonDocument.GetPropertyNameRaw(int index) => throw new NotImplementedException();
    JsonElement IJsonDocument.GetPropertyName(int index) => throw new NotImplementedException();
    string IJsonDocument.GetRawValueAsString(int index) => throw new NotImplementedException();
    string IJsonDocument.GetPropertyRawValueAsString(int valueIndex) => throw new NotImplementedException();
    RawUtf8JsonString IJsonDocument.GetRawValue(int index, bool includeQuotes) => throw new NotImplementedException();
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index, bool includeQuotes) => throw new NotImplementedException();
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValue(int index) => throw new NotImplementedException();
    ReadOnlyMemory<byte> IJsonDocument.GetRawSimpleValueUnsafe(int index) => throw new NotImplementedException();
    bool IJsonDocument.ValueIsEscaped(int index, bool isPropertyName) => throw new NotImplementedException();
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName) => throw new NotImplementedException();
    bool IJsonDocument.TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape) => throw new NotImplementedException();
    void IJsonDocument.WriteElementTo(int index, Utf8JsonWriter writer) => throw new NotImplementedException();
    void IJsonDocument.WritePropertyName(int index, Utf8JsonWriter writer) => throw new NotImplementedException();
    JsonElement IJsonDocument.CloneElement(int index) => throw new NotImplementedException();
    TElement IJsonDocument.CloneElement<TElement>(int index) => throw new NotImplementedException();
    int IJsonDocument.GetDbSize(int index, bool includeEndElement) => throw new NotImplementedException();
    int IJsonDocument.GetStartIndex(int endIndex) => throw new NotImplementedException();
    int IJsonDocument.BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking) => throw new NotImplementedException();
    void IJsonDocument.AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db) => throw new NotImplementedException();

    bool IJsonDocument.TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> jsonPointer, int index, out TValue value)
    {
        value = default;
        return false;
    }

    void IDisposable.Dispose() => throw new NotImplementedException();
    int IJsonDocument.GetArrayInsertionIndex(int currentIndex, int arrayIndex) => throw new NotImplementedException();
    ReadOnlyMemory<byte> IJsonDocument.GetPropertyNameRaw(int index, bool includeQuotes) => throw new NotImplementedException();
    UnescapedUtf8JsonString IJsonDocument.GetPropertyNameUnescaped(int index) => throw new NotImplementedException();
    bool IJsonDocument.TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider formatProvider) => throw new NotImplementedException();
    bool IJsonDocument.TryFormat(int index, Span<byte> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider formatProvider) => throw new NotImplementedException();
    string IJsonDocument.ToString(int index, string format, IFormatProvider formatProvider) => throw new NotImplementedException();

    bool IJsonDocument.TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset)
    { line = 0; charOffset = 0; lineByteOffset = 0; return false; }

    bool IJsonDocument.TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset)
    { line = 0; charOffset = 0; lineByteOffset = 0; return false; }

    bool IJsonDocument.TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line)
    { line = default; return false; }

    bool IJsonDocument.TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line)
    { line = null; return false; }
}