// <copyright file="NumberNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A numeric literal.
/// </summary>
internal sealed class NumberNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Number;

    /// <summary>Gets or sets the numeric value.</summary>
    public double Value { get; set; }
}