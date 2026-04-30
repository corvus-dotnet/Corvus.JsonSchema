// <copyright file="PartialNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A partial function application: <c>fn(?, arg, ?)</c>.
/// Arguments that are <see cref="PlaceholderNode"/> represent unfilled positions.
/// </summary>
internal sealed class PartialNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Partial;

    /// <summary>Gets or sets the expression that resolves to the function to partially apply.</summary>
    public JsonataNode Procedure { get; set; } = null!;

    /// <summary>Gets the argument expressions (placeholders represent unfilled positions).</summary>
    public List<JsonataNode> Arguments { get; } = [];
}