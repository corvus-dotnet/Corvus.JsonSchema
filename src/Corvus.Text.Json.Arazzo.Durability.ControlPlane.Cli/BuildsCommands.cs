// <copyright file="BuildsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for listing a workflow version's native build jobs.</summary>
internal sealed class BuildsVersionSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandOption("--status <STATUS>")]
    [Description("Restrict to build jobs in this lifecycle state: Queued, Building, Ready, or Failed.")]
    public string? Status { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings addressing one native build job by its (environment, runtime target).</summary>
internal sealed class BuildsTargetSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandArgument(2, "<environment>")]
    [Description("The deployment environment the binary targets.")]
    public string Environment { get; init; } = string.Empty;

    [CommandArgument(3, "<runtimeIdentifier>")]
    [Description("The runtime identifier (RID) the native binary targets, e.g. linux-x64.")]
    public string RuntimeIdentifier { get; init; } = string.Empty;
}

internal sealed class BuildsListCommand : AsyncCommand<BuildsVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BuildsVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiNativeBuildsClient client) = await settings.CreateNativeBuildsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Environment");
            table.AddColumn("Runtime");
            table.AddColumn("Status");
            table.AddColumn("Label");
            table.AddColumn("Created");
            table.AddColumn("Failure");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListNativeBuildsResponse response = await client.ListNativeBuildsAsync(
                    settings.BaseWorkflowId,
                    (Models.VersionNumber.Source)settings.VersionNumber,
                    status: settings.Status is { } s ? (Models.NativeBuildStatus.Source)s : default,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.NativeBuildView build in list.NativeBuilds.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(build.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)build.Environment),
                                Markup.Escape((string)build.RuntimeIdentifier),
                                Markup.Escape((string)build.Status),
                                Markup.Escape(OperatorCommandHelpers.FormatOptional(build.BuildLabel)),
                                Markup.Escape(OperatorCommandHelpers.FormatInstant(build.CreatedAt)),
                                Markup.Escape(OperatorCommandHelpers.FormatOptional(build.FailureReason)));
                        }

                        return 0;
                    },
                    Output.Problem,
                    Output.Unexpected);
                if (rc != 0)
                {
                    return rc;
                }

                pageToken = next;
            }
            while (pageToken is not null);

            return OperatorCommandHelpers.Finish(asJson, jsonItems, table, "nativeBuilds");
        }
    }
}

internal sealed class BuildsGetCommand : AsyncCommand<BuildsTargetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BuildsTargetSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiNativeBuildsClient client) = await settings.CreateNativeBuildsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetNativeBuildResponse response = await client.GetNativeBuildAsync(settings.BaseWorkflowId, (Models.VersionNumber.Source)settings.VersionNumber, settings.Environment, settings.RuntimeIdentifier, cancellationToken);
            return response.MatchResult(build => Output.Print(build.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}