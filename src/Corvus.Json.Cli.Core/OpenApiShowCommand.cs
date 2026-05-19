// <copyright file="OpenApiShowCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command that displays the operation tree of an OpenAPI specification.
/// </summary>
/// <remarks>
/// Invoked as <c>corvusjson openapi show &lt;specFile&gt;</c>.
/// </remarks>
internal sealed class OpenApiShowCommand : AsyncCommand<OpenApiSettings>
{
    private static readonly Dictionary<OperationMethod, string> MethodColors = new()
    {
        [OperationMethod.Get] = "green",
        [OperationMethod.Post] = "blue",
        [OperationMethod.Put] = "yellow",
        [OperationMethod.Patch] = "yellow",
        [OperationMethod.Delete] = "red",
        [OperationMethod.Options] = "dim",
        [OperationMethod.Head] = "dim",
        [OperationMethod.Trace] = "dim",
    };

    /// <inheritdoc/>
    protected override Task<int> ExecuteAsync(CommandContext context, OpenApiSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SpecFile);

        if (!File.Exists(settings.SpecFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Spec file not found: {settings.SpecFile}");
            return Task.FromResult(1);
        }

        byte[] specBytes = File.ReadAllBytes(settings.SpecFile);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;

        string specVersion = OpenApiCommandHelpers.DetectSpecVersion(specRoot, settings.SpecVersion);
        OperationFilter? filter = OpenApiCommandHelpers.BuildFilter(settings);

        // Header
        string? title = OpenApiCommandHelpers.GetTitle(specRoot);
        string? version = OpenApiCommandHelpers.GetVersion(specRoot);
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title ?? "(untitled)")}[/] v{Markup.Escape(version ?? "?")} [dim](OpenAPI {specVersion})[/]");
        AnsiConsole.WriteLine();

        // List operations
        OperationSummary[] operations = specVersion switch
        {
            "3.0" => OpenApi30CodeGenerator.ListOperations(specRoot, filter),
            "3.2" => OpenApi32CodeGenerator.ListOperations(specRoot, filter),
            _ => OpenApi31CodeGenerator.ListOperations(specRoot, filter),
        };

        if (operations.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No operations found.[/]");
            return Task.FromResult(0);
        }

        bool groupByTag = string.Equals(settings.GroupBy, "tag", StringComparison.OrdinalIgnoreCase);

        if (groupByTag)
        {
            RenderByTag(operations);
        }
        else
        {
            RenderByPath(operations);
        }

        // Summary
        AnsiConsole.WriteLine();
        int deprecatedCount = operations.Count(o => o.IsDeprecated);
        int pathCount = operations.Select(o => o.Path).Distinct(StringComparer.Ordinal).Count();
        AnsiConsole.MarkupLine($"[green]{operations.Length} operations[/] across [green]{pathCount} paths[/]");
        if (deprecatedCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{deprecatedCount} deprecated[/]");
        }

        if (filter is not null)
        {
            AnsiConsole.MarkupLine("[dim](filtered)[/]");
        }

        return Task.FromResult(0);
    }

    private static void RenderByPath(OperationSummary[] operations)
    {
        var tree = new Tree($"[bold]Operations[/] ({operations.Length})");

        var pathGroups = new Dictionary<string, List<OperationSummary>>(StringComparer.Ordinal);
        foreach (OperationSummary op in operations)
        {
            if (!pathGroups.TryGetValue(op.Path, out List<OperationSummary>? group))
            {
                group = [];
                pathGroups[op.Path] = group;
            }

            group.Add(op);
        }

        foreach ((string path, List<OperationSummary> ops) in pathGroups)
        {
            TreeNode pathNode = tree.AddNode($"[bold]{Markup.Escape(path)}[/]");
            foreach (OperationSummary op in ops)
            {
                pathNode.AddNode(FormatOperation(op));
            }
        }

        AnsiConsole.Write(tree);
    }

    private static void RenderByTag(OperationSummary[] operations)
    {
        var tree = new Tree($"[bold]Operations by Tag[/] ({operations.Length})");

        var tagGroups = new Dictionary<string, List<OperationSummary>>(StringComparer.Ordinal);
        foreach (OperationSummary op in operations)
        {
            if (op.Tags.Length == 0)
            {
                if (!tagGroups.TryGetValue("(untagged)", out List<OperationSummary>? untagged))
                {
                    untagged = [];
                    tagGroups["(untagged)"] = untagged;
                }

                untagged.Add(op);
            }
            else
            {
                foreach (string tag in op.Tags)
                {
                    if (!tagGroups.TryGetValue(tag, out List<OperationSummary>? group))
                    {
                        group = [];
                        tagGroups[tag] = group;
                    }

                    group.Add(op);
                }
            }
        }

        foreach ((string tag, List<OperationSummary> ops) in tagGroups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            TreeNode tagNode = tree.AddNode($"[bold]{Markup.Escape(tag)}[/] [dim]({ops.Count})[/]");
            foreach (OperationSummary op in ops)
            {
                tagNode.AddNode(FormatOperation(op, includePath: true));
            }
        }

        AnsiConsole.Write(tree);
    }

    private static string FormatOperation(OperationSummary op, bool includePath = false)
    {
        string methodStr = op.Method.ToString().ToUpperInvariant();
        string color = MethodColors.GetValueOrDefault(op.Method, "white");
        string deprecated = op.IsDeprecated ? " [strikethrough dim](deprecated)[/]" : string.Empty;
        string opIdStr = op.OperationId is not null ? $" [dim]{Markup.Escape(op.OperationId)}[/]" : string.Empty;
        string summaryStr = op.Summary is not null ? $" — {Markup.Escape(op.Summary)}" : string.Empty;
        string pathStr = includePath ? $" [bold]{Markup.Escape(op.Path)}[/]" : string.Empty;

        List<string> detailParts = [];
        if (op.ParameterCount > 0)
        {
            detailParts.Add($"{op.ParameterCount} params");
        }

        if (op.HasRequestBody)
        {
            detailParts.Add("body");
        }

        if (!includePath && op.Tags.Length > 0)
        {
            detailParts.Add($"tags: {string.Join(", ", op.Tags)}");
        }

        string details = detailParts.Count > 0
            ? $" [dim]({string.Join(", ", detailParts)})[/]"
            : string.Empty;

        return $"[{color}]{methodStr}[/]{pathStr}{opIdStr}{deprecated}{summaryStr}{details}";
    }
}

#endif