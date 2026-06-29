// <copyright file="AvailabilityRequestsCommands.cs" company="Endjin Limited">
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

/// <summary>Settings for a command that targets a single availability request by id.</summary>
internal class AvailabilityRequestIdSettings : RunsSettings
{
    [CommandArgument(0, "<requestId>")]
    [Description("The availability request id.")]
    public string RequestId { get; init; } = string.Empty;
}

/// <summary>Settings for submitting a new availability ("promotion") request.</summary>
internal sealed class AvailabilityRequestSubmitSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id whose version to make available.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<versionNumber>")]
    [Description("The version number to make available.")]
    public int VersionNumber { get; init; }

    [CommandArgument(2, "<environment>")]
    [Description("The target environment (approval routes to its administrators).")]
    public string Environment { get; init; } = string.Empty;

    [CommandOption("--reason <TEXT>")]
    [Description("An optional justification for the request.")]
    public string? Reason { get; init; }
}

/// <summary>Settings for listing availability requests (the caller's own, an environment's queue, or the approver inbox).</summary>
internal sealed class AvailabilityRequestListSettings : RunsSettings
{
    [CommandOption("--environment <NAME>")]
    [Description("List the request queue for this environment (you must administer it); omit for your own requests.")]
    public string? Environment { get; init; }

    [CommandOption("--inbox")]
    [Description("List the approver inbox — every request across the environments you administer.")]
    public bool Inbox { get; init; }

    [CommandOption("--status <STATUS>")]
    [Description("Filter by lifecycle state (Pending / Approved / Denied / Withdrawn).")]
    public string? Status { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for a decision (approve / deny / withdraw) that records an optional note.</summary>
internal sealed class AvailabilityRequestDecisionSettings : AvailabilityRequestIdSettings
{
    [CommandOption("--reason <TEXT>")]
    [Description("An optional note recorded with the decision.")]
    public string? Reason { get; init; }
}

internal sealed class AvailabilityRequestSubmitCommand : AsyncCommand<AvailabilityRequestSubmitSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestSubmitSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.AvailabilityRequestSubmit.Source body = AvailabilityRequestCommandHelpers.Submit(settings);
            await using SubmitAvailabilityRequestResponse response = await client.SubmitAvailabilityRequestAsync(body, cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityRequestListCommand : AsyncCommand<AvailabilityRequestListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // Walk every keyset page (the store pages server-side) so the output is the complete queue, never the first
            // page alone. Each view is materialised (ToString) while its page response is alive, then the accumulated set
            // is rendered/printed once. The Source filter values are built inline per call (they are ref structs that
            // cannot be held across the loop's await).
            var items = new List<string>();
            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListAvailabilityRequestsResponse response = await client.ListAvailabilityRequestsAsync(
                    status: settings.Status is { } s ? (Models.GetAvailabilityRequestsStatus.Source)s : default,
                    environment: settings.Environment is { } e ? (Models.JsonString.Source)e : default,
                    scope: settings.Inbox && settings.Environment is null ? (Models.GetAvailabilityRequestsScope.Source)"queue" : default,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.AvailabilityRequestView view in list.AvailabilityRequests.EnumerateArray())
                        {
                            items.Add(view.ToString());
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

            string combined = $"{{\"availabilityRequests\":[{string.Join(",", items)}]}}";
            if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return Output.Print(combined);
            }

            using ParsedJsonDocument<Models.AvailabilityRequestList> rendered = ParsedJsonDocument<Models.AvailabilityRequestList>.Parse(System.Text.Encoding.UTF8.GetBytes(combined).AsMemory());
            return AvailabilityRequestCommandHelpers.RenderTable(rendered.RootElement, settings.Environment, settings.Inbox);
        }
    }
}

internal sealed class AvailabilityRequestGetCommand : AsyncCommand<AvailabilityRequestIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetAvailabilityRequestResponse response = await client.GetAvailabilityRequestAsync(settings.RequestId, cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityRequestApproveCommand : AsyncCommand<AvailabilityRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ApproveAvailabilityRequestResponse response = await client.ApproveAvailabilityRequestAsync(settings.RequestId, AvailabilityRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityRequestDenyCommand : AsyncCommand<AvailabilityRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DenyAvailabilityRequestResponse response = await client.DenyAvailabilityRequestAsync(settings.RequestId, AvailabilityRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AvailabilityRequestWithdrawCommand : AsyncCommand<AvailabilityRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAvailabilityRequestsClient client) = await settings.CreateAvailabilityRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using WithdrawAvailabilityRequestResponse response = await client.WithdrawAvailabilityRequestAsync(settings.RequestId, AvailabilityRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared request-body construction and rendering for the availability-requests commands.</summary>
internal static class AvailabilityRequestCommandHelpers
{
    public static Models.AvailabilityRequestSubmit.Source Submit(AvailabilityRequestSubmitSettings settings)
        => new((ref Models.AvailabilityRequestSubmit.Builder b) => b.Create(
            baseWorkflowId: settings.BaseWorkflowId,
            versionNumber: (Models.AvailabilityRequestSubmit.TheVersionNumberToMakeAvailable.Source)settings.VersionNumber,
            environment: settings.Environment,
            reason: settings.Reason is { } reason ? (Models.JsonString.Source)reason : default));

    // With a reason, builds the note object; without one, a default (undefined) Source so the generated client sends NO
    // body (the decision endpoints' request body is optional, and the client omits an undefined body).
    public static Models.AvailabilityRequestDecisionNote.Source Note(string? reason)
        => reason is { } r
            ? new((ref Models.AvailabilityRequestDecisionNote.Builder b) => b.Create(reason: (Models.JsonString.Source)r))
            : default;

    public static int RenderTable(Models.AvailabilityRequestList list, string? environment, bool inbox)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });

        string title = environment is { } e ? $"availability requests for {e}" : inbox ? "availability approver inbox" : "my availability requests";
        var table = new Table().Border(TableBorder.Rounded);
        table.Title = new TableTitle(Markup.Escape(title));
        table.AddColumn("Id");
        table.AddColumn("Workflow");
        table.AddColumn("Version");
        table.AddColumn("Environment");
        table.AddColumn("Status");
        table.AddColumn("Decided by");

        int count = 0;
        foreach (Models.AvailabilityRequestView view in list.AvailabilityRequests.EnumerateArray())
        {
            table.AddRow(
                Markup.Escape((string)view.Id),
                Markup.Escape((string)view.BaseWorkflowId),
                Markup.Escape(((int)view.VersionNumber).ToString(CultureInfo.InvariantCulture)),
                Markup.Escape((string)view.Environment),
                Markup.Escape((string)view.Status),
                Markup.Escape(view.DecidedBy.IsNotUndefined() ? (string)view.DecidedBy : "—"));
            count++;
        }

        if (count == 0)
        {
            console.MarkupLine("[dim]No availability requests.[/]");
            return 0;
        }

        console.Write(table);
        return 0;
    }
}