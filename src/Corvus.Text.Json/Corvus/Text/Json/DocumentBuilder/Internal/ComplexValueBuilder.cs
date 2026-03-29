// <copyright file="ComplexValueBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides a high-performance, low-allocation builder for constructing complex JSON values
/// (objects and arrays) within an <see cref="IMutableJsonDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ComplexValueBuilder"/> is a ref struct designed for use in stack-based scenarios,
/// enabling efficient construction of JSON objects and arrays by directly manipulating the
/// underlying metadata database.
/// </para>
/// <para>
/// This builder supports adding properties and items of various types, including primitives,
/// strings, numbers, booleans, nulls, and complex/nested values. It also provides methods
/// for starting and ending JSON objects and arrays, as well as for integrating with
/// <see cref="IMutableJsonDocument"/> for document mutation.
/// </para>
/// <para>
/// Typical usage involves creating a builder via <see cref="Create(IMutableJsonDocument, int)"/>,
/// using <see cref="AddProperty"/> and <see cref="AddItem"/> methods to populate the structure,
/// and then finalizing with <see cref="EndObject"/> or <see cref="EndArray"/>.
/// </para>
/// <para>
/// This type is not thread-safe and must not be stored on the heap.
/// </para>
/// </remarks>
public ref struct ComplexValueBuilder
{
    /// <summary>
    /// Delegate for building a value using a <see cref="ComplexValueBuilder"/>.
    /// </summary>
    /// <param name="builder">The builder to use for value construction.</param>
    public delegate void ValueBuilderAction(ref ComplexValueBuilder builder);

    /// <summary>
    /// Delegate for building a value using a <see cref="ComplexValueBuilder"/> and a context object.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object.</typeparam>
    /// <param name="context">The context object.</param>
    /// <param name="builder">The builder to use for value construction.</param>
#if NET9_0_OR_GREATER
    public delegate void ValueBuilderAction<TContext>(in TContext context, ref ComplexValueBuilder builder)
        where TContext : allows ref struct;
#else
    public delegate void ValueBuilderAction<TContext>(in TContext context, ref ComplexValueBuilder builder);
#endif

    private IMutableJsonDocument _parentDocument;

    private MetadataDb _parsedData;

    private int _memberCount;

    private int _rowCount;

    private ComplexValueBuilder(IMutableJsonDocument parentDocument, MetadataDb parsedData)
    {
        _parentDocument = parentDocument;
        _parsedData = parsedData;
        _memberCount = 0;
        _rowCount = 0;
    }

    internal readonly int Length => _parsedData.Length;

    /// <summary>
    /// Gets the number of members (properties or items) added to the current object or array.
    /// </summary>
    public readonly int MemberCount => _memberCount;

    /// <summary>
    /// Creates a new <see cref="ComplexValueBuilder"/> for the specified parent document,
    /// pre-allocating space for the given number of elements.
    /// </summary>
    /// <param name="parentDocument">The parent <see cref="IMutableJsonDocument"/> to build into.</param>
    /// <param name="initialElementCount">The estimated number of elements to allocate space for.</param>
    /// <returns>A new <see cref="ComplexValueBuilder"/> instance.</returns>
    [CLSCompliant(false)]
    public static ComplexValueBuilder Create(IMutableJsonDocument parentDocument, int initialElementCount)
    {
        return new(parentDocument, MetadataDb.CreateRented(initialElementCount * DbRow.Size, convertToAlloc: false));
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ValueBuilderAction createComplexValue)
    {
        AddProperty(propertyName, createComplexValue, true, false);
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate and context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object.</typeparam>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="context">The context object to pass to the delegate.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty<TContext>(ReadOnlySpan<byte> propertyName, in TContext context, ValueBuilderAction<TContext> createComplexValue)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        AddProperty(propertyName, context, createComplexValue, true, false);
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, ValueBuilderAction createComplexValue, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        createComplexValue(ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate and context, with control over escaping.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object.</typeparam>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="context">The context object to pass to the delegate.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty<TContext>(ReadOnlySpan<byte> propertyName, in TContext context, ValueBuilderAction<TContext> createComplexValue, bool escapeName, bool nameRequiresUnescaping)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        createComplexValue(context, ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, ValueBuilderAction createComplexValue)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        createComplexValue(ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with a complex value to the current object using a builder delegate and context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object.</typeparam>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="context">The context object to pass to the delegate.</param>
    /// <param name="createComplexValue">A delegate that builds the property value.</param>
    public void AddProperty<TContext>(ReadOnlySpan<char> propertyName, in TContext context, ValueBuilderAction<TContext> createComplexValue)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        createComplexValue(context, ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with a string value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="utf8String">The property value as a UTF-8 byte span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> utf8String)
    {
        AddProperty(propertyName, utf8String, escapeName: true, escapeValue: true, false, false);
    }

    /// <summary>
    /// Adds a property with a string value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="utf8String">The property value as a UTF-8 byte span.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="escapeValue">Whether to escape the property value.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    /// <param name="valueRequiresUnescaping">Whether the property value requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> utf8String, bool escapeName, bool escapeValue, bool nameRequiresUnescaping, bool valueRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        AddStringValue(JsonTokenType.String, utf8String, escapeValue, valueRequiresUnescaping);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a string value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The property value as a character span.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        AddStringValue(JsonTokenType.String, value);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a string value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The property value as a character span.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        AddStringValue(JsonTokenType.String, value);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a string value to the current object, with control over escaping the value.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The property value as a UTF-8 byte span.</param>
    /// <param name="escapeValue">Whether to escape the property value.</param>
    /// <param name="valueRequiresUnescaping">Whether the property value requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, bool escapeValue, bool valueRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        AddStringValue(JsonTokenType.String, value, escapeValue, valueRequiresUnescaping);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a formatted number value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The number value as a UTF-8 byte span.</param>
    public void AddPropertyFormattedNumber(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
    {
        AddPropertyFormattedNumber(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a formatted number value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The number value as a UTF-8 byte span.</param>
    public void AddPropertyFormattedNumber(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreRawNumberValue(value), requiresUnescapingOrHasExponent: value.IndexOfAny((byte)'e', (byte)'E') >= 0);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a formatted number value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a string.</param>
    /// <param name="value">The number value as a UTF-8 byte span.</param>
    public void AddPropertyFormattedNumber(string propertyName, ReadOnlySpan<byte> value)
    {
        AddPropertyFormattedNumber(propertyName.AsSpan(), value);
    }

    /// <summary>
    /// Adds a property with a formatted number value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-16 span.</param>
    /// <param name="value">The number value as a UTF-8 byte span.</param>
    public void AddPropertyFormattedNumber(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreRawNumberValue(value), requiresUnescapingOrHasExponent: value.IndexOfAny((byte)'e', (byte)'E') >= 0);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a raw string value to the current object, with control over escaping and unescaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The value as a UTF-8 byte span.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    /// <param name="valueRequiresUnescaping">Whether the value requires unescaping.</param>
    public void AddPropertyRawString(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value, bool escapeName, bool nameRequiresUnescaping, bool valueRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        AddStringValue(JsonTokenType.String, value, escape: false, ifNotEscapeRequiresUenscaping: valueRequiresUnescaping);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a raw string value.
    /// </summary>
    /// <param name="propertyName">The property name as a string.</param>
    /// <param name="value">The value as a UTF-8 byte span.</param>
    public void AddPropertyRawString(string propertyName, ReadOnlySpan<byte> value, bool valueRequiresUnescaping)
    {
        AddPropertyRawString(propertyName.AsSpan(), value, valueRequiresUnescaping);
    }

    /// <summary>
    /// Adds a property with a raw string value.
    /// </summary>
    /// <param name="propertyName">The property name as a string.</param>
    /// <param name="value">The value as a UTF-8 byte span.</param>
    public void AddPropertyRawString(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, bool valueRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        AddStringValue(JsonTokenType.String, value, escape: false, ifNotEscapeRequiresUenscaping: valueRequiresUnescaping);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a null value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddPropertyNull(ReadOnlySpan<byte> propertyName)
    {
        AddPropertyNull(propertyName, true, false);
    }

    /// <summary>
    /// Adds a property with a null value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyNull(ReadOnlySpan<byte> propertyName, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Null, _parentDocument.StoreNullValue(), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a null value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    public void AddPropertyNull(ReadOnlySpan<char> propertyName)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Null, _parentDocument.StoreNullValue(), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a boolean value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The boolean value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, bool value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a boolean value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, bool value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(value ? JsonTokenType.True : JsonTokenType.False, _parentDocument.StoreBooleanValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a boolean value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The boolean value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, bool value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(value ? JsonTokenType.True : JsonTokenType.False, _parentDocument.StoreBooleanValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a JSON element value to the current object.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element value.</typeparam>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The JSON element value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty<T>(ReadOnlySpan<byte> propertyName, in T value)
        where T : struct, IJsonElement<T>
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a JSON element value to the current object, with control over escaping.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element value.</typeparam>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The JSON element value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty<T>(ReadOnlySpan<byte> propertyName, T value, bool escapeName, bool nameRequiresUnescaping)
        where T : struct, IJsonElement<T>
    {
        Debug.Assert(value.TokenType != JsonTokenType.None);

        int currentLength = Length;
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        value.ParentDocument.AppendElementToMetadataDb(value.ParentDocumentIndex, _parentDocument.Workspace, ref _parsedData);
        _memberCount++;
        _rowCount += (Length - currentLength) / DbRow.Size;
    }

    /// <summary>
    /// Adds a property with a JSON element value to the current object.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element value.</typeparam>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The JSON element value.</param>
    [CLSCompliant(false)]
    public void AddProperty<T>(ReadOnlySpan<char> propertyName, T value)
        where T : struct, IJsonElement<T>
    {
        Debug.Assert(value.TokenType != JsonTokenType.None);

        int currentLength = Length;
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        value.ParentDocument.AppendElementToMetadataDb(value.ParentDocumentIndex, _parentDocument.Workspace, ref _parsedData);
        _memberCount++;
        _rowCount += (Length - currentLength) / DbRow.Size;
    }

    /// <summary>
    /// Adds a property with a <see cref="Guid"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Guid"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, Guid value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="Guid"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Guid"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, Guid value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="Guid"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="Guid"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, Guid value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="DateTime"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTime value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTime"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="DateTime"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTime value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="DateTime"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in DateTime value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTimeOffset"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTimeOffset value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTimeOffset"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTimeOffset value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="DateTimeOffset"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in DateTimeOffset value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDateTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDateTime"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDateTime value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDateTime"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDateTime"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDateTime value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDateTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDateTime"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetDateTime value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetTime"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetTime value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetTime"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetTime"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetTime value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetTime"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetTime"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetTime value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDate"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDate"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDate value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDate"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDate"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDate value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.OffsetDate"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="NodaTime.OffsetDate"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetDate value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.LocalDate"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.LocalDate"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in LocalDate value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.LocalDate"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.LocalDate"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in LocalDate value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.LocalDate"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="NodaTime.LocalDate"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in LocalDate value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.Period"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.Period"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in Period value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.Period"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="NodaTime.Period"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in Period value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="NodaTime.Period"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="NodaTime.Period"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in Period value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with an <see cref="sbyte"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="sbyte"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, sbyte value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with an <see cref="sbyte"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="sbyte"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, sbyte value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with an <see cref="sbyte"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="sbyte"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, sbyte value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="byte"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="byte"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, byte value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="byte"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="byte"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, byte value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="byte"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="byte"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, byte value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with an <see cref="int"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="int"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, int value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with an <see cref="int"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="int"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, int value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with an <see cref="int"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="int"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, int value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="uint"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="uint"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, uint value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="uint"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="uint"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, uint value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="uint"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="uint"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, uint value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="long"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="long"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, long value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="long"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="long"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, long value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="long"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="long"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, long value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="ulong"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="ulong"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ulong value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="ulong"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="ulong"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ulong value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="ulong"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="ulong"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, ulong value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="short"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="short"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, short value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="short"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="short"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, short value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="short"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="short"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, short value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="ushort"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="ushort"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ushort value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="ushort"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="ushort"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, ushort value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="ushort"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="ushort"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, ushort value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="float"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="float"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, float value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="float"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="float"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, float value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="float"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="float"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, float value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="double"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="double"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, double value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="double"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="double"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, double value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="double"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="double"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, double value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="decimal"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="decimal"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, decimal value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="decimal"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="decimal"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, decimal value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="decimal"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="decimal"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, decimal value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="BigInteger"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="BigInteger"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in BigInteger value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="BigInteger"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="BigInteger"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, in BigInteger value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="BigInteger"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="BigInteger"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, in BigInteger value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="BigNumber"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in BigNumber value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="BigNumber"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, in BigNumber value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: value.Exponent != 0);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="BigNumber"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    /// <summary>
    /// Adds a property with a <see cref="BigNumber"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, in BigNumber value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: value.Exponent != 0);
        _memberCount++;
        _rowCount += 2;
    }

#if NET

    /// <summary>
    /// Adds a property with an <see cref="Int128"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Int128"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, Int128 value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with an <see cref="Int128"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Int128"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, Int128 value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with an <see cref="Int128"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="Int128"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, Int128 value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="UInt128"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="UInt128"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, UInt128 value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="UInt128"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="UInt128"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, UInt128 value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="UInt128"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="UInt128"/> value.</param>
    [CLSCompliant(false)]
    public void AddProperty(ReadOnlySpan<char> propertyName, UInt128 value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="Half"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Half"/> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddProperty(ReadOnlySpan<byte> propertyName, Half value)
    {
        AddProperty(propertyName, value, true, false);
    }

    /// <summary>
    /// Adds a property with a <see cref="Half"/> value to the current object, with control over escaping.
    /// </summary>
    /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
    /// <param name="value">The <see cref="Half"/> value.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddProperty(ReadOnlySpan<byte> propertyName, Half value, bool escapeName, bool nameRequiresUnescaping)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName, escapeName, nameRequiresUnescaping);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

    /// <summary>
    /// Adds a property with a <see cref="Half"/> value to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <param name="value">The <see cref="Half"/> value.</param>
    public void AddProperty(ReadOnlySpan<char> propertyName, Half value)
    {
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount += 2;
    }

#endif

    /// <summary>
    /// Adds a property with an array of <see cref="long"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="long"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<long> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="int"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="int"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<int> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="short"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="short"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<short> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="sbyte"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="sbyte"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<sbyte> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ulong"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="ulong"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<ulong> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="uint"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="uint"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<uint> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ushort"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="ushort"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<ushort> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="byte"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="byte"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<byte> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="decimal"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="decimal"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<decimal> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="double"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="double"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<double> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="float"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="float"/> values.</param>
    public void AddPropertyArrayValue(string name, ReadOnlySpan<float> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

#if NET

    /// <summary>
    /// Adds a property with an array of <see cref="Int128"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="Int128"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<Int128> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="UInt128"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="UInt128"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<UInt128> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

    /// <summary>
    /// Adds a property with an array of <see cref="Half"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="array">The array of <see cref="Half"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(string name, ReadOnlySpan<Half> array)
    {
        AddPropertyArrayValue(name.AsSpan(), array);
    }

#endif

    /// <summary>
    /// Adds a property with an array of <see cref="long"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="long"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<long> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (long item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="int"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="int"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<int> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (int item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="short"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="short"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<short> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (short item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="sbyte"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="sbyte"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<sbyte> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (sbyte item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ulong"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="ulong"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<ulong> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (ulong item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="uint"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="uint"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<uint> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (uint item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ushort"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="ushort"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<ushort> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (ushort item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="byte"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="byte"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<byte> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (byte item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="decimal"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="decimal"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<decimal> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (decimal item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="double"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="double"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<double> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (double item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="float"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="float"/> values.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<float> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (float item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

#if NET

    /// <summary>
    /// Adds a property with an array of <see cref="Int128"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="Int128"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<Int128> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (Int128 item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="UInt128"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="UInt128"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<UInt128> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (UInt128 item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="Half"/> values to the current object.
    /// </summary>
    /// <param name="name">The property name as a character span.</param>
    /// <param name="array">The array of <see cref="Half"/> values.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<char> name, ReadOnlySpan<Half> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, name);
        StartArray();

        foreach (Half item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

#endif

    /// <summary>
    /// Adds a property with an array of <see cref="long"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="long"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<long> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (long item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="int"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="int"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<int> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (int item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="short"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="short"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<short> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (short item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="sbyte"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="sbyte"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<sbyte> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (sbyte item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ulong"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="ulong"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<ulong> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (ulong item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="uint"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="uint"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<uint> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (uint item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="ushort"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="ushort"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<ushort> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (ushort item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="byte"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="byte"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (byte item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="decimal"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="decimal"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<decimal> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (decimal item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="double"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="double"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<double> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (double item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="float"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="float"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<float> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (float item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

#if NET

    /// <summary>
    /// Adds a property with an array of <see cref="Int128"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="Int128"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<Int128> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (Int128 item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="UInt128"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="UInt128"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<UInt128> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (UInt128 item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

    /// <summary>
    /// Adds a property with an array of <see cref="Half"/> values to the current object, with control over escaping.
    /// </summary>
    /// <param name="utf8Name">The property name as a UTF-8 byte span.</param>
    /// <param name="array">The array of <see cref="Half"/> values.</param>
    /// <param name="escapeName">Whether to escape the property name.</param>
    /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
    [CLSCompliant(false)]
    public void AddPropertyArrayValue(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<Half> array, bool escapeName, bool nameRequiresUnescaping)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, utf8Name, escapeName, nameRequiresUnescaping);
        StartArray();

        foreach (Half item in array)
        {
            AddItem(item);
        }

        EndArray();
        _memberCount = currentMemberCount + 1;
        _rowCount = currentRowCount + _rowCount + 1;
    }

#endif

    /// <summary>
    /// Adds an item to the current array as a UTF-8 string.
    /// </summary>
    /// <param name="utf8String">The item value as a UTF-8 byte span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddItem(ReadOnlySpan<byte> utf8String)
    {
        AddItem(utf8String, true, false);
    }

    /// <summary>
    /// Adds an item to the current array as a string.
    /// </summary>
    /// <param name="value">The item value as a string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddItem(string value)
    {
        AddItem(value.AsSpan());
    }

    /// <summary>
    /// Adds an item to the current array as a UTF-8 string with control over escaping.
    /// </summary>
    /// <param name="utf8String">The item value as a UTF-8 byte span.</param>
    /// <param name="escapeValue">Whether to escape the value.</param>
    /// <param name="requiresUnescaping">Whether the value requires unescaping.</param>
    public void AddItem(ReadOnlySpan<byte> utf8String, bool escapeValue, bool requiresUnescaping)
    {
        AddStringValue(JsonTokenType.String, utf8String, escapeValue, requiresUnescaping);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an item to the current array as a character span.
    /// </summary>
    /// <param name="value">The item value as a character span.</param>
    public void AddItem(ReadOnlySpan<char> value)
    {
        AddStringValue(JsonTokenType.String, value);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an item to the current array as a raw string.
    /// </summary>
    /// <param name="value">The item value as a UTF-8 byte span.</param>
    /// <param name="requiresUnescaping">Whether the value requires unescaping.</param>
    public void AddItemRawString(ReadOnlySpan<byte> value, bool requiresUnescaping)
    {
        AddStringValue(JsonTokenType.String, value, false, requiresUnescaping);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an item to the current array using a value builder action.
    /// </summary>
    /// <param name="createValue">The action to create the item value.</param>
    public void AddItem(ValueBuilderAction createValue)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        createValue(ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an item to the current array using a value builder action with context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="context">The context to pass to the create value action.</param>
    /// <param name="createValue">The action to create the item value.</param>
    public void AddItem<TContext>(in TContext context, ValueBuilderAction<TContext> createValue)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;
        createValue(context, ref this);
        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds a null item to the current array.
    /// </summary>
    public void AddItemNull()
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Null, _parentDocument.StoreNullValue(), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a boolean item to the current array.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public void AddItem(bool value)
    {
        _parsedData.AppendDynamicSimpleValue(value ? JsonTokenType.True : JsonTokenType.False, _parentDocument.StoreBooleanValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a formatted number item to the current array.
    /// </summary>
    /// <param name="value">The number value as a UTF-8 byte span.</param>
    public void AddItemFormattedNumber(ReadOnlySpan<byte> value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreRawNumberValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a JSON element item to the current array.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element.</typeparam>
    /// <param name="value">The JSON element value.</param>
    [CLSCompliant(false)]
    public void AddItem<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        Debug.Assert(value.TokenType != JsonTokenType.None);

        int currentLength = Length;
        value.ParentDocument.AppendElementToMetadataDb(value.ParentDocumentIndex, _parentDocument.Workspace, ref _parsedData);
        _memberCount++;
        _rowCount += (Length - currentLength) / DbRow.Size;
    }

    /// <summary>
    /// Adds a <see cref="Guid"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="Guid"/> value.</param>
    public void AddItem(Guid value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="DateTime"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> value.</param>
    public void AddItem(in DateTime value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="DateTimeOffset"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
    public void AddItem(in DateTimeOffset value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an <see cref="OffsetDateTime"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="OffsetDateTime"/> value.</param>
    public void AddItem(in OffsetDateTime value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an <see cref="OffsetDate"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="OffsetDate"/> value.</param>
    public void AddItem(in OffsetDate value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an <see cref="OffsetTime"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="OffsetTime"/> value.</param>
    public void AddItem(in OffsetTime value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="LocalDate"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="LocalDate"/> value.</param>
    public void AddItem(in LocalDate value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="Period"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="Period"/> value.</param>
    public void AddItem(in Period value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.String, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an <see cref="sbyte"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="sbyte"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(sbyte value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="byte"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="byte"/> value.</param>
    public void AddItem(byte value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds an <see cref="int"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="int"/> value.</param>
    public void AddItem(int value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="uint"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="uint"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(uint value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="long"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="long"/> value.</param>
    public void AddItem(long value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="ulong"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="ulong"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(ulong value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="short"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="short"/> value.</param>
    public void AddItem(short value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="ushort"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="ushort"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(ushort value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="float"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="float"/> value.</param>
    public void AddItem(float value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="double"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="double"/> value.</param>
    public void AddItem(double value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="decimal"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="decimal"/> value.</param>
    public void AddItem(decimal value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="BigNumber"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="BigNumber"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(in BigNumber value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="BigInteger"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="BigInteger"/> value.</param>
    public void AddItem(in BigInteger value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

#if NET

    /// <summary>
    /// Adds an <see cref="Int128"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="Int128"/> value.</param>
    public void AddItem(Int128 value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="UInt128"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="UInt128"/> value.</param>
    [CLSCompliant(false)]
    public void AddItem(UInt128 value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

    /// <summary>
    /// Adds a <see cref="Half"/> item to the current array.
    /// </summary>
    /// <param name="value">The <see cref="Half"/> value.</param>
    public void AddItem(Half value)
    {
        _parsedData.AppendDynamicSimpleValue(JsonTokenType.Number, _parentDocument.StoreValue(value), requiresUnescapingOrHasExponent: false);
        _memberCount++;
        _rowCount++;
    }

#endif

    /// <summary>
    /// Adds an array of <see cref="long"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="long"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<long> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (long item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="int"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="int"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<int> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (int item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="short"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="short"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<short> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (short item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="sbyte"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="sbyte"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<sbyte> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (sbyte item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="ulong"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="ulong"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<ulong> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (ulong item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="uint"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="uint"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<uint> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (uint item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="ushort"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="ushort"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<ushort> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (ushort item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="byte"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="byte"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<byte> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (byte item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="decimal"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="decimal"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<decimal> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (decimal item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="double"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="double"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<double> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (double item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="float"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="float"/> values.</param>
    public void AddItemArrayValue(ReadOnlySpan<float> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (float item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

#if NET

    /// <summary>
    /// Adds an array of <see cref="Int128"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="Int128"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<Int128> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (Int128 item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="UInt128"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="UInt128"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<UInt128> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (UInt128 item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

    /// <summary>
    /// Adds an array of <see cref="Half"/> values as an item to the current array.
    /// </summary>
    /// <param name="array">The array of <see cref="Half"/> values.</param>
    [CLSCompliant(false)]
    public void AddItemArrayValue(ReadOnlySpan<Half> array)
    {
        int currentMemberCount = _memberCount;
        int currentRowCount = _rowCount;
        _memberCount = 0;
        _rowCount = 0;

        StartArray();

        foreach (Half item in array)
        {
            AddItem(item);
        }

        EndArray();

        _memberCount = currentMemberCount + 1;
        _rowCount += currentRowCount;
    }

#endif

    /// <summary>
    /// Starts a new JSON object in the builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartObject()
    {
        _parsedData.Append(JsonTokenType.StartObject, 0, -1);
        _rowCount++;
    }

    /// <summary>
    /// Starts a new JSON array in the builder.
    /// </summary>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartArray()
    {
        _parsedData.Append(JsonTokenType.StartArray, 0, -1);
        _rowCount++;
    }

    /// <summary>
    /// Ends the current JSON object, finalizing its structure in the builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndObject()
    {
        int startRowIndex = Length - (_rowCount * DbRow.Size);
        _parsedData.Append(JsonTokenType.EndObject, 0, 1);
        _parsedData.SetLength(startRowIndex, _memberCount);
        _parsedData.SetNumberOfRows(startRowIndex, _rowCount);
        _parsedData.SetNumberOfRows(_parsedData.Length - DbRow.Size, _rowCount);

        // Now increment for the parent
        _rowCount++;
    }

    /// <summary>
    /// Removes a property from the current object.
    /// </summary>
    /// <param name="utf8Name">The name of the property as a string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveProperty(string name)
    {
        RemoveProperty(name.AsSpan());
    }

    /// <summary>
    /// Apply an object instance value to the document.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="value"/>.</typeparam>
    /// <param name="value">The value to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown if the <paramref name="value"/> is not a JSON object.</exception>"
    /// <remarks>
    /// The value must be a JSON object. Its properties will be set on the current document,
    /// replacing any existing values if present.
    /// </remarks>
    [CLSCompliant(false)]
    public void Apply<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        if (value.TokenType != JsonTokenType.StartObject)
        {
            ThrowHelper.ThrowInvalidOperationException_ExpectedObject(value.TokenType);
        }

        TryApply(value);
    }

    /// <summary>
    /// Tries to apply an object instance value to the document.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="value"/>.</typeparam>
    /// <param name="value">The value to apply.</param>
    /// <returns><see langword="true"/> if the value was applied.</returns>
    /// <remarks>
    /// <para>
    /// If the value is a JSON object, its properties (if any) will be set on the current document,
    /// replacing any existing values if present, and the method returns <see langword="true"/>.
    /// </para>
    /// <para>
    /// Otherwise, no changes are made, and the method returns <see langword="false"/>.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public bool TryApply<T>(in T value)
        where T : struct, IJsonElement<T>
    {
        if (value.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        ObjectEnumerator<JsonElement> enumerator = EnumeratorCreator.CreateObjectEnumerator<JsonElement>(value.ParentDocument, value.ParentDocumentIndex);
        while (enumerator.MoveNext())
        {
            // TODO: there is an opportunity here to see if the value's property count * parsedData property count > 10
            // and build a stack-based lookup like the PropertyMap to do these removals. It will be faster to do 1 pass of that
            // build and then constant time lookups for properties.
            // This would also require overloads of the remove property call chain that could take a ref to a stack-based property
            // map.
            JsonProperty<JsonElement> current = enumerator.Current;
            RemoveProperty(current.RawNameSpan, escapeName: false, nameRequiresUnescaping: current.NameIsEscaped);
            AddProperty(current.RawNameSpan, current.Value, escapeName: false, nameRequiresUnescaping: current.NameIsEscaped);
        }

        return true;
    }

    /// <summary>
    /// Removes a property from the current object.
    /// </summary>
    /// <param name="name">The name of the property as a character span.</param>
    public void RemoveProperty(scoped ReadOnlySpan<char> name)
    {
        // 1. Transcode the name to a UTF-8 byte span
        // 2. Return the result of RemoveProperty with the UTF-8 byte span, with escapeName set to true, and nameRequiresUnescaping false
        int length = Encoding.UTF8.GetMaxByteCount(name.Length);
        byte[]? buffer = null;
        Span<byte> utf8Name = length < JsonConstants.StackallocByteThreshold
            ? stackalloc byte[length]
            : (buffer = ArrayPool<byte>.Shared.Rent(length));
        int written = JsonReaderHelper.TranscodeHelper(name, utf8Name);
        try
        {
            RemoveProperty(utf8Name.Slice(0, written), true, false);
        }
        finally
        {
            if (buffer is byte[] b)
            {
                utf8Name.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(b);
            }
        }
    }

    /// <summary>
    /// Removes a property from the current object.
    /// </summary>
    /// <param name="utf8Name">The UTF-8 name of the property.</param>
    /// <param name="escapeName">Indicates whether the name requires escaping.</param>
    /// <param name="nameRequiresUnescaping">If the name does not require escaping, indicates whether the name requires unescaping.</param>
    public void RemoveProperty(scoped ReadOnlySpan<byte> utf8Name, bool escapeName, bool nameRequiresUnescaping)
    {
        // This holds true even for a partially complete property
        int startIndex = Length - ((_rowCount - 1) * DbRow.Size);
        if (startIndex < 0)
        {
            return;
        }

        // There can be no "end object" at the end of this entity because it has not
        // yet been written;
        // However, this is supposed to point at the end object. We will point
        // one off the end of the current metadata DB, which will serve the purpose.
        int endIndex = Length;
        int valueIndex;
        if (!escapeName && nameRequiresUnescaping)
        {
            byte[]? buffer = null;
            Span<byte> unescapedSpan = utf8Name.Length < JsonConstants.StackallocByteThreshold
                ? stackalloc byte[utf8Name.Length]
                : (buffer = ArrayPool<byte>.Shared.Rent(utf8Name.Length));
            bool result = JsonReaderHelper.TryUnescape(utf8Name, unescapedSpan, out int written);
            try
            {
                Debug.Assert(result);

                if (!_parentDocument.TryGetNamedPropertyValueIndex(ref _parsedData, startIndex, endIndex, unescapedSpan.Slice(0, written), out valueIndex))
                {
                    return;
                }
            }
            finally
            {
                if (buffer is byte[] b)
                {
                    // This might contain sensitive data
                    unescapedSpan.Slice(0, written).Clear();
                    ArrayPool<byte>.Shared.Return(b);
                }
            }
        }
        else if (!_parentDocument.TryGetNamedPropertyValueIndex(ref _parsedData, startIndex, endIndex, utf8Name, out valueIndex))
        {
            return;
        }

        // We have found the item, so we need to slice out the property name and value
        DbRow valueRow = _parsedData.Get(valueIndex);

        int rowCountToRemove = valueRow.IsSimpleValue ? 2 : valueRow.NumberOfRows + 2;

        // Directly remove the given range of rows, copying up additional rows
        _parsedData.RemoveRows(valueIndex - DbRow.Size, rowCountToRemove);

        // Then update our local member count and row count.
        _memberCount--;
        _rowCount -= rowCountToRemove;
    }

    /// <summary>
    /// Ends the current JSON array, finalizing its structure in the builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndArray()
    {
        int startRowIndex = Length - (_rowCount * DbRow.Size);
        _parsedData.Append(JsonTokenType.EndArray, 0, 1);
        _parsedData.SetLength(startRowIndex, _memberCount);

        // If the array item count is (e.g.) 12 and the number of rows is (e.g.) 13
        // then the extra row is just the EndArray item, so the array was made up
        // of simple values.
        // If the off-by-one relationship does not hold, then one of the values was
        // more than one row, making it a complex object.
        // This check is similar to tracking the start array and painting it when
        // StartObject or StartArray is encountered, but avoids the mixed state
        // where "UnknownSize" implies "has complex children".
        if (_memberCount + 1 != _rowCount)
        {
            _parsedData.SetHasComplexChildren(startRowIndex);
        }

        _parsedData.SetNumberOfRows(startRowIndex, _rowCount);
        _parsedData.SetNumberOfRows(_parsedData.Length - DbRow.Size, _rowCount);

        // Now increment for the parent
        _rowCount++;
    }

    /// <summary>
    /// Transfers the built data to the specified <see cref="MetadataDb"/> and disposes this builder.
    /// </summary>
    /// <param name="targetData">The target metadata database to receive the data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void SetAndDispose(ref MetadataDb targetData)
    {
        // We don't need to initialize the metadata DB if we are creating a whole document
        // This allows us to hand off the parsed data rather than writing it in.
        Debug.Assert(!targetData.IsInitialized);
        targetData = _parsedData;
    }

    /// <summary>
    /// Inserts the built data into the specified <see cref="MetadataDb"/> at the given index and disposes this builder.
    /// </summary>
    /// <param name="complexObjectStartIndex">The start index of the complex object in the target database.</param>
    /// <param name="targetIndex">The index at which to insert the data.</param>
    /// <param name="targetData">The target metadata database to receive the data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InsertAndDispose(int complexObjectStartIndex, int targetIndex, ref MetadataDb targetData)
    {
        targetData.InsertRowsInComplexObject(_parentDocument, complexObjectStartIndex, targetIndex, _rowCount, _memberCount);
        _parsedData.Overwrite(ref targetData, targetIndex);
    }

    /// <summary>
    /// Add a property name to the current object.
    /// </summary>
    /// <param name="stringValue"></param>
    /// <param name="escape">Indicates whether to escape the property name.</param>
    /// <param name="ifNotEscapeRequiresUenscaping">Indicates whether the property name needs unescaping if it is not to be escaped.</param>
    /// <returns>The handle for the property.</returns>
    public ComplexValueHandle StartProperty(ReadOnlySpan<byte> stringValue, bool escape, bool ifNotEscapeRequiresUenscaping)
    {
        var result = new ComplexValueHandle(_memberCount, _rowCount);
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, stringValue, escape, ifNotEscapeRequiresUenscaping);
        return result;
    }

    /// <summary>
    /// Add a property name to the current object.
    /// </summary>
    /// <param name="propertyName">The property name as a character span.</param>
    /// <returns>The handle for the property.</returns>
    public ComplexValueHandle StartProperty(ReadOnlySpan<char> propertyName)
    {
        var result = new ComplexValueHandle(_memberCount, _rowCount);
        _memberCount = 0;
        _rowCount = 0;
        AddStringValue(JsonTokenType.PropertyName, propertyName);
        return result;
    }

    /// <summary>
    /// Add a property name to the current object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The handle for the property.</returns>
    public ComplexValueHandle StartProperty(string propertyName)
    {
        return StartProperty(propertyName.AsSpan());
    }

    /// <summary>
    /// Ends the property with the given property handle.
    /// </summary>
    /// <param name="handle">The handle of the property to end.</param>
    public void EndProperty(in ComplexValueHandle handle)
    {
        _memberCount = handle.MemberCount + 1;
        _rowCount = handle.RowCount + _rowCount + 1;
    }

    /// <summary>
    /// Start an array item.
    /// </summary>
    /// <returns>The handle for the property.</returns>
    public ComplexValueHandle StartItem()
    {
        var result = new ComplexValueHandle(_memberCount, _rowCount);
        _memberCount = 0;
        _rowCount = 0;
        return result;
    }

    /// <summary>
    /// Ends the given array item.
    /// </summary>
    /// <param name="handle">The handle of the item to end.</param>
    public void EndItem(in ComplexValueHandle handle)
    {
        _memberCount = handle.MemberCount + 1;
        _rowCount += handle.RowCount;
    }

    /// <summary>
    /// Represents a handle that tracks the state of a complex value (property or array item) during construction.
    /// </summary>
    public readonly struct ComplexValueHandle
    {
        /// <summary>
        /// Gets the number of members that were present when this handle was created.
        /// </summary>
        internal int MemberCount { get; }

        /// <summary>
        /// Gets the number of rows that were present when this handle was created.
        /// </summary>
        internal int RowCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplexValueHandle"/> struct.
        /// </summary>
        /// <param name="memberCount">The current member count.</param>
        /// <param name="rowCount">The current row count.</param>
        internal ComplexValueHandle(int memberCount, int rowCount)
        {
            MemberCount = memberCount;
            RowCount = rowCount;
        }
    }

    /// <summary>
    /// Overwrites a range of data in the specified <see cref="MetadataDb"/> with the built data and disposes this builder.
    /// </summary>
    /// <param name="complexObjectStartIndex">The start index of the complex object in the target database.</param>
    /// <param name="startIndex">The start index of the range to overwrite.</param>
    /// <param name="endIndex">The end index of the range to overwrite.</param>
    /// <param name="memberCountToReplace">The number of members to replace.</param>
    /// <param name="targetData">The target metadata database to receive the data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OverwriteAndDispose(int complexObjectStartIndex, int startIndex, int endIndex, int memberCountToReplace, ref MetadataDb targetData)
    {
        targetData.ReplaceRowsInComplexObject(_parentDocument, complexObjectStartIndex, startIndex, endIndex, memberCountToReplace, _rowCount, _memberCount);
        _parsedData.Overwrite(ref targetData, startIndex);
    }

    private void AddStringValue(JsonTokenType tokenType, ReadOnlySpan<byte> stringValue, bool escape, bool ifNotEscapeRequiresUenscaping)
    {
        Debug.Assert(tokenType is JsonTokenType.PropertyName or JsonTokenType.String);

        if (!escape)
        {
            int location = _parentDocument.StoreRawStringValue(stringValue);
            _parsedData.AppendDynamicSimpleValue(tokenType, location, requiresUnescapingOrHasExponent: ifNotEscapeRequiresUenscaping);
        }
        else
        {
            int location = _parentDocument.EscapeAndStoreRawStringValue(stringValue, out bool requiredEscaping);
            _parsedData.AppendDynamicSimpleValue(tokenType, location, requiresUnescapingOrHasExponent: requiredEscaping);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddStringValue(JsonTokenType tokenType, ReadOnlySpan<char> stringValue)
    {
        Debug.Assert(tokenType is JsonTokenType.PropertyName or JsonTokenType.String);

        int location = _parentDocument.EscapeAndStoreRawStringValue(stringValue, out bool requiredEscaping);
        _parsedData.AppendDynamicSimpleValue(tokenType, location, requiresUnescapingOrHasExponent: requiredEscaping);
    }
}