// <copyright file="IIfThenValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Keywords that support if validation.
/// </summary>
public interface IIfThenValidationKeyword : ISingleSubschemaProviderKeyword
{
    /// <summary>
    /// Tries to get the type declaration for a binary or ternary then expression..
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get then declaration.</param>
    /// <param name="thenDeclaration">The type declaration for then clause.</param>
    /// <returns><see langword="true"/> if the <paramref name="thenDeclaration"/>/> is present.</returns>
    bool TryGetThenDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? thenDeclaration);
}