// <copyright file="IExamplesProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides schema-valid examples documentation.
/// </summary>
public interface IExamplesProviderKeyword : IKeyword
{
    /// <summary>
    /// Try to get the examples for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="examples">The examples, or <see langword="null"/> if no
    /// examples are found.</param>
    /// <returns><see langword="true"/> if examples were found.</returns>
    bool TryGetExamples(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string[]? examples);
}