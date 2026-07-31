// <copyright file="RunnersCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for listing the runner roster.</summary>
internal sealed class RunnersListSettings : RunsSettings
{
    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class RunnersListCommand : AsyncCommand<RunnersListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunnersListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunnersClient client) = await settings.CreateRunnersClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Runner");
            table.AddColumn("Environment");
            table.AddColumn("Isolation");
            table.AddColumn("Last seen");
            table.AddColumn("Concurrency");
            table.AddColumn("Hosted");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListRunnersResponse response = await client.ListRunnersAsync(
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    page =>
                    {
                        next = page.NextPageToken.IsNotUndefined() ? (string)page.NextPageToken : null;
                        foreach (Models.Runner runner in page.Runners.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(runner.ToString());
                                continue;
                            }

                            // The advertised isolation defaults to InProcess when unstated (ADR 0058).
                            table.AddRow(
                                Markup.Escape((string)runner.RunnerId),
                                Markup.Escape((string)runner.Environment),
                                Markup.Escape(runner.IsolationModel.IsNotUndefined() ? (string)runner.IsolationModel : "InProcess"),
                                Markup.Escape(OperatorCommandHelpers.FormatInstant(runner.LastSeenAt)),
                                ((int)runner.MaxConcurrency).ToString(CultureInfo.InvariantCulture),
                                runner.HostedVersions.GetArrayLength().ToString(CultureInfo.InvariantCulture));
                        }

                        return 0;
                    },
                    Output.Unexpected);
                if (rc != 0)
                {
                    return rc;
                }

                pageToken = next;
            }
            while (pageToken is not null);

            return OperatorCommandHelpers.Finish(asJson, jsonItems, table, "runners");
        }
    }
}