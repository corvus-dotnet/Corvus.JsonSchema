// <copyright file="OpenApiCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi30;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating API client code from an OpenAPI specification.
/// </summary>
internal class OpenApiCommand : AsyncCommand<OpenApiCommand.Settings>
{
    /// <summary>
    /// Settings for the OpenAPI code generation command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the OpenAPI specification file (JSON or YAML).")]
        [CommandArgument(0, "<specFile>")]
        [NotNull]
        public string? SpecFile { get; init; }

        [CommandOption("--rootNamespace")]
        [Description("The root namespace for generated types.")]
        public string? RootNamespace { get; init; }

        [CommandOption("--outputPath")]
        [Description("The path to which to write the generated code.")]
        public string? OutputPath { get; init; }

        [CommandOption("--clientName")]
        [Description("The prefix for generated client type names. Defaults to the API title.")]
        public string? ClientName { get; init; }

        [CommandOption("--filter")]
        [Description("Glob patterns to filter which paths to include (e.g. /pets/*). Comma-separated or specify multiple times.")]
        public string[]? Filter { get; init; }

        [CommandOption("--specVersion")]
        [Description("The OpenAPI spec version to use (3.0 or 3.1). If not specified, auto-detected from the spec.")]
        [DefaultValue(null)]
        public string? SpecVersion { get; init; }
    }

    /// <inheritdoc/>
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SpecFile);

        if (!File.Exists(settings.SpecFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Spec file not found: {settings.SpecFile}");
            return 1;
        }

        string rootNamespace = settings.RootNamespace ?? "GeneratedApi";
        string outputPath = settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Generated");

        // Read and parse the spec
        byte[] specBytes = await File.ReadAllBytesAsync(settings.SpecFile, cancellationToken)
            .ConfigureAwait(false);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;

        // Detect or use specified version
        string specVersion = settings.SpecVersion ?? DetectSpecVersion(specRoot);

        // Build filter
        OperationFilter? filter = null;
        if (settings.Filter is { Length: > 0 })
        {
            List<string> patterns = [];
            foreach (string f in settings.Filter)
            {
                patterns.AddRange(f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            filter = new OperationFilter(patterns);
        }

        IReadOnlyList<GeneratedFile> files;
        string modelsPath = Path.Combine(outputPath, "Models");

        if (specVersion is "3.1" or not "3.0")
        {
            // OpenAPI 3.1 (or unknown) — use the typed code generator directly
            string[] schemaPointers = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter);

            AnsiConsole.MarkupLine($"[green]API:[/] {GetTitle(specRoot) ?? "(untitled)"} v{GetVersion(specRoot) ?? "?"}");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaPointers.Length}");

            Dictionary<string, string>? schemaTypeMap = schemaPointers.Length > 0
                ? await GenerateSchemaTypesAsync(settings.SpecFile, specVersion, rootNamespace, modelsPath, schemaPointers, parameterNames, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            OpenApi31CodeGenerator generator = new(
                rootNamespace,
                schemaTypeMap ?? new Dictionary<string, string>(),
                settings.ClientName);
            files = generator.Generate(specRoot, filter);
        }
        else
        {
            // OpenAPI 3.0 — use the typed code generator directly
            string[] schemaPointers = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter);

            AnsiConsole.MarkupLine($"[green]API:[/] {GetTitle(specRoot) ?? "(untitled)"} v{GetVersion(specRoot) ?? "?"}");
            AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaPointers.Length}");

            Dictionary<string, string>? schemaTypeMap = schemaPointers.Length > 0
                ? await GenerateSchemaTypesAsync(settings.SpecFile, specVersion, rootNamespace, modelsPath, schemaPointers, parameterNames, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (schemaTypeMap is not null)
            {
                AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
            }

            OpenApi30CodeGenerator generator = new(
                rootNamespace,
                schemaTypeMap ?? new Dictionary<string, string>(),
                settings.ClientName);
            files = generator.Generate(specRoot, filter);
        }

        AnsiConsole.MarkupLine($"[green]Files:[/] {files.Count}");

        // Write client files
        Directory.CreateDirectory(outputPath);

        foreach (GeneratedFile file in files)
        {
            string filePath = Path.Combine(outputPath, file.FileName);
            await File.WriteAllTextAsync(filePath, file.Content, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {filePath}");
        }

        int totalFiles = files.Count;
        AnsiConsole.MarkupLine($"[green]Generated {totalFiles} files in {outputPath}[/]");

        return 0;
    }

    private static async Task<Dictionary<string, string>> GenerateSchemaTypesAsync(
        string specFile,
        string specVersion,
        string rootNamespace,
        string outputPath,
        string[] schemaPointers,
        Dictionary<string, string> parameterNames,
        CancellationToken cancellationToken)
    {
        string specFilePath = Path.GetFullPath(specFile);

        // Set up the document resolver — the FileSystemDocumentResolver reads the spec from disk
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver(),
            new HttpClientDocumentResolver(new HttpClient()));

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

        // Register each schema pointer as a type declaration
        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string pointer in schemaPointers)
        {
            JsonReference reference = new(specFilePath, pointer);
            AnsiConsole.MarkupLine($"  [dim]Registering schema:[/] {pointer}");

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference, defaultVocabulary, rebaseAsRoot: false)
                .ConfigureAwait(false);

            pointerToType[pointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        AnsiConsole.MarkupLine($"[yellow]Registered {typesToGenerate.Count} type declarations, generating code...[/]");

        // Generate code — register OpenAPI naming heuristic for contextual inline schema names
        CSharpLanguageProvider.Options options = new(rootNamespace);
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(new OpenApiSchemaNameHeuristic(parameterNames));
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, cancellationToken);

        AnsiConsole.MarkupLine($"[yellow]Code generation complete, writing {generatedCode.Count} files...[/]");

        // Write schema type files
        Directory.CreateDirectory(outputPath);

        int schemaFilesWritten = 0;
        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            string filePath = Path.Combine(outputPath, codeFile.FileName);
            await File.WriteAllTextAsync(filePath, codeFile.FileContent, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [cyan]Schema type:[/] {filePath}");
            schemaFilesWritten++;
        }

        AnsiConsole.MarkupLine($"[green]Generated {schemaFilesWritten} schema type files[/]");

        // Build the pointer → fully qualified type name map
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach ((string pointerStr, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;

            if (reduced.HasDotnetTypeName())
            {
                schemaTypeMap[pointerStr] = reduced.FullyQualifiedDotnetTypeName();
            }
        }

        return schemaTypeMap;
    }

    private static string DetectSpecVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("openapi"u8, out JsonElement version)
            && version.ValueKind == JsonValueKind.String)
        {
            string? v = version.GetString();
            if (v?.StartsWith("3.0", StringComparison.Ordinal) == true)
            {
                return "3.0";
            }
        }

        return "3.1";
    }

    private static string? GetTitle(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info)
            && info.TryGetProperty("title"u8, out JsonElement title)
            && title.ValueKind == JsonValueKind.String)
        {
            return title.GetString();
        }

        return null;
    }

    private static string? GetVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info)
            && info.TryGetProperty("version"u8, out JsonElement ver)
            && ver.ValueKind == JsonValueKind.String)
        {
            return ver.GetString();
        }

        return null;
    }
}

#endif