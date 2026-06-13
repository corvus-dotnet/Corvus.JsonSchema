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
/// A <see langword="null"/> filter on a query means unrestricted (e.g. an administrative principal or a
/// trusted-network deployment); a non-null filter restricts the result set. A store applies the filter to each
/// candidate row's <see cref="SecurityTag"/> labels — in memory here, and as a translated indexed predicate in
/// the per-backend stores. A single-row read/write check uses <see cref="IsSatisfiedBy"/> directly.
/// </remarks>
public sealed class SecurityFilter
{
    private readonly IReadOnlyList<SecurityRule> rules;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> claims;

    /// <summary>Initializes a new instance of the <see cref="SecurityFilter"/> class.</summary>
    /// <param name="rules">The rules that must all hold (the deployment wrapper rule(s) plus the principal's user rule).</param>
    /// <param name="claims">The principal's claims (name → values) the rules resolve <c>$claim.*</c> operands against.</param>
    public SecurityFilter(IReadOnlyList<SecurityRule> rules, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(claims);
        this.rules = rules;
        this.claims = claims;
    }

    /// <summary>Whether the row's security tags satisfy every rule for this principal.</summary>
    /// <param name="securityTags">The row's security-tag labels.</param>
    /// <returns><see langword="true"/> if the row is visible to the principal.</returns>
    public bool IsSatisfiedBy(IReadOnlyList<SecurityTag> securityTags)
    {
        ArgumentNullException.ThrowIfNull(securityTags);
        foreach (SecurityRule rule in this.rules)
        {
            if (!rule.IsSatisfiedBy(securityTags, this.claims))
            {
                return false;
            }
        }

        return true;
    }
}