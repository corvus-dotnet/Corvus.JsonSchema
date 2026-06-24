// <copyright file="ArazzoControlPlaneSecurityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

    private readonly ISecurityPolicyStore store;
    private readonly PersistentRowSecurityPolicy? policy;
    private readonly string actor;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneSecurityHandler"/> class.</summary>
    /// <param name="store">The persistent rule/binding store the endpoints delegate to.</param>
    /// <param name="policy">An optional policy to refresh after a mutation so changes take effect in-process.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneSecurityHandler(ISecurityPolicyStore store, PersistentRowSecurityPolicy? policy = null, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.policy = policy;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListSecurityRulesResult> HandleListSecurityRulesAsync(ListSecurityRulesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        using PooledDocumentList<SecurityRuleDocument> rules = await this.store.ListRulesAsync(cancellationToken).ConfigureAwait(false);

        // Each summary is a whole-document SecurityRuleSummary.From wrap (the congruent projection the single-document
        // sites use), so it references its pooled document; the body is validated/serialized after this handler returns,
        // so hand the documents to the workspace (it disposes them at request end; `using rules` then only returns the
        // batch's backing array). The list body is built closure-free and consumed in place.
        rules.TransferOwnershipTo(workspace);
        IReadOnlyList<SecurityRuleDocument> ruleList = rules;
        Models.SecurityRuleList.Source<IReadOnlyList<SecurityRuleDocument>> body = Models.SecurityRuleList.Build(
            in ruleList,
            rules: Models.SecurityRuleList.SecurityRuleSummaryArray.Build(in ruleList, BuildRuleSummaries));
        return ListSecurityRulesResult.Ok(body, workspace);
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
    public async ValueTask<ListSecurityBindingsResult> HandleListSecurityBindingsAsync(ListSecurityBindingsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        using PooledDocumentList<SecurityBindingDocument> bindings = await this.store.ListBindingsAsync(cancellationToken).ConfigureAwait(false);

        // Each summary references its pooled binding document (the per-field From() projection is a zero-copy element
        // wrap), and the body is validated/serialized after this handler returns — so hand the documents to the
        // workspace (it disposes them at request end); `using bindings` then only returns the batch's backing array.
        bindings.TransferOwnershipTo(workspace);

        // The list body is built closure-free and consumed in place: SecurityBindingList.Build scopes its result to the
        // `in bindings` argument (ref-safety), so it cannot be returned from a helper — the array is projected through the
        // static BuildBindingSummaries, with the binding list threaded as the context.
        IReadOnlyList<SecurityBindingDocument> bindingList = bindings;
        Models.SecurityBindingList.Source<IReadOnlyList<SecurityBindingDocument>> body = Models.SecurityBindingList.Build(
            in bindingList,
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build(in bindingList, BuildBindingSummaries));
        return ListSecurityBindingsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateSecurityBindingResult> HandleCreateSecurityBindingAsync(CreateSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!ReadBinding(parameters.Body, out SecurityBindingDocument draft, out Models.ProblemDetails.Source problem))
        {
            return CreateSecurityBindingResult.BadRequest(problem, workspace);
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

    // SecurityRuleSummary is congruent with the stored SecurityRuleDocument (identical fields — the single-document
    // create/get/update sites already respond with the whole-document Models.SecurityRuleSummary.From wrap), so the list
    // wraps each rule the same way: no field-copy, no per-field From() ternary. The build is closure-free (the rule list
    // is threaded as the context) and inlined in HandleListSecurityRulesAsync (the list Build is ref-scoped to its `in`
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