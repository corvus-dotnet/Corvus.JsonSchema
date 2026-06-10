// <copyright file="OpenApiSchemaTypeGeneration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Shared logic for generating the JSON Schema model types referenced by an OpenAPI specification and
/// building the pointer → fully-qualified-type-name map the OpenAPI client generators consume. Used by
/// both the <c>openapi generate</c> command and the Arazzo workflow generator (which generates the
/// client + models for each of a workflow document's OpenAPI source descriptions).
/// </summary>
internal static class OpenApiSchemaTypeGeneration
{
    /// <summary>
    /// Generates the schema model types for the given schema references and returns the
    /// pointer → type-name map plus the generated file names.
    /// </summary>
    /// <param name="specFile">The OpenAPI spec file path.</param>
    /// <param name="specVersion">The detected spec version (<c>3.0</c>/<c>3.1</c>/<c>3.2</c>).</param>
    /// <param name="rootNamespace">The root namespace for generated models (models go under <c>&lt;rootNamespace&gt;.Models</c>).</param>
    /// <param name="outputPath">The directory the model files are written to.</param>
    /// <param name="schemaRefs">The schema references collected from the spec.</param>
    /// <param name="parameterNames">The parameter-name hints for the naming heuristic.</param>
    /// <param name="useYaml">Whether referenced documents may be YAML.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="entryUri">
    /// The absolute URI the entry spec was retrieved from, when it is virtualized (not a plain file path).
    /// When supplied it keys the entry document for reference resolution; when <see langword="null"/> the
    /// entry is keyed by its full file-system path (the standalone <c>openapi generate</c> behaviour).
    /// </param>
    /// <param name="documentLoader">
    /// An optional callback that loads a referenced document's raw UTF-8 JSON bytes by absolute URI (or
    /// <see langword="null"/> when it cannot). When supplied, virtualized documents (the entry doc and any
    /// external <c>$ref</c>s) resolve through it ahead of the file-system/HTTP fallback — so the schema
    /// models generate from documents registered in memory.
    /// </param>
    /// <returns>The schema type map and the generated model file names.</returns>
    public static async Task<(Dictionary<string, string> SchemaTypeMap, IReadOnlyList<string> GeneratedFileNames)> GenerateSchemaTypesAsync(
        string specFile,
        string specVersion,
        string rootNamespace,
        string outputPath,
        SchemaReference[] schemaRefs,
        Dictionary<string, string> parameterNames,
        bool useYaml,
        CancellationToken cancellationToken,
        Uri? entryUri = null,
        Func<Uri, byte[]?>? documentLoader = null)
    {
        // The entry document key: its virtualized URI when supplied, otherwise its full file-system path.
        string specFilePath = entryUri is not null ? entryUri.AbsoluteUri : Path.GetFullPath(specFile);

        // Set up the document resolver — virtualized documents (via the loader) first, then the file
        // system and HTTP, with YAML support when the spec is YAML.
        CompoundDocumentResolver documentResolver;

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

        if (documentLoader is { } loader)
        {
            // Bridge the byte loader into the V4 resolver world: the callback hands back a parsed
            // System.Text.Json document for any URI the loader can satisfy (the entry doc and any
            // registered external refs), so the schema-model pipeline never touches disk for them.
            documentResolver = new(
                new CallbackDocumentResolver(uriString =>
                    Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri) && loader(uri) is { } bytes
                        ? System.Text.Json.JsonDocument.Parse(bytes)
                        : null),
                fileResolver,
                httpResolver);
        }
        else
        {
            documentResolver = new(fileResolver, httpResolver);
        }

        documentResolver.AddMetaschema();

        // Register vocabularies
        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // Select vocabulary based on spec version
        IVocabulary defaultVocabulary = specVersion switch
        {
            "3.0" => Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            _ => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
        };

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        // Register each schema reference as a type declaration
        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (SchemaReference schemaRef in schemaRefs)
        {
            // Build the JsonReference from the resolvable pointer.
            // Fragment-only pointers (start with #) resolve against the entry spec file.
            // External pointers (contain a doc path before #) resolve against that external file.
            JsonReference reference;
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');

            if (hashIndex == 0)
            {
                // Fragment-only — resolve against entry document
                reference = new(specFilePath, schemaRef.ResolvablePointer);
            }
            else if (hashIndex > 0)
            {
                // External document + fragment
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                string fragment = schemaRef.ResolvablePointer[hashIndex..];
                string resolvedDocPath = ResolveDocumentPath(docPart);
                reference = new(resolvedDocPath, fragment);
            }
            else
            {
                // No fragment — entire external doc is the schema (unlikely but handled)
                string resolvedDocPath = ResolveDocumentPath(schemaRef.ResolvablePointer);
                reference = new(resolvedDocPath, "#");
            }

            AnsiConsole.MarkupLine($"  [dim]Registering schema:[/] {schemaRef.PositionalPointer}");

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference, defaultVocabulary, rebaseAsRoot: false)
                .ConfigureAwait(false);

