// <copyright file="INamedTypeLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Provides code generation semantics for the output language.
/// </summary>
public interface INamedTypeLanguageProvider : ILanguageProvider
{
    /// <summary>
    /// Set the name for the type declaration and its properties, before the names for its subschema names have been set.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to set the name.</param>
    /// <param name="fallbackName">The name to use as a fallback for the type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName, CancellationToken cancellationToken);

    /// <summary>
    /// Set the name for the type declaration and its properties, after its subschema names have been set.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to set the name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, CancellationToken cancellationToken);
}