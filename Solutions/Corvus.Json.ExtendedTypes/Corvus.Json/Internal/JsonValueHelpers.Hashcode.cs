﻿// <copyright file="JsonValueHelpers.Hashcode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    private static readonly int NullHashCode = CreateNullHashCode();
    private static readonly int UndefinedHashCode = CreateUndefinedHashCode();

    /// <summary>
    /// Gets the hash code for a JSON value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The hash code for the value.</returns>
    public static int GetHashCode<T>(in T value)
        where T : struct, IJsonValue<T>
    {
        JsonValueKind valueKind = value.ValueKind;
        return valueKind switch
        {
            JsonValueKind.Array => GetArrayHashCode(value.AsArray),
            JsonValueKind.Object => GetObjectHashCode(value.AsObject),
            JsonValueKind.Number => GetHashCodeForNumber(value.AsNumber),
            JsonValueKind.String => GetHashCodeForString(value.AsString),
            JsonValueKind.True => true.GetHashCode(),
            JsonValueKind.False => false.GetHashCode(),
            JsonValueKind.Null => NullHashCode,
            _ => UndefinedHashCode,
        };
    }

    /// <summary>
    /// Gets the hash code for a JSON value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The hash code for the value.</returns>
    public static int GetHashCode(in JsonAny value)
    {
        JsonValueKind valueKind = value.ValueKind;
        return valueKind switch
        {
            JsonValueKind.Array => GetArrayHashCode(value.AsArray),
            JsonValueKind.Object => GetObjectHashCode(value.AsObject),
            JsonValueKind.Number => GetHashCodeForNumber(value.AsNumber),
            JsonValueKind.String => GetHashCodeForString(value.AsString),
            JsonValueKind.True => true.GetHashCode(),
            JsonValueKind.False => false.GetHashCode(),
            JsonValueKind.Null => NullHashCode,
            _ => UndefinedHashCode,
        };
    }

    /// <summary>
    /// Gets the HashCode for an array.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The hashcode for the value.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    public static int GetArrayHashCode<T>(in T value)
        where T : struct, IJsonArray<T>
    {
        HashCode hash = default;

        foreach (JsonAny item in value.EnumerateArray())
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Gets the HashCode for an object.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The hashcode for the value.</returns>
    /// <exception cref="InvalidOperationException">The value was not an object.</exception>
    public static int GetObjectHashCode<T>(in T value)
        where T : struct, IJsonObject<T>
    {
        HashCode hash = default;

        // We may want to use a different comparer if the internal implementation
        // of JsonPropertyName changes.
        ImmutableArray<JsonObjectProperty> sortedProperties =
                value.EnumerateObject()
                    .ToImmutableArray()
                    .Sort((x, y) => x.Name.CompareTo(y.Name));

        foreach (JsonObjectProperty item in sortedProperties)
        {
            hash.Add(item.GetHashCode());
        }

        return hash.ToHashCode();
    }

    private static int CreateNullHashCode()
    {
        HashCode code = default;
        code.Add((object?)null);
        return code.ToHashCode();
    }

    private static int CreateUndefinedHashCode()
    {
        HashCode code = default;

        // We'll pick a random value and use it as our undefined hashcode.
        code.Add(Guid.NewGuid());
        return code.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHashCodeForString<T>(in T value)
    where T : struct, IJsonString<T>
    {
#if NET8_0_OR_GREATER
        if (value.TryGetValue(ProcessHashCode, (object?)null, out int hashCode))
        {
            return hashCode;
        }

        return UndefinedHashCode;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ProcessHashCode(ReadOnlySpan<char> span, in object? state, out int value)
        {
            value = string.GetHashCode(span);
            return true;
        }
#else
        return value.GetString().GetHashCode();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHashCodeForNumber<T>(in T value)
        where T : struct, IJsonNumber<T>
    {
        if (value.HasJsonElementBacking)
        {
            // We get a double if we can, otherwise we fall back to a decimal.
            if (value.AsJsonElement.TryGetDouble(out double result1))
            {
                return result1.GetHashCode();
            }

            if (value.AsJsonElement.TryGetDecimal(out decimal result2))
            {
                return result2.GetHashCode();
            }
        }

        // This has the same double if possible, then decimal semantics.
        return value.AsBinaryJsonNumber.GetHashCode();
    }
}