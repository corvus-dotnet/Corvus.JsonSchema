// <copyright file="ILongDocumentationProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides short, summary-like documentation.
/// </summary>
public interface ILongDocumentationProviderKeyword : IKeyword
{
    /// <summary>
    /// Try to get the long documentation for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="documentation">The documentation, or <see langword="null"/> if no
    /// long form documentation is found.</param>
    /// <returns><see langword="true"/> if long form documentation was found.</returns>
    bool TryGetLongDocumentation(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? documentation);
}