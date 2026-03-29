// <copyright file="JsonElementForBooleanFalseSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Represents a placeholder for the <c>false</c> boolean schema which disallows any value.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly partial struct JsonElementForBooleanFalseSchema : IJsonElement<JsonElementForBooleanFalseSchema>
{
    private readonly IJsonDocument _parent;

    private readonly int _idx;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonElementForBooleanFalseSchema"/> struct.
    /// </summary>
    /// <param name="parent">The parent JSON document.</param>
    /// <param name="idx">The index within the parent document.</param>
    internal JsonElementForBooleanFalseSchema(IJsonDocument parent, int idx)
    {
        // parent is usually not null, but the Current property
        // on the enumerators (when initialized as `default`) can
        // get here with a null.
        Debug.Assert(idx >= 0);

        _parent = parent;
        _idx = idx;
    }

    /// <summary>
    /// The <see cref="JsonValueKind"/> that the value is.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonValueKind ValueKind => TokenType.ToValueKind();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private JsonTokenType TokenType
    {
        get
        {
            return _parent?.GetJsonTokenType(_idx) ?? JsonTokenType.None;
        }
    }

    /// <summary>
    /// Implicitly converts a <see cref="JsonElementForBooleanFalseSchema"/> to an <see cref="int"/>.
    /// </summary>
    /// <param name="age">The JSON element to convert.</param>
    /// <returns>The integer value of the JSON element.</returns>
    /// <exception cref="FormatException">The JSON element cannot be converted to an integer.</exception>
    public static implicit operator int(JsonElementForBooleanFalseSchema age)
    {
        age.CheckValidInstance();

        if (!age._parent.TryGetValue(age._idx, out int result))
        {
            CodeGenThrowHelper.ThrowFormatException(CodeGenNumericType.Int32);
        }

        return result;
    }

    /// <summary>
    /// Determines whether two <see cref="JsonElementForBooleanFalseSchema"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(JsonElementForBooleanFalseSchema left, JsonElementForBooleanFalseSchema right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="JsonElementForBooleanFalseSchema"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(JsonElementForBooleanFalseSchema left, JsonElementForBooleanFalseSchema right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Determines whether a <see cref="JsonElementForBooleanFalseSchema"/> and a <see cref="JsonElement"/> are equal.
    /// </summary>
    /// <param name="left">The <see cref="JsonElementForBooleanFalseSchema"/> instance to compare.</param>
    /// <param name="right">The <see cref="JsonElement"/> instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(JsonElementForBooleanFalseSchema left, JsonElement right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether a <see cref="JsonElementForBooleanFalseSchema"/> and a <see cref="JsonElement"/> are not equal.
    /// </summary>
    /// <param name="left">The <see cref="JsonElementForBooleanFalseSchema"/> instance to compare.</param>
    /// <param name="right">The <see cref="JsonElement"/> instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(JsonElementForBooleanFalseSchema left, JsonElement right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current instance; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return (obj is IJsonElement other && Equals(new JsonElementForBooleanFalseSchema(other.ParentDocument, other.ParentDocumentIndex)))
            || (obj is null && this.IsNull());
    }

    /// <summary>
    /// Determines whether the specified JSON element is equal to the current instance.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element to compare.</typeparam>
    /// <param name="other">The JSON element to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified JSON element is equal to the current instance; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals<T>(T other)
        where T : struct, IJsonElement
    {
        return JsonElementHelpers.DeepEquals(this, other);
    }

    /// <summary>
    /// Creates a new <see cref="JsonElementForBooleanFalseSchema"/> from the specified JSON element instance.
    /// </summary>
    /// <typeparam name="T">The type of the JSON element.</typeparam>
    /// <param name="instance">The JSON element instance to create from.</param>
    /// <returns>A new <see cref="JsonElementForBooleanFalseSchema"/> instance.</returns>
    [CLSCompliant(false)]
    public static JsonElementForBooleanFalseSchema From<T>(in T instance)
        where T : struct, IJsonElement<T>
    {
        return new(instance.ParentDocument, instance.ParentDocumentIndex);
    }

    /// <summary>
    /// Parses UTF8-encoded text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="utf8Json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException"><paramref name="utf8Json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElementForBooleanFalseSchema ParseValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<byte> utf8Json, JsonDocumentOptions options = default)
    {
        return JsonElementHelpers.ParseValue<JsonElementForBooleanFalseSchema>(utf8Json);
    }

    /// <summary>
    /// Parses text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException"><paramref name="json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElementForBooleanFalseSchema ParseValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonDocumentOptions options = default)
    {
        return JsonElementHelpers.ParseValue<JsonElementForBooleanFalseSchema>(json);
    }

    /// <summary>
    /// Parses text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException"><paramref name="json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElementForBooleanFalseSchema ParseValue([StringSyntax(StringSyntaxAttribute.Json)] string json, JsonDocumentOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonElementHelpers.ParseValue<JsonElementForBooleanFalseSchema>(json);
    }

    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided reader.
    /// </summary>
    /// <param name="reader">The reader to read.</param>
    /// <returns>
    /// A JsonElement representing the value (and nested values) read from the reader.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the <see cref="Utf8JsonReader.TokenType"/> property of <paramref name="reader"/>
    /// is <see cref="JsonTokenType.PropertyName"/> or <see cref="JsonTokenType.None"/>, the
    /// reader will be advanced by one call to <see cref="Utf8JsonReader.Read"/> to determine
    /// the start of the value.
    /// </para>
    ///
    /// <para>
    /// Upon completion of this method, <paramref name="reader"/> will be positioned at the
    /// final token in the JSON value. If an exception is thrown, the reader is reset to
    /// the state it was in when the method was called.
    /// </para>
    ///
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="reader"/> is using unsupported options.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The current <paramref name="reader"/> token does not start or represent a value.
    /// </exception>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElementForBooleanFalseSchema ParseValue(ref Utf8JsonReader reader)
    {
        return JsonElementHelpers.ParseValue<JsonElementForBooleanFalseSchema>(ref reader);
    }

    /// <summary>
    /// Attempts to parse one JSON value (including objects or arrays) from the provided reader.
    /// </summary>
    /// <param name="reader">The reader to read.</param>
    /// <param name="element">Receives the parsed element.</param>
    /// <returns>
    /// <see langword="true"/> if a value was read and parsed into a JsonElement;
    /// <see langword="false"/> if the reader ran out of data while parsing.
    /// All other situations result in an exception being thrown.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the <see cref="Utf8JsonReader.TokenType"/> property of <paramref name="reader"/>
    /// is <see cref="JsonTokenType.PropertyName"/> or <see cref="JsonTokenType.None"/>, the
    /// reader will be advanced by one call to <see cref="Utf8JsonReader.Read"/> to determine
    /// the start of the value.
    /// </para>
    ///
    /// <para>
    /// Upon completion of this method, <paramref name="reader"/> will be positioned at the
    /// final token in the JSON value.  If an exception is thrown, or <see langword="false"/>
    /// is returned, the reader is reset to the state it was in when the method was called.
    /// </para>
    ///
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="reader"/> is using unsupported options.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The current <paramref name="reader"/> token does not start or represent a value.
    /// </exception>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    public static bool TryParseValue(ref Utf8JsonReader reader, [NotNullWhen(true)] out JsonElementForBooleanFalseSchema? element)
    {
        return JsonElementHelpers.TryParseValue(ref reader, out element);
    }

    /// <summary>
    /// Creates a JSON document containing the specified integer value.
    /// </summary>
    /// <param name="workspace">The JSON workspace to use for document creation.</param>
    /// <param name="year">The integer value to include in the document.</param>
    /// <param name="initialCapacity">The initial capacity for the document builder.</param>
    /// <returns>A JSON document builder containing the specified value.</returns>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateDocument(JsonWorkspace workspace, int year, int initialCapacity = 30)
    {
        // Create the document builder without a MetadataDb
        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);
        cvb.AddItem(year);
        Debug.Assert(cvb.MemberCount == 1);
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Creates a JSON document from the current instance.
    /// </summary>
    /// <param name="workspace">The JSON workspace to use for document creation.</param>
    /// <returns>A JSON document builder containing the current instance.</returns>
    [CLSCompliant(false)]
    public JsonDocumentBuilder<Mutable> CreateDocument(JsonWorkspace workspace)
    {
        return workspace.CreateBuilder<JsonElementForBooleanFalseSchema, Mutable>(this);
    }

    /// <summary>
    /// Write the element into the provided writer as a JSON value.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <exception cref="ArgumentNullException">
    /// The <paramref name="writer"/> parameter is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is <see cref="JsonValueKind.Undefined"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public void WriteTo(Utf8JsonWriter writer)
    {
        CheckValidInstance();

        _parent.WriteElementTo(_idx, writer);
    }

    /// <summary>
    /// Gets a string representation for the current value appropriate to the value type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For JsonElement built from <see cref="JsonDocument"/>:
    /// </para>
    ///
    /// <para>
    /// For <see cref="JsonValueKind.Null"/>, <see cref="string.Empty"/> is returned.
    /// </para>
    ///
    /// <para>
    /// For <see cref="JsonValueKind.True"/>, <see cref="bool.TrueString"/> is returned.
    /// </para>
    ///
    /// <para>
    /// For <see cref="JsonValueKind.False"/>, <see cref="bool.FalseString"/> is returned.
    /// </para>
    ///
    /// <para>
    /// For <see cref="JsonValueKind.String"/>, the value of <see cref="GetString"/>() is returned.
    /// </para>
    ///
    /// <para>
    /// For other types, the value of <see cref="GetRawText"/>() is returned.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A string representation for the current value appropriate to the value type.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public override string ToString()
    {
        if (_parent is null)
        {
            return string.Empty;
        }

        return _parent.ToString(_idx);
    }

    /// <summary>
    /// Gets the hash code for the current instance.
    /// </summary>
    /// <returns>A hash code for the current instance.</returns>
    public override int GetHashCode()
    {
        if (_parent is null)
        {
            return 0;
        }

        return _parent.GetHashCode(_idx);
    }

    private void CheckValidInstance()
    {
        if (_parent == null)
        {
            throw new InvalidOperationException();
        }
    }

    void IJsonElement.CheckValidInstance() => CheckValidInstance();

#if NET

    static JsonElementForBooleanFalseSchema IJsonElement<JsonElementForBooleanFalseSchema>.CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => new(parentDocument, parentDocumentIndex);

#endif

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"JsonElementForBooleanFalseSchema: ValueKind = {ValueKind} : \"{ToString()}\"";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IJsonDocument IJsonElement.ParentDocument => _parent;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int IJsonElement.ParentDocumentIndex => _idx;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonTokenType IJsonElement.TokenType => TokenType;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonValueKind IJsonElement.ValueKind => ValueKind;
}