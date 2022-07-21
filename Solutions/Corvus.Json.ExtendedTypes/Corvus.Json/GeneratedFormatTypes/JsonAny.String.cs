// <copyright file="JsonAny.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
public readonly partial struct JsonAny : IJsonString<JsonAny>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(string value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(in ReadOnlySpan<char> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value.ToString();
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="utf8Value">The value from which to construct the instance.</param>
    public JsonAny(in ReadOnlySpan<byte> utf8Value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = Encoding.UTF8.GetString(utf8Value);
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static implicit operator string(JsonAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            if (value.jsonElementBacking.GetString() is string result)
            {
                return result;
            }

            throw new InvalidOperationException();
        }

        if ((value.backing & Backing.String) != 0)
        {
            return value.stringBacking;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(ReadOnlySpan<char> value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static implicit operator ReadOnlySpan<char>(JsonAny value)
    {
        return ((string)value).AsSpan();
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(ReadOnlySpan<byte> value)
    {
        return new(value);
    }

    /// <inheritdoc/>
    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        if ((this.backing & Backing.String) != 0)
        {
            value = this.stringBacking;
            return true;
        }

        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            value = this.jsonElementBacking.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<char> AsSpan()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return this.stringBacking.AsSpan();
        }

        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return this.jsonElementBacking.GetString().AsSpan();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Gets the string value.
    /// </summary>
    /// <returns><c>The string if this value represents a string</c>, otherwise <c>null</c>.</returns>
    public string? AsOptionalString()
    {
        if (this.TryGetString(out string? value))
        {
            return value;
        }

        return null;
    }
}