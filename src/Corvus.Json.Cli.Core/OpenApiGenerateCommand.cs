// <copyright file="OpenApiGenerateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;
using Corvus.Text.Json.Arazzo.Generation;
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

        // Pre-process YAML if needed (auto-detect from extension, or explicit --yaml flag)
        bool useYaml = settings.Yaml ?? IsYamlFile(settings.SpecFile);

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
            IReadOnlyList<string>? modelFileNames = null;

            if (specVersion is "3.2")
            {
                // OpenAPI 3.2 — use the 3.2 typed code generator
                SchemaReference[] schemaRefs = OpenApi32CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = null;
                if (schemaRefs.Length > 0)
                {
                    (schemaTypeMap, modelFileNames) = await OpenApiSchemaTypeGeneration.GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, cancellationToken, progress: m => AnsiConsole.WriteLine(m))
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
                    settings.IgnoreEmptyFormUrlEncodedBody);
                files = generator.Generate(specRoot, filter, referenceResolver);
            }
            else if (specVersion is "3.1" or not "3.0")
            {
                // OpenAPI 3.1 (or unknown) — use the typed code generator directly
                SchemaReference[] schemaRefs = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = null;
                if (schemaRefs.Length > 0)
                {
                    (schemaTypeMap, modelFileNames) = await OpenApiSchemaTypeGeneration.GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, cancellationToken, progress: m => AnsiConsole.WriteLine(m))
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
                    settings.IgnoreEmptyFormUrlEncodedBody);
                files = generator.Generate(specRoot, filter, referenceResolver);
            }
            else
            {
                // OpenAPI 3.0 — use the typed code generator directly
                SchemaReference[] schemaRefs = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, filter, referenceResolver);

                AnsiConsole.MarkupLine($"[green]API:[/] {OpenApiCommandHelpers.GetTitle(specRoot) ?? "(untitled)"} v{OpenApiCommandHelpers.GetVersion(specRoot) ?? "?"}");
                AnsiConsole.MarkupLine($"[green]Schemas:[/] {schemaRefs.Length}");

                Dictionary<string, string>? schemaTypeMap = null;
                if (schemaRefs.Length > 0)
                {
                    (schemaTypeMap, modelFileNames) = await OpenApiSchemaTypeGeneration.GenerateSchemaTypesAsync(specFilePath, specVersion, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, cancellationToken, progress: m => AnsiConsole.WriteLine(m))
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
                    settings.IgnoreEmptyFormUrlEncodedBody);
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

            // Include model files with relative Models/ prefix
            if (modelFileNames is not null)
            {
                foreach (string modelFile in modelFileNames)
                {
                    generatedFileNames.Add(Path.Combine("Models", modelFile));
                }
            }

            AnsiConsole.MarkupLine($"[green]Generated {generatedFileNames.Count} files ({files.Count} client + {modelFileNames?.Count ?? 0} model) in {outputPath}[/]");

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

    private static bool IsYamlFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

#endif