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
/// <param name="MessageTransport">The message transport for AsyncAPI channel steps, or <see langword="null"/> when the workflow needs none. Its lifetime is owned by the binder (typically shared/long-lived), not the resumer.</param>
public readonly record struct WorkflowTransports(IReadOnlyDictionary<string, IApiTransport> ApiTransports, IMessageTransport? MessageTransport);

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
/// Resolves a run's <see cref="WorkflowRun.WorkflowId"/> to a loaded <see cref="IHostedWorkflow"/> — fetching
/// the version's compiled executor + manifest from the catalog and loading it through a
/// <see cref="WorkflowExecutorLoader"/> on first use (cached thereafter) — then runs it. This is the in-process,
/// collectible-load-context <see cref="IRunExecutionBackend"/> (ADR 0028): its <see cref="AdvanceAsync"/> is the
/// <see cref="WorkflowResumer"/> the durable worker and management client already expect, so dispatch, resume,
/// retry, rewind, skip, and cancel all drive real loaded assemblies.
/// </summary>
public sealed class HostedWorkflowResumer : IRunExecutionBackend
{
    private readonly IWorkflowCatalogStore catalog;
    private readonly WorkflowExecutorLoader loader;
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
        this.catalog = catalog;
        this.loader = loader;
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
            return await scheduler.RunAsync(ImmutableDictionary<string, IApiTransport>.Empty, null, scheduleWorkspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }

        IHostedWorkflow hosted = await this.ResolveAsync(run.WorkflowId, cancellationToken).ConfigureAwait(false);
        WorkflowTransports transports = this.transportBinder(hosted.Descriptor, run.SecurityTags);

        // Unrented (no thread affinity): RunAsync is awaited and a run's async continuation (e.g. an outbound HTTP call
        // completing on a thread-pool thread) can dispose this workspace on a different thread than the one that created
        // it, so a thread-local rented workspace would fail its return-to-cache invariant — aborting the process
        // (SIGABRT) and, if the slot is reused, invalidating live JsonElements. Same posture as the generated OpenAPI
        // response handlers and DraftWorkflowResumer.
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            return await hosted.RunAsync(transports.ApiTransports, transports.MessageTransport, workspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowPauseException)
        {
            // §18: the executor unwound at a debugger pause point. CheckpointAsync already persisted the run as
            // Suspended with a Pause wait (no wake trigger), so this is a clean suspend — report the same
            // tri-state Suspended a timer or message suspend returns; the finally still disposes the transports.
            return WorkflowRunResultKind.Suspended;
        }
        finally
        {
            foreach (IApiTransport apiTransport in transports.ApiTransports.Values)
            {
                await apiTransport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        // Warm the loader cache — fetch, verify, and load the version's executor — so a later AdvanceAsync of it
        // skips that cost. ResolveAsync returns the cached workflow when it is already loaded, so a repeat is cheap.
        _ = await this.ResolveAsync($"{baseWorkflowId}-v{versionNumber}", cancellationToken).ConfigureAwait(false);
    }

    private static (string BaseWorkflowId, int VersionNumber) ParseVersionedId(string workflowId)
    {
        int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
        if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), out int version))
        {
            return (workflowId[..suffix], version);
        }

        throw new InvalidOperationException($"The workflow id '{workflowId}' is not a versioned id of the form '{{base}}-v{{n}}'.");
    }

    private async ValueTask<IHostedWorkflow> ResolveAsync(string workflowId, CancellationToken cancellationToken)
    {
        (string baseWorkflowId, int versionNumber) = ParseVersionedId(workflowId);
        if (this.loader.TryGet(baseWorkflowId, versionNumber, out LoadedWorkflow? cached))
        {
            return cached.Workflow;
        }

        // The version document is owned here only to read its hash (an owned copy, safe after dispose).
        string hash;
        using (ParsedJsonDocument<CatalogVersion> versionDoc = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' is not in the catalog."))
        {
            hash = (string)versionDoc.RootElement.Hash;
        }

        ReadOnlyMemory<byte> assembly = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' is not runnable (no executor in the package).");
        ReadOnlyMemory<byte> manifest = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' has an executor but no manifest.");

        // The detached signature is optional here (empty when the package is unsigned); the loader enforces it only when
        // it was configured with a verifier — a signing-required runner rejects an unsigned or badly-signed package.
        ReadOnlyMemory<byte> signature = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestSignatureDocumentName, cancellationToken).ConfigureAwait(false) ?? default;

        return this.loader.Load(baseWorkflowId, versionNumber, assembly, manifest, hash, signature).Workflow;
    }
}