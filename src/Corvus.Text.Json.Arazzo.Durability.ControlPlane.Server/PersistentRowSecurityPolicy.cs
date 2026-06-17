// <copyright file="PersistentRowSecurityPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
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
    private volatile Compiled compiled = new(-1, []);

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
        // The snapshot is a pooled batch; dispose it once we have extracted the (owned) compiled state. We retain no
        // document references — only rule-expression strings and pre-resolved binding clauses.
        using SecurityPolicySnapshot snapshot = await this.store.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Generation == this.compiled.Generation)
        {
            return;
        }

        var expressions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SecurityRuleDocument rule in snapshot.Rules)
        {
            expressions[rule.NameValue] = rule.ExpressionValue;
        }

        // Pre-resolve each binding's claim match, per-verb clause string, and granted scopes ONCE per generation, so
        // Resolve / ResolveGrantedScopes do no rule-name lookups, clause-string building, or scope materialisation on
        // the hot path. anyScopeGrants lets the scope resolution early-out (zero allocation) when no binding grants a
        // capability — the common case.
        var bindings = new List<BindingClauses>(snapshot.Bindings.Count);
        bool anyScopeGrants = false;
        foreach (SecurityBindingDocument binding in snapshot.Bindings)
        {
            string[] scopes = binding.ScopesArray();
            anyScopeGrants |= scopes.Length > 0;
            bindings.Add(new BindingClauses(
                binding.ClaimTypeValue,
                binding.ClaimValueOrNull,
                VerbClauseFor(binding.Read, expressions),
                VerbClauseFor(binding.Write, expressions),
                VerbClauseFor(binding.Purge, expressions),
                scopes));
        }

        this.compiled = new Compiled(snapshot.Generation, bindings, anyScopeGrants);
    }

    /// <inheritdoc/>
    public override AccessContext Resolve(ClaimsPrincipal? principal)
    {
        Compiled current = this.compiled;
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = CollectClaims(principal);

        // Bindings matching this principal, in resolution order.
        var matched = new List<BindingClauses>();
        if (principal?.Identity?.IsAuthenticated == true)
        {
            foreach (BindingClauses binding in current.Bindings)
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
    public override IReadOnlyList<string> ResolveGrantedScopes(ClaimsPrincipal? principal)
    {
        Compiled current = this.compiled;

        // Common path: no binding in this generation grants any capability scope → nothing to union. Returns the
        // shared empty array — zero allocation on the per-request warm path.
        if (!current.AnyScopeGrants || principal?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        // Elevated path (a binding grants a scope): union the granted scopes of every matched binding. Allocates a
        // small list only when this principal actually holds a per-principal grant.
        List<string>? granted = null;
        foreach (BindingClauses binding in current.Bindings)
        {
            if (binding.Scopes.Length == 0 || !Matches(binding, principal))
            {
                continue;
            }

            foreach (string scope in binding.Scopes)
            {
                granted ??= [];
                if (!granted.Contains(scope))
                {
                    granted.Add(scope);
                }
            }
        }

        return (IReadOnlyList<string>?)granted ?? [];
    }

    /// <inheritdoc/>
    public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        => this.internalTagResolver?.Invoke(principal) ?? base.GetInternalTags(principal);

    /// <inheritdoc/>
    public override void ValidateUserTags(IReadOnlyList<SecurityTag> userTags) => this.shell.ValidateUserTags(userTags);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CollectClaims(ClaimsPrincipal? principal)
    {
        // Build the claim map in a single dictionary (the List values already satisfy IReadOnlyList<string>).
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (principal is not null)
        {
            foreach (Claim claim in principal.Claims)
            {
                if (!map.TryGetValue(claim.Type, out IReadOnlyList<string>? values))
                {
                    values = new List<string>();
                    map[claim.Type] = values;
                }

                ((List<string>)values).Add(claim.Value);
            }
        }

        return map;
    }

    private static bool Matches(BindingClauses binding, ClaimsPrincipal principal)
    {
        if (string.Equals(binding.ClaimType, "*", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (Claim claim in principal.Claims)
        {
            if (string.Equals(claim.Type, binding.ClaimType, StringComparison.Ordinal)
                && (binding.ClaimValue is null || string.Equals(claim.Value, binding.ClaimValue, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    // Pre-resolves a binding's verb grant (at snapshot time) to "((expr1) && (expr2) ...)", or to a null clause if the
    // grant is empty or references an unknown rule (fail-closed: a broken grant contributes nothing).
    private static VerbClause VerbClauseFor(VerbGrant grant, IReadOnlyDictionary<string, string> expressions)
    {
        if (grant.IsUnrestrictedValue)
        {
            return new VerbClause(true, null);
        }

        if (!grant.HasRuleNames)
        {
            return new VerbClause(false, null);
        }

        var parts = new List<string>(grant.RuleNameCount);
        foreach (JsonString ruleNameJson in grant.RuleNames.EnumerateArray())
        {
            if (!expressions.TryGetValue((string)ruleNameJson, out string? expression))
            {
                return new VerbClause(false, null);
            }

            parts.Add("(" + expression + ")");
        }

        return new VerbClause(false, parts.Count == 1 ? parts[0] : "(" + string.Join(" && ", parts) + ")");
    }

    private SecurityFilter? ResolveReach(
        List<BindingClauses> matched,
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims,
        Compiled current,
        Func<BindingClauses, VerbClause> selectVerb)
    {
        List<string>? clauses = null;
        foreach (BindingClauses binding in matched)
        {
            VerbClause verb = selectVerb(binding);
            if (verb.Unrestricted)
            {
                // Any matched Unrestricted grant for the verb → full reach (the operator path).
                return null;
            }

            if (verb.Clause is { } clause)
            {
                (clauses ??= []).Add(clause);
            }
        }

        // No grant for this verb → empty filter → deny-by-default.
        if (clauses is null)
        {
            return new SecurityFilter([], claims);
        }

        string combined = clauses.Count == 1 ? clauses[0] : string.Join(" || ", clauses);

        // Compiling the grammar is the expensive step; memoize per combined expression for this generation so repeat
        // principals/claim-sets reuse the parsed rule. (The filter itself is per-call — it binds the principal's claims.)
        SecurityRule[] rules = current.CompiledCombos.GetOrAdd(combined, static c => [SecurityRule.Compile(c)]);
        return this.shell.BuildFilter(rules, claims);
    }

    // A binding's per-verb grant, pre-resolved at snapshot time: unrestricted (operator) or a (possibly null) clause.
    private readonly record struct VerbClause(bool Unrestricted, string? Clause);

    // A binding pre-resolved at snapshot time: its claim match, the three per-verb clauses, and the granted scopes.
    private sealed record BindingClauses(string ClaimType, string? ClaimValue, VerbClause Read, VerbClause Write, VerbClause Purge, string[] Scopes);

    private sealed record Compiled(long Generation, IReadOnlyList<BindingClauses> Bindings, bool AnyScopeGrants = false)
    {
        // Combined-expression -> compiled rule, scoped to this generation (discarded when the snapshot advances).
        public ConcurrentDictionary<string, SecurityRule[]> CompiledCombos { get; } = new(StringComparer.Ordinal);
    }
}