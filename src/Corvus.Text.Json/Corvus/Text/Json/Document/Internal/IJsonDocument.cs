// <copyright file="IJsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// The interface explicitly implemented by JSON Document providers
/// for internal use only.
/// </summary>
[CLSCompliant(false)]
public interface IJsonDocument : IDisposable
{
    /// <summary>
    /// Ensures the property map is available for the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    void EnsurePropertyMap(int index);

    /// <summary>
    /// Gets a value indicating whether the document is disposable.
    /// </summary>
    bool IsDisposable { get; }

    /// <summary>
    /// Gets a value indicating whether the document is immutable.
    /// </summary>
    bool IsImmutable { get; }

    /// <summary>
    /// Gets the hash code for the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The hash code.</returns>
    int GetHashCode(int index);

    /// <summary>
    /// Converts the element at the specified index to a string.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The string representation of the element.</returns>
    string ToString(int index);

    /// <summary>
    /// Gets the JSON token type for the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The JSON token type.</returns>
    JsonTokenType GetJsonTokenType(int index);

    /// <summary>
    /// Gets the element at the specified array index within the current index.
    /// </summary>
    /// <param name="currentIndex">The current index.</param>
    /// <param name="arrayIndex">The array index.</param>
    /// <returns>The JSON element.</returns>
    JsonElement GetArrayIndexElement(int currentIndex, int arrayIndex);

    /// <summary>
    /// Gets the element at the specified array index within the current index.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="currentIndex">The current index.</param>
    /// <param name="arrayIndex">The array index.</param>
    /// <returns>The JSON element.</returns>
    TElement GetArrayIndexElement<TElement>(int currentIndex, int arrayIndex)
        where TElement : struct, IJsonElement<TElement>;

    /// <summary>
    /// Gets the element at the specified array index within the current index.
    /// </summary>
    /// <param name="currentIndex">The current index.</param>
    /// <param name="arrayIndex">The array index.</param>
    /// <param name="parentDocument">Produces the parent document of the result.</param>
    /// <param name="parentDocumentIndex">Produces the parent document index.</param>
    void GetArrayIndexElement(int currentIndex, int arrayIndex, out IJsonDocument parentDocument, out int parentDocumentIndex);

    /// <summary>
    /// Gets DB index of the item at the array index within the array that starts at <paramref name="currentIndex"/>.
    /// </summary>
    /// <param name="currentIndex">The current index.</param>
    /// <param name="arrayIndex">The array index.</param>
    /// <remarks>Note that this is the DB index in the current document. Contrast with <see cref="GetArrayIndexElement"/> overloads which
    /// return the document and index of the actual element value.</remarks>
    int GetArrayInsertionIndex(int currentIndex, int arrayIndex);

    /// <summary>
    /// Gets the length of the array at the specified index.
    /// </summary>
    /// <param name="index">The index of the array.</param>
    /// <returns>The length of the array.</returns>
    int GetArrayLength(int index);

    /// <summary>
    /// Gets the number of properties for the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The number of properties.</returns>
    int GetPropertyCount(int index);

    /// <summary>
    /// Tries to get the value of a named property as a JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value);

    /// <summary>
    /// Tries to get the value of a named property as a JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value);

    /// <summary>
    /// Tries to get the value of a named property as a JSON element.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value)
        where TElement : struct, IJsonElement<TElement>;

    /// <summary>
    /// Tries to get the value of a named property as a JSON element.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
        where TElement : struct, IJsonElement<TElement>;

    /// <summary>
    /// Tries to get the value of a named property as a mutable JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="elementParent">The parent document of the retrieved value.</param>
    /// <param name="elementIndex">The index of the retrieved value in the parent document.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex);

    /// <summary>
    /// Tries to get the value of a named property as a mutable JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="elementParent">The parent document of the retrieved value.</param>
    /// <param name="elementIndex">The index of the retrieved value in the parent document.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIndex);

