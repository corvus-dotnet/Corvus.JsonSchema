// <copyright file="OpenApiServerCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating API server stubs from an OpenAPI specification.
/// </summary>
/// <remarks>
/// This generates ASP.NET Minimal API server stubs including handler interfaces,
/// parameter structs, result types, and endpoint registration extension methods.
/// </remarks>
internal sealed class OpenApiServerCommand : AsyncCommand<OpenApiGenerateSettings>
{
    /// <inheritdoc/>
    protected override async Task<int> ExecuteAsync(CommandContext context, OpenApiGenerateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SpecFile);

        // Resolve the spec URL — explicit --spec-url takes priority, then lock file descriptionLocation
        string? specUrl = settings.SpecUrl;
        if (specUrl is null)
        {
            string outputPathForLockCheck = settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Generated");
            if (OpenApiLockFile.TryLoad(outputPathForLockCheck, out OpenApiLockFileModel lockCheck)
                && lockCheck.DescriptionLocation.IsNotUndefined())
            {
                specUrl = lockCheck.DescriptionLocation.GetString();
            }
        }

        // If we have a spec URL (either explicit or from lock file), fetch and overwrite the local file
        if (specUrl is not null)
        {
            AnsiConsole.MarkupLine($"[green]Fetching spec from:[/] {specUrl}");
            using HttpClient httpClient = new();
            byte[] remoteBytes = await httpClient.GetByteArrayAsync(specUrl, cancellationToken)
                .ConfigureAwait(false);
            string dir = Path.GetDirectoryName(Path.GetFullPath(settings.SpecFile))!;
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(settings.SpecFile, remoteBytes, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"[green]Saved to:[/] {settings.SpecFile}");
        }

