// <copyright file="IMutableJsonDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Numerics;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a mutable JSON document that supports editing and value storage operations.
/// </summary>
[CLSCompliant(false)]
public interface IMutableJsonDocument : IJsonDocument
{
    /// <summary>
    /// Gets the version of the document.
    /// </summary>
    ulong Version { get; }

    /// <summary>
    /// Gets the array element at the specified index as a mutable JSON element.
    /// </summary>
    /// <param name="currentIndex">The current index in the document.</param>
    /// <param name="arrayIndex">The index within the array.</param>
    /// <returns>The mutable JSON element at the specified array index.</returns>
    new JsonElement.Mutable GetArrayIndexElement(int currentIndex, int arrayIndex);

    /// <summary>
    /// Gets the element at the specified array index within the current index.
    /// </summary>
    /// <param name="currentIndex">The current index.</param>
    /// <param name="arrayIndex">The array index.</param>
    /// <param name="parentDocument">Produces the parent document of the result.</param>
    /// <param name="parentDocumentIndex">Produces the parent document index.</param>
    void GetArrayIndexElement(int currentIndex, int arrayIndex, out IMutableJsonDocument parentDocument, out int parentDocumentIndex);

    /// <summary>
    /// Gets the named property value from a specific <see cref="MetadataDb"/>.
    /// </summary>
    /// <param name="parsedData">The parsed data. This is used in place of the document's own MetadataDb.</param>
    /// <param name="startIndex">The index of the first property name.</param>
    /// <param name="endIndex">The index of the last property value.</param>
    /// <param name="propertyName">The unescaped property name to look up.</param>
    /// <param name="valueIndex">The index of the value corresponding to the given property name.</param>
    /// <returns><see langword="true"/> if the property with the given name is found.</returns>
    bool TryGetNamedPropertyValueIndex(ref MetadataDb parsedData, int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out int valueIndex);

    /// <summary>
    /// Gets the named property value from a specific <see cref="MetadataDb"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The unescaped property name to look up.</param>
    /// <param name="valueIndex">The index of the value corresponding to the given property name.</param>
    /// <returns><see langword="true"/> if the property with the given name is found.</returns>
    bool TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<char> propertyName, out int valueIndex);

    /// <summary>
    /// Gets the named property value from a specific <see cref="MetadataDb"/>.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property as a UTF-8 byte span.</param>
    /// <param name="valueIndex">The index of the value corresponding to the given property name.</param>
    /// <returns><see langword="true"/> if the property with the given name is found.</returns>
    bool TryGetNamedPropertyValueIndex(int index, ReadOnlySpan<byte> propertyName, out int valueIndex);

    /// <summary>
    /// Tries to get the value of a named property as a mutable JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The mutable JSON element value.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement.Mutable value);

    /// <summary>
    /// Tries to get the value of a named property as a mutable JSON element.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <param name="propertyName">The name of the property as a UTF-8 byte span.</param>
    /// <param name="value">The mutable JSON element value.</param>
    /// <returns><c>true</c> if the property value was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement.Mutable value);

    /// <summary>
    /// Gets the index of the parent workspace.
    /// </summary>
    int ParentWorkspaceIndex { get; }

    /// <summary>
    /// Gets the JSON workspace associated with this document.
    /// </summary>
    JsonWorkspace Workspace { get; }

    /// <summary>
    /// Stores a raw number value in the document.
    /// </summary>
    /// <param name="value">The raw number value as a UTF-8 byte span.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreRawNumberValue(ReadOnlySpan<byte> value);

    /// <summary>
    /// Stores a null value in the document.
    /// </summary>
    /// <returns>The index of the stored value.</returns>
    int StoreNullValue();

    /// <summary>
    /// Stores a boolean value in the document.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreBooleanValue(bool value);

    /// <summary>
    /// Escapes and stores a raw string value in the document.
    /// </summary>
    /// <param name="value">The string value to escape and store.</param>
    /// <param name="requiredEscaping">Set to <c>true</c> if escaping was required.</param>
    /// <returns>The index of the stored value.</returns>
    int EscapeAndStoreRawStringValue(ReadOnlySpan<char> value, out bool requiredEscaping);

    /// <summary>
    /// Escapes and stores a raw string value in the document.
    /// </summary>
    /// <param name="value">The UTF-8 string value to escape and store.</param>
    /// <param name="requiredEscaping">Set to <c>true</c> if escaping was required.</param>
    /// <returns>The index of the stored value.</returns>
    int EscapeAndStoreRawStringValue(ReadOnlySpan<byte> value, out bool requiredEscaping);

    /// <summary>
    /// Stores a raw string value in the document.
    /// </summary>
    /// <param name="value">The UTF-8 string value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreRawStringValue(ReadOnlySpan<byte> value);

    /// <summary>
    /// Stores a <see cref="Guid"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="Guid"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(Guid value);

    /// <summary>
    /// Stores a <see cref="DateTime"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in DateTime value);

    /// <summary>
    /// Stores a <see cref="DateTimeOffset"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in DateTimeOffset value);

    /// <summary>
    /// Stores an <see cref="OffsetDateTime"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="OffsetDateTime"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in OffsetDateTime value);

    /// <summary>
    /// Stores an <see cref="OffsetDate"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="OffsetDate"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in OffsetDate value);

    /// <summary>
    /// Stores an <see cref="OffsetTime"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="OffsetTime"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in OffsetTime value);

    /// <summary>
    /// Stores a <see cref="LocalDate"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="LocalDate"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in LocalDate value);

    /// <summary>
    /// Stores a <see cref="Period"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="Period"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in Period value);

    /// <summary>
    /// Stores an <see cref="sbyte"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="sbyte"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(sbyte value);

    /// <summary>
    /// Stores a <see cref="byte"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="byte"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(byte value);

    /// <summary>
    /// Stores an <see cref="int"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="int"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(int value);

    /// <summary>
    /// Stores a <see cref="uint"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="uint"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(uint value);

    /// <summary>
    /// Stores a <see cref="long"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="long"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(long value);

    /// <summary>
    /// Stores a <see cref="ulong"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="ulong"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(ulong value);

    /// <summary>
    /// Stores a <see cref="short"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="short"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(short value);

    /// <summary>
    /// Stores a <see cref="ushort"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="ushort"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(ushort value);

    /// <summary>
    /// Stores a <see cref="float"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="float"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(float value);

    /// <summary>
    /// Stores a <see cref="double"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="double"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(double value);

    /// <summary>
    /// Stores a <see cref="decimal"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="decimal"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(decimal value);

    /// <summary>
    /// Stores a <see cref="BigInteger"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="BigInteger"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in BigInteger value);

    /// <summary>
    /// Stores a <see cref="BigNumber"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="BigNumber"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(in BigNumber value);

