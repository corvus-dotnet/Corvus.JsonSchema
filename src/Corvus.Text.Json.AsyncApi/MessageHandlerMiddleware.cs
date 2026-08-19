// <copyright file="MessageHandlerMiddleware.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Wraps message handler execution, enabling retry, circuit-breaker, timeout, and other
/// resilience patterns.
/// </summary>
/// <remarks>
/// <para>
/// The transport calls through this class for every message handler invocation, passing the
/// operation as a static delegate plus an explicit state value (the pattern
/// <c>Polly.ResiliencePipeline.ExecuteAsync&lt;TState&gt;</c> uses), so a middleware-wrapped
/// dispatch allocates no per-message closure. Implementations invoke
/// <c>operation(state, cancellationToken)</c> once per attempt, passing the state through
/// unchanged, and must not retain the operation or the state beyond the call.
/// </para>
/// <para>
/// If the middleware exhausts all retries and the operation still fails, it should let the
/// exception propagate. The transport then invokes the <see cref="IMessageErrorPolicy"/> to
/// determine the terminal action (skip, dead-letter, or abort).
/// </para>
/// <para>
/// The <c>Corvus.Text.Json.AsyncApi.Polly</c> package provides a ready-made implementation
/// backed by <c>Polly.ResiliencePipeline</c>.
/// </para>
/// </remarks>
public abstract class MessageHandlerMiddleware
{
    /// <summary>
    /// Executes a handler operation through the middleware.
    /// </summary>
    /// <typeparam name="TState">The state the operation needs.</typeparam>
    /// <param name="operation">The handler operation to execute. Invoke it once per attempt
    /// with <paramref name="state"/> and the cancellation token.</param>
    /// <param name="state">The state to pass through to <paramref name="operation"/> unchanged.</param>
    /// <param name="cancellationToken">A cancellation token to pass through to the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the wrapped operation.</returns>
    public abstract ValueTask InvokeAsync<TState>(
        Func<TState, CancellationToken, ValueTask> operation,
        TState state,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a result-producing handler operation through the middleware.
    /// </summary>
    /// <typeparam name="TState">The state the operation needs.</typeparam>
    /// <typeparam name="TResult">The operation's result type.</typeparam>
    /// <param name="operation">The handler operation to execute. Invoke it once per attempt
    /// with <paramref name="state"/> and the cancellation token.</param>
    /// <param name="state">The state to pass through to <paramref name="operation"/> unchanged.</param>
    /// <param name="cancellationToken">A cancellation token to pass through to the operation.</param>
    /// <returns>The result of the last operation attempt.</returns>
    public abstract ValueTask<TResult> InvokeAsync<TState, TResult>(
        Func<TState, CancellationToken, ValueTask<TResult>> operation,
        TState state,
        CancellationToken cancellationToken);
}