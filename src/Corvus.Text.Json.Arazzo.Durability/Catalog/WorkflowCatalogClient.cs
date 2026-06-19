// <copyright file="WorkflowCatalogClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability.Security;

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
    private const int AdministrationMutationRetries = 3;

    private readonly IWorkflowCatalogStore catalog;
    private readonly IWorkflowWaitIndex runs;
    private readonly string actor;
    private readonly ISourceCredentialStore? credentials;
    private readonly IWorkflowAdministratorStore? administrators;

    /// <summary>Initializes a new instance of the <see cref="WorkflowCatalogClient"/> class.</summary>
    /// <param name="catalog">The catalog store.</param>
    /// <param name="runs">The run index, consulted (by exact versioned workflow id) for referential integrity on delete and purge.</param>
    /// <param name="actor">The authenticated identity recorded on writes (<c>createdBy</c>/<c>lastUpdatedBy</c>) and in audit spans.</param>
    /// <param name="credentials">An optional source credential store (design §13). When supplied, adding a version is
    /// refused (<see cref="SourceCredentialAccessDeniedException"/>) if the workflow declares a credential-protected
    /// source the submitter — by the version's security tags — is not entitled to use: the runs would never receive
    /// the credential, so the submission is rejected at catalog time rather than failing silently at run time. When
    /// <see langword="null"/> (the default) no such check is performed.</param>
    /// <param name="administrators">An optional workflow administrator store (design §13/§14.2/§15). When supplied, a
    /// base id's administrator set is governed by it (with the version-1-derived default when no explicit record
    /// exists) and the administration management operations (<see cref="AddAdministratorAsync"/> /
    /// <see cref="RemoveAdministratorAsync"/> / <see cref="TransferAdministrationAsync"/>) are available. When
    /// <see langword="null"/> (the default) administration is the single, immutable version-1 identity and the
    /// management operations throw <see cref="NotSupportedException"/>.</param>
    public WorkflowCatalogClient(IWorkflowCatalogStore catalog, IWorkflowWaitIndex runs, string actor, ISourceCredentialStore? credentials = null, IWorkflowAdministratorStore? administrators = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(actor);
        this.catalog = catalog;
        this.runs = runs;
        this.actor = actor;
        this.credentials = credentials;
        this.administrators = administrators;
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

        // Workflow-id administration (§13/§14.2/§15): a base id's administration is established by its first version's
        // stamped administrator identity and thereafter by its explicit administrator record (transfers / additional
        // administrators); only a current administrator may publish further versions, so the immutable workflow identity
        // (sys:workflow) cannot be squatted. The submitted securityTags carry the submitter's stamped identity. An
        // unknown base id has no administrators yet — the submitter establishes administration by publishing version 1.
        (List<SecurityTagSet> currentAdministrators, _) = await this.LoadAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        if (currentAdministrators.Count > 0 && !IsMember(currentAdministrators, securityTags))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "workflow-not-administered");
            throw new WorkflowAdministrationException(baseWorkflowId);
        }

        // Stamp the immutable workflow identity so the version (and its runs) carry sys:workflow=<baseWorkflowId> — the
        // identity a source credential grant names. Combined with the owner identity, this is the run's unforgeable
        // entitlement for the catalog-time and run-time usage checks.
        SecurityTagSet effectiveTags = WorkflowIdentity.WithWorkflowTag(securityTags, baseWorkflowId);

        // Catalog-time usage gate (§13): refuse to catalogue a workflow that declares a credential-protected source the
        // submitter is not entitled to use (by the version's effective tags, which its runs inherit) — fail early rather
        // than hand the run no credential later. A source with no bindings (unauthenticated, or bindings added later) is
        // allowed; the run-time check at transport bind is the backstop.
        if (this.credentials is { } credentialStore)
        {
            List<string>? denied = null;
            foreach (string source in CatalogPackage.ReadSourceNames(packageUtf8))
            {
                if (await credentialStore.EvaluateSourceAccessAsync(source, effectiveTags, cancellationToken).ConfigureAwait(false) == CredentialSourceAccess.Denied)
                {
                    (denied ??= []).Add(source);
                }
            }

            if (denied is not null)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "source-access-denied");
                throw new SourceCredentialAccessDeniedException(denied);
            }
        }

        CatalogVersion version = await this.catalog.AddAsync(
            baseWorkflowId, packageUtf8, new CatalogMetadata(owner, this.actor, tags, effectiveTags), cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityTagSet>> GetAdministratorsAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        (List<SecurityTagSet> admins, _) = await this.LoadAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return admins;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityTagSet>> AddAdministratorAsync(string baseWorkflowId, SecurityTagSet newAdministrator, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
        => this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "add",
            static (admins, newAdministrator) =>
            {
                if (IsMember(admins, newAdministrator))
                {
                    return null; // already an administrator — idempotent no-op
                }

                admins.Add(newAdministrator);
                return admins;
            },
            newAdministrator,
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityTagSet>> RemoveAdministratorAsync(string baseWorkflowId, SecurityTagSet administrator, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
        => this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "remove",
            static (admins, administrator) =>
            {
                int index = admins.FindIndex(a => WorkflowIdentity.SameAdministrator(a, administrator));
                if (index < 0)
                {
                    return null; // not an administrator — idempotent no-op
                }

                if (admins.Count == 1)
                {
                    throw new ArgumentException("Cannot remove the last administrator of a workflow; a workflow must always have at least one administrator.", nameof(administrator));
                }

                admins.RemoveAt(index);
                return admins;
            },
            administrator,
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityTagSet>> TransferAdministrationAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> newAdministrators, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newAdministrators);
        if (newAdministrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration transfer requires at least one new administrator.", nameof(newAdministrators));
        }

        List<SecurityTagSet> deduped = Dedupe(newAdministrators);
        return this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "transfer",
            static (_, replacement) => replacement,
            deduped,
            cancellationToken);
    }

    // Loads the current administrator identities of a base id and the etag to act against: the explicit administrator
    // record when present, else the version-1-derived default (a single administrator identity, with WorkflowEtag.None
    // signalling "no record yet"). An unknown base id yields an empty list (no administration established).
    private async ValueTask<(List<SecurityTagSet> Administrators, WorkflowEtag Etag)> LoadAdministratorsAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        if (this.administrators is { } store)
        {
            using ParsedJsonDocument<WorkflowAdministrators>? record = await store.GetAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                return (record.RootElement.AdministratorIdentitiesValue, record.RootElement.EtagValue);
            }
        }

        CatalogVersion? firstVersion = await this.catalog.GetAsync(baseWorkflowId, 1, cancellationToken).ConfigureAwait(false);
        List<SecurityTagSet> fallback = firstVersion is { } established
            ? [WorkflowIdentity.AdministratorIdentity(established.SecurityTagsValue)]
            : [];
        return (fallback, WorkflowEtag.None);
    }

    // The read-modify-write core for the administration management operations (§15): load the current administrator
    // set, authorize the caller as a current administrator, apply the mutation, and persist under optimistic
    // concurrency — retrying a bounded number of times if a concurrent change wins the CAS race. A mutation that
    // returns null is a no-op (the operation was idempotent), so no write happens.
    private async ValueTask<IReadOnlyList<SecurityTagSet>> MutateAdministratorsAsync<TArg>(string baseWorkflowId, SecurityTagSet callerIdentity, string operation, Func<List<SecurityTagSet>, TArg, List<SecurityTagSet>?> mutate, TArg argument, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        if (this.administrators is not { } store)
        {
            throw new NotSupportedException("Workflow administration management requires an administrator store; none is configured on this catalog client.");
        }

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity($"catalog.administration.{operation}");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);

        for (int attempt = 0; ; attempt++)
        {
            (List<SecurityTagSet> admins, WorkflowEtag etag) = await this.LoadAdministratorsAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);

            // An unknown base id, or a caller who is not a current administrator, is refused identically (non-disclosing).
            if (admins.Count == 0 || !IsMember(admins, callerIdentity))
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "not-administered");
                throw new WorkflowAdministrationException(baseWorkflowId);
            }

            List<SecurityTagSet>? next = mutate(admins, argument);
            if (next is null)
            {
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, "unchanged");
                return admins;
            }

            try
            {
                using ParsedJsonDocument<WorkflowAdministrators> updated = await store.PutAsync(baseWorkflowId, next, etag, this.actor, cancellationToken).ConfigureAwait(false);
                activity?.SetTag(ArazzoTelemetry.OutcomeTag, operation);
                return updated.RootElement.AdministratorIdentitiesValue;
            }
            catch (WorkflowAdministrationConflictException) when (attempt < AdministrationMutationRetries)
            {
                // A concurrent administration change rotated the etag; reload and retry.
            }
        }
    }

    // Whether a candidate administrator identity is a member of a set (order-independent set equality on any entry).
    private static bool IsMember(List<SecurityTagSet> admins, SecurityTagSet candidate)
    {
        foreach (SecurityTagSet administrator in admins)
        {
            if (WorkflowIdentity.SameAdministrator(administrator, candidate))
            {
                return true;
            }
        }

        return false;
    }

    // Coalesces an administrator list, dropping set-equal duplicates so the persisted set carries each identity once.
    private static List<SecurityTagSet> Dedupe(IReadOnlyList<SecurityTagSet> admins)
    {
        var deduped = new List<SecurityTagSet>(admins.Count);
        foreach (SecurityTagSet administrator in admins)
        {
            if (!IsMember(deduped, administrator))
            {
                deduped.Add(administrator);
            }
        }

        return deduped;
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
        return version is { } v && security.IsSatisfiedBy(v.SecurityTagsValue);
    }

    private async ValueTask<bool> IsReferencedAsync(string workflowId, CancellationToken cancellationToken)
    {
        // A run references a version by its exact versioned workflow id — an indexed exact-match query.
        WorkflowRunPage page = await this.runs.QueryAsync(new WorkflowQuery(WorkflowId: workflowId, Limit: 1), cancellationToken).ConfigureAwait(false);
        return page.Runs.Count > 0;
    }
}