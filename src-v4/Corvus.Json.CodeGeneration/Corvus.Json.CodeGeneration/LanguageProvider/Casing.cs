// <copyright file="Casing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Determines the type of casing for a name.
/// </summary>
public enum Casing
{
    /// <summary>
    /// Unmodified casing.
    /// </summary>
    Unmodified,

    /// <summary>
    /// Pascal casing (LikeThis).
    /// </summary>
    PascalCase,

    /// <summary>
    /// Camel casing (likeThis).
    /// </summary>
    CamelCase,
}