// <copyright file="GenerationDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGenerator;
using Corvus.Text.Json.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Drives code generation from our command line model.
/// </summary>
public static class GenerationDriver
{
    /// <summary>
    /// Generates the code for a configuration into its output folder, keeping the folder's
    /// <c>corvusjson-jsonschema.lock</c>: the run's schemas join the specification the lock recorded and the whole set
    /// is generated as one program; files a previous run wrote that this run did not are deleted; a run that would
    /// change nothing is skipped.
    /// </summary>
    /// <param name="generatorConfig">The run's configuration.</param>
    /// <param name="generationEngine">The engine.</param>
    /// <param name="codeGenerationMode">The code generation mode.</param>
    /// <param name="force">Generate even when the lock says nothing changed, and apply the run's options to the
    /// folder even when they differ from the lock's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> GenerateTypes(GeneratorConfig generatorConfig, Engine generationEngine, CodeGenerationMode codeGenerationMode, bool force, CancellationToken cancellationToken)
    {
        string outputPath = JsonSchemaLockFile.GetOutputPath(generatorConfig);
        GeneratorConfig run = JsonSchemaLockFile.ToRecorded(generatorConfig, outputPath);
        bool hasLock = JsonSchemaLockFile.TryLoad(outputPath, out JsonSchemaLockFileModel lockFile);
        JsonSchemaLockFile.MergeResult merge = JsonSchemaLockFile.Merge(hasLock ? lockFile.Specification.As<GeneratorConfig>() : default(GeneratorConfig?), run);

        List<string> conflicts = [.. merge.Conflicts];
        if (hasLock && lockFile.Engine.GetString() != generationEngine.ToString())
        {
            conflicts.Add("engine");
        }

        if (hasLock && lockFile.CodeGenerationMode.GetString() != codeGenerationMode.ToString())
        {
            conflicts.Add("codeGenerationMode");
        }

        if (conflicts.Count > 0 && !force)
        {
            AnsiConsole.MarkupLineInterpolated(System.Globalization.CultureInfo.CurrentCulture, $"[red]Error:[/] {outputPath} was generated with different options: {string.Join(", ", conflicts)} (see {JsonSchemaLockFile.LockFileName}).");
            AnsiConsole.MarkupLine("Use the same options, pass --force to apply these options to everything the folder was generated from, or delete the lock file and the generated files to start the folder afresh.");
            return 1;
        }

        IReadOnlyDictionary<string, string> hashes = JsonSchemaLockFile.ComputeHashes(merge.Specification, outputPath);
        if (hasLock && !force && JsonSchemaLockFile.IsUpToDate(in lockFile, merge.Specification, generationEngine, codeGenerationMode, hashes))
        {
            AnsiConsole.MarkupLine("[green]Up to date; nothing generated.[/] Use --force to regenerate.");
            return 0;
        }

        GeneratorConfig effective = JsonSchemaLockFile.ToAbsolute(merge.Specification, outputPath);
        bool hasBackup = JsonSchemaLockFile.BackupLockFile(outputPath);
        try
        {
            (int code, IReadOnlyList<string> generatedFiles) = generationEngine switch
            {
                Engine.V4 => await GenerationDriverV4.GenerateTypes(effective, cancellationToken).ConfigureAwait(false),
                Engine.V5 => await GenerationDriverV5.GenerateTypes(effective, codeGenerationMode, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Unsupported generation engine: {generationEngine}"),
            };

            if (code != 0)
            {
                Restore(outputPath, hasBackup);
                return code;
            }

            if (hasLock)
            {
                IReadOnlyList<string> deleted = JsonSchemaLockFile.DeleteStaleFiles(in lockFile, generatedFiles, outputPath);
                if (deleted.Count > 0)
                {
                    AnsiConsole.MarkupLineInterpolated(System.Globalization.CultureInfo.CurrentCulture, $"Deleted {deleted.Count} file(s) a previous run generated that this run did not.");
                }
            }

            JsonSchemaLockFileModel updated = JsonSchemaLockFile.Create(merge.Specification, generationEngine, codeGenerationMode, hashes, generatedFiles);
            JsonSchemaLockFile.Save(in updated, outputPath);
            JsonSchemaLockFile.DeleteBackup(outputPath);
            return 0;
        }
        catch
        {
            Restore(outputPath, hasBackup);
            throw;
        }
    }

    private static void Restore(string outputPath, bool hasBackup)
    {
        if (hasBackup)
        {
            JsonSchemaLockFile.RestoreLockFile(outputPath);
            AnsiConsole.MarkupLine("[yellow]Lock file restored from backup after generation failure.[/]");
        }
    }
}