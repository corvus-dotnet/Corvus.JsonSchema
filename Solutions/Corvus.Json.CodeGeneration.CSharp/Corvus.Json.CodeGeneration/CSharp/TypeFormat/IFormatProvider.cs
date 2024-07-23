// <copyright file="IFormatProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Support for well-known format types.
/// </summary>
public interface IFormatProvider
{
    /// <summary>
    /// Gets the .NET type name for the given candidate format (e.g. JsonUuid, JsonIri, JsonInt64 etc).
    /// </summary>
    /// <param name="format">The candidate format.</param>
    /// <returns>The corresponding .NET type name, or <see langword="null"/> if the format is not explicitly supported.</returns>
    string? GetDotnetTypeNameFor(string format);

    /// <summary>
    /// Gets the expected <see cref="JsonValueKind"/> for instances
    /// that support the given format.
    /// </summary>
    /// <param name="format">The format for which to get the value kind.</param>
    /// <returns>The expected <see cref="JsonValueKind"/>, or <see langword="null"/>
    /// if no value kind is expected for the format.</returns>
    JsonValueKind? GetExpectedValueKind(string format);

    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="valueIdentifier">The identifier for the value to test.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <returns><see langword="true"/> if the assertion was appended successfully.</returns>
    bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier);
}