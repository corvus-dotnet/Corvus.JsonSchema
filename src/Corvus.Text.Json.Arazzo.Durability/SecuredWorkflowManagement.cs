// <copyright file="SecuredWorkflowManagement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Patch;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The default <see cref="ISecuredWorkflowManagement"/> over an <see cref="IWorkflowStateStore"/> (plan §11).
/// Visibility queries use the store's <see cref="IWorkflowWaitIndex"/> (the same index Tier 2 uses for wakeups);
/// control operations take a single-owner lease and write under optimistic concurrency. Resuming a faulted run
/// re-executes it through a host-supplied <see cref="WorkflowResumer"/> — the same adapter a
/// <see cref="WorkflowWorker"/> uses — so the run advances from its last checkpoint (the faulted step), and the
/// generated executor clears the fault on its next checkpoint.
/// </summary>
public sealed class SecuredWorkflowManagement : ISecuredWorkflowManagement
{
    private const int DefaultBufferSize = 512;

    private readonly IWorkflowStateStore store;
    private readonly IWorkflowWaitIndex? index;
    private readonly WorkflowResumer? resumer;
    private readonly TimeProvider timeProvider;
    private readonly string owner;
    private readonly TimeSpan leaseTtl;

    /// <summary>Initializes a new instance of the <see cref="SecuredWorkflowManagement"/> class.</summary>
    /// <param name="store">The state store. Visibility queries (<see cref="ListAsync"/>/<see cref="PurgeAsync"/>) require it to also implement <see cref="IWorkflowWaitIndex"/>.</param>
    /// <param name="owner">This client's identity, used to take run leases.</param>
    /// <param name="resumer">The adapter that re-enters a run's generated executor; required for <see cref="ResumeAsync"/>.</param>
    /// <param name="timeProvider">The time source for index timestamps and lease TTLs; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a lease is held during a control operation; defaults to one minute.</param>
    public SecuredWorkflowManagement(
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
    public async ValueTask<WorkflowRunId> StartAsync(string workflowId, JsonElement inputs, string? correlationId, TagSet tags, SecurityTagSet securityTags, string? environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);

        var id = new WorkflowRunId(Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));
        using WorkflowRun run = WorkflowRun.CreateNew(this.store, id, workflowId, inputs, this.timeProvider, correlationId, tags, securityTags, environment);
        await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunId> StartIdempotentAsync(string workflowId, JsonElement inputs, string idempotencyKey, string? correlationId = null, TagSet tags = default, SecurityTagSet securityTags = default, CancellationToken cancellationToken = default)
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
        // Upper bound (GetMaxByteCount is a multiply, not a scan) to size the scratch — the exact filled length comes from
        // the GetBytes returns below, and the hash is taken over buffer[..written].
        int maxCount = System.Text.Encoding.UTF8.GetMaxByteCount(workflowId.Length) + 1 + System.Text.Encoding.UTF8.GetMaxByteCount(idempotencyKey.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxCount);
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

    // Re-presents an opaque page token (the store's pooled UTF-8) as the JSON string value the query seam carries, for
    // an in-process paging loop (purge) that feeds a previous page's NextPageToken into the next query. The parsed
    // document references the quoted buffer; dispose it once the query has consumed the token.
    private static ParsedJsonDocument<JsonString> WrapContinuationToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Scope the listing to the caller's read reach (§14.2); the store applies the filter in its query. Refuse
        // (rather than leak) if the store does not push the reach filter down.
        SecurityFilter? reach = context.Reach(AccessVerb.Read);
        IWorkflowWaitIndex index = this.RequireIndex();
        RowSecurityPushdown.EnsureSupported(reach, index);
        return index.QueryAsync(query with { Security = reach }, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);

        // A run outside the caller's read reach is reported as absent (non-disclosing, §14.2).
        if (!context.Admits(AccessVerb.Read, state.SecurityTags))
        {
            return null;
        }

