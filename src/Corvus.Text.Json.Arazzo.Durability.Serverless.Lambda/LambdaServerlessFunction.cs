// <copyright file="LambdaServerlessFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda;

/// <summary>
/// The AWS Lambda entry shim for a baked Arazzo serverless function (ADR 0055): a reflection-free custom-runtime host
/// that turns each Lambda invocation stream into a call on the vendor-neutral <see cref="ServerlessInvocationHandler"/>.
/// The per-version function's generated entry point calls <see cref="RunAsync"/> with the executor baked into it and
/// the transport binder for its deployed environment; everything JSON is handled by Corvus.Text.Json, so no Amazon
/// serialization (and its reflection) enters the Native-AOT graph.
/// </summary>
public static class LambdaServerlessFunction
{
    /// <summary>
    /// Runs the Lambda custom-runtime loop, advancing one run per invocation. Blocks for the life of the function
    /// instance (the Lambda runtime calls back for each event).
    /// </summary>
    /// <param name="resolver">The workflow resolver baked into this function — a <c>BakedHostedWorkflowResolver</c>.</param>
    /// <param name="transportBinder">The transport binder for this function's deployed environment.</param>
    /// <returns>A task that completes when the runtime loop ends (the function instance is torn down).</returns>
    public static async Task RunAsync(IHostedWorkflowResolver resolver, WorkflowTransportBinder transportBinder)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transportBinder);

        // One handler for the function instance's life so checkpoint connections pool across warm invocations; the
        // invocation handler builds a per-invocation client over it, addressed at the invocation's checkpoint URL.
        using var checkpointHandler = new SocketsHttpHandler();
        var invocationHandler = new ServerlessInvocationHandler(resolver, transportBinder, checkpointHandler);

        Func<Stream, ILambdaContext, Task<Stream>> handler = (invocation, context) => InvokeAsync(invocationHandler, invocation);
        using HandlerWrapper wrapper = HandlerWrapper.GetHandlerWrapper(handler);
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync().ConfigureAwait(false);
    }

    private static async Task<Stream> InvokeAsync(ServerlessInvocationHandler invocationHandler, Stream invocation)
    {
        byte[] invocationBytes;
        using (var buffer = new MemoryStream())
        {
            await invocation.CopyToAsync(buffer).ConfigureAwait(false);
            invocationBytes = buffer.ToArray();
        }

        // A malformed invocation throws out of here, which the Lambda runtime reports as a function error — the runner's
        // backend sees a failed invocation and re-claims the run, the same recovery an in-process advance failure gets.
        byte[] outcome = await invocationHandler.HandleAsync(invocationBytes, CancellationToken.None).ConfigureAwait(false);
        return new MemoryStream(outcome, writable: false);
    }
}