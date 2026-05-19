// <copyright file="SseEvent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents a Server-Sent Event (SSE) containing a parsed data payload
/// and optional SSE metadata fields as raw UTF-8 bytes.
/// </summary>
/// <typeparam name="T">The element type of the parsed data payload.</typeparam>
/// <remarks>
/// <para>This struct is disposable. Disposing it returns the backing document memory
/// to the pool and returns the metadata buffer (if rented). Always use in a <c>using</c>
/// block or call <see cref="Dispose"/> when done.</para>
/// </remarks>
public struct SseEvent<T> : IDisposable
    where T : struct, IJsonElement<T>
{
    private ParsedJsonDocument<T>? document;
    private byte[]? metadataBuffer;
    private ReadOnlyMemory<byte> eventType;
    private ReadOnlyMemory<byte> id;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseEvent{T}"/> struct.
    /// </summary>
    /// <param name="document">The parsed document owning the data payload.</param>
    /// <param name="eventType">The optional SSE event type as UTF-8 bytes (slice of <paramref name="metadataBuffer"/>).</param>
    /// <param name="id">The optional SSE event ID as UTF-8 bytes (slice of <paramref name="metadataBuffer"/>).</param>
    /// <param name="retry">The optional reconnection time in milliseconds.</param>
    /// <param name="metadataBuffer">The rented buffer backing <paramref name="eventType"/> and <paramref name="id"/>, or <see langword="null"/>.</param>
    public SseEvent(ParsedJsonDocument<T> document, ReadOnlyMemory<byte> eventType, ReadOnlyMemory<byte> id, int? retry, byte[]? metadataBuffer)
    {
        this.document = document;
        this.eventType = eventType;
        this.id = id;
        this.Retry = retry;
        this.metadataBuffer = metadataBuffer;
    }

    /// <summary>
    /// Gets the parsed data payload of the event.
    /// </summary>
    public T Data => this.document is { } doc ? doc.RootElement : default;

    /// <summary>
    /// Gets the SSE event type (from the <c>event:</c> field) as UTF-8 bytes.
    /// Empty if no event type was specified (defaults to "message" per SSE spec).
    /// </summary>
    public ReadOnlySpan<byte> EventType => this.eventType.Span;

    /// <summary>
    /// Gets the SSE event ID (from the <c>id:</c> field) as UTF-8 bytes.
    /// Empty if no ID was specified.
    /// </summary>
    public ReadOnlySpan<byte> Id => this.id.Span;

    /// <summary>
    /// Gets the reconnection time in milliseconds (from the <c>retry:</c> field),
    /// or <see langword="null"/> if not specified.
    /// </summary>
    public int? Retry { get; }

    /// <summary>
    /// Gets the SSE event type as a string, or <see langword="null"/> if not specified.
    /// </summary>
    /// <returns>The event type string, or <see langword="null"/> if the event type field was not present.</returns>
    public string? GetEventTypeAsString()
    {
        return this.eventType.IsEmpty ? null : Encoding.UTF8.GetString(this.eventType.Span);
    }

    /// <summary>
    /// Gets the SSE event ID as a string, or <see langword="null"/> if not specified.
    /// </summary>
    /// <returns>The event ID string, or <see langword="null"/> if the id field was not present.</returns>
    public string? GetIdAsString()
    {
        return this.id.IsEmpty ? null : Encoding.UTF8.GetString(this.id.Span);
    }

    /// <summary>
    /// Disposes the backing document and returns the metadata buffer to the pool.
    /// </summary>
    public void Dispose()
    {
        if (this.document is { } doc)
        {
            doc.Dispose();
            this.document = null;
        }

        if (this.metadataBuffer is { } buf)
        {
            ArrayPool<byte>.Shared.Return(buf);
            this.metadataBuffer = null;
        }
    }
}