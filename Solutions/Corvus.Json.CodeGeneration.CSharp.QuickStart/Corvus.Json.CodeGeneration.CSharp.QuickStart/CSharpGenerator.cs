// <copyright file="CSharpGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration.DocumentResolvers;

namespace Corvus.Json.CodeGeneration.CSharp.QuickStart;

/// <summary>
/// A quickstart generator for C# with default vocabulary analyzers.
/// </summary>
public sealed class CSharpGenerator
{
    private readonly PrepopulatedDocumentResolver metaschemaDocumentResolver;
    private readonly VocabularyRegistry vocabularyRegistry;

    private readonly CompoundDocumentResolver documentResolver;
    private readonly IVocabulary fallbackVocabulary;
    private readonly CSharpLanguageProvider languageProvider;

    private CSharpGenerator(IDocumentResolver documentResolver, IVocabulary fallbackVocabulary, CSharpLanguageProvider languageProvider)
    {
        this.metaschemaDocumentResolver = CreateMetaschemaDocumentResolver();
        this.vocabularyRegistry = RegisterVocabularies(this.metaschemaDocumentResolver);
        this.documentResolver = CompoundWithMetaschemaResolver(this.metaschemaDocumentResolver, documentResolver);
        this.fallbackVocabulary = fallbackVocabulary;
        this.languageProvider = languageProvider;
    }

    /// <summary>
    /// Create a new instance of the <see cref="CSharpGenerator"/>.
    /// </summary>
    /// <param name="fallbackVocabulary">The (optional) fallback vocabulary. This will be 2020-12 if null.</param>
    /// <param name="options">The language provider options.</param>
    /// <returns>An instance of the <see cref="CSharpGenerator"/> to use with the <see cref="FileSystemDocumentResolver"/> and <see cref="HttpClientDocumentResolver"/>.</returns>
    public static CSharpGenerator Create(IVocabulary? fallbackVocabulary = null, CSharpLanguageProvider.Options? options = null)
    {
        return new(
            new CompoundDocumentResolver(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient())),
            fallbackVocabulary ?? Draft202012.VocabularyAnalyser.DefaultVocabulary,
            CSharpLanguageProvider.DefaultWithOptions(options ?? CSharpLanguageProvider.Options.Default));
    }

    /// <summary>
    /// Create a new instance of the <see cref="CSharpGenerator"/>.
    /// </summary>
    /// <param name="resolver">An additional document resolver to use.</param>
    /// <param name="fallbackVocabulary">The (optional) fallback vocabulary. This will be 2020-12 if null.</param>
    /// <param name="options">The language provider options.</param>
    /// <returns>An instance of the <see cref="CSharpGenerator"/> to use with the document resolver.</returns>
    public static CSharpGenerator Create(PrepopulatedDocumentResolver resolver, IVocabulary? fallbackVocabulary = null, CSharpLanguageProvider.Options? options = null)
    {
        return new(
            resolver,
            fallbackVocabulary ?? Draft202012.VocabularyAnalyser.DefaultVocabulary,
            CSharpLanguageProvider.DefaultWithOptions(options ?? CSharpLanguageProvider.Options.Default));
    }

    /// <summary>
    /// Create a new instance of the <see cref="CSharpGenerator"/>.
    /// </summary>
    /// <param name="baseUriResolver">A callback to resolve base URIs.</param>
    /// <param name="fallbackVocabulary">The (optional) fallback vocabulary. This will be 2020-12 if null.</param>
    /// <param name="options">The language provider options.</param>
    /// <returns>An instance of the <see cref="CSharpGenerator"/> to use with the document resolver.</returns>
    public static CSharpGenerator Create(BaseUriResolver baseUriResolver, IVocabulary? fallbackVocabulary = null, CSharpLanguageProvider.Options? options = null)
    {
        return new(
            new CallbackDocumentResolver(baseUriResolver),
            fallbackVocabulary ?? Draft202012.VocabularyAnalyser.DefaultVocabulary,
            CSharpLanguageProvider.DefaultWithOptions(options ?? CSharpLanguageProvider.Options.Default));
    }

    /// <summary>
    /// Generate files for the given schema references.
    /// </summary>
    /// <param name="reference">The schema reference for which to generate files.</param>
    /// <param name="cancellationToken">The (optional) cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the <see cref="IReadOnlyCollection{GeneratedCodeFile}"/>.</returns>
    public async ValueTask<IReadOnlyCollection<GeneratedCodeFile>> GenerateFilesAsync(JsonReference reference, CancellationToken? cancellationToken = null)
    {
        return await this.GenerateFilesAsync([reference], cancellationToken);
    }

    /// <summary>
    /// Generate files for the given schema references.
    /// </summary>
    /// <param name="references">The schema references for which to generate files.</param>
    /// <param name="cancellationToken">The (optional) cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the <see cref="IReadOnlyCollection{GeneratedCodeFile}"/>.</returns>
    public async ValueTask<IReadOnlyCollection<GeneratedCodeFile>> GenerateFilesAsync(JsonReference[] references, CancellationToken? cancellationToken = null)
    {
        JsonSchemaTypeBuilder builder = new(this.documentResolver, this.vocabularyRegistry);
        List<TypeDeclaration> rootTypeDeclarations = [];

        foreach (JsonReference reference in references)
        {
            TypeDeclaration rtd = await builder.AddTypeDeclarationsAsync(
                reference,
                this.fallbackVocabulary,
                false,
                cancellationToken ?? CancellationToken.None);
            rootTypeDeclarations.Add(rtd);
        }

        return builder.GenerateCodeUsing(this.languageProvider, cancellationToken ?? CancellationToken.None, [.. rootTypeDeclarations]);
    }

    private static PrepopulatedDocumentResolver CreateMetaschemaDocumentResolver()
    {
        PrepopulatedDocumentResolver documentResolver = new();

        // Add support for the meta schemas we are interested in.
        documentResolver.AddDocument(JsonSchema.Draft4.MetaSchema.Instance);
        documentResolver.AddMetaschema();

        return documentResolver;
    }

    private static VocabularyRegistry RegisterVocabularies(IDocumentResolver documentResolver)
    {
        VocabularyRegistry vocabularyRegistry = new();

        // Add support for the vocabularies we are interested in.
        CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // And register the custom vocabulary for Corvus extensions.
        vocabularyRegistry.RegisterVocabularies(
            CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

        return vocabularyRegistry;
    }

    private static CompoundDocumentResolver CompoundWithMetaschemaResolver(IDocumentResolver metaschemaDocumentResolver, IDocumentResolver additionalResolver)
    {
        return new(additionalResolver, metaschemaDocumentResolver);
    }
}