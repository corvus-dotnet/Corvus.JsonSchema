// <copyright file="PlaceholderNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A <c>?</c> placeholder in a partial function application.
/// </summary>
internal sealed class PlaceholderNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Placeholder;
}