// <copyright file="AccessRequestsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command that targets a single access request by id.</summary>
internal class AccessRequestIdSettings : RunsSettings
{
    [CommandArgument(0, "<requestId>")]
    [Description("The access request id.")]
    public string RequestId { get; init; } = string.Empty;
}

/// <summary>Settings for submitting a new access request.</summary>
internal sealed class AccessRequestSubmitSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id the request targets (approval routes to its administrators).")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandOption("--scope <SCOPE>")]
    [Description("A requested capability scope, e.g. --scope runs:write (repeat for several; at least one required).")]
    public string[] Scopes { get; init; } = [];

    [CommandOption("--reason <TEXT>")]
    [Description("An optional justification for the request.")]
    public string? Reason { get; init; }

    [CommandOption("--duration-seconds <SECONDS>")]
    [Description("The proposed time-bound (PIM) duration in seconds; defaults to the deployment maximum.")]
    public long? DurationSeconds { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => this.Scopes.Length == 0
            ? Spectre.Console.ValidationResult.Error("at least one --scope <scope> is required.")
            : base.Validate();
}

/// <summary>Settings for listing access requests (the caller's own, or a workflow's approver queue).</summary>
internal sealed class AccessRequestListSettings : RunsSettings
{
    [CommandOption("--workflow <ID>")]
    [Description("List the approver queue for this base workflow id (you must administer it); omit to list your own requests.")]
    public string? Workflow { get; init; }

    [CommandOption("--status <STATUS>")]
    [Description("Filter by lifecycle state (Pending / Approved / Eligible / Denied / Withdrawn / Revoked).")]
    public string? Status { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for a decision (approve / deny / revoke) that records an optional note.</summary>
internal sealed class AccessRequestDecisionSettings : AccessRequestIdSettings
{
    [CommandOption("--reason <TEXT>")]
    [Description("An optional note recorded with the decision.")]
    public string? Reason { get; init; }
}

/// <summary>Settings for approving a request as durable eligibility (§16.5.3).</summary>
internal sealed class AccessRequestEligibilitySettings : AccessRequestIdSettings
{
    [CommandOption("--reason <TEXT>")]
    [Description("An optional note recorded with the decision.")]
    public string? Reason { get; init; }

    [CommandOption("--window-seconds <SECONDS>")]
    [Description("How long the eligibility lasts, in seconds; omit for standing eligibility.")]
    public long? WindowSeconds { get; init; }
}

internal sealed class AccessRequestSubmitCommand : AsyncCommand<AccessRequestSubmitSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestSubmitSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.AccessRequestSubmit.Source body = AccessRequestCommandHelpers.Submit(settings);
            await using SubmitAccessRequestResponse response = await client.SubmitAccessRequestAsync(body, cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestListCommand : AsyncCommand<AccessRequestListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
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
                await using ListAccessRequestsResponse response = await client.ListAccessRequestsAsync(
                    status: settings.Status is { } s ? (Models.GetAccessRequestsStatus.Source)s : default,
                    baseWorkflowId: settings.Workflow is { } w ? (Models.JsonString.Source)w : default,
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.AccessRequestView view in list.AccessRequests.EnumerateArray())
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

            string combined = $"{{\"accessRequests\":[{string.Join(",", items)}]}}";
            if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return Output.Print(combined);
            }

            using ParsedJsonDocument<Models.AccessRequestList> rendered = ParsedJsonDocument<Models.AccessRequestList>.Parse(System.Text.Encoding.UTF8.GetBytes(combined).AsMemory());
            return AccessRequestCommandHelpers.RenderTable(rendered.RootElement, settings.Workflow);
        }
    }
}