#if NET

    /// <summary>
    /// Stores an <see cref="Int128"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="Int128"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(Int128 value);

    /// <summary>
    /// Stores a <see cref="UInt128"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="UInt128"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(UInt128 value);

    /// <summary>
    /// Stores a <see cref="Half"/> value in the document.
    /// </summary>
    /// <param name="value">The <see cref="Half"/> value to store.</param>
    /// <returns>The index of the stored value.</returns>
    int StoreValue(Half value);

#endif

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
    void RemoveRange(int complexObjectStartIndex, int startIndex, int endIndex, int membersToRemove);

    /// <summary>
    /// Sets the value of the document and disposes the provided <see cref="ComplexValueBuilder"/>.
    /// </summary>
    /// <param name="cvb">The <see cref="ComplexValueBuilder"/> to set and dispose.</param>
    void SetAndDispose(ref ComplexValueBuilder cvb);

    /// <summary>
    /// Inserts a value into the document and disposes the provided <see cref="ComplexValueBuilder"/>.
    /// </summary>
    /// <param name="complexObjectStartIndex">The start index of the complex object.</param>
    /// <param name="index">The index at which to insert.</param>
    /// <param name="cvb">The <see cref="ComplexValueBuilder"/> to insert and dispose.</param>
    void InsertAndDispose(int complexObjectStartIndex, int index, ref ComplexValueBuilder cvb);

    /// <summary>
    /// Overwrites values in the document and disposes the provided <see cref="ComplexValueBuilder"/>.
    /// </summary>
    /// <param name="complexObjectStartIndex">The start index of the complex object.</param>
    /// <param name="startIndex">The start index of the range to overwrite.</param>
    /// <param name="endIndex">The end index of the range to overwrite.</param>
    /// <param name="membersToOverwrite">The number of members to overwrite.</param>
    /// <param name="cvb">The <see cref="ComplexValueBuilder"/> to overwrite and dispose.</param>
    void OverwriteAndDispose(int complexObjectStartIndex, int startIndex, int endIndex, int membersToOverwrite, ref ComplexValueBuilder cvb);
}