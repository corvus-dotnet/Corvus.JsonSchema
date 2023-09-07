// <copyright file="JsonValueConverter{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Generic json converter for <see cref="IJsonValue{T}"/>.
/// </summary>
/// <typeparam name="T">The type of <see cref="IJsonValue{T}"/> to convert.</typeparam>
public class JsonValueConverter<T> : System.Text.Json.Serialization.JsonConverter<T>
    where T : struct, IJsonValue<T>
{
    /// <inheritdoc/>
    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (!JsonValueConverter.EnableInefficientDeserializationSupport)
        {
            throw new InvalidOperationException($"Deserialization of IJsonValue is not advised. Prefer constructing from a JsonDocument instance to avoid managed allocations. If you require support for integration purposes, enable this with {nameof(JsonValueConverter.EnableInefficientDeserializationSupport)}.");
        }

        return T.FromJson(JsonElement.ParseValue(ref reader));
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        T value,
        JsonSerializerOptions options) => value.WriteTo(writer);
}