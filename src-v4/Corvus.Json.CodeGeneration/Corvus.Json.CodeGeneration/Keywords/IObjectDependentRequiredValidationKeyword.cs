// <copyright file="IObjectDependentRequiredValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates required object properties.
/// </summary>
public interface IObjectDependentRequiredValidationKeyword : IObjectValidationKeyword
{
    /// <summary>
    /// Gets the path modifier for the keyword and dependent required declaration.
    /// </summary>
    /// <param name="dependentRequired">The depdendent required declaration for which to get the path modifier.</param>
    /// <param name="index">The index in the dependent required array for which to get the path modifier.</param>
    /// <returns>The path modifier for the keyword.</returns>
    string GetPathModifier(DependentRequiredDeclaration dependentRequired, int index);

    /// <summary>
    /// Gets the dependent requried declarations for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The the type declaration for which to get the dependent required declarations.</param>
    /// <returns>The collection of dependent required declarations.</returns>
    IReadOnlyCollection<DependentRequiredDeclaration> GetDependentRequiredDeclarations(TypeDeclaration typeDeclaration);
}