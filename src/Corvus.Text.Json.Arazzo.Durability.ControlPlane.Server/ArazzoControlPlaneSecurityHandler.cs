// <copyright file="ArazzoControlPlaneSecurityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

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

    private readonly ISecurityPolicyStore store;
    private readonly PersistentRowSecurityPolicy? policy;
    private readonly ControlPlaneAccess? access;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ISourceCredentialStore? credentials;
    private readonly string actor;

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
    internal ArazzoControlPlaneSecurityHandler(ISecurityPolicyStore store, PersistentRowSecurityPolicy? policy, ControlPlaneAccess? access, ISecuredWorkflowCatalog? catalog = null, ISourceCredentialStore? credentials = null, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.policy = policy;
        this.access = access;
        this.catalog = catalog;
        this.credentials = credentials;
        this.actor = actor;
    }

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
            return CreateSecurityBindingResult.Forbidden(problem, workspace);
        }

        // The draft is a free, zero-copy element view over the request body (the store reads it synchronously) — no pooled
        // document to dispose. The summary references the returned pooled binding document (per-field From() wrap), so hand
        // that to the workspace; ownership transfers before RefreshAsync so a refresh failure cannot leak it.
        ParsedJsonDocument<SecurityBindingDocument> created = await this.store.AddBindingAsync(draft, this.actor, cancellationToken).ConfigureAwait(false);
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

        // The grantee's identity is already the resolved internal sys: form at the wire (design §16.5.4 — an identity is
        // described back as {dimension,value} grants over its sys: tags, e.g. sys:sub). It is taken verbatim (no
        // re-resolution) and keys the administered-workflows reverse index and the credential IsUsableBy match directly.
        SecurityTagSet granteeIdentity = SecurityTagSet.Empty;
        if (grantee.Identity.IsNotUndefined())
        {
            Models.ResolvedGrantee.AdministratorIdentityArray identity = grantee.Identity;
            granteeIdentity = SecurityTagSet.Build(in identity, WriteGranteeIdentity);
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
                if (BindingAppliesToGrantee(binding, grantee))
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

        // administers: a single reverse-index lookup keyed by the grantee's identity (bounded; materialised in full).
        IReadOnlyList<string> administered = this.catalog is { } catalog
            ? await catalog.ListAdministeredWorkflowsAsync(granteeIdentity, cancellationToken).ConfigureAwait(false)
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

        // Build the response closure-free: a single context carries the three matched collections, threaded through the
        // outer object build and each inner array build (no capturing lambda). The grantee is echoed as a congruent
        // whole-document From wrap over the workspace-owned grantee document.
        var overviewContext = new AccessGrantsContext(administered, matchedBindings, matchedCredentials);
        Models.AccessGrantsOverview.Source<AccessGrantsContext> body = Models.AccessGrantsOverview.Build(
            in overviewContext,
            administers: Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Build(in overviewContext, BuildAdministeredWorkflows),
            bindings: Models.AccessGrantsOverview.SecurityBindingSummaryArray.Build(in overviewContext, BuildAccessBindings),
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
        ClaimsPrincipal? principal = this.access?.CurrentPrincipal;
        if (principal is null)
        {
            return false;
        }

        if (!GrantsElevatedReach(draft.Write) && !GrantsElevatedReach(draft.Purge))
        {
            return false;
        }

        if (!CallerMatches(principal, draft.ClaimTypeValue, draft.ClaimValueOrNull))
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

    // Whether the caller holds the binding's claim: the wildcard '*' matches every authenticated caller; a claim-type-only
    // binding matches any value of that type; otherwise the exact type+value must be present.
    private static bool CallerMatches(ClaimsPrincipal principal, string claimType, string? claimValue)
    {
        if (claimType == "*")
        {
            return true;
        }

        // A claim-type-only binding matches any value of that type; the exact overloads take strings (no capturing
        // predicate closure). HasClaim(type, value) is ordinal; FindFirst(type) avoids a lambda for the type-only case.
        return claimValue is null
            ? principal.FindFirst(claimType) is not null
            : principal.HasClaim(claimType, claimValue);
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
        // congruent with the stored VerbGrantInfo, so they wrap verbatim (Models.VerbGrant.From). The summary
        // deliberately omits the stored scopes/expiresAt/eligibleOnly — per-field selection keeps them out. The optional
        // scalars carry the binding's raw CTJ value straight through From() — which propagates Undefined, so an absent
        // field is omitted with no IsNotUndefined/XxxOrNull ternary (the "Undefined not null" convention).
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
            description: Models.JsonString.From(binding.Description),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy));
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

    // Writes the grantee's already-resolved sys: identity grants into the identity buffer verbatim: each dimension/value
    // is read as unescaped UTF-8 straight off the grantee document (no managed string per grant) and added as a tag. The
    // wire identity is the sys: form (§16.5.4), so no re-resolution is needed. An absent identity yields the empty set.
    private static void WriteGranteeIdentity(ref IdentityBuilder builder, in Models.ResolvedGrantee.AdministratorIdentityArray identity)
    {
        foreach (Models.AdministratorIdentity grant in identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
            using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
            builder.Add(dimension.Span, value.Span);
        }
    }

    // Whether a binding grants the grantee reach: the wildcard '*' claim matches every principal; otherwise the
    // operator-facing claim derived from one of the grantee's sys: identity grants must match the binding's claim type
    // (and value, when the binding pins one). Compared bytes-native off both documents' UTF-8 — the binding's raw
    // ClaimType/ClaimValue vs each grantee identity item's sys:-stripped dimension / value read as unescaped spans — so
    // nothing is materialised into a managed string or grant list.
    private static bool BindingAppliesToGrantee(SecurityBindingDocument binding, Models.ResolvedGrantee grantee)
    {
        if (binding.ClaimType.ValueEquals("*"u8))
        {
            return true;
        }

        if (!grantee.Identity.IsNotUndefined())
        {
            return false;
        }

        bool claimValuePinned = !binding.ClaimValue.IsUndefined();
        foreach (Models.AdministratorIdentity item in grantee.Identity.EnumerateArray())
        {
            using UnescapedUtf8JsonString dimension = item.DimensionValue.GetUtf8String();

            // The grantee identity is the resolved sys: form; a binding keys on the operator-facing claim it derives
            // from (design §6.5, lossy), so strip the sys: prefix before matching (sys:sub -> sub; a bare team/role
            // dimension is unchanged).
            if (!binding.ClaimType.ValueEquals(StripSysPrefix(dimension.Span)))
            {
                continue;
            }

            if (!claimValuePinned)
            {
                return true;
            }

            using UnescapedUtf8JsonString value = item.Value.GetUtf8String();
            if (binding.ClaimValue.ValueEquals(value.Span))
            {
                return true;
            }
        }

        return false;
    }

    // Strips the internal "sys:" namespace prefix from a resolved identity dimension to recover the operator-facing claim
    // a binding keys on (sys:sub -> sub); a dimension without the prefix (a bare team/role) is returned unchanged.
    private static ReadOnlySpan<byte> StripSysPrefix(ReadOnlySpan<byte> dimension)
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
        foreach (string baseWorkflowId in ctx.Administered)
        {
            array.AddItem(Models.AccessGrantsAdministeredWorkflow.Build(in baseWorkflowId, BuildAdministeredWorkflow));
        }
    }

    private static void BuildAdministeredWorkflow(in string baseWorkflowId, ref Models.AccessGrantsAdministeredWorkflow.Builder b)
        => b.Create(baseWorkflowId: baseWorkflowId);

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
    private readonly ref struct AccessGrantsContext(IReadOnlyList<string> administered, List<SecurityBindingDocument> bindings, List<SourceCredentialBinding> credentials)
    {
        public IReadOnlyList<string> Administered { get; } = administered;

        public List<SecurityBindingDocument> Bindings { get; } = bindings;

        public List<SourceCredentialBinding> Credentials { get; } = credentials;
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