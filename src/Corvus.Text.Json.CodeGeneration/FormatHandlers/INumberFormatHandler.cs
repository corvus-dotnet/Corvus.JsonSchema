// <copyright file="INumberFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A format handler for number formats.
/// </summary>
public interface INumberFormatHandler : IFormatHandler
{
    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="formatKeywordProviderExpression">The expression that produces the JsonSchemaPathProvider for the keyword.</param>
    /// <param name="isNegativeIdentifier">The identifier that contains the isNegative value of the normalized JSON number.</param>
    /// <param name="integralIdentifier">The identifier that contains the integral value of the normalized JSON number.</param>
    /// <param name="fractionalIdentifier">The identifier that contains the fractional value of the normalized JSON number.</param>
    /// <param name="exponentIdentifier">The identifier that contains the exponent value of the normalized JSON number.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatAssertion(
        CodeGenerator generator,
        string format,
        string formatKeywordProviderExpression,
        string isNegativeIdentifier,
        string integralIdentifier,
        string fractionalIdentifier,
        string exponentIdentifier,
        string validationContextIdentifier);

    /// <summary>
    /// Get the preferred numeric type for a format.
    /// </summary>
    /// <param name="format">The format for which to determine the preferred numeric type.</param>
    /// <param name="typeName">The preferred type name, or <see langword="null"/> if there was no preferred numeric type for this format.</param>
    /// <param name="isNetOnly"><see langword="true"/> if the format is for .NET only (not available on netstandard2.0).</param>
    /// <param name="netStandardFallback">The name of the netstandard fallback type, if <paramref name="isNetOnly"/> is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool TryGetNumericTypeName(string format, [NotNullWhen(true)] out string? typeName, out bool isNetOnly, out string? netStandardFallback);
}