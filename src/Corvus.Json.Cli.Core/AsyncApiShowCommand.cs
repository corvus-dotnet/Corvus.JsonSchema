// <copyright file="AsyncApiShowCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command that displays the channel/operation tree of an AsyncAPI specification.
/// </summary>
/// <remarks>
/// Invoked as <c>corvusjson asyncapi-show &lt;specFile&gt;</c>.
/// </remarks>
internal sealed class AsyncApiShowCommand : AsyncCommand<AsyncApiSettings>
{
    /// <inheritdoc/>
    protected override Task<int> ExecuteAsync(CommandContext context, AsyncApiSettings settings, CancellationToken cancellationToken)
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

        string specVersion = DetectAsyncApiVersion(specRoot, settings.SpecVersion);
        OperationFilter? filter = BuildFilter(settings);

        // Header
        string? title = GetTitle(specRoot);
        string? version = GetVersion(specRoot);
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title ?? "(untitled)")}[/] v{Markup.Escape(version ?? "?")} [dim](AsyncAPI {specVersion})[/]");
        AnsiConsole.WriteLine();

        // List operations
        AsyncApiOperationSummary[] operations;
        if (IsAsyncApi26Version(specVersion))
        {
            operations = AsyncApi26CodeGenerator.ListOperations(specRoot, filter);
        }
        else if (IsAsyncApi30Version(specVersion))
        {
            operations = AsyncApi30CodeGenerator.ListOperations(specRoot, filter);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Unsupported AsyncAPI version: {specVersion}");
            return Task.FromResult(1);
        }

        if (operations.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No operations found.[/]");
            return Task.FromResult(0);
        }

        RenderByChannel(operations);

        // Summary
        AnsiConsole.WriteLine();
        int channelCount = operations.Select(o => o.ChannelAddress).Distinct(StringComparer.Ordinal).Count();
        int sendCount = operations.Count(o => o.Action == OperationAction.Send);
        int recvCount = operations.Count(o => o.Action == OperationAction.Receive);
        AnsiConsole.MarkupLine($"[green]{operations.Length} operations[/] across [green]{channelCount} channels[/] ([blue]{sendCount} send[/], [cyan]{recvCount} receive[/])");

        if (filter is not null)
        {
            AnsiConsole.MarkupLine("[dim](filtered)[/]");
        }

        return Task.FromResult(0);
    }

    private static void RenderByChannel(AsyncApiOperationSummary[] operations)
    {
        var tree = new Tree($"[bold]Operations[/] ({operations.Length})");

        var channelGroups = new Dictionary<string, List<AsyncApiOperationSummary>>(StringComparer.Ordinal);
        foreach (AsyncApiOperationSummary op in operations)
        {
            if (!channelGroups.TryGetValue(op.ChannelAddress, out List<AsyncApiOperationSummary>? group))
            {
                group = [];
                channelGroups[op.ChannelAddress] = group;
            }

            group.Add(op);
        }

        foreach ((string channel, List<AsyncApiOperationSummary> ops) in channelGroups)
        {
            TreeNode channelNode = tree.AddNode($"[bold]{Markup.Escape(channel)}[/]");
            foreach (AsyncApiOperationSummary op in ops)
            {
                channelNode.AddNode(FormatOperation(op));
            }
        }

        AnsiConsole.Write(tree);
    }

    private static string FormatOperation(AsyncApiOperationSummary op)
    {
        string actionStr = op.Action == OperationAction.Send ? "SEND" : "RECV";
        string color = op.Action == OperationAction.Send ? "blue" : "cyan";
        string opIdStr = op.OperationId is not null ? $" [dim]{Markup.Escape(op.OperationId)}[/]" : string.Empty;
        string summaryStr = op.Summary is not null ? $" — {Markup.Escape(op.Summary)}" : string.Empty;

        List<string> detailParts = [];
        if (op.MessageCount > 0)
        {
            detailParts.Add($"{op.MessageCount} msg{(op.MessageCount == 1 ? string.Empty : "s")}");
        }

        if (op.Protocol is not null)
        {
            detailParts.Add(op.Protocol);
        }

        if (op.Tags.Length > 0)
        {
            detailParts.Add($"tags: {string.Join(", ", op.Tags)}");
        }

        string details = detailParts.Count > 0
            ? $" [dim]({string.Join(", ", detailParts)})[/]"
            : string.Empty;

        return $"[{color}]{actionStr}[/]{opIdStr}{summaryStr}{details}";
    }

    internal static string DetectAsyncApiVersion(JsonElement specRoot, string? explicitVersion)
    {
        if (explicitVersion is not null)
        {
            return explicitVersion;
        }

        if (specRoot.TryGetProperty("asyncapi"u8, out JsonElement versionEl) &&
            versionEl.ValueKind == JsonValueKind.String)
        {
            string ver = versionEl.GetString()!;
            if (ver.StartsWith("3.0", StringComparison.Ordinal))
            {
                return "3.0";
            }

            if (ver.StartsWith("2.6", StringComparison.Ordinal))
            {
                return "2.6";
            }

            return ver;
        }

        return "3.0";
    }

    internal static OperationFilter? BuildFilter(AsyncApiSettings settings)
    {
        string[] includes = settings.IncludeChannel ?? [];
        string[] excludes = settings.ExcludeChannel ?? [];
        string[] tags = settings.Tags ?? [];

        if (includes.Length == 0 && excludes.Length == 0 && tags.Length == 0)
        {
            return null;
        }

        return new OperationFilter(includes, excludes, tags);
    }

    internal static bool IsAsyncApi26Version(string specVersion)
    {
        return specVersion.StartsWith("2.6", StringComparison.Ordinal) ||
            string.Equals(specVersion, "2.6", StringComparison.Ordinal);
    }

    internal static bool IsAsyncApi30Version(string specVersion)
    {
        return specVersion.StartsWith("3.0", StringComparison.Ordinal) ||
            string.Equals(specVersion, "3.0", StringComparison.Ordinal);
    }

    private static string? GetTitle(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info) &&
            info.TryGetProperty("title"u8, out JsonElement title) &&
            title.ValueKind == JsonValueKind.String)
        {
            return title.GetString();
        }

        return null;
    }

    private static string? GetVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info) &&
            info.TryGetProperty("version"u8, out JsonElement version) &&
            version.ValueKind == JsonValueKind.String)
        {
            return version.GetString();
        }

        return null;
    }
}

#endif