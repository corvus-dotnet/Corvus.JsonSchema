// <copyright file="JsonConverter{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Generic json converter for <see cref="IJsonValue{T}"/>.
/// </summary>
/// <typeparam name="T">The type of <see cref="IJsonValue{T}"/> to convert.</typeparam>
public class JsonConverter<T> : System.Text.Json.Serialization.JsonConverter<T>
    where T : struct, IJsonValue<T>
{
    /// <inheritdoc/>
    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (!JsonConverter.EnableInefficientDeserializationSupport)
        {
            throw new InvalidOperationException($"Serialization of IJsonValue is not advised. Prefer constructing from a JsonDocument instance to avoid managed allocations. If you require support for integration purposes, enable this with {nameof(JsonConverter.EnableInefficientDeserializationSupport)}.");
        }

        using var jd = JsonDocument.ParseValue(ref reader);

        // Because we cannot control the lifetime of the resulting JSON Document, we have to clone the element
        // which creates a copy of the underlying byte data into managed memory.
        return T.FromJson(jd.RootElement.Clone());
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        T value,
        JsonSerializerOptions options) => value.WriteTo(writer);
}