// <copyright file="IOneOfSubschemaValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where exactly one of the composed validation conditions must be met.
/// </summary>
public interface IOneOfSubschemaValidationKeyword : IOneOfValidationKeyword, ISubschemaProviderKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the subchema type declaration.
    /// </summary>
    /// <param name="subschema">The reduced subschema type declaration.</param>
    /// <param name="index">The index of the item in the subschema set.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(ReducedTypeDeclaration subschema, int index);
}