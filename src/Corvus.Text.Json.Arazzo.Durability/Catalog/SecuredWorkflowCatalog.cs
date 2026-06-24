// <copyright file="SecuredWorkflowCatalog.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability.Security;
using AdministratorIdentity = Corvus.Text.Json.Arazzo.Durability.Security.WorkflowAdministrators.AdministratorIdentity;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The default <see cref="ISecuredWorkflowCatalog"/> over an <see cref="IWorkflowCatalogStore"/> and the run
/// store. It enforces the submission rules (no <c>-vN</c> suffix), enforces referential integrity on delete and
/// purge by consulting the run store, and emits <c>catalog.*</c> audit spans tagged with the actor, base id,
/// version and outcome — telemetry is the forensic trail; the durable record carries <c>createdBy</c>/
/// <c>lastUpdatedBy</c> for governance visibility.
/// </summary>
public sealed class SecuredWorkflowCatalog : ISecuredWorkflowCatalog
{
    private const int AdministrationMutationRetries = 3;

    private readonly IWorkflowCatalogStore catalog;
    private readonly IWorkflowWaitIndex runs;
    private readonly string actor;
    private readonly ISourceCredentialStore? credentials;
    private readonly IWorkflowAdministratorStore? administrators;

    /// <summary>Initializes a new instance of the <see cref="SecuredWorkflowCatalog"/> class.</summary>
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
    public SecuredWorkflowCatalog(IWorkflowCatalogStore catalog, IWorkflowWaitIndex runs, string actor, ISourceCredentialStore? credentials = null, IWorkflowAdministratorStore? administrators = null)
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
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, CancellationToken cancellationToken)
        => this.AddAsync(packageUtf8, owner, tags, securityTags: default, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(ReadOnlyMemory<byte> packageUtf8, CatalogOwner owner, TagSet tags, SecurityTagSet securityTags, CancellationToken cancellationToken)
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
        (bool established, bool isAdministrator) = await this.CheckAdministrationAsync(baseWorkflowId, securityTags, cancellationToken).ConfigureAwait(false);
        if (established && !isAdministrator)
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

        ParsedJsonDocument<CatalogVersion> version = await this.catalog.AddAsync(
            baseWorkflowId, packageUtf8, new CatalogMetadata(owner, this.actor, tags, effectiveTags), cancellationToken).ConfigureAwait(false);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, version.RootElement.Ref.VersionNumber);
        activity?.SetTag(ArazzoTelemetry.WorkflowIdTag, version.RootElement.Ref.WorkflowId);
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
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        // A version outside the caller's read reach is reported as absent (non-disclosing, §14.2) — dispose the document
        // we won't hand back so its pooled buffer is returned rather than leaked.
        if (context.Admits(AccessVerb.Read, version.RootElement.SecurityTagsValue))
        {
            return version;
        }

        version.Dispose();
        return null;
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
    public async ValueTask<CatalogUpdateResult> UpdateAsync(string baseWorkflowId, int versionNumber, CatalogOwner? owner, TagSet? tags, CatalogStatus? status, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.update");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        // Resolve read-then-write reach on a single fetch (§14.2): outside read reach → 404 (non-disclosing); readable
        // but outside write reach → 403. The handler relies on this split, so it no longer pre-fetches the version itself.
        WriteReach reach = await this.ResolveWriteReachAsync(baseWorkflowId, versionNumber, context, cancellationToken).ConfigureAwait(false);
        if (reach == WriteReach.NotFound)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return new CatalogUpdateResult(CatalogUpdateOutcome.NotFound, null);
        }

        if (reach == WriteReach.Forbidden)
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "forbidden");
            return new CatalogUpdateResult(CatalogUpdateOutcome.Forbidden, null);
        }

        ParsedJsonDocument<CatalogVersion>? updated = await this.catalog.UpdateMetadataAsync(
            baseWorkflowId, versionNumber, new CatalogMetadataPatch(this.actor, owner, tags, status), cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            // Unrestricted write reach with no version present (or a concurrent delete) → not found.
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return new CatalogUpdateResult(CatalogUpdateOutcome.NotFound, null);
        }

        activity?.SetTag(ArazzoTelemetry.OutcomeTag, "updated");
        return new CatalogUpdateResult(CatalogUpdateOutcome.Updated, updated);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogDeleteOutcome> DeleteAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("catalog.delete");
        activity?.SetTag(ArazzoTelemetry.ActorTag, this.actor);
        activity?.SetTag(ArazzoTelemetry.BaseWorkflowIdTag, baseWorkflowId);
        activity?.SetTag(ArazzoTelemetry.VersionNumberTag, versionNumber);

        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);

        // Read-then-write reach on the single fetch (§14.2): absent or outside read reach → 404 (non-disclosing); the
        // handler relies on this split, so it no longer pre-fetches the version itself.
        if (version is not { } v || !context.Admits(AccessVerb.Read, v.RootElement.SecurityTagsValue))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "missing");
            return CatalogDeleteOutcome.NotFound;
        }

        // Readable but outside write reach → forbidden (the caller can already GET it, so this discloses nothing new).
        if (!context.Admits(AccessVerb.Write, v.RootElement.SecurityTagsValue))
        {
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, "forbidden");
            return CatalogDeleteOutcome.Forbidden;
        }

        if (await this.IsReferencedAsync((string)v.RootElement.WorkflowId, cancellationToken).ConfigureAwait(false))
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
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAdministratorsAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        if (this.administrators is { } store)
        {
            ParsedJsonDocument<WorkflowAdministrators>? record = await store.GetAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                return record;
            }
        }

        // No explicit record: administration defaults to version 1's administrator identity. Materialize that as a
        // synthetic (display-only) record so callers project administration uniformly. An unknown base id yields null.
        using ParsedJsonDocument<CatalogVersion>? firstVersion = await this.catalog.GetAsync(baseWorkflowId, 1, cancellationToken).ConfigureAwait(false);
        if (firstVersion is null)
        {
            return null;
        }

        SecurityTagSet ownerIdentity = WorkflowIdentity.AdministratorIdentity(firstVersion.RootElement.SecurityTagsValue);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        AdministratorIdentity owner = WorkflowAdministrators.BuildIdentity(workspace, ownerIdentity, default, hasKind: false, default, hasLabel: false);
        return WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, [owner], this.actor, default, WorkflowEtag.None);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> AddAdministratorAsync(string baseWorkflowId, SecurityTagSet identity, AdministratorIdentity.KindEntity kind, bool hasKind, JsonString label, bool hasLabel, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
    {
        // Build the resolved administrator identity once (workspace held across the etag-retry loop's awaits, so it must be
        // the unrented, thread-affinity-free workspace); the RMW carries the existing identities forward bytes-to-bytes and
        // appends this one.
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        AdministratorIdentity newAdministrator = WorkflowAdministrators.BuildIdentity(workspace, identity, kind, hasKind, label, hasLabel);
        return await this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "add",
            static (admins, newAdministrator) =>
            {
                if (IsMember(admins, TagsOf(newAdministrator)))
                {
                    return null; // already an administrator — idempotent no-op
                }

                admins.Add(newAdministrator);
                return admins;
            },
            newAdministrator,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<WorkflowAdministrators>> RemoveAdministratorAsync(string baseWorkflowId, string digest, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
        => this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "remove",
            static (admins, digest) =>
            {
                int index = IndexOfDigest(admins, digest);
                if (index < 0)
                {
                    return null; // no administrator with that digest — idempotent no-op
                }

                if (admins.Count == 1)
                {
                    throw new ArgumentException("Cannot remove the last administrator of a workflow; a workflow must always have at least one administrator.", nameof(digest));
                }

                admins.RemoveAt(index);
                return admins;
            },
            digest,
            cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> TransferAdministrationAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> newAdministrators, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newAdministrators);
        if (newAdministrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration transfer requires at least one new administrator.", nameof(newAdministrators));
        }

        // Materialize each resolved identity as the durable AdministratorIdentity (tags only — the bulk hand-off carries no
        // per-administrator kind/label) in a workspace held across the etag-retry loop's awaits (so it must be the unrented,
        // thread-affinity-free workspace), then coalesce set-equal duplicates.
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        var identities = new List<AdministratorIdentity>(newAdministrators.Count);
        foreach (SecurityTagSet tags in newAdministrators)
        {
            identities.Add(WorkflowAdministrators.BuildIdentity(workspace, tags, default, hasKind: false, default, hasLabel: false));
        }

        List<AdministratorIdentity> deduped = Dedupe(identities);
        return await this.MutateAdministratorsAsync(
            baseWorkflowId,
            callerIdentity,
            "transfer",
            static (_, replacement) => replacement,
            deduped,
            cancellationToken).ConfigureAwait(false);
    }

    // Read-only administration probe for the publish gate (§13/§14.2): whether the base id has an established administration
    // and, if so, whether the candidate (the submitter's stamped identity) is one of its administrators — compared by tags
    // set-equality, no identity-list materialization. An unknown base id (no explicit record and no version 1) is
    // unestablished, so the submitter establishes administration by publishing version 1. Works whether or not an explicit
    // administrator store is configured (the version-1 identity is the implicit default).
    private async ValueTask<(bool Established, bool IsAdministrator)> CheckAdministrationAsync(string baseWorkflowId, SecurityTagSet candidate, CancellationToken cancellationToken)
    {
        if (this.administrators is { } store)
        {
            using ParsedJsonDocument<WorkflowAdministrators>? record = await store.GetAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                return (true, record.RootElement.IsAdministeredBy(candidate));
            }
        }

        using ParsedJsonDocument<CatalogVersion>? firstVersion = await this.catalog.GetAsync(baseWorkflowId, 1, cancellationToken).ConfigureAwait(false);
        if (firstVersion is null)
        {
            return (false, false);
        }

        SecurityTagSet ownerIdentity = WorkflowIdentity.AdministratorIdentity(firstVersion.RootElement.SecurityTagsValue);
        return (true, WorkflowIdentity.SameAdministrator(ownerIdentity, candidate));
    }

    // Loads the current administrators of a base id for a mutation: the explicit record (returned to keep its identities
    // alive for bytes-to-bytes carry-forward) with its etag, else the version-1-derived default identity built in a
    // workspace (with WorkflowEtag.None signalling "no record yet"). An unknown base id yields an empty set. The caller
    // disposes the returned record / workspace.
    private async ValueTask<(ParsedJsonDocument<WorkflowAdministrators>? Record, JsonWorkspace? FallbackWorkspace, List<AdministratorIdentity> Administrators, WorkflowEtag Etag)> LoadForMutateAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        IWorkflowAdministratorStore store = this.administrators!;
        ParsedJsonDocument<WorkflowAdministrators>? record = await store.GetAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        if (record is not null)
        {
            WorkflowAdministrators current = record.RootElement;
            var admins = new List<AdministratorIdentity>(current.AdministratorCount);
            if (current.Administrators.IsNotUndefined())
            {
                admins.AddRange(current.Administrators.EnumerateArray());
            }

            return (record, null, admins, current.EtagValue);
        }

        using ParsedJsonDocument<CatalogVersion>? firstVersion = await this.catalog.GetAsync(baseWorkflowId, 1, cancellationToken).ConfigureAwait(false);
        if (firstVersion is null)
        {
            return (null, null, [], WorkflowEtag.None);
        }

        SecurityTagSet ownerIdentity = WorkflowIdentity.AdministratorIdentity(firstVersion.RootElement.SecurityTagsValue);

        // The caller (MutateAdministratorsAsync) disposes this workspace in a finally that runs after its PutAsync await, so
        // it may dispose on a different thread — it must be the unrented, thread-affinity-free workspace.
        JsonWorkspace fallbackWorkspace = JsonWorkspace.CreateUnrented();
        AdministratorIdentity owner = WorkflowAdministrators.BuildIdentity(fallbackWorkspace, ownerIdentity, default, hasKind: false, default, hasLabel: false);
        return (null, fallbackWorkspace, [owner], WorkflowEtag.None);
    }

    // The read-modify-write core for the administration management operations (§15): load the current administrators,
    // authorize the caller as a current administrator, apply the mutation, and persist under optimistic concurrency —
    // retrying a bounded number of times if a concurrent change wins the CAS race. Returns the resulting administration
    // record (the caller projects/owns it). A mutation that returns null is a no-op (idempotent), returning the current
    // record unchanged. The existing identities are carried forward bytes-to-bytes (referencing the loaded record, held
    // alive across the write).
    private async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> MutateAdministratorsAsync<TArg>(string baseWorkflowId, SecurityTagSet callerIdentity, string operation, Func<List<AdministratorIdentity>, TArg, List<AdministratorIdentity>?> mutate, TArg argument, CancellationToken cancellationToken)
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
            (ParsedJsonDocument<WorkflowAdministrators>? record, JsonWorkspace? fallbackWorkspace, List<AdministratorIdentity> admins, WorkflowEtag etag) =
                await this.LoadForMutateAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            bool handedOffRecord = false;
            try
            {
                // An unknown base id, or a caller who is not a current administrator, is refused identically (non-disclosing).
                if (admins.Count == 0 || !IsMember(admins, callerIdentity))
                {
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, "not-administered");
                    throw new WorkflowAdministrationException(baseWorkflowId);
                }

                List<AdministratorIdentity>? next = mutate(admins, argument);
                if (next is null)
                {
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, "unchanged");
                    if (record is not null)
                    {
                        handedOffRecord = true;
                        return record;
                    }

                    // No explicit record and nothing changed: materialize the current (fallback) set for the caller.
                    return WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, admins, this.actor, default, WorkflowEtag.None);
                }

                try
                {
                    ParsedJsonDocument<WorkflowAdministrators> updated = await store.PutAsync(baseWorkflowId, next, etag, this.actor, cancellationToken).ConfigureAwait(false);
                    activity?.SetTag(ArazzoTelemetry.OutcomeTag, operation);
                    return updated;
                }
                catch (WorkflowAdministrationConflictException) when (attempt < AdministrationMutationRetries)
                {
                    // A concurrent administration change rotated the etag; reload and retry.
                }
            }
            finally
            {
                if (!handedOffRecord)
                {
                    record?.Dispose();
                }

                fallbackWorkspace?.Dispose();
            }
        }
    }

    // Whether a candidate identity is a member of a set (order-independent set equality on any entry's tags).
    private static bool IsMember(List<AdministratorIdentity> admins, SecurityTagSet candidate)
    {
        foreach (AdministratorIdentity administrator in admins)
        {
            if (WorkflowIdentity.SameAdministrator(TagsOf(administrator), candidate))
            {
                return true;
            }
        }

        return false;
    }

    // The index of the administrator whose identity digest equals the (hex, ASCII) target, or -1 if none matches.
    private static int IndexOfDigest(List<AdministratorIdentity> admins, string digest)
    {
        Span<byte> target = stackalloc byte[SecurityIdentityDigest.DigestUtf8Length];
        if (digest.Length != SecurityIdentityDigest.DigestUtf8Length)
        {
            return -1;
        }

        int targetLength = Encoding.ASCII.GetBytes(digest, target);
        Span<byte> buffer = stackalloc byte[SecurityIdentityDigest.DigestUtf8Length];
        for (int i = 0; i < admins.Count; i++)
        {
            int written = SecurityIdentityDigest.FormatUtf8(TagsOf(admins[i]), buffer);
            if (written == targetLength && buffer[..written].SequenceEqual(target[..targetLength]))
            {
                return i;
            }
        }

        return -1;
    }

    // A non-owning view of an administrator identity's tags (the raw {key,value} array UTF-8), valid while the backing
    // document/workspace is alive — no per-identity copy.
    private static SecurityTagSet TagsOf(in AdministratorIdentity administrator)
        => SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(administrator.Tags).Memory);

    // Coalesces an administrator list, dropping set-equal duplicates so the persisted set carries each identity once.
    private static List<AdministratorIdentity> Dedupe(IReadOnlyList<AdministratorIdentity> admins)
    {
        var deduped = new List<AdministratorIdentity>(admins.Count);
        foreach (AdministratorIdentity administrator in admins)
        {
            if (!IsMember(deduped, TagsOf(administrator)))
            {
                deduped.Add(administrator);
            }
        }

        return deduped;
    }

    // Whether a version is within the caller's reach for a verb (§14.2): unrestricted reach short-circuits without
    // a fetch; otherwise the version must exist and its security tags must satisfy the verb's reach.
    // The write-reach verdict ResolveWriteReachAsync returns: writable, absent/unreadable (→404), or readable-not-writable (→403).
    private enum WriteReach
    {
        Writable,
        NotFound,
        Forbidden,
    }

    // Resolves the write-reach outcome on a single fetch, distinguishing 404 (absent or outside read reach) from 403
    // (readable but outside write reach) so callers need not pre-fetch the version themselves. Read is checked BEFORE
    // write: a caller who cannot read the version must not learn it exists via a 403 (§14.2 non-disclosure).
    private async ValueTask<WriteReach> ResolveWriteReachAsync(string baseWorkflowId, int versionNumber, AccessContext context, CancellationToken cancellationToken)
    {
        // Unrestricted write reach (e.g. the trusted system path) needs no inspection; existence falls to the mutation.
        if (context.Reach(AccessVerb.Write) is null)
        {
            return WriteReach.Writable;
        }

        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (version is not { } v || !context.Admits(AccessVerb.Read, v.RootElement.SecurityTagsValue))
        {
            return WriteReach.NotFound;
        }

        return context.Admits(AccessVerb.Write, v.RootElement.SecurityTagsValue) ? WriteReach.Writable : WriteReach.Forbidden;
    }

    private async ValueTask<bool> IsWithinReachAsync(string baseWorkflowId, int versionNumber, AccessContext context, AccessVerb verb, CancellationToken cancellationToken)
    {
        if (context.Reach(verb) is null)
        {
            return true;
        }

        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return version is { } v && context.Admits(verb, v.RootElement.SecurityTagsValue);
    }

    private async ValueTask<bool> IsVersionVisibleAsync(string baseWorkflowId, int versionNumber, SecurityFilter security, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return version is { } v && security.IsSatisfiedBy(v.RootElement.SecurityTagsValue);
    }

    private async ValueTask<bool> IsReferencedAsync(string workflowId, CancellationToken cancellationToken)
    {
        // A run references a version by its exact versioned workflow id — an indexed exact-match query.
        WorkflowRunPage page = await this.runs.QueryAsync(new WorkflowQuery(WorkflowId: workflowId, Limit: 1), cancellationToken).ConfigureAwait(false);
        return page.Runs.Count > 0;
    }
}