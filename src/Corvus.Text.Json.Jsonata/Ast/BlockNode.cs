// <copyright file="BlockNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A block expression: <c>( expr; expr; ... )</c>.
/// The value of a block is the value of its last expression.
/// </summary>
internal sealed class BlockNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Block;

    /// <summary>Gets the list of semicolon-separated expressions in the block.</summary>
    public List<JsonataNode> Expressions { get; } = [];

    /// <summary>Gets or sets a value indicating whether this block contains an array constructor.</summary>
    public bool ConsArray { get; set; }
}