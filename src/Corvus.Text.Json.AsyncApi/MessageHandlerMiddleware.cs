// <copyright file="MessageHandlerMiddleware.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// A delegate that wraps message handler execution, enabling retry, circuit-breaker,
/// timeout, and other resilience patterns.
/// </summary>
/// <remarks>
/// <para>
/// The transport calls through this delegate for every message handler invocation.
/// Implementations may retry the operation, add backoff delays, apply circuit-breaker
/// logic, or add observability instrumentation.
/// </para>
/// <para>
/// If the middleware exhausts all retries and the operation still fails, it should
/// let the exception propagate. The transport will then invoke the
/// <see cref="IMessageErrorPolicy"/> to determine the terminal action (skip,
/// dead-letter, or abort).
/// </para>
/// <para>
/// The <c>Corvus.Text.Json.AsyncApi.Polly</c> package provides a ready-made
/// implementation backed by <c>Polly.ResiliencePipeline</c>.
/// </para>
/// </remarks>
/// <param name="operation">The handler operation to execute.</param>
/// <param name="cancellationToken">A cancellation token that should be passed
/// through to the operation.</param>
/// <returns>A <see cref="ValueTask"/> representing the wrapped operation.</returns>
public delegate ValueTask MessageHandlerMiddleware(
    Func<CancellationToken, ValueTask> operation,
    CancellationToken cancellationToken);