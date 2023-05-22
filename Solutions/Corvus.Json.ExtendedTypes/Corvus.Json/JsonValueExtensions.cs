// <copyright file="JsonValueExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
                JsonValueKind.Object => T.FromObject(new JsonObject(value.AsObject.AsImmutableDictionary())),
                JsonValueKind.Array => T.FromArray(new JsonArray(value.AsArray.AsImmutableList())),
                JsonValueKind.Number => T.FromNumber(new JsonNumber((double)value.AsNumber)),
                JsonValueKind.String => T.FromString(new JsonString((string)value.AsString)),
                JsonValueKind.True => T.FromBoolean(new JsonBoolean(true)),
                JsonValueKind.False => T.FromBoolean(new JsonBoolean(false)),
                JsonValueKind.Null => T.Null,
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
            return T.FromJson(value.AsJsonElement);
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

        return parser(jsonValue.AsSpan(), state, out result);
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
    }
}