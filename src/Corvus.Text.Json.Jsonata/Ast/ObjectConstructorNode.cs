// <copyright file="ObjectConstructorNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// An object constructor expression: <c>{ key: value, ... }</c>.
/// </summary>
internal sealed class ObjectConstructorNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.ObjectConstructor;

    /// <summary>Gets the key-value expression pairs.</summary>
    public List<(JsonataNode Key, JsonataNode Value)> Pairs { get; } = [];
}