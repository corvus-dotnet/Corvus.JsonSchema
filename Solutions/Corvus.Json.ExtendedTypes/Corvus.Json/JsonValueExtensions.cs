// <copyright file="JsonValueExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Extension methods for <see cref="IJsonValue"/> instances.
/// </summary>
public static class JsonValueExtensions
{
    /// <summary>
    /// Gets a property.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonObject{T}"/> from which to get the property.</typeparam>
    /// <typeparam name="TValue">The type of the result.</typeparam>
    /// <typeparam name="TString">The type of the string containing the name.</typeparam>
    /// <param name="jsonObject">The instance of the <see cref="IJsonObject{T}"/> from which to get the property.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="property">The resulting property, if any.</param>
    /// <returns><see langword="true"/> if the property exists.</returns>
    public static bool TryGetProperty<T, TValue, TString>(this T jsonObject, in TString name, out TValue property)
        where T : struct, IJsonObject<T>
        where TValue : struct, IJsonValue<TValue>
        where TString : struct, IJsonString<TString>
    {
        Debug.Assert(name.IsValid(), $"The string must be a valid {name.GetType().Name}");

        if (name.HasDotnetBacking)
        {
#if NET8_0_OR_GREATER
            return jsonObject.TryGetProperty((string)name, out property);
#else
            return jsonObject.TryGetProperty((string)name.AsString, out property);
#endif
        }
        else
        {
            if (jsonObject.HasDotnetBacking)
            {
                return name.TryGetValue(TryGetString, jsonObject, out property);
            }
            else
            {
                return name.TryGetValue(TryGetStringUtf8, jsonObject, out property);
            }
        }

        static bool TryGetString(ReadOnlySpan<char> span, in T state, out TValue value)
        {
            return state.TryGetProperty(span, out value);
        }

        static bool TryGetStringUtf8(ReadOnlySpan<char> span, in T state, out TValue value)
        {
            return state.TryGetProperty(span, out value);
        }
    }

    /// <summary>
    /// Clones an <see cref="IJsonValue"/> to enable it to be
    /// used safely outside of its construction context.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be cloned.</typeparam>
    /// <param name="value">The value to be cloned.</param>
    /// <returns>An instance of the value that is safe to be used detached from its previous context.</returns>
    public static TValue Clone<TValue>(this TValue value)
        where TValue : struct, IJsonValue<TValue>
    {
        if (value.HasJsonElementBacking)
        {
#if NET8_0_OR_GREATER
            return TValue.FromJson(value.AsJsonElement.Clone());
#else
            return JsonValueNetStandard20Extensions.FromJsonElement<TValue>(value.AsJsonElement.Clone());
#endif
        }

        return value;
    }

    /// <summary>
    /// Serialize the entity to a string.
    /// </summary>
    /// <typeparam name="TValue">The type of <see cref="IJsonValue"/>.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A string representation fo the value.</returns>
    public static string Serialize<TValue>(this TValue value)
        where TValue : struct, IJsonValue
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        value.WriteTo(writer);
        writer.Flush();

#if NET8_0_OR_GREATER
        int length = Encoding.UTF8.GetMaxCharCount(abw.WrittenCount);
        char[]? pooledChars = null;

        Span<char> chars = length <= JsonValueHelpers.MaxStackAlloc ?
            stackalloc char[length] :
            (pooledChars = ArrayPool<char>.Shared.Rent(length));

        int count = Encoding.UTF8.GetChars(abw.WrittenSpan, chars);

        Span<char> writtenChars = chars[..count];
        string result = new(writtenChars);

        if (pooledChars != null)
        {
            ArrayPool<char>.Shared.Return(pooledChars, true);
        }

        return result;
#else
        int length = Encoding.UTF8.GetMaxCharCount(abw.WrittenCount);
        char[] chars = ArrayPool<char>.Shared.Rent(length);

        int count = Encoding.UTF8.GetChars(abw.WrittenArray, 0, abw.WrittenCount, chars, 0);

        string result = new(chars, 0, count);

        ArrayPool<char>.Shared.Return(chars, true);

