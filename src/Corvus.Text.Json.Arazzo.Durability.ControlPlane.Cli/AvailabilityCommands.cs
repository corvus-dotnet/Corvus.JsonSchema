// <copyright file="AvailabilityCommands.cs" company="Endjin Limited">
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

/// <summary>Settings for making/withdrawing a workflow version's availability in an environment.</summary>
internal sealed class AvailabilityTargetSettings : RunsSettings
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
}

/// <summary>Settings for listing the environments a workflow version is available in.</summary>
internal sealed class AvailabilityVersionSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for listing the workflow versions available in an environment.</summary>
internal sealed class AvailabilityEnvironmentSettings : RunsSettings
{
    [CommandArgument(0, "<environment>")]
    [Description("The deployment environment.")]
    public string Environment { get; init; } = string.Empty;

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class AvailabilityMakeCommand : AsyncCommand<AvailabilityTargetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityTargetSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityClient client) = await settings.CreateAvailabilityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using MakeVersionAvailableResponse response = await client.MakeVersionAvailableAsync(settings.BaseWorkflowId, (Models.VersionNumber.Source)settings.VersionNumber, settings.Environment, cancellationToken);
            return response.MatchResult(
                entry => Output.Print(entry.ToString()),
                entry => Output.Print(entry.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityWithdrawCommand : AsyncCommand<AvailabilityTargetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityTargetSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityClient client) = await settings.CreateAvailabilityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using WithdrawVersionAvailabilityResponse response = await client.WithdrawVersionAvailabilityAsync(settings.BaseWorkflowId, (Models.VersionNumber.Source)settings.VersionNumber, settings.Environment, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Withdrew version {settings.VersionNumber} of '{settings.BaseWorkflowId}' from '{settings.Environment}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityListEnvironmentsCommand : AsyncCommand<AvailabilityVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityClient client) = await settings.CreateAvailabilityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Environment");
            table.AddColumn("Made available by");
            table.AddColumn("Since");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListVersionAvailabilityResponse response = await client.ListVersionAvailabilityAsync(
                    settings.BaseWorkflowId,
                    (Models.VersionNumber.Source)settings.VersionNumber,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.AvailabilityEntry entry in list.Availability.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(entry.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)entry.Environment),
                                Markup.Escape((string)entry.CreatedBy),
                                Markup.Escape(AvailabilityCommandHelpers.FormatDate(entry.CreatedAt)));
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

            return AvailabilityCommandHelpers.Finish(asJson, jsonItems, table);
        }
    }
}

internal sealed class AvailabilityListVersionsCommand : AsyncCommand<AvailabilityEnvironmentSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityEnvironmentSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityClient client) = await settings.CreateAvailabilityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Workflow");
            table.AddColumn("Version");
            table.AddColumn("Made available by");
            table.AddColumn("Since");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListEnvironmentAvailabilityResponse response = await client.ListEnvironmentAvailabilityAsync(
                    settings.Environment,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.AvailabilityEntry entry in list.Availability.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(entry.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)entry.BaseWorkflowId),
                                ((int)entry.VersionNumber).ToString(CultureInfo.InvariantCulture),
                                Markup.Escape((string)entry.CreatedBy),
                                Markup.Escape(AvailabilityCommandHelpers.FormatDate(entry.CreatedAt)));
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

            return AvailabilityCommandHelpers.Finish(asJson, jsonItems, table);
        }
    }
}

/// <summary>Shared rendering helpers for the availability list commands.</summary>
internal static class AvailabilityCommandHelpers
{
    public static string FormatDate(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "—";

    public static int Finish(bool asJson, List<string>? jsonItems, Table table)
    {
        if (asJson)
        {
            return Output.Print($"{{\"availability\":[{string.Join(",", jsonItems!)}]}}");
        }

        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
        console.Write(table);
        return 0;
    }
}