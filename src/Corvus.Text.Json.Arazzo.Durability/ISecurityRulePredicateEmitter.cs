// <copyright file="ISecurityRulePredicateEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The fragments a backend supplies so a <see cref="SecurityRule"/> / <see cref="SecurityFilter"/> can be
/// translated into a predicate its own query engine understands (design §14.4, "true indexed WHERE") — a SQL
/// <c>WHERE</c> clause, a document-database filter, or a plan over a secondary index. The security-critical AST
/// walk lives once in <see cref="SecurityRule"/>; a backend provides only how to spell an operand and the
/// "row has a tag like this" tests against <em>its</em> storage of the security tags, so a translation bug is
/// confined to small, reviewable fragments rather than re-implementing the rule semantics per backend.
/// </summary>
/// <remarks>
/// <para>
/// An implementation is <b>stateful per query</b>: <see cref="Parameter"/> registers an operand value and returns
/// the token to refer to it by. A backend that binds parameters (SQL) returns a placeholder and accumulates the
/// value for the caller to bind; a backend whose filters carry values directly returns the value itself.
/// </para>
/// <para>
/// <typeparamref name="TPredicate"/> is the backend's boolean fragment type. It is a string for a SQL dialect, and
/// a native filter object for a backend whose query language is not text — the latter matters, because building a
/// query language by concatenating claim-derived strings is both an injection surface and an allocation on the
/// query path.
/// </para>
/// </remarks>
/// <typeparam name="TPredicate">The backend's boolean fragment type.</typeparam>
public interface ISecurityRulePredicateEmitter<TPredicate>
{
    /// <summary>Gets a predicate that is always true.</summary>
    TPredicate TrueLiteral { get; }

    /// <summary>Gets a predicate that is always false.</summary>
    TPredicate FalseLiteral { get; }

    /// <summary>Registers an operand value and returns the token the fragment methods should refer to it by.</summary>
    /// <param name="value">The value to bind.</param>
    /// <returns>The token — a bound-parameter placeholder for a dialect that binds, otherwise the value itself.</returns>
    string Parameter(string value);

    /// <summary>Builds "the current row has at least one security tag" — the deny-by-default guard (§14.2): an
    /// untagged (unclassified) row is never admitted to a reach-restricted principal, mirroring
    /// <see cref="SecurityFilter.IsSatisfiedBy(in SecurityTagSet)"/>.</summary>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsAnyTag();

    /// <summary>Builds "the current row has a security tag whose key is <paramref name="keyToken"/>".</summary>
    /// <param name="keyToken">A token bound to the tag key.</param>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsTagKey(string keyToken);

    /// <summary>Builds "the current row has a security tag (<paramref name="keyToken"/>, v) where v is one of <paramref name="valueTokens"/>".</summary>
    /// <param name="keyToken">A token bound to the tag key.</param>
    /// <param name="valueTokens">Tokens bound to the candidate values (never empty — the caller handles the empty case).</param>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsTagValueIn(string keyToken, IReadOnlyList<string> valueTokens);

    /// <summary>
    /// Builds "the current row has at least one security tag with key <paramref name="keyToken"/> <b>and every</b>
    /// such tag's value is one of <paramref name="valueTokens"/>" — the ordered-comparison admissible-set guard
    /// (<c>&lt;</c>/<c>&lt;=</c>/<c>&gt;</c>/<c>&gt;=</c>, design §14.2): the value set is the dimension's labels whose
    /// rank satisfies the bound, so the row is admitted only when the dimension is present and <em>none</em> of its
    /// values falls outside the bound (an out-of-range or unranked value denies). Distinct from
    /// <see cref="ExistsTagValueIn"/> (which is "<em>some</em> value in the set", i.e. <c>==</c>/<c>in</c>).
    /// </summary>
    /// <remarks>
    /// A backend answering this from a secondary index must model "every value is admissible" directly rather than by
    /// enumerating the ordering's labels and excluding the rest: a row carrying a value the ordering does not rank at
    /// all must deny, and it appears in no admissible-label index entry, so an implementation built only from the
    /// admissible entries would silently admit it.
    /// </remarks>
    /// <param name="keyToken">A token bound to the tag key (the ordered dimension).</param>
    /// <param name="valueTokens">Tokens bound to the admissible values (never empty — the caller emits <see cref="FalseLiteral"/> for an empty admissible set).</param>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsTagAllValuesIn(string keyToken, IReadOnlyList<string> valueTokens);

    /// <summary>Builds "the current row has a tag with key <paramref name="keyToken1"/> and a tag with key <paramref name="keyToken2"/> sharing a value".</summary>
    /// <param name="keyToken1">A token bound to the first tag key.</param>
    /// <param name="keyToken2">A token bound to the second tag key.</param>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsTagKeysShareValue(string keyToken1, string keyToken2);

    /// <summary>
    /// Builds the ABAC superset predicate (<c>$claims.superset</c>, §14.2): the current row has <b>no</b> security
    /// tag left uncovered by the principal's claims — i.e. for every row tag (k, v) some supplied claim entry has
    /// key token equal to k and a value token equal to v.
    /// </summary>
    /// <param name="claimEntries">The principal's claims as (keyToken, valueTokens) pairs, each with at
    /// least one value. An empty list means the principal has no claims — no tag can be covered, so the predicate
    /// must hold only for an untagged row (which the filter denies anyway via <see cref="ExistsAnyTag"/>).</param>
    /// <returns>A boolean fragment.</returns>
    TPredicate ExistsAllTagsCovered(IReadOnlyList<(string KeyToken, IReadOnlyList<string> ValueTokens)> claimEntries);

    /// <summary>Negates a boolean fragment.</summary>
    /// <param name="predicate">The fragment.</param>
    /// <returns><c>NOT (predicate)</c>.</returns>
    TPredicate Negate(TPredicate predicate);

    /// <summary>Conjoins two boolean fragments.</summary>
    /// <param name="left">The left fragment.</param>
    /// <param name="right">The right fragment.</param>
    /// <returns><c>(left AND right)</c>.</returns>
    TPredicate AndAlso(TPredicate left, TPredicate right);

    /// <summary>Disjoins two boolean fragments.</summary>
    /// <param name="left">The left fragment.</param>
    /// <param name="right">The right fragment.</param>
    /// <returns><c>(left OR right)</c>.</returns>
    TPredicate OrElse(TPredicate left, TPredicate right);
}