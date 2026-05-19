// <copyright file="SseEvent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents a Server-Sent Event (SSE) containing a parsed data payload
/// and optional SSE metadata fields.
/// </summary>
/// <typeparam name="T">The element type of the parsed data payload.</typeparam>
public readonly struct SseEvent<T>
    where T : struct, IJsonElement<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SseEvent{T}"/> struct.
    /// </summary>
    /// <param name="data">The parsed data payload.</param>
    /// <param name="eventType">The optional SSE event type.</param>
    /// <param name="id">The optional SSE event ID.</param>
    /// <param name="retry">The optional reconnection time in milliseconds.</param>
    public SseEvent(T data, string? eventType = null, string? id = null, int? retry = null)
    {
        this.Data = data;
        this.EventType = eventType;
        this.Id = id;
        this.Retry = retry;
    }

    /// <summary>
    /// Gets the parsed data payload of the event.
    /// </summary>
    public T Data { get; }

    /// <summary>
    /// Gets the SSE event type (from the <c>event:</c> field), or <see langword="null"/>
    /// if no event type was specified (defaults to "message" per SSE spec).
    /// </summary>
    public string? EventType { get; }

    /// <summary>
    /// Gets the SSE event ID (from the <c>id:</c> field), or <see langword="null"/>
    /// if no ID was specified.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the reconnection time in milliseconds (from the <c>retry:</c> field),
    /// or <see langword="null"/> if not specified.
    /// </summary>
    public int? Retry { get; }
}