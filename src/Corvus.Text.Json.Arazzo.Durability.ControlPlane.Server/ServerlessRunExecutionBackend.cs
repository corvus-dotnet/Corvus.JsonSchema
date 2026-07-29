// <copyright file="ServerlessRunExecutionBackend.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The runner-side serverless <see cref="IRunExecutionBackend"/> (ADR 0055): it advances a run by invoking the
/// serverless function baked for the run's (environment, version) over HTTP, rather than loading and running the
/// executor in-process. Because a run is a durable checkpoint in the shared store, the invocation carries only the run
/// id, its environment, and the checkpoint base URL the function calls back to; the function-side
/// <see cref="ServerlessWorkflowRunHost"/> restores the run, advances it, and checkpoints it back to that URL. This is
/// the dispatch-and-invoke counterpart to that host, and the out-of-process peer of the in-process
/// <see cref="HostedWorkflowResumer"/>.
/// </summary>
/// <remarks>
/// Leasing is unchanged: the dispatcher holds the run's lease across <see cref="AdvanceAsync"/>, so this backend does
/// not lease. A failed invocation throws, which leaves the run claimable, so the dispatcher's lease and retry
/// re-claim and re-invoke it — the same recovery an in-process advance failure gets. The returned kind is
/// informational only (every resume delegate call site ignores it; the checkpoint the function wrote is
/// authoritative). The checkpoint base URL it advertises names <em>this</em> deployment's checkpoint surface (§6b),
/// whose per-run coordinator holds the run's write-sequence and etag while the function's fire-and-forget saves land,
/// so a run checkpoints back to the runner that dispatched it. It lives in the control-plane server rather than the
/// durable core because it is an HTTP transport, and Durability is deliberately transport-free.
/// </remarks>
public sealed class ServerlessRunExecutionBackend : IRunExecutionBackend
{
    private readonly HttpClient client;
    private readonly Func<WorkflowRun, CancellationToken, ValueTask<Uri>> functionUrl;
    private readonly string checkpointBaseUrl;
    private readonly Func<WorkflowRunId, string>? checkpointTokenIssuer;

    /// <summary>Initializes a new instance of the <see cref="ServerlessRunExecutionBackend"/> class.</summary>
    /// <param name="client">The HTTP client the function is invoked through; its auth and timeout are the deployment's concern.</param>
    /// <param name="functionUrl">Resolves the invoke URL of the serverless function deployed for a run's (base workflow, version, environment). The resolution is asynchronous because it reads the run's Deployed <c>WorkflowDeployment</c> from the store (ADR 0059); <see cref="DeployedFunctionUrlResolver.ForStore"/> is the production resolver. It throws when the run has no deployed function, which leaves the run claimable for retry.</param>
    /// <param name="checkpointBaseUrl">The base URL of this deployment's checkpoint surface (§6b) the invoked function checkpoints back to; the function posts <c>runs/{id}/checkpoint</c> relative to it.</param>
    /// <param name="checkpointTokenIssuer">An optional run-scoped checkpoint-token issuer (ADR 0062): given a run, it mints the bearer token the invoked function presents on its checkpoint callbacks, which a publicly reachable checkpoint surface validates. When <see langword="null"/>, no token is carried (the checkpoint surface is not token-authenticated).</param>
    public ServerlessRunExecutionBackend(HttpClient client, Func<WorkflowRun, CancellationToken, ValueTask<Uri>> functionUrl, Uri checkpointBaseUrl, Func<WorkflowRunId, string>? checkpointTokenIssuer = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(functionUrl);
        ArgumentNullException.ThrowIfNull(checkpointBaseUrl);
        this.client = client;
        this.functionUrl = functionUrl;
        this.checkpointBaseUrl = checkpointBaseUrl.ToString();
        this.checkpointTokenIssuer = checkpointTokenIssuer;
    }

    /// <inheritdoc/>
    public RunIsolationModel IsolationModel => RunIsolationModel.Isolated;

    /// <summary>Gets this backend as the <see cref="WorkflowResumer"/> delegate the worker and management client consume.</summary>
    /// <returns>The resumer delegate.</returns>
    public WorkflowResumer AsResumer() => this.AdvanceAsync;

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunResultKind> AdvanceAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        Uri url = await this.functionUrl(run, cancellationToken).ConfigureAwait(false);
        string? checkpointToken = this.checkpointTokenIssuer?.Invoke(run.Id);
        byte[] body = PersistedJson.ToArray(
            (RunId: run.Id.Value, Environment: run.Environment, CheckpointUrl: this.checkpointBaseUrl, CheckpointToken: checkpointToken),
            static (Utf8JsonWriter writer, in (string RunId, string? Environment, string CheckpointUrl, string? CheckpointToken) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString("runId"u8, s.RunId);
                if (s.Environment is not null)
                {
                    writer.WriteString("environment"u8, s.Environment);
                }

                writer.WriteString("checkpointUrl"u8, s.CheckpointUrl);
                if (s.CheckpointToken is not null)
                {
                    writer.WriteString("checkpointToken"u8, s.CheckpointToken);
                }

                writer.WriteEndObject();
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } },
        };

        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // A failed invocation leaves the run claimable, so the dispatcher's lease and retry re-claim and
            // re-invoke it — the same recovery an in-process advance failure gets.
            ServerThrowHelper.ThrowServerlessRunHostFailed(response.StatusCode, run.Id, url);
        }

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ParseOutcome(payload);
    }

    /// <inheritdoc/>
    public ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        => ValueTask.CompletedTask; // the platform keeps the baked per-version function warm; there is nothing to pre-warm from here

    private static WorkflowRunResultKind ParseOutcome(ReadOnlyMemory<byte> payload)
    {
        using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
        string? outcome = document.RootElement.TryGetProperty("outcome"u8, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

        // The function returns the tri-state outcome, or a null/absent one when it found the run not dispatchable (an
        // at-least-once duplicate of a settled run). This kind is informational — the checkpoint the function wrote is
        // authoritative — so a null outcome maps to a benign Suspended rather than inventing a terminal state.
        return outcome switch
        {
            "Completed" => WorkflowRunResultKind.Completed,
            "Faulted" => WorkflowRunResultKind.Faulted,
            _ => WorkflowRunResultKind.Suspended,
        };
    }
}