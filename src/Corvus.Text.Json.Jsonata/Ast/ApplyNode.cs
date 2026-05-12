// <copyright file="ApplyNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// The chain/apply operator: <c>lhs ~&gt; rhs</c>.
/// Pipes the result of the left-hand expression as the first argument of the right-hand function.
/// </summary>
internal sealed class ApplyNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Apply;

    /// <summary>Gets or sets the left-hand expression (the value to pipe).</summary>
    public JsonataNode Lhs { get; set; } = null!;

    /// <summary>Gets or sets the right-hand expression (the function to apply).</summary>
    public JsonataNode Rhs { get; set; } = null!;
}