// <copyright file="IScopeKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides scope semantics.
/// </summary>
public interface IScopeKeyword : IKeyword
{
    /// <summary>
    /// Enter a scope.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The current type declaration.</param>
    /// <param name="scopeName">The scope name.</param>
    /// <param name="existingTypeDeclaration">The existing type declaration for the new scope, if any.</param>
    /// <returns><see langword="true"/> if the scope was entered.</returns>
    bool TryEnterScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, string scopeName, out TypeDeclaration? existingTypeDeclaration);

    /// <summary>
    /// Gets the scope for the keyword.
    /// </summary>
    /// <param name="schema">The parent schema containing the keyword.</param>
    /// <param name="scope">The scope defined by the keyword.</param>
    /// <returns><see langword="true"/> if this schema defines a scope with this keyword.</returns>
    bool TryGetScope(in JsonElement schema, [NotNullWhen(true)] out string? scope);
}