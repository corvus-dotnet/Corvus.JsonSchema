// <copyright file="ParentNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// The parent operator: <c>%</c>. Navigates up to the parent context.
/// </summary>
internal sealed class ParentNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Parent;

    /// <summary>Gets or sets the parent resolution slot.</summary>
    public ParentSlot Slot { get; set; } = null!;
}