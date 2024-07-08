// <copyright file="ITypeBuilderCustomKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A custom keyword that is applied during a type builder phase.
/// </summary>
/// <remarks>
/// Note that a custom keyword may well apply in both the <see cref="ISchemaRegistrationCustomKeyword"/> phase
/// and the <see cref="ITypeBuilderCustomKeyword"/> phase.
/// </remarks>
public interface ITypeBuilderCustomKeyword
{
    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    void Apply(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration);
}