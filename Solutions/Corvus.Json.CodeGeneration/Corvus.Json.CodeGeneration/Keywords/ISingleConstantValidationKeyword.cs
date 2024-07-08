// <copyright file="ISingleConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validator for a single constant value.
/// </summary>
public interface ISingleConstantValidationKeyword : IAnyOfValidationKeyword, IValidationConstantProviderKeyword
{
    /// <summary>
    /// Try to get the single constant value for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="constantValue">The defaultValue, or <see langword="default"/> if no
    /// examples are found.</param>
    /// <returns><see langword="true"/> if a default value was found.</returns>
    bool TryGetConstantValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement constantValue);
}