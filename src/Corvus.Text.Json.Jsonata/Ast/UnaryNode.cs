// <copyright file="UnaryNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A unary operator expression: <c>op expr</c>. Currently only unary negation (<c>-</c>).
/// </summary>
internal sealed class UnaryNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Unary;

    /// <summary>Gets or sets the operator symbol.</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>Gets or sets the operand expression.</summary>
    public JsonataNode Expression { get; set; } = null!;
}