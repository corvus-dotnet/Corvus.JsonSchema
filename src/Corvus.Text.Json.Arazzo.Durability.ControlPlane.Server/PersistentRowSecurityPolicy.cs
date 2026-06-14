// <copyright file="PersistentRowSecurityPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A <see cref="ControlPlaneRowSecurityPolicy"/> driven by a persistent <see cref="ISecurityPolicyStore"/>
/// (design §14.2): a principal's <see cref="AccessContext"/> is resolved from the stored rules and claim→rule
/// bindings rather than hand-rolled in code. The compiled rule/binding set is cached against the store's
/// generation token; call <see cref="RefreshAsync"/> at startup (after bootstrap seeding) and whenever the policy
/// changes (the security API does this after a write; a host may also poll on a timer for multi-process freshness).
/// </summary>
/// <remarks>
/// <para><b>Resolution (per verb):</b> collect the bindings whose claim matches the principal; if any grants the
/// verb <c>Unrestricted</c>, the reach is <see langword="null"/> (full access — the operator path). Otherwise the
/// reach is the deployment shell wrapper AND the OR across matched bindings of (that binding's rules ANDed) — so
/// grants compose as OR (more bindings → more access), always within the shell. If no binding grants the verb the
/// reach is an empty filter, which is <b>deny-by-default</b> (an authenticated principal with no binding sees
/// nothing).</para>
/// <para>Before the first <see cref="RefreshAsync"/> the policy denies everything (fail-closed).</para>
/// </remarks>
public sealed class PersistentRowSecurityPolicy : ControlPlaneRowSecurityPolicy
{
    private readonly ISecurityPolicyStore store;
    private readonly SecurityShell shell;
    private readonly Func<ClaimsPrincipal?, IReadOnlyList<SecurityTag>>? internalTagResolver;
    private volatile Compiled compiled = new(-1, [], new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Initializes a new instance of the <see cref="PersistentRowSecurityPolicy"/> class.</summary>
    /// <param name="store">The persistent rule/binding store.</param>
    /// <param name="shell">The deployment shell (mandated wrapper rules + reserved internal-tag prefix). Defaults to a prefix-only shell with no wrapper.</param>
    /// <param name="internalTagResolver">An optional deployment hook stamping internal (e.g. <c>sys:tenant</c>) tags from the principal on row creation.</param>
    public PersistentRowSecurityPolicy(ISecurityPolicyStore store, SecurityShell? shell = null, Func<ClaimsPrincipal?, IReadOnlyList<SecurityTag>>? internalTagResolver = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.shell = shell ?? new SecurityShell([]);
        this.internalTagResolver = internalTagResolver;
    }

    /// <summary>Reloads and recompiles the rule/binding snapshot if the store's generation has advanced.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the cache is current.</returns>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        SecurityPolicySnapshot snapshot = await this.store.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Generation == this.compiled.Generation)
        {
            return;
        }

        var expressions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SecurityRuleDocument rule in snapshot.Rules)
        {
            expressions[rule.NameValue] = rule.ExpressionValue;
        }

        this.compiled = new Compiled(snapshot.Generation, snapshot.Bindings, expressions);
    }

    /// <inheritdoc/>
    public override AccessContext Resolve(ClaimsPrincipal? principal)
    {
        Compiled current = this.compiled;
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = CollectClaims(principal);

        // Bindings matching this principal, in resolution order.
        var matched = new List<SecurityBindingDocument>();
        if (principal?.Identity?.IsAuthenticated == true)
        {
            foreach (SecurityBindingDocument binding in current.Bindings)
            {
                if (Matches(binding, principal))
                {
                    matched.Add(binding);
                }
            }
        }

        return new AccessContext(
            this.ResolveReach(matched, claims, current, static b => b.Read),
            this.ResolveReach(matched, claims, current, static b => b.Write),
            this.ResolveReach(matched, claims, current, static b => b.Purge));
    }

    /// <inheritdoc/>
    public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        => this.internalTagResolver?.Invoke(principal) ?? base.GetInternalTags(principal);

    /// <inheritdoc/>
    public override void ValidateUserTags(IReadOnlyList<SecurityTag> userTags) => this.shell.ValidateUserTags(userTags);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CollectClaims(ClaimsPrincipal? principal)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (principal is not null)
        {
            foreach (Claim claim in principal.Claims)
            {
                if (!map.TryGetValue(claim.Type, out List<string>? values))
                {
                    values = [];
                    map[claim.Type] = values;
                }

                values.Add(claim.Value);
            }
        }

        return map.ToDictionary(static e => e.Key, static e => (IReadOnlyList<string>)e.Value, StringComparer.Ordinal);
    }

    private static bool Matches(SecurityBindingDocument binding, ClaimsPrincipal principal)
    {
        if (string.Equals(binding.ClaimTypeValue, "*", StringComparison.Ordinal))
        {
            return true;
        }

        string? claimValue = binding.ClaimValueOrNull;
        foreach (Claim claim in principal.Claims)
        {
            if (string.Equals(claim.Type, binding.ClaimTypeValue, StringComparison.Ordinal)
                && (claimValue is null || string.Equals(claim.Value, claimValue, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    // Builds "((expr1) && (expr2) ...)" for a binding's verb grant, or null if the grant is empty or references an
    // unknown rule (fail-closed: a broken grant contributes nothing).
    private static string? ClauseFor(VerbGrant grant, Compiled current)
    {
        IReadOnlyList<string> ruleNames = grant.RuleNameList;
        if (grant.IsUnrestrictedValue || ruleNames.Count == 0)
        {
            return null;
        }

        var parts = new List<string>(ruleNames.Count);
        foreach (string ruleName in ruleNames)
        {
            if (!current.RuleExpressions.TryGetValue(ruleName, out string? expression))
            {
                return null;
            }

            parts.Add("(" + expression + ")");
        }

        return parts.Count == 1 ? parts[0] : "(" + string.Join(" && ", parts) + ")";
    }

    private SecurityFilter? ResolveReach(
        List<SecurityBindingDocument> matched,
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims,
        Compiled current,
        Func<SecurityBindingDocument, VerbGrant> selectGrant)
    {
        var clauses = new List<string>();
        foreach (SecurityBindingDocument binding in matched)
        {
            VerbGrant grant = selectGrant(binding);
            if (grant.IsUnrestrictedValue)
            {
                // Any matched Unrestricted grant for the verb → full reach (the operator path).
                return null;
            }

            if (ClauseFor(grant, current) is { } clause)
            {
                clauses.Add(clause);
            }
        }

        // No grant for this verb → empty filter → deny-by-default.
        if (clauses.Count == 0)
        {
            return new SecurityFilter([], claims);
        }

        string combined = clauses.Count == 1 ? clauses[0] : string.Join(" || ", clauses);
        return this.shell.BuildFilter([SecurityRule.Compile(combined)], claims);
    }

    private sealed record Compiled(long Generation, IReadOnlyList<SecurityBindingDocument> Bindings, IReadOnlyDictionary<string, string> RuleExpressions);
}