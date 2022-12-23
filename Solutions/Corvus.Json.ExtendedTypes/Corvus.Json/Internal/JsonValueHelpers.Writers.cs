// <copyright file="JsonValueHelpers.Writers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Write an items array to a <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="items">The items to write.</param>
    /// <param name="writer">The writer to which to write the array.</param>
    public static void WriteItems(ImmutableList<JsonAny> items, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();

        foreach (JsonAny item in items)
        {
            item.WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes a property dictionary to a JSON writer.
    /// </summary>
    /// <param name="properties">The property dictionary to write.</param>
    /// <param name="writer">The writer to which to write the object.</param>
    public static void WriteProperties(ImmutableDictionary<JsonPropertyName, JsonAny> properties, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        foreach (KeyValuePair<JsonPropertyName, JsonAny> property in properties)
        {
            writer.WritePropertyName(property.Key.Name);
            property.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}