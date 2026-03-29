// <copyright file="ScopeType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Well-known scope types.
/// </summary>
public static class ScopeType
{
    /// <summary>
    /// Gets the unknown scope type.
    /// </summary>
    public const int Unknown = 0;

    /// <summary>
    /// Gets a scope type for a method.
    /// </summary>
    public const int Method = 1;

    /// <summary>
    /// Gets a scope type for a type.
    /// </summary>
    public const int Type = 2;

    /// <summary>
    /// Gets a scope type for a type container (such as a namespace or module).
    /// </summary>
    public const int TypeContainer = 3;
}