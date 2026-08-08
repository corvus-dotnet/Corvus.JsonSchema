// <copyright file="MongoSecurityRuleEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// An <see cref="ISecurityRulePredicateEmitter{TPredicate}"/> for MongoDB (design §14.4): the reach predicate is a
/// native <see cref="FilterDefinition{TDocument}"/> over the document's embedded security-tag array (each element
/// <c>{ "k": …, "v": … }</c>), so a reach-filtered list is one indexed server-side query rather than a scan whose
/// rows the client discards.
/// </summary>
/// <remarks>
/// <para>
/// Mongo's query language is BSON, not text, so <see cref="Parameter"/> returns the value itself and every operand
/// reaches the server as a BSON value in the filter. Nothing is concatenated, and the emitter holds no per-query
/// state — one instance is immutable, thread-safe and reusable across queries, which is why the stores keep a
/// single static one rather than allocating per call.
/// </para>
/// <para>
/// The predicates are shaped to use a compound multikey index on <c>{ &lt;array&gt;.&lt;key&gt;, &lt;array&gt;.&lt;value&gt; }</c>:
/// a single-condition test is a dotted-path equality and a same-element (key, value) test is an
/// <c>$elemMatch</c>, both of which the planner can answer from that index.
/// <see cref="ExistsTagKeysShareValue"/> is the exception — a self-join on value has no query-language form, so it
/// is an <c>$expr</c> aggregation predicate the server evaluates per candidate document. It is still server-side
/// (no row crosses the wire that the principal cannot see) but it is not index-backed.
/// </para>
/// </remarks>
/// <param name="arrayField">The document field holding the security-tag array (e.g. <c>securityTags</c>).</param>
/// <param name="keyProperty">The key property name on each array element (e.g. <c>k</c>).</param>
/// <param name="valueProperty">The value property name on each array element (e.g. <c>v</c>).</param>
public sealed class MongoSecurityRuleEmitter(string arrayField, string keyProperty, string valueProperty)
    : ISecurityRulePredicateEmitter<FilterDefinition<BsonDocument>>
{
    private static readonly FilterDefinitionBuilder<BsonDocument> Filter = Builders<BsonDocument>.Filter;

    /// <inheritdoc/>
    public FilterDefinition<BsonDocument> TrueLiteral => Filter.Empty;

    /// <inheritdoc/>
    /// <remarks><c>$nor</c> of the match-everything predicate: the one filter that selects no document.</remarks>
    public FilterDefinition<BsonDocument> FalseLiteral => new BsonDocument("$nor", new BsonArray { new BsonDocument() });

    private string KeyPath => arrayField + "." + keyProperty;

    /// <inheritdoc/>
    /// <remarks>A BSON filter carries its operands as values, so the token IS the value.</remarks>
    public string Parameter(string value) => value;

    /// <inheritdoc/>
    /// <remarks>
    /// Tested as "the array has a zeroth element" rather than "the field is not null", so it holds for exactly the
    /// documents that carry a tag: an untagged row stores <see cref="BsonNull"/> (see <c>MongoSecurityTags</c>),
    /// and a null field has no indexable element at position zero.
    /// </remarks>
    public FilterDefinition<BsonDocument> ExistsAnyTag() => Filter.Exists(arrayField + ".0");

    /// <inheritdoc/>
    public FilterDefinition<BsonDocument> ExistsTagKey(string keyToken) => Filter.Eq(this.KeyPath, keyToken);

    /// <inheritdoc/>
    /// <remarks>The key and the value must match the SAME array element, which is what <c>$elemMatch</c> means and
    /// what a pair of dotted-path conditions would not.</remarks>
    public FilterDefinition<BsonDocument> ExistsTagValueIn(string keyToken, IReadOnlyList<string> valueTokens)
        => this.ElemMatch(new BsonDocument
        {
            [keyProperty] = keyToken,
            [valueProperty] = new BsonDocument("$in", new BsonArray(valueTokens)),
        });

    /// <inheritdoc/>
    /// <remarks>
    /// "The dimension is present, and no tag of that dimension falls outside the admissible set" — the second
    /// conjunct is written as the absence of an inadmissible value (<c>$nin</c>) rather than as the presence of an
    /// admissible one. That is what makes an <b>unranked</b> value deny: it is in no admissible-value list, so it
    /// matches <c>$nin</c>, so the negated <c>$elemMatch</c> excludes the row.
    /// </remarks>
    public FilterDefinition<BsonDocument> ExistsTagAllValuesIn(string keyToken, IReadOnlyList<string> valueTokens)
        => Filter.And(
            Filter.Eq(this.KeyPath, keyToken),
            Filter.Not(this.ElemMatch(new BsonDocument
            {
                [keyProperty] = keyToken,
                [valueProperty] = new BsonDocument("$nin", new BsonArray(valueTokens)),
            })));

    /// <inheritdoc/>
    /// <remarks>
    /// A self-join on the tag value, which the query language cannot express, so this is an <c>$expr</c> over the
    /// two keys' value sets. The array is wrapped in <c>$ifNull</c> because an untagged row stores
    /// <see cref="BsonNull"/> and <c>$filter</c> raises on a non-array input rather than yielding nothing.
    /// </remarks>
    public FilterDefinition<BsonDocument> ExistsTagKeysShareValue(string keyToken1, string keyToken2)
        => new BsonDocument(
            "$expr",
            new BsonDocument("$gt", new BsonArray
            {
                new BsonDocument("$size", new BsonDocument("$setIntersection", new BsonArray
                {
                    this.ValuesOfKey(keyToken1),
                    this.ValuesOfKey(keyToken2),
                })),
                0,
            }));

    /// <inheritdoc/>
    /// <remarks>
    /// Read as the absence of a counterexample: the row has no tag that fails to match any of the principal's
    /// (key, values) claim entries. With no claim entries there is no way to cover a tag, so the predicate reduces
    /// to "the row has no tags at all".
    /// </remarks>
    public FilterDefinition<BsonDocument> ExistsAllTagsCovered(IReadOnlyList<(string KeyToken, IReadOnlyList<string> ValueTokens)> claimEntries)
    {
        ArgumentNullException.ThrowIfNull(claimEntries);

        if (claimEntries.Count == 0)
        {
            return this.Negate(this.ExistsAnyTag());
        }

        var covered = new BsonArray(claimEntries.Count);
        foreach ((string keyToken, IReadOnlyList<string> valueTokens) in claimEntries)
        {
            covered.Add(new BsonDocument
            {
                [keyProperty] = keyToken,
                [valueProperty] = new BsonDocument("$in", new BsonArray(valueTokens)),
            });
        }

        return this.Negate(this.ElemMatch(new BsonDocument("$nor", covered)));
    }

    /// <inheritdoc/>
    public FilterDefinition<BsonDocument> Negate(FilterDefinition<BsonDocument> predicate) => Filter.Not(predicate);

    /// <inheritdoc/>
    public FilterDefinition<BsonDocument> AndAlso(FilterDefinition<BsonDocument> left, FilterDefinition<BsonDocument> right) => Filter.And(left, right);

    /// <inheritdoc/>
    public FilterDefinition<BsonDocument> OrElse(FilterDefinition<BsonDocument> left, FilterDefinition<BsonDocument> right) => Filter.Or(left, right);

    private FilterDefinition<BsonDocument> ElemMatch(BsonDocument condition)
        => new BsonDocument(arrayField, new BsonDocument("$elemMatch", condition));

    // The values the row carries for one tag key, as an aggregation expression. The key is wrapped in $literal:
    // inside $expr a bare string beginning with '$' is a FIELD PATH, so a tag key that happened to start with '$'
    // would otherwise be evaluated as a reference into the document rather than compared as a value.
    private BsonDocument ValuesOfKey(string keyToken)
        => new("$map", new BsonDocument
        {
            ["input"] = new BsonDocument("$filter", new BsonDocument
            {
                ["input"] = new BsonDocument("$ifNull", new BsonArray { "$" + arrayField, new BsonArray() }),
                ["as"] = "tag",
                ["cond"] = new BsonDocument("$eq", new BsonArray
                {
                    "$$tag." + keyProperty,
                    new BsonDocument("$literal", keyToken),
                }),
            }),
            ["as"] = "tag",
            ["in"] = "$$tag." + valueProperty,
        });
}