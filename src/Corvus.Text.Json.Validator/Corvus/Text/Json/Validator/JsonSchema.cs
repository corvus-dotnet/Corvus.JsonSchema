// <copyright file="JsonSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// A JSON schema for validation.
/// </summary>
public readonly struct JsonSchema
{
    private static readonly PrepopulatedDocumentResolver MetaschemaDocumentResolver = CreateMetaschemaDocumentResolver();
    private static readonly ConcurrentDictionary<string, ValidatorPipeline> CachedSchema = [];

    private readonly ValidatorPipeline pipeline;

    private JsonSchema(ValidatorPipeline pipeline)
    {
        this.pipeline = pipeline;
    }

    /// <summary>
    /// Create an instance of a JSON schema from a JSON document string.
    /// </summary>
    /// <param name="text">The text for the document.</param>
    /// <param name="canonicalUri">The canonical URI for the document. If
    /// <see langword="null"/> then an attempt will be made to find the canonical URI in the schema.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <exception cref="InvalidOperationException">No canonical URI could be found for the schema document.</exception>
    public static JsonSchema FromText(string text, string? canonicalUri = null, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        var document = System.Text.Json.JsonDocument.Parse(text);

        if (canonicalUri is null && !TryGetCanonicalUri(document, out canonicalUri))
        {
            throw new InvalidOperationException("The document does not have a canonical URI and one was not provided.");
        }

        string cacheKey = BuildCacheKey(canonicalUri!, options.AlwaysAssertFormat);

        if (!refreshCache && CachedSchema.TryGetValue(cacheKey, out ValidatorPipeline? cached))
        {
            return new(cached);
        }

        if (refreshCache)
        {
            CachedSchema.TryRemove(cacheKey, out _);
        }

        PrepopulatedDocumentResolver documentResolver = new();
        documentResolver.AddDocument(canonicalUri!, document);
        return FromCore(canonicalUri!, cacheKey, CompoundWithMetaschemaResolver(documentResolver, options), options);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a stream containing a JSON document.
    /// </summary>
    /// <param name="stream">The stream containing the document.</param>
    /// <param name="canonicalUri">The canonical URI for the document. If
    /// <see langword="null"/> then an attempt will be made to find the canonical URI in the schema.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <exception cref="InvalidOperationException">No canonical URI could be found for the schema document.</exception>
    public static JsonSchema FromStream(Stream stream, string? canonicalUri = null, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        var document = System.Text.Json.JsonDocument.Parse(stream);

        if (canonicalUri is null && !TryGetCanonicalUri(document, out canonicalUri))
        {
            throw new InvalidOperationException("The document does not have a canonical URI and one was not provided.");
        }

        string cacheKey = BuildCacheKey(canonicalUri!, options.AlwaysAssertFormat);

        if (!refreshCache && CachedSchema.TryGetValue(cacheKey, out ValidatorPipeline? cached))
        {
            return new(cached);
        }

        if (refreshCache)
        {
            CachedSchema.TryRemove(cacheKey, out _);
        }

        PrepopulatedDocumentResolver documentResolver = new();
        documentResolver.AddDocument(canonicalUri!, document);
        return FromCore(canonicalUri!, cacheKey, CompoundWithMetaschemaResolver(documentResolver, options), options);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a file.
    /// </summary>
    /// <param name="fileName">The path to the schema file.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    public static JsonSchema FromFile(string fileName, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(fileName, out string? normalized))
        {
            fileName = normalized;
        }

        string cacheKey = BuildCacheKey(fileName, options.AlwaysAssertFormat);

        if (!refreshCache && CachedSchema.TryGetValue(cacheKey, out ValidatorPipeline? cached))
        {
            return new(cached);
        }

        if (refreshCache)
        {
            CachedSchema.TryRemove(cacheKey, out _);
        }

        PrepopulatedDocumentResolver documentResolver = new();
        documentResolver.AddDocument(fileName, System.Text.Json.JsonDocument.Parse(File.ReadAllText(fileName)));
        return FromCore(fileName, cacheKey, CompoundWithMetaschemaResolver(documentResolver, options), options);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a URI.
    /// </summary>
    /// <param name="jsonSchemaUri">The URI of the schema.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    public static JsonSchema FromUri(string jsonSchemaUri, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        string cacheKey = BuildCacheKey(jsonSchemaUri, options.AlwaysAssertFormat);

        if (!refreshCache && CachedSchema.TryGetValue(cacheKey, out ValidatorPipeline? cached))
        {
            return new(cached);
        }

        if (refreshCache)
        {
            CachedSchema.TryRemove(cacheKey, out _);
        }

        return FromCore(jsonSchemaUri, cacheKey, CompoundWithMetaschemaResolver(null, options), options);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a URI, resolving via all configured resolvers.
    /// </summary>
    /// <param name="jsonSchemaUri">The URI of the schema.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    public static JsonSchema From(string jsonSchemaUri, Options? options = null, bool refreshCache = false)
    {
        return FromUri(jsonSchemaUri, options, refreshCache);
    }

    /// <summary>
    /// Validate a JSON string against this schema.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(json, resultsCollector);
    }

    /// <summary>
    /// Validate UTF-8 JSON bytes against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(utf8Json, resultsCollector);
    }

    /// <summary>
    /// Validate JSON characters against this schema.
    /// </summary>
    /// <param name="json">The JSON characters to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(json, resultsCollector);
    }

    /// <summary>
    /// Validate a UTF-8 JSON stream against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON stream to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(utf8Json, resultsCollector);
    }

    /// <summary>
    /// Validate a UTF-8 JSON byte sequence against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte sequence to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(utf8Json, resultsCollector);
    }

    /// <summary>
    /// Validate a pre-parsed <see cref="JsonElement"/> against this schema.
    /// </summary>
    /// <param name="element">The JSON element to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.pipeline.Validate(element, resultsCollector);
    }

    private static string BuildCacheKey(string uri, bool alwaysAssertFormat)
    {
        return $"{uri}__{alwaysAssertFormat}";
    }

    private static JsonSchema FromCore(string jsonSchemaUri, string cacheKey, CompoundDocumentResolver documentResolver, Options options)
    {
        VocabularyRegistry vocabularyRegistry = RegisterVocabularies(documentResolver);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        TypeDeclaration rootType =
            typeBuilder.AddTypeDeclarations(
                new JsonReference(jsonSchemaUri),
                options.FallbackVocabulary);

        // Check for built-in types before code generation
        if (rootType.IsBuiltInJsonAnyType())
        {
            var alwaysTruePipeline = ValidatorPipeline.Create(isAlwaysTrue: true, isAlwaysFalse: false);
            return new(CachedSchema.GetOrAdd(cacheKey, alwaysTruePipeline));
        }

        if (rootType.IsBuiltInJsonNotAnyType())
        {
            var alwaysFalsePipeline = ValidatorPipeline.Create(isAlwaysTrue: false, isAlwaysFalse: true);
            return new(CachedSchema.GetOrAdd(cacheKey, alwaysFalsePipeline));
        }

        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(
                CSharpLanguageProvider.DefaultWithOptions(
                    new CSharpLanguageProvider.Options(
                        "Corvus.Text.Json.Validator.GeneratedTypes",
                        alwaysAssertFormat: options.AlwaysAssertFormat)),
                CancellationToken.None,
                rootType);

        string rootTypeName = CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(rootType.ReducedTypeDeclaration().ReducedType);

        Type generatedType = DynamicCompiler.CompileGeneratedType(
            rootTypeName,
            generatedCode,
            options.HostAssembly);

        DynamicJsonType dynamicType = new(generatedType);
        var pipeline = ValidatorPipeline.Create(dynamicType);
        return new(CachedSchema.GetOrAdd(cacheKey, pipeline));
    }

    private static bool TryGetCanonicalUri(System.Text.Json.JsonDocument document, out string? canonicalUri)
    {
        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
            document.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement value) &&
            value.ValueKind == System.Text.Json.JsonValueKind.String &&
            value.GetString() is string id)
        {
            canonicalUri = id;
            return true;
        }

        canonicalUri = null;
        return false;
    }

    private static PrepopulatedDocumentResolver CreateMetaschemaDocumentResolver()
    {
        PrepopulatedDocumentResolver result = new();
        result.AddMetaschema();
        return result;
    }

    private static VocabularyRegistry RegisterVocabularies(IDocumentResolver documentResolver)
    {
        VocabularyRegistry vocabularyRegistry = new();

        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        vocabularyRegistry.RegisterVocabularies(Corvus.Json.CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);
        return vocabularyRegistry;
    }

    private static CompoundDocumentResolver CompoundWithMetaschemaResolver(IDocumentResolver? additionalResolver, Options options)
    {
        List<IDocumentResolver> resolvers = [];

        if (additionalResolver is not null)
        {
            resolvers.Add(additionalResolver);
        }

        RegisterAdditionalFiles(resolvers, options);

        resolvers.Add(MetaschemaDocumentResolver);

        if (options.AllowFileSystemAndHttpResolution)
        {
            resolvers.Add(new FileSystemDocumentResolver());
            resolvers.Add(new HttpClientDocumentResolver(new HttpClient()));
        }

        return new([.. resolvers]);
    }

    private static void RegisterAdditionalFiles(List<IDocumentResolver> resolvers, Options options)
    {
        if (options.AdditionalSchemaFiles is not { Count: > 0 })
        {
            return;
        }

        PrepopulatedDocumentResolver additionalFilesResolver = new();
        foreach (AdditionalSchemaFile file in options.AdditionalSchemaFiles)
        {
            var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file.FilePath));
            string canonicalUri = file.CanonicalUri;

            additionalFilesResolver.AddDocument(canonicalUri, document);

            // Also register by $id if present and different from the canonical URI
            if (document.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement) &&
                idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                idElement.GetString() is string id &&
                !string.Equals(id, canonicalUri, StringComparison.Ordinal))
            {
                additionalFilesResolver.AddDocument(id, document);
            }

            // Also register by the resolved file path if different
            string resolvedPath = Path.GetFullPath(file.FilePath);
            if (!string.Equals(resolvedPath, canonicalUri, StringComparison.Ordinal))
            {
                additionalFilesResolver.AddDocument(resolvedPath, document);
            }
        }

        resolvers.Add(additionalFilesResolver);
    }

    /// <summary>
    /// Options for validation.
    /// </summary>
    public sealed class Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Options"/> class.
        /// </summary>
        /// <param name="additionalSchemaFiles">Additional schema files to preload into the document resolver.</param>
        /// <param name="allowFileSystemAndHttpResolution">If <see langword="true"/> then FileSystem and HttpClient document resolvers will be available.</param>
        /// <param name="fallbackVocabulary">The fallback vocabulary (defaults to <see cref="Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary"/>).</param>
        /// <param name="alwaysAssertFormat">If <see langword="true"/>, <c>format</c> will always be asserted, even for vocabularies that usually annotate.</param>
        /// <param name="hostAssembly">The host assembly whose compilation context provides metadata references. Defaults to the entry assembly.</param>
        public Options(
            IReadOnlyList<AdditionalSchemaFile>? additionalSchemaFiles = null,
            bool allowFileSystemAndHttpResolution = true,
            IVocabulary? fallbackVocabulary = null,
            bool alwaysAssertFormat = true,
            Assembly? hostAssembly = null)
        {
            this.AdditionalSchemaFiles = additionalSchemaFiles;
            this.AllowFileSystemAndHttpResolution = allowFileSystemAndHttpResolution;
            this.FallbackVocabulary = fallbackVocabulary ?? Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
            this.AlwaysAssertFormat = alwaysAssertFormat;
            this.HostAssembly = hostAssembly ?? Assembly.GetEntryAssembly() ?? typeof(JsonSchema).Assembly;
        }

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new();

        /// <summary>
        /// Gets the additional schema files to preload.
        /// </summary>
        public IReadOnlyList<AdditionalSchemaFile>? AdditionalSchemaFiles { get; }

        /// <summary>
        /// Gets a value indicating whether FileSystem and HttpClient document resolvers will be available.
        /// </summary>
        public bool AllowFileSystemAndHttpResolution { get; }

        /// <summary>
        /// Gets the fallback vocabulary.
        /// </summary>
        public IVocabulary FallbackVocabulary { get; }

        /// <summary>
        /// Gets a value indicating whether <c>format</c> will always be asserted, even for vocabularies that usually annotate.
        /// </summary>
        public bool AlwaysAssertFormat { get; }

        /// <summary>
        /// Gets the host assembly whose compilation context provides metadata references.
        /// </summary>
        public Assembly HostAssembly { get; }
    }
}