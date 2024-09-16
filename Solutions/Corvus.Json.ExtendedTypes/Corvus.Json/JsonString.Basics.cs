﻿// <copyright file="JsonString.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a Json string value.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonString>))]
public readonly partial struct JsonString
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonString"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonString(string value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonString"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonString(in ReadOnlySpan<char> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonString"/> struct.
    /// </summary>
    /// <param name="utf8Value">The value from which to construct the instance.</param>
    public JsonString(in ReadOnlySpan<byte> utf8Value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
#if NET8_0_OR_GREATER
        this.stringBacking = Encoding.UTF8.GetString(utf8Value);
#else
        byte[] bytes = ArrayPool<byte>.Shared.Rent(utf8Value.Length);

        try
        {
            utf8Value.CopyTo(bytes);
            this.stringBacking = Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
#endif
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonString value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonString(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonString(JsonAny value)
    {
        return value.As<JsonString>();
    }

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static explicit operator string(JsonString value)
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
    /// Gets the string value.
    /// </summary>
    /// <returns><c>The string if this value represents a string</c>, otherwise <c>null</c>.</returns>
    public string? GetString()
    {
        if (this.TryGetString(out string? value))
        {
            return value;
        }

        return null;
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

    /// <summary>
    /// Compare to a sequence of characters.
    /// </summary>
    /// <param name="utf8Bytes">The UTF8-encoded character sequence to compare.</param>
    /// <returns><c>True</c> if teh sequences match.</returns>
    public bool EqualsUtf8Bytes(ReadOnlySpan<byte> utf8Bytes)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
            {
                return this.jsonElementBacking.ValueEquals(utf8Bytes);
            }
        }

        if ((this.backing & Backing.String) != 0)
        {
            int maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Bytes.Length);
#if NET8_0_OR_GREATER
            char[]? pooledChars = null;

            Span<char> chars = maxCharCount <= JsonConstants.StackallocThreshold ?
                stackalloc char[maxCharCount] :
                (pooledChars = ArrayPool<char>.Shared.Rent(maxCharCount));

            try
            {
                int written = Encoding.UTF8.GetChars(utf8Bytes, chars);
                return chars[..written].SequenceEqual(this.stringBacking);
            }
            finally
            {
                if (pooledChars is char[] pc)
                {
                    ArrayPool<char>.Shared.Return(pc);
                }
            }
#else
            char[] chars = ArrayPool<char>.Shared.Rent(maxCharCount);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(utf8Bytes.Length);
            utf8Bytes.CopyTo(bytes);

            try
            {
                int written = Encoding.UTF8.GetChars(bytes, 0, bytes.Length, chars, 0);
                return chars.SequenceEqual(this.stringBacking);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chars);
                ArrayPool<byte>.Shared.Return(bytes);
            }
#endif
        }

        return false;
    }

    /// <summary>
    /// Compare to a sequence of characters.
    /// </summary>
    /// <param name="chars">The character sequence to compare.</param>
    /// <returns><c>True</c> if teh sequences match.</returns>
    public bool EqualsString(string chars)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
            {
                return this.jsonElementBacking.ValueEquals(chars);
            }

            return false;
        }

        if ((this.backing & Backing.String) != 0)
        {
            return chars.Equals(this.stringBacking, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Compare to a sequence of characters.
    /// </summary>
    /// <param name="chars">The character sequence to compare.</param>
    /// <returns><c>True</c> if teh sequences match.</returns>
    public bool EqualsString(ReadOnlySpan<char> chars)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
            {
                return this.jsonElementBacking.ValueEquals(chars);
            }

            return false;
        }

        if ((this.backing & Backing.String) != 0)
        {
#if NET8_0_OR_GREATER
            return chars.SequenceEqual(this.stringBacking);
#else
            return chars.SequenceEqual(this.stringBacking.AsSpan());
#endif
        }

        return false;
    }
}