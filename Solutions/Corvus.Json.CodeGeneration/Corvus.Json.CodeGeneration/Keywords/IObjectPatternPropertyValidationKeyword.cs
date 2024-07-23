// <copyright file="IObjectPatternPropertyValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> property values by matching
/// the property value against a schema if the property name matches a given pattern.
/// </summary>
public interface IObjectPatternPropertyValidationKeyword : IObjectValidationKeyword, ISubschemaProviderKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the pattern property declaration.
    /// </summary>
    /// <param name="propertyTypeDeclaration">The pattern property type declaration.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(ReducedTypeDeclaration propertyTypeDeclaration);
}