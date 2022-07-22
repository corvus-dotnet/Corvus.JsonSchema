// <copyright file="JsonValueExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
            writtenChars.Clear();
            ArrayPool<char>.Shared.Return(pooledChars);
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
}