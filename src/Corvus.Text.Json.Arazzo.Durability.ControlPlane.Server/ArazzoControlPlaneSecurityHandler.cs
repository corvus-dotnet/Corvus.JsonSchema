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
        return ListSecurityRulesResult.Ok(ToRuleList(rules), workspace);
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
        return ListSecurityBindingsResult.Ok(ToBindingList(bindings), workspace);
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

    private static Models.SecurityRuleSummary.Source ToRuleSource(SecurityRuleDocument r)
        => new((ref Models.SecurityRuleSummary.Builder b) =>
        {
            Models.JsonString.Source description = default;
            if (r.DescriptionOrNull is { } d)
            {
                description = d;
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (r.UpdatedByOrNull is { } u)
            {
                lastUpdatedBy = u;
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (r.UpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            b.Create(
                createdAt: r.CreatedAtValue,
                createdBy: r.CreatedByValue,
                etag: r.EtagValue.Value ?? string.Empty,
                expression: r.ExpressionValue,
                name: r.NameValue,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy);
        });

    private static Models.SecurityRuleList.Source ToRuleList(IReadOnlyList<SecurityRuleDocument> rules)
        => new((ref Models.SecurityRuleList.Builder b) => b.Create(
            rules: new Models.SecurityRuleList.SecurityRuleSummaryArray.Source((ref Models.SecurityRuleList.SecurityRuleSummaryArray.Builder ab) =>
            {
                foreach (SecurityRuleDocument r in rules)
                {
                    ab.AddItem(ToRuleSource(r));
                }
            })));

    private static Models.SecurityBindingSummary.Source ToBindingSource(SecurityBindingDocument binding)
        => new((ref Models.SecurityBindingSummary.Builder b) =>
        {
            Models.JsonString.Source claimValue = default;
            if (binding.ClaimValue.IsNotUndefined())
            {
                claimValue = Models.JsonString.From(binding.ClaimValue);
            }

            Models.JsonString.Source description = default;
            if (binding.Description.IsNotUndefined())
            {
                description = Models.JsonString.From(binding.Description);
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (binding.LastUpdatedBy.IsNotUndefined())
            {
                lastUpdatedBy = Models.JsonString.From(binding.LastUpdatedBy);
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (binding.UpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            // Scalars carried bytes-native (Models.JsonString.From — zero-copy element wrap); the three verb grants are
            // congruent with the stored VerbGrantInfo, so they wrap verbatim (Models.VerbGrant.From). The summary
            // deliberately omits the stored scopes/expiresAt/eligibleOnly — per-field selection keeps them out.
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
                claimValue: claimValue,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy);
        });

    private static Models.SecurityBindingList.Source ToBindingList(IReadOnlyList<SecurityBindingDocument> bindings)
        => new((ref Models.SecurityBindingList.Builder b) => b.Create(
            bindings: new Models.SecurityBindingList.SecurityBindingSummaryArray.Source((ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder ab) =>
            {
                foreach (SecurityBindingDocument binding in bindings)
                {
                    ab.AddItem(ToBindingSource(binding));
                }
            })));

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