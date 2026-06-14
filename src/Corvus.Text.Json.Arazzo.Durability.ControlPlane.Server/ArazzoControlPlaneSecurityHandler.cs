// <copyright file="ArazzoControlPlaneSecurityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using DurabilityVerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.VerbGrant;

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
        IReadOnlyList<SecurityRuleDocument> rules = await this.store.ListRulesAsync(cancellationToken).ConfigureAwait(false);
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
            SecurityRuleDocument created = await this.store.AddRuleAsync(name, new SecurityRuleDefinition(expression, OptionalString(body.Description)), this.actor, cancellationToken).ConfigureAwait(false);
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return CreateSecurityRuleResult.Created(ToRuleSource(created), workspace);
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
        SecurityRuleDocument? rule = await this.store.GetRuleAsync(name, cancellationToken).ConfigureAwait(false);
        return rule is { } r
            ? GetSecurityRuleResult.Ok(ToRuleSource(r), workspace)
            : GetSecurityRuleResult.NotFound(NotFoundProblem("rule", name), workspace);
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

        SecurityRuleDocument? updated = await this.store.UpdateRuleAsync(name, new SecurityRuleDefinition(expression, OptionalString(body.Description)), WorkflowEtag.None, this.actor, cancellationToken).ConfigureAwait(false);
        if (updated is not { } r)
        {
            return UpdateSecurityRuleResult.NotFound(NotFoundProblem("rule", name), workspace);
        }

        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSecurityRuleResult.Ok(ToRuleSource(r), workspace);
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
        IReadOnlyList<SecurityBinding> bindings = await this.store.ListBindingsAsync(cancellationToken).ConfigureAwait(false);
        return ListSecurityBindingsResult.Ok(ToBindingList(bindings), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateSecurityBindingResult> HandleCreateSecurityBindingAsync(CreateSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (ReadBinding(parameters.Body, out SecurityBindingDefinition definition, out Models.ProblemDetails.Source problem))
        {
            SecurityBinding created = await this.store.AddBindingAsync(definition, this.actor, cancellationToken).ConfigureAwait(false);
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return CreateSecurityBindingResult.Created(ToBindingSource(created), workspace);
        }

        return CreateSecurityBindingResult.BadRequest(problem, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetSecurityBindingResult> HandleGetSecurityBindingAsync(GetSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.BindingId;
        SecurityBinding? binding = await this.store.GetBindingAsync(id, cancellationToken).ConfigureAwait(false);
        return binding is { } b
            ? GetSecurityBindingResult.Ok(ToBindingSource(b), workspace)
            : GetSecurityBindingResult.NotFound(NotFoundProblem("binding", id), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateSecurityBindingResult> HandleUpdateSecurityBindingAsync(UpdateSecurityBindingParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.BindingId;
        if (!ReadBinding(parameters.Body, out SecurityBindingDefinition definition, out Models.ProblemDetails.Source problem))
        {
            return UpdateSecurityBindingResult.BadRequest(problem, workspace);
        }

        SecurityBinding? updated = await this.store.UpdateBindingAsync(id, definition, WorkflowEtag.None, this.actor, cancellationToken).ConfigureAwait(false);
        if (updated is not { } b)
        {
            return UpdateSecurityBindingResult.NotFound(NotFoundProblem("binding", id), workspace);
        }

        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSecurityBindingResult.Ok(ToBindingSource(b), workspace);
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

    private static string? OptionalString(Models.JsonString value) => value.IsNotUndefined() ? (string)value : null;

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

    private static bool ReadBinding(Models.SecurityBindingWrite body, out SecurityBindingDefinition definition, out Models.ProblemDetails.Source problem)
    {
        problem = default;
        if (body.ClaimType.IsUndefined())
        {
            problem = Problem("invalid-binding", "Invalid binding", 400, "A 'claimType' is required.");
            definition = default;
            return false;
        }

        definition = new SecurityBindingDefinition(
            (string)body.ClaimType,
            OptionalString(body.ClaimValue),
            ToGrant(body.Read),
            ToGrant(body.Write),
            ToGrant(body.Purge),
            body.Order.IsNotUndefined() ? (int)body.Order : 0,
            OptionalString(body.Description));
        return true;
    }

    private static DurabilityVerbGrant ToGrant(Models.VerbGrant grant)
    {
        if (grant.IsUndefined())
        {
            return DurabilityVerbGrant.None;
        }

        if (grant.Unrestricted.IsNotUndefined() && (bool)grant.Unrestricted)
        {
            return DurabilityVerbGrant.Full;
        }

        var names = new List<string>();
        if (grant.RuleNames.IsNotUndefined())
        {
            foreach (Models.JsonString name in grant.RuleNames.EnumerateArray())
            {
                names.Add((string)name);
            }
        }

        return new DurabilityVerbGrant(false, names);
    }

    private static Models.VerbGrant.Source ToGrantSource(DurabilityVerbGrant grant)
        => new((ref Models.VerbGrant.Builder b) =>
        {
            Models.VerbGrant.JsonStringArray.Source ruleNames = default;
            if (!grant.Unrestricted && grant.RuleNames.Count > 0)
            {
                ruleNames = new Models.VerbGrant.JsonStringArray.Source((ref Models.VerbGrant.JsonStringArray.Builder ab) =>
                {
                    foreach (string name in grant.RuleNames)
                    {
                        ab.AddItem(name);
                    }
                });
            }

            b.Create(ruleNames: ruleNames, unrestricted: grant.Unrestricted);
        });

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

    private static Models.SecurityBindingSummary.Source ToBindingSource(SecurityBinding binding)
        => new((ref Models.SecurityBindingSummary.Builder b) =>
        {
            Models.JsonString.Source claimValue = default;
            if (binding.ClaimValue is { } cv)
            {
                claimValue = cv;
            }

            Models.JsonString.Source description = default;
            if (binding.Description is { } d)
            {
                description = d;
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (binding.UpdatedBy is { } u)
            {
                lastUpdatedBy = u;
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (binding.UpdatedAt is { } ua)
            {
                lastUpdatedAt = ua;
            }

            b.Create(
                claimType: binding.ClaimType,
                createdAt: binding.CreatedAt,
                createdBy: binding.CreatedBy,
                etag: binding.Etag.Value ?? string.Empty,
                id: binding.Id,
                order: binding.Order,
                purge: ToGrantSource(binding.Purge),
                read: ToGrantSource(binding.Read),
                write: ToGrantSource(binding.Write),
                claimValue: claimValue,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy);
        });

    private static Models.SecurityBindingList.Source ToBindingList(IReadOnlyList<SecurityBinding> bindings)
        => new((ref Models.SecurityBindingList.Builder b) => b.Create(
            bindings: new Models.SecurityBindingList.SecurityBindingSummaryArray.Source((ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder ab) =>
            {
                foreach (SecurityBinding binding in bindings)
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