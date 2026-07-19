// <copyright file="ArazzoGenerateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.Linq;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Generation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating strongly-typed workflow executors (and their inputs
/// models and the OpenAPI clients their source descriptions reference) from an Arazzo workflow document.
/// </summary>
/// <remarks>
/// Maintains a lock file (<c>corvusjson-arazzo.lock</c>): generation is skipped when the Arazzo document and every source
/// it resolves are unchanged (incremental), and each source is pinned by the SHA-256 of the exact bytes loaded so a
/// generation whose sources were fetched from a remote URL is reproducible (#871). Use <c>--force</c> to regenerate.
/// </remarks>
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

        // The same file-system + http(s) loader the generation pipeline resolves sources through: used here to read the
        // Arazzo document (converting YAML) for the lock hash, and to re-resolve each recorded source for the up-to-date
        // check — so the lock's view of a source is identical to what generation loads.
        Func<Uri, byte[]?> loader = ArazzoGenerationDriver.CreateFileSystemDocumentLoader();
        var arazzoUri = new Uri(Path.GetFullPath(settings.ArazzoFile));
        byte[]? arazzoJson = loader(arazzoUri);
        if (arazzoJson is null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Could not read the Arazzo document: {settings.ArazzoFile}");
            return 1;
        }

        using ParsedJsonDocument<JsonElement> arazzoDoc = ParsedJsonDocument<JsonElement>.Parse(arazzoJson);
        JsonElement arazzoRoot = arazzoDoc.RootElement;

        // Incremental: skip generation when the lock records the same parameters and every source still resolves to the
        // same digest (a remote source that changed forces a regeneration). Bypassed by --force.
        if (!settings.Force
            && ArazzoLockFile.TryLoad(outputPath, out ArazzoLockFileModel existingLock)
            && ArazzoLockFile.IsUpToDate(in existingLock, in arazzoRoot, rootNamespace, settings.ClientName, settings.Durable, loader))
        {
            AnsiConsole.MarkupLine("[green]Up to date — skipping generation.[/] Use --force to regenerate.");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Generating workflows from:[/] {settings.ArazzoFile}");

        // Back up the existing lock so it can be restored if generation fails.
        bool hasBackup = ArazzoLockFile.BackupLockFile(outputPath);
        try
        {
            var digests = new Dictionary<string, string>(StringComparer.Ordinal);
            IReadOnlyList<string> written = await ArazzoGenerationDriver
                .GenerateAsync(settings.ArazzoFile, rootNamespace, outputPath, settings.ClientName, settings.Durable, cancellationToken, progress: m => AnsiConsole.WriteLine(m), resolvedSourceDigests: digests)
                .ConfigureAwait(false);

            foreach (string path in written)
            {
                AnsiConsole.MarkupLine($"  [blue]Wrote:[/] {path}");
            }

            // Record the lock: every resolved source (each loaded document except the Arazzo document itself, whose content
            // is pinned separately by arazzoFileHash), pinned by digest, plus the generated files (relative to output).
            List<ArazzoLockFile.LockedSource> sources = digests
                .Where(kv => !string.Equals(kv.Key, arazzoUri.AbsoluteUri, StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new ArazzoLockFile.LockedSource(kv.Key, kv.Value))
                .ToList();
            string[] generatedFiles = [.. written.Select(p => Path.GetRelativePath(outputPath, p))];

            ArazzoLockFileModel lockFile = ArazzoLockFile.Create(in arazzoRoot, rootNamespace, settings.ClientName, settings.Durable, sources, generatedFiles);
            ArazzoLockFile.Save(in lockFile, outputPath);
            ArazzoLockFile.DeleteBackup(outputPath);

            AnsiConsole.MarkupLine($"[green]Generated {written.Count} workflow file(s) in {outputPath}[/]");
            return 0;
        }
        catch
        {
            if (hasBackup)
            {
                ArazzoLockFile.RestoreLockFile(outputPath);
                AnsiConsole.MarkupLine("[yellow]Lock file restored from backup after generation failure.[/]");
            }

            throw;
        }
    }
}

#endif