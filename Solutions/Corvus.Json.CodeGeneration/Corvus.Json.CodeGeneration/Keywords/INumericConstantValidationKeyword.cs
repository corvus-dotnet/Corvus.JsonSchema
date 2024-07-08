// <copyright file="INumericConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates numeric values against a constant.
/// </summary>
public interface INumericConstantValidationKeyword : IValidationConstantProviderKeyword
{
    /// <summary>
    /// Gets the operator to use for the comparison.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="op">The resulting operator, or <see langword="null"/> if the keyword
    /// was not available.</param>
    /// <returns><see langword="true"/> if the operator was retrieved.</returns>
    bool TryGetOperator(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out Operator op);
}