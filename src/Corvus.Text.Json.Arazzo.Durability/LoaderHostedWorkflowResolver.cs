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
/// collectible-load-context, dynamic-IL path; an AOT execution backend uses a baked resolver instead (ADR 0055).
/// </summary>
public sealed class LoaderHostedWorkflowResolver : IHostedWorkflowResolver
{
    private readonly IWorkflowArtifactSource artifacts;
    private readonly WorkflowExecutorLoader loader;
    private readonly IExecutorAdmission? admission;

    /// <summary>Initializes a new instance of the <see cref="LoaderHostedWorkflowResolver"/> class over a catalog.</summary>
    /// <param name="catalog">The catalog the executor assembly + manifest + content hash are fetched from.</param>
    /// <param name="loader">The loader that verifies, loads, and caches the executor assembly.</param>
    public LoaderHostedWorkflowResolver(IWorkflowCatalogStore catalog, WorkflowExecutorLoader loader)
        : this(new CatalogWorkflowArtifactSource(catalog), loader, admission: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LoaderHostedWorkflowResolver"/> class over any artifact source.</summary>
    /// <param name="artifacts">Where the content hash and the package's documents come from. A runner without a catalog credential passes the runner API's source (ADR 0065); a host that owns the catalog passes <see cref="CatalogWorkflowArtifactSource"/>.</param>
    /// <param name="loader">The loader that verifies, loads, and caches the executor assembly.</param>
    /// <param name="admission">The runner's own executor policy (ADR 0065 phase C), consulted for every run with the manifest of the executor actually loaded and the run's environment; <see langword="null"/> for a host with no policy of its own, which runs whatever the loader verified.</param>
    public LoaderHostedWorkflowResolver(IWorkflowArtifactSource artifacts, WorkflowExecutorLoader loader, IExecutorAdmission? admission = null)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(loader);
        this.artifacts = artifacts;
        this.loader = loader;
        this.admission = admission;
    }

    /// <inheritdoc/>
    public async ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        LoadedWorkflow loaded = await this.ResolveByIdAsync(run.WorkflowId, cancellationToken).ConfigureAwait(false);

        // The tenant's authority (ADR 0065 phase C), checked after the platform's: the loader verified that the
        // platform produced this executor, and the policy decides whether the tenant authorized it for the run's
        // environment. It sees the manifest of the executor that was loaded, so what it admits is what runs. A refusal
        // is not an executor fault: the run is handed back rather than ended, since a countersignature the operator
        // has not yet recorded is the ordinary state of a version just promoted.
        if (this.admission is { } admission)
        {
            (string baseWorkflowId, int versionNumber) = ParseVersionedId(run.WorkflowId);
            ExecutorAdmission verdict = await admission.AdmitAsync(run.Environment, baseWorkflowId, versionNumber, loaded.Manifest, cancellationToken).ConfigureAwait(false);
            if (verdict != ExecutorAdmission.Admitted)
            {
                throw ThrowHelper.GetExecutorNotAdmittedException(run.Environment, run.WorkflowId, verdict);
            }
        }

        return loaded.Workflow;
    }

    /// <inheritdoc/>
    public async ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        // Warm the loader cache — fetch, verify, and load the version's executor — so a later ResolveAsync of it
        // skips that cost. ResolveByIdAsync returns the cached workflow when already loaded, so a repeat is cheap.
        _ = await this.ResolveByIdAsync($"{baseWorkflowId}-v{versionNumber}", cancellationToken).ConfigureAwait(false);
    }

    // A run whose workflow id names no version is refused, the same answer on every attempt (WorkflowExecutorFault).
    private static (string BaseWorkflowId, int VersionNumber) ParseVersionedId(string workflowId)
        => WorkflowVersionId.TryParse(workflowId, out string baseWorkflowId, out int versionNumber)
            ? (baseWorkflowId, versionNumber)
            : throw ThrowHelper.GetWorkflowIdNotVersionedException(workflowId);

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The in-process resolver loads the version's IL executor through the loader by design, and runs only in in-process (non-AOT, non-trimmed) runner hosts. AOT execution backends use a baked resolver that has the executor at build time (ADR 0055).")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The in-process resolver loads the version's IL executor through the loader by design, and runs only in in-process (non-AOT) runner hosts. AOT execution backends use a baked resolver that has the executor at build time (ADR 0055).")]
    private async ValueTask<LoadedWorkflow> ResolveByIdAsync(string workflowId, CancellationToken cancellationToken)
    {
        (string baseWorkflowId, int versionNumber) = ParseVersionedId(workflowId);
        if (this.loader.TryGet(baseWorkflowId, versionNumber, out LoadedWorkflow? cached))
        {
            return cached;
        }

        // RECOMPUTE the content hash from the version's actual workflow + sources rather than trusting the stored
        // column (H13): the hash is what binds the executor manifest to the version's content, so a column that
        // matched a forged manifest would load an executor not derived from the stored documents. The AOT build path
        // already recomputes (CatalogPackage.HashCanonical over the package); this seam is per-document, so the
        // recompute assembles the same logical content document-wise. The stored column is still fetched and must
        // agree — a divergence is a tampered or corrupted version and refuses the load.
        string storedHash = await this.artifacts.GetContentHashAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false)
            ?? throw ThrowHelper.GetVersionNotInCatalogException(versionNumber, baseWorkflowId);
        string hash = await this.RecomputeContentHashAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(storedHash, hash, StringComparison.Ordinal))
        {
            throw ThrowHelper.GetStoredContentHashDivergesException(baseWorkflowId, versionNumber, storedHash, hash);
        }

        ReadOnlyMemory<byte> assembly = await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw ThrowHelper.GetVersionNotRunnableException(versionNumber, baseWorkflowId);
        ReadOnlyMemory<byte> manifest = await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw ThrowHelper.GetVersionHasNoManifestException(versionNumber, baseWorkflowId);

        // The detached signature is optional here (empty when the package is unsigned); the loader enforces it only when
        // it was configured with a verifier — a signing-required runner rejects an unsigned or badly-signed package.
        ReadOnlyMemory<byte> signature = await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestSignatureDocumentName, cancellationToken).ConfigureAwait(false) ?? default;

        return this.loader.Load(baseWorkflowId, versionNumber, assembly, manifest, hash, signature);
    }

    // Recomputes the version's content hash (ADR 0031: SHA-256 of the RFC 8785 canonical { workflow, sources })
    // from the documents this seam actually serves: the workflow document names its sources, and each non-arazzo
    // source description resolves to a source document of the same name — the same logical content
    // CatalogPackage.HashCanonical reads from a whole package.
    private async ValueTask<string> RecomputeContentHashAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> workflow = await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, CatalogPackage.WorkflowDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw ThrowHelper.GetVersionNotInCatalogException(versionNumber, baseWorkflowId);

        IReadOnlyList<CatalogSourceRef> sourceRefs;
        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(workflow))
        {
            sourceRefs = CatalogPackage.ReadSourceRefs(doc.RootElement);
        }

        var sources = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>(sourceRefs.Count);
        foreach (CatalogSourceRef sourceRef in sourceRefs)
        {
            if (await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, sourceRef.Name, cancellationToken).ConfigureAwait(false) is { } source)
            {
                sources.Add(new(sourceRef.Name, source));
            }
        }

        return WorkflowPackage.ComputeContentHash(workflow, sources);
    }
}