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
    public ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, CancellationToken cancellationToken)
        => this.AddAsync(packageUtf8, owner, tags, securityTags: default, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, SecurityTagSet securityTags, CancellationToken cancellationToken)
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
    public ValueTask<CatalogPage> SearchAsync(CatalogQuery query, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Scope the search to the caller's read reach (§14.2); the store applies the filter in its query. Refuse
        // (rather than leak) if the store does not push the reach filter down.
        SecurityFilter? reach = context.Reach(AccessVerb.Read);
        RowSecurityPushdown.EnsureSupported(reach, this.catalog);
        return this.catalog.QueryAsync(query with { Security = reach }, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);

        // A version outside the caller's read reach is reported as absent (non-disclosing, §14.2).
        return version is { } v && context.Admits(AccessVerb.Read, v.SecurityTagsValue) ? v : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!await this.IsWithinReachAsync(baseWorkflowId, versionNumber, context, AccessVerb.Read, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!await this.IsWithinReachAsync(baseWorkflowId, versionNumber, context, AccessVerb.Read, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, documentName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, TagSet? tags, CatalogStatus? status, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.update");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        // A version outside the caller's write reach is not modifiable; reported as absent (§14.2).
        if (!await this.IsWithinReachAsync(baseWorkflowId, versionNumber, context, AccessVerb.Write, cancellationToken).ConfigureAwait(false))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return null;
        }

        CatalogVersion? updated = await this.catalog.UpdateMetadataAsync(
            baseWorkflowId, versionNumber, new CatalogMetadataPatch(this.actor, owner, tags, status), cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.OutcomeTag, updated is null ? "missing" : "updated");
        return updated;
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogDeleteOutcome> DeleteAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.delete");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);

        // Absent, or outside the caller's write reach → reported as not found (non-disclosing, §14.2).
        if (version is not { } v || !context.Admits(AccessVerb.Write, v.SecurityTagsValue))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return CatalogDeleteOutcome.NotFound;
        }

        if (await this.IsReferencedAsync((string)v.WorkflowId, cancellationToken).ConfigureAwait(false))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "referenced");
            return CatalogDeleteOutcome.Referenced;
        }

        await this.catalog.DeleteAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.OutcomeTag, "deleted");
        return CatalogDeleteOutcome.Deleted;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PurgeAsync(AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.purge");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);

        SecurityFilter? purgeReach = context.Reach(AccessVerb.Purge);
        IReadOnlyList<CatalogVersionRef> obsolete = await this.catalog.ListObsoleteAsync(cancellationToken).ConfigureAwait(false);
        var unreferenced = new List<CatalogVersionRef>();
        foreach (CatalogVersionRef reference in obsolete)
        {
            // Row-scope the purge (§14.2): obsolete candidates outside the caller's purge reach are left untouched.
            // Obsolete refs carry no tags, so reach needs the version's metadata — fetched per candidate (a purge
            // is rare/administrative; a true indexed filter is the per-backend pushdown, §14.4).
            if (purgeReach is not null && !await this.IsVersionVisibleAsync(reference.BaseWorkflowId, reference.VersionNumber, purgeReach, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

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

    // Whether a version is within the caller's reach for a verb (§14.2): unrestricted reach short-circuits without
    // a fetch; otherwise the version must exist and its security tags must satisfy the verb's reach.
    private async ValueTask<bool> IsWithinReachAsync(string baseWorkflowId, int versionNumber, AccessContext context, AccessVerb verb, CancellationToken cancellationToken)
    {
        if (context.Reach(verb) is null)
        {
            return true;
        }

        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return version is { } v && context.Admits(verb, v.SecurityTagsValue);
    }

    private async ValueTask<bool> IsVersionVisibleAsync(string baseWorkflowId, int versionNumber, SecurityFilter security, CancellationToken cancellationToken)
    {
        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return version is { } v && security.IsSatisfiedBy(v.SecurityTagsValue.ToList());
    }

    private async ValueTask<bool> IsReferencedAsync(string workflowId, CancellationToken cancellationToken)
    {
        // A run references a version by its exact versioned workflow id — an indexed exact-match query.
        WorkflowRunPage page = await this.runs.QueryAsync(new WorkflowQuery(WorkflowId: workflowId, Limit: 1), cancellationToken).ConfigureAwait(false);
        return page.Runs.Count > 0;
    }
}