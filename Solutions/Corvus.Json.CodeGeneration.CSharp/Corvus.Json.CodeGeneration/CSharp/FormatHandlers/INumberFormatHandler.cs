// <copyright file="INumberFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A format handler for number formats.
/// </summary>
public interface INumberFormatHandler : IFormatHandler
{
    /// <summary>
    /// Gets the .NET BCL type name for the given C# numeric langword, or BCL type name.
    /// </summary>
    /// <param name="langword">The .NET numeric langword.</param>
    /// <returns>The JSON string form suffix (e.g. <see langword="long"/> becomes <c>Int64</c>.</returns>
    string? GetTypeNameForNumericLangwordOrTypeName(string langword);

    /// <summary>
    /// Gets the Corvus.Json type for the given integer format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <c>JsonNumber</c> if the format is not recognized.</returns>
    string? GetIntegerCorvusJsonTypeNameFor(string format);

    /// <summary>
    /// Gets the Corvus.Json type for the given floating-point format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <c>JsonNumber</c> if the format is not recognized.</returns>
    string? GetFloatCorvusJsonTypeNameFor(string format);
}