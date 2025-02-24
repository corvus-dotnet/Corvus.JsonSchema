// <copyright file="GeneratedTypeAccessibility.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Defines the accessibility of the generated types.
/// </summary>
public enum GeneratedTypeAccessibility
{
    /// <summary>
    /// The generated types should be <see langword="public"/>.
    /// </summary>
    Public,

    /// <summary>
    /// The generated types should be <see langword="internal"/>.
    /// </summary>
    Internal,
}