    /// <summary>
    /// Gets the string value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="expectedType">The expected JSON token type.</param>
    /// <returns>The string value.</returns>
    string? GetString(int index, JsonTokenType expectedType);

    /// <summary>
    /// Tries to get the string value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="expectedType">The expected JSON token type.</param>
    /// <param name="result">The string value, or <see langword="null"/> if the value was not retrieved.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetString(int index, JsonTokenType expectedType, [NotNullWhen(true)] out string? result);

    /// <summary>
    /// Gets the UTF-8 JSON string value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="expectedType">The expected JSON token type.</param>
    /// <returns>The UTF-8 JSON string value.</returns>
    /// <remarks>
    /// You are permitted to pass <see cref="JsonTokenType.None"/> as the
    /// <paramref name="expectedType"/> which will check both
    /// String and PropertyName as valid types.
    /// </remarks>
    UnescapedUtf8JsonString GetUtf8JsonString(int index, JsonTokenType expectedType);

    /// <summary>
    /// Gets the UTF-16 JSON string value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="expectedType">The expected JSON token type.</param>
    /// <returns>The UTF-16 JSON string value.</returns>
    /// <remarks>
    /// You are permitted to pass <see cref="JsonTokenType.None"/> as the
    /// <paramref name="expectedType"/> which will check both
    /// String and PropertyName as valid types.
    /// </remarks>
    UnescapedUtf16JsonString GetUtf16JsonString(int index, JsonTokenType expectedType);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a byte array.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The byte array value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, [NotNullWhen(true)] out byte[]? value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="sbyte"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="sbyte"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out sbyte value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="byte"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="byte"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out byte value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="short"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="short"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out short value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="ushort"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="ushort"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out ushort value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="int"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="int"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out int value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="uint"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="uint"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out uint value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="long"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="long"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out long value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="ulong"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="ulong"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out ulong value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="double"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="double"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out double value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="float"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="float"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out float value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="decimal"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="decimal"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out decimal value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="BigInteger"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="BigInteger"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out BigInteger value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out BigNumber value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="DateTime"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out DateTime value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out DateTimeOffset value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="OffsetDateTime"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="OffsetDateTime"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out OffsetDateTime value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="OffsetDate"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="OffsetDate"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out OffsetDate value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="OffsetTime"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="OffsetTime"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out OffsetTime value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="LocalDate"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="LocalDate"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out LocalDate value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="Period"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="Period"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out Period value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="Guid"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="Guid"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out Guid value);
#if NET

    /// <summary>
    /// Tries to get the value of the element at the specified index as an <see cref="Int128"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="Int128"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out Int128 value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="UInt128"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="UInt128"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out UInt128 value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="Half"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="Half"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out Half value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="DateOnly"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="DateOnly"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out DateOnly value);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="TimeOnly"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="TimeOnly"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetValue(int index, out TimeOnly value);