internal sealed class AccessRequestGetCommand : AsyncCommand<AccessRequestIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetAccessRequestResponse response = await client.GetAccessRequestAsync(settings.RequestId, cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestApproveCommand : AsyncCommand<AccessRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ApproveAccessRequestResponse response = await client.ApproveAccessRequestAsync(settings.RequestId, AccessRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestApproveAsEligibleCommand : AsyncCommand<AccessRequestEligibilitySettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestEligibilitySettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.AccessRequestEligibilityNote.Source body = AccessRequestCommandHelpers.EligibilityNote(settings.Reason, settings.WindowSeconds);
            await using ApproveAccessRequestAsEligibleResponse response = await client.ApproveAccessRequestAsEligibleAsync(settings.RequestId, body, cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestDenyCommand : AsyncCommand<AccessRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DenyAccessRequestResponse response = await client.DenyAccessRequestAsync(settings.RequestId, AccessRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestWithdrawCommand : AsyncCommand<AccessRequestIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using WithdrawAccessRequestResponse response = await client.WithdrawAccessRequestAsync(settings.RequestId, AccessRequestCommandHelpers.Note(null), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AccessRequestRevokeCommand : AsyncCommand<AccessRequestDecisionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AccessRequestDecisionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAccessRequestsClient client) = await settings.CreateAccessRequestsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RevokeAccessRequestResponse response = await client.RevokeAccessRequestAsync(settings.RequestId, AccessRequestCommandHelpers.Note(settings.Reason), cancellationToken);
            return response.MatchResult(view => Output.Print(view.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared request-body construction and rendering for the access-requests commands.</summary>
internal static class AccessRequestCommandHelpers
{
    public static Models.AccessRequestSubmit.Source Submit(AccessRequestSubmitSettings settings)
        => new((ref Models.AccessRequestSubmit.Builder b) => b.Create(
            baseWorkflowId: settings.BaseWorkflowId,
            requestedScopes: new Models.AccessRequestSubmit.JsonStringArray.Source((ref Models.AccessRequestSubmit.JsonStringArray.Builder ab) =>
            {
                foreach (string scope in settings.Scopes)
                {
                    ab.AddItem(scope);
                }
            }),
            reason: settings.Reason is { } reason ? (Models.JsonString.Source)reason : default,
            requestedDurationSeconds: settings.DurationSeconds is { } duration ? (Models.AccessRequestSubmit.RequestedDurationSecondsEntity.Source)duration : default));

    // With a reason, builds the note object; without one, a default (undefined) Source so the generated client
    // sends NO body (the decision endpoints' request body is optional, and the client omits an undefined body).
    public static Models.AccessRequestDecisionNote.Source Note(string? reason)
        => reason is { } r
            ? new((ref Models.AccessRequestDecisionNote.Builder b) => b.Create(reason: (Models.JsonString.Source)r))
            : default;

    public static Models.AccessRequestEligibilityNote.Source EligibilityNote(string? reason, long? windowSeconds)
        => reason is null && windowSeconds is null
            ? default
            : new((ref Models.AccessRequestEligibilityNote.Builder b) => b.Create(
                eligibilityWindowSeconds: windowSeconds is { } window ? (Models.AccessRequestEligibilityNote.EligibilityWindowSecondsEntity.Source)window : default,
                reason: reason is { } r ? (Models.JsonString.Source)r : default));

    public static int RenderTable(Models.AccessRequestList list, string? workflow)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });

        var table = new Table().Border(TableBorder.Rounded);
        table.Title = new TableTitle(Markup.Escape(workflow is { } w ? $"access requests for {w}" : "my access requests"));
        table.AddColumn("Id");
        table.AddColumn("Workflow");
        table.AddColumn("Subject");
        table.AddColumn("Scopes");
        table.AddColumn("Status");
        table.AddColumn("Decided by");

        int count = 0;
        foreach (Models.AccessRequestView view in list.AccessRequests.EnumerateArray())
        {
            var scopes = new List<string>();
            foreach (Models.JsonString scope in view.RequestedScopes.EnumerateArray())
            {
                scopes.Add((string)scope);
            }

            table.AddRow(
                Markup.Escape((string)view.Id),
                Markup.Escape((string)view.BaseWorkflowId),
                Markup.Escape((string)view.SubjectClaimValue),
                Markup.Escape(string.Join(", ", scopes)),
                Markup.Escape((string)view.Status),
                Markup.Escape(view.DecidedBy.IsNotUndefined() ? (string)view.DecidedBy : "—"));
            count++;
        }

        if (count == 0)
        {
            console.MarkupLine("[dim]No access requests.[/]");
            return 0;
        }

        console.Write(table);
        return 0;
    }
}