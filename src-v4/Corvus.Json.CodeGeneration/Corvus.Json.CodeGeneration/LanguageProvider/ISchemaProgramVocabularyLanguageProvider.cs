// <copyright file="ISchemaProgramVocabularyLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An <see cref="ISchemaProgramLanguageProvider"/> that is given the fallback vocabulary itself, not just its URI.
/// </summary>
/// <remarks>
/// <para>
/// The OpenAPI 2.0 and 3.0 Schema Object vocabularies share the draft 4 URI, so a provider that has to tell them
/// from draft 4 (they add <c>nullable</c>, and formats draft 4 does not define) needs the vocabulary.
/// </para>
/// <para>
/// <see cref="JsonSchemaTypeBuilder"/> calls <see cref="SetFallbackVocabulary"/> before it supplies the schema
/// documents. It is a separate interface rather than a default interface member because the library targets
/// netstandard2.0.
/// </para>
/// </remarks>
public interface ISchemaProgramVocabularyLanguageProvider : ISchemaProgramLanguageProvider
{
    /// <summary>
    /// Sets the vocabulary applied to documents without <c>$schema</c>.
    /// </summary>
    /// <param name="fallbackVocabulary">The fallback vocabulary, or <see langword="null"/>.</param>
    void SetFallbackVocabulary(IVocabulary? fallbackVocabulary);
}