#endif

    /// <summary>
    /// Gets the name of the property value at the specified index.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <returns>The name of the property value.</returns>
    string GetNameOfPropertyValue(int index);

    /// <summary>
    /// Gets the raw property name as a byte span for the specified index.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <returns>The raw property name as a byte span.</returns>
    ReadOnlySpan<byte> GetPropertyNameRaw(int index);

    /// <summary>
    /// Gets the raw property name as a byte span for the specified index.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <param name="includeQuotes">Whether to include quotes in the raw property name.</param>
    /// <returns>The raw property name as a byte span.</returns>
    ReadOnlyMemory<byte> GetPropertyNameRaw(int index, bool includeQuotes);

    /// <summary>
    /// Gets the property name as a JSON element.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <returns>The raw property name as a byte span.</returns>
    JsonElement GetPropertyName(int index);

    /// <summary>
    /// Gets the property name as a JSON element.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <returns>The unescaped property name.</returns>
    UnescapedUtf8JsonString GetPropertyNameUnescaped(int index);

    /// <summary>
    /// Gets the raw value of the element at the specified index as a string.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw value as a string.</returns>
    string GetRawValueAsString(int index);

    /// <summary>
    /// Gets the raw value of the property at the specified index as a string.
    /// </summary>
    /// <param name="valueIndex">The index of the property value.</param>
    /// <returns>The raw value as a string.</returns>
    string GetPropertyRawValueAsString(int valueIndex);

    /// <summary>
    /// Gets the raw value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes in the raw value.</param>
    /// <returns>The raw value.</returns>
    RawUtf8JsonString GetRawValue(int index, bool includeQuotes);

    /// <summary>
    /// Gets the raw simple value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeQuotes">Whether to include quotes in the raw value.</param>
    /// <returns>The raw simple value.</returns>
    ReadOnlyMemory<byte> GetRawSimpleValue(int index, bool includeQuotes);

    /// <summary>
    /// Gets the raw simple value of the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw simple value.</returns>
    ReadOnlyMemory<byte> GetRawSimpleValue(int index);

    /// <summary>
    /// Gets the raw simple value of the element at the specified index, without
    /// checking if the document has been disposed.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The raw simple value.</returns>
    ReadOnlyMemory<byte> GetRawSimpleValueUnsafe(int index);

    /// <summary>
    /// Determines whether the value at the specified index is escaped.
    /// </summary>
    /// <param name="index">The index of the value.</param>
    /// <param name="isPropertyName">Whether the value is a property name.</param>
    /// <returns><c>true</c> if the value is escaped; otherwise, <c>false</c>.</returns>
    bool ValueIsEscaped(int index, bool isPropertyName);

    /// <summary>
    /// Determines whether the text at the specified index equals the specified text.
    /// </summary>
    /// <param name="index">The index of the text.</param>
    /// <param name="otherText">The text to compare.</param>
    /// <param name="isPropertyName">Whether the text is a property name.</param>
    /// <returns><c>true</c> if the text equals the specified text; otherwise, <c>false</c>.</returns>
    bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName);

    /// <summary>
    /// Determines whether the UTF-8 text at the specified index equals the specified text.
    /// </summary>
    /// <param name="index">The index of the text.</param>
    /// <param name="otherUtf8Text">The UTF-8 text to compare.</param>
    /// <param name="isPropertyName">Whether the text is a property name.</param>
    /// <param name="shouldUnescape">Whether the text should be unescaped.</param>
    /// <returns><c>true</c> if the text equals the specified text; otherwise, <c>false</c>.</returns>
    bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape);

    /// <summary>
    /// Writes the element at the specified index to the provided JSON writer.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="writer">The JSON writer.</param>
    void WriteElementTo(int index, Utf8JsonWriter writer);

    /// <summary>
    /// Writes the property name at the specified index to the provided JSON writer.
    /// </summary>
    /// <param name="index">The index of the property name.</param>
    /// <param name="writer">The JSON writer.</param>
    void WritePropertyName(int index, Utf8JsonWriter writer);

    /// <summary>
    /// Clones the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The cloned JSON element.</returns>
    JsonElement CloneElement(int index);

    /// <summary>
    /// Clones the element at the specified index.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="index">The index of the element.</param>
    /// <returns>The cloned JSON element.</returns>
    TElement CloneElement<TElement>(int index)
        where TElement : struct, IJsonElement<TElement>;

    /// <summary>
    /// Gets the size of the database for the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="includeEndElement">Whether to include the end element in the size.</param>
    /// <returns>The size of the database.</returns>
    int GetDbSize(int index, bool includeEndElement);

    /// <summary>
    /// Gets the start index of the element from the end index.
    /// </summary>
    /// <param name="endIndex">The end index of the element.</param>
    /// <returns>The start index of the element.</returns>
    int GetStartIndex(int endIndex);

    /// <summary>
    /// Builds a rented metadata database for the specified parent document index.
    /// </summary>
    /// <param name="parentDocumentIndex">The index of the parent document.</param>
    /// <param name="workspace">The JSON workspace.</param>
    /// <param name="rentedBacking">The rented backing array.</param>
    /// <returns>The size of the metadata database.</returns>
    int BuildRentedMetadataDb(int parentDocumentIndex, JsonWorkspace workspace, out byte[] rentedBacking);

    /// <summary>
    /// Appends the element at the specified index to the metadata database.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="workspace">The JSON workspace.</param>
    /// <param name="db">The metadata database.</param>
    void AppendElementToMetadataDb(int index, JsonWorkspace workspace, ref MetadataDb db);

    /// <summary>
    /// Try to resolve the given JSON pointer.
    /// </summary>
    /// <typeparam name="TValue">The type of the target value.</typeparam>
    /// <param name="jsonPointer">The JSON pointer to resolve.</param>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">Providers the resolved value, if the pointer could be resolved.</param>
    /// <returns><see langword="true"/> if the pointer could be resolved, otherwise <see langword="false"/>.</returns>
    bool TryResolveJsonPointer<TValue>(ReadOnlySpan<byte> jsonPointer, int index, out TValue value)
        where TValue : struct, IJsonElement<TValue>;

    /// <summary>
    /// Formats the value to the provided destination span according to the specified format and format provider.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="destination">The destination span to write the formatted value to.</param>
    /// <param name="charsWritten">The number of characters written to the destination span.</param>
    /// <param name="format">The format string.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    bool TryFormat(int index, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider);

    /// <summary>
    /// Formats the value to the provided destination UTF-8 span according to the specified format and format provider.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="destination">The destination span to write the UTF-8 formatted value to.</param>
    /// <param name="bytesWritten">The number of bytes written to the destination span.</param>
    /// <param name="format">The format string.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    bool TryFormat(int index, Span<byte> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? formatProvider);

    /// <summary>
    /// Gets the display string representation of the element at the specified index according to the specified format and format provider.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="format">The format string.</param>
    /// <param name="formatProvider">The format provider.</param>
    /// <returns>The display string representation of the element.</returns>
    string ToString(int index, string? format, IFormatProvider? formatProvider);

    /// <summary>
    /// Tries to get the line number and character offset in the original source document
    /// for the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="line">When this method returns, contains the 1-based line number if successful.</param>
    /// <param name="charOffset">When this method returns, contains the 1-based character offset within the line if successful.</param>
    /// <param name="lineByteOffset">When this method returns, contains the byte offset of the start of the line if successful.</param>
    /// <returns><see langword="true"/> if the line and offset were successfully determined; otherwise, <see langword="false"/>.</returns>
    bool TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset);

    /// <summary>
    /// Resolves a JSON pointer against the element at the specified index and gets the line number
    /// and character offset of the target element in the original source document.
    /// </summary>
    /// <param name="jsonPointer">The JSON pointer to resolve.</param>
    /// <param name="index">The index of the element at the root of the pointer resolution.</param>
    /// <param name="line">When this method returns, contains the 1-based line number if successful.</param>
    /// <param name="charOffset">When this method returns, contains the 1-based character offset within the line if successful.</param>
    /// <param name="lineByteOffset">When this method returns, contains the byte offset of the start of the line if successful.</param>
    /// <returns><see langword="true"/> if the pointer was resolved and the line and offset were successfully determined; otherwise, <see langword="false"/>.</returns>
    bool TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset);

    /// <summary>
    /// Tries to get the specified line from the original source document as UTF-8 bytes.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number to retrieve.</param>
    /// <param name="line">When this method returns, contains the UTF-8 bytes of the line if successful.</param>
    /// <returns><see langword="true"/> if the line was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    bool TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line);

    /// <summary>
    /// Tries to get the specified line from the original source document as a string.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number to retrieve.</param>
    /// <param name="line">When this method returns, contains the line text if successful.</param>
    /// <returns><see langword="true"/> if the line was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    bool TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line);
}