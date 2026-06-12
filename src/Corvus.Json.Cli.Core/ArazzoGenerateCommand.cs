// <copyright file="ArazzoGenerateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Text.Json.Arazzo.Generation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating strongly-typed workflow executors (and their inputs
/// models and the OpenAPI clients their source descriptions reference) from an Arazzo workflow document.
/// </summary>
internal sealed class ArazzoGenerateCommand : AsyncCommand<ArazzoGenerateSettings>
{
    /// <inheritdoc/>
    protected override async Task<int> ExecuteAsync(CommandContext context, ArazzoGenerateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.ArazzoFile);

        if (!File.Exists(settings.ArazzoFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Arazzo document not found: {settings.ArazzoFile}");
            return 1;
        }

        string rootNamespace = settings.RootNamespace ?? "GeneratedWorkflows";
        string outputPath = settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Generated");

        AnsiConsole.MarkupLine($"[green]Generating workflows from:[/] {settings.ArazzoFile}");

        IReadOnlyList<string> written = await ArazzoGenerationDriver
            .GenerateAsync(settings.ArazzoFile, rootNamespace, outputPath, settings.ClientName, settings.Durable, cancellationToken, progress: m => AnsiConsole.WriteLine(m))
            .ConfigureAwait(false);

        foreach (string path in written)
        {
            AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {path}");
        }

        AnsiConsole.MarkupLine($"[green]Generated {written.Count} workflow file(s) in {outputPath}[/]");
        return 0;
    }
}

#endif