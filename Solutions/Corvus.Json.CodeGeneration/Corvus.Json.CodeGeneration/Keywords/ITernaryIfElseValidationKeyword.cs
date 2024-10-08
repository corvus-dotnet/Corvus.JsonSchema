// <copyright file="ITernaryIfElseValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Keywords that support ternary if/else validation.
/// </summary>
public interface ITernaryIfElseValidationKeyword : ISingleSubschemaProviderKeyword
{
    /// <summary>
    /// Tries to get the type declaration for a ternary else expression..
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the else declaration.</param>
    /// <param name="elseDeclaration">The type declaration for the else clause.</param>
    /// <returns><see langword="true"/> if the <paramref name="elseDeclaration"/>/> is present.</returns>
    bool TryGetElseDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? elseDeclaration);
}