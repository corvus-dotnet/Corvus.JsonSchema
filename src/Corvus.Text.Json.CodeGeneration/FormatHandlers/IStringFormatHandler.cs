// <copyright file="IStringFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A format provider for string formats.
/// </summary>
public interface IStringFormatHandler : IFormatHandler
{
    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="formatKeywordProviderExpression">The expression that produces the JsonSchemaPathProvider for the keyword.</param>
    /// <param name="valueIdentifier">The identifier for the value to test.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatAssertion(
        CodeGenerator generator,
        string format,
        string formatKeywordProviderExpression,
        string valueIdentifier,
        string validationContextIdentifier);

    /// <summary>
    /// Indicates whether the string format requires the simple types backing.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <param name="requiresSimpleType"><see langword="true"/> if the format requires the fixed-size simple types backing.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool RequiresSimpleTypesBacking(string format, out bool requiresSimpleType);

    /// <summary>
    /// Appends format-aware <c>ToString(string?, IFormatProvider?)</c>,
    /// <c>TryFormat(Span&lt;char&gt;, ...)</c>, and <c>TryFormat(Span&lt;byte&gt;, ...)</c>
    /// overload implementations for a string-format type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the overloads.</param>
    /// <param name="format">The format string (e.g. "date", "uuid").</param>
    /// <param name="forMutable">If <see langword="true"/>, the code should be emitted for a mutable type.</param>
    /// <returns><see langword="true"/> if the instance handled this format and generated all three overloads.</returns>
    bool AppendFormatToStringAndTryFormatOverrides(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, bool forMutable);
}