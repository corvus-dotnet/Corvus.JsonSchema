// <copyright file="SqlSecurityRuleEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A reusable <see cref="ISecurityRuleSqlEmitter"/> for relational backends (design §14.4): the
/// <c>EXISTS</c>/<c>IN</c>/<c>AND</c>/<c>OR</c>/<c>NOT</c> fragments are standard SQL shared across SQLite,
/// Postgres, MySQL and SQL Server, correlated to the outer query's row via a child security-tag table
/// (<c>(ownerColumn, TagKey, TagValue)</c>). Only the parameter-placeholder spelling differs per dialect, which
/// the caller supplies through the <c>parameter</c> delegate (it binds the value to the command and returns the
/// placeholder, e.g. <c>@s0</c> or <c>$1</c>).
/// </summary>
public sealed class SqlSecurityRuleEmitter : ISecurityRuleSqlEmitter
{
    private readonly string tagTable;
    private readonly string ownerColumn;
    private readonly string outerOwnerReference;
    private readonly Func<string, string> parameter;

    /// <summary>Initializes a new instance of the <see cref="SqlSecurityRuleEmitter"/> class.</summary>
    /// <param name="tagTable">The child table holding one row per security tag: <c>(ownerColumn, TagKey, TagValue)</c>.</param>
    /// <param name="ownerColumn">The column in <paramref name="tagTable"/> that references the owning row's id.</param>
    /// <param name="outerOwnerReference">The qualified id column of the outer query's row to correlate against (e.g. <c>WorkflowRuns.RunId</c>).</param>
    /// <param name="parameter">Binds a value to the command and returns its dialect placeholder.</param>
    public SqlSecurityRuleEmitter(string tagTable, string ownerColumn, string outerOwnerReference, Func<string, string> parameter)
    {
        ArgumentException.ThrowIfNullOrEmpty(tagTable);
        ArgumentException.ThrowIfNullOrEmpty(ownerColumn);
        ArgumentException.ThrowIfNullOrEmpty(outerOwnerReference);
        ArgumentNullException.ThrowIfNull(parameter);
        this.tagTable = tagTable;
        this.ownerColumn = ownerColumn;
        this.outerOwnerReference = outerOwnerReference;
        this.parameter = parameter;
    }

    /// <inheritdoc/>
    public string TrueLiteral => "1 = 1";

    /// <inheritdoc/>
    public string FalseLiteral => "1 = 0";

    /// <inheritdoc/>
    public string Parameter(string value) => this.parameter(value);

    /// <inheritdoc/>
    public string ExistsTagKey(string keyPlaceholder)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st WHERE st.{this.ownerColumn} = {this.outerOwnerReference} AND st.TagKey = {keyPlaceholder})";

    /// <inheritdoc/>
    public string ExistsTagValueIn(string keyPlaceholder, IReadOnlyList<string> valuePlaceholders)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st WHERE st.{this.ownerColumn} = {this.outerOwnerReference} AND st.TagKey = {keyPlaceholder} AND st.TagValue IN ({string.Join(", ", valuePlaceholders)}))";

    /// <inheritdoc/>
    public string ExistsTagKeysShareValue(string keyPlaceholder1, string keyPlaceholder2)
        => $"EXISTS (SELECT 1 FROM {this.tagTable} st1 JOIN {this.tagTable} st2 ON st1.{this.ownerColumn} = st2.{this.ownerColumn} AND st1.TagValue = st2.TagValue " +
           $"WHERE st1.{this.ownerColumn} = {this.outerOwnerReference} AND st1.TagKey = {keyPlaceholder1} AND st2.TagKey = {keyPlaceholder2})";

    /// <inheritdoc/>
    public string Negate(string predicate) => $"NOT ({predicate})";

    /// <inheritdoc/>
    public string AndAlso(string left, string right) => $"({left} AND {right})";

    /// <inheritdoc/>
    public string OrElse(string left, string right) => $"({left} OR {right})";
}