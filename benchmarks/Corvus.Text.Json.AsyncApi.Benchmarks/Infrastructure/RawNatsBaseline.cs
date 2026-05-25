// <copyright file="RawNatsBaseline.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsyncApiBenchmark.Infrastructure;

/// <summary>
/// Simulates raw NATS.Net publish/subscribe with System.Text.Json —
/// the floor measurement (no Corvus pipeline, no validation).
/// </summary>
/// <remarks>
/// This doesn't use a real NatsConnection — we measure the serialization cost
/// that a raw NATS user would pay, not the transport I/O (which would be
/// identical for both Corvus and raw approaches).
/// </remarks>
public static class RawNatsBaseline
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_buffer;

    /// <summary>
    /// Serializes a payload using System.Text.Json (simulating what a raw NATS publisher does).
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="payload">The payload to serialize.</param>
    /// <returns>The number of bytes written.</returns>
    public static int Publish<T>(in T payload)
    {
        ArrayBufferWriter<byte> buffer = t_buffer ??= new ArrayBufferWriter<byte>(1024);
        buffer.ResetWrittenCount();

        using Utf8JsonWriter writer = new(buffer);
        JsonSerializer.Serialize(writer, payload, Options);

        return buffer.WrittenCount;
    }

    /// <summary>
    /// Deserializes bytes using System.Text.Json (simulating what a raw NATS subscriber does).
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="payloadJson">The raw JSON bytes.</param>
    /// <returns>The deserialized payload.</returns>
    public static T Subscribe<T>(ReadOnlySpan<byte> payloadJson)
    {
        return JsonSerializer.Deserialize<T>(payloadJson, Options)!;
    }
}

/// <summary>
/// A POCO representation of the light measured payload for STJ baseline comparison.
/// </summary>
public sealed class LightMeasuredPoco
{
    /// <summary>
    /// Gets or sets the sensor ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the lumens measurement.
    /// </summary>
    [JsonPropertyName("lumens")]
    public double Lumens { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = string.Empty;
}