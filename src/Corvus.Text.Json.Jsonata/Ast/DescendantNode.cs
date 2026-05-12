// <copyright file="DescendantNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A multi-level descendant wildcard step: <c>**</c>.
/// Recursively matches all fields at all levels.
/// </summary>
internal sealed class DescendantNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Descendant;
}