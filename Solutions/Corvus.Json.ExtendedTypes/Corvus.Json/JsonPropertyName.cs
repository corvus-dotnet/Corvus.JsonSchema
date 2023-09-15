// <copyright file="JsonPropertyName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Json;

/// <summary>
/// A JSON property name.
/// </summary>
/// <param name="Name">The name of the property.</param>
public readonly record struct JsonPropertyName(JsonString Name) : IEquatable<JsonString>
{
    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator string(in JsonPropertyName value)
    {
        return value.Name;
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonPropertyName(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonString(in JsonPropertyName value)
    {
        return value.Name;
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonPropertyName(in JsonString value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonPropertyName(ReadOnlySpan<char> value)
    {
        return new(value.ToString());
    }

    /// <summary>
    /// Conversion from utf8 bytes.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator JsonPropertyName(ReadOnlySpan<byte> value)
    {
        return new(Encoding.UTF8.GetString(value));
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="value">The value with which to compare.</param>
    /// <returns><c>True</c> is the values are equal.</returns>
    public bool Equals(string value)
    {
        return this.Name.EqualsString(value);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="value">The value with which to compare.</param>
    /// <returns><c>True</c> is the values are equal.</returns>
    public bool Equals(ReadOnlySpan<char> value)
    {
        return this.Name.EqualsString(value);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return (string)this.Name;
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="value">The value with which to compare.</param>
    /// <returns><c>True</c> is the values are equal.</returns>
    public bool Equals(JsonString value)
    {
        return this.Name.Equals(value);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="value">The value with which to compare.</param>
    /// <returns><c>True</c> is the values are equal.</returns>
    public bool Equals(ReadOnlySpan<byte> value)
    {
        return this.Name.EqualsUtf8Bytes(value);
    }
}