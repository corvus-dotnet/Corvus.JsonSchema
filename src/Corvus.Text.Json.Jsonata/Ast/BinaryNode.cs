// <copyright file="BinaryNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A binary operator expression: <c>lhs op rhs</c>.
/// Covers arithmetic, comparison, boolean, string concatenation, and containment operators.
/// </summary>
internal sealed class BinaryNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Binary;

    /// <summary>Gets or sets the operator symbol (e.g. <c>+</c>, <c>=</c>, <c>and</c>).</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>Gets or sets the left-hand operand.</summary>
    public JsonataNode Lhs { get; set; } = null!;

    /// <summary>Gets or sets the right-hand operand.</summary>
    public JsonataNode Rhs { get; set; } = null!;
}