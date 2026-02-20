// <copyright file="IGlobalOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Json.SourceGeneratorTools;

/// <summary>
/// Implemented by types which provide the global options for a particular source generator.
/// </summary>
public interface IGlobalOptions
{
    /// <summary>
    /// Gets the fallback vocabulary for code generation.
    /// </summary>
    IVocabulary FallbackVocabulary { get; }

    /// <summary>
    /// Add a named type to the global options.
    /// </summary>
    /// <param name="reference">The canonical schema location for the type.</param>
    /// <param name="dotnetTypeName">The .NET type name.</param>
    /// <param name="dotnetNamespace">The .NET namespace.</param>
    /// <param name="accessibility">The accessibility for the type.</param>
    void AddNamedType(JsonReference reference, string dotnetTypeName, string? dotnetNamespace = null, GeneratedTypeAccessibility? accessibility = null);

    /// <summary>
    /// Creates a language provider from the Global Options.
    /// </summary>
    /// <param name="defaultNamespace">The default namespace to use for the language provider, or <see langword="null"/> if no language provider is specified.</param>
    /// <returns>An instance of the language provider created from the global options.</returns>
    ILanguageProvider CreateLanguageProvider(string? defaultNamespace);
}