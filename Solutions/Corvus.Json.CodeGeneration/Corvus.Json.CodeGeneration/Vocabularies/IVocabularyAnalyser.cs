// <copyright file="IVocabularyAnalyser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Implemented by types which can analyse a document and provide a suitable vocabulary.
/// </summary>
public interface IVocabularyAnalyser
{
    /// <summary>
    /// Gets the vocabulary for the given schema instance.
    /// </summary>
    /// <param name="schemaInstance">The schema instance for which to get the vocabulary.</param>
    /// <returns>The <see cref="IVocabulary"/> for the schema instance.</returns>
    ValueTask<IVocabulary?> TryGetVocabulary(JsonElement schemaInstance);
}