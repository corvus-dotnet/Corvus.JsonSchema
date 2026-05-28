// <copyright file="ITransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Common configuration for all message transport implementations.
/// </summary>
/// <remarks>
/// <para>
/// Transport-specific options types implement this interface to carry the
/// shared resilience settings (<see cref="ErrorPolicy"/> and
/// <see cref="HandlerMiddleware"/>) alongside their transport-specific
/// configuration.
/// </para>
/// </remarks>
public interface ITransportOptions
{
    /// <summary>
    /// Gets or sets the error policy that determines what action to take
    /// when a message permanently fails processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see langword="null"/>, a <see cref="DefaultMessageErrorPolicy"/> is used
    /// (dead-letter on handler/deserialization errors, abort on transport errors).
    /// </para>
    /// </remarks>
    IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <summary>
    /// Gets or sets the middleware that wraps handler invocations with resilience
    /// patterns (retry, circuit-breaker, timeout, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see langword="null"/>, the handler is invoked directly with no retry.
    /// Use the <c>Corvus.Text.Json.AsyncApi.Polly</c> package to create middleware
    /// backed by a <c>ResiliencePipeline</c>.
    /// </para>
    /// </remarks>
    MessageHandlerMiddleware? HandlerMiddleware { get; set; }

    /// <summary>
    /// Gets or sets the processing loop heartbeat tracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, the transport's subscribe loop will call <see cref="ProcessingLoopHeartbeat.Tick"/>
    /// on every iteration, and <see cref="ProcessingLoopHeartbeat.Start"/>/<see cref="ProcessingLoopHeartbeat.Stop"/>
    /// on subscription lifecycle events. This enables external monitoring to detect
    /// when a processing loop silently dies.
    /// </para>
    /// <para>
    /// If <see langword="null"/>, no heartbeat tracking occurs (zero overhead).
    /// </para>
    /// </remarks>
    ProcessingLoopHeartbeat? Heartbeat { get; set; }
}