// <copyright file="TransformNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A transform expression: <c>|source|update,delete|</c>.
/// Performs a non-destructive document transformation.
/// </summary>
internal sealed class TransformNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Transform;

    /// <summary>Gets or sets the pattern (source) expression.</summary>
    public JsonataNode Pattern { get; set; } = null!;

    /// <summary>Gets or sets the update expression.</summary>
    public JsonataNode Update { get; set; } = null!;

    /// <summary>Gets or sets the delete expression, or <c>null</c> if no keys are deleted.</summary>
    public JsonataNode? Delete { get; set; }
}