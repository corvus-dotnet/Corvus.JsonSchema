// <copyright file="OpenApiGenerateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating API client code from an OpenAPI specification.
/// </summary>
/// <remarks>
/// This is the default command for the <c>openapi</c> branch.
/// It can be invoked as <c>corvusjson openapi &lt;specFile&gt;</c> or
/// <c>corvusjson openapi generate &lt;specFile&gt;</c>.
/// </remarks>
internal sealed class OpenApiGenerateCommand : AsyncCommand<OpenApiGenerateSettings>
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

        // Check lock file (skip if up to date, unless --force)
        if (!settings.Force)
        {
            if (OpenApiLockFile.TryLoad(outputPath, out OpenApiLockFileModel existingLock)
                && OpenApiLockFile.IsUpToDate(in existingLock, in specRoot, specVersion, rootNamespace, settings.ClientName, filter))
            {
                AnsiConsole.MarkupLine("[green]Up to date — skipping generation.[/] Use --force to regenerate.");
                return 0;
            }
        }

        // Back up existing lock file before generation so we can restore on failure
        bool hasBackup = OpenApiLockFile.BackupLockFile(outputPath);

        // Create the external reference resolver — supports local, relative, and absolute $ref
        using ExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        try
        {
            IReadOnlyList<GeneratedFile> files;
            string modelsPath = Path.Combine(outputPath, "Models");

            if (specVersion is "3.2")
            {
                // OpenAPI 3.2 — use the 3.2 typed code generator
                SchemaReference[] schemaRefs = OpenApi32CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = schemaRefs.Length > 0
                    ? await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
                        .ConfigureAwait(false)
                    : null;

                if (schemaTypeMap is not null)
                {
                    AnsiConsole.MarkupLine($"[green]Resolved schema types:[/] {schemaTypeMap.Count}");
                }

                OpenApi32CodeGenerator generator = new(
                    rootNamespace,
                    schemaTypeMap ?? new Dictionary<string, string>(),
                    settings.ClientName);
                files = generator.Generate(specRoot, filter, referenceResolver);
            }
            else if (specVersion is "3.1" or not "3.0")
            {
                // OpenAPI 3.1 (or unknown) — use the typed code generator directly
                SchemaReference[] schemaRefs = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = schemaRefs.Length > 0
                    ? await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
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
                files = generator.Generate(specRoot, filter, referenceResolver);
            }
            else
            {
                // OpenAPI 3.0 — use the typed code generator directly
                SchemaReference[] schemaRefs = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = schemaRefs.Length > 0
                    ? await GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, cancellationToken)
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
                files = generator.Generate(specRoot, filter, referenceResolver);
            }

            AnsiConsole.MarkupLine($"[green]Files:[/] {files.Count}");

            // Write client files
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

            int totalFiles = files.Count;
            AnsiConsole.MarkupLine($"[green]Generated {totalFiles} files in {outputPath}[/]");

            // Write lock file and clean up backup
            OpenApiLockFileModel lockFile = OpenApiLockFile.Create(in specRoot, specVersion, rootNamespace, settings.ClientName, filter, generatedFileNames, specUrl);
            OpenApiLockFile.Save(in lockFile, outputPath);
            OpenApiLockFile.DeleteBackup(outputPath);

            return 0;
        }
        catch
        {
            // Restore lock file to pre-generation state on failure
            if (hasBackup)
            {
                OpenApiLockFile.RestoreLockFile(outputPath);
                AnsiConsole.MarkupLine("[yellow]Lock file restored from backup after generation failure.[/]");
            }

            throw;
        }
    }

    private static async Task<Dictionary<string, string>> GenerateSchemaTypesAsync(
        string specFile,
        string specVersion,
        string rootNamespace,
        string outputPath,
        SchemaReference[] schemaRefs,
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

    /// <summary>
    /// Extracts the document path from a resolvable pointer's document portion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document portion is already absolute because <c>ResolveToAbsolute</c> resolved it
    /// against the correct base URI at scan time (per RFC 3986 §5). This method simply handles
    /// the file URI vs non-file URI distinction for <see cref="JsonReference"/> construction.
    /// </para>
    /// </remarks>
    /// <param name="docPart">The document portion of a reference (before the # fragment).
    /// Must be absolute (either an absolute file path or an absolute non-file URI).</param>
    /// <returns>The document path suitable for <see cref="JsonReference"/>.</returns>
    private static string ResolveDocumentPath(string docPart)
    {
        // Non-file absolute URIs (http://, https://, urn:, etc.) pass through directly.
        // File paths (already absolute from ResolveToAbsolute) also pass through —
        // Path.GetFullPath normalizes path separators.
        if (Uri.TryCreate(docPart, UriKind.Absolute, out Uri? uri)
            && !uri.IsFile)
        {
            return docPart;
        }

        // Absolute file path — normalize separators
        return Path.GetFullPath(docPart);
    }
}

#endif