        if (!File.Exists(settings.SpecFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Spec file not found: {settings.SpecFile}");
            return 1;
        }

        string rootNamespace = settings.RootNamespace ?? "GeneratedApi";
        string outputPath = settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Generated");
        string specFilePath = Path.GetFullPath(settings.SpecFile);

        // Read and parse the spec
        byte[] specBytes = await File.ReadAllBytesAsync(settings.SpecFile, cancellationToken)
            .ConfigureAwait(false);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;

        // Detect or use specified version
        string specVersion = OpenApiCommandHelpers.DetectSpecVersion(specRoot, settings.SpecVersion);

        // Build filter from --include-path / --exclude-path / --filter
        OperationFilter? filter = OpenApiCommandHelpers.BuildFilter(settings);

        // Create the external reference resolver — supports local, relative, and absolute $ref
        using ExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        IReadOnlyList<GeneratedFile> files;
        string modelsPath = Path.Combine(outputPath, "Models");
        IReadOnlyList<string>? modelFileNames = null;

        if (specVersion is "3.2")
        {
            SchemaReference[] schemaRefs = OpenApi32CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

            AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

            Dictionary<string, string>? schemaTypeMap = null;
            IReadOnlySet<string>? contextBodies = null;
            if (schemaRefs.Length > 0)
            {
                (schemaTypeMap, contextBodies, modelFileNames) = await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            OpenApi32CodeGenerator generator = new(
                rootNamespace,
                schemaTypeMap ?? new Dictionary<string, string>(),
                settings.ClientName,
                settings.IgnoreEmptyFormUrlEncodedBody,
                contextBodies);
            files = generator.GenerateServer(specRoot, filter, referenceResolver);
        }
        else if (specVersion is "3.1" or not "3.0")
        {
            SchemaReference[] schemaRefs = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

            AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

            Dictionary<string, string>? schemaTypeMap = null;
            IReadOnlySet<string>? contextBodies = null;
            if (schemaRefs.Length > 0)
            {
                (schemaTypeMap, contextBodies, modelFileNames) = await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            OpenApi31CodeGenerator generator = new(
                rootNamespace,
                schemaTypeMap ?? new Dictionary<string, string>(),
                settings.ClientName,
                settings.IgnoreEmptyFormUrlEncodedBody,
                contextBodies);
            files = generator.GenerateServer(specRoot, filter, referenceResolver);
        }
        else
        {
            SchemaReference[] schemaRefs = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

            AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

            Dictionary<string, string>? schemaTypeMap = null;
            IReadOnlySet<string>? contextBodies = null;
            if (schemaRefs.Length > 0)
            {
                (schemaTypeMap, contextBodies, modelFileNames) = await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            OpenApi30CodeGenerator generator = new(
                rootNamespace,
                schemaTypeMap ?? new Dictionary<string, string>(),
                settings.ClientName,
                settings.IgnoreEmptyFormUrlEncodedBody,
                contextBodies);
            files = generator.GenerateServer(specRoot, filter, referenceResolver);
        }

        AnsiConsole.MarkupLine($"[green]Server files:[/] {files.Count}");

        // Write server files
        Directory.CreateDirectory(outputPath);

        List<string> generatedFileNames = [];
        foreach (GeneratedFile file in files)
        {
            string filePath = Path.Combine(outputPath, file.FileName);
            await File.WriteAllTextAsync(filePath, file.Content, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {filePath}");
            generatedFileNames.Add(file.FileName);
        }

        // Include model files with relative Models/ prefix
        if (modelFileNames is not null)
        {
            foreach (string modelFile in modelFileNames)
            {
                generatedFileNames.Add(Path.Combine("Models", modelFile));
            }
        }

        AnsiConsole.MarkupLine($"[green]Generated {generatedFileNames.Count} files ({files.Count} server + {modelFileNames?.Count ?? 0} model) in {outputPath}[/]");

        return 0;
    }

    private static async Task<(Dictionary<string, string> SchemaTypeMap, IReadOnlySet<string> ContextSourceBodyPointers, IReadOnlyList<string> GeneratedFileNames)> GenerateSchemaTypesAsync(
        string specFile,
        string specVersion,
        string rootNamespace,
        string outputPath,
        SchemaReference[] schemaRefs,
        Dictionary<string, string> parameterNames,
        CancellationToken cancellationToken)
    {
        string specFilePath = Path.GetFullPath(specFile);

        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver(),
            new HttpClientDocumentResolver(new HttpClient()));

        documentResolver.AddMetaschema();

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        IVocabulary defaultVocabulary = specVersion switch
        {
            "3.0" => Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            _ => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
        };

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (SchemaReference schemaRef in schemaRefs)
        {
            JsonReference reference;
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');

            if (hashIndex == 0)
            {
                reference = new(specFilePath, schemaRef.ResolvablePointer);
            }
            else if (hashIndex > 0)
            {
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                string fragment = schemaRef.ResolvablePointer[hashIndex..];
                string resolvedDocPath = ResolveDocumentPath(docPart);
                reference = new(resolvedDocPath, fragment);
            }
            else
            {
                string resolvedDocPath = ResolveDocumentPath(schemaRef.ResolvablePointer);
                reference = new(resolvedDocPath, "#");
            }

            AnsiConsole.MarkupLine($"  [dim]Registering schema:[/] {schemaRef.PositionalPointer}");

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference, defaultVocabulary, rebaseAsRoot: false)
                .ConfigureAwait(false);

            pointerToType[schemaRef.PositionalPointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        AnsiConsole.MarkupLine($"[yellow]Registered {typesToGenerate.Count} type declarations, generating code...[/]");

        CSharpLanguageProvider.Options options = new(rootNamespace + ".Models");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(new OpenApiSchemaNameHeuristic(parameterNames));
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        AnsiConsole.MarkupLine($"[yellow]Code generation complete, writing {generatedCode.Count} files...[/]");

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

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        // The subset of map pointers whose type is an object or array — i.e. types for which the model generator emits a
        // context-threaded Source<TContext>. The server generator emits a closure-free, single-materialisation
        // Ok<TContext> response factory only for a body in this set; a scalar body has no Source<TContext>.
        HashSet<string> contextSourceBodyPointers = new(StringComparer.Ordinal);

        foreach ((string pointerStr, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;

            if (reduced.HasDotnetTypeName())
            {
                schemaTypeMap[pointerStr] = reduced.FullyQualifiedDotnetTypeName();
                if ((reduced.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0)
                {
                    contextSourceBodyPointers.Add(pointerStr);
                }
            }

            AddChildTypesToMap(reduced, schemaTypeMap, contextSourceBodyPointers);
        }

        return (schemaTypeMap, contextSourceBodyPointers, schemaFileNames);
    }

    private static void AddChildTypesToMap(TypeDeclaration parentType, Dictionary<string, string> schemaTypeMap, HashSet<string> contextSourceBodyPointers)
    {
        HashSet<TypeDeclaration> visited = [];
        AddChildTypesToMapCore(parentType, schemaTypeMap, contextSourceBodyPointers, visited);
    }

    private static void AddChildTypesToMapCore(TypeDeclaration parentType, Dictionary<string, string> schemaTypeMap, HashSet<string> contextSourceBodyPointers, HashSet<TypeDeclaration> visited)
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
                if (schemaTypeMap.TryAdd(key, reducedChild.FullyQualifiedDotnetTypeName())
                    && (reducedChild.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0)
                {
                    contextSourceBodyPointers.Add(key);
                }
            }

            AddChildTypesToMapCore(reducedChild, schemaTypeMap, contextSourceBodyPointers, visited);
        }
    }

    private static string ResolveDocumentPath(string docPart)
    {
        if (Uri.TryCreate(docPart, UriKind.Absolute, out Uri? uri)
            && !uri.IsFile)
        {
            return docPart;
        }

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