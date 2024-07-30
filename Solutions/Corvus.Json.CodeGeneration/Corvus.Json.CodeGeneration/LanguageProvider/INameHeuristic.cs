// <copyright file="INameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Defines a heuristic for generating names.
/// </summary>
public interface INameHeuristic
{
    /// <summary>
    /// Gets a value indicating whether this is an optional heuristic.
    /// </summary>
    bool IsOptional { get; }

    /// <summary>
    /// Gets the priority of the heuristic (lower number is higher priority).
    /// </summary>
    uint Priority { get; }

    /// <summary>
    /// Try to get a name for a type declaration using the heuristic.
    /// </summary>
    /// <param name="languageProvider">The language provider for the heuristic.</param>
    /// <param name="typeDeclaration">The type declaration for which to get the name.</param>
    /// <param name="reference">The decomposed reference for the type declaration.</param>
    /// <param name="typeNameBuffer">The working buffer for the type name into which the name will be written.</param>
    /// <param name="written">The number of characters written.</param>
    /// <returns><see langword="true"/> if the heuristic was able to generate a name.</returns>
    bool TryGetName(
        ILanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        out int written);
}