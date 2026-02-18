// <copyright file="VocabularyRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A registry for vocabularies and their analysers.
/// </summary>
public class VocabularyRegistry
{
    private List<IVocabularyAnalyser> analysers = [];
    private Dictionary<string, IVocabulary> vocabularies = [];

    /// <summary>
    /// Registers an analyser.
    /// </summary>
    /// <param name="analyser">The analyser to register.</param>
    public void RegisterAnalyser(IVocabularyAnalyser analyser)
    {
        this.analysers.Add(analyser);
    }

    /// <summary>
    /// Registers a set of vocabularies with the registry.
    /// </summary>
    /// <param name="vocabularies">The vocabularies to register.</param>
    public void RegisterVocabularies(params IVocabulary[] vocabularies)
    {
        foreach (IVocabulary vocabulary in vocabularies)
        {
#if NET8_0_OR_GREATER
            this.vocabularies.TryAdd(vocabulary.Uri, vocabulary);
#else
            if (!this.vocabularies.ContainsKey(vocabulary.Uri))
            {
                this.vocabularies.Add(vocabulary.Uri, vocabulary);
            }
#endif
        }
    }

    /// <summary>
    /// Gets the vocabulary for the given schema instance.
    /// </summary>
    /// <param name="vocabularyUri">The vocabulary URI.</param>
    /// <param name="vocabulary">The <see cref="IVocabulary"/> with the given URI.</param>
    /// <returns><see langword="true"/> when the vocabulary is found.</returns>
    public bool TryGetVocabulary(string vocabularyUri, [NotNullWhen(true)] out IVocabulary? vocabulary)
    {
        return this.vocabularies.TryGetValue(vocabularyUri, out vocabulary);
    }

    /// <summary>
    /// Gets the standard vocabulary for the given schema dialect.
    /// </summary>
    /// <param name="schemaUri">The schema URI.</param>
    /// <param name="vocabulary">The <see cref="IVocabulary"/> with the given URI.</param>
    /// <returns><see langword="true"/> when the vocabulary is found.</returns>
    public bool TryGetSchemaDialect(string schemaUri, [NotNullWhen(true)] out IVocabulary? vocabulary)
    {
        foreach (IVocabularyAnalyser analyser in this.analysers)
        {
            if (analyser.TryGetVocabulary(schemaUri) is IVocabulary v)
            {
                vocabulary = v;
                return true;
            }
        }

        vocabulary = null;
        return false;
    }

    /// <summary>
    /// Analyses a schema element to discover its vocabulary.
    /// </summary>
    /// <param name="schemaElement">The schema element to analyse.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> which, when complete, provides the <see cref="IVocabulary"/> or <see langword="null"/> if
    /// no vocabulary could be found for the schema.</returns>
    public async ValueTask<IVocabulary?> AnalyseSchema(JsonElement schemaElement)
    {
        foreach (IVocabularyAnalyser analyser in this.analysers)
        {
            if (await analyser.TryGetVocabulary(schemaElement) is IVocabulary vocabulary)
            {
                return vocabulary;
            }
        }

        return null;
    }
}