// <copyright file="SortNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A sort step in a path expression: <c>^(&lt;field, &gt;field)</c>.
/// </summary>
internal sealed class SortNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Sort;

    /// <summary>Gets the sort terms (each with a direction and key expression).</summary>
    public List<SortTerm> Terms { get; } = [];
}