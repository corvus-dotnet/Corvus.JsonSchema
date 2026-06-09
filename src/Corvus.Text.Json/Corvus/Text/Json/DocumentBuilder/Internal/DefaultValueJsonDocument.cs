// <copyright file="DefaultValueJsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// A read-only <see cref="IMutableJsonDocument"/> facade over an immutable document, used to back the
/// mutable view of a schema <c>default</c> value (issue #811).
/// </summary>
/// <remarks>
/// <para>
/// A mutable element (<c>T.Mutable</c>) must be backed by an <see cref="IMutableJsonDocument"/>, but a
/// schema default is materialised as an <em>immutable</em> instance. This facade lets an absent,
/// non-nullable defaulted property be surfaced through the mutable view without copying any data: every
/// read is forwarded to the wrapped immutable <paramref name="inner"/> document, and every mutating
/// operation throws an <see cref="InvalidOperationException"/> whose message tells the caller to set the
/// value on its parent before modifying it.
/// </para>
/// <para>
/// This type owns no resources — it merely wraps <paramref name="inner"/> — so <see cref="Dispose"/> is a
/// no-op and never disposes the wrapped document.
/// </para>
/// </remarks>
/// <param name="inner">The immutable document that holds the default value.</param>
[CLSCompliant(false)]
public sealed class DefaultValueJsonDocument(IJsonDocument inner) : IMutableJsonDocument
{
    private readonly IJsonDocument inner = inner;

    /// <inheritdoc />
    public bool IsImmutable => true;

    /// <inheritdoc />
    public bool IsDisposable => false;

    /// <inheritdoc />
    public ulong Version => 0;

    /// <inheritdoc />
    public JsonWorkspace? CachedWorkspace
    {
        get => this.inner.CachedWorkspace;
        set => this.inner.CachedWorkspace = value;
    }

    /// <inheritdoc />
    public int CachedWorkspaceDocumentIndex
    {
        get => this.inner.CachedWorkspaceDocumentIndex;
        set => this.inner.CachedWorkspaceDocumentIndex = value;
    }

    /// <inheritdoc />
    public int CachedWorkspaceGeneration
    {
        get => this.inner.CachedWorkspaceGeneration;
        set => this.inner.CachedWorkspaceGeneration = value;
    }

    /// <inheritdoc />
    public int ParentWorkspaceIndex => throw Frozen();

    /// <inheritdoc />
    public JsonWorkspace Workspace => throw Frozen();

    // ──────────────────────────────────────────────────────────────────────────
    // IMutableJsonDocument reads: forward to the inner document, but surface the
    // result as a mutable element backed by this facade so nested mutation also
    // raises the indicative exception.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public TElement FreezeElement<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>
#if NET
        => TElement.CreateInstance(this.inner, index);
#else
        => JsonElementHelpers.CreateInstance<TElement>(this.inner, index);
#endif

    /// <inheritdoc />
    public TElement RefreshElementUnsafe<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>
#if NET
        => TElement.CreateInstance(this, index);
#else
        => JsonElementHelpers.CreateInstance<TElement>(this, index);
#endif

    /// <inheritdoc />
    public JsonElement.Mutable GetArrayIndexElement(int currentIndex, int arrayIndex)
    {
        this.inner.GetArrayIndexElement(currentIndex, arrayIndex, out _, out int valueIndex);
        return new JsonElement.Mutable(this, valueIndex);
    }

