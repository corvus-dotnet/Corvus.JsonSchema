// <copyright file="IObjectPropertyDependentSchemasValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates an object against a schema if it contains a property with the given value.
/// </summary>
public interface IObjectPropertyDependentSchemasValidationKeyword : IObjectValidationKeyword
{
    /// <summary>
    /// Gets the dependent schema declarations for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The the type declaration for which to get the dependent schema declarations.</param>
    /// <returns>The collection of dependent schema declarations.</returns>
    IReadOnlyCollection<DependentSchemaDeclaration> GetDependentSchemaDeclarations(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets the path modifier for the type declaration and property name.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The reduced path modifier.</returns>
    string GetPathModifier(ReducedTypeDeclaration typeDeclaration, string propertyName);
}