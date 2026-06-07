// <copyright file="ArazzoShowCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo10;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command that displays the source descriptions, workflows, and steps
/// of an Arazzo workflow document.
/// </summary>
/// <remarks>
/// Invoked as <c>corvusjson arazzo-show &lt;arazzoFile&gt;</c>.
/// </remarks>
internal sealed class ArazzoShowCommand : AsyncCommand<ArazzoSettings>
{
    /// <inheritdoc/>
    protected override Task<int> ExecuteAsync(CommandContext context, ArazzoSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.ArazzoFile);

        if (!File.Exists(settings.ArazzoFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Arazzo file not found: {settings.ArazzoFile}");
            return Task.FromResult(1);
        }

        byte[] documentBytes = File.ReadAllBytes(settings.ArazzoFile);

        if (IsYamlFile(settings.ArazzoFile))
        {
            YamlPreProcessor yamlPreProcessor = new();
            using MemoryStream inputStream = new(documentBytes);
            using Stream processedStream = yamlPreProcessor.Process(inputStream);
            using MemoryStream outputStream = new();
            processedStream.CopyTo(outputStream);
            documentBytes = outputStream.ToArray();
        }

        using ParsedJsonDocument<ArazzoDocument> doc = ParsedJsonDocument<ArazzoDocument>.Parse(documentBytes);
        ArazzoDocument arazzo = doc.RootElement;

        // Header
        ArazzoDocument.Info info = arazzo.InfoValue;
        string title = info.Title.IsNotUndefined() ? info.Title.GetString()! : "(untitled)";
        string version = info.Version.IsNotUndefined() ? info.Version.GetString()! : "?";
        string arazzoVersion = arazzo.Arazzo.IsNotUndefined() ? arazzo.Arazzo.GetString()! : "?";
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/] v{Markup.Escape(version)} [dim](Arazzo {Markup.Escape(arazzoVersion)})[/]");
        AnsiConsole.WriteLine();

        RenderSourceDescriptions(arazzo);
        AnsiConsole.WriteLine();
        int stepCount = RenderWorkflows(arazzo);

        // Summary
        AnsiConsole.WriteLine();
        int workflowCount = arazzo.Workflows.GetArrayLength();
        int sourceCount = arazzo.SourceDescriptions.GetArrayLength();
        AnsiConsole.MarkupLine($"[green]{workflowCount} workflows[/] ([green]{stepCount} steps[/]) across [green]{sourceCount} source descriptions[/]");

        return Task.FromResult(0);
    }

    private static void RenderSourceDescriptions(ArazzoDocument arazzo)
    {
        ArazzoDocument.SourceDescriptionObjectArray sources = arazzo.SourceDescriptions;
        var tree = new Tree($"[bold]Source Descriptions[/] ({sources.GetArrayLength()})");

        foreach (ArazzoDocument.SourceDescriptionObject source in sources.EnumerateArray())
        {
            string name = source.Name.IsNotUndefined() ? source.Name.GetString()! : "(unnamed)";
            string urlStr = source.Url.IsNotUndefined() ? $" [dim]{Markup.Escape(source.Url.GetString()!)}[/]" : string.Empty;

            ArazzoDocument.SourceDescriptionObject.TypeEntity type = source.Type;
            string typeStr = type.IsNotUndefined()
                ? type.Match(
                    static () => " [italic cyan]arazzo[/]",
                    static () => " [italic cyan]openapi[/]",
                    () => $" [italic cyan]{Markup.Escape(type.GetString()!)}[/]")
                : string.Empty;

            tree.AddNode($"[bold]{Markup.Escape(name)}[/]{typeStr}{urlStr}");
        }

        AnsiConsole.Write(tree);
    }

    private static int RenderWorkflows(ArazzoDocument arazzo)
    {
        ArazzoDocument.WorkflowObjectArray workflows = arazzo.Workflows;
        var tree = new Tree($"[bold]Workflows[/] ({workflows.GetArrayLength()})");
        int totalSteps = 0;

        foreach (ArazzoDocument.WorkflowObject workflow in workflows.EnumerateArray())
        {
            string workflowId = workflow.WorkflowId.IsNotUndefined() ? workflow.WorkflowId.GetString()! : "(unnamed)";
            string summaryStr = workflow.Summary.IsNotUndefined() ? $" [dim]— {Markup.Escape(workflow.Summary.GetString()!)}[/]" : string.Empty;

            ArazzoDocument.WorkflowObject.StepObjectArray steps = workflow.Steps;
            TreeNode workflowNode = tree.AddNode($"[bold yellow]{Markup.Escape(workflowId)}[/] [dim]({steps.GetArrayLength()} steps)[/]{summaryStr}");

            foreach (ArazzoDocument.StepObject step in steps.EnumerateArray())
            {
                totalSteps++;
                workflowNode.AddNode(FormatStep(step));
            }
        }

        AnsiConsole.Write(tree);
        return totalSteps;
    }

    private static string FormatStep(ArazzoDocument.StepObject step)
    {
        string stepId = step.StepId.IsNotUndefined() ? step.StepId.GetString()! : "(unnamed)";
        string target = DescribeStepTarget(step);
        string descriptionStr = step.Description.IsNotUndefined() ? $" [dim]— {Markup.Escape(step.Description.GetString()!)}[/]" : string.Empty;
        return $"[green]{Markup.Escape(stepId)}[/] {target}{descriptionStr}";
    }

    private static string DescribeStepTarget(ArazzoDocument.StepObject step)
        => step.Match(
            static (in ArazzoDocument.StepObject.RequiredOperationId s) => $"[blue]operationId[/] [dim]{Markup.Escape(s.OperationId.GetString()!)}[/]",
            static (in ArazzoDocument.StepObject.RequiredOperationPath s) => $"[blue]operationPath[/] [dim]{Markup.Escape(s.OperationPath.GetString()!)}[/]",
            static (in ArazzoDocument.StepObject.RequiredWorkflowId s) => $"[blue]workflowId[/] [dim]{Markup.Escape(s.WorkflowId.GetString()!)}[/]",
            static (in ArazzoDocument.StepObject s) => "[dim](no target)[/]");

    private static bool IsYamlFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

#endif