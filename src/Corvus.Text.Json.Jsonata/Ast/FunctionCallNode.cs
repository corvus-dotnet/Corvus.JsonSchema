// <copyright file="FunctionCallNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A function call expression: <c>procedure(args)</c>.
/// </summary>
internal sealed class FunctionCallNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.FunctionCall;

    /// <summary>Gets or sets the expression that resolves to the function to invoke.</summary>
    public JsonataNode Procedure { get; set; } = null!;

    /// <summary>Gets the argument expressions.</summary>
    public List<JsonataNode> Arguments { get; } = [];
}