// <copyright file="JsonLogicCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating optimized C# evaluator classes from JsonLogic rules.
/// </summary>
internal class JsonLogicCommand : Command<JsonLogicCommand.Settings>
{
    /// <summary>
    /// Settings for the jsonlogic command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the JSON file containing the JsonLogic rule.")]
        [CommandArgument(0, "<ruleFile>")]
        [NotNull]
        public string? RuleFile { get; init; }

        [CommandOption("--className")]
        [Description("The name of the generated static class.")]
        [NotNull]
        public string? ClassName { get; init; }

        [CommandOption("--namespace")]
        [Description("The namespace for the generated class.")]
        [NotNull]
        public string? Namespace { get; init; }

        [CommandOption("--outputPath")]
        [Description("The path to which to write the generated C# file. If not specified, writes to <className>.cs in the current directory.")]
        public string? OutputPath { get; init; }

        [CommandOption("--operators")]
        [Description("Path to a .jlops file defining custom operator templates.")]
        public string? OperatorsFile { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.RuleFile);
        ArgumentNullException.ThrowIfNullOrEmpty(settings.ClassName);
        ArgumentNullException.ThrowIfNullOrEmpty(settings.Namespace);

        if (!File.Exists(settings.RuleFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Rule file not found: {settings.RuleFile}");
            return 1;
        }

        string ruleJson = File.ReadAllText(settings.RuleFile);

        IReadOnlyList<CustomOperator>? customOperators = null;
        if (!string.IsNullOrEmpty(settings.OperatorsFile))
        {
            if (!File.Exists(settings.OperatorsFile))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Operators file not found: {settings.OperatorsFile}");
                return 1;
            }

            try
            {
                string opsContent = File.ReadAllText(settings.OperatorsFile);
                customOperators = JlopsParser.Parse(opsContent);
            }
            catch (FormatException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error parsing .jlops file:[/] {ex.Message}");
                return 1;
            }
        }

        string outputPath = settings.OutputPath ?? $"{settings.ClassName}.cs";

        try
        {
            string generatedCode = JsonLogicCodeGenerator.Generate(ruleJson, settings.ClassName, settings.Namespace, customOperators);

            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputPath, generatedCode);
            AnsiConsole.MarkupLine($"[green]Generated:[/] {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}