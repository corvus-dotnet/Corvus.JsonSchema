// <copyright file="ArazzoControlPlaneEnvironmentsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using AdminKind = Corvus.Text.Json.Arazzo.Durability.Security.EnvironmentAdministrators.AdministratorIdentity.KindEntity;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiEnvironmentsHandler"/> over an <see cref="IEnvironmentStore"/> and a
/// <see cref="SecuredEnvironmentAdministration"/> — the control-plane surface that manages governed, reach-scoped
/// deployment environments and their administrators (design §7.7). The endpoints are gated by the
/// <c>environments:read</c>/<c>environments:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Two-layer authorization.</strong> Visibility and the data plane (list/get/create/update/delete) are
/// reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) over each environment's <c>managementTags</c>; an
/// invisible environment is reported as not found. Governance (update/delete/administrator changes) additionally requires
/// <em>current-administrator</em> membership — creating an environment grants the creator administration (§7.7), and an
/// administrator-gated mutation a non-administrator attempts is refused (403, non-disclosing once visibility is
/// established). The <see cref="EnvironmentSummary"/> response is congruent with the persisted <see cref="Environment"/>,
/// so reads project as a free whole-document re-wrap (<see cref="Models.EnvironmentSummary"/><c>.From</c>).</para>
/// </remarks>
public sealed class ArazzoControlPlaneEnvironmentsHandler : IApiEnvironmentsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IEnvironmentStore store;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ControlPlaneAccess access;
    private readonly IObservedIdentityStore? observed;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/>).</summary>
    /// <param name="store">The persistent environment store.</param>
    /// <param name="administration">The environment-administration governance service.</param>
    /// <param name="actor">The audit actor recorded on writes.</param>
    public ArazzoControlPlaneEnvironmentsHandler(IEnvironmentStore store, SecuredEnvironmentAdministration administration, string actor = "control-plane")
        : this(store, administration, new ControlPlaneAccess(), null, actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneEnvironmentsHandler"/> class.</summary>
    /// <param name="store">The persistent environment store the data-plane endpoints delegate to.</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request, the internal tags stamped onto
    /// created environments, and the grant↔internal-tag mapping for administrator identities (§14.2/§16.5.4).</param>
    /// <param name="observed">An optional observed-identity store; a newly added administrator is recorded as a resolvable
    /// grantee for the §16.5.4 typeahead (best-effort).</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    internal ArazzoControlPlaneEnvironmentsHandler(IEnvironmentStore store, SecuredEnvironmentAdministration administration, ControlPlaneAccess access, IObservedIdentityStore? observed = null, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.administration = administration;
        this.access = access;
        this.observed = observed;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListEnvironmentsResult> HandleListEnvironmentsAsync(ListEnvironmentsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // managed string; an undefined token rewraps to an undefined JsonString); the store decodes it bytes-native.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using EnvironmentPage page = await this.store.ListAsync(this.access.Current(), limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The persisted Environment and the API EnvironmentSummary share the same JSON shape, so each environment is a
        // free whole-document re-wrap (Models.EnvironmentSummary.From) — no per-field projection. The summaries reference
        // the pooled documents, and the body is validated/serialized after this returns, so hand the documents to the
        // workspace (it disposes them at request end); `using page` then only returns the batch's backing array.
        page.Environments.TransferOwnershipTo(workspace);
        IReadOnlyList<Environment> environments = page.Environments;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.EnvironmentList.Source<IReadOnlyList<Environment>> body = Models.EnvironmentList.Build(
            in environments,
            environments: Models.EnvironmentList.EnvironmentSummaryArray.Build(in environments, BuildEnvironments),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListEnvironmentsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetEnvironmentResult> HandleGetEnvironmentAsync(GetEnvironmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        ParsedJsonDocument<Environment>? environment = await this.store.GetAsync(name, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environment is not { } e)
        {
            return GetEnvironmentResult.NotFound(NotFoundProblem(name), workspace);
        }

        // Congruent whole-document re-wrap (Models.EnvironmentSummary.From) — hand the pooled document to the workspace so
        // the deferred body validation/serialization is safe (it disposes the document afterwards).
        workspace.TakeOwnership(e);
        return GetEnvironmentResult.Ok(Models.EnvironmentSummary.From(e.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateEnvironmentResult> HandleCreateEnvironmentAsync(CreateEnvironmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.EnvironmentWrite body = parameters.Body;
        SecurityTagSet managementTags;
        try
        {
            if (!body.Name.IsNotUndefined())
            {
                throw new ArgumentException("A 'name' is required.");
            }

            // managementTags = the principal's deployment-internal tenant tag (always stamped, so the creator keeps
            // management) PLUS any operator-supplied management labels (validated against the reserved internal prefix as a
            // non-owning view over the request body's array). Built once into a SecurityTagSet — the draft and the
            // privilege-escalation guard both read it.
            SecurityTagSet userManagement = body.ManagementTags.IsNotUndefined()
                ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(body.ManagementTags).Memory)
                : SecurityTagSet.Empty;
            this.access.ValidateUserTags(userManagement);
            var tagsState = new ManagementTagsState(this.access.InternalTags(), userManagement);
            managementTags = SecurityTagSet.Build(in tagsState, WriteManagementTags);
        }
        catch (ArgumentException ex)
        {
            return CreateEnvironmentResult.BadRequest(Problem("invalid-environment", "Invalid environment", 400, ex.Message), workspace);
        }

        // Guard against privilege escalation: a principal may not create an environment it could not itself manage.
        if (!managementTags.IsEmpty && !this.access.Current().Admits(AccessVerb.Write, managementTags))
        {
            return CreateEnvironmentResult.BadRequest(
                Problem("management-out-of-reach", "Management scope out of reach", 400, "The environment's management tags are outside your own management reach."), workspace);
        }

        string name = (string)body.Name;
        try
        {
            // The persisted environment carries the request body's mutable JSON values bytes-to-bytes plus the resolved
            // management tags; the store stamps createdBy/createdAt/etag.
            using ParsedJsonDocument<Environment> draft = Environment.Draft(
                (JsonElement)body.Name,
                (JsonElement)body.DisplayName,
                (JsonElement)body.Description,
                managementTags);
            ParsedJsonDocument<Environment> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);

            // Create-grants-admin (§7.7): materialize the administration record with the creator's resolved identity as the
            // sole administrator. Unscoped deployments (no internal identity) cannot establish governance — administration
            // management requires a configured policy, exactly like workflow §15.
            SecurityTagSet callerIdentity = SecurityTagSet.FromTags(this.access.InternalTags());
            if (!callerIdentity.IsEmpty)
            {
                await this.administration.EstablishAsync(name, callerIdentity, default, hasKind: false, default, hasLabel: false, cancellationToken).ConfigureAwait(false);
            }

            workspace.TakeOwnership(created);
            return CreateEnvironmentResult.Created(Models.EnvironmentSummary.From(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateEnvironmentResult.BadRequest(Problem("invalid-environment", "Invalid environment", 400, ex.Message), workspace);
        }
        catch (InvalidOperationException ex)
        {
            return CreateEnvironmentResult.Conflict(Problem("environment-exists", "Environment already exists", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateEnvironmentResult> HandleUpdateEnvironmentAsync(UpdateEnvironmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;

        // Visibility first (404 if outside reach), then current-administrator membership (403) — the §7.7 governance gate.
        GovernanceGate gate = await this.AuthorizeGovernanceAsync(name, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return UpdateEnvironmentResult.NotFound(NotFoundProblem(name), workspace);
        }

        if (gate == GovernanceGate.Forbidden)
        {
            return UpdateEnvironmentResult.Forbidden(NotAdministratorProblem(name), workspace);
        }

        Models.EnvironmentUpdate body = parameters.Body;

        // Only the mutable content (display name, description) is carried bytes-to-bytes; the immutable name + management
        // tags + created-* audit are carried forward from the stored environment by the store, so the draft omits them.
        using ParsedJsonDocument<Environment> draft = Environment.Draft(default, (JsonElement)body.DisplayName, (JsonElement)body.Description, SecurityTagSet.Empty);
        ParsedJsonDocument<Environment>? updated = await this.store.UpdateAsync(name, draft.RootElement, WorkflowEtag.None, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (updated is not { } e)
        {
            return UpdateEnvironmentResult.NotFound(NotFoundProblem(name), workspace);
        }

        workspace.TakeOwnership(e);
        return UpdateEnvironmentResult.Ok(Models.EnvironmentSummary.From(e.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteEnvironmentResult> HandleDeleteEnvironmentAsync(DeleteEnvironmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;

        GovernanceGate gate = await this.AuthorizeGovernanceAsync(name, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return DeleteEnvironmentResult.NotFound(NotFoundProblem(name), workspace);
        }

        if (gate == GovernanceGate.Forbidden)
        {
            return DeleteEnvironmentResult.Forbidden(NotAdministratorProblem(name), workspace);
        }

        bool deleted = await this.store.DeleteAsync(name, WorkflowEtag.None, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return DeleteEnvironmentResult.NotFound(NotFoundProblem(name), workspace);
        }

        // Clean up the now-orphaned administration record (best-effort; the environment is gone either way).
        await this.administration.DeleteRecordAsync(name, cancellationToken).ConfigureAwait(false);
        return DeleteEnvironmentResult.NoContent();
    }

    /// <inheritdoc/>
    public async ValueTask<ListEnvironmentAdministratorsResult> HandleListEnvironmentAdministratorsAsync(ListEnvironmentAdministratorsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(name, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return ListEnvironmentAdministratorsResult.NotFound(NotFoundProblem(name), workspace);
        }

        var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
        return ListEnvironmentAdministratorsResult.Ok(
            Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
            workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AddEnvironmentAdministratorResult> HandleAddEnvironmentAdministratorAsync(AddEnvironmentAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        Models.AdministratorMemberWrite body = parameters.Body;

        SecurityTagSet newAdministrator;
        ObservedIdentity.GranteeKind kind = default;
        bool hasKind = false;
        bool complete;
        try
        {
            if (body.Identity.IsNotUndefined() && body.Identity.GetArrayLength() > 0)
            {
                // Resolved-grantee path: build the full resolved identity bytes-to-bytes (a multi-tag grantee named precisely).
                var state = new GranteeIdentityState(this.access, body.Identity);
                newAdministrator = SecurityTagSet.Build(in state, BuildGranteeIdentity);
                if (body.Kind.IsNotUndefined())
                {
                    kind = ObservedIdentity.GranteeKind.From(body.Kind);
                    hasKind = true;
                }

                complete = !body.Complete.IsNotUndefined() || (bool)body.Complete;
            }
            else
            {
                // Interim single-grant path ({dimension, value}); the kind is inferred from the dimension.
                if (!body.DimensionValue.IsNotUndefined())
                {
                    throw new ArgumentException("Provide either a resolved grantee `identity` or a single `{ dimension, value }` grant.");
                }

                var state = new SingleGrantState(this.access, body.DimensionValue, body.Value);
                newAdministrator = SecurityTagSet.Build(in state, BuildSingleGrantIdentity);
                hasKind = TryGranteeKindForDimension(body.DimensionValue, out GranteeKind dimensionKind);
                complete = hasKind && this.access.IsWholeGrainGrantee(dimensionKind);
                if (hasKind)
                {
                    kind = dimensionKind.ToObservedKind();
                }
            }

            if (newAdministrator.IsEmpty)
            {
                throw new ArgumentException("The named grantee does not resolve to a deployment identity.");
            }
        }
        catch (ArgumentException ex)
        {
            return AddEnvironmentAdministratorResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        JsonString value = JsonString.From(body.Value);
        bool hasLabel = body.Label.IsNotUndefined();
        JsonString label = hasLabel ? JsonString.From(body.Label) : default;
        AdminKind adminKind = hasKind ? AdminKind.From(kind) : default;

        // Collision guard (§16.5.4): refuse if the resolved identity already belongs to a different recorded grantee.
        if (this.observed is not null && hasKind)
        {
            using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(kind, value, newAdministrator, cancellationToken).ConfigureAwait(false);
            if (conflict is not null)
            {
                return AddEnvironmentAdministratorResult.Conflict(CollisionProblem(), workspace);
            }
        }

        try
        {
            using ParsedJsonDocument<EnvironmentAdministrators> record = await this.administration.AddAdministratorAsync(name, newAdministrator, adminKind, hasKind, label, hasLabel, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);

            if (this.observed is not null && hasKind)
            {
                await this.observed.SeenAsync(kind, value, label, newAdministrator, complete, "environment-administrator", cancellationToken).ConfigureAwait(false);
            }

            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
            return AddEnvironmentAdministratorResult.Ok(
                Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
                workspace);
        }
        catch (EnvironmentAdministrationException)
        {
            return AddEnvironmentAdministratorResult.Forbidden(NotAdministratorProblem(name), workspace);
        }
        catch (EnvironmentAdministrationConflictException ex)
        {
            return AddEnvironmentAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TransferEnvironmentAdministrationResult> HandleTransferEnvironmentAdministrationAsync(TransferEnvironmentAdministrationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        List<SecurityTagSet> newAdministrators;
        try
        {
            newAdministrators = [];
            foreach (Models.AdministratorIdentity identity in parameters.Body.Administrators.EnumerateArray())
            {
                var state = new AdministratorGrantState(this.access, identity);
                SecurityTagSet resolved = SecurityTagSet.Build(in state, BuildAdministratorGrantIdentity);
                if (resolved.IsEmpty)
                {
                    throw new ArgumentException($"The administrator grant '{(string)identity.DimensionValue}={(string)identity.Value}' does not resolve to a deployment identity.");
                }

                newAdministrators.Add(resolved);
            }
        }
        catch (ArgumentException ex)
        {
            return TransferEnvironmentAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        // Collision guard (§16.5.4): refuse the transfer if any named administrator resolves to an identity already held by
        // a different grantee (a non-unique deployment mapping). Generic 409 — the conflicting party is never echoed.
        if (this.observed is not null)
        {
            int index = 0;
            foreach (Models.AdministratorIdentity identity in parameters.Body.Administrators.EnumerateArray())
            {
                SecurityTagSet resolved = newAdministrators[index++];
                if (!TryGranteeKindForDimension(identity.DimensionValue, out GranteeKind transferKind))
                {
                    continue;
                }

                using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(transferKind.ToObservedKind(), JsonString.From(identity.Value), resolved, cancellationToken).ConfigureAwait(false);
                if (conflict is not null)
                {
                    return TransferEnvironmentAdministrationResult.Conflict(CollisionProblem(), workspace);
                }
            }
        }

        try
        {
            using ParsedJsonDocument<EnvironmentAdministrators> record = await this.administration.TransferAdministrationAsync(name, newAdministrators, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
            return TransferEnvironmentAdministrationResult.Ok(
                Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
                workspace);
        }
        catch (ArgumentException ex)
        {
            return TransferEnvironmentAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator set", 400, ex.Message), workspace);
        }
        catch (EnvironmentAdministrationException)
        {
            return TransferEnvironmentAdministrationResult.Forbidden(NotAdministratorProblem(name), workspace);
        }
        catch (EnvironmentAdministrationConflictException ex)
        {
            return TransferEnvironmentAdministrationResult.Conflict(ConflictProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RemoveEnvironmentAdministratorResult> HandleRemoveEnvironmentAdministratorAsync(RemoveEnvironmentAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        string digest = (string)parameters.Digest;
        try
        {
            using ParsedJsonDocument<EnvironmentAdministrators> record = await this.administration.RemoveAdministratorAsync(name, digest, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            var listContext = new AdministratorListContext(record.RootElement.Administrators, this.access);
            return RemoveEnvironmentAdministratorResult.Ok(
                Models.AdministratorList.Build(in listContext, administrators: Models.AdministratorList.AdministratorGrantArray.Build(in listContext, BuildGrants)),
                workspace);
        }
        catch (ArgumentException ex)
        {
            return RemoveEnvironmentAdministratorResult.Conflict(Problem("last-administrator", "Cannot remove the last administrator", 409, ex.Message), workspace);
        }
        catch (EnvironmentAdministrationException)
        {
            return RemoveEnvironmentAdministratorResult.Forbidden(NotAdministratorProblem(name), workspace);
        }
        catch (EnvironmentAdministrationConflictException ex)
        {
            return RemoveEnvironmentAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
    }

    // ── governance gate (visibility + current-administrator membership) ─────────────────────────────────────────────
    private enum GovernanceGate
    {
        NotFound,
        Forbidden,
        Authorized,
    }

    // Resolves the §7.7 governance outcome for a mutating data-plane op: NotFound when the environment is outside the
    // caller's read reach (non-disclosing), Forbidden when visible but the caller is not a current administrator, else
    // Authorized. Read is checked before administration so a caller who cannot see the environment never learns it exists.
    private async ValueTask<GovernanceGate> AuthorizeGovernanceAsync(string name, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<Environment>? environment = await this.store.GetAsync(name, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environment is null)
        {
            return GovernanceGate.NotFound;
        }

        using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(name, cancellationToken).ConfigureAwait(false);
        return record?.RootElement.IsAdministeredBy(this.CallerIdentity()) == true
            ? GovernanceGate.Authorized
            : GovernanceGate.Forbidden;
    }

    // The caller's deployment identity (the internal tags the row-security policy stamps for the current principal). Empty
    // when unscoped, so the current-administrator membership check refuses every governance mutation.
    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    // ── environment-summary list projection (congruent whole-document From) ─────────────────────────────────────────
    private static void BuildEnvironments(in IReadOnlyList<Environment> environments, ref Models.EnvironmentList.EnvironmentSummaryArray.Builder array)
    {
        foreach (Environment environment in environments)
        {
            array.AddItem(Models.EnvironmentSummary.From(environment));
        }
    }

    // ── management-tag build (internal + operator tags → one SecurityTagSet) ────────────────────────────────────────
    // Writes the environment's management tags into a pooled SecurityTagSet: the deployment-internal tags first
    // (string-sourced from the policy; the short key encoded to a stack buffer), then the operator's user tags as
    // unescaped UTF-8 spans straight off the request body.
    private static void WriteManagementTags(ref IdentityBuilder builder, in ManagementTagsState state)
    {
        WriteInternalTags(ref builder, state.InternalTags);
        SecurityTagSet.Utf8Enumerator e = state.UserTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                builder.Add(e.CurrentKey, e.CurrentValue);
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void WriteInternalTags(ref IdentityBuilder builder, IReadOnlyList<SecurityTag> internalTags)
    {
        Span<byte> keyBuffer = stackalloc byte[256];
        foreach (SecurityTag tag in internalTags)
        {
            if (Encoding.UTF8.GetMaxByteCount(tag.Key.Length) <= keyBuffer.Length)
            {
                int written = Encoding.UTF8.GetBytes(tag.Key, keyBuffer);
                builder.Add(keyBuffer[..written], tag.Value);
            }
            else
            {
                builder.Add(Encoding.UTF8.GetBytes(tag.Key), tag.Value);
            }
        }
    }

    private readonly struct ManagementTagsState(IReadOnlyList<SecurityTag> internalTags, SecurityTagSet userTags)
    {
        public IReadOnlyList<SecurityTag> InternalTags { get; } = internalTags;

        public SecurityTagSet UserTags { get; } = userTags;
    }

    // ── administrator grantee resolution (ported from the §15 administrators handler) ───────────────────────────────
    private static bool TryGranteeKindForDimension(in Models.JsonString dimension, out GranteeKind kind)
    {
        if (dimension.ValueEquals("sub"u8))
        {
            kind = GranteeKind.Person;
            return true;
        }

        if (dimension.ValueEquals("tenant"u8))
        {
            kind = GranteeKind.Team;
            return true;
        }

        if (dimension.ValueEquals("role"u8))
        {
            kind = GranteeKind.Role;
            return true;
        }

        if (dimension.ValueEquals("workflow"u8))
        {
            kind = GranteeKind.Workflow;
            return true;
        }

        kind = default;
        return false;
    }

    private static void BuildGranteeIdentity(ref IdentityBuilder builder, in GranteeIdentityState state)
    {
        foreach (Models.AdministratorIdentity grant in state.Identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
            using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
            state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
        }
    }

    private static void BuildSingleGrantIdentity(ref IdentityBuilder builder, in SingleGrantState state)
    {
        using UnescapedUtf8JsonString dimension = state.Dimension.GetUtf8String();
        using UnescapedUtf8JsonString value = state.Value.GetUtf8String();
        state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
    }

    private static void BuildAdministratorGrantIdentity(ref IdentityBuilder builder, in AdministratorGrantState state)
    {
        using UnescapedUtf8JsonString dimension = state.Identity.DimensionValue.GetUtf8String();
        using UnescapedUtf8JsonString value = state.Identity.Value.GetUtf8String();
        state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
    }

    private readonly ref struct GranteeIdentityState(ControlPlaneAccess access, Models.AdministratorMemberWrite.AdministratorIdentityArray identity)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.AdministratorMemberWrite.AdministratorIdentityArray Identity { get; } = identity;
    }

    private readonly ref struct SingleGrantState(ControlPlaneAccess access, Models.JsonString dimension, Models.JsonString value)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.JsonString Dimension { get; } = dimension;

        public Models.JsonString Value { get; } = value;
    }

    private readonly ref struct AdministratorGrantState(ControlPlaneAccess access, Models.AdministratorIdentity identity)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.AdministratorIdentity Identity { get; } = identity;
    }

    // ── administrator-grant projection (digest + described {dimension,value} grants; ported from §15) ───────────────
    private static void BuildGrants(in AdministratorListContext ctx, ref Models.AdministratorList.AdministratorGrantArray.Builder array)
    {
        if (ctx.Administrators.IsUndefined())
        {
            return;
        }

        ControlPlaneAccess access = ctx.Access;
        foreach (EnvironmentAdministrators.AdministratorIdentity administrator in ctx.Administrators.EnumerateArray())
        {
            var state = new GrantState(administrator, TagsOf(administrator), access);
            array.AddItem(Models.AdministratorGrant.Build(in state, BuildGrant));
        }
    }

    private static void BuildGrant(in GrantState state, ref Models.AdministratorGrant.Builder grant)
    {
        EnvironmentAdministrators.AdministratorIdentity administrator = state.Administrator;
        bool hasKind = administrator.Kind.IsNotUndefined();
        bool hasLabel = administrator.Label.IsNotUndefined();

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

    private static SecurityTagSet TagsOf(in EnvironmentAdministrators.AdministratorIdentity administrator)
        => SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(administrator.Tags).Memory);

    private readonly ref struct AdministratorListContext(EnvironmentAdministrators.AdministratorIdentityArray administrators, ControlPlaneAccess access)
    {
        public EnvironmentAdministrators.AdministratorIdentityArray Administrators { get; } = administrators;

        public ControlPlaneAccess Access { get; } = access;
    }

    private readonly ref struct GrantState(EnvironmentAdministrators.AdministratorIdentity administrator, SecurityTagSet tags, ControlPlaneAccess access)
    {
        public EnvironmentAdministrators.AdministratorIdentity Administrator { get; } = administrator;

        public SecurityTagSet Tags { get; } = tags;

        public ControlPlaneAccess Access { get; } = access;
    }

    // ── problem documents ──────────────────────────────────────────────────────────────────────────────────────────
    private static Models.ProblemDetails.Source NotFoundProblem(string name)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{name}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string name)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{name}'.");

    private static Models.ProblemDetails.Source ConflictProblem(EnvironmentAdministrationConflictException ex)
        => Problem("administration-conflict", "Administration changed concurrently", 409, ex.Message);

    private static Models.ProblemDetails.Source CollisionProblem()
        => Problem(
            "identity-collision",
            "Ambiguous grantee identity",
            409,
            "The named grantee resolves to an identity that already belongs to a different grantee, so the grant would be ambiguous. Check the deployment's directory identity mapping — it must resolve each distinct principal to a unique identity.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}