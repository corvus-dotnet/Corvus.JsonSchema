// <copyright file="ISchemaProgramLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A language provider that emits a schema evaluation program alongside the generated types, and therefore
/// needs the text of every schema document the type builder loaded.
/// </summary>
public interface ISchemaProgramLanguageProvider
{
    /// <summary>
    /// Supplies the schema documents and the fallback vocabulary before code generation.
    /// </summary>
    /// <param name="documents">The documents, keyed by their root document URI, with their JSON text.</param>
    /// <param name="fallbackVocabularyUri">The URI of the vocabulary applied to documents without <c>$schema</c>, or <see langword="null"/>.</param>
    void SetSchemaDocuments(IReadOnlyList<KeyValuePair<string, string>> documents, string? fallbackVocabularyUri);

    /// <summary>
    /// Sets the root type declarations code is being generated for, before <see cref="ILanguageProvider.GenerateCodeFor"/>
    /// is called. A root that reduces to another type (for example a root that is nothing but a <c>$ref</c>) is still
    /// the schema the generated type stands for, so its location, not the reduced target's, is the type's entry point.
    /// </summary>
    /// <param name="rootTypes">The root type declarations.</param>
    void SetProgramRootTypes(IReadOnlyList<TypeDeclaration> rootTypes);
}