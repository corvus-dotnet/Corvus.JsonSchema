// <copyright file="WildcardNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A single-level field wildcard step: <c>*</c>.
/// Matches all fields of an object at the current level.
/// </summary>
internal sealed class WildcardNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Wildcard;

    /// <summary>Gets or sets the step annotations (predicates, group-by, focus/index bindings).</summary>
    public StepAnnotations? Annotations { get; set; }
}