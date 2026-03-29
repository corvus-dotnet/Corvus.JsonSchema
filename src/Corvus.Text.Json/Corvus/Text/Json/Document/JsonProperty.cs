// <copyright file="JsonProperty.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Represents a single property for a JSON object.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[CLSCompliant(false)]
public readonly struct JsonProperty<TValue>
    where TValue : struct, IJsonElement<TValue>
{
    internal JsonProperty(TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// The name of this property.
    /// </summary>
    /// <remarks>Note that this allocates.</remarks>
    /// <seealso cref="Utf8NameSpan"/>.
    public string Name
    {
        get
        {
            Value.CheckValidInstance();
            return Value.ParentDocument.GetNameOfPropertyValue(Value.ParentDocumentIndex);
        }
    }

    /// <summary>
    /// Gets the name as an unescaped UTF-8 JSON string.
    /// </summary>
    /// <remarks>
    /// Note that this does not allocate. The result should be
    /// disposed when it is no longer needed, as it may use a rented buffer to back the string.
    /// It is only valid for the lifetime of the document that contains this property.
    /// </remarks>
    public UnescapedUtf8JsonString Utf8NameSpan
    {
        get
        {
            return Value.ParentDocument.GetUtf8JsonString(Value.ParentDocumentIndex - DbRow.Size, JsonTokenType.PropertyName);
        }
    }

    /// <summary>
    /// Gets the name as an unescaped UTF-16 JSON string.
    /// </summary>
    /// <remarks>
    /// Note that this does not allocate. The result should be
    /// disposed when it is no longer needed, as it may use a rented buffer to back the string.
    /// It is only valid for the lifetime of the document that contains this property.
    /// </remarks>
    public UnescapedUtf16JsonString Utf16NameSpan
    {
        get
        {
            return Value.ParentDocument.GetUtf16JsonString(Value.ParentDocumentIndex - DbRow.Size, JsonTokenType.PropertyName);
        }
    }

    /// <summary>
    /// The value of this property.
    /// </summary>
    public TValue Value { get; }

    internal bool NameIsEscaped => Value.ParentDocument.ValueIsEscaped(Value.ParentDocumentIndex, isPropertyName: true);

    internal ReadOnlySpan<byte> RawNameSpan
    {
        get
        {
            Value.CheckValidInstance();
            return Value.ParentDocument.GetPropertyNameRaw(Value.ParentDocumentIndex);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
            => Value.ValueKind == JsonValueKind.Undefined ? "<Undefined>" : $"\"{ToString()}\"";

    /// <summary>
    /// Compares <paramref name="text" /> to the name of this property.
    /// </summary>
    /// <param name="text">The text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the name of this property matches <paramref name="text"/>,
    /// <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="Type"/> is not <see cref="JsonTokenType.PropertyName"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
    /// <see cref="Name" />, but can avoid creating the string instance.
    /// </remarks>
    public bool NameEquals(string? text)
    {
        return NameEquals(text.AsSpan());
    }

    /// <summary>
    /// Compares the text represented by <paramref name="utf8Text" /> to the name of this property.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the name of this property has the same UTF-8 encoding as
    /// <paramref name="utf8Text" />, <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="Type"/> is not <see cref="JsonTokenType.PropertyName"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of <paramref name="utf8Text" /> and
    /// <see cref="Utf8NameSpan" />, but can avoid creating the UTF8 string instance.
    /// </remarks>
    public bool NameEquals(ReadOnlySpan<byte> utf8Text)
    {
        Value.CheckValidInstance();
        return Value.ParentDocument.TextEquals(Value.ParentDocumentIndex, utf8Text, isPropertyName: true, shouldUnescape: true);
    }

    /// <summary>
    /// Compares <paramref name="text" /> to the name of this property.
    /// </summary>
    /// <param name="text">The text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the name of this property matches <paramref name="text"/>,
    /// <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="Type"/> is not <see cref="JsonTokenType.PropertyName"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of <paramref name="utf8Text" /> and
    /// <see cref="Utf8NameSpan" />, but can avoid creating the UTF-8 string instance.
    /// </remarks>
    public bool NameEquals(ReadOnlySpan<char> text)
    {
        Value.CheckValidInstance();
        return Value.ParentDocument.TextEquals(Value.ParentDocumentIndex, text, isPropertyName: true);
    }

    /// <summary>
    /// Provides a <see cref="string"/> representation of the property for
    /// debugging purposes.
    /// </summary>
    /// <returns>
    /// A string containing the un-interpreted value of the property, beginning
    /// at the declaring open-quote and ending at the last character that is part of
    /// the value.
    /// </returns>
    public override string ToString()
    {
        if (Value.ParentDocument is null)
        {
            return string.Empty;
        }

        return Value.ParentDocument.GetPropertyRawValueAsString(Value.ParentDocumentIndex);
    }

    /// <summary>
    /// Write the property into the provided writer as a named JSON object property.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <exception cref="ArgumentNullException">
    /// The <paramref name="writer"/> parameter is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// This <see cref="Name"/>'s length is too large to be a JSON object property.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This <see cref="Value"/>'s <see cref="JsonElement.ValueKind"/> would result in an invalid JSON.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        Value.CheckValidInstance();

        Value.ParentDocument.WritePropertyName(Value.ParentDocumentIndex, writer);
        Value.ParentDocument.WriteElementTo(Value.ParentDocumentIndex, writer);
    }
}