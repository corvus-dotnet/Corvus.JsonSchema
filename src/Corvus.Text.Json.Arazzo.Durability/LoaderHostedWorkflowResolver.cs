// <copyright file="LoaderHostedWorkflowResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-process <see cref="IHostedWorkflowResolver"/>: resolves a run's <see cref="WorkflowRun.WorkflowId"/> to a
/// loaded <see cref="IHostedWorkflow"/> by fetching the version's compiled executor + manifest from the catalog and
/// loading it through a <see cref="WorkflowExecutorLoader"/> on first use (cached thereafter). This is the
/// collectible-load-context, dynamic-IL path; an AOT execution backend uses a baked resolver instead (ADR 0028).
/// </summary>
public sealed class LoaderHostedWorkflowResolver : IHostedWorkflowResolver
{
    private readonly IWorkflowCatalogStore catalog;
    private readonly WorkflowExecutorLoader loader;

    /// <summary>Initializes a new instance of the <see cref="LoaderHostedWorkflowResolver"/> class.</summary>
    /// <param name="catalog">The catalog the executor assembly + manifest + content hash are fetched from.</param>
    /// <param name="loader">The loader that verifies, loads, and caches the executor assembly.</param>
    public LoaderHostedWorkflowResolver(IWorkflowCatalogStore catalog, WorkflowExecutorLoader loader)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        this.catalog = catalog;
        this.loader = loader;
    }

    /// <inheritdoc/>
    public ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return this.ResolveByIdAsync(run.WorkflowId, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        // Warm the loader cache — fetch, verify, and load the version's executor — so a later ResolveAsync of it
        // skips that cost. ResolveByIdAsync returns the cached workflow when already loaded, so a repeat is cheap.
        _ = await this.ResolveByIdAsync($"{baseWorkflowId}-v{versionNumber}", cancellationToken).ConfigureAwait(false);
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

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The in-process resolver loads the version's IL executor through the loader by design, and runs only in in-process (non-AOT, non-trimmed) runner hosts. AOT execution backends use a baked resolver that has the executor at build time (ADR 0028).")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The in-process resolver loads the version's IL executor through the loader by design, and runs only in in-process (non-AOT) runner hosts. AOT execution backends use a baked resolver that has the executor at build time (ADR 0028).")]
    private async ValueTask<IHostedWorkflow> ResolveByIdAsync(string workflowId, CancellationToken cancellationToken)
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