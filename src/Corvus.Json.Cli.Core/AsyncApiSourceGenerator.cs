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

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Generates the AsyncAPI producers/consumers + message schema models for a single specification file
/// and returns the channel descriptors — the pieces an Arazzo workflow generator needs to bind each
/// <c>channelPath</c> step to a generated producer/consumer. The AsyncAPI analogue of
/// <see cref="OpenApiSourceGenerator"/>; wraps the same schema-type generation the
/// <c>asyncapi-generate</c> command uses, without its console output or lock-file handling.
/// </summary>
internal static class AsyncApiSourceGenerator
{
    /// <summary>
    /// Generates the producers/consumers and models for one AsyncAPI source description and returns its
    /// channel operations.
    /// </summary>
    /// <param name="specFilePath">The absolute path to the AsyncAPI spec file.</param>
    /// <param name="rootNamespace">The root namespace for the generated code.</param>
    /// <param name="outputPath">The directory the producers/consumers are written to (models under <c>Models/</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One descriptor per channel operation the spec declares, with generated producer/consumer details.</returns>
    /// <exception cref="NotSupportedException">The spec is an AsyncAPI version that cannot yet describe channels for Arazzo (only 3.0).</exception>
    public static async Task<IReadOnlyList<AsyncApiChannelDescriptor>> GenerateAsync(
        string specFilePath,
        string rootNamespace,
        string outputPath,
        CancellationToken cancellationToken)
    {
        bool useYaml = IsYamlFile(specFilePath);
        byte[] specBytes = await File.ReadAllBytesAsync(specFilePath, cancellationToken).ConfigureAwait(false);
        if (useYaml)
        {
            specBytes = YamlToJson(specBytes);
        }

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;
        string version = AsyncApiShowCommand.DetectAsyncApiVersion(specRoot, null);

        // Only AsyncAPI 3.0 can describe its channel operations for Arazzo today (2.6 has no
        // DescribeChannelOperations surface yet).
        if (!AsyncApiShowCommand.IsAsyncApi30Version(version))
        {
            throw new NotSupportedException(
                $"AsyncAPI source '{Path.GetFileName(specFilePath)}' is version {version}; Arazzo channel steps require AsyncAPI 3.0.");
        }

        using AsyncApiExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(specRoot, null, referenceResolver);

        string modelsPath = Path.Combine(outputPath, "Models");
        Dictionary<string, string> schemaTypeMap = pointers.Length > 0
            ? await GenerateSchemaTypesAsync(specFilePath, rootNamespace, modelsPath, pointers, useYaml, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        AsyncApi30CodeGenerator generator = new(rootNamespace, schemaTypeMap);
        IReadOnlyList<GeneratedFile> clientFiles = generator.Generate(specRoot, null, referenceResolver);

        Directory.CreateDirectory(outputPath);
        foreach (GeneratedFile file in clientFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(outputPath, file.FileName), file.Content, cancellationToken).ConfigureAwait(false);
        }

        return generator.DescribeChannelOperations(specRoot, null, referenceResolver);
    }

    private static async Task<Dictionary<string, string>> GenerateSchemaTypesAsync(
        string specFilePath,
        string rootNamespace,
        string outputPath,
        string[] pointers,
        bool useYaml,
        CancellationToken cancellationToken)
    {
        CompoundDocumentResolver documentResolver;
        if (useYaml)
        {
            YamlPreProcessor preProcessor = new();
            documentResolver = new(new FileSystemDocumentResolver(preProcessor), new HttpClientDocumentResolver(new HttpClient(), preProcessor));
        }
        else
        {
            documentResolver = new(new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient()));
        }

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
            JsonReference reference = new(specFilePath, pointer);
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