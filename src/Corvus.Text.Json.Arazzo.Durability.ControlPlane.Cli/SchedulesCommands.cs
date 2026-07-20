// <copyright file="SchedulesCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings addressing a single schedule by its id (#896).</summary>
internal sealed class ScheduleIdSettings : RunsSettings
{
    [CommandArgument(0, "<scheduleId>")]
    [Description("The schedule's stable id.")]
    public string ScheduleId { get; init; } = string.Empty;
}

/// <summary>Settings for listing schedules.</summary>
internal sealed class SchedulesListSettings : RunsSettings
{
    [CommandOption("--environment <ENVIRONMENT>")]
    [Description("Only show schedules pinned to this environment.")]
    public string? Environment { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for creating a schedule.</summary>
internal sealed class ScheduleCreateSettings : RunsSettings
{
    [CommandArgument(0, "<scheduleId>")]
    [Description("The schedule's stable id (also its idempotency key).")]
    public string ScheduleId { get; init; } = string.Empty;

    [CommandArgument(1, "<environment>")]
    [Description("The environment the schedule is pinned to.")]
    public string Environment { get; init; } = string.Empty;

    [CommandArgument(2, "<baseWorkflowId>")]
    [Description("The target's base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(3, "<versionNumber>")]
    [Description("The target's 1-based version number.")]
    public int VersionNumber { get; init; }

    [CommandOption("--cron <EXPRESSION>")]
    [Description("The cron expression driving occurrences (5-field, or 6-field with --include-seconds).")]
    public string Cron { get; init; } = string.Empty;

    [CommandOption("--time-zone <IANA>")]
    [Description("The IANA time zone the cadence is expressed in. Defaults to UTC.")]
    public string? TimeZone { get; init; }

    [CommandOption("--include-seconds")]
    [Description("The cron expression carries a leading seconds field.")]
    public bool IncludeSeconds { get; init; }

    [CommandOption("--inputs <JSON>")]
    [Description("A JSON object of inputs each occurrence's target run starts with. Omit to start with no inputs.")]
    public string? Inputs { get; init; }

    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrWhiteSpace(this.Cron)
            ? Spectre.Console.ValidationResult.Error("--cron <EXPRESSION> is required.")
            : Spectre.Console.ValidationResult.Success();
}

internal sealed class SchedulesListCommand : AsyncCommand<SchedulesListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SchedulesListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSchedulesClient client) = await settings.CreateSchedulesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Schedule");
            table.AddColumn("Environment");
            table.AddColumn("Target");
            table.AddColumn("Cron");
            table.AddColumn("Status");
            table.AddColumn("Next occurrence");
            List<string>? jsonItems = asJson ? [] : null;

            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListSchedulesResponse response = await client.ListSchedulesAsync(
                    environment: settings.Environment is { } env ? (Models.JsonString.Source)env : default,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.Schedule schedule in list.Schedules.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(schedule.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)schedule.ScheduleId),
                                Markup.Escape((string)schedule.Environment),
                                Markup.Escape((string)schedule.TargetWorkflowId),
                                Markup.Escape((string)schedule.Cron),
                                Markup.Escape((string)schedule.Status),
                                Markup.Escape(SchedulesCommandHelpers.FormatInstant(schedule.NextOccurrence)));
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

            return SchedulesCommandHelpers.Finish(asJson, jsonItems, table);
        }
    }
}

internal sealed class SchedulesCreateCommand : AsyncCommand<ScheduleCreateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScheduleCreateSettings settings, CancellationToken cancellationToken)
    {
        // Parse the optional inputs object up front and hold it alive through the request send: the schedule-create body
        // bridges it in as a JSON value, resolved when the request is serialized inside CreateScheduleAsync. The document
        // (a class) is captured by the body builder; the JsonObject.Source (a ref struct) is derived inside it.
        using ParsedJsonDocument<Models.JsonObject>? inputsDoc = settings.Inputs is { } json
            ? ParsedJsonDocument<Models.JsonObject>.Parse(Encoding.UTF8.GetBytes(json))
            : null;

        (HttpClient http, HttpClientTransport transport, ApiSchedulesClient client) = await settings.CreateSchedulesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.ScheduleCreate.Source body = new((ref Models.ScheduleCreate.Builder b) =>
            {
                Models.ScheduleCreate.TheIanaTimeZoneIdTheCadenceIsExpressedIn.Source timeZone =
                    settings.TimeZone is { } tz ? (Models.ScheduleCreate.TheIanaTimeZoneIdTheCadenceIsExpressedIn.Source)tz : default;
                Models.ScheduleCreate.WhetherTheCronExpressionCarriesALeadingSecondsField.Source includeSeconds =
                    settings.IncludeSeconds ? (Models.ScheduleCreate.WhetherTheCronExpressionCarriesALeadingSecondsField.Source)true : default;
                Models.JsonObject.Source targetInputs = default;
                if (inputsDoc is { } d)
                {
                    targetInputs = d.RootElement;
                }

                b.Create(
                    cron: settings.Cron,
                    environment: settings.Environment,
                    scheduleId: settings.ScheduleId,
                    targetBaseWorkflowId: settings.BaseWorkflowId,
                    targetVersionNumber: (Models.JsonInt32.Source)settings.VersionNumber,
                    includeSeconds: includeSeconds,
                    targetInputs: targetInputs,
                    timeZone: timeZone);
            });
            await using CreateScheduleResponse response = await client.CreateScheduleAsync(body, cancellationToken);
            return response.MatchResult(
                schedule => Output.Print(schedule.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Validation,
                Output.Unexpected);
        }
    }
}

internal sealed class SchedulesGetCommand : AsyncCommand<ScheduleIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScheduleIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSchedulesClient client) = await settings.CreateSchedulesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetScheduleResponse response = await client.GetScheduleAsync(settings.ScheduleId, cancellationToken);
            return response.MatchResult(
                schedule => Output.Print(schedule.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class SchedulesDeleteCommand : AsyncCommand<ScheduleIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScheduleIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSchedulesClient client) = await settings.CreateSchedulesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteScheduleResponse response = await client.DeleteScheduleAsync(settings.ScheduleId, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted schedule '{settings.ScheduleId}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SchedulesRunNowCommand : AsyncCommand<ScheduleIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScheduleIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSchedulesClient client) = await settings.CreateSchedulesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RunScheduleNowResponse response = await client.RunScheduleNowAsync(settings.ScheduleId, cancellationToken);
            return response.MatchResult(
                accepted => Output.Print(accepted.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Validation,
                Output.Unexpected);
        }
    }
}

/// <summary>Shared rendering and request-body construction for the schedules commands.</summary>
internal static class SchedulesCommandHelpers
{
    public static string FormatInstant(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "—";

    public static int Finish(bool asJson, List<string>? jsonItems, Table table)
    {
        if (asJson)
        {
            return Output.Print($"{{\"schedules\":[{string.Join(",", jsonItems!)}]}}");
        }

        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
        console.Write(table);
        return 0;
    }
}