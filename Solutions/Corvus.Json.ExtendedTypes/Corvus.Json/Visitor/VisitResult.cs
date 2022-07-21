// <copyright file="VisitResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Visitor;

/// <summary>
/// Records the result of visiting the node.
/// </summary>
/// <param name="Output">The output from visiting the node. This will be the original node if <see cref="IsTransformed"/> is <c>false</c>.</param>
/// <param name="Transformed">Indicates whether the node was transformed or not.</param>
/// <param name="Walk">The action for the parent to take after visiting this node.</param>
public readonly record struct VisitResult(JsonAny Output, Transformed Transformed, Walk Walk)
{
    /// <summary>
    /// Gets a value indicating whether the node was transformed.
    /// </summary>
    public bool IsTransformed => this.Transformed == Transformed.Yes;
}