// <copyright file="INumberFormatProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A format provider for number formats.
/// </summary>
public interface INumberFormatProvider : IFormatProvider
{
    /// <summary>
    /// Gets the .NET BCL type name for the given C# numeric langword or type.
    /// </summary>
    /// <param name="langword">The .NET numeric langword.</param>
    /// <returns>The JSON string form suffix (e.g. <see langword="long"/> becomes <c>Int64</c>.</returns>
    string? GetDotnetTypeNameForCSharpNumericLangwordOrTypeName(string langword);

    /// <summary>
    /// Gets the JSON type for the given integer format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    string? GetIntegerDotnetTypeNameFor(string format);

    /// <summary>
    /// Gets the JSON type for the given floating-point format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    string? GetFloatDotnetTypeNameFor(string format);
}