    /// <inheritdoc />
    public void GetArrayIndexElement(int currentIndex, int arrayIndex, out IMutableJsonDocument parentDocument, out int parentDocumentIndex)
    {
        this.inner.GetArrayIndexElement(currentIndex, arrayIndex, out _, out parentDocumentIndex);
        parentDocument = this;
    }

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement.Mutable value)
    {
        if (this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out int valueIndex))
        {
            value = new JsonElement.Mutable(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement.Mutable value)
    {
        if (this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out int valueIndex))
        {
            value = new JsonElement.Mutable(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<char> propertyName, out int valueIndex)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out valueIndex);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<byte> propertyName, out int valueIndex)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out valueIndex);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValueIndex(ref MetadataDb parsedData, int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out int valueIndex)
        => throw Frozen();

    // ──────────────────────────────────────────────────────────────────────────
    // IJsonDocument reads: forward verbatim to the inner document.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void EnsurePropertyMap(int index) => this.inner.EnsurePropertyMap(index);

    /// <inheritdoc />
    public int GetHashCode(int index) => this.inner.GetHashCode(index);

    /// <inheritdoc />
    public string ToString(int index) => this.inner.ToString(index);

    /// <inheritdoc />
    public JsonTokenType GetJsonTokenType(int index) => this.inner.GetJsonTokenType(index);

    /// <inheritdoc />
    JsonElement IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex) => this.inner.GetArrayIndexElement(currentIndex, arrayIndex);

    /// <inheritdoc />
    public TElement GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex)
        where TElement : struct, IJsonElement<TElement>
    {
        this.inner.GetArrayIndexElement(currentIndex, arrayIndex, out _, out int valueIndex);
        return this.Create<TElement>(valueIndex);
    }

    /// <inheritdoc />
    void IJsonDocument.GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex)
        => this.inner.GetArrayIndexElement(currentIndex, arrayIndex, out parentDocument, out parentDocumentIndex);

    /// <inheritdoc />
    public int GetArrayInsertionIndex(int currentIndex, int arrayIndex) => this.inner.GetArrayInsertionIndex(currentIndex, arrayIndex);

    /// <inheritdoc />
    public int GetArrayLength(int index) => this.inner.GetArrayLength(index);

    /// <inheritdoc />
    public int GetPropertyCount(int index) => this.inner.GetPropertyCount(index);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out value);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out value);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value)
        where TElement : struct, IJsonElement<TElement>
    {
        if (this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out int valueIndex))
        {
            value = this.Create<TElement>(valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
        where TElement : struct, IJsonElement<TElement>
    {
        if (this.inner.TryGetNamedPropertyValue(index, propertyName, out _, out int valueIndex))
        {
            value = this.Create<TElement>(valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out elementParent, out elementIndex);

    /// <inheritdoc />
    public bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex)
        => this.inner.TryGetNamedPropertyValue(index, propertyName, out elementParent, out elementIndex);

    /// <inheritdoc />
    public string? GetString(int index, JsonTokenType expectedType) => this.inner.GetString(index, expectedType);

    /// <inheritdoc />
    public bool TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result) => this.inner.TryGetString(index, expectedType, out result);

    /// <inheritdoc />
    public UnescapedUtf8JsonString GetUtf8JsonString(int index, JsonTokenType expectedType) => this.inner.GetUtf8JsonString(index, expectedType);

    /// <inheritdoc />
    public UnescapedUtf16JsonString GetUtf16JsonString(int index, JsonTokenType expectedType) => this.inner.GetUtf16JsonString(index, expectedType);

    /// <inheritdoc />
    public bool TryGetValue(int index, [NotNullWhen(true)] out byte[]? value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out sbyte value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out byte value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out short value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out ushort value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out int value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out uint value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out long value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out ulong value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out double value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out float value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out decimal value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out BigInteger value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out BigNumber value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out DateTime value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out DateTimeOffset value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out OffsetDateTime value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out OffsetDate value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out OffsetTime value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out LocalDate value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out Period value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out Guid value) => this.inner.TryGetValue(index, out value);

#if NET

    /// <inheritdoc />
    public bool TryGetValue(int index, out Int128 value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out UInt128 value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out Half value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out DateOnly value) => this.inner.TryGetValue(index, out value);

    /// <inheritdoc />
    public bool TryGetValue(int index, out TimeOnly value) => this.inner.TryGetValue(index, out value);

#endif

    /// <inheritdoc />
    public string GetNameOfPropertyValue(int index) => this.inner.GetNameOfPropertyValue(index);

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetPropertyNameRaw(int index) => this.inner.GetPropertyNameRaw(index);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPropertyNameRaw(int index, bool includeQuotes) => this.inner.GetPropertyNameRaw(index, includeQuotes);

    /// <inheritdoc />
    public JsonElement GetPropertyName(int index) => this.inner.GetPropertyName(index);

    /// <inheritdoc />
    public UnescapedUtf8JsonString GetPropertyNameUnescaped(int index) => this.inner.GetPropertyNameUnescaped(index);

    /// <inheritdoc />
    public string GetRawValueAsString(int index) => this.inner.GetRawValueAsString(index);

    /// <inheritdoc />
    public string GetPropertyRawValueAsString(int valueIndex) => this.inner.GetPropertyRawValueAsString(valueIndex);

    /// <inheritdoc />
    public RawUtf8JsonString GetRawValue(int index, bool includeQuotes) => this.inner.GetRawValue(index, includeQuotes);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetRawSimpleValue(int index, bool includeQuotes) => this.inner.GetRawSimpleValue(index, includeQuotes);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetRawSimpleValue(int index) => this.inner.GetRawSimpleValue(index);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index) => this.inner.GetRawSimpleValueUnsafe(index);

    /// <inheritdoc />
    public bool ValueIsEscaped(int index, bool isPropertyName) => this.inner.ValueIsEscaped(index, isPropertyName);

    /// <inheritdoc />
    public bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName) => this.inner.TextEquals(index, otherText, isPropertyName);

    /// <inheritdoc />
    public bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape) => this.inner.TextEquals(index, otherUtf8Text, isPropertyName, shouldUnescape);

    /// <inheritdoc />
    public void WriteElementTo(int index, Utf8JsonWriter writer) => this.inner.WriteElementTo(index, writer);

    /// <inheritdoc />
    public void WritePropertyName(int index, Utf8JsonWriter writer) => this.inner.WritePropertyName(index, writer);

    /// <inheritdoc />
    public JsonElement CloneElement(int index) => this.inner.CloneElement(index);

    /// <inheritdoc />
    public TElement CloneElement<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>
        => this.Create<TElement>(index);

    /// <inheritdoc />
    JsonDocumentBuilder<JsonElement.Mutable> IJsonDocument.CloneElementAsBuilder(int index, JsonWorkspace workspace)
        => this.inner.CloneElementAsBuilder(index, workspace);

    /// <inheritdoc />
    public int GetDbSize(int index, bool includeEndElement) => this.inner.GetDbSize(index, includeEndElement);

    /// <inheritdoc />
    public bool TryFindNextDescendantPropertyValue(int elementIndex, ref int scanIndex, ReadOnlySpan<byte> utf8PropertyName, out int valueIndex)
        => this.inner.TryFindNextDescendantPropertyValue(elementIndex, ref scanIndex, utf8PropertyName, out valueIndex);

    /// <inheritdoc />
    public int GetStartIndex(int endIndex) => this.inner.GetStartIndex(endIndex);

    /// <inheritdoc />
    public int BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking)
        => this.inner.BuildRentedMetadataDb(parentDocumentIndex, workspace, out rentedBacking);

    /// <inheritdoc />
    public void AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db)
        => this.inner.AppendElementToMetadataDb(index, workspace, ref db);

    /// <inheritdoc />
    public int WriteElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db, int writePosition)
        => this.inner.WriteElementToMetadataDb(index, workspace, ref db, writePosition);

    /// <inheritdoc />
    public bool TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> jsonPointer, int index, out TValue value)
        where TValue : struct, IJsonElement<TValue>
        => this.inner.TryResolveJsonPointer(jsonPointer, index, out value);

    /// <inheritdoc />
    public bool TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
        => this.inner.TryFormat(index, destination, out charsWritten, format, formatProvider);

    /// <inheritdoc />
    public bool TryFormat(int index, Span<byte> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
        => this.inner.TryFormat(index, destination, out charsWritten, format, formatProvider);

    /// <inheritdoc />
    public string ToString(int index, string? format, IFormatProvider? formatProvider) => this.inner.ToString(index, format, formatProvider);

    /// <inheritdoc />
    public bool TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset)
        => this.inner.TryGetLineAndOffset(index, out line, out charOffset, out lineByteOffset);

    /// <inheritdoc />
    public bool TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset)
        => this.inner.TryGetLineAndOffsetForPointer(jsonPointer, index, out line, out charOffset, out lineByteOffset);

    /// <inheritdoc />
    public bool TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line) => this.inner.TryGetLine(lineNumber, out line);

    /// <inheritdoc />
    public bool TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line) => this.inner.TryGetLine(lineNumber, out line);

    /// <summary>
    /// Does nothing: this facade does not own the wrapped document, so it must not dispose it.
    /// </summary>
    public void Dispose()
    {
        // No-op. This is purely a facade over an immutable document it does not own.
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IMutableJsonDocument mutators: a default value cannot be modified in place.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public int StoreRawNumberValue(ReadOnlySpan<byte> value) => throw Frozen();

    /// <inheritdoc />
    public int StorePrebakedValue(ReadOnlySpan<byte> prebakedValue) => throw Frozen();

    /// <inheritdoc />
    public int StoreNullValue() => throw Frozen();

    /// <inheritdoc />
    public int StoreBooleanValue(bool value) => throw Frozen();

    /// <inheritdoc />
    public int EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping) => throw Frozen();

    /// <inheritdoc />
    public int EscapeAndStoreRawStringValue(ReadOnlySpan<byte> value, out bool requiredEscaping) => throw Frozen();

    /// <inheritdoc />
    public int StoreRawStringValue(ReadOnlySpan<byte> value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(Guid value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in DateTime value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in DateTimeOffset value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in OffsetDateTime value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in OffsetDate value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in OffsetTime value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in LocalDate value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in Period value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(sbyte value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(byte value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(int value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(uint value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(long value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(ulong value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(short value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(ushort value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(float value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(double value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(decimal value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in BigInteger value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(in BigNumber value) => throw Frozen();

#if NET

    /// <inheritdoc />
    public int StoreValue(Int128 value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(UInt128 value) => throw Frozen();

    /// <inheritdoc />
    public int StoreValue(Half value) => throw Frozen();

#endif

    /// <inheritdoc />
    public void RemoveRange(int complexObjectStartIndex, int startIndex, int endIndex, int membersToRemove) => throw Frozen();

    /// <inheritdoc />
    public void SetAndDispose(ref ComplexValueBuilder cvb) => throw Frozen();

    /// <inheritdoc />
    public void ReplaceRootAndDispose(ref ComplexValueBuilder cvb) => throw Frozen();

    /// <inheritdoc />
    public void InsertAndDispose(int complexObjectStartIndex, int index, ref ComplexValueBuilder cvb) => throw Frozen();

    /// <inheritdoc />
    public void OverwriteAndDispose(int complexObjectStartIndex, int startIndex, int endIndex, int membersToOverwrite, ref ComplexValueBuilder cvb) => throw Frozen();

    /// <inheritdoc />
    public void InsertSimpleValue(int complexObjectStartIndex, int targetIndex, int memberCount, JsonTokenType tokenType, int location, int sizeOrLength) => throw Frozen();

    /// <inheritdoc />
    public void OverwriteSimpleValue(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, JsonTokenType tokenType, int location, int sizeOrLength) => throw Frozen();

    /// <inheritdoc />
    public void InsertSimpleProperty(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, JsonTokenType valueTokenType, int valueLocation, int valueSizeOrLength) => throw Frozen();

    /// <inheritdoc />
    public void InsertFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, IJsonDocument sourceDocument, int sourceIndex) => throw Frozen();

    /// <inheritdoc />
    public void OverwriteFromDocument(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, IJsonDocument sourceDocument, int sourceIndex) => throw Frozen();

    /// <inheritdoc />
    public void InsertPropertyFromDocument(int complexObjectStartIndex, int targetIndex, int memberCount, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex) => throw Frozen();

    /// <inheritdoc />
    public bool TryReplacePropertyValue(int objectIndex, ReadOnlySpan<byte> propertyName, JsonTokenType tokenType, int location, int sizeOrLength) => throw Frozen();

    /// <inheritdoc />
    public bool TryReplacePropertyFromDocument(int objectIndex, ReadOnlySpan<byte> propertyName, IJsonDocument sourceDocument, int sourceIndex) => throw Frozen();

    /// <inheritdoc />
    public void CopyValueToProperty(int srcValueIndex, int dstObjectIndex, ReadOnlySpan<byte> propertyName) => throw Frozen();

    /// <inheritdoc />
    public void CopyValueToArrayIndex(int srcValueIndex, int dstArrayIndex, int itemIndex) => throw Frozen();

    /// <inheritdoc />
    public void CopyValueToArrayEnd(int srcValueIndex, int dstArrayIndex) => throw Frozen();

    /// <inheritdoc />
    public bool MovePropertyToProperty(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstObjectIndex, ReadOnlySpan<byte> dstPropertyName) => throw Frozen();

    /// <inheritdoc />
    public bool MovePropertyToArray(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex, int destIndex) => throw Frozen();

    /// <inheritdoc />
    public bool MovePropertyToArrayEnd(int srcObjectIndex, ReadOnlySpan<byte> srcPropertyName, int dstArrayIndex) => throw Frozen();

    /// <inheritdoc />
    public void MoveItemToArray(int srcArrayIndex, int srcIndex, int dstArrayIndex, int destIndex) => throw Frozen();

    /// <inheritdoc />
    public void MoveItemToArrayEnd(int srcArrayIndex, int srcIndex, int dstArrayIndex) => throw Frozen();

    /// <inheritdoc />
    public void MoveItemToProperty(int srcArrayIndex, int srcIndex, int dstObjectIndex, ReadOnlySpan<byte> destPropertyName) => throw Frozen();

    // Builds an element at the given index backed by this facade (never by the inner immutable
    // document), so that mutable child elements wrap an IMutableJsonDocument and nested reads and
    // mutation attempts continue to flow through this default-value facade.
    private TElement Create<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>
#if NET
        => TElement.CreateInstance(this, index);
#else
        => JsonElementHelpers.CreateInstance<TElement>(this, index);
#endif

    private static InvalidOperationException Frozen() => new(SR.CannotModifyADefaultValue);
}