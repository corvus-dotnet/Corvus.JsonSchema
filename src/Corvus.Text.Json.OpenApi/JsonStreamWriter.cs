// <copyright file="JsonStreamWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Pipelines;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Writes JSON stream items using the media framing required by OpenAPI <c>itemSchema</c> responses.
/// </summary>
public readonly struct JsonStreamWriter
{
    private static ReadOnlySpan<byte> SseDataPrefix => "data: "u8;

    private static ReadOnlySpan<byte> SseDataSuffix => "\n\n"u8;

    private static ReadOnlySpan<byte> NewLine => "\n"u8;

    private readonly PipeWriter pipeWriter;
    private readonly Utf8JsonWriter jsonWriter;
    private readonly JsonStreamWriterFormat format;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStreamWriter"/> struct.
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to which the stream should be written.</param>
    /// <param name="jsonWriter">The JSON writer used to serialize each item.</param>
    /// <param name="contentType">The response content type.</param>
    public JsonStreamWriter(PipeWriter pipeWriter, Utf8JsonWriter jsonWriter, string contentType)
    {
        this.pipeWriter = pipeWriter;
        this.jsonWriter = jsonWriter;
        this.format = contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase)
            ? JsonStreamWriterFormat.ServerSentEvents
            : JsonStreamWriterFormat.NewlineDelimitedJson;
    }

    /// <summary>
    /// Writes a single stream item.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="item">The item to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A value task that completes when the item has been flushed.</returns>
    public async ValueTask WriteItemAsync<T>(T item, CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        if (this.format == JsonStreamWriterFormat.ServerSentEvents)
        {
            Write(this.pipeWriter, SseDataPrefix);
        }

        item.WriteTo(this.jsonWriter);
        this.jsonWriter.Flush();

        Write(this.pipeWriter, this.format == JsonStreamWriterFormat.ServerSentEvents ? SseDataSuffix : NewLine);

        FlushResult result = await this.pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        this.jsonWriter.Reset(this.pipeWriter);
    }

    private static void Write(PipeWriter writer, ReadOnlySpan<byte> value)
    {
        value.CopyTo(writer.GetSpan(value.Length));
        writer.Advance(value.Length);
    }

    private enum JsonStreamWriterFormat
    {
        NewlineDelimitedJson,
        ServerSentEvents,
    }
}