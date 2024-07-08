// <copyright file="IDefaultValueProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides a default value for a type declaration.
/// </summary>
/// <remarks>
/// The default value also forms part of the examples for a schema.
/// </remarks>
public interface IDefaultValueProviderKeyword : IExamplesProviderKeyword
{
    /// <summary>
    /// Try to get the default value for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="defaultValue">The defaultValue, or <see langword="default"/> if no
    /// examples are found.</param>
    /// <returns><see langword="true"/> if a default value was found.</returns>
    bool TryGetDefaultValue(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out JsonElement defaultValue);
}