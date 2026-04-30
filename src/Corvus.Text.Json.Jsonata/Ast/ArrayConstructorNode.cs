// <copyright file="ArrayConstructorNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// An array constructor expression: <c>[ expr, expr, ... ]</c>.
/// </summary>
internal sealed class ArrayConstructorNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.ArrayConstructor;

    /// <summary>Gets the element expressions.</summary>
    public List<JsonataNode> Expressions { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this is a "constructed array" that should not
    /// be flattened during path evaluation. Set when the array constructor is the first or last
    /// step in a path expression.
    /// </summary>
    public bool ConsArray { get; set; }
}