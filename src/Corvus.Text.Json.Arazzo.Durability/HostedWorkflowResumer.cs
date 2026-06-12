// <copyright file="HostedWorkflowResumer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>The transports a run executes through, resolved per run from its workflow's descriptor.</summary>
/// <param name="ApiTransport">The API transport the workflow's OpenAPI operation steps call through. The resumer disposes it after the run.</param>
/// <param name="MessageTransport">The message transport for AsyncAPI channel steps, or <see langword="null"/> when the workflow needs none. Its lifetime is owned by the binder (typically shared/long-lived), not the resumer.</param>
public readonly record struct WorkflowTransports(IApiTransport ApiTransport, IMessageTransport? MessageTransport);

/// <summary>
/// Binds a workflow's <see cref="WorkflowDescriptor"/> (its source names + transport needs) to the concrete
/// transports a run executes through — the seam where a host maps each version's sources to real endpoints
/// and credentials.
/// </summary>
/// <param name="descriptor">The descriptor of the workflow about to run.</param>
/// <returns>The transports for the run.</returns>
public delegate WorkflowTransports WorkflowTransportBinder(WorkflowDescriptor descriptor);

/// <summary>
/// Resolves a run's <see cref="WorkflowRun.WorkflowId"/> to a loaded <see cref="IHostedWorkflow"/> — fetching
/// the version's compiled executor + manifest from the catalog and loading it through a
/// <see cref="WorkflowExecutorLoader"/> on first use (cached thereafter) — then runs it. Its
/// <see cref="ResumeAsync"/> is the <see cref="WorkflowResumer"/> the durable worker and management client
/// already expect, so dispatch, resume, retry, rewind, skip, and cancel all drive real loaded assemblies.
/// </summary>
public sealed class HostedWorkflowResumer
{
    private readonly IWorkflowCatalogStore catalog;
    private readonly WorkflowExecutorLoader loader;
    private readonly WorkflowTransportBinder transportBinder;

    /// <summary>Initializes a new instance of the <see cref="HostedWorkflowResumer"/> class.</summary>
    /// <param name="catalog">The catalog the executor assembly + manifest + content hash are fetched from.</param>
    /// <param name="loader">The loader that verifies, loads, and caches the executor assembly.</param>
    /// <param name="transportBinder">Binds a workflow's descriptor to the transports a run executes through.</param>
    public HostedWorkflowResumer(IWorkflowCatalogStore catalog, WorkflowExecutorLoader loader, WorkflowTransportBinder transportBinder)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(transportBinder);
        this.catalog = catalog;
        this.loader = loader;
        this.transportBinder = transportBinder;
    }

    /// <summary>Gets this resumer as the <see cref="WorkflowResumer"/> delegate the worker/management client consume.</summary>
    /// <returns>The resumer delegate.</returns>
    public WorkflowResumer AsResumer() => this.ResumeAsync;

    /// <summary>
    /// Resolves the run's workflow to a loaded <see cref="IHostedWorkflow"/>, binds its transports, and starts
    /// or resumes the durable run — returning the tri-state outcome.
    /// </summary>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    public async ValueTask<WorkflowRunResultKind> ResumeAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        IHostedWorkflow hosted = await this.ResolveAsync(run.WorkflowId, cancellationToken).ConfigureAwait(false);
        WorkflowTransports transports = this.transportBinder(hosted.Descriptor);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        try
        {
            return await hosted.RunAsync(transports.ApiTransport, transports.MessageTransport, workspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await transports.ApiTransport.DisposeAsync().ConfigureAwait(false);
        }
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

        CatalogVersion version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' is not in the catalog.");

        ReadOnlyMemory<byte> assembly = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' is not runnable (no executor in the package).");
        ReadOnlyMemory<byte> manifest = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' has an executor but no manifest.");

        return this.loader.Load(baseWorkflowId, versionNumber, assembly, manifest, (string)version.Hash).Workflow;
    }
}