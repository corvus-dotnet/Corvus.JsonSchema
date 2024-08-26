// <copyright file="JsonUri.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uri.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonUri>))]
public readonly partial struct JsonUri
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUri"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonUri(string value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUri"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonUri(in ReadOnlySpan<char> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.String;
        this.stringBacking = value.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUri"/> struct.
    /// </summary>
    /// <param name="utf8Value">The value from which to construct the instance.</param>
    public JsonUri(in ReadOnlySpan<byte> utf8Value)
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
    public static implicit operator JsonAny(JsonUri value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUri(JsonAny value)
    {
        return value.As<JsonUri>();
    }

    /// <summary>
    /// Conversion to JsonString.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonString(JsonUri value)
    {
        return value.AsString;
    }

    /// <summary>
    /// Conversion from JsonString.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUri(JsonString value)
    {
        if (value.HasDotnetBacking && value.ValueKind == JsonValueKind.String)
        {
            return new(value.GetString()!);
        }

        return new(value.AsJsonElement);
    }

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUri(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static explicit operator string(JsonUri value)
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
    /// Concatenate two JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2>(Span<byte> buffer, in T1 firstValue, in T2 secondValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate three JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate four JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3, T4>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate five JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3, T4, T5>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate six JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3, T4, T5, T6>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue, sixthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate seven JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3, T4, T5, T6, T7>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue, sixthValue, seventhValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
    }

    /// <summary>
    /// Concatenate eight JSON values, producing an instance of the string type JsonUri.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <typeparam name="T8">The type of the eighth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <param name="eighthValue">The eighth value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonUri Concatenate<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue, in T8 eighthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>
        where T8 : struct, IJsonValue<T8>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue, sixthValue, seventhValue, eighthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonUri>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonUri>(buffer[..written]);
#endif
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
                int written = Encoding.UTF8.GetChars(bytes, 0, utf8Bytes.Length, chars, 0);
                return chars.AsSpan(0, written).SequenceEqual(this.stringBacking.AsSpan());
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