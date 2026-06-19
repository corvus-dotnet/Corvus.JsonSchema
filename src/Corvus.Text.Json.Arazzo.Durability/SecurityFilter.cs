// <copyright file="SecurityFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A principal's resolved row-authorization filter (design §14.2/§14.3): the set of <see cref="SecurityRule"/>s
/// that must all hold for a row (a run or catalog version) to be visible, together with the principal's claims
/// the rules resolve <c>$claim.*</c> operands against. The rule set is the deployment's mandated wrapper rule(s)
/// AND the principal's user rule — composed as a conjunction (defense in depth: a user rule can only narrow
/// within the deployment shell, never widen past it).
/// </summary>
/// <remarks>
/// <para>
/// A <see langword="null"/> filter on a query means unrestricted (an explicit full-reach / system credential —
/// see <see cref="AccessContext.System"/>); a non-null filter restricts the result set. A store applies the
/// filter to each candidate row's <see cref="SecurityTag"/> labels — in memory here, and as a translated indexed
/// predicate in the per-backend stores. A single-row read/write check uses <see cref="IsSatisfiedBy(in SecurityTagSet)"/> directly.
/// </para>
/// <para>
/// <b>Deny-by-default (fail-closed).</b> A non-null filter denies unless it positively admits a row: an
/// <b>empty rule set</b> (an under-specified / misconfigured filter) admits <b>nothing</b> — "no restriction" is
/// expressed by a <see langword="null"/> filter, never an empty one — and a row with <b>no security tags</b> (an
/// unclassified row) is admitted to <b>no</b> scoped principal. To make a row visible to scoped principals it
/// must be explicitly tagged; only the full-reach (<see langword="null"/>) credential sees untagged rows.
/// </para>
/// </remarks>
public sealed class SecurityFilter
{
    private readonly IReadOnlyList<SecurityRule> rules;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> claims;
    private readonly Utf8ClaimSet utf8Claims;

    /// <summary>Initializes a new instance of the <see cref="SecurityFilter"/> class.</summary>
    /// <param name="rules">The rules that must all hold (the deployment wrapper rule(s) plus the principal's user rule).</param>
    /// <param name="claims">The principal's claims (name → values) the rules resolve <c>$claim.*</c> operands against.</param>
    public SecurityFilter(IReadOnlyList<SecurityRule> rules, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(claims);
        this.rules = rules;
        this.claims = claims;

        // Encode the principal's claims to UTF-8 once (they are fixed for this filter's lifetime); every per-row scan
        // then compares the small claim set bytes-to-bytes against each row tag, never decoding a row tag to a string.
        this.utf8Claims = new Utf8ClaimSet(claims);
    }

    /// <summary>Whether the row's security tags satisfy every rule for this principal (deny-by-default) — the
    /// bytes-to-bytes path: the row's tags (a deferred <see cref="SecurityTagSet"/> holder) are parsed once and every
    /// rule is evaluated over the unescaped UTF-8, materialising no managed <see cref="SecurityTag"/> per row.</summary>
    /// <param name="securityTags">The row's security tags.</param>
    /// <returns><see langword="true"/> only if the filter positively admits the row: it has at least one rule, the
    /// row carries at least one security tag, and every rule holds. An empty rule set or an untagged row denies.</returns>
    public bool IsSatisfiedBy(in SecurityTagSet securityTags)
    {
        // Fail-closed: an under-specified filter (no rules) or an unclassified row (no tags) admits nothing.
        if (this.rules.Count == 0 || securityTags.IsEmpty)
        {
            return false;
        }

        return SecurityRule.EvaluateAll(this.rules, securityTags, in this.utf8Claims);
    }

    /// <summary>Whether the row's security tags (as a materialised tag list) satisfy every rule (deny-by-default). A
    /// convenience over the deferred-holder path for callers (and tests) that already hold a list; it delegates to the
    /// same bytes-to-bytes evaluator via <see cref="SecurityTagSet.FromTags"/>.</summary>
    /// <param name="securityTags">The row's security-tag labels.</param>
    /// <returns><see langword="true"/> only if the filter positively admits the row.</returns>
    public bool IsSatisfiedBy(IReadOnlyList<SecurityTag> securityTags)
    {
        ArgumentNullException.ThrowIfNull(securityTags);
        return this.IsSatisfiedBy(SecurityTagSet.FromTags(securityTags));
    }

    /// <summary>
    /// Translates the filter into a SQL <c>WHERE</c> boolean fragment (design §14.4) selecting exactly the rows
    /// <see cref="IsSatisfiedBy(in SecurityTagSet)"/> would admit — using the backend's dialect/schema fragments. Deny-by-default: an
    /// empty filter (no rules) selects nothing (<see cref="ISecurityRuleSqlEmitter.FalseLiteral"/>), and the
    /// conjunction of the rules' predicates is further guarded by <see cref="ISecurityRuleSqlEmitter.ExistsAnyTag"/>
    /// so an untagged row is never selected.
    /// </summary>
    /// <param name="emitter">The backend's SQL fragment provider (stateful per query; accumulates bound parameters).</param>
    /// <returns>A boolean SQL fragment.</returns>
    public string ToSqlPredicate(ISecurityRuleSqlEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);

        // Fail-closed: an under-specified filter (no rules) selects nothing.
        if (this.rules.Count == 0)
        {
            return emitter.FalseLiteral;
        }

        // Deny-by-default for unclassified rows: every rule must hold AND the row must carry at least one tag.
        string predicate = emitter.ExistsAnyTag();
        foreach (SecurityRule rule in this.rules)
        {
            predicate = emitter.AndAlso(predicate, rule.ToSqlPredicate(emitter, this.claims));
        }

        return predicate;
    }
}