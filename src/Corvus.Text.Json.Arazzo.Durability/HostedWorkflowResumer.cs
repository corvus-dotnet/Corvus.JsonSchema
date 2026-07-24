// <copyright file="HostedWorkflowResumer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>The transports a run executes through, resolved per run from its workflow's descriptor.</summary>
/// <param name="ApiTransports">The API transports the workflow's OpenAPI operation steps call through, keyed by source-description name (<see cref="WorkflowDescriptor.Sources"/>) — one per API source the version uses. The resumer disposes them after the run.</param>
/// <param name="MessageTransports">The message transports the workflow's AsyncAPI channel steps send/receive on, keyed by channel-source name (<see cref="WorkflowDescriptor.MessageSources"/>, ADR 0051) — empty when the workflow needs none. Their lifetimes are owned by the binder (shared per source × environment, connection-scoped), not the resumer.</param>
public readonly record struct WorkflowTransports(IReadOnlyDictionary<string, IApiTransport> ApiTransports, IReadOnlyDictionary<string, IMessageTransport> MessageTransports)
{
    /// <summary>An empty message-transport map for workflows with no channel steps.</summary>
    public static readonly IReadOnlyDictionary<string, IMessageTransport> NoMessageTransports = System.Collections.Immutable.ImmutableDictionary<string, IMessageTransport>.Empty;
}

/// <summary>
/// Binds a workflow's <see cref="WorkflowDescriptor"/> (its source names + transport needs) to the concrete
/// transports a run executes through — the seam where a host maps each version's sources to real endpoints
/// and credentials.
/// </summary>
/// <param name="descriptor">The descriptor of the workflow about to run.</param>
/// <param name="runTags">The run's own security tags (§14.2), so a credential-aware binder can resolve only the
/// source credential bindings the run is entitled to use (§13). Empty for an unscoped run.</param>
/// <returns>The transports for the run.</returns>
public delegate WorkflowTransports WorkflowTransportBinder(WorkflowDescriptor descriptor, SecurityTagSet runTags);

/// <summary>
/// The in-process, collectible-load-context <see cref="IRunExecutionBackend"/> (ADR 0028). It resolves a run's
/// workflow to a loaded <see cref="IHostedWorkflow"/> through a <see cref="LoaderHostedWorkflowResolver"/> (the
/// catalog fetch + <see cref="WorkflowExecutorLoader"/> load, cached), then runs it through
/// <see cref="HostedWorkflowExecution"/>. Its <see cref="AdvanceAsync"/> is the <see cref="WorkflowResumer"/> the
/// durable worker and management client already expect, so dispatch, resume, retry, rewind, skip, and cancel all
/// drive real loaded assemblies.
/// </summary>
public sealed class HostedWorkflowResumer : IRunExecutionBackend
{
    private readonly IHostedWorkflowResolver resolver;
    private readonly WorkflowTransportBinder transportBinder;
    private readonly ScheduleHostedWorkflow? scheduleWorkflow;

    /// <summary>Initializes a new instance of the <see cref="HostedWorkflowResumer"/> class.</summary>
    /// <param name="catalog">The catalog the executor assembly + manifest + content hash are fetched from.</param>
    /// <param name="loader">The loader that verifies, loads, and caches the executor assembly.</param>
    /// <param name="transportBinder">Binds a workflow's descriptor to the transports a run executes through.</param>
    /// <param name="scheduleWorkflow">The built-in scheduler workflow (#896) that <c>$schedule</c> runs are routed to,
    /// or <see langword="null"/> on a host that does not serve schedules. A schedule is a durable run of it, not a
    /// catalogued executor, so it is dispatched and resumed by the same worker but bypasses the catalog + loader.</param>
    public HostedWorkflowResumer(IWorkflowCatalogStore catalog, WorkflowExecutorLoader loader, WorkflowTransportBinder transportBinder, ScheduleHostedWorkflow? scheduleWorkflow = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(transportBinder);
        this.resolver = new LoaderHostedWorkflowResolver(catalog, loader);
        this.transportBinder = transportBinder;
        this.scheduleWorkflow = scheduleWorkflow;
    }

    /// <inheritdoc/>
    public RunIsolationModel IsolationModel => RunIsolationModel.InProcess;

    /// <summary>Gets this resumer as the <see cref="WorkflowResumer"/> delegate the worker/management client consume.</summary>
    /// <returns>The resumer delegate.</returns>
    public WorkflowResumer AsResumer() => this.AdvanceAsync;

    /// <summary>
    /// Resolves the run's workflow to a loaded <see cref="IHostedWorkflow"/>, binds its transports, and starts
    /// or resumes the durable run — returning the tri-state outcome.
    /// </summary>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    public async ValueTask<WorkflowRunResultKind> AdvanceAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        // A schedule is a durable run of the built-in scheduler workflow (#896), not a catalogued executor: it needs
        // no transports and is not a versioned id, so route it straight to the scheduler rather than parsing its id
        // and loading an assembly. It is still dispatched and resumed by the same worker (its due timer + lease).
        if (this.scheduleWorkflow is { } scheduler && string.Equals(run.WorkflowId, ScheduleHostedWorkflow.ScheduleWorkflowId, StringComparison.Ordinal))
        {
            using JsonWorkspace scheduleWorkspace = JsonWorkspace.CreateUnrented();
            return await scheduler.RunAsync(ImmutableDictionary<string, IApiTransport>.Empty, WorkflowTransports.NoMessageTransports, scheduleWorkspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }

        IHostedWorkflow hosted = await this.resolver.ResolveAsync(run, cancellationToken).ConfigureAwait(false);
        return await HostedWorkflowExecution.RunAsync(hosted, this.transportBinder, run, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        => this.resolver.PrepareAsync(baseWorkflowId, versionNumber, cancellationToken);
}