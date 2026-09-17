// <copyright file="ISchemaProgramUtf8LanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An <see cref="ISchemaProgramLanguageProvider"/> that accepts the schema documents as UTF-8 JSON, so that they
/// need not be converted to strings on their way to a program compiler.
/// </summary>
/// <remarks>
/// <see cref="JsonSchemaTypeBuilder"/> passes the documents to the UTF-8 <c>SetSchemaDocuments</c> overload instead of
/// the string one of <see cref="ISchemaProgramLanguageProvider"/> on a provider that implements this interface. It is a separate interface rather than a default interface member
/// because the library targets netstandard2.0.
/// </remarks>
public interface ISchemaProgramUtf8LanguageProvider : ISchemaProgramLanguageProvider
{
    /// <summary>
    /// Sets the UTF-8 JSON text of every root document the type builder loaded, keyed by root document URI, in
    /// first-seen order.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="fallbackVocabularyUri">The URI of the fallback vocabulary, or <see langword="null"/>.</param>
    void SetSchemaDocuments(IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> documents, string? fallbackVocabularyUri);
}