// <copyright file="ArazzoRunnerCheckpointsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Implements the runner API's checkpoint operations: the run row, read and written under a held lease. This is the
/// hottest surface in the system, so the checkpoint bytes are moved rather than modelled — a stream in, pooled bytes
/// out, and the document itself opaque to the server.
/// </summary>
public sealed class ArazzoRunnerCheckpointsHandler : IApiCheckpointsHandler
{
    private readonly WorkflowCheckpointCoordinator checkpoints;
    private readonly RunnerRunCoordinator coordinator;
    private readonly RunnerPrincipalAccessor principals;
    private readonly RunnerApiOptions options;
    private readonly RunnerQuotaGate quotas;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerCheckpointsHandler"/> class.</summary>
    /// <param name="checkpoints">The checkpoint coordinator that terminates saves into the store.</param>
    /// <param name="coordinator">The store-facing coordinator, for the lease check every operation makes.</param>
    /// <param name="principals">Reads the authenticated machine principal from the current request.</param>
    /// <param name="quotas">Meters the deployment's checkpoint quotas.</param>
    /// <param name="options">The deployment's body bounds; defaults are used when omitted.</param>
    public ArazzoRunnerCheckpointsHandler(
        WorkflowCheckpointCoordinator checkpoints,
        RunnerRunCoordinator coordinator,
        RunnerPrincipalAccessor principals,
        RunnerQuotaGate quotas,
        RunnerApiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(principals);
        ArgumentNullException.ThrowIfNull(quotas);

        this.checkpoints = checkpoints;
        this.coordinator = coordinator;
        this.principals = principals;
        this.quotas = quotas;
        this.options = options ?? new RunnerApiOptions();
    }

    /// <inheritdoc/>
    public async ValueTask<LoadCheckpointResult> HandleLoadCheckpointAsync(LoadCheckpointParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return LoadCheckpointResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        // Metered before the lease is checked, so a caller hammering this surface is turned away without a store read.
        // Doing it the other way round would let a refused caller drive the store at whatever rate it liked, which is
        // the load the quota exists to stop.
        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Checkpoint, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return LoadCheckpointResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        var id = new WorkflowRunId((string)parameters.RunId);

        // Reading a run's state is a tenant-data read, so it takes the same interlock as writing it. The check is also
        // the non-disclosure rule: a run outside the bindings, one held by a peer, and one that does not exist all fail
        // here identically.
        if (!await this.coordinator.HoldsLeaseAsync(principal, id, (string)parameters.XArazzoLease, cancellationToken).ConfigureAwait(false))
        {
            return LoadCheckpointResult.Conflict(RunnerProblems.LeaseLost(), workspace);
        }

        CheckpointLoad? loaded = await this.checkpoints.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded is not { } load)
        {
            // The lease is current and the row is absent: the narrow, serious case. A runner holding a non-terminal
            // anchor entry for this run faults rather than treating it as fresh.
            return LoadCheckpointResult.NotFound(RunnerProblems.NoCheckpoint(), workspace);
        }

        return LoadCheckpointResult.Ok(load.Checkpoint, workspace, xArazzoCheckpointSeq: load.LastAppliedSequence);
    }

    /// <inheritdoc/>
    public async ValueTask<SaveCheckpointResult> HandleSaveCheckpointAsync(SaveCheckpointParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return SaveCheckpointResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Checkpoint, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return SaveCheckpointResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        var id = new WorkflowRunId((string)parameters.RunId);
        if (!await this.coordinator.HoldsLeaseAsync(principal, id, (string)parameters.XArazzoLease, cancellationToken).ConfigureAwait(false))
        {
            return SaveCheckpointResult.Conflict(RunnerProblems.LeaseLostOnWrite(), workspace);
        }

        long sequence = (long)parameters.XArazzoCheckpointSeq;
        (byte[] rented, int length, bool overCap) = await this.ReadBodyAsync(parameters.Body, cancellationToken).ConfigureAwait(false);
        try
        {
            if (overCap)
            {
                return SaveCheckpointResult.Status413(RunnerProblems.CheckpointTooLarge(), workspace);
            }

            // The volume is charged here: after the body is measured and before anything is written, so a refusal
            // persists nothing. Charging it on the way in is not possible, because a Content-Length is the caller's
            // claim about its own body and the cap above exists precisely because that claim may be a lie.
            if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.CheckpointBytes, principal, length, cancellationToken).ConfigureAwait(false) is { } tooMuch)
            {
                return SaveCheckpointResult.TooManyRequests(RunnerProblems.QuotaExceeded(tooMuch), workspace, RunnerQuotaGate.RetryAfterSeconds(tooMuch));
            }

            // Project (and thereby validate) the index from the received bytes here, so a malformed body is a clean 400
            // and the coordinator only ever handles a well-formed checkpoint. The same bytes are stored verbatim.
            ReadOnlyMemory<byte> checkpointUtf8 = rented.AsMemory(0, length);
            if (!WorkflowCheckpointSerializer.TryProjectIndex(checkpointUtf8, out WorkflowRunIndexEntry index))
            {
                return SaveCheckpointResult.BadRequest(RunnerProblems.MalformedCheckpoint(), workspace);
            }

            CheckpointSaveResult result = await this.checkpoints.SaveAsync(id, checkpointUtf8, index, sequence, cancellationToken).ConfigureAwait(false);
            return result.Outcome switch
            {
                CheckpointSaveOutcome.Applied => SaveCheckpointResult.NoContent(workspace, xArazzoCheckpointSeq: sequence),

                // A superseded save reported as success is indistinguishable from a durable write, which would leave a
                // runner's anchor committed to a checkpoint the store does not have. That is the one failure this
                // operation exists to make impossible, so it is a refusal carrying the sequence that would be accepted.
                CheckpointSaveOutcome.Superseded => SaveCheckpointResult.Conflict(RunnerProblems.Superseded(result.AcceptedSequence), workspace),
                _ => SaveCheckpointResult.Conflict(RunnerProblems.WriterConflict(result.AcceptedSequence), workspace),
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // Reads the request body into a pooled buffer. The generated endpoint hands over the raw stream, so the body never
    // becomes a modelled value: it is rented, projected, stored, and returned to the pool. Reports the over-cap case
    // rather than throwing, so a lying Content-Length cannot force an unbounded rent.
    private async ValueTask<(byte[] Rented, int Length, bool OverCap)> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        int cap = this.options.MaximumCheckpointBytes;

        // One byte past the cap is the largest buffer worth growing to: reading it is what distinguishes a body at the
        // cap from one over it, and stopping there is what keeps an over-cap body from costing twice the cap in rented
        // memory before it is refused.
        int limit = cap + 1;
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Min(8192, limit));
        int total = 0;

        // The pool rounds a rent up, so the window is tracked rather than read off the array: without it an over-cap
        // body would be read as far as whatever size the pool happened to hand back.
        int window = Math.Min(rented.Length, limit);
        try
        {
            while (true)
            {
                if (total == window)
                {
                    if (window >= limit)
                    {
                        return (rented, total, true);
                    }

                    byte[] bigger = ArrayPool<byte>.Shared.Rent(Math.Min(window * 2, limit));
                    Array.Copy(rented, bigger, total);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = bigger;
                    window = Math.Min(rented.Length, limit);
                }

                int read = await body.ReadAsync(rented.AsMemory(total, window - total), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return (rented, total, total > cap);
                }

                total += read;
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }
}