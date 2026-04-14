// <copyright file="StringNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A string literal.
/// </summary>
internal sealed class StringNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.String;

    /// <summary>Gets or sets the string value (with escape sequences resolved).</summary>
    public string Value { get; set; } = string.Empty;
}