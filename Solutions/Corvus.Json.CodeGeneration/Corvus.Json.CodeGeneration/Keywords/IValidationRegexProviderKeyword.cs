// <copyright file="IValidationRegexProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides one or more validation regular expressions.
/// </summary>
/// <remarks>
/// These are regular expression values that may be used by code generators
/// to minimize overhead when implementing validators. They may be
/// cached, or provided as static values as appropriate.
/// </remarks>
public interface IValidationRegexProviderKeyword : IValidationKeyword
{
    /// <summary>
    /// Try to get validation regular expressions from the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration from which to get validation regular expressions.</param>
    /// <param name="regexes">The validation regular expressions, or <see langword="null"/> if no validation
    /// regular expressions were generated.</param>
    /// <returns><see langword="true"/> if any validation regular expressions were produced.</returns>
    bool TryGetValidationRegularExpressions(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out IReadOnlyList<string>? regexes);
}