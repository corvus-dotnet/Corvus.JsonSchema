// <copyright file="IAnyOfConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where one or more of the composed validation conditions must be met.
/// </summary>
public interface IAnyOfConstantValidationKeyword : IAnyOfValidationKeyword, IValidationConstantProviderKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the subchema type declaration.
    /// </summary>
    /// <param name="index">The index of the item in the subschema set.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(int index);
}