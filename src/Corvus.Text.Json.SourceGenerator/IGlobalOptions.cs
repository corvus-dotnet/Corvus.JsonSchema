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
    /// Creates a language provider from the Global Options and the inputs of a single generation.
    /// </summary>
    /// <param name="defaultNamespace">The default namespace to use for the language provider, or <see langword="null"/> if no language provider is specified.</param>
    /// <param name="namedTypes">The named types requested by this generation.</param>
    /// <param name="emitEvaluator">Whether at least one generation specification requested evaluator emission.</param>
    /// <returns>An instance of the language provider created from the global options.</returns>
    /// <remarks>
    /// The global options are cached by the incremental pipeline and shared by every generation
    /// in the lifetime of the generator driver, so per-generation state must not be stored in them.
    /// </remarks>
    ILanguageProvider CreateLanguageProvider(string? defaultNamespace, IReadOnlyList<NamedTypeSpecification> namedTypes, bool emitEvaluator);
}