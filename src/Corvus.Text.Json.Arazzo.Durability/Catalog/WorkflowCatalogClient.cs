// <copyright file="WorkflowCatalogClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The default <see cref="IWorkflowCatalogClient"/> over an <see cref="IWorkflowCatalogStore"/> and the run
/// store. It enforces the submission rules (no <c>-vN</c> suffix), enforces referential integrity on delete and
/// purge by consulting the run store, and emits <c>catalog.*</c> audit spans tagged with the actor, base id,
/// version and outcome — telemetry is the forensic trail; the durable record carries <c>createdBy</c>/
/// <c>lastUpdatedBy</c> for governance visibility.
/// </summary>
public sealed class WorkflowCatalogClient : IWorkflowCatalogClient
{
    private readonly IWorkflowCatalogStore catalog;
    private readonly IWorkflowWaitIndex runs;
    private readonly string actor;

    /// <summary>Initializes a new instance of the <see cref="WorkflowCatalogClient"/> class.</summary>
    /// <param name="catalog">The catalog store.</param>
    /// <param name="runs">The run index, consulted (by exact versioned workflow id) for referential integrity on delete and purge.</param>
    /// <param name="actor">The authenticated identity recorded on writes (<c>createdBy</c>/<c>lastUpdatedBy</c>) and in audit spans.</param>
    public WorkflowCatalogClient(IWorkflowCatalogStore catalog, IWorkflowWaitIndex runs, string actor)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(actor);
        this.catalog = catalog;
        this.runs = runs;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, IReadOnlyList<string>? tags, CancellationToken cancellationToken)
        => this.AddAsync(packageUtf8, owner, tags, securityTags: null, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, IReadOnlyList<string>? tags, IReadOnlyList<SecurityTag>? securityTags, CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.add");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);

        string baseWorkflowId = CatalogPackage.ReadBaseWorkflowId(packageUtf8);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        if (CatalogPackage.IsVersioned(baseWorkflowId))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "versioned-id-rejected");
            throw new ArgumentException(
                $"The submitted workflow id '{baseWorkflowId}' already carries a version suffix; submit the base id without '-vN'.",
                nameof(packageUtf8));
        }

        CatalogVersion version = await this.catalog.AddAsync(
            baseWorkflowId, packageUtf8, new CatalogMetadata(owner, this.actor, tags, securityTags), cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, version.Ref.VersionNumber);
        activity?.SetTag(ArazzoTelemetry.WorkflowIdTag, version.Ref.WorkflowId);
        activity?.SetTag(ArazzoTelemetry.OutcomeTag, "added");
        return version;
    }

    /// <inheritdoc/>
    public ValueTask<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken cancellationToken)
        => this.catalog.QueryAsync(query, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        => this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        => this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
        => this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, documentName, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, IReadOnlyList<string>? tags, CatalogStatus? status, CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.update");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        CatalogVersion? updated = await this.catalog.UpdateMetadataAsync(
            baseWorkflowId, versionNumber, new CatalogMetadataPatch(this.actor, owner, tags, status), cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.OutcomeTag, updated is null ? "missing" : "updated");
        return updated;
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogDeleteOutcome> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.delete");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return CatalogDeleteOutcome.NotFound;
        }

        if (await this.IsReferencedAsync((string)version.Value.WorkflowId, cancellationToken).ConfigureAwait(false))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "referenced");
            return CatalogDeleteOutcome.Referenced;
        }

        await this.catalog.DeleteAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.OutcomeTag, "deleted");
        return CatalogDeleteOutcome.Deleted;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PurgeAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.purge");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);

        IReadOnlyList<CatalogVersionRef> obsolete = await this.catalog.ListObsoleteAsync(cancellationToken).ConfigureAwait(false);
        var unreferenced = new List<CatalogVersionRef>();
        foreach (CatalogVersionRef reference in obsolete)
        {
            if (!await this.IsReferencedAsync(reference.WorkflowId, cancellationToken).ConfigureAwait(false))
            {
                unreferenced.Add(reference);
            }
        }

        if (unreferenced.Count > 0)
        {
            await this.catalog.DeleteManyAsync(unreferenced, cancellationToken).ConfigureAwait(false);
        }

        activity?.SetTag("corvus.arazzo.purged_count", unreferenced.Count.ToString(CultureInfo.InvariantCulture));
        return unreferenced.Count;
    }

    private async ValueTask<bool> IsReferencedAsync(string workflowId, CancellationToken cancellationToken)
    {
        // A run references a version by its exact versioned workflow id — an indexed exact-match query.
        WorkflowRunPage page = await this.runs.QueryAsync(new WorkflowQuery(WorkflowId: workflowId, Limit: 1), cancellationToken).ConfigureAwait(false);
        return page.Runs.Count > 0;
    }
}