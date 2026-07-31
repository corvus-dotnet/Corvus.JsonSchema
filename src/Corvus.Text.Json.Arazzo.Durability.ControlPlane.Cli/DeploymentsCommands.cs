// <copyright file="DeploymentsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for listing a workflow version's serverless deployments.</summary>
internal sealed class DeploymentsVersionSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandOption("--status <STATUS>")]
    [Description("Restrict to deployments in this lifecycle state: Queued, Deploying, Deployed, or Failed.")]
    public string? Status { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings addressing one serverless deployment by its (environment, runtime target).</summary>
internal sealed class DeploymentsTargetSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandArgument(2, "<environment>")]
    [Description("The deployment environment.")]
    public string Environment { get; init; } = string.Empty;

    [CommandArgument(3, "<runtimeIdentifier>")]
    [Description("The runtime identifier (RID) the deployed binary targets, e.g. linux-x64.")]
    public string RuntimeIdentifier { get; init; } = string.Empty;
}

internal sealed class DeploymentsListCommand : AsyncCommand<DeploymentsVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DeploymentsVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDeploymentsClient client) = await settings.CreateDeploymentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Environment");
            table.AddColumn("Runtime");
            table.AddColumn("Status");
            table.AddColumn("Function URL");
            table.AddColumn("Created");
            table.AddColumn("Failure");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListDeploymentsResponse response = await client.ListDeploymentsAsync(
                    settings.BaseWorkflowId,
                    (Models.VersionNumber.Source)settings.VersionNumber,
                    status: settings.Status is { } s ? (Models.DeploymentStatus.Source)s : default,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.DeploymentView deployment in list.Deployments.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(deployment.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)deployment.Environment),
                                Markup.Escape((string)deployment.RuntimeIdentifier),
                                Markup.Escape((string)deployment.Status),
                                Markup.Escape(OperatorCommandHelpers.FormatOptional(deployment.FunctionUrl)),
                                Markup.Escape(OperatorCommandHelpers.FormatInstant(deployment.CreatedAt)),
                                Markup.Escape(OperatorCommandHelpers.FormatOptional(deployment.FailureReason)));
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

            return OperatorCommandHelpers.Finish(asJson, jsonItems, table, "deployments");
        }
    }
}

internal sealed class DeploymentsGetCommand : AsyncCommand<DeploymentsTargetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DeploymentsTargetSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDeploymentsClient client) = await settings.CreateDeploymentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetDeploymentResponse response = await client.GetDeploymentAsync(settings.BaseWorkflowId, (Models.VersionNumber.Source)settings.VersionNumber, settings.Environment, settings.RuntimeIdentifier, cancellationToken);
            return response.MatchResult(deployment => Output.Print(deployment.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}