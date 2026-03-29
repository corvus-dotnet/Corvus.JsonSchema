// <copyright file="IFormatProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Format provider.
/// </summary>
public interface IFormatProviderKeyword : IKeyword
{
    /// <summary>
    /// Try to get the format for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the format.</param>
    /// <param name="format">The format, or <see langword="null"/> if the format is not available.</param>
    /// <returns><see langword="true"/> if the format was available on the type declaration.</returns>
    bool TryGetFormat(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? format);
}