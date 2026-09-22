// <copyright file="ArazzoControlPlaneAdministratorsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;
using AdminKind = Corvus.Text.Json.Arazzo.Durability.Security.WorkflowAdministrators.AdministratorIdentity.KindEntity;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAdministratorsHandler"/> over an <see cref="ISecuredWorkflowCatalog"/> — the
/// control-plane surface that manages a base workflow id's administrator set (design §15). The endpoints are gated by
/// the <c>administrators:read</c>/<c>administrators:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Identity model (§15).</strong> An administrator is a deployment-stamped <c>sys:</c> identity (the same
/// unforgeable internal tags a catalogued version is stamped with), and the set is governed by current-administrator
/// membership — never reach. The operator-facing API never exposes raw internal tags: a write names a grantee
/// (<c>{kind, value}</c>) the server resolves to its exact identity through the <see cref="GranteeResolver"/> (ADR 0008),
/// and a read describes each stored identity back as <c>{dimension, value}</c> grants (via
/// <see cref="ControlPlaneRowSecurityPolicy.DescribeUsageScope"/>). The caller's own identity is read from the
/// deployment's row-security policy, so administration is meaningful only when a policy is configured.</para>
/// <para>An unknown base id and a caller who is not a current administrator are refused identically (403,
/// non-disclosing). A concurrent change that loses the optimistic-concurrency race conflicts (409). Removing the last
/// administrator is refused (409) — a workflow always has at least one. When the catalog client has no administrator
/// store, mutation is unavailable (409); listing still works (the version-1-derived sole administrator).</para>
/// </remarks>
public sealed class ArazzoControlPlaneAdministratorsHandler : IApiAdministratorsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ControlPlaneAccess access;
    private readonly GranteeResolver resolver;
    private readonly IObservedIdentityStore? observed;
    private readonly GovernanceAuditor auditor;

    // The audited resource kind for a workflow-administration change on this surface (design §850).
    private const string TargetKind = "workflow";

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAdministratorsHandler"/> class.</summary>
    /// <param name="catalog">The catalog client that owns the administrator store and the administration operations.</param>
    /// <param name="access">Resolves the caller's deployment identity per request and maps administrator grants to and
    /// from internal tags. Unscoped (no identity) when no row security is configured.</param>
    /// <param name="resolver">Resolves a grantee a write names to its exact deployment-stamped identity (ADR 0008).</param>
    /// <param name="observed">An optional observed-identity store; a newly added administrator is recorded as a resolvable
    /// grantee for the §16.5.4 typeahead (best-effort).</param>
    /// <param name="auditor">The governance auditor; <see langword="null"/> records nothing.</param>
    internal ArazzoControlPlaneAdministratorsHandler(ISecuredWorkflowCatalog catalog, ControlPlaneAccess access, GranteeResolver resolver, IObservedIdentityStore? observed = null, GovernanceAuditor? auditor = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(resolver);
        this.catalog = catalog;
        this.access = access;
        this.resolver = resolver;
        this.observed = observed;
        this.auditor = auditor ?? GovernanceAuditor.None;
    }

    // The §850 audit subject: the authenticated principal who changed the administrator set (the human-facing name;
    // the unforgeable governance identity is the sys: tag set from CallerIdentity), or "control-plane" when unresolved.
    private AuditSubject AuditActor() => this.access.AuditSubject();

    /// <inheritdoc/>
    public async ValueTask<ListAdministratorsResult> HandleListAdministratorsAsync(ListAdministratorsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The catalog hands back the administration record (explicit or the version-1-derived synthetic), or null for an
        // unknown base id. We project it bytes-to-bytes; an unknown base id yields an empty set (200). The record is a
        // pooled document — read it into the response model while it is alive, then dispose it.
        using ParsedJsonDocument<WorkflowAdministrators>? record = await this.catalog.GetAdministratorsAsync((string)parameters.BaseWorkflowId, cancellationToken).ConfigureAwait(false);
        var listContext = new AdministratorListContext(record?.RootElement.Administrators ?? default, this.access);
        return ListAdministratorsResult.Ok(
            Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
            workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AddAdministratorResult> HandleAddAdministratorAsync(AddAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.GranteeReference body = parameters.Body;

        // The grantee is resolved by the server (ADR 0008): the body names a kind and a value, never an identity. The
        // request's kind IS a JSON value; it converts to the store's kind with a straight From() (free rewrap, no reify)
        // and reifies to the domain enum only at the resolver's C#-enum leaf. The resolver's directory leg is the one
        // dependency that can be unreachable; that is reported (502), never guessed around.
        ObservedIdentity.GranteeKind kind;
        SecurityTagSet newAdministrator;
        bool complete;
        try
        {
            if (!body.Kind.IsNotUndefined() || !body.Value.IsNotUndefined())
            {
                ServerThrowHelper.ThrowGranteeKindAndValueRequired();
            }

            kind = ObservedIdentity.GranteeKind.From(body.Kind);
            GranteeResolution resolution = await this.resolver.ResolveAsync(kind.ToGranteeKind(), JsonString.From(body.Value), cancellationToken).ConfigureAwait(false);
            if (resolution.Identity.IsEmpty)
            {
                ServerThrowHelper.ThrowGranteeDoesNotResolve();
            }

            newAdministrator = resolution.Identity;
            complete = resolution.Complete;
        }
        catch (ArgumentException ex)
        {
            return AddAdministratorResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }
        catch (PrincipalDirectoryException)
        {
            return AddAdministratorResult.BadGateway(DirectoryUnavailableProblem(), workspace);
        }

        // baseWorkflowId is the catalog/admin-store key (string-keyed stores) — its genuine leaf is a string, read once.
        string baseWorkflowId = (string)parameters.BaseWorkflowId;

        // The grantee value/label are the JSON values from the request body; they flow to the observed store as those
        // JSON values (From() rewraps, no reify, no managed string) and are written bytes-to-bytes at the store's leaf.
        // The request document outlives this call, so no owned copy is needed.
        JsonString value = JsonString.From(body.Value);
        bool hasLabel = body.Label.IsNotUndefined();
        JsonString label = hasLabel ? JsonString.From(body.Label) : default;

        // The administration record persists the resolved kind as its own (string-enum) JSON value: From() rewraps the
        // observed-store kind (same person/team/role/workflow tokens) into the durable record's kind with no reify.
        AdminKind adminKind = AdminKind.From(kind);

        // Collision guard (§16.5.4): if the resolved identity already belongs to a DIFFERENT recorded grantee, the
        // deployment's identity mapping is not minting unique identities — refuse the ambiguous grant (409) rather than
        // author a grant that would silently also admit that other principal. The message is generic: the conflicting
        // party is never echoed (it may be outside the caller's reach), so the probe — which runs at full reach — does
        // not become a cross-tenant disclosure oracle. The conflicting record (a pooled document) is disposed here.
        if (this.observed is not null)
        {
            using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(kind, value, newAdministrator, cancellationToken).ConfigureAwait(false);
            if (conflict is not null)
            {
                return AddAdministratorResult.Conflict(CollisionProblem(), workspace);
            }
        }

        try
        {
            using ParsedJsonDocument<WorkflowAdministrators> record = await this.catalog.AddAdministratorAsync(baseWorkflowId, newAdministrator, adminKind, hasKind: true, label, hasLabel, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            await this.auditor.MutationAsync("workflow.add-administrator", this.AuditActor(), TargetKind, baseWorkflowId, "added").ConfigureAwait(false);

            // Record the newly named administrator as a resolvable grantee for the §16.5.4 typeahead. Best-effort: the
            // sighting is an idempotent projection and never fails the add. `complete` is the resolver's verdict (§17.2):
            // whole for a directory or recorded identity, the policy's whole-grain verdict for its own mapping.
            if (this.observed is not null)
            {
                await this.observed.SeenAsync(kind, value, label, newAdministrator, complete, "administrator", cancellationToken).ConfigureAwait(false);
            }

            // Broadening advisory (§16.5.4 H5): surface (non-blocking) any existing grantee this new, narrower identity
            // STRICTLY subsumes — the grant broadens administration to also admit it (and everyone whose identity contains
            // the grant). The probe runs at full reach; the echo is reach-filtered to grantees the caller may see (the
            // full count is recorded on the ambient activity for audit). The overlap page is held alive across the
            // synchronous response build below, because the advisory items read its grantee kind/value/label spans.
            using ObservedIdentityPage overlapPage = this.observed is not null
                ? await this.observed.FindBroadeningOverlapsAsync(kind, value, newAdministrator, MaxBroadeningOverlaps, cancellationToken).ConfigureAwait(false)
                : ObservedIdentityPage.Create(new PooledDocumentList<ObservedIdentity>(0));
            List<ObservedIdentity>? overlaps = this.ReachVisibleOverlaps(overlapPage);
            if (overlaps is { Count: > 0 })
            {
                System.Diagnostics.Activity.Current?.SetTag("arazzo.administration.broadening_overlap_count", overlaps.Count);
            }

            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access, overlaps);
            return AddAdministratorResult.Ok(
                Models.AdministratorList.Build(
                    in listContext,
                    administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants),
                    broadeningAdvisory: overlaps is { Count: > 0 }
                        ? Models.BroadeningAdvisory.Build(
                            in listContext,
                            message: (Models.JsonString.Source)BroadeningAdvisoryMessage,
                            subsumesGrantees: Models.BroadeningAdvisory.BroadeningOverlapGranteeArray.Build(in listContext, BuildOverlaps))
                        : default),
                workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return AddAdministratorResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return AddAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return AddAdministratorResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TransferAdministrationResult> HandleTransferAdministrationAsync(TransferAdministrationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;

        // Each grantee is resolved by the server (ADR 0008); the kinds flow as JSON values and reify to the domain enum
        // only at the resolver's leaf. A grantee nothing resolves is refused (400) naming it, since the caller wrote it.
        List<SecurityTagSet> newAdministrators;
        try
        {
            newAdministrators = [];
            foreach (Models.GranteeReference grantee in parameters.Body.Administrators.EnumerateArray())
            {
                if (!grantee.Kind.IsNotUndefined() || !grantee.Value.IsNotUndefined())
                {
                    ServerThrowHelper.ThrowGranteeKindAndValueRequired();
                }

                GranteeResolution resolution = await this.resolver.ResolveAsync(ObservedIdentity.GranteeKind.From(grantee.Kind).ToGranteeKind(), JsonString.From(grantee.Value), cancellationToken).ConfigureAwait(false);
                if (resolution.Identity.IsEmpty)
                {
                    ServerThrowHelper.ThrowAdministratorGranteeDoesNotResolve((string)grantee.Kind, (string)grantee.Value);
                }

                newAdministrators.Add(resolution.Identity);
            }
        }
        catch (ArgumentException ex)
        {
            return TransferAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }
        catch (PrincipalDirectoryException)
        {
            return TransferAdministrationResult.BadGateway(DirectoryUnavailableProblem(), workspace);
        }

        // Collision guard (§16.5.4): refuse the transfer if any named administrator resolves to an identity already held
        // by a different grantee (a non-unique deployment mapping). Generic 409 — the conflicting party is never echoed.
        // The grantee's kind and value flow to the probe as their JSON values (From() rewraps, no reify).
        if (this.observed is not null)
        {
            int index = 0;
            foreach (Models.GranteeReference grantee in parameters.Body.Administrators.EnumerateArray())
            {
                SecurityTagSet resolved = newAdministrators[index++];
                using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(ObservedIdentity.GranteeKind.From(grantee.Kind), JsonString.From(grantee.Value), resolved, cancellationToken).ConfigureAwait(false);
                if (conflict is not null)
                {
                    return TransferAdministrationResult.Conflict(CollisionProblem(), workspace);
                }
            }
        }

        try
        {
            using ParsedJsonDocument<WorkflowAdministrators> record = await this.catalog.TransferAdministrationAsync(baseWorkflowId, newAdministrators, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            await this.auditor.MutationAsync("workflow.transfer-administration", this.AuditActor(), TargetKind, baseWorkflowId, "transferred").ConfigureAwait(false);
            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
            return TransferAdministrationResult.Ok(
                Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
                workspace);
        }
        catch (ArgumentException ex)
        {
            return TransferAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator set", 400, ex.Message), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return TransferAdministrationResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return TransferAdministrationResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return TransferAdministrationResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RemoveAdministratorResult> HandleRemoveAdministratorAsync(RemoveAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;

        // Removal is keyed by the administrator's stable identity digest (§16.5.4) — the opaque key the list/add responses
        // hand back, so the operator never re-presents raw tags. An unmatched digest is an idempotent no-op; we still route
        // through the catalog so the current-administrator gate (403) runs first and membership stays non-disclosing.
        string digest = (string)parameters.Digest;

        try
        {
            using ParsedJsonDocument<WorkflowAdministrators> record = await this.catalog.RemoveAdministratorAsync(baseWorkflowId, digest, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            await this.auditor.MutationAsync("workflow.remove-administrator", this.AuditActor(), TargetKind, baseWorkflowId, "removed").ConfigureAwait(false);
            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
            return RemoveAdministratorResult.Ok(
                Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
                workspace);
        }
        catch (ArgumentException ex)
        {
            // Removing the last administrator would orphan the workflow — refused (a workflow always has one).
            return RemoveAdministratorResult.Conflict(Problem("last-administrator", "Cannot remove the last administrator", 409, ex.Message), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return RemoveAdministratorResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return RemoveAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return RemoveAdministratorResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    // The caller's deployment identity (the internal tags the row-security policy stamps for the current principal).
    // Empty when unscoped, so the current-administrator membership check refuses every mutation (403) — administration
    // management requires a configured policy.
    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    private static Models.ProblemDetails.Source NotAdministratorProblem(string baseWorkflowId)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of '{baseWorkflowId}', or it has no established administration.");

    private static Models.ProblemDetails.Source ConflictProblem(WorkflowAdministrationConflictException ex)
        => Problem("administration-conflict", "Administration changed concurrently", 409, ex.Message);

    private static Models.ProblemDetails.Source UnavailableProblem(NotSupportedException ex)
        => Problem("administration-unavailable", "Administration management unavailable", 409, ex.Message);

    // A resolved grantee identity that already belongs to a different grantee (§16.5.4) — the deployment's identity
    // mapping is not unique. Generic by design: the conflicting party is never named.
    // The resolver's directory leg could not be reached: the identity it would have resolved is never guessed around,
    // the same 502 the explicit directory search reports.
    private static Models.ProblemDetails.Source DirectoryUnavailableProblem()
        => Problem("directory-unavailable", "Directory unavailable", 502, "The external principal directory could not be reached.");

    private static Models.ProblemDetails.Source CollisionProblem()
        => Problem(
            "identity-collision",
            "Ambiguous grantee identity",
            409,
            "The named grantee resolves to an identity that already belongs to a different grantee, so the grant would be ambiguous. Check the deployment's directory identity mapping — it must resolve each distinct principal to a unique identity.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => Models.ProblemDetails.Build(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type);

    // Projects each stored administrator identity as the operator-facing AdministratorGrant: a stable identity digest (the
    // removal key), the identity described back as the {dimension, value} grants it maps from (never raw tags), and the
    // optional resolved kind/label carried verbatim. Built closure-free AND allocation-free off the durable record while it
    // is alive: each administrator's tags are a non-owning view of its persisted {key,value} array, the digest is formatted
    // into a stack span, and each grant is written bytes-native. The list Build is ref-scoped to its `in` argument, so each
    // of the four call sites (list/add/transfer/remove) builds it inline.
    private static void BuildGrants(in AdministratorListContext ctx, ref Models.AdministratorList.AdministratorGrantArray.Builder array)
    {
        if (ctx.Administrators.IsUndefined())
        {
            return;
        }

        ControlPlaneAccess access = ctx.Access;
        foreach (WorkflowAdministrators.AdministratorIdentity administrator in ctx.Administrators.EnumerateArray())
        {
            var state = new GrantState(administrator, TagsOf(administrator), access);
            array.AddItem(Models.AdministratorGrant.Build(in state, BuildGrant));
        }
    }

    // Builds one administrator grant. The digest is the stable hex SHA-256 of the identity's tags, formatted into a stack
    // span and copied into the value by the JsonString.Source conversion; kind/label flow from the durable record as UTF-8
    // leases (no managed string), held until Create copies the bytes. The identity sub-array shares this build's context.
    private static void BuildGrant(in GrantState state, ref Models.AdministratorGrant.Builder grant)
    {
        WorkflowAdministrators.AdministratorIdentity administrator = state.Administrator;
        bool hasKind = administrator.Kind.IsNotUndefined();
        bool hasLabel = administrator.Label.IsNotUndefined();

        // The digest is formatted into a pooled (heap-backed) buffer rather than a stackalloc, so its span is safe to pass
        // alongside the ref-struct build context; the JsonString.Source conversion copies the bytes into the value before
        // this returns, so the buffer is returned immediately after. No managed string (Convert.ToHexStringLower) is formed.
        byte[] digestBuffer = ArrayPool<byte>.Shared.Rent(SecurityIdentityDigest.DigestUtf8Length);
        try
        {
            int digestLength = SecurityIdentityDigest.FormatUtf8(state.Tags, digestBuffer);
            ReadOnlySpan<byte> digest = digestBuffer.AsSpan(0, digestLength);
            if (hasKind && hasLabel)
            {
                using UnescapedUtf8JsonString kind = administrator.Kind.GetUtf8String();
                using UnescapedUtf8JsonString label = administrator.Label.GetUtf8String();
                grant.Create(in state, digest: (Models.JsonString.Source)digest, identity: Models.AdministratorGrant.AdministratorIdentityArray.Build(in state, BuildGrantIdentity), kind: kind.Span, label: (Models.JsonString.Source)label.Span);
            }
            else if (hasKind)
            {
                using UnescapedUtf8JsonString kind = administrator.Kind.GetUtf8String();
                grant.Create(in state, digest: (Models.JsonString.Source)digest, identity: Models.AdministratorGrant.AdministratorIdentityArray.Build(in state, BuildGrantIdentity), kind: kind.Span);
            }
            else if (hasLabel)
            {
                using UnescapedUtf8JsonString label = administrator.Label.GetUtf8String();
                grant.Create(in state, digest: (Models.JsonString.Source)digest, identity: Models.AdministratorGrant.AdministratorIdentityArray.Build(in state, BuildGrantIdentity), label: (Models.JsonString.Source)label.Span);
            }
            else
            {
                grant.Create(in state, digest: (Models.JsonString.Source)digest, identity: Models.AdministratorGrant.AdministratorIdentityArray.Build(in state, BuildGrantIdentity));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(digestBuffer);
        }
    }

    // Describes the administrator's internal tags back as the {dimension, value} grants they map from — the inverse of the
    // grant resolution, allocation-free: the identity's SecurityTagSet is enumerated as unescaped spans and each prefixed
    // tag described bytes-native via the policy's span-based TryDescribeUsageGrant. Internal tags are never exposed raw;
    // unscoped deployments describe nothing.
    private static void BuildGrantIdentity(in GrantState state, ref Models.AdministratorGrant.AdministratorIdentityArray.Builder identities)
    {
        ControlPlaneAccess access = state.Access;
        SecurityTagSet.Utf8Enumerator e = state.Tags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (access.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    var spans = new UsageGrantSpans(dimension, e.CurrentValue);
                    identities.AddItem(Models.AdministratorIdentity.Build(in spans, BuildIdentity));
                }
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildIdentity(in UsageGrantSpans spans, ref Models.AdministratorIdentity.Builder b)
        => b.Create((Models.JsonString.Source)spans.Dimension, (Models.JsonString.Source)spans.Value);

    // A non-owning view of a stored administrator identity's tags (its persisted {key,value} array UTF-8), valid while the
    // backing record is alive — no per-identity copy. Drives both the digest and the {dimension,value} projection.
    private static SecurityTagSet TagsOf(in WorkflowAdministrators.AdministratorIdentity administrator)
        => SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(administrator.Tags).Memory);

    // The most subsumed grantees echoed in a single broadening advisory (a bounded warning, not a listing); the full
    // count is recorded on the activity for audit.
    private const int MaxBroadeningOverlaps = 16;

    // The advisory text — it names the effect (broadening), not a specific grantee; the subsumesGrantees array carries the
    // reach-visible parties.
    private static ReadOnlySpan<byte> BroadeningAdvisoryMessage =>
        "The identity you granted is broader than the listed existing grantees; it confers administration on every principal whose identity contains it, not only those grantees."u8;

    // Reach-filters the broadening overlaps to grantees the caller may see (§17.1): an unrestricted (System) caller sees
    // all; a scoped caller sees only overlaps whose identity its read-reach admits. Returns null when nothing is visible so
    // the advisory is omitted. The returned structs view the still-alive overlap page.
    private List<ObservedIdentity>? ReachVisibleOverlaps(ObservedIdentityPage page)
    {
        if (page.Identities.Count == 0)
        {
            return null;
        }

        SecurityFilter? readReach = this.access.Current().Reach(AccessVerb.Read);
        var visible = new List<ObservedIdentity>(page.Identities.Count);
        foreach (ObservedIdentity overlap in page.Identities)
        {
            // An unrestricted (System) reach admits every overlap; a scoped reach admits only those its read-reach permits.
            if (readReach?.IsSatisfiedBy(overlap.IdentityTagsValue) ?? true)
            {
                visible.Add(overlap);
            }
        }

        return visible.Count > 0 ? visible : null;
    }

    // Builds the advisory's subsumesGrantees array: each reach-visible overlap's kind/value/label, bytes-native from the
    // still-alive overlap page (the same GetUtf8String -> JsonString.Source idiom BuildGrant uses).
    private static void BuildOverlaps(in AdministratorListContext ctx, ref Models.BroadeningAdvisory.BroadeningOverlapGranteeArray.Builder array)
    {
        if (ctx.Overlaps is null)
        {
            return;
        }

        foreach (ObservedIdentity overlap in ctx.Overlaps)
        {
            using UnescapedUtf8JsonString kind = overlap.SubjectKind.GetUtf8String();
            using UnescapedUtf8JsonString value = overlap.SubjectValue.GetUtf8String();
            if (overlap.Label.IsNotUndefined())
            {
                using UnescapedUtf8JsonString label = overlap.Label.GetUtf8String();
                array.AddItem(Models.BroadeningOverlapGrantee.Build(kind: (Models.GranteeKind.Source)kind.Span, value: (Models.JsonString.Source)value.Span, label: (Models.JsonString.Source)label.Span));
            }
            else
            {
                array.AddItem(Models.BroadeningOverlapGrantee.Build(kind: (Models.GranteeKind.Source)kind.Span, value: (Models.JsonString.Source)value.Span));
            }
        }
    }

    private readonly ref struct AdministratorListContext(WorkflowAdministrators.AdministratorIdentityArray administrators, ControlPlaneAccess access, IReadOnlyList<ObservedIdentity>? overlaps = null)
    {
        public WorkflowAdministrators.AdministratorIdentityArray Administrators { get; } = administrators;

        public ControlPlaneAccess Access { get; } = access;

        // The reach-visible existing grantees the just-granted (narrower) identity strictly subsumes (§16.5.4 H5), or null
        // on the read paths that do not compute a broadening advisory. Their kind/value/label back the advisory items; the
        // owning ObservedIdentityPage is kept alive by the add handler across the synchronous response build.
        public IReadOnlyList<ObservedIdentity>? Overlaps { get; } = overlaps;
    }

    // Carries one stored administrator (for its kind/label) plus its tags (a non-owning view, for the digest and identity
    // projection) and the access facade through the closure-free AdministratorGrant build.
    private readonly ref struct GrantState(WorkflowAdministrators.AdministratorIdentity administrator, SecurityTagSet tags, ControlPlaneAccess access)
    {
        public WorkflowAdministrators.AdministratorIdentity Administrator { get; } = administrator;

        public SecurityTagSet Tags { get; } = tags;

        public ControlPlaneAccess Access { get; } = access;
    }
}