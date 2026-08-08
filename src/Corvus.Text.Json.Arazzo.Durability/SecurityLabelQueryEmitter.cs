// <copyright file="SecurityLabelQueryEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Translates a <see cref="SecurityRule"/> / <see cref="SecurityFilter"/> into a
/// <see cref="SecurityLabelQuery"/> — the plan a backend resolves against its security-label index (design
/// §14.4). This is the shared translation for every backend that cannot express the rule grammar in its own
/// query language: Azure Table Storage (OData over flat properties, no quantifiers), Redis and NATS (no query
/// language over row contents at all).
/// </summary>
/// <remarks>
/// <para>
/// Every fragment must denote a <b>superset</b> of the rows the rule admits. Three of them cannot be exact, and
/// each is widened to the tightest sound plan rather than to <see cref="SecurityLabelQuery.Universe"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="ExistsTagAllValuesIn"/> is "the key is present AND every value of it is admissible"; the
/// index can answer the first conjunct, and rows satisfying the whole are a subset of those, so
/// <see cref="SecurityLabelQuery.AnyValueOfKey"/> is sound and still narrows.</item>
/// <item><see cref="ExistsTagKeysShareValue"/> requires both keys present, so the intersection of the two
/// key sets is sound and still narrows.</item>
/// <item><see cref="ExistsAllTagsCovered"/> is satisfied by an <em>untagged</em> row, which no label entry
/// mentions, so there is nothing to narrow to.</item>
/// </list>
/// <para>
/// <see cref="Negate"/> resolves to <see cref="SecurityLabelQuery.Universe"/> unconditionally. That is the one
/// rule keeping the widenings above safe: negating a widened set narrows the result, which would hide rows the
/// principal is entitled to see, and this makes the situation unrepresentable rather than merely avoided.
/// </para>
/// <para>
/// <see cref="Parameter"/> is the identity, because a plan carries operand values directly.
/// The emitter holds no per-query state, so a single instance is reusable and thread-safe.
/// </para>
/// </remarks>
public sealed class SecurityLabelQueryEmitter : ISecurityRulePredicateEmitter<SecurityLabelQuery>
{
    /// <summary>Gets the shared instance. The emitter is immutable, so every caller can use it.</summary>
    public static SecurityLabelQueryEmitter Instance { get; } = new();

    /// <inheritdoc/>
    public SecurityLabelQuery TrueLiteral => SecurityLabelQuery.Universe;

    /// <inheritdoc/>
    public SecurityLabelQuery FalseLiteral => SecurityLabelQuery.Empty;

    /// <inheritdoc/>
    public string Parameter(string value) => value;

    /// <inheritdoc/>
    /// <remarks>Every tagged row carries some label, so the index cannot narrow this. The caller's exact
    /// evaluation still denies the untagged rows, which is where that guard actually bites.</remarks>
    public SecurityLabelQuery ExistsAnyTag() => SecurityLabelQuery.Universe;

    /// <inheritdoc/>
    public SecurityLabelQuery ExistsTagKey(string keyToken) => SecurityLabelQuery.AnyValueOfKey(keyToken);

    /// <inheritdoc/>
    public SecurityLabelQuery ExistsTagValueIn(string keyToken, IReadOnlyList<string> valueTokens)
    {
        ArgumentNullException.ThrowIfNull(valueTokens);

        SecurityLabelQuery query = SecurityLabelQuery.Empty;
        foreach (string value in valueTokens)
        {
            query = SecurityLabelQuery.Union(query, SecurityLabelQuery.Label(keyToken, value));
        }

        return query;
    }

    /// <inheritdoc/>
    public SecurityLabelQuery ExistsTagAllValuesIn(string keyToken, IReadOnlyList<string> valueTokens)
        => SecurityLabelQuery.AnyValueOfKey(keyToken);

    /// <inheritdoc/>
    public SecurityLabelQuery ExistsTagKeysShareValue(string keyToken1, string keyToken2)
        => SecurityLabelQuery.Intersect(SecurityLabelQuery.AnyValueOfKey(keyToken1), SecurityLabelQuery.AnyValueOfKey(keyToken2));

    /// <inheritdoc/>
    public SecurityLabelQuery ExistsAllTagsCovered(IReadOnlyList<(string KeyToken, IReadOnlyList<string> ValueTokens)> claimEntries)
        => SecurityLabelQuery.Universe;

    /// <inheritdoc/>
    public SecurityLabelQuery Negate(SecurityLabelQuery predicate) => SecurityLabelQuery.Universe;

    /// <inheritdoc/>
    public SecurityLabelQuery AndAlso(SecurityLabelQuery left, SecurityLabelQuery right) => SecurityLabelQuery.Intersect(left, right);

    /// <inheritdoc/>
    public SecurityLabelQuery OrElse(SecurityLabelQuery left, SecurityLabelQuery right) => SecurityLabelQuery.Union(left, right);
}