﻿// <copyright file="JsonString.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON string.
/// </summary>
public readonly partial struct JsonString
#if NET8_0_OR_GREATER
    : IJsonString<JsonString>, ISpanFormattable
#else
    : IJsonString<JsonString>
#endif
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly string stringBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonString"/> struct.
    /// </summary>
    public JsonString()
    {
        this.jsonElementBacking = default;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonString"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonString(in JsonElement value)
    {
        this.jsonElementBacking = value;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Gets a Null instance.
    /// </summary>
    public static JsonString Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonString Undefined { get; }

    /// <summary>
    /// Gets a default instance.
    /// </summary>
    public static JsonString DefaultInstance { get; }

    /// <inheritdoc/>
    public JsonAny AsAny
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.String) != 0)
            {
                return new(this.stringBacking);
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonAny.Null;
            }

            return JsonAny.Undefined;
        }
    }

    /// <inheritdoc/>
    public JsonElement AsJsonElement
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking;
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueHelpers.NullElement;
            }

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueHelpers.StringToJsonElement(this.stringBacking);
            }

            return default;
        }
    }

    /// <inheritdoc/>
    public JsonString AsString
    {
        get
        {
            return this;
        }
    }

    /// <inheritdoc/>
    JsonBoolean IJsonValue.AsBoolean
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonNumber IJsonValue.AsNumber
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonObject IJsonValue.AsObject
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonArray IJsonValue.AsArray
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public bool HasJsonElementBacking => (this.backing & Backing.JsonElement) != 0;

    /// <inheritdoc/>
    public bool HasDotnetBacking => (this.backing & Backing.Dotnet) != 0;

    /// <inheritdoc/>
    public JsonValueKind ValueKind
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking.ValueKind;
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueKind.Null;
            }

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueKind.String;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator ==(in JsonString left, in JsonString right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator !=(in JsonString left, in JsonString right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a JsonAny value.
    /// </summary>
    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/>.</returns>
    /// <remarks>The returned value will have a <see cref="IJsonValue.ValueKind"/> of <see cref="JsonValueKind.Undefined"/> if the
    /// value cannot be constructed from the given instance (e.g. because they have an incompatible dotnet backing type.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonString FromAny(in JsonAny value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        JsonValueKind valueKind = value.ValueKind;
        return valueKind switch
        {
            JsonValueKind.String => new((string)value.AsString),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
    /// </summary>
    /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonString FromJson(in JsonElement value)
    {
        return new(value);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a string value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonString FromString<TValue>(in TValue value)
        where TValue : struct, IJsonString<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
#if NET8_0_OR_GREATER
            return new((string)value);
#else
            return new((string)value.AsString);
#endif
        }

        return Undefined;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets an instance of the JSON value from a boolean value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonString IJsonValue<JsonString>.FromBoolean<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from a double value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonString IJsonValue<JsonString>.FromNumber<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from an array value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonString IJsonValue<JsonString>.FromArray<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from an object value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonString IJsonValue<JsonString>.FromObject<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }
#endif

    /// <summary>
    /// Parses a JSON string into a JsonString.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonString"/> instance built from the JSON string.</returns>
    public static JsonString Parse(string json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonString(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonString.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonString"/> instance built from the JSON string.</returns>
    public static JsonString Parse(Stream utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonString(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonString.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonString"/> instance built from the JSON string.</returns>
    public static JsonString Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonString(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonString.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonString"/> instance built from the JSON string.</returns>
    public static JsonString Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonString(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonString.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonString"/> instance built from the JSON string.</returns>
    public static JsonString Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonString(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonString ParseValue(string buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.AsSpan());
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonString ParseValue(ReadOnlySpan<char> buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer);
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonString ParseValue(ReadOnlySpan<byte> buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer);
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonString ParseValue(ref Utf8JsonReader reader)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(ref reader);
#else
        return JsonValueHelpers.ParseValue<JsonString>(ref reader);
#endif
    }

    /// <summary>
    /// Concatenate two JSON values, producing an instance of the string type JsonString.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonString Concatenate<T1, T2>(Span<byte> buffer, in T1 firstValue, in T2 secondValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate three JSON values, producing an instance of the string type JsonString.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <returns>An instance of this string type.</returns>
    public static JsonString Concatenate<T1, T2, T3>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue)
            where T1 : struct, IJsonValue<T1>
            where T2 : struct, IJsonValue<T2>
            where T3 : struct, IJsonValue<T3>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate four JSON values, producing an instance of the string type JsonString.
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
    public static JsonString Concatenate<T1, T2, T3, T4>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate five JSON values, producing an instance of the string type JsonString.
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
    public static JsonString Concatenate<T1, T2, T3, T4, T5>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue)
            where T1 : struct, IJsonValue<T1>
            where T2 : struct, IJsonValue<T2>
            where T3 : struct, IJsonValue<T3>
            where T4 : struct, IJsonValue<T4>
            where T5 : struct, IJsonValue<T5>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate six JSON values, producing an instance of the string type JsonString.
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
    public static JsonString Concatenate<T1, T2, T3, T4, T5, T6>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue)
            where T1 : struct, IJsonValue<T1>
            where T2 : struct, IJsonValue<T2>
            where T3 : struct, IJsonValue<T3>
            where T4 : struct, IJsonValue<T4>
            where T5 : struct, IJsonValue<T5>
            where T6 : struct, IJsonValue<T6>
    {
        int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, firstValue, secondValue, thirdValue, fourthValue, fifthValue, sixthValue);
#if NET8_0_OR_GREATER
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate seven JSON values, producing an instance of the string type JsonString.
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
    public static JsonString Concatenate<T1, T2, T3, T4, T5, T6, T7>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue)
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
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Concatenate eight JSON values, producing an instance of the string type JsonString.
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
    public static JsonString Concatenate<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue, in T8 eighthValue)
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
        return IJsonValue<JsonString>.ParseValue(buffer[..written]);
#else
        return JsonValueHelpers.ParseValue<JsonString>(buffer.Slice(0, written));

#endif
    }

    /// <summary>
    /// Gets the value as an instance of the target value.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target.</typeparam>
    /// <returns>An instance of the target type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TTarget As<TTarget>()
            where TTarget : struct, IJsonValue<TTarget>
    {
#if NET8_0_OR_GREATER
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return TTarget.FromJson(this.jsonElementBacking);
        }

        if ((this.backing & Backing.String) != 0)
        {
            return TTarget.FromString(this);
        }

        if ((this.backing & Backing.Null) != 0)
        {
            return TTarget.Null;
        }

        return TTarget.Undefined;
#else
        return this.As<JsonString, TTarget>();
#endif
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return
            (obj is IJsonValue jv && this.Equals(jv.AsAny)) ||
            (obj is null && this.IsNull());
    }

    /// <inheritdoc/>
    public bool Equals<T>(in T other)
        where T : struct, IJsonValue<T>
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other item with which to compare.</param>
    /// <returns><see langword="true"/> if the values were equal.</returns>
    public bool Equals(in JsonString other)
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <summary>
    /// Compare with a string.
    /// </summary>
    /// <param name="other">The span with which to compare.</param>
    /// <returns><see langword="true"/> if they are equal, otherwise <see langword="false"/>.</returns>
    public bool Equals(ReadOnlySpan<char> other)
    {
        return JsonValueHelpers.CompareWithString(this, other);
    }

    /// <summary>
    /// Compare with a UTF8 string.
    /// </summary>
    /// <param name="other">The span with which to compare.</param>
    /// <returns><see langword="true"/> if they are equal, otherwise <see langword="false"/>.</returns>
    public bool Equals(ReadOnlySpan<byte> other)
    {
        return JsonValueHelpers.CompareWithUtf8Bytes(this, other);
    }

    /// <inheritdoc/>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Undefined)
            {
                this.jsonElementBacking.WriteTo(writer);
            }

            return;
        }

        if ((this.backing & Backing.Null) != 0)
        {
            writer.WriteNullValue();
            return;
        }

        if ((this.backing & Backing.String) != 0)
        {
            writer.WriteStringValue(this.stringBacking);
            return;
        }
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return JsonValueHelpers.GetHashCode(this);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Serialize();
    }

    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
    {
        return Json.Validate.TypeString(this.ValueKind, validationContext, level);
    }

#if NET8_0_OR_GREATER
    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if ((this.backing & Backing.String) != 0)
        {
            int length = Math.Min(destination.Length, this.stringBacking.Length);
            this.stringBacking.AsSpan(0, length).CopyTo(destination);
            charsWritten = length;
            return true;
        }

        if ((this.backing & Backing.JsonElement) != 0)
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(destination.Length);
            try
            {
                bool result = this.jsonElementBacking.TryGetValue(FormatSpan, new Output(buffer, destination.Length), out charsWritten);
                if (result)
                {
                    buffer.AsSpan(0, charsWritten).CopyTo(destination);
                }

                return result;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        charsWritten = 0;
        return false;

        static bool FormatSpan(ReadOnlySpan<char> source, in Output output, out int charsWritten)
        {
            int length = Math.Min(output.Length, source.Length);
            source[..length].CopyTo(output.Destination);
            charsWritten = length;
            return true;
        }
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        // There is no formatting for the string
        return this.ToString();
    }

    private readonly record struct Output(char[] Destination, int Length);
#endif
}