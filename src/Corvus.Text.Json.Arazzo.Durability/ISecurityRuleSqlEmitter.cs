// <copyright file="ISecurityRuleSqlEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The dialect/schema fragments a backend supplies so a <see cref="SecurityRule"/> / <see cref="SecurityFilter"/>
/// can be translated into a SQL <c>WHERE</c> predicate (design §14.4, "true indexed WHERE"). The security-critical
/// AST walk lives once in <see cref="SecurityRule"/>; a backend only provides how to spell a bound parameter and
/// the correlated <c>EXISTS</c> subqueries against <em>its</em> security-tag table — so a translation bug is
/// confined to small, reviewable fragments rather than re-implementing the rule semantics per backend.
/// </summary>
/// <remarks>
/// An implementation is <b>stateful per query</b>: <see cref="Parameter"/> accumulates bound values (returning the
/// dialect placeholder), which the caller binds to the command after building the predicate. The correlated
/// subqueries reference the outer row of the query the predicate is appended to (e.g. <c>WorkflowRuns.RunId</c>).
/// </remarks>
public interface ISecurityRuleSqlEmitter
{
    /// <summary>A SQL literal that is always true (e.g. <c>1=1</c>).</summary>
    string TrueLiteral { get; }

    /// <summary>A SQL literal that is always false (e.g. <c>1=0</c>).</summary>
    string FalseLiteral { get; }

    /// <summary>Registers a bound parameter value and returns its dialect placeholder (e.g. <c>@s0</c>).</summary>
    /// <param name="value">The value to bind.</param>
    /// <returns>The placeholder to use in the SQL.</returns>
    string Parameter(string value);

    /// <summary>Builds "the current row has at least one security tag" — the deny-by-default guard (§14.2): an
    /// untagged (unclassified) row is never admitted to a reach-restricted principal, mirroring
    /// <see cref="SecurityFilter.IsSatisfiedBy(in SecurityTagSet)"/>.</summary>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsAnyTag();

    /// <summary>Builds "the current row has a security tag whose key is <paramref name="keyPlaceholder"/>".</summary>
    /// <param name="keyPlaceholder">A placeholder bound to the tag key.</param>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsTagKey(string keyPlaceholder);

    /// <summary>Builds "the current row has a security tag (<paramref name="keyPlaceholder"/>, v) where v is one of <paramref name="valuePlaceholders"/>".</summary>
    /// <param name="keyPlaceholder">A placeholder bound to the tag key.</param>
    /// <param name="valuePlaceholders">Placeholders bound to the candidate values (never empty — the caller handles the empty case).</param>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsTagValueIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders);

    /// <summary>
    /// Builds "the current row has at least one security tag with key <paramref name="keyPlaceholder"/> <b>and every</b>
    /// such tag's value is one of <paramref name="valuePlaceholders"/>" — the ordered-comparison admissible-set guard
    /// (<c>&lt;</c>/<c>&lt;=</c>/<c>&gt;</c>/<c>&gt;=</c>, design §14.2): the value set is the dimension's labels whose
    /// rank satisfies the bound, so the row is admitted only when the dimension is present and <em>none</em> of its
    /// values falls outside the bound (an out-of-range or unranked value denies). Distinct from
    /// <see cref="ExistsTagValueIn"/> (which is "<em>some</em> value in the set", i.e. <c>==</c>/<c>in</c>).
    /// </summary>
    /// <param name="keyPlaceholder">A placeholder bound to the tag key (the ordered dimension).</param>
    /// <param name="valuePlaceholders">Placeholders bound to the admissible values (never empty — the caller emits <see cref="FalseLiteral"/> for an empty admissible set).</param>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsTagAllValuesIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders);

    /// <summary>Builds "the current row has a tag with key <paramref name="keyPlaceholder1"/> and a tag with key <paramref name="keyPlaceholder2"/> sharing a value".</summary>
    /// <param name="keyPlaceholder1">A placeholder bound to the first tag key.</param>
    /// <param name="keyPlaceholder2">A placeholder bound to the second tag key.</param>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsTagKeysShareValue(string keyPlaceholder1, string keyPlaceholder2);

    /// <summary>
    /// Builds the ABAC superset predicate (<c>$claims.superset</c>, §14.2): the current row has <b>no</b> security
    /// tag left uncovered by the principal's claims — i.e. for every row tag (k, v) some supplied claim entry has
    /// key placeholder equal to k and a value placeholder equal to v.
    /// </summary>
    /// <param name="claimEntries">The principal's claims as (keyPlaceholder, valuePlaceholders) pairs, each with at
    /// least one value. An empty list means the principal has no claims — no tag can be covered, so the predicate
    /// must hold only for an untagged row (which the filter denies anyway via <see cref="ExistsAnyTag"/>).</param>
    /// <returns>A boolean SQL fragment.</returns>
    string ExistsAllTagsCovered(IReadOnlyList<(string KeyPlaceholder, IReadOnlyList<string> ValuePlaceholders)> claimEntries);

    /// <summary>Negates a boolean fragment.</summary>
    /// <param name="predicate">The fragment.</param>
    /// <returns><c>NOT (predicate)</c>.</returns>
    string Negate(string predicate);

    /// <summary>Conjoins two boolean fragments.</summary>
    /// <param name="left">The left fragment.</param>
    /// <param name="right">The right fragment.</param>
    /// <returns><c>(left AND right)</c>.</returns>
    string AndAlso(string left, string right);

    /// <summary>Disjoins two boolean fragments.</summary>
    /// <param name="left">The left fragment.</param>
    /// <param name="right">The right fragment.</param>
    /// <returns><c>(left OR right)</c>.</returns>
    string OrElse(string left, string right);
}