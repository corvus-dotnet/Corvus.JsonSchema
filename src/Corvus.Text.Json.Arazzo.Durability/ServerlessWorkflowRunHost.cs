// <copyright file="ServerlessWorkflowRunHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The serverless function-side host: the reusable core a vendor entry shim (an AWS Lambda handler or an Azure
/// Functions HTTP trigger) invokes with a run id. Because a run is a durable checkpoint in the shared store, the host
/// restores the run from the store, resolves the workflow that runs it, and advances it through the shared
/// <see cref="HostedWorkflowExecution"/> core (ADR 0055). The environment is fixed at deploy time and selects the
/// <em>resolver</em> and <em>transport binder</em> baked into the function; the <em>store</em> is per-invocation —
/// the shim points it at the checkpoint URL the invocation carries, so a run checkpoints back to the specific runner
/// that dispatched it and holds its lease (§6b Model B).
/// </summary>
/// <remarks>
/// <para>
/// It is the out-of-process counterpart to the in-process <see cref="HostedWorkflowResumer"/>: the resumer is handed
/// a run the durable worker already restored under a lease, whereas this host is invoked with only an id and restores
/// the run itself. The resolver is normally a <see cref="BakedHostedWorkflowResolver"/> — the AOT executor compiled
/// into the deployed function — which is what keeps this host loader-free and AOT-clean; nothing here loads IL.
/// </para>
/// <para>
/// Leasing stays the dispatching runner's concern: it holds the lease across the invocation, so no other host advances
/// the same run concurrently, and this host does not lease. It does re-check dispatchability on the freshly restored
/// checkpoint (the same condition <see cref="WorkflowDispatcher"/> claims on), so an at-least-once duplicate invocation
/// of an already-settled run is a safe no-op — reported as <see langword="null"/> — rather than a re-run of finished
/// work. This host serves the dispatcher's advance kinds: a fresh run, an orphaned run, and a control-plane-requested
/// resume (§18). Timer-due and message-delivery resumes are the durable worker's separate paths (they turn on a due
/// timer or a delivered message the invocation would have to carry) and are a later increment, not served here.
/// </para>
/// </remarks>
public sealed class ServerlessWorkflowRunHost
{
    private readonly IWorkflowCheckpointStore store;
    private readonly IHostedWorkflowResolver resolver;
    private readonly WorkflowTransportBinder transportBinder;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ServerlessWorkflowRunHost"/> class.</summary>
    /// <param name="store">The shared run-state store the invocation's run is restored from and checkpointed back to.</param>
    /// <param name="resolver">Resolves the run to the <see cref="IHostedWorkflow"/> that runs it — a <see cref="BakedHostedWorkflowResolver"/> in a deployed serverless function.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports the run executes through, for this host's (deployed) environment.</param>
    /// <param name="timeProvider">The time provider the restored run uses for its timer waits; defaults to <see cref="TimeProvider.System"/>.</param>
    public ServerlessWorkflowRunHost(IWorkflowCheckpointStore store, IHostedWorkflowResolver resolver, WorkflowTransportBinder transportBinder, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transportBinder);
        this.store = store;
        this.resolver = resolver;
        this.transportBinder = transportBinder;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Restores the run from the store, resolves its workflow, and advances it (start or resume), returning the
    /// tri-state outcome — or <see langword="null"/> when the run is not dispatchable (not found, already terminal, or
    /// a resume kind this host does not serve) so nothing was run.
    /// </summary>
    /// <param name="address">The <c>(environment, runId)</c> address of the run to advance, carried by the invocation
    /// (ADR 0065 decision 9).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome, or <see langword="null"/> when there was nothing dispatchable to advance.</returns>
    public async ValueTask<WorkflowRunResultKind?> InvokeAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, address, this.timeProvider, cancellationToken).ConfigureAwait(false);

        // Re-check dispatchability on the fresh checkpoint, matching WorkflowDispatcher: a Pending or Running run, or a
        // Suspended/Faulted run the control plane marked resume-claimable (§18), is advanced; a missing run (deleted)
        // or an already-terminal one is a safe no-op reported as null, never a re-run of settled work. A serverless
        // invocation is at-least-once, so this guard is what makes a duplicate invocation idempotent.
        if (run is null
            || !(run.Status is WorkflowRunStatus.Pending or WorkflowRunStatus.Running
                 || (run.IsResumeRequested && run.Status is WorkflowRunStatus.Suspended or WorkflowRunStatus.Faulted)))
        {
            return null;
        }

        IHostedWorkflow hosted = await this.resolver.ResolveAsync(run, cancellationToken).ConfigureAwait(false);
        WorkflowRunResultKind kind = await HostedWorkflowExecution.RunAsync(hosted, this.transportBinder, run, cancellationToken).ConfigureAwait(false);

        // If the store defers its checkpoint writes (the serverless HTTP store's fire-and-forget saves), force the
        // terminal checkpoint to land before the outcome is reported — otherwise the runner would be told the run
        // advanced while its final state is still in flight. A synchronous store (every database backend) has no
        // such capability and nothing to flush.
        if (this.store is IWorkflowCheckpointFlush deferred)
        {
            await deferred.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        return kind;
    }
}