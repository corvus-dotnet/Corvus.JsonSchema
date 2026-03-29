// <copyright file="IValidationConstantProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides one or more validation constants.
/// </summary>
/// <remarks>
/// These are constant values that may be used by code generators
/// to minimize overhead when implementing validators. They may be
/// cached, or provided as static values as appropriate.
/// </remarks>
public interface IValidationConstantProviderKeyword : IValidationKeyword
{
    /// <summary>
    /// Try to get validation constants from the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration from which to get validation constants.</param>
    /// <param name="constants">The validation constants, or <see langword="null"/> if no validation constants
    /// were generated.</param>
    /// <returns><see langword="true"/> if any validation constants were produced.</returns>
    bool TryGetValidationConstants(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement[]? constants);
}