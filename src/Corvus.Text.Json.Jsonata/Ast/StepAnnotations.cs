// <copyright file="StepAnnotations.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// Annotations added to path steps during AST post-processing.
/// Steps in a path (name, wildcard, etc.) can have predicates, group-by,
/// focus-variable bindings, and index-variable bindings attached.
/// </summary>
internal sealed class StepAnnotations
{
    /// <summary>Gets the predicate/filter stages attached to this step.</summary>
    public List<JsonataNode> Stages { get; } = [];

    /// <summary>Gets or sets the group-by clause, if any.</summary>
    public GroupBy? Group { get; set; }

    /// <summary>Gets or sets the name of the focus variable bound by <c>@$var</c>.</summary>
    public string? Focus { get; set; }

    /// <summary>Gets or sets the name of the index variable bound by <c>#$var</c>.</summary>
    public string? Index { get; set; }

    /// <summary>Gets or sets a value indicating whether this step participates in a tuple context.</summary>
    public bool Tuple { get; set; }

    /// <summary>Gets the labels of parent-operator slots resolved at this step.
    /// When present, the input context to this step should be bound under these labels
    /// so that <c>%</c> operators can look them up.</summary>
    public List<string>? AncestorLabels { get; set; }
}