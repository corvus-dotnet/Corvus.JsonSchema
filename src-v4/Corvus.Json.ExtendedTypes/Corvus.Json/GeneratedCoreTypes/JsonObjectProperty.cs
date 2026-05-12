// <copyright file="JsonObjectProperty.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// A property on a <see cref="IJsonObject{T}"/>.
/// </summary>
public readonly struct JsonObjectProperty
{
    private readonly Backing backing;
    private readonly JsonProperty jsonProperty;
    private readonly JsonPropertyName name;
    private readonly JsonAny value;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectProperty"/> struct.
    /// </summary>
    /// <param name="jsonProperty">The JSON property over which to construct this instance.</param>
    public JsonObjectProperty(in JsonProperty jsonProperty)
    {
        this.backing = Backing.JsonProperty;
        this.jsonProperty = jsonProperty;
        this.name = default;
        this.value = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectProperty"/> struct.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    public JsonObjectProperty(in JsonPropertyName name, in JsonAny value)
    {
        this.backing = Backing.NameValue;
        this.jsonProperty = default;
        this.name = name;
        this.value = value;
    }

    [Flags]
    private enum Backing : byte
    {
        Undefined = 0b00,
        JsonProperty = 0b01,
        NameValue = 0b10,
    }

    /// <summary>
    /// Gets the value kind of the property value.
    /// </summary>
    public JsonValueKind ValueKind
    {
        get
        {
            if ((this.backing & Backing.JsonProperty) != 0)
            {
                return this.jsonProperty.Value.ValueKind;
            }

            if ((this.backing & Backing.NameValue) != 0)
            {
                return this.value.ValueKind;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <summary>
    /// Gets the value of the property.
    /// </summary>
    public JsonAny Value
    {
        get
        {
            if ((this.backing & Backing.JsonProperty) != 0)
            {
                return new(this.jsonProperty.Value);
            }

            if ((this.backing & Backing.NameValue) != 0)
            {
                return this.value;
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the name of the property as a string.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value does not have a name.</exception>
    public JsonPropertyName Name
    {
        get
        {
            if ((this.backing & Backing.JsonProperty) != 0)
            {
                return new(this.jsonProperty.Name);
            }

            if ((this.backing & Backing.NameValue) != 0)
            {
                return this.name;
            }

            throw new InvalidOperationException("The property does not have a name.");
        }
    }

    /// <summary>
    /// Standard equality operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns>True if they are equal.</returns>
    public static bool operator ==(in JsonObjectProperty left, in JsonObjectProperty right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Standard inequality operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns>True if they are not equal.</returns>
    public static bool operator !=(in JsonObjectProperty left, in JsonObjectProperty right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Gets the value as an instance of the given type.
    /// </summary>
    /// <typeparam name="T">The type for which to get the value.</typeparam>
    /// <returns>An instance of the value as the given type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueAs<T>()
        where T : struct, IJsonValue<T>
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
#if NET8_0_OR_GREATER
            return T.FromJson(this.jsonProperty.Value);
#else
            return JsonValueNetStandard20Extensions.FromJsonElement<T>(this.jsonProperty.Value);
#endif
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
#if NET8_0_OR_GREATER
            return T.FromAny(this.value);
#else
            return this.value.As<T>();
#endif
        }

        return default;
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetName<TState, TResult>(in Utf8Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        return this.name.TryGetValue(parser, state, out value);
    }

    /// <summary>
    ///   Attempts to represent the current JSON string as the given type.
    /// </summary>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <typeparam name="TResult">The type with which to represent the JSON string.</typeparam>
    /// <param name="parser">A delegate to the method that parses the JSON string.</param>
    /// <param name="state">The state for the parser.</param>
    /// <param name="value">Receives the value.</param>
    /// <remarks>
    ///   This method does not create a representation of values other than JSON strings.
    /// </remarks>
    /// <returns>
    ///   <see langword="true"/> if the string can be represented as the given type,
    ///   <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    ///   The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    public bool TryGetName<TState, TResult>(in Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? value)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
#if NET8_0_OR_GREATER
            return parser(this.jsonProperty.Name, state, out value);
#else
            return parser(this.jsonProperty.Name.AsSpan(), state, out value);
#endif
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.TryGetValue(parser, state, out value);
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Compares the specified UTF-8 encoded text to the name of this property.
    /// </summary>
    /// <param name="utf8Name">The name to match as a UTF8 string.</param>
    /// <param name="name">The name to match as a string.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameEquals(in ReadOnlySpan<byte> utf8Name, string name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            return this.jsonProperty.NameEquals(utf8Name);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.Equals(utf8Name, name);
        }

        return false;
    }

    /// <summary>
    /// Compares the specified UTF-8 encoded text to the name of this property.
    /// </summary>
    /// <param name="utf8Name">The name to match.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    public bool NameEquals(ReadOnlySpan<byte> utf8Name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            return this.jsonProperty.NameEquals(utf8Name);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.EqualsUtf8String(utf8Name);
        }

        return false;
    }

    /// <summary>
    /// Compares the specified text to the name of this property.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    public bool NameEquals(ReadOnlySpan<char> name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            return this.jsonProperty.NameEquals(name);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.EqualsString(name);
        }

        return false;
    }

    /// <summary>
    /// Compares the specified text to the name of this property.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    public bool NameEquals(in JsonPropertyName name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            return name.EqualsPropertyNameOf(this.jsonProperty);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.Equals(name);
        }

        return false;
    }

    /// <summary>
    /// Compares the specified text to the name of this property.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    public bool NameEquals(in JsonString name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            if (name.TryGetValue(NameEquals, this.jsonProperty, out bool result))
            {
                return result;
            }

            return false;
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.EqualsJsonString(name);
        }

        return false;

        static bool NameEquals(ReadOnlySpan<char> span, in JsonProperty state, out bool result)
        {
            result = state.NameEquals(span);
            return true;
        }
    }

    /// <summary>
    /// Compares the specified text to the name of this property.
    /// </summary>
    /// <param name="name">The name to match.</param>
    /// <returns><c>True</c> if the name matches.</returns>
    public bool NameEquals(string name)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            return this.jsonProperty.NameEquals(name);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            return this.name.Equals(name);
        }

        return false;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is JsonObjectProperty property && this.Equals(property);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other item with which to compare.</param>
    /// <returns><see langword="true"/> if the two objects are equal.</returns>
    public bool Equals(in JsonObjectProperty other)
    {
        return this.Value.Equals(other.Value) &&
               this.Name.Equals(other.Name);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.Value, this.Name);
    }

    /// <summary>
    /// Writes the property to a JSON object writer.
    /// </summary>
    /// <param name="writer">The writer to which to write the property.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if ((this.backing & Backing.JsonProperty) != 0)
        {
            this.jsonProperty.WriteTo(writer);
        }

        if ((this.backing & Backing.NameValue) != 0)
        {
            this.name.WriteTo(writer);
            this.value.WriteTo(writer);
        }
    }
}