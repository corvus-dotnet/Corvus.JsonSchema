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
    private readonly IAmbientIdentityDimensions? ambient;
    private readonly TimeProvider timeProvider;
    private readonly bool allowWildcardUnrestrictedReach;
    private volatile Compiled compiled = new(-1, []);

    /// <summary>Initializes a new instance of the <see cref="PersistentRowSecurityPolicy"/> class.</summary>
    /// <param name="store">The persistent rule/binding store.</param>
    /// <param name="shell">The deployment shell (mandated wrapper rules + reserved internal-tag prefix). Defaults to a prefix-only shell with no wrapper.</param>
    /// <param name="internalTagResolver">An optional deployment hook stamping internal (e.g. <c>sys:tenant</c>) tags from the principal on row creation.</param>
    /// <param name="timeProvider">The time source used to expire time-bound (§16.5.2) grants; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="allowWildcardUnrestrictedReach">
    /// When <see langword="false"/> (the secure default, §17.5/F7) a binding whose claim type is the wildcard <c>*</c>
    /// (matching every authenticated principal) may <strong>not</strong> grant <c>Unrestricted</c> (full, null) reach —
    /// that would hand every caller operator-level access and collapse tenant isolation. Such a grant is demoted to no
    /// reach for that verb; a wildcard binding may still grant rule-bounded reach. A single-tenant deployment that
    /// genuinely wants "everyone is an operator" sets this to <see langword="true"/> to opt back in.
    /// </param>
    /// <param name="ambient">
    /// An optional request-context ambient-dimension provider (§16.5.5). When supplied, the policy stamps its dimensions
    /// onto both runtime moments — <see cref="GetInternalTags"/> (the caller's own identity, for row creation and
    /// administration membership) and <see cref="Resolve"/> (the reach claims, prefix-stripped so a rule
    /// <c>sys:tenant == $claim.tenant</c> resolves the context tenant) — and onto the non-directory
    /// <see cref="ResolveGranteeIdentity"/>. Supplying the <strong>same</strong> provider here and to the directory
    /// projector is what makes a grantee resolved in a tenant context and the caller in that context stamp identically, so
    /// the two can never drift (the whole point of §16.5.5). <see langword="null"/> (the default) leaves behaviour unchanged.
    /// </param>
    public PersistentRowSecurityPolicy(ISecurityPolicyStore store, SecurityShell? shell = null, Func<ClaimsPrincipal?, IReadOnlyList<SecurityTag>>? internalTagResolver = null, TimeProvider? timeProvider = null, bool allowWildcardUnrestrictedReach = false, IAmbientIdentityDimensions? ambient = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.shell = shell ?? new SecurityShell([]);
        this.internalTagResolver = internalTagResolver;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.allowWildcardUnrestrictedReach = allowWildcardUnrestrictedReach;
        this.ambient = ambient;
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
        bool anyExpiringBindings = false;
        foreach (SecurityBindingDocument binding in snapshot.Bindings)
        {
            // An eligibility assignment (§16.5.3/§16.5.4) confers nothing active — exclude it from the compiled set so
            // the resolver never grants from it. The self-elevation strategy reads eligibility from the store directly.
            if (binding.EligibleOnlyValue)
            {
                continue;
            }

            string[] scopes = binding.ScopesArray();
            DateTimeOffset? expiresAt = binding.ExpiresAtValue;
            anyScopeGrants |= scopes.Length > 0;
            anyExpiringBindings |= expiresAt.HasValue;

            // §17.5/F7: a wildcard (`*`) binding matches every authenticated principal, so an Unrestricted grant on it
            // would make everyone an operator and collapse tenant isolation. Demote its Unrestricted verbs to no-reach
            // (rule-bounded grants on a wildcard binding are still honoured) unless the deployment has opted in.
            bool demoteUnrestricted = !this.allowWildcardUnrestrictedReach && string.Equals(binding.ClaimTypeValue, "*", StringComparison.Ordinal);
            bindings.Add(new BindingClauses(
                binding.ClaimTypeValue,
                binding.ClaimValueOrNull,
                VerbClauseFor(binding.Read, expressions, demoteUnrestricted),
                VerbClauseFor(binding.Write, expressions, demoteUnrestricted),
                VerbClauseFor(binding.Purge, expressions, demoteUnrestricted),
                scopes,
                expiresAt));
        }

        this.compiled = new Compiled(snapshot.Generation, bindings, anyScopeGrants, anyExpiringBindings);
    }

    /// <inheritdoc/>
    public override AccessContext Resolve(ClaimsPrincipal? principal)
    {
        Compiled current = this.compiled;
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims = this.CollectClaims(principal);

        // Read the clock only when some binding is time-bound (the common case has none → no clock read).
        DateTimeOffset now = current.AnyExpiringBindings ? this.timeProvider.GetUtcNow() : default;

        // Bindings matching this principal and still in effect, in resolution order (an expired grant is excluded).
        var matched = new List<BindingClauses>();
        if (principal?.Identity?.IsAuthenticated == true)
        {
            foreach (BindingClauses binding in current.Bindings)
            {
                if (Matches(binding, principal) && !IsExpired(binding, now))
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

        // Elevated path (a binding grants a scope): union the granted scopes of every matched, in-effect binding.
        // Allocates a small list only when this principal actually holds a per-principal grant. The clock is read once
        // (and only when some binding is time-bound); an expired grant is excluded fail-safe.
        DateTimeOffset now = current.AnyExpiringBindings ? this.timeProvider.GetUtcNow() : default;
        List<string>? granted = null;
        foreach (BindingClauses binding in current.Bindings)
        {
            if (binding.Scopes.Length == 0 || !Matches(binding, principal) || IsExpired(binding, now))
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
    /// <remarks>The caller's own deployment-stamped identity (row creation + administration membership). When an ambient
    /// provider is configured, its <c>sys:</c> dimensions are strip-and-restamped onto the resolver's output — the same
    /// values a grantee resolved in this context carries, so membership set-equals; a no-op (and no allocation) when no
    /// provider is configured.</remarks>
    public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        => AmbientIdentityStamp.Apply(this.ambient, this.internalTagResolver?.Invoke(principal) ?? base.GetInternalTags(principal));

    /// <inheritdoc/>
    /// <remarks>The non-directory grantee resolution. When an ambient provider is configured, its dimensions are stamped
    /// onto the base single-tag identity from the same source the runtime caller is stamped from, so a free-typed grantee
    /// authored in a tenant context matches the caller in that context.</remarks>
    public override SecurityTagSet ResolveGranteeIdentity(GranteeKind kind, string value)
        => AmbientIdentityStamp.Apply(this.ambient, base.ResolveGranteeIdentity(kind, value));

    /// <inheritdoc/>
    public override void ValidateUserTags(SecurityTagSet userTags) => this.shell.ValidateUserTags(userTags);

    private IReadOnlyDictionary<string, IReadOnlyList<string>> CollectClaims(ClaimsPrincipal? principal)
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

        this.AddAmbientClaims(map);
        return map;
    }

    // Stamps the request-context ambient dimensions (§16.5.5) into the reach claim map, prefix-stripped to claim space
    // (sys:tenant -> the tenant claim) so a rule `sys:tenant == $claim.tenant` resolves the context tenant uniformly with
    // a token-sourced claim. The ambient value is AUTHORITATIVE: it replaces any token-supplied claim of the same name,
    // so a forged `tenant` claim cannot widen the context-derived reach (the trust boundary). A no-op (no extra entry)
    // when no provider is configured or the context resolves none. Ambient keys are distinct, so each resolves to its own
    // claim name with no accumulation needed.
    private void AddAmbientClaims(Dictionary<string, IReadOnlyList<string>> map)
    {
        if (this.ambient is null)
        {
            return;
        }

        AmbientDimensionSet set = this.ambient.Resolve();
        if (set.IsEmpty)
        {
            return;
        }

        string prefix = this.InternalTagPrefix;
        foreach (SecurityTag tag in set.Tags)
        {
            string claimName = tag.Key.StartsWith(prefix, StringComparison.Ordinal) ? tag.Key[prefix.Length..] : tag.Key;
            map[claimName] = [tag.Value];
        }
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
    // grant is empty or references an unknown rule (fail-closed: a broken grant contributes nothing). When
    // demoteUnrestricted is set (a wildcard binding without the F7 opt-in), an Unrestricted grant is downgraded to no
    // reach rather than full reach.
    private static VerbClause VerbClauseFor(VerbGrant grant, IReadOnlyDictionary<string, string> expressions, bool demoteUnrestricted)
    {
        if (grant.IsUnrestrictedValue)
        {
            return demoteUnrestricted ? new VerbClause(false, null) : new VerbClause(true, null);
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

    // A grant is expired (and excluded fail-safe) once now is at/after its expiry. A standing grant (no expiry) never
    // expires; when no binding is time-bound the caller passes now = default and this is always false (no clock read).
    private static bool IsExpired(BindingClauses binding, DateTimeOffset now)
        => binding.ExpiresAt is { } expiresAt && now >= expiresAt;

    // A binding's per-verb grant, pre-resolved at snapshot time: unrestricted (operator) or a (possibly null) clause.
    private readonly record struct VerbClause(bool Unrestricted, string? Clause);

    // A binding pre-resolved at snapshot time: its claim match, the three per-verb clauses, the granted scopes, and the
    // optional time-bound expiry.
    private sealed record BindingClauses(string ClaimType, string? ClaimValue, VerbClause Read, VerbClause Write, VerbClause Purge, string[] Scopes, DateTimeOffset? ExpiresAt = null);

    private sealed record Compiled(long Generation, IReadOnlyList<BindingClauses> Bindings, bool AnyScopeGrants = false, bool AnyExpiringBindings = false)
    {
        // Combined-expression -> compiled rule, scoped to this generation (discarded when the snapshot advances).
        public ConcurrentDictionary<string, SecurityRule[]> CompiledCombos { get; } = new(StringComparer.Ordinal);
    }
}