        return new WorkflowRunDetail(state.RunId, state.WorkflowId, state.Status, state.Cursor, state.CreatedAt, state.Wait, state.Fault, cp.Etag, state.CorrelationId, state.Tags, state.SecurityTags, state.Environment);
    }

    // Whether a run is within the caller's write reach (§14.2): unrestricted writers and a missing run pass (the
    // operation then reports its own not-found / no-op); otherwise the run's security tags must satisfy the reach.
    private async ValueTask<bool> IsWithinWriteReachAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        if (context.WriteReach is null)
        {
            return true;
        }

        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return true;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);
        return context.Admits(AccessVerb.Write, state.SecurityTags);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (this.resumer is null)
        {
            throw new InvalidOperationException("This management client was created without a resumer; ResumeAsync requires one.");
        }

        // A run outside the caller's write reach is not actionable (§14.2).
        if (!await this.IsWithinWriteReachAsync(id, context, cancellationToken).ConfigureAwait(false))
        {
            return false;
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
    public async ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's write reach is not actionable (§14.2).
        if (!await this.IsWithinWriteReachAsync(id, context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

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
            using (ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(cp.Utf8))
            {
                JsonElement root = document.RootElement;
                WorkflowRunStatus status = Enum.Parse<WorkflowRunStatus>(root.GetProperty("status"u8).GetString() ?? nameof(WorkflowRunStatus.Pending));
                if (status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled)
                {
                    // Terminal already; nothing to cancel.
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, "already-terminal");
                    return false;
                }

                workflowId = root.GetProperty("workflowId"u8).GetString() ?? string.Empty;
                string? correlationId = root.TryGetProperty("correlationId"u8, out JsonElement correlationIdElement) ? correlationIdElement.GetString() : null;
                DateTimeOffset createdAt = root.TryGetProperty("createdAt"u8, out JsonElement createdAtElement) ? createdAtElement.GetDateTimeOffset() : default;
                string? errorType = root.TryGetProperty("fault"u8, out JsonElement faultElement) && faultElement.TryGetProperty("error"u8, out JsonElement errorElement) ? errorElement.GetString() : null;
                TagSet tags = root.TryGetProperty("tags"u8, out JsonElement tagsElement) ? TagSet.CopyFrom(tagsElement) : default;
                SecurityTagSet securityTags = WorkflowCheckpointSerializer.ReadSecurityTags(root);

                // Mark cancelled and clear any wait by rewriting the document verbatim — the run-creation metadata and
                // the working state (retry counters, correlation tokens, step outputs) are carried through as raw JSON,
                // not deserialized into dictionaries only to be re-serialized unchanged.
                updated = WorkflowCheckpointSerializer.RewriteStatus(cp.Utf8.Span, WorkflowRunStatus.Cancelled, dropWait: true);

                indexEntry = new WorkflowRunIndexEntry(
                    workflowId,
                    WorkflowRunStatus.Cancelled,
                    createdAt,
                    this.timeProvider.GetUtcNow(),
                    ErrorType: errorType,
                    CorrelationId: correlationId,
                    Tags: tags,
                    SecurityTags: securityTags);

                if (activity is { IsAllDataRequested: true } && correlationId is { } cid)
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
    public async ValueTask<int> PurgeAsync(WorkflowPurgeQuery query, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IWorkflowWaitIndex waitIndex = this.RequireIndex();

        // Refuse (rather than over-purge) if the store does not push the purge reach filter down (§14.2/§14.4).
        RowSecurityPushdown.EnsureSupported(context.Reach(AccessVerb.Purge), waitIndex);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.purge");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
            activity.SetTag("corvus.arazzo.older_than", query.OlderThan.ToString("O"));
        }

        int purged = 0;
        foreach (WorkflowRunStatus status in new[] { WorkflowRunStatus.Completed, WorkflowRunStatus.Cancelled })
        {
            // Page through every run of this status (keyset paging is unaffected by the deletions we make). This is an
            // in-process loop: re-present the page's opaque token (the store's pooled UTF-8) to the next query through
            // the JsonString seam (the store decodes it bytes-native).
            ParsedJsonDocument<JsonString>? tokenDoc = null;
            try
            {
                do
                {
                    // Reuse the row-filtered query path so the purge reaps only rows within the caller's purge reach
                    // (§14.2): a tenant admin purges only their tenant's runs, a service operator (null reach) purges all.
                    using WorkflowRunPage page = await waitIndex.QueryAsync(
                        new WorkflowQuery(status, null, query.Limit, tokenDoc?.RootElement ?? default, Security: context.Reach(AccessVerb.Purge)),
                        cancellationToken).ConfigureAwait(false);
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

                    // The previous token has been consumed by the query above; swap in the next page's (if any).
                    ParsedJsonDocument<JsonString>? consumed = tokenDoc;
                    tokenDoc = page.NextPageToken.IsEmpty ? null : WrapContinuationToken(page.NextPageToken.Span);
                    consumed?.Dispose();
                }
                while (tokenDoc is not null);
            }
            finally
            {
                tokenDoc?.Dispose();
            }
        }

        activity?.SetTag("corvus.arazzo.purged_count", purged);
        if (purged > 0)
        {
            ArazzoTelemetry.WorkflowsPurged.Add(purged);
        }

        return purged;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's write reach is not actionable (§14.2).
        if (!await this.IsWithinWriteReachAsync(id, context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

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

        // A state-patch resume builds a fresh step-outputs map (over the patched context) that this method owns and
        // must return to the pool; the other modes reuse the state's map, which the state disposes.
        PooledUtf8Map<JsonElement>? patchedStepOutputs = null;
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
                PooledUtf8Map<JsonElement> stepOutputs = state.StepOutputs;

                switch (options.Mode)
                {
                    case ResumeMode.Rewind:
                        cursor = options.TargetCursor
                            ?? throw new ArgumentException("Rewind requires a target cursor.", nameof(options));
                        break;

                    case ResumeMode.Skip:
                        if (state.Fault is { } fault && options.SkipOutputs.ValueKind != JsonValueKind.Undefined)
                        {
                            stepOutputs.Set(fault.StepId, options.SkipOutputs);
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
                        patchedStepOutputs = stepOutputs;
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
            patchedStepOutputs?.Dispose();
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
        PooledUtf8Map<JsonElement> stepOutputs,
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

        // Serialize the patched root through the pooled writer cache (reusing this method's workspace), then hand back a
        // pooled document the caller owns (ToPooledDocument copies into its own pooled buffer, so the scratch returns here).
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            root.WriteTo(writer);
            writer.Flush();
            patched = PersistedJson.ToPooledDocument<JsonElement>(buffer.WrittenSpan);
            return true;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private static byte[] ComposeContext(in JsonElement inputs, PooledUtf8Map<JsonElement> stepOutputs)
        => PersistedJson.ToArray(
            (Inputs: inputs, StepOutputs: stepOutputs),
            static (Utf8JsonWriter writer, in (JsonElement Inputs, PooledUtf8Map<JsonElement> StepOutputs) c) =>
            {
                writer.WriteStartObject();

                // Omit undefined values rather than writing null: "not present" is Undefined.
                if (c.Inputs.ValueKind != JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("inputs"u8);
                    c.Inputs.WriteTo(writer);
                }

                writer.WriteStartObject("stepOutputs"u8);
                PooledUtf8Map<JsonElement>.Enumerator step = c.StepOutputs.GetEnumerator();
                while (step.MoveNext())
                {
                    if (step.CurrentValue.ValueKind == JsonValueKind.Undefined)
                    {
                        continue;
                    }

                    writer.WritePropertyName(step.CurrentKey);
                    step.CurrentValue.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            });

    private static PooledUtf8Map<JsonElement> ReadStepOutputs(in JsonElement context)
    {
        if (!context.TryGetProperty("stepOutputs"u8, out JsonElement stepOutputsElement))
        {
            return PooledUtf8Map<JsonElement>.Rent(0);
        }

        var stepOutputs = PooledUtf8Map<JsonElement>.Rent(stepOutputsElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> step in stepOutputsElement.EnumerateObject())
        {
            using UnescapedUtf8JsonString name = step.Utf8NameSpan;
            stepOutputs.Set(name.Span, step.Value);
        }

        return stepOutputs;
    }

    private IWorkflowWaitIndex RequireIndex()
        => this.index ?? throw new NotSupportedException("The state store must implement IWorkflowWaitIndex for visibility queries (ListAsync/PurgeAsync).");
}