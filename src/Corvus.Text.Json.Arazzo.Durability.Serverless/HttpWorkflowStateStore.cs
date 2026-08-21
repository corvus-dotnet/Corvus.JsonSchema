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
/// under the lease it already holds, re-projecting the index from the opaque bytes (ADR 0055).
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

    // Bounded resends of one sequence's bytes. A save lost in transport has to be retried rather than skipped, but an
    // unbounded retry would hold the run's chain against a server that is not coming back.
    private const int SendAttempts = 3;

    private readonly HttpClient client;
    private readonly object gate = new();

    // The tail of each run's dispatch chain. ADR 0065 decision 6 allows at most one save per run in flight, and the
    // interlock is per RUN rather than per component: the runner and the listener both author checkpoints for the
    // same run, so a lock held per component would let two well-behaved tenant components dispatch concurrently.
    // Keyed by the run's full address (ADR 0065 decision 9) so different runs never wait on each other.
    private readonly Dictionary<WorkflowRunAddress, Task> tails = [];
    private readonly List<Task<bool>> pending = [];

    /// <summary>Initializes a new instance of the <see cref="HttpWorkflowStateStore"/> class.</summary>
    /// <param name="client">The HTTP client whose base address is the dispatching runner's checkpoint surface.</param>
    public HttpWorkflowStateStore(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CheckpointPath(address));
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // The sequence is not seeded here: it travels inside the checkpoint the run authors, so the run continues its
        // own series from the document it just loaded rather than from a counter this store keeps alongside it. Two
        // counters for one series can only ever drift apart, and in a sealed environment the document's copy is inside
        // the MAC while a header is not.
        return new WorkflowCheckpoint(bytes, new WorkflowEtag(response.Headers.ETag?.Tag));
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        // The runner re-projects the index from the bytes, so only the bytes and the write-sequence go over the wire.
        // The sequence comes from the document the run authored — one series, one author. Reading it is a forward-only
        // scan that stops at the property rather than a parse of the whole checkpoint.
        WorkflowCheckpointSerializer.TryReadSequence(checkpointUtf8, out long seq);

        // Copy the bytes: the caller may reuse its buffer once we return, but the POST outlives the call, and a retry
        // is a byte-identical resend of the same sequence rather than a re-authoring.
        byte[] body = checkpointUtf8.ToArray();
        Task<bool> post;
        lock (this.gate)
        {
            Task previous = this.tails.TryGetValue(address, out Task? tail) ? tail : Task.CompletedTask;
            post = this.DispatchAfterAsync(previous, address, body, seq, cancellationToken);
            this.tails[address] = post;
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
            ThrowHelper.ThrowTerminalCheckpointNotCommitted();
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

    // The address rides the URL (ADR 0065 decision 9): the runner-side surface keys the run by (environment, runId),
    // and the callback token binds the same address, so a guest cannot re-address its checkpoints.
    private static string CheckpointPath(in WorkflowRunAddress address)
        => $"environments/{Uri.EscapeDataString(address.Environment)}/runs/{Uri.EscapeDataString(address.RunId.Value)}/checkpoint";

    private static long ReadSequence(HttpHeaders headers)
        => headers.TryGetValues(WriteSequenceHeader, out IEnumerable<string>? values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seq)
            ? seq
            : 0;

    // Runs this save once the run's previous one has finished, so at most one is ever in flight for a run. The previous
    // task never faults (a failed send is a false, not an exception), but it is awaited defensively so one broken link
    // cannot stall the rest of the chain.
    private async Task<bool> DispatchAfterAsync(Task previous, WorkflowRunAddress address, byte[] body, long seq, CancellationToken cancellationToken)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The predecessor's outcome is its own caller's business; this save still has to be dispatched.
        }

        return await this.PostCheckpointAsync(address, body, seq, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> PostCheckpointAsync(WorkflowRunAddress address, byte[] body, long seq, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < SendAttempts; ++attempt)
        {
            if (await this.TrySendAsync(address, body, seq, attempt, cancellationToken).ConfigureAwait(false) is { } settled)
            {
                return settled;
            }
        }

        return false;
    }

    // Sends once. Answers null to ask for another attempt, so the retry decision lives in one place rather than being
    // spread across the transport and status paths.
    private async Task<bool?> TrySendAsync(WorkflowRunAddress address, byte[] body, long seq, int attempt, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, CheckpointPath(address))
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(WriteSequenceHeader, seq.ToString(CultureInfo.InvariantCulture));
            using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // A refusal carries the sequence the server will accept next (ADR 0065 decision 6). Adopt it, so the next
            // save proposes that value. Without this the run is stranded by the first send that never lands: the local
            // counter has advanced past what the store persisted, the server accepts only persisted plus one, and every
            // subsequent save is refused for the same reason as the last.
            // A 409 means the store will not take this sequence, and resending the identical bytes cannot change that:
            // the run has to author the next checkpoint. Renumbering here instead would desynchronise the header from
            // the sequence inside the document, which a sealed environment's MAC covers and a header never is.
            return response.StatusCode == HttpStatusCode.Conflict ? false : Retry(attempt);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // A transport failure loses the save without the store having refused it, so the same bytes are resent
            // under the same sequence. Skipping to the next checkpoint instead would leave a hole the store will not
            // accept, and every later save would be refused for a gap the runner could no longer close.
            return cancellationToken.IsCancellationRequested ? false : Retry(attempt);
        }

        // Whether to try again, or settle as failed because the attempts are spent. No delay between attempts: the
        // run's next checkpoint is waiting behind this one, so backing off here backs the whole run off.
        static bool? Retry(int attempt) => attempt + 1 < SendAttempts ? null : false;
    }
}