// <copyright file="DraftRunManagement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>The capture a §18 draft run starts from: the working copy's document (the chosen workflow first), its
/// source documents, and the audit tuple (who, which working copy, which document etag, which environment).</summary>
/// <param name="WorkingCopyId">The working copy the draft was captured from.</param>
/// <param name="WorkflowId">The workflow within the document the run executes (the document's FIRST workflow).</param>
/// <param name="DocumentUtf8">The captured Arazzo document as UTF-8 JSON.</param>
/// <param name="Sources">The captured source documents, by <c>sourceDescriptions</c> name.</param>
/// <param name="Environment">The development-class environment the run is pinned to (its <c>allowsDraftRuns</c> gate passed at start).</param>
/// <param name="DocumentEtag">The working copy's document etag at capture — the audited draft identity.</param>
/// <param name="StartedBy">The audit actor starting the run.</param>
public readonly record struct DraftRunStart(
    string WorkingCopyId,
    string WorkflowId,
    ReadOnlyMemory<byte> DocumentUtf8,
    IReadOnlyList<KeyValuePair<string, byte[]>> Sources,
    string Environment,
    string DocumentEtag,
    string StartedBy);

/// <summary>
/// Starts §18 draft runs on the ordinary runs machinery (workflow-designer design §18, staging item 3): the
/// captured draft is persisted to the <see cref="IDraftRunStore"/> (keyed by the run id, with the §18 audit
/// tuple), then a <see cref="WorkflowRunStatus.Pending"/> run carrying the reserved
/// <see cref="DraftRuns.RunWorkflowId"/> is enqueued, pinned to the target environment and row-scoped to its
/// working copy — the store-as-dispatch-queue then hands it to an environment-pinned, draft-hosting runner
/// exactly as it hands a catalog run to a version-hosting one.
/// </summary>
/// <remarks>
/// The capture is written before the run is enqueued, so a claimable draft run always finds its draft. The
/// forward-only lifecycle from there is the ordinary one: the runner drives the compiled draft through
/// Pending → Running → Completed/Faulted/Suspended/Cancelled with the existing <see cref="WorkflowRun"/>
/// primitives, and durable waits resume through the existing <see cref="WorkflowWorker"/>.
/// </remarks>
public sealed class DraftRunManagement
{
    private readonly IWorkflowStateStore store;
    private readonly IDraftRunStore drafts;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="DraftRunManagement"/> class.</summary>
    /// <param name="store">The state store the Pending run is enqueued to (the shared dispatch queue).</param>
    /// <param name="drafts">The draft-run store the capture is persisted to.</param>
    /// <param name="timeProvider">The time source for the capture timestamp; defaults to <see cref="TimeProvider.System"/>.</param>
    public DraftRunManagement(IWorkflowStateStore store, IDraftRunStore drafts, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(drafts);
        this.store = store;
        this.drafts = drafts;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Captures a draft and enqueues its durable run: the packed document + sources and the audit record land in
    /// the draft-run store, then the Pending run — reserved workflow id, pinned environment, working-copy row
    /// scope — lands in the state store for an authorized draft-hosting runner to claim.
    /// </summary>
    /// <param name="start">The captured draft and its audit tuple.</param>
    /// <param name="inputs">The workflow inputs (rides the run record, exactly as a catalog run's inputs do).</param>
    /// <param name="securityTags">The starter's ambient identity tags (§14.2); the working-copy scope tag is added here.</param>
    /// <param name="correlationId">The run-wide telemetry correlation id; defaults to the ambient trace id.</param>
    /// <param name="tags">Free-form tags to apply to the run.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The new run's id.</returns>
    public async ValueTask<WorkflowRunId> StartAsync(
        DraftRunStart start,
        JsonElement inputs,
        SecurityTagSet securityTags = default,
        WorkflowPauseConfig? pause = null,
        string? correlationId = null,
        TagSet tags = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(start.WorkingCopyId);
        ArgumentException.ThrowIfNullOrEmpty(start.WorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(start.Environment);
        ArgumentException.ThrowIfNullOrEmpty(start.DocumentEtag);
        ArgumentException.ThrowIfNullOrEmpty(start.StartedBy);
        ArgumentNullException.ThrowIfNull(start.Sources);

        var id = new WorkflowRunId(Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));
        string contentHash = WorkflowPackage.ComputeContentHash(start.DocumentUtf8, start.Sources);
        byte[] package = WorkflowPackage.Pack(start.DocumentUtf8, start.Sources);

        // The capture first, the run second: a claimable Pending run must always find its draft.
        // Unrented (no thread affinity): the builder's RootElement is held across the awaited PutAsync below, so this
        // workspace is disposed on the store call's continuation thread — a rented (thread-local) one would fail its
        // return-to-cache invariant there and abort the process. Same fix as HostedWorkflowResumer/DraftWorkflowResumer.
        using (JsonWorkspace workspace = JsonWorkspace.CreateUnrented())
        {
            using JsonDocumentBuilder<DraftRun.Mutable> builder = DraftRun.CreateBuilder(
                workspace,
                runId: id.Value,
                workingCopyId: start.WorkingCopyId,
                workflowId: start.WorkflowId,
                documentEtag: start.DocumentEtag,
                environment: start.Environment,
                startedBy: start.StartedBy,
                startedAt: this.timeProvider.GetUtcNow(),
                contentHash: contentHash);
            await this.drafts.PutAsync(id, builder.RootElement, package, cancellationToken).ConfigureAwait(false);
        }

        SecurityTagSet scopedTags = DraftRuns.WithWorkingCopyTag(securityTags, start.WorkingCopyId);
        using WorkflowRun run = WorkflowRun.CreateNew(
            this.store,
            id,
            DraftRuns.RunWorkflowId,
            inputs,
            this.timeProvider,
            correlationId,
            tags,
            scopedTags,
            start.Environment);

        // §18 R5: persist the debugger pause on the Pending run so the runner that claims it honours the stops on its
        // first advance. The control plane never executes the run; a runner does. Null leaves the run unpaused.
        run.SetPause(pause);
        await run.EnqueueAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }
}