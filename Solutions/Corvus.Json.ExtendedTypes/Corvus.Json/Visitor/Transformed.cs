// <copyright file="Transformed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Visitor;

/// <summary>
/// Used by <see cref="VisitResult"/> to determine whether the node has been transformed or not.
/// </summary>
public enum Transformed
{
    /// <summary>
    /// The node was not transformed.
    /// </summary>
    No,

    /// <summary>
    /// The node was transformed.
    /// </summary>
    Yes,
}