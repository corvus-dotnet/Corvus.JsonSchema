// <copyright file="ArazzoControlPlaneAdministratorsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAdministratorsHandler"/> over an <see cref="ISecuredWorkflowCatalog"/> — the
/// control-plane surface that manages a base workflow id's administrator set (design §15). The endpoints are gated by
/// the <c>administrators:read</c>/<c>administrators:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Identity model (§15).</strong> An administrator is a deployment-stamped <c>sys:</c> identity (the same
/// unforgeable internal tags a catalogued version is stamped with), and the set is governed by current-administrator
/// membership — never reach. The operator-facing API never exposes raw internal tags: each administrator is named by
/// the grant <c>{dimension, value}</c> the deployment maps it to (the same mapping the §13 usage grants use, via
/// <see cref="ControlPlaneRowSecurityPolicy.ResolveUsageGrants"/> / <see cref="ControlPlaneRowSecurityPolicy.DescribeUsageScope"/>).
/// The caller's own identity is read from the deployment's row-security policy, so administration is meaningful only
/// when a policy is configured.</para>
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
    private readonly IObservedIdentityStore? observed;

    /// <summary>Initializes a new, unscoped instance (the caller resolves to no identity — administration management
    /// requires a configured row-security policy).</summary>
    /// <param name="catalog">The catalog client that owns the administrator store and the administration operations.</param>
    public ArazzoControlPlaneAdministratorsHandler(ISecuredWorkflowCatalog catalog)
        : this(catalog, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAdministratorsHandler"/> class.</summary>
    /// <param name="catalog">The catalog client that owns the administrator store and the administration operations.</param>
    /// <param name="access">Resolves the caller's deployment identity per request and maps administrator grants to and
    /// from internal tags. Unscoped (no identity) when no row security is configured.</param>
    /// <param name="observed">An optional observed-identity store; a newly added administrator is recorded as a resolvable
    /// grantee for the §16.5.4 typeahead (best-effort).</param>
    internal ArazzoControlPlaneAdministratorsHandler(ISecuredWorkflowCatalog catalog, ControlPlaneAccess access, IObservedIdentityStore? observed = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        this.catalog = catalog;
        this.access = access;
        this.observed = observed;
    }

    /// <inheritdoc/>
    public async ValueTask<ListAdministratorsResult> HandleListAdministratorsAsync(ListAdministratorsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SecurityTagSet> administrators = await this.catalog.GetAdministratorsAsync((string)parameters.BaseWorkflowId, cancellationToken).ConfigureAwait(false);
        return ListAdministratorsResult.Ok(this.ToList(administrators), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AddAdministratorResult> HandleAddAdministratorAsync(AddAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.AdministratorMemberWrite body = parameters.Body;

        SecurityTagSet newAdministrator;

        // The grantee kind the observed store seam carries — its own JSON value (ObservedIdentity.GranteeKind). The
        // resolved path converts the request kind straight across with From(); the interim single-grant path infers it
        // from the dimension (a domain enum, for the policy's whole-grain test) and maps that to the store kind.
        ObservedIdentity.GranteeKind kind = default;
        bool hasKind = false;
        bool complete;
        try
        {
            if (body.Identity.IsNotUndefined() && body.Identity.GetArrayLength() > 0)
            {
                // Resolved-grantee path (the grantee picker): build the full resolved identity BYTES-TO-BYTES, so a
                // multi-tag grantee (e.g. a person resolved to {sys:tenant, sys:sub}) is named precisely — each grant's
                // dimension/value is read as UTF-8 and the resolved tag is written straight into the pooled buffer (no
                // managed string per dimension/value, no intermediate grant list).
                var state = new GranteeIdentityState(this.access, body.Identity);
                newAdministrator = SecurityTagSet.Build(in state, BuildGranteeIdentity);

                // The request's grantee kind IS a JSON value; convert it to the store's kind with a straight From()
                // (free rewrap, no reify, no token re-parse) — the store carries it through bytes-to-bytes.
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

                // Map the dimension to its grantee kind by comparing the JSON value DIRECTLY (ValueEquals — no
                // GetUtf8String unescape lease). The policy's whole-grain verdict consumes the domain enum; the store kind
                // is the equivalent JSON value (a pre-built constant — no reify).
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
            return AddAdministratorResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        // baseWorkflowId is the catalog/admin-store key (string-keyed stores) — its genuine leaf is a string, read once.
        string baseWorkflowId = (string)parameters.BaseWorkflowId;

        // The grantee value/label are the JSON values from the request body; they flow to the observed store as those
        // JSON values (From() rewraps, no reify, no managed string) and are written bytes-to-bytes at the store's leaf.
        // The request document outlives this call, so no owned copy is needed.
        JsonString value = JsonString.From(body.Value);
        JsonString label = body.Label.IsNotUndefined() ? JsonString.From(body.Label) : default;

        // Collision guard (§16.5.4): if the resolved identity already belongs to a DIFFERENT recorded grantee, the
        // deployment's identity mapping is not minting unique identities — refuse the ambiguous grant (409) rather than
        // author a grant that would silently also admit that other principal. The message is generic: the conflicting
        // party is never echoed (it may be outside the caller's reach), so the probe — which runs at full reach — does
        // not become a cross-tenant disclosure oracle. The conflicting record (a pooled document) is disposed here.
        if (this.observed is not null && hasKind)
        {
            using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(kind, value, newAdministrator, cancellationToken).ConfigureAwait(false);
            if (conflict is not null)
            {
                return AddAdministratorResult.Conflict(CollisionProblem(), workspace);
            }
        }

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.AddAdministratorAsync(baseWorkflowId, newAdministrator, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);

            // Record the newly named administrator as a resolvable grantee for the §16.5.4 typeahead. Best-effort: the
            // sighting is an idempotent projection and never fails the add. `complete` is honest (§17.2): the picker's
            // resolved completeness for a grantee-path add, or the policy's whole-grain verdict for the interim grant.
            if (this.observed is not null && hasKind)
            {
                await this.observed.SeenAsync(kind, value, label, newAdministrator, complete, "administrator", cancellationToken).ConfigureAwait(false);
            }

            return AddAdministratorResult.Ok(this.ToList(administrators), workspace);
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

    // Maps an interim {dimension, value} grant's dimension to its grantee kind by comparing the JSON value DIRECTLY
    // (ValueEquals — no GetUtf8String unescape lease, no managed string), the inverse of the policy's kind→dimension map.
    // A custom dimension names no well-known kind (false), so it records no sighting / skips the collision probe.
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

    // Builds the grantee identity from the resolved {dimension, value} grants, reading each as UTF-8 and writing its
    // resolved internal tag straight into the pooled buffer (the multi-tag, bytes-to-bytes picker path).
    private static void BuildGranteeIdentity(ref IdentityBuilder builder, in GranteeIdentityState state)
    {
        foreach (Models.AdministratorIdentity grant in state.Identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
            using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
            state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
        }
    }

    // Builds the interim single-grant identity ({dimension, value}) bytes-to-bytes.
    private static void BuildSingleGrantIdentity(ref IdentityBuilder builder, in SingleGrantState state)
    {
        using UnescapedUtf8JsonString dimension = state.Dimension.GetUtf8String();
        using UnescapedUtf8JsonString value = state.Value.GetUtf8String();
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

    // Builds one transfer administrator's identity from its {dimension, value} grant element, reading each as UTF-8 and
    // writing the resolved internal tag straight into the pooled buffer (the bytes-to-bytes set-replacement counterpart
    // of the picker's BuildGranteeIdentity, one grant per administrator).
    private static void BuildAdministratorGrantIdentity(ref IdentityBuilder builder, in AdministratorGrantState state)
    {
        using UnescapedUtf8JsonString dimension = state.Identity.DimensionValue.GetUtf8String();
        using UnescapedUtf8JsonString value = state.Identity.Value.GetUtf8String();
        state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
    }

    private readonly ref struct AdministratorGrantState(ControlPlaneAccess access, Models.AdministratorIdentity identity)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.AdministratorIdentity Identity { get; } = identity;
    }

    /// <inheritdoc/>
    public async ValueTask<TransferAdministrationResult> HandleTransferAdministrationAsync(TransferAdministrationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        List<SecurityTagSet> newAdministrators;
        try
        {
            // Build each administrator's identity BYTES-TO-BYTES: read its {dimension, value} grant as UTF-8 from the body
            // and write the resolved internal tag straight into a pooled buffer (the same span seam the add/picker path
            // uses), so a transfer set is resolved without a managed string per dimension/value.
            newAdministrators = [];
            foreach (Models.AdministratorIdentity identity in parameters.Body.Administrators.EnumerateArray())
            {
                var state = new AdministratorGrantState(this.access, identity);
                SecurityTagSet resolved = SecurityTagSet.Build(in state, BuildAdministratorGrantIdentity);
                if (resolved.IsEmpty)
                {
                    // A grant the deployment declines to map names no identity — rejected. Only this error path forms the
                    // {dimension, value} strings (for the 400 message); the success path stays string-free.
                    throw new ArgumentException($"The administrator grant '{(string)identity.DimensionValue}={(string)identity.Value}' does not resolve to a deployment identity.");
                }

                newAdministrators.Add(resolved);
            }
        }
        catch (ArgumentException ex)
        {
            return TransferAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        // Collision guard (§16.5.4): refuse the transfer if any named administrator resolves to an identity already held
        // by a different grantee (a non-unique deployment mapping). Generic 409 — the conflicting party is never echoed.
        // The grant's value flows to the (UTF-8) conflict probe as owned, pooled memory; no managed string is formed.
        if (this.observed is not null)
        {
            int index = 0;
            foreach (Models.AdministratorIdentity identity in parameters.Body.Administrators.EnumerateArray())
            {
                SecurityTagSet resolved = newAdministrators[index++];

                // Map the dimension to its grantee kind by comparing the JSON value DIRECTLY (ValueEquals — no
                // GetUtf8String unescape lease); a custom dimension names no well-known kind, so it has no collision probe.
                if (!TryGranteeKindForDimension(identity.DimensionValue, out GranteeKind transferKind))
                {
                    continue;
                }

                // The grantee value flows to the probe as its JSON value (From() rewraps, no reify); the dimension-inferred
                // domain kind maps to the store's JSON kind (a pre-built constant). The conflicting record (a pooled
                // document) is disposed at the end of this iteration.
                using ParsedJsonDocument<ObservedIdentity>? conflict = await this.observed.FindIdentityConflictAsync(transferKind.ToObservedKind(), JsonString.From(identity.Value), resolved, cancellationToken).ConfigureAwait(false);
                if (conflict is not null)
                {
                    return TransferAdministrationResult.Conflict(CollisionProblem(), workspace);
                }
            }
        }

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.TransferAdministrationAsync(baseWorkflowId, newAdministrators, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            return TransferAdministrationResult.Ok(this.ToList(administrators), workspace);
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

        // A grant that does not map to a deployment identity (or the unscoped default) resolves to the empty set, which
        // is never a member — so removal becomes an idempotent no-op. We still route through the catalog so the
        // current-administrator gate (403) runs first and membership stays non-disclosing. The {dimension, value} path
        // parameters are read as UTF-8 and resolved BYTES-TO-BYTES through the span seam — no managed string is formed.
        var state = new SingleGrantState(this.access, parameters.Dimension, parameters.Value);
        SecurityTagSet administrator = SecurityTagSet.Build(in state, BuildSingleGrantIdentity);

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.RemoveAdministratorAsync(baseWorkflowId, administrator, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            return RemoveAdministratorResult.Ok(this.ToList(administrators), workspace);
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

    private static Models.AdministratorIdentity.Source ToIdentity(CredentialUsageGrant grant)
        => new((ref Models.AdministratorIdentity.Builder b) => b.Create(grant.Dimension, grant.Value));

    private static Models.ProblemDetails.Source NotAdministratorProblem(string baseWorkflowId)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of '{baseWorkflowId}', or it has no established administration.");

    private static Models.ProblemDetails.Source ConflictProblem(WorkflowAdministrationConflictException ex)
        => Problem("administration-conflict", "Administration changed concurrently", 409, ex.Message);

    private static Models.ProblemDetails.Source UnavailableProblem(NotSupportedException ex)
        => Problem("administration-unavailable", "Administration management unavailable", 409, ex.Message);

    // A resolved grantee identity that already belongs to a different grantee (§16.5.4) — the deployment's identity
    // mapping is not unique. Generic by design: the conflicting party is never named.
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

    // Describes each administrator (a set of internal identity tags) back as the operator-facing grants it maps from —
    // the inverse of the grant resolution. Internal tags are never exposed raw; unscoped deployments describe nothing.
    private Models.AdministratorList.Source ToList(IReadOnlyList<SecurityTagSet> administrators)
        => new((ref Models.AdministratorList.Builder b) => b.Create(
            administrators: new Models.AdministratorList.AdministratorIdentityArray.Source((ref Models.AdministratorList.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (SecurityTagSet administrator in administrators)
                {
                    foreach (CredentialUsageGrant grant in this.access.DescribeUsageScope(administrator))
                    {
                        ab.AddItem(ToIdentity(grant));
                    }
                }
            })));
}