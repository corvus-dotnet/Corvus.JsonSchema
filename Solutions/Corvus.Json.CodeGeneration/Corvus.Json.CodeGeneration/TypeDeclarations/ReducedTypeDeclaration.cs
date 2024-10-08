// <copyright file="ReducedTypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A reduced type declaration.
/// </summary>
/// <param name="reducedTypeDeclaration">The reduced type declaration.</param>
/// <param name="reducedPathModifier">The reduced path modifier.</param>
public readonly struct ReducedTypeDeclaration(TypeDeclaration reducedTypeDeclaration, JsonReference reducedPathModifier)
{
    /// <summary>
    /// Gets the reduced type declaration.
    /// </summary>
    public TypeDeclaration ReducedType { get; } = reducedTypeDeclaration;

    /// <summary>
    /// Gets the reduced path modifier.
    /// </summary>
    public JsonReference ReducedPathModifier { get; } = reducedPathModifier;
}