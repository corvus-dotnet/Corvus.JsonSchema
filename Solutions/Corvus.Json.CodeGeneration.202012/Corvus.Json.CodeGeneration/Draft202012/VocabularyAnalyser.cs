// <copyright file="VocabularyAnalyser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft202012;

/// <summary>
/// A vocabulary analyser for Draft 2020-12.
/// </summary>
public sealed class VocabularyAnalyser : IVocabularyAnalyser
{
    private readonly IDocumentResolver documentResolver;
    private readonly VocabularyRegistry vocabularyRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="VocabularyAnalyser"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver for the vocabulary resolver.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry for which this is an analyser.</param>
    private VocabularyAnalyser(IDocumentResolver documentResolver, VocabularyRegistry vocabularyRegistry)
    {
        this.documentResolver = documentResolver;
        this.vocabularyRegistry = vocabularyRegistry;
    }

    /// <summary>
    /// Gets the default vocabulary for the analyser.
    /// </summary>
    public static IVocabulary DefaultVocabulary => SchemaVocabulary.DefaultInstance;

    /// <summary>
    /// Register the vocabulary analyser.
    /// </summary>
    /// <param name="documentResolver">The document resolver for the vocabulary resolver.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry for which this is an analyser.</param>
    public static void RegisterAnalyser(IDocumentResolver documentResolver, VocabularyRegistry vocabularyRegistry)
    {
        VocabularyAnalyser analyser = new(documentResolver, vocabularyRegistry);
        vocabularyRegistry.RegisterAnalyser(analyser);
        vocabularyRegistry.RegisterVocabularies(
            SchemaVocabulary.Core.Instance,
            SchemaVocabulary.Applicator.Instance,
            SchemaVocabulary.Content.Instance,
            SchemaVocabulary.FormatAnnotation.Instance,
            SchemaVocabulary.FormatAssertion.Instance,
            SchemaVocabulary.MetaData.Instance,
            SchemaVocabulary.Unevaluated.Instance,
            SchemaVocabulary.Validation.Instance);
    }

    /// <inheritdoc/>
    public async ValueTask<IVocabulary?> TryGetVocabulary(JsonElement schemaInstance)
    {
        return await TryGetVocabularyCore(
            this.documentResolver,
            this.vocabularyRegistry,
            schemaInstance,
            AnalyseChild);
    }

    private static async ValueTask<IVocabulary?> AnalyseChild(IDocumentResolver documentResolver, VocabularyRegistry vocabularyRegistry, JsonReference childSchema)
    {
        JsonElement? childSchemaInstance = await documentResolver.TryResolve(childSchema);
        if (childSchemaInstance is null)
        {
            return default;
        }

        // We use a null child analyser in the child, rather than recursing
        // as vocabs do not support deeper nesting.
        return await TryGetVocabularyCore(
            documentResolver,
            vocabularyRegistry,
            childSchemaInstance.Value,
            null);
    }

    private static async ValueTask<IVocabulary?> TryGetVocabularyCore(IDocumentResolver documentResolver, VocabularyRegistry vocabularyRegistry, JsonElement schemaInstance, Func<IDocumentResolver, VocabularyRegistry, JsonReference, ValueTask<IVocabulary?>>? processUnknownSchema)
    {
        if (schemaInstance.ValueKind != JsonValueKind.Object || !schemaInstance.TryGetProperty(DollarSchemaKeyword.Instance.KeywordUtf8, out JsonElement dollarSchema))
        {
            return default;
        }

        if (dollarSchema.ValueKind != JsonValueKind.String)
        {
            return default;
        }

        if (dollarSchema.ValueEquals(SchemaVocabulary.DefaultInstance.UriUtf8))
        {
            IVocabulary? vocabulary = ProcessKnownSchemaObject(vocabularyRegistry, schemaInstance);
            if (vocabulary is IVocabulary v)
            {
                return v;
            }
        }

        if (processUnknownSchema is not null)
        {
            if (dollarSchema.GetString() is string s)
            {
                IVocabulary? result = await processUnknownSchema(documentResolver, vocabularyRegistry, new(s));
                if (result is IVocabulary v)
                {
                    return v;
                }
            }
        }

        return default;
    }

    private static SchemaVocabulary? ProcessKnownSchemaObject(VocabularyRegistry registry, JsonElement schemaInstance)
    {
        // Do we have a $vocabulary
        if (schemaInstance.TryGetProperty(DollarVocabularyKeyword.Instance.KeywordUtf8, out JsonElement vocabularyKeyword) &&
            vocabularyKeyword.ValueKind == JsonValueKind.Object)
        {
            List<IVocabulary> vocabularies = [];

            foreach (JsonProperty property in vocabularyKeyword.EnumerateObject())
            {
                if (registry.TryGetVocabulary(property.Name, out IVocabulary? vocab))
                {
                    vocabularies.Add(vocab);
                }
                else if (property.Value.ValueKind != JsonValueKind.False)
                {
                    // This was not "relaxed" and we don't have a vocabulary that understands this
                    // so we will have to give up.
                    return default;
                }
            }

            return new SchemaVocabulary([.. vocabularies]);
        }

        return SchemaVocabulary.DefaultInstance;
    }
}