        return result;
#endif
    }

    /// <summary>
    /// Gets a value indicating whether this value is null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNull<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.ValueKind == JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUndefined<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.ValueKind == JsonValueKind.Undefined;
    }

    /// <summary>
    /// Gets a value indicating whether this value is not null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is not null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotNull<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.ValueKind != JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is not undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is not undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotUndefined<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    /// Gets a value indicating whether this value is null or undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrUndefined<T>(this T value)
        where T : struct, IJsonValue
    {
        JsonValueKind kind = value.ValueKind;
        return kind == JsonValueKind.Undefined || kind == JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is neither null nor undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is neither null nor undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotNullOrUndefined<T>(this T value)
        where T : struct, IJsonValue
    {
        JsonValueKind kind = value.ValueKind;
        return kind != JsonValueKind.Undefined && kind != JsonValueKind.Null;
    }

    /// <summary>
    /// Gets a nullable instance of the value.
    /// </summary>
    /// <typeparam name="T">The type of the value for wich to get a nullable instance.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>null</c> if the value is null, or undefined. Otherwise an instance of the value.</returns>
    public static T? AsOptional<T>(this T value)
        where T : struct, IJsonValue<T>
    {
        return value.IsNullOrUndefined() ? null : value;
    }

    /// <summary>
    /// Gets a value indicating whether the value is valid.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <returns><c>True</c> if the value is a valid instance of the type.</returns>
    public static bool IsValid<T>(this T value)
        where T : struct, IJsonValue
    {
        return value.Validate(ValidationContext.ValidContext).IsValid;
    }

    /// <summary>
    /// Gets the instance as a dotnet backed value.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>An instance of the given value, backed by a dotnet value rather than a JsonElement.</returns>
    public static T AsDotnetBackedValue<T>(this T value)
        where T : struct, IJsonValue<T>
    {
        if (value.HasJsonElementBacking)
        {
            JsonValueKind valueKind = value.ValueKind;

            return valueKind switch
            {
#if NET8_0_OR_GREATER
                JsonValueKind.Object => T.FromObject(new JsonObject(value.AsObject.AsPropertyBacking())),
                JsonValueKind.Array => T.FromArray(new JsonArray(value.AsArray.AsImmutableList())),
                JsonValueKind.Number => T.FromNumber(new JsonNumber(value.AsNumber.AsBinaryJsonNumber)),
                JsonValueKind.String => T.FromString(new JsonString((string)value.AsString)),
                JsonValueKind.True => T.FromBoolean(new JsonBoolean(true)),
                JsonValueKind.False => T.FromBoolean(new JsonBoolean(false)),
                JsonValueKind.Null => T.Null,
#else
                JsonValueKind.Object => new JsonAny(value.AsObject.AsPropertyBacking()).As<T>(),
                JsonValueKind.Array => new JsonAny(value.AsArray.AsImmutableList()).As<T>(),
                JsonValueKind.Number => new JsonAny(value.AsNumber.AsBinaryJsonNumber).As<T>(),
                JsonValueKind.String => new JsonAny((string)value.AsString).As<T>(),
                JsonValueKind.True => new JsonAny(true).As<T>(),
                JsonValueKind.False => new JsonAny(false).As<T>(),
                JsonValueKind.Null => JsonAny.Null.As<T>(),
#endif
                _ => value,
            };
        }

        return value;
    }

    /// <summary>
    /// Gets the instance as a dotnet backed value.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>An instance of the given value, backed by a dotnet value rather than a JsonElement.</returns>
    public static T AsJsonElementBackedValue<T>(this T value)
        where T : struct, IJsonValue<T>
    {
        if (!value.HasJsonElementBacking)
        {
#if NET8_0_OR_GREATER
            return T.FromJson(value.AsJsonElement);
#else
            return JsonValueNetStandard20Extensions.FromJsonElement<T>(value.AsJsonElement);
#endif
        }

        return value;
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonString{T}"/> to parse.</typeparam>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="jsonValue">The instance of the <see cref="IJsonString{T}"/> to parse.</param>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public static bool TryGetValue<T, TState, TResult>(this T jsonValue, Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? result)
        where T : struct, IJsonString<T>
    {
        if (jsonValue.HasJsonElementBacking)
        {
            return jsonValue.AsJsonElement.TryGetValue(parser, state, out result);
        }

#if NET8_0_OR_GREATER
        return parser(((string)jsonValue).AsSpan(), state, out result);
#else
        return parser(((string)jsonValue.AsString).AsSpan(), state, out result);
#endif
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonString{T}"/> to parse.</typeparam>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="jsonValue">The instance of the <see cref="IJsonString{T}"/> to parse.</param>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public static bool TryGetValue<T, TState, TResult>(this T jsonValue, Utf8Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? result)
        where T : struct, IJsonString<T>
    {
        return TryGetValue(jsonValue, parser, state, true, out result);
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonString{T}"/> to parse.</typeparam>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="jsonValue">The instance of the <see cref="IJsonString{T}"/> to parse.</param>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="decode">Determines whether to decode the UTF8 bytes.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public static bool TryGetValue<T, TState, TResult>(this T jsonValue, Utf8Parser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? result)
        where T : struct, IJsonString<T>
    {
        if (jsonValue.HasJsonElementBacking)
        {
            return jsonValue.AsJsonElement.TryGetValue(parser, state, decode, out result);
        }

        if (!jsonValue.TryGetString(out string? value))
        {
            result = default;
            return false;
        }

#if NET8_0_OR_GREATER
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[]? pooledBytes = null;

        Span<byte> bytes = maxByteCount <= JsonConstants.StackallocThreshold ?
            stackalloc byte[maxByteCount] :
            (pooledBytes = ArrayPool<byte>.Shared.Rent(maxByteCount));

        int written = Encoding.UTF8.GetBytes(value, bytes);

        bool success = parser(bytes[..written], state, out result);

        if (pooledBytes is byte[] pb)
        {
            ArrayPool<byte>.Shared.Return(pb);
        }

        return success;
#else
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(maxByteCount);

        int written = Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);

        bool success = parser(bytes.AsSpan(0, written), state, out result);

        ArrayPool<byte>.Shared.Return(bytes);

        return success;
#endif
    }
}