// <copyright file="OpenApiClientEmitters.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Resolves the client emitter for a language identifier (<see cref="LanguageIds"/>).
/// </summary>
public static class OpenApiClientEmitters
{
    /// <summary>
    /// Creates the client emitter for a language.
    /// </summary>
    /// <param name="language">A normalised language identifier from <see cref="LanguageIds"/>.</param>
    /// <returns>The emitter.</returns>
    /// <exception cref="NotSupportedException">The language has no client emitter.</exception>
    public static IOpenApiClientEmitter Create(string language)
    {
        if (string.Equals(language, LanguageIds.CSharp, StringComparison.Ordinal))
        {
            return new CSharpClientEmitter();
        }

        throw new NotSupportedException($"No OpenAPI client emitter exists for the language '{language}'. Supported: {string.Join(", ", LanguageIds.Supported)}.");
    }
}