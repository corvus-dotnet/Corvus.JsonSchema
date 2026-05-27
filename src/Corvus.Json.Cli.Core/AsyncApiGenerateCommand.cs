// <copyright file="AsyncApiGenerateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating typed producers, consumers, and message
/// types from an AsyncAPI specification.
/// </summary>
/// <remarks>
/// Invoked as <c>corvusjson asyncapi-generate &lt;specFile&gt;</c>.
/// </remarks>
internal sealed class AsyncApiGenerateCommand : AsyncCommand<AsyncApiGenerateSettings>
{
    /// <inheritdoc/>
    protected override async Task<int> ExecuteAsync(CommandContext context, AsyncApiGenerateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SpecFile);

        string outputPath = settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Generated");

        // Resolve the spec URL — explicit --spec-url takes priority, then lock file descriptionLocation
        string? specUrl = settings.SpecUrl;
        if (specUrl is null)
        {
            if (AsyncApiLockFile.TryLoad(outputPath, out AsyncApiLockFileModel lockCheck)
                && lockCheck.DescriptionLocation.IsNotUndefined())
            {
                specUrl = lockCheck.DescriptionLocation.GetString();
            }
        }

        // If we have a spec URL, fetch and overwrite the local file
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

        string rootNamespace = settings.RootNamespace ?? "GeneratedAsyncApi";
        string specFilePath = Path.GetFullPath(settings.SpecFile);

        byte[] specBytes = await File.ReadAllBytesAsync(settings.SpecFile, cancellationToken)
            .ConfigureAwait(false);

        // Pre-process YAML if needed (auto-detect from extension or explicit --yaml flag)
        bool useYaml = settings.SupportYaml ?? IsYamlFile(settings.SpecFile);
        if (useYaml)
        {
            YamlPreProcessor yamlPreProcessor = new();
            using MemoryStream inputStream = new(specBytes);
            using Stream processedStream = yamlPreProcessor.Process(inputStream);
            using MemoryStream outputStream = new();
            processedStream.CopyTo(outputStream);
            specBytes = outputStream.ToArray();
        }

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;

        string specVersion = AsyncApiShowCommand.DetectAsyncApiVersion(specRoot, settings.SpecVersion);
        OperationFilter? filter = AsyncApiShowCommand.BuildFilter(settings);

        // Check lock file (skip if up to date, unless --force)
        if (!settings.Force)
        {
            if (AsyncApiLockFile.TryLoad(outputPath, out AsyncApiLockFileModel existingLock)
                && AsyncApiLockFile.IsUpToDate(in existingLock, in specRoot, specVersion, rootNamespace, settings.Mode, filter))
            {
                AnsiConsole.MarkupLine("[green]Up to date — skipping generation.[/] Use --force to regenerate.");
                return 0;
            }
        }

        // Back up existing lock file before generation so we can restore on failure
        bool hasBackup = AsyncApiLockFile.BackupLockFile(outputPath);

