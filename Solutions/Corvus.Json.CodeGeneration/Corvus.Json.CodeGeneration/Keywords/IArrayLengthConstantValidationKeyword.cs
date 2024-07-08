// <copyright file="IArrayLengthConstantValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates array lengths against a constant.
/// </summary>
public interface IArrayLengthConstantValidationKeyword : IValidationConstantProviderKeyword
{
    /// <summary>
    /// Gets the operator to use for the comparison.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="op">The resulting operator.</param>
    /// <returns><see langword="true"/> if the operator was retrieved.</returns>
    bool TryGetOperator(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out Operator op);

    /// <summary>
    /// Gets the value for comparison.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the value.</param>
    /// <param name="value">The resulting value.</param>
    /// <returns><see langword="true"/> if the value was retrieved.</returns>
    bool TryGetValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out int value);
}