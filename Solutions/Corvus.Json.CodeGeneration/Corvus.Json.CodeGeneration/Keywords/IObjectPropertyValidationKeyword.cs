// <copyright file="IObjectPropertyValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> property values.
/// </summary>
public interface IObjectPropertyValidationKeyword : IObjectValidationKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the property declaration.
    /// </summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(PropertyDeclaration property);
}