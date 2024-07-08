// <copyright file="IIfValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Keywords that support binary or ternary if validation.
/// </summary>
public interface IIfValidationKeyword : IValidationKeyword, ISubschemaProviderKeyword
{
    /// <summary>
    /// Tries to get the type declaration for a binary or ternary if expression..
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the if declaration.</param>
    /// <param name="ifDeclaration">The type declaration for the if clause.</param>
    /// <returns><see langword="true"/> if the <paramref name="ifDeclaration"/>/> is present.</returns>
    bool TryGetIfDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? ifDeclaration);
}