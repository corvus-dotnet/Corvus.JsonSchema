// <copyright file="VariableNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A variable reference: <c>$name</c>. The <see cref="Name"/> excludes the leading <c>$</c>.
/// </summary>
internal sealed class VariableNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Variable;

    /// <summary>Gets or sets the variable name (without the <c>$</c> prefix).</summary>
    public string Name { get; set; } = string.Empty;
}