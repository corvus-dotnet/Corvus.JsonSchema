// <copyright file="IShortDocumentationProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides short, summary-like documentation.
/// </summary>
public interface IShortDocumentationProviderKeyword : IKeyword
{
    /// <summary>
    /// Try to get the short documentation for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="documentation">The documentation, or <see langword="null"/> if no
    /// short form documentation is found.</param>
    /// <returns><see langword="true"/> if short documentation was found.</returns>
    bool TryGetShortDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? documentation);
}