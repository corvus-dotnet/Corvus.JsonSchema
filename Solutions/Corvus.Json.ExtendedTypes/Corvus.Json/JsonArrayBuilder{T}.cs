// <copyright file="JsonArrayBuilder{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Build JSON arrays.
/// </summary>
/// <typeparam name="T">The type of the array to build.</typeparam>
public static class JsonArrayBuilder<T>
    where T : struct, IJsonArray<T>
{
    /// <summary>
    /// Creates an array from the given values.
    /// </summary>
    /// <typeparam name="TValue">The type of the item.</typeparam>
    /// <param name = "value1">The first value from which to construct the instance.</param>
    /// <returns>A T instantiated from the given items.</returns>
    public static T FromItems<TValue>(in TValue value1)
        where TValue : struct, IJsonValue<TValue>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1.AsAny);
        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Creates an array from the given values.
    /// </summary>
    /// <typeparam name="TValue1">The type of the first item.</typeparam>
    /// <typeparam name="TValue2">The type of the second item.</typeparam>
    /// <param name = "value1">The first value from which to construct the instance.</param>
    /// <param name = "value2">The second value from which to construct the instance.</param>
    /// <returns>A T instantiated from the given items.</returns>
    public static T FromItems<TValue1, TValue2>(in TValue1 value1, in TValue2 value2)
        where TValue1 : struct, IJsonValue<TValue1>
        where TValue2 : struct, IJsonValue<TValue2>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1.AsAny);
        builder.Add(value2.AsAny);
        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Creates an array from the given values.
    /// </summary>
    /// <typeparam name="TValue1">The type of the first item.</typeparam>
    /// <typeparam name="TValue2">The type of the second item.</typeparam>
    /// <typeparam name="TValue3">The type of the third item.</typeparam>
    /// <param name = "value1">The first value from which to construct the instance.</param>
    /// <param name = "value2">The second value from which to construct the instance.</param>
    /// <param name = "value3">The third value from which to construct the instance.</param>
    /// <returns>A T instantiated from the given items.</returns>
    public static T FromItems<TValue1, TValue2, TValue3>(in TValue1 value1, in TValue2 value2, in TValue3 value3)
        where TValue1 : struct, IJsonValue<TValue1>
        where TValue2 : struct, IJsonValue<TValue2>
        where TValue3 : struct, IJsonValue<TValue3>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1.AsAny);
        builder.Add(value2.AsAny);
        builder.Add(value3.AsAny);
        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Creates an array from the given values.
    /// </summary>
    /// <typeparam name="TValue1">The type of the first item.</typeparam>
    /// <typeparam name="TValue2">The type of the second item.</typeparam>
    /// <typeparam name="TValue3">The type of the third item.</typeparam>
    /// <typeparam name="TValue4">The type of the fourth item.</typeparam>
    /// <param name = "value1">The first value from which to construct the instance.</param>
    /// <param name = "value2">The second value from which to construct the instance.</param>
    /// <param name = "value3">The third value from which to construct the instance.</param>
    /// <param name = "value4">The fourth value from which to construct the instance.</param>
    /// <returns>A T instantiated from the given items.</returns>
    public static T FromItems<TValue1, TValue2, TValue3, TValue4>(in TValue1 value1, in TValue2 value2, in TValue3 value3, in TValue4 value4)
        where TValue1 : struct, IJsonValue<TValue1>
        where TValue2 : struct, IJsonValue<TValue2>
        where TValue3 : struct, IJsonValue<TValue3>
        where TValue4 : struct, IJsonValue<TValue4>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        builder.Add(value1.AsAny);
        builder.Add(value2.AsAny);
        builder.Add(value3.AsAny);
        builder.Add(value4.AsAny);
        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Creates an array from the given values.
    /// </summary>
    /// <typeparam name="TValue">The type of the item.</typeparam>
    /// <param name = "value">The value from which to construct the instance.</param>
    /// <returns>A JsonAny instantiated from the given items.</returns>
    public static T FromItems<TValue>(params TValue[] value)
        where TValue : struct, IJsonValue<TValue>
    {
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (TValue item in value)
        {
            builder.Add(item.AsAny);
        }

        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Create an array from the given items.
    /// </summary>
    /// <typeparam name = "TItem">The type of the <paramref name = "items"/> from which to create the array.</typeparam>
    /// <param name = "items">The items from which to create the array.</param>
    /// <returns>The new array created from the items.</returns>
    /// <remarks>
    /// This will serialize the items to create the underlying JsonArray. Note the
    /// other overloads which avoid this serialization step.
    /// </remarks>
    public static T FromSerializableRange<TItem>(IEnumerable<TItem> items)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
        foreach (TItem item in items)
        {
            JsonSerializer.Serialize(writer, item);
            writer.Flush();
            builder.Add(JsonValueParser.ParseValue<JsonAny>(abw.WrittenSpan));
            writer.Reset();
        }

        return T.From(builder.ToImmutable());
    }

    /// <summary>
    /// Creates an array from an enumerable of string.
    /// </summary>
    /// <param name="value">The set of values to add.</param>
    /// <returns>The new array created from the items.</returns>
    public static T FromRange(IEnumerable<string> value)
    {
        return T.From(value.Select(static v => new JsonAny(v)).ToImmutableList());
    }

    /// <summary>
    /// Creates an array from an enumerable of doubles.
    /// </summary>
    /// <param name="value">The set of values to add.</param>
    /// <returns>The new array created from the items.</returns>
    public static T FromRange(IEnumerable<double> value)
    {
        return T.From(value.Select(static v => new JsonAny(v)).ToImmutableList());
    }

    /// <summary>
    /// Creates an array from an enumerable of long.
    /// </summary>
    /// <param name="value">The set of values to add.</param>
    /// <returns>The new array created from the items.</returns>
    public static T FromRange(IEnumerable<long> value)
    {
        return T.From(value.Select(static v => new JsonAny(v)).ToImmutableList());
    }

    /// <summary>
    /// Creates an array from an enumerable of bool.
    /// </summary>
    /// <param name="value">The set of values to add.</param>
    /// <returns>The new array created from the items.</returns>
    public static T FromRange(IEnumerable<bool> value)
    {
        return T.From(value.Select(static v => new JsonAny(v)).ToImmutableList());
    }
}