// <copyright file="JsonElement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;

namespace Corvus.Text.Json;

/// <summary>
/// Represents a specific JSON value within a <see cref="JsonDocument"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly partial struct JsonElement
    : IJsonElement<JsonElement>,
#if NET
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
#else
    IFormattable
#endif
{
    private readonly IJsonDocument _parent;

    private readonly int _idx;

    internal JsonElement(IJsonDocument parent, int idx)
    {
        // parent is usually not null, but the Current property
        // on the enumerators (when initialized as `default`) can
        // get here with a null.
        Debug.Assert(idx >= 0);

        _parent = parent;
        _idx = idx;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private JsonTokenType TokenType
    {
        get
        {
            return _parent?.GetJsonTokenType(_idx) ?? JsonTokenType.None;
        }
    }

    /// <summary>
    /// The <see cref="JsonValueKind"/> that the value is.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonValueKind ValueKind => TokenType.ToValueKind();

    /// <summary>
    /// Get the value at a specified index when the current value is a
    /// <see cref="JsonValueKind.Array"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
    /// </exception>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="index"/> is not in the range [0, <see cref="GetArrayLength"/>()).
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonElement this[int index]
    {
        get
        {
            CheckValidInstance();

            return _parent.GetArrayIndexElement(_idx, index);
        }
    }

    /// <summary>
    /// Gets the value of the property with the given UTF-8 encoded name when the current value is an
    /// <see cref="JsonValueKind.Object"/>.
    /// </summary>
    /// <param name="propertyName">
    /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property.
    /// </param>
    /// <returns>
    /// The value of the property with the given name, or a default <see cref="JsonElement"/>
    /// if no such property exists.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonElement this[ReadOnlySpan<byte> propertyName]
    {
        get
        {
            CheckValidInstance();

            if (!_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                return default;
            }

            return value;
        }
    }

    /// <summary>
    /// Gets the value of the property with the given name when the current value is an
    /// <see cref="JsonValueKind.Object"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>
    /// The value of the property with the given name, or a default <see cref="JsonElement"/>
    /// if no such property exists.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonElement this[ReadOnlySpan<char> propertyName]
    {
        get
        {
            CheckValidInstance();

            if (!_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                return default;
            }

            return value;
        }
    }

    /// <summary>
    /// Gets the value of the property with the given name when the current value is an
    /// <see cref="JsonValueKind.Object"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>
    /// The value of the property with the given name, or a default <see cref="JsonElement"/>
    /// if no such property exists.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="propertyName"/> is <see langword="null"/>.
    /// </exception>
    public JsonElement this[string propertyName]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return this[propertyName.AsSpan()];
        }
    }

    /// <summary>
    /// Compares two JsonElement values for equality.
    /// </summary>
    /// <param name="left">The first JsonElement to compare.</param>
    /// <param name="right">The second JsonElement to compare.</param>
    /// <returns><see langword="true"/> if the JsonElement values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(JsonElement left, JsonElement right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two JsonElement values for inequality.
    /// </summary>
    /// <param name="left">The first JsonElement to compare.</param>
    /// <param name="right">The second JsonElement to compare.</param>
    /// <returns><see langword="true"/> if the JsonElement values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(JsonElement left, JsonElement right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current JsonElement.
    /// </summary>
    /// <param name="obj">The object to compare with the current JsonElement.</param>
    /// <returns><see langword="true"/> if the specified object is equal to the current JsonElement; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return (obj is IJsonElement other && Equals(new JsonElement(other.ParentDocument, other.ParentDocumentIndex)))
            || (obj is null && this.IsNull());
    }

    /// <summary>
    /// Determines whether the current JsonElement is equal to another JsonElement-like value through deep comparison.
    /// </summary>
    /// <typeparam name="T">The type of the other JSON element that implements IJsonElement.</typeparam>
    /// <param name="other">The JSON element to compare with this JsonElement.</param>
    /// <returns><see langword="true"/> if the current JsonElement is equal to the other parameter; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals<T>(T other)
        where T : struct, IJsonElement
    {
        return JsonElementHelpers.DeepEquals(this, other);
    }

    /// <summary>
    /// Creates a mutable document builder from this JsonElement using the specified workspace.
    /// </summary>
    /// <param name="workspace">The JsonWorkspace to use for creating the document builder.</param>
    /// <returns>A JsonDocumentBuilder configured for mutable operations on this JsonElement.</returns>
    [CLSCompliant(false)]
    public JsonDocumentBuilder<Mutable> CreateBuilder(JsonWorkspace workspace)
    {
        return workspace.CreateBuilder<JsonElement, Mutable>(this);
    }

    /// <summary>
    /// Create an instance of a <see cref="JsonElement"/> from a <see cref="IJsonElement"/>.
    /// </summary>
    /// <typeparam name="T">The type of the source element.</typeparam>
    /// <param name="instance">The instance of the source element.</param>
    /// <returns>An instance of a <see cref="JsonELement"/>.</returns>
    [CLSCompliant(false)]
    public static JsonElement From<T>(in T instance)
        where T : struct, IJsonElement<T>
    {
        return new(instance.ParentDocument, instance.ParentDocumentIndex);
    }

    /// <summary>
    /// Get the number of values contained within the current array value.
    /// </summary>
    /// <returns>The number of values contained within the current array value.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public int GetArrayLength()
    {
        CheckValidInstance();

        return _parent.GetArrayLength(_idx);
    }

    /// <summary>
    /// Get the number of properties contained within the current object value.
    /// </summary>
    /// <returns>The number of properties contained within the current object value.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public int GetPropertyCount()
    {
        CheckValidInstance();

        return _parent.GetPropertyCount(_idx);
    }

    /// <summary>
    /// Gets a <see cref="JsonElement"/> representing the value of a required property identified
    /// by <paramref name="propertyName"/>.
    /// </summary>
    /// <remarks>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    ///
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </remarks>
    /// <param name="propertyName">Name of the property whose value to return.</param>
    /// <returns>
    /// A <see cref="JsonElement"/> representing the value of the requested property.
    /// </returns>
    /// <seealso cref="EnumerateObject"/>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// No property was found with the requested name.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="propertyName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonElement GetProperty(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (TryGetProperty(propertyName, out JsonElement property))
        {
            return property;
        }

        throw new KeyNotFoundException();
    }

    /// <summary>
    /// Gets a <see cref="JsonElement"/> representing the value of a required property identified
    /// by <paramref name="propertyName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    /// </para>
    ///
    /// <para>
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">Name of the property whose value to return.</param>
    /// <returns>
    /// A <see cref="JsonElement"/> representing the value of the requested property.
    /// </returns>
    /// <seealso cref="EnumerateObject"/>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// No property was found with the requested name.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public JsonElement GetProperty(ReadOnlySpan<char> propertyName)
    {
        if (TryGetProperty(propertyName, out JsonElement property))
        {
            return property;
        }

        throw new KeyNotFoundException();
    }

    /// <summary>
    /// Gets a <see cref="JsonElement"/> representing the value of a required property identified
    /// by <paramref name="utf8PropertyName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    /// </para>
    ///
    /// <para>
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </para>
    /// </remarks>
    /// <param name="utf8PropertyName">
    /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
    /// </param>
    /// <returns>
    /// A <see cref="JsonElement"/> representing the value of the requested property.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// No property was found with the requested name.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="EnumerateObject"/>
    public JsonElement GetProperty(ReadOnlySpan<byte> utf8PropertyName)
    {
        if (TryGetProperty(utf8PropertyName, out JsonElement property))
        {
            return property;
        }

        throw new KeyNotFoundException();
    }

    /// <summary>
    /// Looks for a property named <paramref name="propertyName"/> in the current object, returning
    /// whether or not such a property existed. When the property exists <paramref name="value"/>
    /// is assigned to the value of that property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    /// </para>
    ///
    /// <para>
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">Name of the property to find.</param>
    /// <param name="value">Receives the value of the located property.</param>
    /// <returns>
    /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="propertyName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="EnumerateObject"/>
    public bool TryGetProperty(string propertyName, out JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        return TryGetProperty(propertyName.AsSpan(), out value);
    }

    /// <summary>
    /// Looks for a property named <paramref name="propertyName"/> in the current object, returning
    /// whether or not such a property existed. When the property exists <paramref name="value"/>
    /// is assigned to the value of that property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    /// </para>
    ///
    /// <para>
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">Name of the property to find.</param>
    /// <param name="value">Receives the value of the located property.</param>
    /// <returns>
    /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
    /// </returns>
    /// <seealso cref="EnumerateObject"/>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetProperty(ReadOnlySpan<char> propertyName, out JsonElement value)
    {
        CheckValidInstance();

        return _parent.TryGetNamedPropertyValue(_idx, propertyName, out value);
    }

    /// <summary>
    /// Looks for a property named <paramref name="utf8PropertyName"/> in the current object, returning
    /// whether or not such a property existed. When the property exists <paramref name="value"/>
    /// is assigned to the value of that property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property name matching is performed as an ordinal, case-sensitive, comparison.
    /// </para>
    ///
    /// <para>
    /// If a property is defined multiple times for the same object, the last such definition is
    /// what is matched.
    /// </para>
    /// </remarks>
    /// <param name="utf8PropertyName">
    /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
    /// </param>
    /// <param name="value">Receives the value of the located property.</param>
    /// <returns>
    /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
    /// </returns>
    /// <seealso cref="EnumerateObject"/>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetProperty(ReadOnlySpan<byte> utf8PropertyName, out JsonElement value)
    {
        CheckValidInstance();

        return _parent.TryGetNamedPropertyValue(_idx, utf8PropertyName, out value);
    }

    /// <summary>
    /// Tries to get the value as a boolean
    /// </summary>
    /// <param name="value">Provides the boolean value if successful.</param>
    /// <returns><see langword="true"/> if the value was a boolean, otherwise false.</returns>
    public bool TryGetBoolean(out bool value)
    {
        JsonTokenType type = TokenType;

        switch (type)
        {
            case JsonTokenType.True:
                value = true;
                return true;
            case JsonTokenType.False:
                value = false;
                return true;
            default:
                value = default;
                return false;
        }
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="bool"/>.
    /// </summary>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="bool"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.True"/> or
    /// <see cref="JsonValueKind.False"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool GetBoolean()
    {
        // CheckValidInstance is redundant.  Asking for the type will
        // return None, which then throws the same exception in the return statement.
        JsonTokenType type = TokenType;

#pragma warning disable IDE0075, RCS1104 // Disable the IDE suggestion to simplify the conditional as it makes it unreadable
        return
            type == JsonTokenType.True ||
            (type == JsonTokenType.False ? false :
            ThrowJsonElementWrongTypeException(type));
#pragma warning restore IDE0075, RCS1104

        static bool ThrowJsonElementWrongTypeException(JsonTokenType actualType)
        {
            throw ThrowHelper.GetJsonElementWrongTypeException(nameof(Boolean), actualType.ToValueKind());
        }
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="string"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a string representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="string"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.String"/> nor <see cref="JsonValueKind.Null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public string? GetString()
    {
        CheckValidInstance();

        return _parent.GetString(_idx, JsonTokenType.String);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="UnescapedUtf8JsonString"/>.
    /// </summary>
    /// <returns>The value of the element as an <see cref="UnescapedUtf8JsonString"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.String"/> nor <see cref="JsonValueKind.Null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    /// <remarks>
    /// The <see cref="UnescapedUtf8JsonString"/> should be disposed when it is finished with, as it may have rented
    /// storage to provide the unescaped value. It is only valid for as long as the source <see cref="JsonElement"/>
    /// is valid.
    /// </remarks>
    public UnescapedUtf8JsonString GetUtf8String()
    {
        CheckValidInstance();

        return _parent.GetUtf8JsonString(_idx, JsonTokenType.String);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="UnescapedUtf16JsonString"/>.
    /// </summary>
    /// <returns>The value of the element as an <see cref="UnescapedUtf16JsonString"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.String"/> nor <see cref="JsonValueKind.Null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    /// <remarks>
    /// The <see cref="UnescapedUtf16JsonString"/> should be disposed when it is finished with, as it may have rented
    /// storage to provide the unescaped value. It is only valid for as long as the source <see cref="JsonElement"/>
    /// is valid.
    /// </remarks>
    public UnescapedUtf16JsonString GetUtf16String()
    {
        CheckValidInstance();

        return _parent.GetUtf16JsonString(_idx, JsonTokenType.String);
    }

    /// <summary>
    /// Attempts to represent the current JSON string as bytes assuming it is Base64 encoded.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a byte[] representation of values other than base 64 encoded JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the entire token value is encoded as valid Base64 text and can be successfully decoded to bytes.
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetBytesFromBase64([NotNullWhen(true)] out byte[]? value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as bytes.
    /// </summary>
    /// <remarks>
    /// This method does not create a byte[] representation of values other than Base64 encoded JSON strings.
    /// </remarks>
    /// <returns>The value decode to bytes.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value is not encoded as Base64 text and hence cannot be decoded to bytes.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public byte[] GetBytesFromBase64()
    {
        if (!TryGetBytesFromBase64(out byte[]? value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as an <see cref="sbyte"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as an <see cref="sbyte"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public bool TryGetSByte(out sbyte value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as an <see cref="sbyte"/>.
    /// </summary>
    /// <returns>The current JSON number as an <see cref="sbyte"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as an <see cref="sbyte"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public sbyte GetSByte()
    {
        if (TryGetSByte(out sbyte value))
        {
            return value;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="byte"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="byte"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetByte(out byte value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="byte"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="byte"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="byte"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public byte GetByte()
    {
        if (TryGetByte(out byte value))
        {
            return value;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Attempts to represent the current JSON number as an <see cref="short"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as an <see cref="short"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetInt16(out short value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as an <see cref="short"/>.
    /// </summary>
    /// <returns>The current JSON number as an <see cref="short"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as an <see cref="short"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public short GetInt16()
    {
        if (TryGetInt16(out short value))
        {
            return value;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="ushort"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="ushort"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public bool TryGetUInt16(out ushort value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="ushort"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="ushort"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="ushort"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public ushort GetUInt16()
    {
        if (TryGetUInt16(out ushort value))
        {
            return value;
        }

        throw new FormatException();
    }

    /// <summary>
    /// Attempts to represent the current JSON number as an <see cref="int"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as an <see cref="int"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetInt32(out int value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as an <see cref="int"/>.
    /// </summary>
    /// <returns>The current JSON number as an <see cref="int"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as an <see cref="int"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public int GetInt32()
    {
        if (!TryGetInt32(out int value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="uint"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="uint"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public bool TryGetUInt32(out uint value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="uint"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="uint"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="uint"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public uint GetUInt32()
    {
        if (!TryGetUInt32(out uint value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="long"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="long"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetInt64(out long value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="long"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="long"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="long"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public long GetInt64()
    {
        if (!TryGetInt64(out long value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="ulong"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="ulong"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public bool TryGetUInt64(out ulong value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

#if NET

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="Int128"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="Int128"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public readonly bool TryGetInt128(out Int128 value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="Int128"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="Int128"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="Int128"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public readonly Int128 GetInt128()
    {
        if (!TryGetInt128(out Int128 value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="UInt128"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="UInt128"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    [CLSCompliant(false)]
    public readonly bool TryGetUInt128(out UInt128 value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="UInt128"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="UInt128"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="UInt128"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    [CLSCompliant(false)]
    public readonly UInt128 GetUInt128()
    {
        if (!TryGetUInt128(out UInt128 value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="Half"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="Half"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public readonly bool TryGetHalf(out Half value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="Half"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="Half"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="Half"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public readonly Half GetHalf()
    {
        if (!TryGetHalf(out Half value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

#endif

    /// <summary>
    /// Gets the current JSON number as a <see cref="ulong"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="ulong"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="ulong"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public ulong GetUInt64()
    {
        if (!TryGetUInt64(out ulong value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="double"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// <para>
    /// This method does not parse the contents of a JSON string value.
    /// </para>
    ///
    /// <para>
    /// On .NET Core this method does not return <see langword="false"/> for values larger than
    /// <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>),
    /// instead <see langword="true"/> is returned and <see cref="double.PositiveInfinity"/> (or
    /// <see cref="double.NegativeInfinity"/>) is emitted.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="double"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetDouble(out double value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="double"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="double"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method does not parse the contents of a JSON string value.
    /// </para>
    ///
    /// <para>
    /// On .NET Core this method returns <see cref="double.PositiveInfinity"/> (or
    /// <see cref="double.NegativeInfinity"/>) for values larger than
    /// <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>).
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="double"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public double GetDouble()
    {
        if (!TryGetDouble(out double value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="float"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// <para>
    /// This method does not parse the contents of a JSON string value.
    /// </para>
    ///
    /// <para>
    /// On .NET Core this method does not return <see langword="false"/> for values larger than
    /// <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>),
    /// instead <see langword="true"/> is returned and <see cref="float.PositiveInfinity"/> (or
    /// <see cref="float.NegativeInfinity"/>) is emitted.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="float"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetSingle(out float value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="float"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="float"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method does not parse the contents of a JSON string value.
    /// </para>
    ///
    /// <para>
    /// On .NET Core this method returns <see cref="float.PositiveInfinity"/> (or
    /// <see cref="float.NegativeInfinity"/>) for values larger than
    /// <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>).
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="float"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public float GetSingle()
    {
        if (!TryGetSingle(out float value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="decimal"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="decimal"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public bool TryGetDecimal(out decimal value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="decimal"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="decimal"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="decimal"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public decimal GetDecimal()
    {
        if (!TryGetDecimal(out decimal value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="BigNumber"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="BigNumber"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    [CLSCompliant(false)]
    public bool TryGetBigNumber(out BigNumber value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="BigNumber"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="BigNumber"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="BigNumber"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    [CLSCompliant(false)]
    public BigNumber GetBigNumber()
    {
        if (!TryGetBigNumber(out BigNumber value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON number as a <see cref="BigInteger"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the number can be represented as a <see cref="BigInteger"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public bool TryGetBigInteger(out BigInteger value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the current JSON number as a <see cref="BigInteger"/>.
    /// </summary>
    /// <returns>The current JSON number as a <see cref="BigInteger"/>.</returns>
    /// <remarks>
    /// This method does not parse the contents of a JSON string value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="BigInteger"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="GetRawText"/>
    public BigInteger GetBigInteger()
    {
        if (!TryGetBigInteger(out BigInteger value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="LocalDate"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a LocalDate representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="LocalDate"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetLocalDate(out LocalDate value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="LocalDate"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a LocalDate representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="LocalDate"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public LocalDate GetLocalDate()
    {
        if (!TryGetLocalDate(out LocalDate value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="OffsetTime"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a OffsetTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="OffsetTime"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetOffsetTime(out OffsetTime value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="OffsetTime"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a OffsetTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="OffsetTime"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public OffsetTime GetOffsetTime()
    {
        if (!TryGetOffsetTime(out OffsetTime value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="OffsetDateTime"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a OffsetDateTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="OffsetDateTime"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetOffsetDateTime(out OffsetDateTime value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="OffsetDate"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a OffsetDate representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="OffsetDate"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public readonly bool TryGetOffsetDate(out OffsetDate value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="OffsetDate"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a OffsetDate representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="OffsetDate"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public readonly OffsetDate GetOffsetDate()
    {
        if (!TryGetOffsetDate(out OffsetDate value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="OffsetDateTime"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a OffsetDateTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="OffsetDateTime"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public OffsetDateTime GetOffsetDateTime()
    {
        if (!TryGetOffsetDateTime(out OffsetDateTime value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="Period"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a Period representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="Period"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetPeriod(out Period value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="Period"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a Period representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="Period"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public Period GetPeriod()
    {
        if (!TryGetPeriod(out Period value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a DateTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="DateTime"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetDateTime(out DateTime value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="DateTime"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a DateTime representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="DateTime"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTime"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public DateTime GetDateTime()
    {
        if (!TryGetDateTime(out DateTime value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a DateTimeOffset representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="DateTimeOffset"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetDateTimeOffset(out DateTimeOffset value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a DateTimeOffset representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="DateTimeOffset"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="DateTimeOffset"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public DateTimeOffset GetDateTimeOffset()
    {
        if (!TryGetDateTimeOffset(out DateTimeOffset value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    /// <summary>
    /// Attempts to represent the current JSON string as a <see cref="Guid"/>.
    /// </summary>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    /// This method does not create a Guid representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if the string can be represented as a <see cref="Guid"/>,
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetGuid(out Guid value)
    {
        CheckValidInstance();

        return _parent.TryGetValue(_idx, out value);
    }

    /// <summary>
    /// Gets the value of the element as a <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// This method does not create a Guid representation of values other than JSON strings.
    /// </remarks>
    /// <returns>The value of the element as a <see cref="Guid"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The value cannot be represented as a <see cref="Guid"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <seealso cref="ToString"/>
    public Guid GetGuid()
    {
        if (!TryGetGuid(out Guid value))
        {
            ThrowHelper.ThrowFormatException();
        }

        return value;
    }

    internal string GetPropertyName()
    {
        CheckValidInstance();

        return _parent.GetNameOfPropertyValue(_idx);
    }

    internal ReadOnlySpan<byte> GetPropertyNameRaw()
    {
        CheckValidInstance();

        return _parent.GetPropertyNameRaw(_idx);
    }

    /// <summary>
    /// Gets the original input data backing this value, returning it as a <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// The original input data backing this value, returning it as a <see cref="string"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <remarks>Note that this method allocates.</remarks>
    public string GetRawText()
    {
        CheckValidInstance();

        return _parent.GetRawValueAsString(_idx);
    }

    internal bool ValueIsEscaped
    {
        get
        {
            CheckValidInstance();

            return _parent.ValueIsEscaped(_idx, isPropertyName: false);
        }
    }

    internal ReadOnlySpan<byte> ValueSpan
    {
        get
        {
            CheckValidInstance();

            return _parent.GetRawValue(_idx, includeQuotes: false).Span;
        }
    }

    /// <summary>
    /// Ensures that a fast-lookup property map is created for this element.
    /// </summary>
    /// <remarks>
    /// This enables dictionary-based lookup of property values in the element.
    /// If the cost of lookups exceeds the cost of building the map, this can
    /// provide substantial performance improvements. It is a zero-allocation
    /// operation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsurePropertyMap()
    {
        CheckValidInstance();
        _parent.EnsurePropertyMap(_idx);
    }

    /// <summary>
    /// Compares <paramref name="text" /> to the string value of this element.
    /// </summary>
    /// <param name="text">The text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
    /// <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
    /// the result of calling <see cref="GetString" />, but avoids creating the string instance.
    /// </remarks>
    public bool ValueEquals(string? text)
    {
        // CheckValidInstance is done in the helper
        if (TokenType == JsonTokenType.Null)
        {
            return text == null;
        }

        return TextEqualsHelper(text.AsSpan(), isPropertyName: false);
    }

    /// <summary>
    /// Compares the text represented by <paramref name="utf8Text" /> to the string value of this element.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the string value of this element has the same UTF-8 encoding as
    /// <paramref name="utf8Text" />, <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of the string produced by UTF-8 decoding
    /// <paramref name="utf8Text" /> with the result of calling <see cref="GetString" />, but avoids creating the
    /// string instances.
    /// </remarks>
    public bool ValueEquals(ReadOnlySpan<byte> utf8Text)
    {
        // CheckValidInstance is done in the helper
        if (TokenType == JsonTokenType.Null)
        {
            // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
            return utf8Text.Slice(0, 0) == default;
#pragma warning restore CA2265
        }

        return TextEqualsHelper(utf8Text, isPropertyName: false, shouldUnescape: true);
    }

    /// <summary>
    /// Compares <paramref name="text" /> to the string value of this element.
    /// </summary>
    /// <param name="text">The text to compare against.</param>
    /// <returns>
    /// <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
    /// <see langword="false" /> otherwise.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
    /// </exception>
    /// <remarks>
    /// This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
    /// the result of calling <see cref="GetString" />, but avoids creating the string instance.
    /// </remarks>
    public bool ValueEquals(ReadOnlySpan<char> text)
    {
        // CheckValidInstance is done in the helper
        if (TokenType == JsonTokenType.Null)
        {
            // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
            return text.Slice(0, 0) == default;
#pragma warning restore CA2265
        }

        return TextEqualsHelper(text, isPropertyName: false);
    }

    internal bool TextEqualsHelper(ReadOnlySpan<byte> utf8Text, bool isPropertyName, bool shouldUnescape)
    {
        CheckValidInstance();

        return _parent.TextEquals(_idx, utf8Text, isPropertyName, shouldUnescape);
    }

    internal bool TextEqualsHelper(ReadOnlySpan<char> text, bool isPropertyName)
    {
        CheckValidInstance();

        return _parent.TextEquals(_idx, text, isPropertyName);
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
        ArgumentNullException.ThrowIfNull(writer);

        CheckValidInstance();

        _parent.WriteElementTo(_idx, writer);
    }

    /// <summary>
    /// Get an enumerator to enumerate the values in the JSON array represented by this JsonElement.
    /// </summary>
    /// <returns>
    /// An enumerator to enumerate the values in the JSON array represented by this JsonElement.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public ArrayEnumerator<JsonElement> EnumerateArray()
    {
        CheckValidInstance();

        JsonTokenType tokenType = TokenType;

        if (tokenType != JsonTokenType.StartArray)
        {
            ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, tokenType);
        }

        return new ArrayEnumerator<JsonElement>(_parent, _idx);
    }

    /// <summary>
    /// Get an enumerator to enumerate the properties in the JSON object represented by this JsonElement.
    /// </summary>
    /// <returns>
    /// An enumerator to enumerate the properties in the JSON object represented by this JsonElement.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    [CLSCompliant(false)]
    public ObjectEnumerator<JsonElement> EnumerateObject()
    {
        CheckValidInstance();

        JsonTokenType tokenType = TokenType;

        if (tokenType != JsonTokenType.StartObject)
        {
            ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, tokenType);
        }

        return new ObjectEnumerator<JsonElement>(_parent, _idx);
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

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_parent is null)
        {
            return 0;
        }

        return _parent.GetHashCode(_idx);
    }

    /// <summary>
    /// Get a JsonElement which can be safely stored beyond the lifetime of the
    /// original <see cref="JsonDocument"/>.
    /// </summary>
    /// <returns>
    /// A JsonElement which can be safely stored beyond the lifetime of the
    /// original <see cref="JsonDocument"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If this JsonElement is itself the output of a previous call to Clone, or
    /// a value contained within another JsonElement which was the output of a previous
    /// call to Clone, this method results in no additional memory allocation.
    /// </para>
    /// </remarks>
    public JsonElement Clone()
    {
        CheckValidInstance();

        if (!_parent.IsDisposable)
        {
            return this;
        }

        return _parent.CloneElement(_idx);
    }

    private void CheckValidInstance()
    {
        if (_parent == null)
        {
            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    void IJsonElement.CheckValidInstance() => CheckValidInstance();

#if NET
    /// <inheritdoc/>
    static JsonElement IJsonElement<JsonElement>.CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => new(parentDocument, parentDocumentIndex);

#endif

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        CheckValidInstance();

        return _parent.ToString(_idx, format, formatProvider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        CheckValidInstance();

        return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        CheckValidInstance();

        return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
    }

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"JsonElement: ValueKind = {ValueKind} : \"{ToString()}\"";

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IJsonDocument IJsonElement.ParentDocument => _parent;

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int IJsonElement.ParentDocumentIndex => _idx;

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonTokenType IJsonElement.TokenType => TokenType;

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    JsonValueKind IJsonElement.ValueKind => ValueKind;
}