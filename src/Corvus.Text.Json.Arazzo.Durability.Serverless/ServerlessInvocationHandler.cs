// <copyright file="ServerlessInvocationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless;

/// <summary>
/// The vendor-neutral core a serverless entry shim (an AWS Lambda handler or an Azure Functions HTTP trigger) invokes:
/// it turns one HTTP invocation — <c>{ "runId", "environment", "checkpointUrl" }</c> — into a run advance, checkpointing
/// back to the dispatching runner named by <c>checkpointUrl</c> (ADR 0055). The workflow resolver and transport binder
/// are deploy-fixed (the function is baked per (environment, version)); only the checkpoint store's address is
/// per-invocation, so a run's checkpoints reach the specific runner that dispatched it and holds its lease.
/// </summary>
/// <remarks>
/// It binds no store SDK and holds no store credentials: it proxies checkpoints over HTTP through
/// <see cref="HttpWorkflowStateStore"/>. The <see cref="HttpMessageHandler"/> is shared across invocations (connection
/// pooling) and owned by the caller; a per-invocation <see cref="HttpClient"/> carries the invocation's checkpoint base
/// address over it. This core has no dependency on any vendor runtime, so it is unit-testable in-process against the
/// runner's real checkpoint surface; the vendor entry shims are the thin bootstrap that feeds it the invocation bytes.
/// </remarks>
public sealed class ServerlessInvocationHandler
{
    // The outcome document is fixed-shape, so it is emitted from UTF-8 literals rather than a writer — the runner's
    // ServerlessRunExecutionBackend reads the "outcome" string (a null/absent one is a benign not-dispatchable result).
    private static readonly byte[] CompletedOutcome = """{"outcome":"Completed"}"""u8.ToArray();
    private static readonly byte[] FaultedOutcome = """{"outcome":"Faulted"}"""u8.ToArray();
    private static readonly byte[] SuspendedOutcome = """{"outcome":"Suspended"}"""u8.ToArray();
    private static readonly byte[] NotDispatchableOutcome = "{}"u8.ToArray();

    private readonly IHostedWorkflowResolver resolver;
    private readonly WorkflowTransportBinder transportBinder;
    private readonly HttpMessageHandler checkpointHandler;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ServerlessInvocationHandler"/> class.</summary>
    /// <param name="resolver">Resolves a run to the workflow that runs it — a <c>BakedHostedWorkflowResolver</c> in a deployed function.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports it executes through, for this function's (deployed) environment.</param>
    /// <param name="checkpointHandler">The shared, caller-owned HTTP message handler the per-invocation checkpoint client runs over (connection pooling).</param>
    /// <param name="timeProvider">The time provider the restored run uses for its timer waits; defaults to <see cref="TimeProvider.System"/>.</param>
    public ServerlessInvocationHandler(IHostedWorkflowResolver resolver, WorkflowTransportBinder transportBinder, HttpMessageHandler checkpointHandler, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transportBinder);
        ArgumentNullException.ThrowIfNull(checkpointHandler);
        this.resolver = resolver;
        this.transportBinder = transportBinder;
        this.checkpointHandler = checkpointHandler;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Advances the run named by one invocation and returns the outcome document. Throws <see cref="ArgumentException"/>
    /// on a malformed invocation (so the vendor runtime reports a function error and the dispatcher re-claims the run).
    /// </summary>
    /// <param name="invocationJson">The invocation body — <c>{ "runId", "environment", "checkpointUrl" }</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome document (UTF-8 JSON) the runner-side backend parses.</returns>
    public async ValueTask<byte[]> HandleAsync(ReadOnlyMemory<byte> invocationJson, CancellationToken cancellationToken)
    {
        (WorkflowRunId runId, Uri checkpointUrl) = ParseInvocation(invocationJson);

        // A per-invocation client over the shared handler carries the dispatching runner's checkpoint base address, so a
        // run's checkpoints reach the runner that holds its lease (Model B). disposeHandler:false keeps the pooled handler.
        var client = new HttpClient(this.checkpointHandler, disposeHandler: false) { BaseAddress = checkpointUrl };
        try
        {
            var store = new HttpWorkflowStateStore(client);
            try
            {
                var host = new ServerlessWorkflowRunHost(store, this.resolver, this.transportBinder, this.timeProvider);
                WorkflowRunResultKind? kind = await host.InvokeAsync(runId, cancellationToken).ConfigureAwait(false);
                return Outcome(kind);
            }
            finally
            {
                // Drain any still-pending fire-and-forget writes before the client is torn down. The host's flush already
                // awaited the terminal one on a real advance; this also covers the not-dispatchable early return.
                await store.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    private static (WorkflowRunId RunId, Uri CheckpointUrl) ParseInvocation(ReadOnlyMemory<byte> invocationJson)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(invocationJson);
        JsonElement root = document.RootElement;

        // A Lambda Function URL (or API Gateway) delivers the request wrapped in an event envelope — the actual invocation
        // body is the `body` string (base64-encoded when `isBase64Encoded` is true). A direct Invoke delivers the body
        // verbatim. When the top-level payload is not our {runId, ...} shape but carries a `body`, unwrap it and re-parse,
        // so both invocation paths reach the same {runId, environment, checkpointUrl}.
        if (!root.TryGetProperty("runId"u8, out _)
            && root.TryGetProperty("body"u8, out JsonElement bodyElement)
            && bodyElement.ValueKind == JsonValueKind.String
            && bodyElement.GetString() is { Length: > 0 } bodyText)
        {
            bool base64 = root.TryGetProperty("isBase64Encoded"u8, out JsonElement flagElement)
                && flagElement.ValueKind == JsonValueKind.True;
            byte[] innerBytes = base64 ? Convert.FromBase64String(bodyText) : System.Text.Encoding.UTF8.GetBytes(bodyText);
            return ParseInvocation(innerBytes);
        }

        string? runId = root.TryGetProperty("runId"u8, out JsonElement runIdElement) ? runIdElement.GetString() : null;
        if (string.IsNullOrEmpty(runId))
        {
            ThrowHelper.ThrowMissingRunId(nameof(invocationJson));
        }

        string? checkpointUrl = root.TryGetProperty("checkpointUrl"u8, out JsonElement urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrEmpty(checkpointUrl) || !Uri.TryCreate(checkpointUrl, UriKind.Absolute, out Uri? parsed))
        {
            throw ThrowHelper.GetMissingCheckpointUrlException(nameof(invocationJson));
        }

        return (new WorkflowRunId(runId), parsed);
    }

    private static byte[] Outcome(WorkflowRunResultKind? kind) => kind switch
    {
        WorkflowRunResultKind.Completed => CompletedOutcome,
        WorkflowRunResultKind.Faulted => FaultedOutcome,
        WorkflowRunResultKind.Suspended => SuspendedOutcome,
        _ => NotDispatchableOutcome,
    };
}