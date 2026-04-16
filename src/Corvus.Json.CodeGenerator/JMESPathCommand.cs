// <copyright file="JMESPathCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.JMESPath.CodeGeneration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for generating optimized C# evaluator classes from JMESPath expressions.
/// </summary>
internal class JMESPathCommand : Command<JMESPathCommand.Settings>
{
    /// <summary>
    /// Settings for the jmespath command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the file containing the JMESPath expression.")]
        [CommandArgument(0, "<expressionFile>")]
        [NotNull]
        public string? ExpressionFile { get; init; }

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
    }

    /// <inheritdoc/>
    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.ExpressionFile);
        ArgumentNullException.ThrowIfNullOrEmpty(settings.ClassName);
        ArgumentNullException.ThrowIfNullOrEmpty(settings.Namespace);

        if (!File.Exists(settings.ExpressionFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Expression file not found: {settings.ExpressionFile}");
            return 1;
        }

        string expression = File.ReadAllText(settings.ExpressionFile).Trim();

        if (string.IsNullOrEmpty(expression))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Expression file is empty: {settings.ExpressionFile}");
            return 1;
        }

        string outputPath = settings.OutputPath ?? $"{settings.ClassName}.cs";

        try
        {
            string generatedCode = JMESPathCodeGenerator.Generate(expression, settings.ClassName, settings.Namespace);

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