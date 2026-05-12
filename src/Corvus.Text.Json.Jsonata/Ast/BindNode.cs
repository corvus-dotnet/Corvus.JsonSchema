// <copyright file="BindNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A variable binding expression: <c>$name := expr</c>.
/// </summary>
internal sealed class BindNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Bind;

    /// <summary>Gets or sets the left-hand side (the variable being bound).</summary>
    public JsonataNode Lhs { get; set; } = null!;

    /// <summary>Gets or sets the right-hand side (the value expression).</summary>
    public JsonataNode Rhs { get; set; } = null!;
}