// <copyright file="FilterNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A filter predicate applied to a path step: <c>step[predicate]</c>.
/// </summary>
internal sealed class FilterNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Filter;

    /// <summary>Gets or sets the predicate expression.</summary>
    public JsonataNode Expression { get; set; } = null!;
}