        // Create external reference resolver for multi-file specs
        using AsyncApiExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        try
        {
            // Collect schema pointers from the spec
            string[] pointers;
            if (AsyncApiShowCommand.IsAsyncApi26Version(specVersion))
            {
                pointers = AsyncApi26CodeGenerator.CollectSchemaPointers(specRoot, filter, referenceResolver);
            }
            else if (AsyncApiShowCommand.IsAsyncApi30Version(specVersion))
            {
                pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(specRoot, filter, referenceResolver);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Unsupported AsyncAPI version: {specVersion}");
                return 1;
            }

            string? title = GetTitle(specRoot);
            string? version = GetVersion(specRoot);
            AnsiConsole.MarkupLine($"[green]API:[/] {title ?? "(untitled)"} v{version ?? "?"} [dim](AsyncAPI {specVersion})[/]");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {pointers.Length}");

            // Generate schema model types via the V5 code generator
            Dictionary<string, string>? schemaTypeMap = null;
            IReadOnlyList<string>? modelFileNames = null;
            string modelsPath = Path.Combine(outputPath, "Models");

            if (pointers.Length > 0)
            {
                (schemaTypeMap, modelFileNames) = await GenerateSchemaTypesAsync(
                    specFilePath, rootNamespace, modelsPath, pointers, useYaml, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            // Generate producer/consumer code
            IReadOnlyList<GeneratedFile> files;
            if (AsyncApiShowCommand.IsAsyncApi26Version(specVersion))
            {
                AsyncApi26CodeGenerator generator = new(
                    rootNamespace,
                    schemaTypeMap ?? new Dictionary<string, string>());
                files = generator.Generate(specRoot, filter, referenceResolver);
            }
            else
            {
                AsyncApi30CodeGenerator generator = new(
                    rootNamespace,
                    schemaTypeMap ?? new Dictionary<string, string>());
                files = generator.Generate(specRoot, filter, referenceResolver);
            }

            // Filter by mode
            IReadOnlyList<GeneratedFile> filteredFiles = FilterByMode(files, settings.Mode);

            AnsiConsole.MarkupLine($"[green]Files:[/] {filteredFiles.Count}");

            // Write output files
            Directory.CreateDirectory(outputPath);
            List<string> generatedFileNames = [];

            foreach (GeneratedFile file in filteredFiles)
            {
                string filePath = Path.Combine(outputPath, file.FileName);
                await File.WriteAllTextAsync(filePath, file.Content, cancellationToken)
                    .ConfigureAwait(false);
                AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {filePath}");
                generatedFileNames.Add(file.FileName);
            }

            if (modelFileNames is not null)
            {
                foreach (string modelFile in modelFileNames)
                {
                    generatedFileNames.Add(Path.Combine("Models", modelFile));
                }
            }

            AnsiConsole.MarkupLine($"[green]Generated {generatedFileNames.Count} files ({filteredFiles.Count} client + {modelFileNames?.Count ?? 0} model) in {outputPath}[/]");

            // Write lock file and clean up backup
            AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(in specRoot, specVersion, rootNamespace, settings.Mode, filter, generatedFileNames, specUrl);
            AsyncApiLockFile.Save(in lockFile, outputPath);
            AsyncApiLockFile.DeleteBackup(outputPath);

            return 0;
        }
        catch
        {
            // Restore lock file to pre-generation state on failure
            if (hasBackup)
            {
                AsyncApiLockFile.RestoreLockFile(outputPath);
                AnsiConsole.MarkupLine("[yellow]Lock file restored from backup after generation failure.[/]");
            }

            throw;
        }
    }

    private static IReadOnlyList<GeneratedFile> FilterByMode(IReadOnlyList<GeneratedFile> files, string mode)
    {
        if (string.Equals(mode, "both", StringComparison.OrdinalIgnoreCase))
        {
            return files;
        }

        if (string.Equals(mode, "producer", StringComparison.OrdinalIgnoreCase))
        {
            return files.Where(f => f.FileName.Contains("Producer", StringComparison.OrdinalIgnoreCase) ||
                                    f.FileName.Contains("Message", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (string.Equals(mode, "consumer", StringComparison.OrdinalIgnoreCase))
        {
            return files.Where(f => f.FileName.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                                    f.FileName.Contains("Message", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return files;
    }

    private static async Task<(Dictionary<string, string> SchemaTypeMap, IReadOnlyList<string> GeneratedFileNames)> GenerateSchemaTypesAsync(
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

        // AsyncAPI uses JSON Schema Draft 7
        IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary;

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string pointer in pointers)
        {
            // All AsyncAPI schema pointers are same-document fragment-only
            JsonReference reference = new(specFilePath, pointer);

            AnsiConsole.MarkupLine($"  [dim]Registering schema:[/] {pointer}");

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference, defaultVocabulary, rebaseAsRoot: false)
                .ConfigureAwait(false);

            pointerToType[pointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        AnsiConsole.MarkupLine($"[yellow]Registered {typesToGenerate.Count} type declarations, generating code...[/]");

        CSharpLanguageProvider.Options options = new(rootNamespace);
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(AsyncApiSchemaNameHeuristic.Instance);
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        AnsiConsole.MarkupLine($"[yellow]Code generation complete, writing {generatedCode.Count} files...[/]");

        Directory.CreateDirectory(outputPath);

        HashSet<string> writtenFiles = new(StringComparer.OrdinalIgnoreCase);
        List<string> schemaFileNames = [];

        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            string outputFile = Path.Combine(outputPath, codeFile.FileName);
            string normalized = PathTruncator.NormalizePath(outputFile);
            outputFile = PathTruncator.TruncatePath(normalized);

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

            await File.WriteAllTextAsync(outputFile, codeFile.FileContent, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [cyan]Schema type:[/] {outputFile}");
            schemaFileNames.Add(Path.GetFileName(outputFile));
        }

        AnsiConsole.MarkupLine($"[green]Generated {schemaFileNames.Count} schema type files[/]");

        // Build pointer → fully qualified type name map
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

        return (schemaTypeMap, schemaFileNames);
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
                string key = "#" + rootPointer;
                schemaTypeMap.TryAdd(key, reducedChild.FullyQualifiedDotnetTypeName());
            }

            AddChildTypesToMapCore(reducedChild, schemaTypeMap, visited);
        }
    }

    private static string? GetTitle(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info) &&
            info.TryGetProperty("title"u8, out JsonElement title) &&
            title.ValueKind == JsonValueKind.String)
        {
            return title.GetString();
        }

        return null;
    }

    private static string? GetVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info) &&
            info.TryGetProperty("version"u8, out JsonElement version) &&
            version.ValueKind == JsonValueKind.String)
        {
            return version.GetString();
        }

        return null;
    }

    private static bool IsYamlFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

#endif