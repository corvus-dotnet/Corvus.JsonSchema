// <copyright file="Validation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validation helpers.
/// </summary>
public static class Validation
{
    /// <summary>
    /// Gets the validation constant value for the <see cref="IValidationConstantProviderKeyword"/>, at the specific index.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keyword">The keyword for which to get the validation constant.</param>
    /// <param name="index">The index at which to get the validation constant (or null if the keyword expects a single validation constant).</param>
    /// <param name="value">The resulting validation constant, or an undefined <see cref="JsonElement"/> if no such value exists.
    /// This includes out-of-range requests.</param>
    /// <returns><see langword="true"/> if the validation constant was found, otherwise <see langword="false"/>.</returns>
    public static bool TryGetValidationConstantForKeyword(this TypeDeclaration typeDeclaration, IValidationConstantProviderKeyword keyword, int? index, [NotNullWhen(true)] out JsonElement value)
    {
        int i = index ?? 0;
        IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]>? constants = typeDeclaration.ValidationConstants();
        if (constants is not null &&
            constants.TryGetValue(keyword, out JsonElement[]? values) &&
            values.Length > i)
        {
            value = values[i];
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets the number of validation constant values for the <see cref="IValidationConstantProviderKeyword"/>.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keyword">The keyword for which to get the validation constant.</param>
    /// <returns>The number of validation constants for the keyword.</returns>
    public static int GetValidationConstantCountForKeyword(this TypeDeclaration typeDeclaration, IValidationConstantProviderKeyword keyword)
    {
        IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]>? constants = typeDeclaration.ValidationConstants();
        if (constants is not null &&
            constants.TryGetValue(keyword, out JsonElement[]? values))
        {
            return values.Length;
        }

        return 0;
    }
}