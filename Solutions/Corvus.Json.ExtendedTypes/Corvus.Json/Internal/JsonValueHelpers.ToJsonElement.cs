// <copyright file="JsonValueHelpers.ToJsonElement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Write a bool to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <returns>The <see cref="JsonElement"/> serialized from the value.</returns>
    public static JsonElement BoolToJsonElement(bool value)
    {
        return value ? TrueElement : FalseElement;
    }

    /// <summary>
    /// Write a number to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="numberBacking">The byte array backing the number.</param>
    /// <returns>The <see cref="JsonElement"/> serialized from the value.</returns>
    public static JsonElement NumberToJsonElement(in BinaryJsonNumber numberBacking)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        numberBacking.WriteTo(writer);
        writer.Flush();
        var reader = new Utf8JsonReader(abw.WrittenSpan);
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Write a property dictionary to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="properties">The property dictionary to write.</param>
    /// <returns>The <see cref="JsonElement"/> serialized from the value.</returns>
    public static JsonElement ObjectToJsonElement(ImmutableList<JsonObjectProperty> properties)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        WriteProperties(properties, writer);
        writer.Flush();
        var reader = new Utf8JsonReader(abw.WrittenSpan);
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Convert an items array to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="items">The items to convert.</param>
    /// <returns>The <see cref="JsonElement"/> serialized from the value.</returns>
    public static JsonElement ArrayToJsonElement(ImmutableList<JsonAny> items)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        WriteItems(items, writer);
        writer.Flush();
        var reader = new Utf8JsonReader(abw.WrittenSpan);
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Write a string to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <returns>The <see cref="JsonElement"/> serialized from the value.</returns>
    public static JsonElement StringToJsonElement(string value)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        writer.WriteStringValue(value);
        writer.Flush();
        var reader = new Utf8JsonReader(abw.WrittenSpan);
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }
}