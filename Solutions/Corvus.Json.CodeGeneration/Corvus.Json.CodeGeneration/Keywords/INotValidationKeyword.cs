// <copyright file="INotValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Composite validator where the composed validation condition must *not* be met.
/// </summary>
public interface INotValidationKeyword : IValidationKeyword, ISubschemaTypeBuilderKeyword, ISubschemaProviderKeyword
{
    /// <summary>
    /// Gets the path modifier for the type declaration.
    /// </summary>
    /// <param name="notType">The reduced type declaration for which to get the path modifier.</param>
    /// <returns>The path modifier for the type.</returns>
    string GetPathModifier(ReducedTypeDeclaration notType);

    /// <summary>
    /// Try to get the type for the keyword.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration from which to get the type.</param>
    /// <param name="notType">The type, or <see langword="null"/> if no type is available.</param>
    /// <returns><see langword="true"/> if a not type was provided.</returns>
    bool TryGetNotType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out ReducedTypeDeclaration? notType);
}