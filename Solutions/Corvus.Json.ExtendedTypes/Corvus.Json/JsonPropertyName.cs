// <copyright file="JsonPropertyName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// A JSON property name.
/// </summary>
/// <param name="Name">The name of the property.</param>
public readonly record struct JsonPropertyName(string Name)
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
    public static implicit operator ReadOnlySpan<char>(in JsonPropertyName value)
    {
        return value.Name.AsSpan();
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
    /// Conversion to utf8 bytes.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator ReadOnlySpan<byte>(in JsonPropertyName value)
    {
        return Encoding.UTF8.GetBytes(value.Name);
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
    public bool Equals(ReadOnlySpan<char> value)
    {
        return this.Name.AsSpan().SequenceEqual(value);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="value">The value with which to compare.</param>
    /// <returns><c>True</c> is the values are equal.</returns>
    public bool Equals(string value)
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
        int required = Encoding.UTF8.GetMaxCharCount(value.Length);
        char[]? rentedFromPool = null;
        Span<char> buffer =
            required > JsonValueHelpers.MaxStackAlloc
            ? (rentedFromPool = ArrayPool<char>.Shared.Rent(required))
            : stackalloc char[JsonValueHelpers.MaxStackAlloc];

        try
        {
            int written = Encoding.UTF8.GetChars(value, buffer);
            return this.Name.AsSpan().SequenceEqual(buffer);
        }
        finally
        {
            if (rentedFromPool is char[] rfp)
            {
                // Clear the buffer on return as property names may be security sensitive
                ArrayPool<char>.Shared.Return(rfp, true);
            }
        }
    }
}