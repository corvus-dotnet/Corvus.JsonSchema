// <copyright file="ConditionNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A ternary conditional expression: <c>condition ? then : else</c>.
/// Also used for the elvis (<c>?:</c>) and null-coalescing (<c>??</c>) operators.
/// </summary>
internal sealed class ConditionNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Condition;

    /// <summary>Gets or sets the condition expression.</summary>
    public JsonataNode Condition { get; set; } = null!;

    /// <summary>Gets or sets the expression evaluated when the condition is truthy.</summary>
    public JsonataNode Then { get; set; } = null!;

    /// <summary>Gets or sets the expression evaluated when the condition is falsy, or <c>null</c> if there is no else branch.</summary>
    public JsonataNode? Else { get; set; }
}