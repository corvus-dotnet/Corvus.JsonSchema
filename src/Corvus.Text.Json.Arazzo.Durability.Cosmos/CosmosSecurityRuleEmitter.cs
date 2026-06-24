// <copyright file="CosmosSecurityRuleEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An <see cref="ISecurityRuleSqlEmitter"/> for the Cosmos DB SQL (core) API (design §14.4). Unlike the
/// relational backends — which correlate to a child tag table — a Cosmos document embeds its security tags as an
/// array property (<c>c.securityTags</c>, each element <c>{ "k": …, "v": … }</c>), so the reach predicate is a
/// native <c>EXISTS</c> over an array sub-query. Every value flows through the supplied <c>parameter</c> delegate
/// (which records the value and returns its <c>@p…</c> placeholder), so nothing the caller supplies is ever
/// concatenated into the query text — only the fixed property paths are.
/// </summary>
/// <param name="arrayPath">The document path of the security-tag array (e.g. <c>c.securityTags</c>).</param>
/// <param name="keyProperty">The key property name on each array element (e.g. <c>k</c>).</param>
/// <param name="valueProperty">The value property name on each array element (e.g. <c>v</c>).</param>
/// <param name="parameter">Records a value and returns its query placeholder (e.g. <c>@p0</c>).</param>
public sealed class CosmosSecurityRuleEmitter(string arrayPath, string keyProperty, string valueProperty, Func<string, string> parameter)
    : ISecurityRuleSqlEmitter
{
    /// <inheritdoc/>
    public string TrueLiteral => "true";

    /// <inheritdoc/>
    public string FalseLiteral => "false";

    /// <inheritdoc/>
    public string Parameter(string value) => parameter(value);

    /// <inheritdoc/>
    public string ExistsAnyTag()
        => $"EXISTS (SELECT VALUE st FROM st IN {arrayPath})";

    /// <inheritdoc/>
    public string ExistsTagKey(string keyPlaceholder)
        => $"EXISTS (SELECT VALUE st FROM st IN {arrayPath} WHERE st.{keyProperty} = {keyPlaceholder})";

    /// <inheritdoc/>
    public string ExistsTagValueIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders)
        => $"EXISTS (SELECT VALUE st FROM st IN {arrayPath} WHERE st.{keyProperty} = {keyPlaceholder} AND st.{valueProperty} IN ({string.Join(", ", valuePlaceholders)}))";

    /// <inheritdoc/>
    public string ExistsTagAllValuesIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders)
        => $"(EXISTS (SELECT VALUE st FROM st IN {arrayPath} WHERE st.{keyProperty} = {keyPlaceholder}) " +
           $"AND NOT EXISTS (SELECT VALUE st FROM st IN {arrayPath} WHERE st.{keyProperty} = {keyPlaceholder} AND st.{valueProperty} NOT IN ({string.Join(", ", valuePlaceholders)})))";

    /// <inheritdoc/>
    public string ExistsAllTagsCovered(IReadOnlyList<(string KeyPlaceholder, IReadOnlyList<string> ValuePlaceholders)> claimEntries)
    {
        if (claimEntries.Count == 0)
        {
            return $"NOT EXISTS (SELECT VALUE st FROM st IN {arrayPath})";
        }

        string covered = string.Join(
            " OR ",
            claimEntries.Select(e => $"(st.{keyProperty} = {e.KeyPlaceholder} AND st.{valueProperty} IN ({string.Join(", ", e.ValuePlaceholders)}))"));

        return $"NOT EXISTS (SELECT VALUE st FROM st IN {arrayPath} WHERE NOT ({covered}))";
    }

    /// <inheritdoc/>
    public string ExistsTagKeysShareValue(string keyPlaceholder1, string keyPlaceholder2)
        => $"EXISTS (SELECT VALUE st1 FROM st1 IN {arrayPath} JOIN st2 IN {arrayPath} " +
           $"WHERE st1.{keyProperty} = {keyPlaceholder1} AND st2.{keyProperty} = {keyPlaceholder2} AND st1.{valueProperty} = st2.{valueProperty})";

    /// <inheritdoc/>
    public string Negate(string predicate) => $"NOT ({predicate})";

    /// <inheritdoc/>
    public string AndAlso(string left, string right) => $"({left} AND {right})";

    /// <inheritdoc/>
    public string OrElse(string left, string right) => $"({left} OR {right})";
}