// <copyright file="ValueNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A literal value: <c>true</c>, <c>false</c>, or <c>null</c>.
/// </summary>
internal sealed class ValueNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Value;

    /// <summary>Gets or sets the raw value string (<c>"true"</c>, <c>"false"</c>, or <c>"null"</c>).</summary>
    public string Value { get; set; } = string.Empty;
}