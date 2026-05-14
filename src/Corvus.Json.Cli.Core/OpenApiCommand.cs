// <copyright file="OpenApiCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.OpenApi;
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

        ISpecWalker walker = specVersion switch
        {
            "3.0" => new OpenApi30Walker(),
            "3.1" => new OpenApi31Walker(),
            _ => new OpenApi31Walker(),
        };

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

        // Build client model
        ClientModel model = ClientModelBuilder.Build(specRoot, walker, filter);

        AnsiConsole.MarkupLine($"[green]API:[/] {model.Title ?? "(untitled)"} v{model.Version ?? "?"}");
        AnsiConsole.MarkupLine($"[green]Operations:[/] {model.Operations.Count}");
        AnsiConsole.MarkupLine($"[green]Schemas:[/] {model.SchemaPointers.Count}");

        // Emit client code
        ClientCodeEmitter emitter = new(rootNamespace, settings.ClientName);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        // Write files
        Directory.CreateDirectory(outputPath);

        foreach (GeneratedFile file in files)
        {
            string filePath = Path.Combine(outputPath, file.FileName);
            await File.WriteAllTextAsync(filePath, file.Content, cancellationToken)
                .ConfigureAwait(false);
            AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {filePath}");
        }

        AnsiConsole.MarkupLine($"[green]Generated {files.Count} files in {outputPath}[/]");

        return 0;
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
}

#endif