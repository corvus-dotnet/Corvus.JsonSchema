// <copyright file="HttpWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless;

/// <summary>
/// The serverless function-side <see cref="IWorkflowCheckpointStore"/>: it proxies a run's checkpoint load and
/// save to the dispatching runner's HTTP checkpoint surface, so a baked, Native-AOT-compiled function binds no
/// database SDK and holds no store credentials. The runner — a normal host — terminates these into the real store
/// under the lease it already holds, re-projecting the index from the opaque bytes (ADR 0028).
/// </summary>
/// <remarks>
/// <para>
/// Saves are <em>fire-and-forget</em>: the POST is issued but not awaited, so per-step checkpointing adds no
/// network round-trip to the run's critical path. Each carries a per-run monotonic write-sequence, so the runner
/// drops any out-of-order or stale arrival and the single stored checkpoint only ever moves forward. A lost or
/// late interim checkpoint is a safe replay (runs are idempotent), so it is tolerated.
/// </para>
/// <para>
/// The terminal checkpoint must still be durable before the run's outcome is reported, so this store implements
/// <see cref="IWorkflowCheckpointFlush"/>: the host calls <see cref="FlushAsync"/> after the advance to await the
/// pending writes and confirm the last (terminal) one landed. Because it is checkpoint-only it implements just
/// <see cref="IWorkflowCheckpointStore"/> — a serverless function never leases or deletes.
/// </para>
/// </remarks>
public sealed class HttpWorkflowStateStore : IWorkflowCheckpointStore, IWorkflowCheckpointFlush, IAsyncDisposable
{
    /// <summary>The header carrying the monotonic write-sequence on a checkpoint request and load response.</summary>
    public const string WriteSequenceHeader = "X-Arazzo-Checkpoint-Seq";

    private readonly HttpClient client;
    private readonly object gate = new();
    private readonly Dictionary<string, long> writeSequences = [];
    private readonly List<Task<bool>> pending = [];

    /// <summary>Initializes a new instance of the <see cref="HttpWorkflowStateStore"/> class.</summary>
    /// <param name="client">The HTTP client whose base address is the dispatching runner's checkpoint surface.</param>
    public HttpWorkflowStateStore(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CheckpointPath(id));
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // Seed the run's write-sequence from the runner so a warm function instance advancing a run picks up where
        // the last invocation left off, keeping the sequence monotonic across the run's whole life.
        long seq = ReadSequence(response.Headers);
        lock (this.gate)
        {
            this.writeSequences[id.Value] = seq;
        }

        return new WorkflowCheckpoint(bytes, new WorkflowEtag(response.Headers.ETag?.Tag));
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        // The runner re-projects the index from the bytes, so only the bytes and the write-sequence go over the wire.
        long seq;
        lock (this.gate)
        {
            seq = (this.writeSequences.TryGetValue(id.Value, out long last) ? last : 0) + 1;
            this.writeSequences[id.Value] = seq;
        }

        // Copy the bytes: the caller may reuse its buffer once we return, but the fire-and-forget POST outlives the call.
        Task<bool> post = this.PostCheckpointAsync(id, checkpointUtf8.ToArray(), seq, cancellationToken);
        lock (this.gate)
        {
            this.pending.Add(post);
        }

        // The function does not thread the etag chain (the runner does, as sole writer under the lease); the returned
        // value is advisory, so the write-sequence stands in for it.
        return new ValueTask<WorkflowEtag>(new WorkflowEtag(seq.ToString(CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc/>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        Task<bool>[] inFlight;
        lock (this.gate)
        {
            if (this.pending.Count == 0)
            {
                return;
            }

            inFlight = [.. this.pending];
            this.pending.Clear();
        }

        bool[] committed = await Task.WhenAll(inFlight).ConfigureAwait(false);

        // The last-issued POST carries the highest write-sequence — the terminal state. Interim failures are
        // tolerated (superseded by a later checkpoint, or a safe idempotent replay), but the terminal must be
        // durable or the run's outcome cannot be reported.
        if (!committed[^1])
        {
            throw new InvalidOperationException(
                "The terminal checkpoint did not commit to the dispatching runner; the run stays claimable for re-invocation.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Drain any still-pending fire-and-forget writes so none dangle. A dropped one is a safe replay, so disposal
        // does not surface their outcome — FlushAsync is the barrier that does.
        Task<bool>[] inFlight;
        lock (this.gate)
        {
            inFlight = [.. this.pending];
            this.pending.Clear();
        }

        if (inFlight.Length > 0)
        {
            await Task.WhenAll(inFlight).ConfigureAwait(false);
        }
    }

    private static string CheckpointPath(WorkflowRunId id) => $"runs/{Uri.EscapeDataString(id.Value)}/checkpoint";

    private static long ReadSequence(HttpHeaders headers)
        => headers.TryGetValues(WriteSequenceHeader, out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seq)
            ? seq
            : 0;

    private async Task<bool> PostCheckpointAsync(WorkflowRunId id, byte[] body, long seq, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, CheckpointPath(id))
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(WriteSequenceHeader, seq.ToString(CultureInfo.InvariantCulture));
            using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Fire-and-forget: a failed interim is tolerated; FlushAsync decides whether the terminal landed.
            return false;
        }
    }
}