            // Map by positional pointer — this is the key used by the client codegen
            pointerToType[schemaRef.PositionalPointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        AnsiConsole.MarkupLine($"[yellow]Registered {typesToGenerate.Count} type declarations, generating code...[/]");

        // Generate code — register OpenAPI naming heuristic for contextual inline schema names
        CSharpLanguageProvider.Options options = new(rootNamespace + ".Models");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(new OpenApiSchemaNameHeuristic(parameterNames));
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        AnsiConsole.MarkupLine($"[yellow]Code generation complete, writing {generatedCode.Count} files...[/]");

        // Write schema type files
        Directory.CreateDirectory(outputPath);

        HashSet<string> writtenFiles = new(StringComparer.OrdinalIgnoreCase);
        List<string> schemaFileNames = [];
        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            string filePath = TruncateFileNameIfRequired(outputPath, writtenFiles, codeFile);
            await File.WriteAllTextAsync(filePath, codeFile.FileContent, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [cyan]Schema type:[/] {filePath}");
            schemaFileNames.Add(Path.GetFileName(filePath));
        }

        AnsiConsole.MarkupLine($"[green]Generated {schemaFileNames.Count} schema type files[/]");

        // Build the pointer → fully qualified type name map
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach ((string pointerStr, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;

            if (reduced.HasDotnetTypeName())
            {
                schemaTypeMap[pointerStr] = reduced.FullyQualifiedDotnetTypeName();
            }

            // Also add child types (items, additionalProperties, etc.) so that
            // deeply nested header/parameter element types can be resolved.
            AddChildTypesToMap(reduced, schemaTypeMap);
        }

        return (schemaTypeMap, schemaFileNames);
    }

    /// <summary>
    /// Recursively walks child type declarations and adds their pointer mappings to the schema type map.
    /// This ensures sub-schema types (e.g., array items, additionalProperties) are resolvable by pointer.
    /// </summary>
    /// <param name="parentType">The parent type declaration.</param>
    /// <param name="schemaTypeMap">The map to populate.</param>
    public static void AddChildTypesToMap(TypeDeclaration parentType, Dictionary<string, string> schemaTypeMap)
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
                string key = "#" + rootPointer;

                // Don't overwrite existing entries (root pointers take precedence)
                schemaTypeMap.TryAdd(key, reducedChild.FullyQualifiedDotnetTypeName());
            }

            // Recurse into grandchildren
            AddChildTypesToMapCore(reducedChild, schemaTypeMap, visited);
        }
    }

    /// <summary>
    /// Extracts the document path from a resolvable pointer's document portion.
    /// </summary>
    /// <param name="docPart">The document portion of a reference (before the # fragment), already absolute.</param>
    /// <returns>The document path suitable for <see cref="JsonReference"/>.</returns>
    private static string ResolveDocumentPath(string docPart)
    {
        // Non-file absolute URIs (http://, https://, urn:, etc.) pass through directly.
        if (Uri.TryCreate(docPart, UriKind.Absolute, out Uri? uri)
            && !uri.IsFile)
        {
            return docPart;
        }

        // Absolute file path — normalize separators
        return Path.GetFullPath(docPart);
    }

    private static string TruncateFileNameIfRequired(string outputPath, HashSet<string> writtenFiles, GeneratedCodeFile generatedCodeFile)
    {
        string outputFile = Path.Combine(outputPath, generatedCodeFile.FileName);
        string originalFileName = PathTruncator.NormalizePath(outputFile);
        outputFile = PathTruncator.TruncatePath(originalFileName);
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

            if (counter == 1000)
            {
                throw new InvalidOperationException("Unexpected duplicate file generated.");
            }
        }

        return outputFile;
    }
}

#endif