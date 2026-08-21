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
        (WorkflowRunAddress address, Uri checkpointUrl, string? checkpointToken) = ParseInvocation(invocationJson);

        // A bearer credential must never travel in cleartext, so refuse a token over a non-HTTPS checkpoint URL (a loopback
        // address is exempt: in-process and local tests use plain HTTP where TLS adds nothing, but a token never crosses
        // the internet unencrypted). Checked before the client is created, so nothing leaks.
        if (checkpointToken is not null && !checkpointUrl.IsLoopback && !checkpointUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A checkpoint token must not be sent over a non-HTTPS checkpoint URL.", nameof(invocationJson));
        }

        // A per-invocation client over the shared handler carries the dispatching runner's checkpoint base address, so a
        // run's checkpoints reach the runner that holds its lease (Model B). disposeHandler:false keeps the pooled handler.
        var client = new HttpClient(this.checkpointHandler, disposeHandler: false) { BaseAddress = checkpointUrl };

        // When the invocation carries a run-scoped checkpoint token (ADR 0062), present it as a bearer credential on every
        // checkpoint request, so a publicly reachable checkpoint surface can authenticate this callback. The token is
        // opaque here — the runner minted it and the checkpoint surface validates it.
        if (checkpointToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", checkpointToken);
        }

        try
        {
            var store = new HttpWorkflowStateStore(client);
            try
            {
                var host = new ServerlessWorkflowRunHost(store, this.resolver, this.transportBinder, this.timeProvider);
                WorkflowRunResultKind? kind = await host.InvokeAsync(address, cancellationToken).ConfigureAwait(false);
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

    private static (WorkflowRunAddress Address, Uri CheckpointUrl, string? CheckpointToken) ParseInvocation(ReadOnlyMemory<byte> invocationJson)
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

        if (!root.TryGetProperty("runId"u8, out JsonElement runIdElement) || runIdElement.ValueKind != JsonValueKind.String)
        {
            ThrowHelper.ThrowMissingRunId(nameof(invocationJson));
        }

        // The invocation arrives from the dispatch fabric, outside the function's trust boundary, so the run-id
        // grammar is validated at the parse (ADR 0065 §9: at every ingress, before any store touch) — and on the
        // UTF-8 value, so an oversized or malformed id is refused without materializing it as a string.
        using (UnescapedUtf8JsonString runIdUtf8 = runIdElement.GetUtf8String())
        {
            if (runIdUtf8.Span.IsEmpty)
            {
                ThrowHelper.ThrowMissingRunId(nameof(invocationJson));
            }

            if (!WorkflowRunId.IsWellFormedUtf8(runIdUtf8.Span))
            {
                ThrowHelper.ThrowMalformedRunId(nameof(invocationJson));
            }
        }

        // Exactly 32 ASCII characters, validated above: the one bounded allocation the run half of the address needs.
        string runId = runIdElement.GetString()!;

        // The environment is the other half of the run's address (ADR 0065 decision 9) and is required: validated
        // UTF-8-first against the environment-name grammar, so an oversized or malformed value is refused without
        // materializing it as a string, exactly as the run id above.
        if (!root.TryGetProperty("environment"u8, out JsonElement environmentElement) || environmentElement.ValueKind != JsonValueKind.String)
        {
            ThrowHelper.ThrowMissingEnvironment(nameof(invocationJson));
        }

        using (UnescapedUtf8JsonString environmentUtf8 = environmentElement.GetUtf8String())
        {
            if (environmentUtf8.Span.IsEmpty)
            {
                ThrowHelper.ThrowMissingEnvironment(nameof(invocationJson));
            }

            if (!Environments.EnvironmentName.IsWellFormedUtf8(environmentUtf8.Span))
            {
                ThrowHelper.ThrowMalformedEnvironment(nameof(invocationJson));
            }
        }

        string environment = environmentElement.GetString()!;

        string? checkpointUrl = root.TryGetProperty("checkpointUrl"u8, out JsonElement urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrEmpty(checkpointUrl) || !Uri.TryCreate(checkpointUrl, UriKind.Absolute, out Uri? parsed))
        {
            throw ThrowHelper.GetMissingCheckpointUrlException(nameof(invocationJson));
        }

        // The checkpoint token is optional (a not-token-authenticated checkpoint surface carries none), so its absence is
        // not an error; when present it is the bearer credential for the checkpoint callbacks (ADR 0062).
        string? checkpointToken = root.TryGetProperty("checkpointToken"u8, out JsonElement tokenElement) && tokenElement.ValueKind == JsonValueKind.String
            ? tokenElement.GetString()
            : null;

        return (new WorkflowRunAddress(environment, new WorkflowRunId(runId)), parsed, checkpointToken);
    }

    private static byte[] Outcome(WorkflowRunResultKind? kind) => kind switch
    {
        WorkflowRunResultKind.Completed => CompletedOutcome,
        WorkflowRunResultKind.Faulted => FaultedOutcome,
        WorkflowRunResultKind.Suspended => SuspendedOutcome,
        _ => NotDispatchableOutcome,
    };
}