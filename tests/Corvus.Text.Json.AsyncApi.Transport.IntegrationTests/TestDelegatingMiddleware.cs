// <copyright file="TestDelegatingMiddleware.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Adapts the old delegate-shaped middleware body to <see cref="MessageHandlerMiddleware"/>
/// for tests. The closure per invocation is fine here; production callers use the
/// allocation-free generic contract directly.
/// </summary>
/// <param name="impl">The middleware body: receives the bound operation and the token.</param>
internal sealed class TestDelegatingMiddleware(
    Func<Func<CancellationToken, ValueTask>, CancellationToken, ValueTask> impl) : MessageHandlerMiddleware
{
    /// <inheritdoc/>
    public override ValueTask InvokeAsync<TState>(
        Func<TState, CancellationToken, ValueTask> operation,
        TState state,
        CancellationToken cancellationToken)
    {
        return impl(ct => operation(state, ct), cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask<TResult> InvokeAsync<TState, TResult>(
        Func<TState, CancellationToken, ValueTask<TResult>> operation,
        TState state,
        CancellationToken cancellationToken)
    {
        TResult result = default!;
        await impl(async ct => result = await operation(state, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return result;
    }
}