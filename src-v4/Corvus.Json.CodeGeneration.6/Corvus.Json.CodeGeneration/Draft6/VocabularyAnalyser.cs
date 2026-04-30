// <copyright file="VocabularyAnalyser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft6;

/// <summary>
/// A vocabulary analyser for Draft 6.
/// </summary>
public sealed class VocabularyAnalyser : IVocabularyAnalyser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VocabularyAnalyser"/> class.
    /// </summary>
    private VocabularyAnalyser()
    {
    }

    /// <summary>
    /// Gets the default vocabulary for the analyser.
    /// </summary>
    public static IVocabulary DefaultVocabulary => SchemaVocabulary.DefaultInstance;

    /// <summary>
    /// Register the vocabulary analyser.
    /// </summary>
    /// <param name="vocabularyRegistry">The vocabulary registry for which this is an analyser.</param>
    public static void RegisterAnalyser(VocabularyRegistry vocabularyRegistry)
    {
        VocabularyAnalyser analyser = new();
        vocabularyRegistry.RegisterAnalyser(analyser);
    }

    /// <inheritdoc/>
    public ValueTask<IVocabulary?> TryGetVocabulary(JsonElement schemaInstance)
    {
        if (schemaInstance.ValueKind != JsonValueKind.Object || !schemaInstance.TryGetProperty(DollarSchemaKeyword.Instance.KeywordUtf8, out JsonElement dollarSchema))
        {
            return new ValueTask<IVocabulary?>(default(IVocabulary?));
        }

        if (dollarSchema.ValueKind != JsonValueKind.String)
        {
            return new ValueTask<IVocabulary?>(default(IVocabulary?));
        }

        if (dollarSchema.ValueEquals(SchemaVocabulary.DefaultInstance.UriUtf8))
        {
            return new ValueTask<IVocabulary?>(SchemaVocabulary.DefaultInstance);
        }

        return new ValueTask<IVocabulary?>(default(IVocabulary?));
    }

    /// <inheritdoc/>
    public IVocabulary? TryGetVocabulary(string iri)
    {
        if (iri.Equals(SchemaVocabulary.DefaultInstance.Uri))
        {
            return SchemaVocabulary.DefaultInstance;
        }

        return null;
    }
}