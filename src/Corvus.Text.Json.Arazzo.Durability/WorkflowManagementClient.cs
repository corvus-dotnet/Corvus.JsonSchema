// <copyright file="WorkflowManagementClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The default <see cref="IWorkflowManagementClient"/> over an <see cref="IWorkflowStateStore"/> (plan §11).
/// Visibility queries use the store's <see cref="IWorkflowWaitIndex"/> (the same index Tier 2 uses for wakeups);
/// control operations take a single-owner lease and write under optimistic concurrency. Resuming a faulted run
/// re-executes it through a host-supplied <see cref="WorkflowResumer"/> — the same adapter a
/// <see cref="WorkflowWorker"/> uses — so the run advances from its last checkpoint (the faulted step), and the
/// generated executor clears the fault on its next checkpoint.
/// </summary>
public sealed class WorkflowManagementClient : IWorkflowManagementClient
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowWaitIndex? index;
    private readonly WorkflowResumer? resumer;
    private readonly TimeProvider timeProvider;
    private readonly string owner;
    private readonly TimeSpan leaseTtl;

    /// <summary>Initializes a new instance of the <see cref="WorkflowManagementClient"/> class.</summary>
    /// <param name="store">The state store. Visibility queries (<see cref="ListAsync"/>/<see cref="PurgeAsync"/>) require it to also implement <see cref="IWorkflowWaitIndex"/>.</param>
    /// <param name="owner">This client's identity, used to take run leases.</param>
    /// <param name="resumer">The adapter that re-enters a run's generated executor; required for <see cref="ResumeAsync"/>.</param>
    /// <param name="timeProvider">The time source for index timestamps and lease TTLs; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a lease is held during a control operation; defaults to one minute.</param>
    public WorkflowManagementClient(
        IWorkflowStateStore store,
        string owner,
        WorkflowResumer? resumer = null,
        TimeProvider? timeProvider = null,
        TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(owner);
        this.store = store;
        this.index = store as IWorkflowWaitIndex;
        this.resumer = resumer;
        this.owner = owner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.leaseTtl = leaseTtl ?? TimeSpan.FromMinutes(1);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunId> StartAsync(string workflowId, JsonElement inputs, string? correlationId, IReadOnlyList<string>? tags, IReadOnlyList<SecurityTag>? securityTags, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);

        var id = new WorkflowRunId(Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));
        using WorkflowRun run = WorkflowRun.CreateNew(this.store, id, workflowId, inputs, this.timeProvider, correlationId, tags, securityTags);
        await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunId> StartIdempotentAsync(string workflowId, JsonElement inputs, string idempotencyKey, string? correlationId = null, IReadOnlyList<string>? tags = null, IReadOnlyList<SecurityTag>? securityTags = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var id = new WorkflowRunId(DeterministicRunId(workflowId, idempotencyKey));
        try
        {
            using WorkflowRun run = WorkflowRun.CreateNew(this.store, id, workflowId, inputs, this.timeProvider, correlationId, tags, securityTags);
            await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowConflictException)
        {
            // A run already exists for this (workflowId, idempotencyKey) — re-delivery / duplicate fire; no-op.
        }

        return id;
    }

    private static string DeterministicRunId(string workflowId, string idempotencyKey)
    {
        int count = System.Text.Encoding.UTF8.GetByteCount(workflowId) + 1 + System.Text.Encoding.UTF8.GetByteCount(idempotencyKey);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            int written = System.Text.Encoding.UTF8.GetBytes(workflowId, buffer);
            buffer[written++] = 0;
            written += System.Text.Encoding.UTF8.GetBytes(idempotencyKey, buffer.AsSpan(written));
            Span<byte> hash = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(buffer.AsSpan(0, written), hash);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, CancellationToken cancellationToken)
        => this.RequireIndex().QueryAsync(query, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);
        return new WorkflowRunDetail(state.RunId, state.WorkflowId, state.Status, state.Cursor, state.CreatedAt, state.Wait, state.Fault, cp.Etag, state.CorrelationId, state.Tags, state.SecurityTags);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, CancellationToken cancellationToken)
    {
        if (this.resumer is null)
        {
            throw new InvalidOperationException("This management client was created without a resumer; ResumeAsync requires one.");
        }

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.resume");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.RunIdTag, id.Value);
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
            activity.SetTag(ArazzoTelemetry.ResumeModeTag, options.Mode.ToString());
        }

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            // Another owner (operator or worker) is acting on this run.
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            // For every mode but a plain retry, mutate the checkpoint (cursor/state) under optimistic concurrency
            // before re-entering: rewind the cursor, skip past the faulted step, or apply a state patch. The run
            // stays Faulted, so the re-entered executor still clears the fault on its first checkpoint.
            if (options.Mode != ResumeMode.RetryFaultedStep &&
                !await this.TryApplyResumeMutationAsync(id, options, activity, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, id, this.timeProvider, cancellationToken).ConfigureAwait(false);
            if (run is null || run.Status != WorkflowRunStatus.Faulted)
            {
                // Only a faulted run is retriable; it may have been resumed, cancelled, or deleted meanwhile.
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "not-faulted");
                return false;
            }

            // Re-enter the executor at the (possibly mutated) cursor; its first checkpoint clears the fault and sets Running.
            await this.resumer(run, cancellationToken).ConfigureAwait(false);

            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(ArazzoTelemetry.WorkflowIdTag, run.WorkflowId);
                activity.SetTag(ArazzoTelemetry.OutcomeTag, "resumed");
                if (run.CorrelationId is { } cid)
                {
                    activity.SetTag(ArazzoTelemetry.CorrelationIdTag, cid);
                }
            }

            ArazzoTelemetry.WorkflowsResumed.Add(
                1,
                new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, run.WorkflowId),
                new KeyValuePair<string, object?>(ArazzoTelemetry.ResumeModeTag, options.Mode.ToString()));
            return true;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.cancel");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.RunIdTag, id.Value);
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
            activity.SetTag("corvus.arazzo.cancel_reason", reason);
        }

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
            if (checkpoint is not { } cp)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
                return false;
            }

            byte[] updated;
            WorkflowRunIndexEntry indexEntry;
            string workflowId;
            using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8))
            {
                if (state.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled)
                {
                    // Terminal already; nothing to cancel.
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, "already-terminal");
                    return false;
                }

                workflowId = state.WorkflowId;

                // Mark cancelled, clear any wait, but keep the fault record (if any) for post-mortem visibility.
                // The run-creation metadata (correlation id + tags) is immutable, so carry it through unchanged.
                updated = WorkflowCheckpointSerializer.Serialize(
                    state.RunId,
                    state.WorkflowId,
                    WorkflowRunStatus.Cancelled,
                    state.Cursor,
                    state.CreatedAt,
                    state.RetryCounters,
                    state.CorrelationTokens,
                    state.Inputs,
                    state.StepOutputs,
                    state.Outputs,
                    wait: null,
                    fault: state.Fault,
                    correlationId: state.CorrelationId,
                    tags: state.Tags,
                    securityTags: state.SecurityTags);

                indexEntry = new WorkflowRunIndexEntry(
                    state.WorkflowId,
                    WorkflowRunStatus.Cancelled,
                    state.CreatedAt,
                    this.timeProvider.GetUtcNow(),
                    ErrorType: state.Fault?.Error,
                    CorrelationId: state.CorrelationId,
                    Tags: state.Tags,
                    SecurityTags: state.SecurityTags);

                if (activity is { IsAllDataRequested: true } && state.CorrelationId is { } cid)
                {
                    activity.SetTag(ArazzoTelemetry.CorrelationIdTag, cid);
                }
            }

            await this.store.SaveAsync(id, updated, indexEntry, cp.Etag, cancellationToken).ConfigureAwait(false);

            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(ArazzoTelemetry.WorkflowIdTag, workflowId);
                activity.SetTag(ArazzoTelemetry.OutcomeTag, "cancelled");
            }

            ArazzoTelemetry.WorkflowsCancelled.Add(1, new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, workflowId));
            return true;
        }
        catch (WorkflowConflictException)
        {
            // A worker or another operator wrote concurrently; the caller can retry.
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "conflict");
            return false;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> PurgeAsync(WorkflowPurgeQuery query, CancellationToken cancellationToken)
    {
        IWorkflowWaitIndex waitIndex = this.RequireIndex();

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.purge");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
            activity.SetTag("corvus.arazzo.older_than", query.OlderThan.ToString("O"));
        }

        int purged = 0;
        foreach (WorkflowRunStatus status in new[] { WorkflowRunStatus.Completed, WorkflowRunStatus.Cancelled })
        {
            // Page through every run of this status (keyset paging is unaffected by the deletions we make).
            string? token = null;
            do
            {
                WorkflowRunPage page = await waitIndex.QueryAsync(new WorkflowQuery(status, null, query.Limit, token), cancellationToken).ConfigureAwait(false);
                foreach (WorkflowRunListing listing in page.Runs)
                {
                    if (purged >= query.Limit)
                    {
                        activity?.SetTag("corvus.arazzo.purged_count", purged);
                        ArazzoTelemetry.WorkflowsPurged.Add(purged);
                        return purged;
                    }

                    if (listing.Index.UpdatedAt >= query.OlderThan)
                    {
                        continue;
                    }

                    await this.store.DeleteAsync(listing.Id, cancellationToken).ConfigureAwait(false);
                    purged++;
                }

                token = page.ContinuationToken;
            }
            while (token is not null);
        }

        activity?.SetTag("corvus.arazzo.purged_count", purged);
        if (purged > 0)
        {
            ArazzoTelemetry.WorkflowsPurged.Add(purged);
        }

        return purged;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.delete");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.RunIdTag, id.Value);
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
        }

        // Take the lease so we don't delete a run a worker or operator is mid-operation on.
        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
            if (checkpoint is null)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
                return false;
            }

            await this.store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "deleted");
            ArazzoTelemetry.WorkflowsDeleted.Add(1);
            return true;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies a resume mode's mutation (rewind / skip / state-patch) to a faulted run's checkpoint under
    /// optimistic concurrency, leaving it Faulted at the new cursor/state ready for the executor to re-enter.
    /// </summary>
    private async ValueTask<bool> TryApplyResumeMutationAsync(WorkflowRunId id, ResumeOptions options, Activity? activity, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return false;
        }

        byte[] mutated;
        WorkflowRunIndexEntry indexEntry;

        // The patched/composed context document must outlive the call to Serialize that reads its elements.
        ParsedJsonDocument<JsonElement>? patchedContext = null;
        try
        {
            using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8))
            {
                if (state.Status != WorkflowRunStatus.Faulted)
                {
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, "not-faulted");
                    return false;
                }

                int cursor = state.Cursor;
                JsonElement inputs = state.Inputs;
                Dictionary<string, JsonElement> stepOutputs = state.StepOutputs;

                switch (options.Mode)
                {
                    case ResumeMode.Rewind:
                        cursor = options.TargetCursor
                            ?? throw new ArgumentException("Rewind requires a target cursor.", nameof(options));
                        break;

                    case ResumeMode.Skip:
                        if (state.Fault is { } fault && options.SkipOutputs.ValueKind != JsonValueKind.Undefined)
                        {
                            stepOutputs[fault.StepId] = options.SkipOutputs;
                        }

                        cursor = options.TargetCursor ?? state.Cursor + 1;
                        break;

                    case ResumeMode.StatePatch:
                        if (!TryApplyStatePatch(state.Inputs, state.StepOutputs, options.Patch, out patchedContext))
                        {
                            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "patch-failed");
                            return false;
                        }

                        JsonElement context = patchedContext!.RootElement;
                        inputs = context.TryGetProperty("inputs"u8, out JsonElement patchedInputs) ? patchedInputs : default;
                        stepOutputs = ReadStepOutputs(context);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown resume mode.");
                }

                // Carry the immutable run-creation metadata (correlation id + tags) through the mutation.
                mutated = WorkflowCheckpointSerializer.Serialize(
                    state.RunId,
                    state.WorkflowId,
                    WorkflowRunStatus.Faulted,
                    cursor,
                    state.CreatedAt,
                    state.RetryCounters,
                    state.CorrelationTokens,
                    inputs,
                    stepOutputs,
                    default,
                    wait: null,
                    fault: state.Fault,
                    correlationId: state.CorrelationId,
                    tags: state.Tags,
                    securityTags: state.SecurityTags);

                indexEntry = new WorkflowRunIndexEntry(
                    state.WorkflowId,
                    WorkflowRunStatus.Faulted,
                    state.CreatedAt,
                    this.timeProvider.GetUtcNow(),
                    ErrorType: state.Fault?.Error,
                    CorrelationId: state.CorrelationId,
                    Tags: state.Tags,
                    SecurityTags: state.SecurityTags);
            }
        }
        finally
        {
            patchedContext?.Dispose();
        }

        try
        {
            await this.store.SaveAsync(id, mutated, indexEntry, cp.Etag, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (WorkflowConflictException)
        {
            // A worker or another operator wrote concurrently; the caller can retry.
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "conflict");
            return false;
        }
    }

    /// <summary>
    /// Applies an RFC 6902 JSON Patch to a run's context — the object <c>{ "inputs": …, "stepOutputs": { … } }</c> —
    /// returning the patched document for the caller to read the new inputs/step outputs from.
    /// </summary>
    private static bool TryApplyStatePatch(
        in JsonElement inputs,
        Dictionary<string, JsonElement> stepOutputs,
        in JsonElement patch,
        out ParsedJsonDocument<JsonElement>? patched)
    {
        byte[] contextBytes = ComposeContext(inputs, stepOutputs);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(contextBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        JsonPatchDocument patchDocument = patch;
        if (!root.TryValidateAndApplyPatch(in patchDocument))
        {
            patched = null;
            return false;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            root.WriteTo(writer);
        }

        patched = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        return true;
    }

    private static byte[] ComposeContext(in JsonElement inputs, Dictionary<string, JsonElement> stepOutputs)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            // Omit undefined values rather than writing null: "not present" is Undefined.
            if (inputs.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("inputs"u8);
                inputs.WriteTo(writer);
            }

            writer.WriteStartObject("stepOutputs"u8);
            foreach (KeyValuePair<string, JsonElement> step in stepOutputs)
            {
                if (step.Value.ValueKind == JsonValueKind.Undefined)
                {
                    continue;
                }

                writer.WritePropertyName(step.Key);
                step.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static Dictionary<string, JsonElement> ReadStepOutputs(in JsonElement context)
    {
        Dictionary<string, JsonElement> stepOutputs = [];
        if (context.TryGetProperty("stepOutputs"u8, out JsonElement stepOutputsElement))
        {
            foreach (JsonProperty<JsonElement> step in stepOutputsElement.EnumerateObject())
            {
                stepOutputs[step.Name] = step.Value;
            }
        }

        return stepOutputs;
    }

    private IWorkflowWaitIndex RequireIndex()
        => this.index ?? throw new NotSupportedException("The state store must implement IWorkflowWaitIndex for visibility queries (ListAsync/PurgeAsync).");
}