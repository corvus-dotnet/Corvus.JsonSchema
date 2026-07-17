// <copyright file="ArazzoControlPlaneSecurityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiSecurityHandler"/> over an <see cref="ISecurityPolicyStore"/> — the
/// control-plane surface that authors the row-security policy (rules + claim→rule bindings, design §14.2). The
/// endpoints are gated by the <c>security:read</c>/<c>security:write</c> capability scopes. Rule expressions are
/// validated against the grammar on write (a malformed expression is a 400). After a successful mutation the
/// optional <see cref="PersistentRowSecurityPolicy"/> is refreshed so the change takes effect for subsequent
/// authorization decisions in this process.
/// </summary>
public sealed class ArazzoControlPlaneSecurityHandler : IApiSecurityHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // The page size the access-grants aggregation reads the credential store with. The credential store treats a
    // non-positive limit as 1 (unlike the security-policy store's "use the default" sentinel), so a positive size is
    // passed to avoid a page-per-row scan.
    private const int CredentialScanPageSize = 50;

    // The inclusive upper bound the /count endpoints report; a busier list renders "100+".
    private const int CountCap = 100;

    private readonly ISecurityPolicyStore store;
    private readonly PersistentRowSecurityPolicy? policy;
    private readonly ControlPlaneAccess? access;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ISourceCredentialStore? credentials;
    private readonly SecuredEnvironmentAdministration? environments;
    private readonly IEnvironmentStore? environmentStore;
    private readonly IAvailabilityStore? availabilityStore;
    private readonly TimeProvider timeProvider;
    private readonly string actor;
    private readonly ILogger? auditLogger;

    // The audited resource kinds for this surface (design §850).
    private const string RuleKind = "security-rule";
    private const string BindingKind = "security-binding";

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneSecurityHandler"/> class.</summary>
    /// <param name="store">The persistent rule/binding store the endpoints delegate to.</param>
    /// <param name="policy">An optional policy to refresh after a mutation so changes take effect in-process.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneSecurityHandler(ISecurityPolicyStore store, PersistentRowSecurityPolicy? policy = null, string actor = "control-plane")
        : this(store, policy, null, actor: actor)
    {
    }

    /// <summary>Initializes a new instance bound to the current request's caller, so binding create/update can apply the
    /// self-elevation guard (§16.5.3): a caller may not author a binding that grants <em>itself</em> write/purge reach.</summary>
    /// <param name="store">The persistent rule/binding store.</param>
    /// <param name="policy">An optional policy to refresh after a mutation.</param>
    /// <param name="access">The request-scoped access binding the guard reads the caller's claims from (<see langword="null"/> disables the guard — the unscoped posture).</param>
    /// <param name="actor">The audit actor recorded on writes.</param>
    internal ArazzoControlPlaneSecurityHandler(ISecurityPolicyStore store, PersistentRowSecurityPolicy? policy, ControlPlaneAccess? access, ISecuredWorkflowCatalog? catalog = null, ISourceCredentialStore? credentials = null, SecuredEnvironmentAdministration? environments = null, TimeProvider? timeProvider = null, string actor = "control-plane", ILogger? auditLogger = null, IEnvironmentStore? environmentStore = null, IAvailabilityStore? availabilityStore = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.policy = policy;
        this.access = access;
        this.catalog = catalog;
        this.credentials = credentials;
        this.environments = environments;
        this.environmentStore = environmentStore;
        this.availabilityStore = availabilityStore;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.actor = actor;
        this.auditLogger = auditLogger;
    }

    // The §850 audit subject: the authenticated principal who authored the change, falling back to the
    // deployment-configured audit actor when no principal is resolvable (the guard-disabled unscoped posture).
    private string AuditActor() => PrincipalDisplayName.Resolve(this.access?.CurrentPrincipal) ?? this.actor;

    /// <inheritdoc/>
    public async ValueTask<SearchSecurityRulesResult> HandleSearchSecurityRulesAsync(SearchSecurityRulesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // An absent limit passes 0 — the contract's "use the store's default page size" sentinel (the store owns that
        // size; the handler does not duplicate it). The page token and the q filter flow to the store as their JSON
        // values (From() rewraps each — free, no reify, no managed string; an undefined value rewraps to undefined); the
        // store decodes/matches bytes-native and returns one keyset page (bounded — never all rules).
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString q = JsonString.From(parameters.Q);
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using SecurityRulePage page = await this.store.ListRulesAsync(limit, pageToken, q, cancellationToken).ConfigureAwait(false);

        // Each summary is a whole-document SecurityRuleSummary.From wrap (the congruent projection the single-document
        // sites use), so it references its pooled document; the body is validated/serialized after this handler returns,
        // so hand the documents to the workspace (it disposes them at request end; `using page` then only returns the
        // batch's backing array + the token buffer). The list body is built closure-free and consumed in place; the
        // continuation token is copied verbatim from the page's UTF-8 (the Ok materialisation copies it before dispose).
        page.Rules.TransferOwnershipTo(workspace);
        IReadOnlyList<SecurityRuleDocument> ruleList = page.Rules;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.SecurityRuleList.Source<IReadOnlyList<SecurityRuleDocument>> body = Models.SecurityRuleList.Build(
            in ruleList,
            rules: Models.SecurityRuleList.SecurityRuleSummaryArray.Build(in ruleList, BuildRuleSummaries),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return SearchSecurityRulesResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountSecurityRulesResult> HandleCountSecurityRulesAsync(CountSecurityRulesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Same q filter as HandleSearchSecurityRulesAsync, minus paging — the store returns only a bounded total (§14.2
        // footer), never rows. Access is gated by the security:read capability scope, not row reach, so no AccessContext.
        JsonString q = JsonString.From(parameters.Q);
        (int count, bool capped) = await this.store.CountRulesAsync(CountCap, q, cancellationToken).ConfigureAwait(false);
        return CountSecurityRulesResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountSecurityBindingsResult> HandleCountSecurityBindingsAsync(CountSecurityBindingsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Same q filter as HandleSearchSecurityBindingsAsync, minus paging; capability-scoped, no row reach.
        JsonString q = JsonString.From(parameters.Q);
        (int count, bool capped) = await this.store.CountBindingsAsync(CountCap, q, cancellationToken).ConfigureAwait(false);
        return CountSecurityBindingsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
    }

    /// <inheritdoc/>
    public ValueTask<ListSecurityOrderingsResult> HandleListSecurityOrderingsAsync(ListSecurityOrderingsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The orderings are deployment configuration the policy already holds (the single source the authoring UI offers
        // ordered templates from); there is no store read, so the projection is synchronous. Build it closure-free with
        // the orderings threaded as the context, materialised once into the request workspace — the only allocation is the
        // genuine response document (the config's label strings encoded to UTF-8 once).
        SecurityLabelOrderings cfg = this.policy?.Orderings ?? SecurityLabelOrderings.Empty;
        Models.SecurityOrderingList.Source<SecurityLabelOrderings> body = Models.SecurityOrderingList.Build(
            in cfg,
            orderings: Models.SecurityOrderingList.SecurityOrderingArray.Build(in cfg, BuildOrderings));
        return ValueTask.FromResult(ListSecurityOrderingsResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    public async ValueTask<CreateSecurityRuleResult> HandleCreateSecurityRuleAsync(CreateSecurityRuleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.SecurityRuleCreate body = parameters.Body;
        string name = (string)body.Name;
        string expression = (string)body.Expression;
        if (IsInvalidRule(expression, out Models.ProblemDetails.Source problem))
        {
            return CreateSecurityRuleResult.BadRequest(problem, workspace);
        }

        try
        {
            ParsedJsonDocument<SecurityRuleDocument> created = await this.store.AddRuleAsync(name, SecurityRuleDocument.From(body), this.actor, cancellationToken).ConfigureAwait(false);

            // The summary is a zero-copy view over the rule document (a congruent projection — identical property
            // names/types/required set), so hand the pooled document to the workspace (it disposes it after the
            // response is written) and wrap with From() (a pointer reinterpret). Ownership transfers before
            // RefreshAsync so a refresh failure cannot leak the document.
            GovernanceAudit.Mutation(this.auditLogger, "security-rule.create", this.AuditActor(), RuleKind, name, "created");
            workspace.TakeOwnership(created);
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return CreateSecurityRuleResult.Created(Models.SecurityRuleSummary.From(created.RootElement), workspace);
        }
        catch (InvalidOperationException)
        {
            return CreateSecurityRuleResult.Conflict(
                Problem("rule-exists", "Rule already exists", 409, $"A security rule named '{name}' already exists."), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetSecurityRuleResult> HandleGetSecurityRuleAsync(GetSecurityRuleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.RuleName;
        ParsedJsonDocument<SecurityRuleDocument>? rule = await this.store.GetRuleAsync(name, cancellationToken).ConfigureAwait(false);
        if (rule is not { } r)
        {
            return GetSecurityRuleResult.NotFound(NotFoundProblem("rule", name), workspace);
        }

        // Zero-copy view over the pooled rule document handed to the workspace (it disposes it after the response is
        // written); From() reinterprets the stored element (congruent projection) and serializes its backing verbatim.
        workspace.TakeOwnership(r);
        return GetSecurityRuleResult.Ok(Models.SecurityRuleSummary.From(r.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateSecurityRuleResult> HandleUpdateSecurityRuleAsync(UpdateSecurityRuleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.RuleName;
        Models.SecurityRuleUpdate body = parameters.Body;
        string expression = (string)body.Expression;
        if (IsInvalidRule(expression, out Models.ProblemDetails.Source problem))
        {
            return UpdateSecurityRuleResult.BadRequest(problem, workspace);
        }

        ParsedJsonDocument<SecurityRuleDocument>? updated = await this.store.UpdateRuleAsync(name, SecurityRuleDocument.From(body), WorkflowEtag.None, this.actor, cancellationToken).ConfigureAwait(false);
        if (updated is not { } r)
        {
            return UpdateSecurityRuleResult.NotFound(NotFoundProblem("rule", name), workspace);
        }

        // Zero-copy view over the pooled rule document handed to the workspace; ownership transfers before RefreshAsync
        // so a refresh failure cannot leak the document.
        GovernanceAudit.Mutation(this.auditLogger, "security-rule.update", this.AuditActor(), RuleKind, name, "updated");
        workspace.TakeOwnership(r);
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSecurityRuleResult.Ok(Models.SecurityRuleSummary.From(r.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteSecurityRuleResult> HandleDeleteSecurityRuleAsync(DeleteSecurityRuleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.RuleName;
        bool deleted = await this.store.DeleteRuleAsync(name, WorkflowEtag.None, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return DeleteSecurityRuleResult.NotFound(NotFoundProblem("rule", name), workspace);
        }

        GovernanceAudit.Mutation(this.auditLogger, "security-rule.delete", this.AuditActor(), RuleKind, name, "deleted");
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return DeleteSecurityRuleResult.NoContent();
    }

    /// <inheritdoc/>
    public async ValueTask<SearchSecurityBindingsResult> HandleSearchSecurityBindingsAsync(SearchSecurityBindingsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // An absent limit passes 0 — the contract's "use the store's default page size" sentinel (the store owns that
        // size; the handler does not duplicate it). The page token and the q filter flow to the store as their JSON values
        // (From() rewraps each — free, no reify, no managed string; an undefined value rewraps to undefined); the store
        // decodes/matches bytes-native and returns one keyset page (bounded — never all bindings).
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString q = JsonString.From(parameters.Q);
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using SecurityBindingPage page = await this.store.ListBindingsAsync(limit, pageToken, q, cancellationToken).ConfigureAwait(false);

        // Each summary references its pooled binding document (the per-field From() projection is a zero-copy element
        // wrap), and the body is validated/serialized after this handler returns — so hand the documents to the
        // workspace (it disposes them at request end); `using page` then only returns the batch's backing array + the
        // token buffer. The list body is built closure-free and consumed in place (the array projected through the static
        // BuildBindingSummaries, the binding list threaded as the context); the continuation token is copied verbatim
        // from the page's UTF-8 (the Ok materialisation copies the scalar before dispose).
        page.Bindings.TransferOwnershipTo(workspace);
        IReadOnlyList<SecurityBindingDocument> bindingList = page.Bindings;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.SecurityBindingList.Source<IReadOnlyList<SecurityBindingDocument>> body = Models.SecurityBindingList.Build(
            in bindingList,
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build(in bindingList, BuildBindingSummaries),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return SearchSecurityBindingsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateSecurityBindingResult> HandleCreateSecurityBindingAsync(CreateSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!ReadBinding(parameters.Body, out SecurityBindingDocument draft, out Models.ProblemDetails.Source problem))
        {
            return CreateSecurityBindingResult.BadRequest(problem, workspace);
        }

        if (this.SelfElevates(draft, out problem))
        {
            // The self-elevation guard firing — a caller authoring a grant that would elevate itself — is a security
            // control worth auditing (no id is assigned to a refused create).
            GovernanceAudit.Mutation(this.auditLogger, "security-binding.create", this.AuditActor(), BindingKind, string.Empty, "refused-self-elevation");
            return CreateSecurityBindingResult.Forbidden(problem, workspace);
        }

        // The draft is a free, zero-copy element view over the request body (the store reads it synchronously) — no pooled
        // document to dispose. The summary references the returned pooled binding document (per-field From() wrap), so hand
        // that to the workspace; ownership transfers before RefreshAsync so a refresh failure cannot leak it.
        ParsedJsonDocument<SecurityBindingDocument> created = await this.store.AddBindingAsync(draft, this.actor, cancellationToken).ConfigureAwait(false);
        GovernanceAudit.Mutation(this.auditLogger, "security-binding.create", this.AuditActor(), BindingKind, (string)created.RootElement.Id, "created");
        workspace.TakeOwnership(created);
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return CreateSecurityBindingResult.Created(ToBindingSource(created.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetSecurityBindingResult> HandleGetSecurityBindingAsync(GetSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.BindingId;
        ParsedJsonDocument<SecurityBindingDocument>? binding = await this.store.GetBindingAsync(id, cancellationToken).ConfigureAwait(false);
        if (binding is not { } b)
        {
            return GetSecurityBindingResult.NotFound(NotFoundProblem("binding", id), workspace);
        }

        // The summary references the pooled binding document (per-field From() zero-copy wrap) — hand it to the
        // workspace so the deferred body validation/serialization is safe (it disposes the document afterwards).
        workspace.TakeOwnership(b);
        return GetSecurityBindingResult.Ok(ToBindingSource(b.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateSecurityBindingResult> HandleUpdateSecurityBindingAsync(UpdateSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.BindingId;
        if (!ReadBinding(parameters.Body, out SecurityBindingDocument draft, out Models.ProblemDetails.Source problem))
        {
            return UpdateSecurityBindingResult.BadRequest(problem, workspace);
        }

        if (this.SelfElevates(draft, out problem))
        {
            GovernanceAudit.Mutation(this.auditLogger, "security-binding.update", this.AuditActor(), BindingKind, id, "refused-self-elevation");
            return UpdateSecurityBindingResult.Forbidden(problem, workspace);
        }

        // The draft is a free, zero-copy element view over the request body (the store reads it synchronously) — no pooled
        // document to dispose.
        ParsedJsonDocument<SecurityBindingDocument>? updated = await this.store.UpdateBindingAsync(id, draft, WorkflowEtag.None, this.actor, cancellationToken).ConfigureAwait(false);
        if (updated is not { } b)
        {
            return UpdateSecurityBindingResult.NotFound(NotFoundProblem("binding", id), workspace);
        }

        // The summary references the returned pooled binding document (per-field From() zero-copy wrap) — hand it to the
        // workspace; ownership transfers before RefreshAsync so a refresh failure cannot leak the document.
        GovernanceAudit.Mutation(this.auditLogger, "security-binding.update", this.AuditActor(), BindingKind, id, "updated");
        workspace.TakeOwnership(b);
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSecurityBindingResult.Ok(ToBindingSource(b.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteSecurityBindingResult> HandleDeleteSecurityBindingAsync(DeleteSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.BindingId;
        bool deleted = await this.store.DeleteBindingAsync(id, WorkflowEtag.None, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return DeleteSecurityBindingResult.NotFound(NotFoundProblem("binding", id), workspace);
        }

        GovernanceAudit.Mutation(this.auditLogger, "security-binding.delete", this.AuditActor(), BindingKind, id, "deleted");
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return DeleteSecurityBindingResult.NoContent();
    }

    /// <inheritdoc/>
    public async ValueTask<GetAccessGrantsResult> HandleGetAccessGrantsAsync(GetAccessGrantsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The grantee is an opaque URL-safe token that round-trips a resolved grantee's full JSON (design §6.1). Decode
        // it into a pooled buffer, then parse it into an OWNED document that TAKES OWNERSHIP of that buffer — the parsed
        // document retains (does not copy) the backing memory, so the buffer must live for the document's lifetime; the
        // document returns it to the pool on dispose. Any empty/malformed token, or a non-object payload, is a 400.
        if (!parameters.Grantee.IsNotUndefined())
        {
            return InvalidGrantee(workspace);
        }

        string token = (string)parameters.Grantee;
        if (token.Length == 0)
        {
            return InvalidGrantee(workspace);
        }

        byte[] decodeBuffer = ArrayPool<byte>.Shared.Rent(Base64Url.GetMaxDecodedLength(token.Length));
        if (Base64Url.DecodeFromChars(token, decodeBuffer, out _, out int decodedLength) != OperationStatus.Done)
        {
            ArrayPool<byte>.Shared.Return(decodeBuffer);
            return InvalidGrantee(workspace);
        }

        ParsedJsonDocument<Models.ResolvedGrantee> granteeDoc;
        try
        {
            granteeDoc = ParsedJsonDocument<Models.ResolvedGrantee>.Parse(decodeBuffer.AsMemory(0, decodedLength), decodeBuffer);
        }
        catch (System.Text.Json.JsonException)
        {
            // Parse attaches the rented buffer to the document only on success, so return it ourselves on failure.
            ArrayPool<byte>.Shared.Return(decodeBuffer);
            return InvalidGrantee(workspace);
        }

        if (granteeDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            granteeDoc.Dispose(); // returns the rented decode buffer it now owns
            return InvalidGrantee(workspace);
        }

        // The grantee is echoed verbatim in the response (a congruent whole-document From wrap), which is validated and
        // serialized after this handler returns — so hand the document to the workspace (it disposes it, and returns the
        // rented decode buffer, at request end).
        workspace.TakeOwnership(granteeDoc);
        Models.ResolvedGrantee grantee = granteeDoc.RootElement;

        // The grantee's identity arrives in the operator-facing (sys:-stripped) wire form; resolve it back to the internal
        // sys: tag set (via ControlPlaneAccess) so it keys the administered-workflows reverse index and the credential
        // IsUsableBy match correctly (the digest is over the sys: tags).
        SecurityTagSet granteeIdentity = SecurityTagSet.Empty;
        if (this.access is { } access && grantee.Identity.IsNotUndefined())
        {
            var identityState = new GranteeIdentityState(access, grantee.Identity);
            granteeIdentity = SecurityTagSet.Build(in identityState, WriteGranteeIdentity);
        }

        // bindings: page the store keeping only the bindings whose claim the grantee satisfies. A contributing page's
        // documents are handed to the workspace so the matched summaries survive the deferred body serialization; the
        // page itself is disposed each iteration, so the next-page token is copied into a workspace-owned JsonString
        // (which survives that dispose — a JsonString over the page's own span would dangle) before the loop repeats.
        var matchedBindings = new List<SecurityBindingDocument>();
        JsonString bindingPageToken = default;
        while (true)
        {
            using SecurityBindingPage page = await this.store.ListBindingsAsync(0, bindingPageToken, default, cancellationToken).ConfigureAwait(false);
            bool contributed = false;
            foreach (SecurityBindingDocument binding in page.Bindings)
            {
                if (BindingAppliesToGrantee(binding, grantee, this.access))
                {
                    matchedBindings.Add(binding);
                    contributed = true;
                }
            }

            if (contributed)
            {
                page.Bindings.TransferOwnershipTo(workspace);
            }

            if (page.NextPageToken.IsEmpty)
            {
                break;
            }

            bindingPageToken = (JsonString)JsonString.CreateBuilder(workspace, (JsonString.Source)page.NextPageToken.Span).RootElement;
        }

        // capabilities: the scopes the matched bindings confer, resolved exactly as the runtime resolver does
        // (PersistentRowSecurityPolicy.ResolveGrantedScopes): an expired binding confers nothing, and an eligible-only
        // binding records §16.5.3 eligibility (self-elevation) rather than an active scope. One entry per scope, active
        // dominating eligible; the entry's expiry is the last conferring binding's, absent when one never expires.
        SortedDictionary<string, CapabilityGrant>? capabilities = null;
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        foreach (SecurityBindingDocument binding in matchedBindings)
        {
            if (binding.ExpiresAtValue is { } bindingExpiry && now >= bindingExpiry)
            {
                continue;
            }

            bool eligible = binding.EligibleOnlyValue;
            foreach (string scope in binding.ScopesArray())
            {
                capabilities ??= new SortedDictionary<string, CapabilityGrant>(StringComparer.Ordinal);
                var conferred = new CapabilityGrant(eligible, binding.ExpiresAtValue);
                capabilities[scope] = capabilities.TryGetValue(scope, out CapabilityGrant existing)
                    ? existing.Merge(conferred)
                    : conferred;
            }
        }

        // administers: a single reverse-index lookup keyed by the grantee's identity (bounded; materialised in full).
        IReadOnlyList<string> administered = this.catalog is { } catalog
            ? await catalog.ListAdministeredWorkflowsAsync(granteeIdentity, cancellationToken).ConfigureAwait(false)
            : [];

        // administersEnvironments: the environment twin of administers (the S3 reverse index, membership over the
        // grantee's identity).
        IReadOnlyList<string> administeredEnvironments = this.environments is { } environmentAdministration
            ? await environmentAdministration.ListAdministeredEnvironmentsAsync(granteeIdentity, cancellationToken).ConfigureAwait(false)
            : [];

        // credentialUsage: page the credential store (System context — usage entitlement is not management-reach-scoped)
        // keeping the bindings the grantee's identity may use (label-superset). Same page-lifetime discipline as bindings.
        var matchedCredentials = new List<SourceCredentialBinding>();
        if (this.credentials is { } credentialStore)
        {
            JsonString credentialPageToken = default;
            while (true)
            {
                using SourceCredentialPage page = await credentialStore.ListAsync(AccessContext.System, CredentialScanPageSize, credentialPageToken, cancellationToken).ConfigureAwait(false);
                bool contributed = false;
                foreach (SourceCredentialBinding binding in page.Bindings)
                {
                    // Only the credentials scoped to THIS grantee's identity (a usage-scoped binding the grantee
                    // satisfies). A shared binding (no usage grant, usable by any run) is deployment-wide, not a
                    // grantee-specific grant, so it is omitted from the overview (design §6.1).
                    if (binding.IsUsageScoped && binding.IsUsableBy(granteeIdentity))
                    {
                        matchedCredentials.Add(binding);
                        contributed = true;
                    }
                }

                if (contributed)
                {
                    page.Bindings.TransferOwnershipTo(workspace);
                }

                if (page.NextPageToken.IsEmpty)
                {
                    break;
                }

                credentialPageToken = (JsonString)JsonString.CreateBuilder(workspace, (JsonString.Source)page.NextPageToken.Span).RootElement;
            }
        }

        // §849: enrich the administered rows server-side so each reads without a per-row detail fetch (the UI still makes
        // one /access/grants call). Bounded — the administered sets are materialised in full above. The reads run under
        // System context: the overview completely describes what the grantee administers, gated by the security:read scope.
        // Each row's display values are materialised to managed strings inside the loop, so no page document is held.
        var administeredWorkflows = new List<AdministeredWorkflowRow>(administered.Count);
        foreach (string baseWorkflowId in administered)
        {
            AdministeredWorkflowRow row = new(baseWorkflowId, null, null, null, null);
            if (this.catalog is { } enrichCatalog)
            {
                // The representative version (the one the catalog surfaces for this workflow) via a single bounded lookup.
                using CatalogPage page = await enrichCatalog.SearchAsync(
                    new CatalogQuery(BaseWorkflowId: baseWorkflowId, DistinctWorkflows: true, Limit: 1), AccessContext.System, cancellationToken).ConfigureAwait(false);
                if (page.Versions.Count > 0)
                {
                    CatalogVersion version = page.Versions[0];
                    string? title = version.Title.IsNotUndefined() ? (string)version.Title : null;
                    string? owner = version.Owner.IsNotUndefined() && version.Owner.Name.IsNotUndefined() ? (string)version.Owner.Name : null;
                    string? status = version.Status.IsNotUndefined() ? (string)version.Status : null;
                    row = new(baseWorkflowId, title, version.Ref.VersionNumber, status, owner);
                }
            }

            administeredWorkflows.Add(row);
        }

        var administeredEnvironmentRows = new List<AdministeredEnvironmentRow>(administeredEnvironments.Count);
        foreach (string environmentName in administeredEnvironments)
        {
            string? displayName = null;
            bool hasEnvironment = false;
            bool allowsDraftRuns = false;
            if (this.environmentStore is { } enrichEnvironments)
            {
                using ParsedJsonDocument<Environment>? environmentDoc = await enrichEnvironments.GetAsync(environmentName, AccessContext.System, cancellationToken).ConfigureAwait(false);
                if (environmentDoc is { } environmentRecord)
                {
                    hasEnvironment = true;
                    displayName = environmentRecord.RootElement.DisplayName.IsNotUndefined() ? (string)environmentRecord.RootElement.DisplayName : null;
                    allowsDraftRuns = ((JsonElement)environmentRecord.RootElement.AllowsDraftRuns).ValueKind == JsonValueKind.True;
                }
            }

            bool hasAvailability = false;
            int availabilityCount = 0;
            bool availabilityCapped = false;
            if (this.availabilityStore is { } enrichAvailability)
            {
                (availabilityCount, availabilityCapped) = await enrichAvailability.CountByEnvironmentAsync(environmentName, CountCap, cancellationToken).ConfigureAwait(false);
                hasAvailability = true;
            }

            administeredEnvironmentRows.Add(new(environmentName, displayName, hasEnvironment, allowsDraftRuns, hasAvailability, availabilityCount, availabilityCapped));
        }

        // Build the response closure-free: a single context carries the five matched collections, threaded through the
        // outer object build and each inner array build (no capturing lambda). The grantee is echoed as a congruent
        // whole-document From wrap over the workspace-owned grantee document.
        var overviewContext = new AccessGrantsContext(administeredWorkflows, administeredEnvironmentRows, capabilities, matchedBindings, matchedCredentials);
        Models.AccessGrantsOverview.Source<AccessGrantsContext> body = Models.AccessGrantsOverview.Build(
            in overviewContext,
            administers: Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Build(in overviewContext, BuildAdministeredWorkflows),
            administersEnvironments: Models.AccessGrantsOverview.AccessGrantsAdministeredEnvironmentArray.Build(in overviewContext, BuildAdministeredEnvironments),
            bindings: Models.AccessGrantsOverview.SecurityBindingSummaryArray.Build(in overviewContext, BuildAccessBindings),
            capabilities: Models.AccessGrantsOverview.AccessGrantsCapabilityArray.Build(in overviewContext, BuildCapabilities),
            credentialUsage: Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Build(in overviewContext, BuildCredentialUsages),
            grantee: (Models.ResolvedGrantee.Source)Models.ResolvedGrantee.From(grantee));
        return GetAccessGrantsResult.Ok(body, workspace);
    }

    private static bool IsInvalidRule(string expression, out Models.ProblemDetails.Source problem)
    {
        try
        {
            SecurityRule.Compile(expression);
            problem = default;
            return false;
        }
        catch (FormatException ex)
        {
            problem = Problem("invalid-rule", "Invalid rule expression", 400, ex.Message);
            return true;
        }
    }

    // Builds the draft binding the store completes: a free, zero-copy element view over the already-parsed request body
    // (its operator content carried bytes-to-bytes — no grant rebuild, no per-field strings, no pooled draft document).
    // The store stamps id/etag/created and defaults any omitted verb grant to None (write-body grants are optional).
    private static bool ReadBinding(Models.SecurityBindingWrite body, out SecurityBindingDocument draft, out Models.ProblemDetails.Source problem)
    {
        problem = default;
        draft = default;
        if (body.ClaimType.IsUndefined())
        {
            problem = Problem("invalid-binding", "Invalid binding", 400, "A 'claimType' is required.");
            return false;
        }

        draft = SecurityBindingDocument.From(body);
        return true;
    }

    // The self-elevation guard (§16.5.3, defense in depth): a caller may not author a binding that grants ITSELF elevated
    // (write/purge) reach — that escalation must go through the access-request → approve flow (separation of duties). A
    // binding the caller matches that grants only read is allowed (direct group/role policy authoring). With no caller
    // (the unscoped/Open posture, where there is no row reach to elevate) the guard is inert.
    private bool SelfElevates(SecurityBindingDocument draft, out Models.ProblemDetails.Source problem)
    {
        problem = default;
        if (this.access is not { } access || access.CurrentPrincipal is null)
        {
            return false;
        }

        if (!GrantsElevatedReach(draft.Write) && !GrantsElevatedReach(draft.Purge))
        {
            return false;
        }

        if (!CallerMatches(access.InternalTags(), access.InternalTagPrefix, draft))
        {
            return false;
        }

        problem = Problem(
            "self-elevation",
            "Self-elevation not permitted",
            403,
            "You may not create a binding that grants write or purge reach to a claim you hold. Request elevated access through the access-request flow instead.");
        return true;
    }

    // Whether a verb grant confers reach (unrestricted or rule-bounded). Gated on ValueKind == Object so an absent verb
    // grant — a default VerbGrantInfo with a null parent, which IsNotUndefined() does NOT catch — never NREs its nested
    // RuleNames accessor (the absent-optional-complex-property trap).
    private static bool GrantsElevatedReach(SecurityBindingDocument.VerbGrantInfo grant)
        => grant.ValueKind == JsonValueKind.Object && !grant.IsEmptyValue;

    // Whether the caller holds the binding's full selector by MEMBERSHIP over the caller's canonical sys: identity
    // (§16.5.4): the caller's stamped identity must CONTAIN every clause — the primary claimType/claimValue clause AND
    // every additional clause. The wildcard '*' primary matches every authenticated caller; a clause with no value
    // matches any value of its dimension; otherwise the identity must carry a tag whose operator-facing dimension (the
    // sys: prefix stripped) equals the clause dimension and value. Decided on the same identity the runtime reach matcher
    // uses (not raw token claims), so the self-elevation guard fires exactly when the caller is in the set the binding
    // grants to — a caller who satisfies the primary but not an additional clause is outside the grant and not elevating.
    private static bool CallerMatches(IReadOnlyList<SecurityTag> identity, string prefix, SecurityBindingDocument draft)
    {
        // Primary clause: the wildcard matches every authenticated caller; otherwise the identity must contain it.
        if (!string.Equals(draft.ClaimTypeValue, "*", StringComparison.Ordinal)
            && !IdentityContains(identity, prefix, draft.ClaimTypeValue, draft.ClaimValueOrNull))
        {
            return false;
        }

        // Every additional clause must also be contained (the tag-set selector is a conjunction).
        if (draft.AdditionalClauses.IsNotUndefined())
        {
            foreach (SecurityBindingDocument.AdditionalClause clause in draft.AdditionalClauses.EnumerateArray())
            {
                if (!IdentityContains(identity, prefix, (string)clause.DimensionValue, clause.Value.IsNotUndefined() ? (string)clause.Value : null))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Whether the caller's stamped identity contains a tag whose operator-facing dimension (the internal prefix stripped)
    // equals the clause dimension, and whose value equals the clause value when one is pinned.
    private static bool IdentityContains(IReadOnlyList<SecurityTag> identity, string prefix, string dimension, string? value)
    {
        foreach (SecurityTag tag in identity)
        {
            string tagDimension = tag.Key.StartsWith(prefix, StringComparison.Ordinal) ? tag.Key[prefix.Length..] : tag.Key;
            if (string.Equals(tagDimension, dimension, StringComparison.Ordinal)
                && (value is null || string.Equals(tag.Value, value, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    // SecurityRuleSummary is congruent with the stored SecurityRuleDocument (identical fields — the single-document
    // create/get/update sites already respond with the whole-document Models.SecurityRuleSummary.From wrap), so the list
    // wraps each rule the same way: no field-copy, no per-field From() ternary. The build is closure-free (the rule list
    // is threaded as the context) and inlined in HandleSearchSecurityRulesAsync (the list Build is ref-scoped to its `in`
    // argument, so it cannot be returned from a helper).
    private static void BuildRuleSummaries(in IReadOnlyList<SecurityRuleDocument> rules, ref Models.SecurityRuleList.SecurityRuleSummaryArray.Builder array)
    {
        foreach (SecurityRuleDocument r in rules)
        {
            array.AddItem(Models.SecurityRuleSummary.From(r));
        }
    }

    // The orderings projection is closure-free (each dimension's name + label list is threaded as a ref-struct context).
    // The labels are genuine C# config strings (not a CTJ element), so they encode to UTF-8 once via the implicit
    // string -> JsonString.Source conversion, applied INSIDE the builder's Create/AddItem (a void mutate) rather than
    // passed to a Source-returning Build — the span-bearing temp would otherwise fail ref-safety escape analysis.
    private static void BuildOrderings(in SecurityLabelOrderings cfg, ref Models.SecurityOrderingList.SecurityOrderingArray.Builder array)
    {
        foreach (string dimension in cfg.Dimensions)
        {
            cfg.TryGetOrdering(dimension, out IReadOnlyList<string> labels);
            var ctx = new OrderingContext(dimension, labels);
            array.AddItem(Models.SecurityOrdering.Build(in ctx, BuildOrdering));
        }
    }

    private static void BuildOrdering(in OrderingContext ctx, ref Models.SecurityOrdering.Builder b)
        => b.Create(
            in ctx,
            dimension: ctx.Dimension,
            labels: Models.SecurityOrdering.JsonStringArray.Build(in ctx, BuildLabels));

    private static void BuildLabels(in OrderingContext ctx, ref Models.SecurityOrdering.JsonStringArray.Builder array)
    {
        foreach (string label in ctx.Labels)
        {
            array.AddItem(label);
        }
    }

    // One ordered dimension's name + ascending labels, threaded so the SecurityOrdering build stays closure-free.
    private readonly ref struct OrderingContext(string dimension, IReadOnlyList<string> labels)
    {
        public string Dimension { get; } = dimension;

        public IReadOnlyList<string> Labels { get; } = labels;
    }

    // The binding summary is a closure-free Build<TContext> projection: the binding itself is threaded as the context
    // (it is a struct : IJsonElement, span-capable) through the static BuildBindingSummary, so neither the single-document
    // sites nor the list allocate a capturing delegate.
    private static Models.SecurityBindingSummary.Source<SecurityBindingDocument> ToBindingSource(SecurityBindingDocument binding)
        => Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary);

    private static void BuildBindingSummary(in SecurityBindingDocument binding, ref Models.SecurityBindingSummary.Builder b)
    {
        // Scalars carried bytes-native (Models.JsonString.From — zero-copy element wrap); the three verb grants are
        // congruent with the stored VerbGrantInfo, so they wrap verbatim (Models.VerbGrant.From). The optional
        // scalars carry the binding's raw CTJ value straight through From() — which propagates Undefined, so an absent
        // field is omitted with no IsNotUndefined/XxxOrNull ternary (the "Undefined not null" convention). The stored
        // scopes/expiresAt/eligibleOnly ride along the same way, so the overview's capabilities are traceable to the
        // bindings that confer them.
        b.Create(
            claimType: Models.JsonString.From(binding.ClaimType),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            order: binding.OrderValue,
            purge: Models.VerbGrant.From(binding.Purge),
            read: Models.VerbGrant.From(binding.Read),
            write: Models.VerbGrant.From(binding.Write),
            claimValue: Models.JsonString.From(binding.ClaimValue),
            additionalClauses: Models.SecurityBindingSummary.SecurityBindingClauseArray.From(binding.AdditionalClauses),
            description: Models.JsonString.From(binding.Description),
            eligibleOnly: Models.JsonBoolean.From(binding.EligibleOnly),
            expiresAt: Models.JsonDateTime.From(binding.ExpiresAt),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy),
            scopes: Models.SecurityBindingSummary.JsonStringArray.From(binding.Scopes));
    }

    private static void BuildBindingSummaries(in IReadOnlyList<SecurityBindingDocument> bindings, ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder array)
    {
        foreach (SecurityBindingDocument binding in bindings)
        {
            array.AddItem(Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary));
        }
    }

    private static GetAccessGrantsResult InvalidGrantee(JsonWorkspace workspace)
        => GetAccessGrantsResult.BadRequest(
            Problem("invalid-grantee", "Invalid grantee token", 400, "The 'grantee' query parameter is not a valid resolved-grantee token."), workspace);

    // Reconstructs the grantee's INTERNAL identity from its wire grants. A grantee's identity is described back over the
    // wire in the operator-facing form — the sys: prefix STRIPPED (design §16.5.4 / DescribeUsageScope), e.g. {group,iss}
    // for a team — so it must be resolved back through ControlPlaneAccess.ResolveUsageGrantInto (which re-adds the
    // reserved prefix: group -> sys:group), exactly as every peer handler does (AdministratorsHandler.BuildGranteeIdentity).
    // Using the wire dimension verbatim yields {group,iss} whose digest never matches the stored {sys:group,sys:iss}
    // founder/usage identity, so administers and usage-scoped credentials would resolve to nothing. An absent identity
    // yields the empty set.
    private static void WriteGranteeIdentity(ref IdentityBuilder builder, in GranteeIdentityState state)
    {
        foreach (Models.AdministratorIdentity grant in state.Identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
            using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
            state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
        }
    }

    // The grantee identity + the access policy that maps its wire grants back to internal sys: tags, threaded into the
    // closure-free SecurityTagSet.Build.
    private readonly ref struct GranteeIdentityState(ControlPlaneAccess access, Models.ResolvedGrantee.AdministratorIdentityArray identity)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.ResolvedGrantee.AdministratorIdentityArray Identity { get; } = identity;
    }

    // Whether a binding grants the grantee reach by MEMBERSHIP over the grantee's resolved identity: the binding's
    // selector is a tag SET (its primary claimType/claimValue clause AND every additional clause), and it applies only
    // when the grantee's identity CONTAINS every clause. The wildcard '*' primary matches every principal (and, with no
    // additional clauses, even an identity-less grantee); an additional clause carries no wildcard. Compared bytes-native
    // off both documents' UTF-8 — each clause's raw dimension/value vs each grantee identity item's sys:-stripped
    // dimension / value read as unescaped spans — so nothing is materialised into a managed string or grant list.
    private static bool BindingAppliesToGrantee(SecurityBindingDocument binding, Models.ResolvedGrantee grantee, ControlPlaneAccess? access)
    {
        bool wildcardPrimary = binding.ClaimType.ValueEquals("*"u8);
        bool hasAdditional = binding.AdditionalClauses.IsNotUndefined() && binding.AdditionalClauses.GetArrayLength() > 0;

        // A pure wildcard binding (no additional clauses) matches every principal, even one with no resolved identity.
        if (wildcardPrimary && !hasAdditional)
        {
            return true;
        }

        // Any non-wildcard clause needs a resolved identity to satisfy.
        if (!grantee.Identity.IsNotUndefined())
        {
            return false;
        }

        // Primary clause (skipped when wildcard: it already matches every principal).
        if (!wildcardPrimary && !GranteeSatisfiesClause(binding.ClaimType, binding.ClaimValue, grantee, access))
        {
            return false;
        }

        // Every additional clause must also be satisfied (the tag-set selector is a conjunction).
        if (hasAdditional)
        {
            foreach (SecurityBindingDocument.AdditionalClause clause in binding.AdditionalClauses.EnumerateArray())
            {
                if (!GranteeSatisfiesClause(clause.DimensionValue, clause.Value, grantee, access))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Whether the grantee's resolved identity contains a tag satisfying one selector clause: the operator-facing claim
    // derived from one of the grantee's sys: identity grants must equal the clause dimension (and value, when the clause
    // pins one). The grantee identity is the resolved internal form; a clause keys on the operator-facing dimension it
    // derives from (design §6.5, lossy), so strip the deployment-configured internal prefix before matching (sys:sub ->
    // sub; a bare team/role dimension is unchanged). Using the configured prefix (via ControlPlaneAccess) keeps this
    // overview match aligned with runtime enforcement on a deployment whose prefix is not the "sys:" default; with no
    // access resolver at all, the default prefix is the correct fallback.
    private static bool GranteeSatisfiesClause(JsonString clauseDimension, JsonString clauseValue, Models.ResolvedGrantee grantee, ControlPlaneAccess? access)
    {
        bool valuePinned = !clauseValue.IsUndefined();
        foreach (Models.AdministratorIdentity item in grantee.Identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = item.DimensionValue.GetUtf8String();
            ReadOnlySpan<byte> claim = access is { } a ? a.StripInternalPrefix(dimension.Span) : StripDefaultInternalPrefix(dimension.Span);
            if (!clauseDimension.ValueEquals(claim))
            {
                continue;
            }

            if (!valuePinned)
            {
                return true;
            }

            using UnescapedUtf8JsonString value = item.Value.GetUtf8String();
            if (clauseValue.ValueEquals(value.Span))
            {
                return true;
            }
        }

        return false;
    }

    // Strips the DEFAULT internal namespace prefix from a resolved identity dimension — the fallback used only when there
    // is no ControlPlaneAccess resolver to supply the deployment-configured prefix (an unscoped host). The literal is the
    // UTF-8 of SecurityShell.DefaultInternalPrefix ("sys:"). A dimension without the prefix (a bare team/role) is
    // returned unchanged.
    private static ReadOnlySpan<byte> StripDefaultInternalPrefix(ReadOnlySpan<byte> dimension)
        => dimension.StartsWith("sys:"u8) ? dimension["sys:"u8.Length..] : dimension;

    // The bindings array reuses the existing whole-summary projection (BuildBindingSummary) per matched binding — the
    // item type is shared with the search response; only the enclosing array type differs. Closure-free: the matched
    // list is threaded as the context and each binding as the per-item context.
    private static void BuildAccessBindings(in AccessGrantsContext ctx, ref Models.AccessGrantsOverview.SecurityBindingSummaryArray.Builder array)
    {
        foreach (SecurityBindingDocument binding in ctx.Bindings)
        {
            array.AddItem(Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary));
        }
    }

    // Each administered base workflow id is a genuine C# string (from the reverse index), so it encodes to UTF-8 once
    // via the implicit string -> JsonString.Source conversion applied INSIDE the item's Create (a void mutate) rather
    // than passed to a Source-returning Build — the span-bearing temp would otherwise fail ref-safety escape analysis
    // (the same pattern the security-orderings labels use).
    private static void BuildAdministeredWorkflows(in AccessGrantsContext ctx, ref Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Builder array)
    {
        foreach (AdministeredWorkflowRow row in ctx.Administered)
        {
            array.AddItem(Models.AccessGrantsAdministeredWorkflow.Build(in row, BuildAdministeredWorkflow));
        }
    }

    private static void BuildAdministeredEnvironments(in AccessGrantsContext ctx, ref Models.AccessGrantsOverview.AccessGrantsAdministeredEnvironmentArray.Builder array)
    {
        foreach (AdministeredEnvironmentRow row in ctx.AdministeredEnvironments)
        {
            array.AddItem(Models.AccessGrantsAdministeredEnvironment.Build(in row, BuildAdministeredEnvironment));
        }
    }

    // §849: the enriched environment row — its display name, draft-run policy, and a bounded availability count (the
    // count-API pattern). Optional fields are omitted (default) when the environment could not be read or no store is wired.
    private static void BuildAdministeredEnvironment(in AdministeredEnvironmentRow row, ref Models.AccessGrantsAdministeredEnvironment.Builder b)
        => b.Create(
            environment: row.Environment,
            allowsDraftRuns: row.HasEnvironment ? (Models.JsonBoolean.Source)row.AllowsDraftRuns : default,
            availability: row.HasAvailability ? Models.CountResult.Build(capped: row.AvailabilityCapped, count: row.AvailabilityCount) : default,
            displayName: row.DisplayName is { } displayName ? (Models.JsonString.Source)displayName : default);

    private static void BuildCapabilities(in AccessGrantsContext ctx, ref Models.AccessGrantsOverview.AccessGrantsCapabilityArray.Builder array)
    {
        if (ctx.Capabilities is not { } capabilities)
        {
            return;
        }

        // The dictionary is ordinal-sorted by scope, so the response order is deterministic. The optional expiry is
        // omitted via default when no conferring binding expires.
        foreach (KeyValuePair<string, CapabilityGrant> capability in capabilities)
        {
            array.AddItem(Models.AccessGrantsCapability.Build(
                eligible: capability.Value.Eligible,
                scope: capability.Key,
                expiresAt: capability.Value.ExpiresAt is { } expiresAt ? (Models.JsonDateTime.Source)expiresAt : default));
        }
    }

    // §849: the enriched workflow row — its representative version's title, number, status, and owner name. Optional
    // fields are omitted (default) when no version is readable.
    private static void BuildAdministeredWorkflow(in AdministeredWorkflowRow row, ref Models.AccessGrantsAdministeredWorkflow.Builder b)
        => b.Create(
            baseWorkflowId: row.BaseWorkflowId,
            latestVersion: row.LatestVersion is { } latestVersion ? (Models.JsonInt32.Source)latestVersion : default,
            owner: row.Owner is { } owner ? (Models.JsonString.Source)owner : default,
            status: row.Status is { } status ? (Models.JsonString.Source)status : default,
            title: row.Title is { } title ? (Models.JsonString.Source)title : default);

    // Each usable credential is projected to its (sourceName, environment) key, carried bytes-native (Models.JsonString.From
    // over the binding's raw accessors — zero-copy element wraps referencing the workspace-owned page document).
    private static void BuildCredentialUsages(in AccessGrantsContext ctx, ref Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Builder array)
    {
        foreach (SourceCredentialBinding binding in ctx.Credentials)
        {
            array.AddItem(Models.AccessGrantsCredentialUsage.Build(
                environment: Models.JsonString.From(binding.Environment),
                sourceName: Models.JsonString.From(binding.SourceName)));
        }
    }

    // The matched aggregation sets, threaded as one context through the access-grants overview build (no closure). The
    // lists hold the matched documents (whose pages were handed to the workspace), so they live to serialization.
    private readonly ref struct AccessGrantsContext(List<AdministeredWorkflowRow> administered, List<AdministeredEnvironmentRow> administeredEnvironments, SortedDictionary<string, CapabilityGrant>? capabilities, List<SecurityBindingDocument> bindings, List<SourceCredentialBinding> credentials)
    {
        public List<AdministeredWorkflowRow> Administered { get; } = administered;

        public List<AdministeredEnvironmentRow> AdministeredEnvironments { get; } = administeredEnvironments;

        public SortedDictionary<string, CapabilityGrant>? Capabilities { get; } = capabilities;

        public List<SecurityBindingDocument> Bindings { get; } = bindings;

        public List<SourceCredentialBinding> Credentials { get; } = credentials;
    }

    // An administered-workflow row enriched with its representative version's display values (design §849). All display
    // fields are materialised managed values (no page-document lifetime), absent when no version is readable.
    private readonly record struct AdministeredWorkflowRow(string BaseWorkflowId, string? Title, int? LatestVersion, string? Status, string? Owner);

    // An administered-environment row enriched with the environment's summary and a bounded availability count (§849).
    private readonly record struct AdministeredEnvironmentRow(string Environment, string? DisplayName, bool HasEnvironment, bool AllowsDraftRuns, bool HasAvailability, int AvailabilityCount, bool AvailabilityCapped);

    // One resolved capability entry: eligible-only (a §16.5.3 eligibility) vs active, and the expiry to show. Merging
    // two conferrals of the same scope: active dominates eligible (an eligibility adds nothing when the scope is held
    // actively); within the same class a never-expiring conferral wins outright, otherwise the later expiry stands.
    private readonly record struct CapabilityGrant(bool Eligible, DateTimeOffset? ExpiresAt)
    {
        public CapabilityGrant Merge(CapabilityGrant other)
        {
            if (this.Eligible != other.Eligible)
            {
                return this.Eligible ? other : this;
            }

            DateTimeOffset? expiresAt = this.ExpiresAt is { } mine && other.ExpiresAt is { } theirs
                ? (mine >= theirs ? mine : theirs)
                : null;
            return new CapabilityGrant(this.Eligible, expiresAt);
        }
    }

    private static Models.ProblemDetails.Source NotFoundProblem(string kind, string id)
        => Problem($"{kind}-not-found", $"{char.ToUpperInvariant(kind[0])}{kind[1..]} not found", 404, $"No security {kind} '{id}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    private ValueTask RefreshAsync(CancellationToken cancellationToken)
        => this.policy is { } p ? p.RefreshAsync(cancellationToken) : default;
}