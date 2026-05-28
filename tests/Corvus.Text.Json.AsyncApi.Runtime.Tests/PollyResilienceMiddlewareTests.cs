// <copyright file="PollyResilienceMiddlewareTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Polly;
using Polly;
using Polly.Retry;
using DelayBackoffType = Polly.DelayBackoffType;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for <see cref="PollyResilienceMiddleware"/> integration with the
/// <see cref="MessageHandlerMiddleware"/> delegate pattern.
/// </summary>
[TestClass]
public class PollyResilienceMiddlewareTests
{
    [TestMethod]
    public void Create_NullPipeline_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PollyResilienceMiddleware.Create(null!));
    }

    [TestMethod]
    public async Task Create_ReturnsMiddlewareThatExecutesOperation()
    {
        ResiliencePipeline pipeline = ResiliencePipeline.Empty;
        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        bool operationExecuted = false;
        await middleware(
            ct =>
            {
                operationExecuted = true;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.IsTrue(operationExecuted);
    }

    [TestMethod]
    public async Task Create_WithRetryPolicy_RetriesOnFailure()
    {
        int attempts = 0;
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            })
            .Build();

        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        await middleware(
            ct =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException($"Attempt {attempts}");
                }

                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public async Task Create_WithRetryPolicy_ExhaustsRetriesAndThrows()
    {
        int attempts = 0;
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            })
            .Build();

        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await middleware(
                ct =>
                {
                    attempts++;
                    throw new InvalidOperationException("Always fails");
                },
                CancellationToken.None));

        // Initial attempt + 2 retries = 3 total attempts
        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public async Task Create_PassesCancellationTokenToOperation()
    {
        ResiliencePipeline pipeline = ResiliencePipeline.Empty;
        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        using CancellationTokenSource cts = new();
        CancellationToken capturedToken = default;

        await middleware(
            ct =>
            {
                capturedToken = ct;
                return ValueTask.CompletedTask;
            },
            cts.Token);

        Assert.AreEqual(cts.Token, capturedToken);
    }

    [TestMethod]
    public async Task Create_WithCancelledToken_ThrowsOperationCanceledException()
    {
        ResiliencePipeline pipeline = ResiliencePipeline.Empty;
        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await middleware(
                ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.CompletedTask;
                },
                cts.Token));
    }

    [TestMethod]
    public async Task Create_WithExponentialBackoff_RetriesWithDelay()
    {
        int attempts = 0;
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(10),
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            })
            .Build();

        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await middleware(
            ct =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException($"Attempt {attempts}");
                }

                return ValueTask.CompletedTask;
            },
            CancellationToken.None);
        stopwatch.Stop();

        Assert.AreEqual(3, attempts);

        // Exponential backoff: 10ms + 20ms = ~30ms minimum (with jitter)
        Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 20, $"Expected at least 20ms delay, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task Create_NonHandledException_PropagatesImmediately()
    {
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            })
            .Build();

        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);
        int attempts = 0;

        // ArgumentException is NOT handled by the retry policy
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await middleware(
                ct =>
                {
                    attempts++;
                    throw new ArgumentException("Not retryable");
                },
                CancellationToken.None));

        // Only 1 attempt — no retry for unhandled exceptions
        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task Create_IntegrationWithTransport_MiddlewareRetriesThenPolicyHandlesTerminalFailure()
    {
        // End-to-end: Polly retries 2 times, then the error policy catches the final exception.
        int handlerCalls = 0;
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            })
            .Build();

        MessageHandlerMiddleware middleware = PollyResilienceMiddleware.Create(pipeline);

        // Simulate what a transport does: call middleware, catch exception, invoke error policy
        bool policyInvoked = false;
        MessageErrorAction? policyAction = null;
        DefaultMessageErrorPolicy errorPolicy = new(
            MessageErrorAction.Skip,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        try
        {
            await middleware(
                ct =>
                {
                    handlerCalls++;
                    throw new InvalidOperationException("Permanent failure");
                },
                CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            policyInvoked = true;
            MessageErrorContext ctx = new("test-channel"u8.ToArray(), MessageErrorKind.Handler);
            policyAction = await errorPolicy.HandleErrorAsync(ex, ctx, CancellationToken.None);
        }

        // 1 initial + 2 retries = 3
        Assert.AreEqual(3, handlerCalls);
        Assert.IsTrue(policyInvoked);
        Assert.AreEqual(MessageErrorAction.DeadLetter, policyAction);
    }
}