// <copyright file="AsyncApiSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.CodeGenerator;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// Generates the AsyncAPI producers/consumers + message schema models for a single specification file
/// and returns the channel descriptors — the pieces an Arazzo workflow generator needs to bind each
/// <c>channelPath</c> step to a generated producer/consumer. The AsyncAPI analogue of
/// <see cref="OpenApiSourceGenerator"/>; wraps the same schema-type generation the
/// <c>asyncapi-generate</c> command uses, without its console output or lock-file handling.
/// </summary>
public static class AsyncApiSourceGenerator
{
    /// <summary>
    /// Generates the producers/consumers and models for one AsyncAPI source description and returns its
    /// channel operations.
    /// </summary>
    /// <param name="specFilePath">The absolute path to the AsyncAPI spec file.</param>
    /// <param name="rootNamespace">The root namespace for the generated code.</param>
    /// <param name="outputPath">The directory the producers/consumers are written to (models under <c>Models/</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="progress">An optional callback invoked with human-readable progress messages.</param>
    /// <returns>One descriptor per channel operation the spec declares, with generated producer/consumer details.</returns>
    /// <exception cref="NotSupportedException">The spec is an AsyncAPI version that cannot yet describe channels for Arazzo (only 3.0).</exception>
    public static async Task<IReadOnlyList<AsyncApiChannelDescriptor>> GenerateAsync(
        string specFilePath,
        string rootNamespace,
        string outputPath,
        CancellationToken cancellationToken,
        Action<string>? progress = null)
    {
        _ = progress; // reserved for parity with the OpenAPI source generator; no AsyncAPI progress messages yet
        bool useYaml = IsYamlFile(specFilePath);
        byte[] specBytes = await File.ReadAllBytesAsync(specFilePath, cancellationToken).ConfigureAwait(false);
        if (useYaml)
        {
            specBytes = YamlToJson(specBytes);
        }

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;
        using AsyncApiExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        return await GenerateCoreAsync(
            specRoot, referenceResolver, specFilePath, entryUri: null, documentLoader: null, useYaml,
            rootNamespace, outputPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates the producers/consumers and models for one AsyncAPI source description whose document —
    /// and any it references — are supplied through a virtualized loader rather than read from disk,
    /// returning its channel operations.
    /// </summary>
    /// <param name="specUri">The absolute URI the AsyncAPI spec was retrieved from (its reference-resolution base).</param>
    /// <param name="documentLoader">Loads a document's raw UTF-8 JSON bytes by absolute URI, or returns <see langword="null"/>.</param>
    /// <param name="rootNamespace">The root namespace for the generated code.</param>
    /// <param name="outputPath">The directory the producers/consumers are written to (models under <c>Models/</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="progress">An optional callback invoked with human-readable progress messages.</param>
    /// <returns>One descriptor per channel operation the spec declares.</returns>
    public static async Task<IReadOnlyList<AsyncApiChannelDescriptor>> GenerateAsync(
        Uri specUri,
        Func<Uri, byte[]?> documentLoader,
        string rootNamespace,
        string outputPath,
        CancellationToken cancellationToken,
        Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(specUri);
        ArgumentNullException.ThrowIfNull(documentLoader);

        byte[] specBytes = documentLoader(specUri)
            ?? throw new FileNotFoundException($"The AsyncAPI source document '{specUri}' could not be loaded.");

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;
        using AsyncApiExternalReferenceResolver referenceResolver = new(specRoot, specUri, documentLoader);

        return await GenerateCoreAsync(
            specRoot, referenceResolver, specUri.AbsoluteUri, specUri, documentLoader, useYaml: false,
            rootNamespace, outputPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<AsyncApiChannelDescriptor>> GenerateCoreAsync(
        JsonElement specRoot,
        AsyncApiExternalReferenceResolver referenceResolver,
        string schemaEntryKey,
        Uri? entryUri,
        Func<Uri, byte[]?>? documentLoader,
        bool useYaml,
        string rootNamespace,
        string outputPath,
        CancellationToken cancellationToken)
    {
        string version = AsyncApiSpecVersion.Detect(specRoot, null);

        bool is26 = AsyncApiSpecVersion.Is26(version);
        if (!is26 && !AsyncApiSpecVersion.Is30(version))
        {
            throw new NotSupportedException(
                $"AsyncAPI source '{schemaEntryKey}' is version {version}; Arazzo channel steps require AsyncAPI 2.6 or 3.0.");
        }

        string[] pointers = is26
            ? AsyncApi26CodeGenerator.CollectSchemaPointers(specRoot, null, referenceResolver)
            : AsyncApi30CodeGenerator.CollectSchemaPointers(specRoot, null, referenceResolver);

        string modelsPath = Path.Combine(outputPath, "Models");
        Dictionary<string, string> schemaTypeMap = pointers.Length > 0
            ? await GenerateSchemaTypesAsync(schemaEntryKey, rootNamespace, modelsPath, pointers, useYaml, entryUri, documentLoader, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        IReadOnlyList<GeneratedFile> clientFiles;
        IReadOnlyList<AsyncApiChannelDescriptor> channels;
        if (is26)
        {
            AsyncApi26CodeGenerator generator = new(rootNamespace, schemaTypeMap);
            clientFiles = generator.Generate(specRoot, null, referenceResolver);
            channels = generator.DescribeChannelOperations(specRoot, null, referenceResolver);
        }
        else
        {
            AsyncApi30CodeGenerator generator = new(rootNamespace, schemaTypeMap);
            clientFiles = generator.Generate(specRoot, null, referenceResolver);
            channels = generator.DescribeChannelOperations(specRoot, null, referenceResolver);
        }

        Directory.CreateDirectory(outputPath);
        foreach (GeneratedFile file in clientFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(outputPath, file.FileName), file.Content, cancellationToken).ConfigureAwait(false);
        }

        return channels;
    }

    private static async Task<Dictionary<string, string>> GenerateSchemaTypesAsync(
        string specFilePath,
        string rootNamespace,
        string outputPath,
        string[] pointers,
        bool useYaml,
        Uri? entryUri,
        Func<Uri, byte[]?>? documentLoader,
        CancellationToken cancellationToken)
    {
        // The entry document key. A non-file virtualized URI (http(s)/urn/$self) is handed to the V4
        // generator verbatim so its resolver matches the in-memory registry; a file URI is reduced to a
        // plain OS path (the V4 reference normalizer mangles a "file://" URI into "{cwd}/file:/..."); and
        // with no URI we use the supplied file path.
        string schemaEntryKey = entryUri is null
            ? specFilePath
            : entryUri.IsFile ? entryUri.LocalPath : entryUri.AbsoluteUri;

        IDocumentResolver fileResolver;
        IDocumentResolver httpResolver;
        if (useYaml)
        {
            YamlPreProcessor preProcessor = new();
            fileResolver = new FileSystemDocumentResolver(preProcessor);
            httpResolver = new HttpClientDocumentResolver(new HttpClient(), preProcessor);
        }
        else
        {
            fileResolver = new FileSystemDocumentResolver();
            httpResolver = new HttpClientDocumentResolver(new HttpClient());
        }

        CompoundDocumentResolver documentResolver = documentLoader is { } loader
            ? new(
                new CallbackDocumentResolver(uriString =>
                    Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri) && loader(uri) is { } bytes
                        ? System.Text.Json.JsonDocument.Parse(bytes)
                        : null),
                fileResolver,
                httpResolver)
            : new(fileResolver, httpResolver);

        documentResolver.AddMetaschema();

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // AsyncAPI uses JSON Schema Draft 7.
        IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary;

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string pointer in pointers)
        {
            // All AsyncAPI schema pointers are same-document fragment-only.
            JsonReference reference = new(schemaEntryKey, pointer);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference, defaultVocabulary, rebaseAsRoot: false).ConfigureAwait(false);
            pointerToType[pointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        CSharpLanguageProvider.Options options = new(rootNamespace + ".Models");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(AsyncApiSchemaNameHeuristic.Instance);
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        Directory.CreateDirectory(outputPath);

        HashSet<string> writtenFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            string outputFile = PathTruncator.TruncatePath(PathTruncator.NormalizePath(Path.Combine(outputPath, codeFile.FileName)));

            if (!writtenFiles.Add(outputFile))
            {
                string path = Path.GetDirectoryName(outputFile)!;
                string baseName = Path.GetFileNameWithoutExtension(outputFile);
                string extension = Path.GetExtension(outputFile);
                int counter = 1;
                do
                {
                    outputFile = PathTruncator.TruncatePath(Path.Combine(path, $"{baseName}{counter++}{extension}"));
                }
                while (!writtenFiles.Add(outputFile) && counter < 1000);
            }

            await File.WriteAllTextAsync(outputFile, codeFile.FileContent, cancellationToken).ConfigureAwait(false);
        }

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        foreach ((string pointerStr, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;
            if (reduced.HasDotnetTypeName())
            {
                schemaTypeMap[pointerStr] = reduced.FullyQualifiedDotnetTypeName();
            }

            AddChildTypesToMap(reduced, schemaTypeMap);
        }

        return schemaTypeMap;
    }

    private static void AddChildTypesToMap(TypeDeclaration parentType, Dictionary<string, string> schemaTypeMap)
    {
        HashSet<TypeDeclaration> visited = [];
        AddChildTypesToMapCore(parentType, schemaTypeMap, visited);
    }

    private static void AddChildTypesToMapCore(TypeDeclaration parentType, Dictionary<string, string> schemaTypeMap, HashSet<TypeDeclaration> visited)
    {
        foreach (TypeDeclaration child in parentType.Children())
        {
            TypeDeclaration reducedChild = child.ReducedTypeDeclaration().ReducedType;
            if (!visited.Add(reducedChild))
            {
                continue;
            }

            if (reducedChild.HasDotnetTypeName()
                && reducedChild.LocatedSchema.RootDocumentPointer is { Length: > 0 } rootPointer)
            {
                schemaTypeMap.TryAdd("#" + rootPointer, reducedChild.FullyQualifiedDotnetTypeName());
            }

            AddChildTypesToMapCore(reducedChild, schemaTypeMap, visited);
        }
    }

    private static byte[] YamlToJson(byte[] yamlBytes)
    {
        YamlPreProcessor preProcessor = new();
        using MemoryStream input = new(yamlBytes);
        using Stream processed = preProcessor.Process(input);
        using MemoryStream output = new();
        processed.CopyTo(output);
        return output.ToArray();
    }

    private static bool IsYamlFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

#endif