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
/// Visibility queries use the store's <see cref="IWorkflowWaitIndex"/> (the same index Tier 2 uses for wakeups),
/// and every bare-run-id operation resolves through the same reach-filtered index predicate the listing pushes
/// down (ADR 0065 §9), so visibility by id cannot drift from visibility in the list;
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
    private readonly WorkflowRunDerivation? runDerivation;
    private readonly Environments.IEnvironmentStore? environments;
    private readonly byte[] ownerGroupTagKey;

    /// <summary>Initializes a new instance of the <see cref="SecuredWorkflowManagement"/> class.</summary>
    /// <param name="store">The state store. Visibility queries (<see cref="ListAsync"/>/<see cref="PurgeAsync"/>) and every run-addressed operation (which resolves its bare id through the reach-filtered index query, ADR 0065 §9) require it to also implement <see cref="IWorkflowWaitIndex"/>.</param>
    /// <param name="owner">This client's identity, used to take run leases.</param>
    /// <param name="resumer">The adapter that re-enters a run's generated executor; required for <see cref="ResumeAsync"/>.</param>
    /// <param name="timeProvider">The time source for index timestamps and lease TTLs; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a lease is held during a control operation; defaults to one minute.</param>
    /// <param name="runDerivation">The deployment's run-id derivation (ADR 0065 §9); required for
    /// <see cref="StartIdempotentAsync"/>, which refuses without it. The same instance serves every surface that
    /// derives these ids (exposed through <see cref="RunDerivation"/>), so no two components can hold divergent keys.</param>
    /// <param name="environments">The environment registry the idempotent derivation resolves an environment's owner
    /// group from; <see langword="null"/> (a deployment without tenancy governance) derives with no owner group,
    /// consistently.</param>
    /// <param name="internalTagPrefix">The deployment's reserved internal tag prefix the owner group is stamped
    /// under; defaults to the <c>sys:</c> prefix.</param>
    public SecuredWorkflowManagement(
        IWorkflowStateStore store,
        string owner,
        WorkflowResumer? resumer = null,
        TimeProvider? timeProvider = null,
        TimeSpan? leaseTtl = null,
        WorkflowRunDerivation? runDerivation = null,
        Environments.IEnvironmentStore? environments = null,
        string? internalTagPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(owner);
        this.store = store;
        this.index = store as IWorkflowWaitIndex;
        this.resumer = resumer;
        this.owner = owner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.leaseTtl = leaseTtl ?? TimeSpan.FromMinutes(1);
        this.runDerivation = runDerivation;
        this.environments = environments;
        this.ownerGroupTagKey = internalTagPrefix is null
            ? Environments.OwnerGroupTag.DefaultKeyUtf8.ToArray()
            : Environments.OwnerGroupTag.KeyFor(internalTagPrefix);
    }

    /// <inheritdoc/>
    public WorkflowRunDerivation? RunDerivation => this.runDerivation;

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunId> StartAsync(string workflowId, JsonElement inputs, string? correlationId, TagSet tags, SecurityTagSet securityTags, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        var id = new WorkflowRunId(Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));
        using WorkflowRun run = WorkflowRun.CreateNew(this.store, id, workflowId, inputs, environment, this.timeProvider, correlationId, tags, securityTags);
        await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc/>
    public async ValueTask<IdempotentStartResult> StartIdempotentAsync(string workflowId, JsonElement inputs, string idempotencyKey, string environment, string? correlationId = null, TagSet tags = default, SecurityTagSet securityTags = default, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        if (this.runDerivation is not { } derivation)
        {
            throw ThrowHelper.GetIdempotentStartRequiresDerivationException();
        }

        string? ownerGroup = await this.ResolveOwnerGroupAsync(environment, cancellationToken).ConfigureAwait(false);
        WorkflowRunId id = derivation.IdempotentStart(ownerGroup, environment, workflowId, idempotencyKey);
        return await this.StartNamedAsync(id, workflowId, inputs, environment, correlationId, tags, securityTags, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IdempotentStartResult> StartNamedAsync(WorkflowRunId runId, string workflowId, JsonElement inputs, string environment, string? correlationId = null, TagSet tags = default, SecurityTagSet securityTags = default, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (!WorkflowRunId.IsWellFormed(runId.Value))
        {
            throw ThrowHelper.GetNamedRunIdOutsideGrammarException(runId.Value, nameof(runId));
        }

        try
        {
            using WorkflowRun run = WorkflowRun.CreateNew(this.store, runId, workflowId, inputs, environment, this.timeProvider, correlationId, tags, securityTags);
            await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
            return new IdempotentStartResult(runId, Created: true);
        }
        catch (WorkflowConflictException)
        {
            // A run already exists under this id. Only a run that IS this logical start (same workflow, same
            // environment) reads as the idempotent convergence; anything else occupying the id — the pre-created-id
            // attack the keyed derivation exists to prevent, a leaked key, or an in-process caller minting a
            // colliding name — is refused rather than reported as this run (ADR 0065 §9).
            WorkflowCheckpoint? existing = await this.store.LoadAsync(new WorkflowRunAddress(environment, runId), cancellationToken).ConfigureAwait(false);
            if (existing is { } checkpoint)
            {
                // The load was by the composite address, so the occupant IS in this environment structurally
                // (ADR 0065 decision 9) — only the workflow id can still diverge.
                WorkflowRunIndexEntry indexEntry = WorkflowCheckpointSerializer.ProjectIndex(checkpoint.Utf8);
                if (indexEntry.WorkflowId == workflowId)
                {
                    return new IdempotentStartResult(runId, Created: false);
                }
            }

            throw ThrowHelper.GetIdempotentRunCollisionException(runId.Value);
        }
    }

    // The owner group of the run's pinned environment (the sys:tenant management tag, ADR 0065), resolved through
    // the wired environment registry with system reach — the caller's reach gates the start itself at the surface,
    // not the derivation's inputs. No registry wired (a deployment without tenancy governance) resolves to no
    // group, consistently, so every start in such a deployment derives the same way.
    private async ValueTask<string?> ResolveOwnerGroupAsync(string environment, CancellationToken cancellationToken)
    {
        if (this.environments is not { } registry)
        {
            return null;
        }

        using ParsedJsonDocument<Environments.Environment>? doc = await registry.GetAsync(environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (doc is not { } environmentDoc)
        {
            return null;
        }

        return Environments.OwnerGroupTag.Read(environmentDoc.RootElement, this.ownerGroupTagKey);
    }

    // Re-presents an opaque page token (the store's pooled UTF-8) as the JSON string value the query seam carries, for
    // an in-process paging loop (purge) that feeds a previous page's NextPageToken into the next query. The generated
    // Create() escapes with the default encoder — byte-identical to a bare quote-wrap for our base64url tokens (the
    // equivalence is pinned by PageTokenWrapEscapeEquivalenceTests) and, unlike the old hand wrap, still VALID JSON if a
    // token ever carries an escapable byte. Dispose the document once the query has consumed the token.
    private static ParsedJsonDocument<JsonString> WrapContinuationToken(ReadOnlySpan<byte> tokenUtf8)
        => JsonString.Create(tokenUtf8);

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
    public ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Same read-reach scoping and pushdown guard as ListAsync — the count reuses the store's list predicate, so
        // it counts exactly the runs the caller could see (§14.2), never more.
        SecurityFilter? reach = context.Reach(AccessVerb.Read);
        IWorkflowWaitIndex index = this.RequireIndex();
        RowSecurityPushdown.EnsureSupported(reach, index);
        return index.CountAsync(query with { Security = reach }, cap, cancellationToken);
    }

    // Resolves a bare run id to its full (environment, runId) address through the store's reach-filtered index
    // query — the SAME predicate ListAsync pushes down (ADR 0065 §9) — so a run's visibility by id can never drift
    // from its visibility in the list, for any verb. A run outside the reach and a run the store does not hold
    // answer identically: not resolved. Under the composite key the same bare id can name a run in two
    // environments, both within reach (only by deliberate construction — canonical ids carry 128 bits of entropy);
    // a bare-id operation refuses to guess between them and fails closed, while the listing shows both.
    private async ValueTask<WorkflowRunAddress?> ResolveWithinReachAsync(WorkflowRunId id, AccessContext context, AccessVerb verb, CancellationToken cancellationToken)
    {
        SecurityFilter? reach = context.Reach(verb);
        IWorkflowWaitIndex index = this.RequireIndex();
        RowSecurityPushdown.EnsureSupported(reach, index);
        using WorkflowRunPage page = await index.QueryAsync(new WorkflowQuery(RunId: id.Value, Limit: 2, Security: reach), cancellationToken).ConfigureAwait(false);
        return page.Runs.Count == 1 ? page.Runs[0].Address : null;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's read reach is reported as absent (non-disclosing, §14.2), decided by the store's
        // index predicate — the one the listing pushes down — never by a second in-process check that could drift
        // from it (ADR 0065 §9, C4).
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Read, cancellationToken).ConfigureAwait(false) is not { } address)
        {
            return null;
        }

        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);
        return new WorkflowRunDetail(state.RunId, state.WorkflowId, state.Status, state.Cursor, state.CreatedAt, state.Wait, state.Fault, cp.Etag, state.CorrelationId, state.Tags, state.SecurityTags, state.Environment, state.UpdatedAt);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetStepJournalAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's read reach is reported as absent (non-disclosing, §14.2) — the same resolve as
        // GetAsync, because the journal discloses strictly more than the detail.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Read, cancellationToken).ConfigureAwait(false) is not { } address)
        {
            return null;
        }

        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);

        // The projection is written while the checkpoint state is alive (its elements are views into the
        // pooled document); the returned array is fully detached. Outputs are copied bytes-to-bytes.
        return PersistedJson.ToArray(state, static (Utf8JsonWriter writer, in WorkflowCheckpointState s) =>
        {
            writer.WriteStartObject();
            writer.WriteString("runId"u8, s.RunId.Value);
            writer.WriteStartArray("steps"u8);
            if (s.StepJournal.Count > 0)
            {
                // ADR 0050: project the per-step journal in execution order, each entry carrying its outcome, attempt,
                // and time window, joined to the step's outputs (from the products map) when it recorded any. Outputs
                // are copied bytes-to-bytes; the metadata is not sensitive and is never redacted.
                foreach (WorkflowStepJournalEntry entry in s.StepJournal)
                {
                    writer.WriteStartObject();
                    writer.WriteString("stepId"u8, entry.StepId);
                    writer.WriteString("status"u8, StepStatusName(entry.Status));
                    writer.WriteNumber("attempt"u8, entry.Attempt);
                    writer.WriteString("startedAt"u8, entry.StartedAt);
                    writer.WriteString("endedAt"u8, entry.EndedAt);
                    if (s.StepOutputs.TryGetValue(entry.StepId, out JsonElement stepOutputs) && stepOutputs.ValueKind != JsonValueKind.Undefined)
                    {
                        writer.WritePropertyName("outputs"u8);
                        stepOutputs.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                }
            }
            else
            {
                // Old-run fallback: a checkpoint written before the journal existed has step outputs but no journal, so
                // project the outputs alone, in the map's order, with no invented status or timing.
                PooledUtf8Map<JsonElement>.Enumerator entries = s.StepOutputs.GetEnumerator();
                while (entries.MoveNext())
                {
                    writer.WriteStartObject();
                    writer.WriteString("stepId"u8, entries.CurrentKey);
                    writer.WritePropertyName("outputs"u8);
                    entries.CurrentValue.WriteTo(writer);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            if (s.JournalTruncated)
            {
                writer.WriteBoolean("truncated"u8, true);
            }

            writer.WriteEndObject();
        });
    }

    // ADR 0050: the journal status token, matching the checkpoint's serialization (PascalCase, so a read and the stored
    // value agree). Written as a UTF-8 span so the projection allocates no per-entry string.
    private static ReadOnlySpan<byte> StepStatusName(WorkflowStepStatus status) => status switch
    {
        WorkflowStepStatus.Succeeded => "Succeeded"u8,
        WorkflowStepStatus.Faulted => "Faulted"u8,
        WorkflowStepStatus.Skipped => "Skipped"u8,
        _ => "Succeeded"u8,
    };

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpointState?> LoadStateAsync(WorkflowRunId id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's read reach reads back as absent (non-disclosing, §14.2) — the same resolve as
        // GetAsync.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Read, cancellationToken).ConfigureAwait(false) is not { } address)
        {
            return null;
        }

        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        return WorkflowCheckpointSerializer.Deserialize(cp.Utf8);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (this.resumer is null)
        {
            ThrowHelper.ThrowResumerRequired();
        }

        // A run outside the caller's write reach is not actionable (§14.2), decided by the store's index predicate
        // exactly as the listing decides visibility (ADR 0065 §9); the resolve also names the run's address, which
        // every store operation below takes. A missing run answers the same false.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Write, cancellationToken).ConfigureAwait(false) is not { } address)
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

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(address, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
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
                !await this.TryApplyResumeMutationAsync(address, options, activity, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, address, this.timeProvider, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Marks a faulted run resume-claimable after applying a resume mutation (design §18 R5b), instead of
    /// re-executing it in-process. The multi-process fault-remediation path: the control plane applies the mutation
    /// (rewind / skip / patch) under optimistic concurrency, stamps the resume-requested marker, and returns; a runner
    /// surfaces the run through its dispatch index, claims it, and re-enters the executor. Unlike <see cref="ResumeAsync"/>
    /// it needs no <see cref="WorkflowResumer"/> — the control plane never executes.</summary>
    /// <param name="id">The faulted run.</param>
    /// <param name="options">The resume verb (retry / skip / rewind / patch).</param>
    /// <param name="context">The caller's access context (must have write reach, §14.2).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the run was marked resume-claimable; <see langword="false"/> when it is out
    /// of reach, leased by another owner, not faulted, or the mutation did not apply.</returns>
    public async ValueTask<bool> RequestFaultedResumeAsync(WorkflowRunId id, ResumeOptions options, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A run outside the caller's write reach is not actionable (§14.2), decided by the store's index predicate
        // exactly as the listing decides visibility (ADR 0065 §9); the resolve also names the run's address, which
        // every store operation below takes. A missing run answers the same false.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Write, cancellationToken).ConfigureAwait(false) is not { } address)
        {
            return false;
        }

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.resume.request");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.RunIdTag, id.Value);
            activity.SetTag(ArazzoTelemetry.ActorTag, this.owner);
            activity.SetTag(ArazzoTelemetry.ResumeModeTag, options.Mode.ToString());
        }

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(address, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            // For every mode but a plain retry, mutate the checkpoint (cursor/state) under optimistic concurrency
            // before handing off: rewind the cursor, skip past the faulted step, or apply a state patch.
            if (options.Mode != ResumeMode.RetryFaultedStep &&
                !await this.TryApplyResumeMutationAsync(address, options, activity, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, address, this.timeProvider, cancellationToken).ConfigureAwait(false);
            if (run is null || run.Status != WorkflowRunStatus.Faulted)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "not-faulted");
                return false;
            }

            // Hand the (possibly mutated) faulted run to a runner: stamp the resume-requested marker, preserving the
            // run's pause. QueryClaimable surfaces it; a runner claims it and its first checkpoint clears the fault.
            await run.RequestResumeKeepingPauseAsync(cancellationToken).ConfigureAwait(false);
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "resume-requested");
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

        // A run outside the caller's write reach is not actionable (§14.2), decided by the store's index predicate
        // exactly as the listing decides visibility (ADR 0065 §9); the resolve also names the run's address, which
        // every store operation below takes. A missing run answers the same false.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Write, cancellationToken).ConfigureAwait(false) is not { } address)
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

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(address, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
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

            await this.store.SaveAsync(address, updated, indexEntry, cp.Etag, cancellationToken).ConfigureAwait(false);

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

                        await this.store.DeleteAsync(listing.Address, cancellationToken).ConfigureAwait(false);
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

        // A run outside the caller's write reach is not actionable (§14.2), decided by the store's index predicate
        // exactly as the listing decides visibility (ADR 0065 §9); the resolve also names the run's address, which
        // every store operation below takes. A missing run answers the same false.
        if (await this.ResolveWithinReachAsync(id, context, AccessVerb.Write, cancellationToken).ConfigureAwait(false) is not { } address)
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
        WorkflowLease? lease = await this.store.AcquireLeaseAsync(address, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "leased-by-other");
            return false;
        }

        try
        {
            WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
            if (checkpoint is null)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
                return false;
            }

            await this.store.DeleteAsync(address, cancellationToken).ConfigureAwait(false);
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
    private async ValueTask<bool> TryApplyResumeMutationAsync(WorkflowRunAddress address, ResumeOptions options, Activity? activity, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
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
                            ?? throw ThrowHelper.GetRewindRequiresTargetCursorException();
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
                        throw ThrowHelper.GetUnknownResumeModeException(options.Mode);
                }

                // Carry the immutable run-creation metadata (correlation id, pinned environment, tags) through the
                // mutation — the run does not change environment when it is remediated, and dropping it here would make
                // the run unclaimable (§5.5: a runner claims only runs pinned to exactly its environment).
                // The stored write sequence is carried forward unchanged. This is a control-plane remediation, not a
                // runner save (ADR 0065 decision 7), so it must not consume a sequence the runner is about to propose:
                // advancing it here would refuse the runner's next legitimate save as superseded.
                DateTimeOffset mutatedAt = this.timeProvider.GetUtcNow();
                mutated = WorkflowCheckpointSerializer.Serialize(
                    state.RunId,
                    state.WorkflowId,
                    WorkflowRunStatus.Faulted,
                    cursor,
                    state.Sequence,
                    state.CreatedAt,
                    state.RetryCounters,
                    state.CorrelationTokens,
                    inputs,
                    stepOutputs,
                    default,
                    wait: null,
                    fault: state.Fault,
                    correlationId: state.CorrelationId,
                    environment: state.Environment,
                    tags: state.Tags,
                    securityTags: state.SecurityTags,
                    updatedAt: mutatedAt);

                indexEntry = new WorkflowRunIndexEntry(
                    state.WorkflowId,
                    WorkflowRunStatus.Faulted,
                    state.CreatedAt,
                    mutatedAt,
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
            await this.store.SaveAsync(address, mutated, indexEntry, cp.Etag, cancellationToken).ConfigureAwait(false);
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
        => this.index ?? throw ThrowHelper.GetStateStoreMustImplementWaitIndexForVisibilityException();
}