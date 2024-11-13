// <copyright file="IObjectRequiredPropertyValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates required object properties.
/// </summary>
public interface IObjectRequiredPropertyValidationKeyword : IObjectValidationKeyword
{
    /// <summary>
    /// Gets the path modifier for the keyword and required property.
    /// </summary>
    /// <param name="property">The property for which to get the path modifier.</param>
    /// <returns>The path modifier for the keyword.</returns>
    string GetPathModifier(PropertyDeclaration property);
}