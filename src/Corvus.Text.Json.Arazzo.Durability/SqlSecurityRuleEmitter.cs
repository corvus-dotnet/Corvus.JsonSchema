// <copyright file="SqlSecurityRuleEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A reusable <see cref="ISecurityRuleSqlEmitter"/> for relational backends (design §14.4): the
/// <c>EXISTS</c>/<c>IN</c>/<c>AND</c>/<c>OR</c>/<c>NOT</c> fragments are standard SQL shared across SQLite,
/// Postgres, MySQL and SQL Server, correlated to the outer query's row via a child security-tag table whose owner
/// columns mirror the parent's key (so it works for a single-column key like <c>RunId</c> and a composite key
/// like <c>(BaseWorkflowId, VersionNumber)</c>). Only the parameter-placeholder spelling differs per dialect,
/// which the caller supplies through the <c>parameter</c> delegate (it binds the value and returns the
/// placeholder, e.g. <c>@s0</c> or <c>$1</c>).
/// </summary>
public sealed class SqlSecurityRuleEmitter : ISecurityRuleSqlEmitter
{
    private readonly string tagTable;
    private readonly IReadOnlyList<string> ownerColumns;
    private readonly string keyColumn;
    private readonly string valueColumn;
    private readonly string outerReference;
    private readonly Func<string, string> parameter;

    /// <summary>Initializes a new instance of the <see cref="SqlSecurityRuleEmitter"/> class.</summary>
    /// <param name="tagTable">The child table holding one row per security tag: the owner columns plus the key/value columns.</param>
    /// <param name="ownerColumns">The columns (in both the tag table and the outer row) that identify the owning row — e.g. <c>["RunId"]</c> or <c>["BaseWorkflowId", "VersionNumber"]</c>.</param>
    /// <param name="keyColumn">The tag-key column name in <paramref name="tagTable"/> (e.g. <c>TagKey</c> or <c>tag_key</c>).</param>
    /// <param name="valueColumn">The tag-value column name in <paramref name="tagTable"/> (e.g. <c>TagValue</c> or <c>tag_value</c>).</param>
    /// <param name="outerReference">The outer query's table name or alias to qualify the owner columns against (e.g. <c>WorkflowRuns</c>).</param>
    /// <param name="parameter">Binds a value to the command and returns its dialect placeholder.</param>
    public SqlSecurityRuleEmitter(string tagTable, IReadOnlyList<string> ownerColumns, string keyColumn, string valueColumn, string outerReference, Func<string, string> parameter)
    {
        ArgumentException.ThrowIfNullOrEmpty(tagTable);
        ArgumentNullException.ThrowIfNull(ownerColumns);
        ArgumentOutOfRangeException.ThrowIfZero(ownerColumns.Count);
        ArgumentException.ThrowIfNullOrEmpty(keyColumn);
        ArgumentException.ThrowIfNullOrEmpty(valueColumn);
        ArgumentException.ThrowIfNullOrEmpty(outerReference);
        ArgumentNullException.ThrowIfNull(parameter);
        this.tagTable = tagTable;
        this.ownerColumns = ownerColumns;
        this.keyColumn = keyColumn;
        this.valueColumn = valueColumn;
        this.outerReference = outerReference;
        this.parameter = parameter;
    }

    /// <inheritdoc/>
    public string TrueLiteral => "1 = 1";

    /// <inheritdoc/>
    public string FalseLiteral => "1 = 0";

    /// <inheritdoc/>
    public string Parameter(string value) => this.parameter(value);

    /// <inheritdoc/>
    public string ExistsAnyTag()
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")})";

    /// <inheritdoc/>
    public string ExistsTagKey(string keyPlaceholder)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")} AND st.{this.keyColumn} = {keyPlaceholder})";

    /// <inheritdoc/>
    public string ExistsTagValueIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")} AND st.{this.keyColumn} = {keyPlaceholder} AND st.{this.valueColumn} IN ({string.Join(", ", valuePlaceholders)}))";

    /// <inheritdoc/>
    public string ExistsTagAllValuesIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders)
        => $"(EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")} AND st.{this.keyColumn} = {keyPlaceholder}) " +
           $"AND NOT EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")} AND st.{this.keyColumn} = {keyPlaceholder} AND st.{this.valueColumn} NOT IN ({string.Join(", ", valuePlaceholders)})))";

    /// <inheritdoc/>
    public string ExistsAllTagsCovered(IReadOnlyList<(string KeyPlaceholder, IReadOnlyList<string> ValuePlaceholders)> claimEntries)
    {
        // No claims → no tag can be covered, so "no uncovered tag" reduces to "the row has no tag at all".
        // Emitting this (rather than a true literal) keeps the rule-level SQL equal to the in-memory evaluator
        // on a tagged row; the filter's ExistsAnyTag guard denies the untagged row on both paths.
        if (claimEntries.Count == 0)
        {
            return $"NOT EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")})";
        }

        string covered = string.Join(
            " OR ",
            claimEntries.Select(e => $"(st.{this.keyColumn} = {e.KeyPlaceholder} AND st.{this.valueColumn} IN ({string.Join(", ", e.ValuePlaceholders)}))"));

        return $"NOT EXISTS (SELECT 1 FROM {this.tagTable} st WHERE {this.CorrelateOuter("st")} AND NOT ({covered}))";
    }

    /// <inheritdoc/>
    public string ExistsTagKeysShareValue(string keyPlaceholder1, string keyPlaceholder2)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st1 JOIN {this.tagTable} st2 ON {Correlate("st1", "st2")} AND st1.{this.valueColumn} = st2.{this.valueColumn} " +
           $"WHERE {this.CorrelateOuter("st1")} AND st1.{this.keyColumn} = {keyPlaceholder1} AND st2.{this.keyColumn} = {keyPlaceholder2})";

    /// <inheritdoc/>
    public string Negate(string predicate) => $"NOT ({predicate})";

    /// <inheritdoc/>
    public string AndAlso(string left, string right) => $"({left} AND {right})";

    /// <inheritdoc/>
    public string OrElse(string left, string right) => $"({left} OR {right})";

    private static string Correlate(string aliasA, string aliasB, IReadOnlyList<string> columns)
        => string.Join(" AND ", columns.Select(c => $"{aliasA}.{c} = {aliasB}.{c}"));

    // Correlates a tag-table alias to the outer query's row across all owner columns.
    private string CorrelateOuter(string alias) => Correlate(alias, this.outerReference, this.ownerColumns);

    // Correlates two tag-table aliases (the self-join for tag-key == tag-key) across all owner columns.
    private string Correlate(string aliasA, string aliasB) => Correlate(aliasA, aliasB, this.ownerColumns);
}