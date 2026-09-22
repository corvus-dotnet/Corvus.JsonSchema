// <copyright file="EnvironmentsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command that targets a single environment by name.</summary>
internal class EnvironmentNameSettings : RunsSettings
{
    [CommandArgument(0, "<name>")]
    [Description("The environment's name (its stable identity, e.g. production).")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Settings for listing environments.</summary>
internal sealed class EnvironmentListSettings : RunsSettings
{
    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>
/// The environment's execution-budget override (ADR 0068): each limit named tightens the deployment's ceiling for runs
/// in the environment, and a limit left out is the ceiling's. The server refuses a limit wider than the ceiling.
/// </summary>
internal class EnvironmentBudgetSettings : EnvironmentNameSettings
{
    [CommandOption("--max-steps <COUNT>")]
    [Description("Budget: the most step attempts a run may make, retries and revisits counted.")]
    public long? MaxSteps { get; init; }

    [CommandOption("--wall-clock-seconds <SECONDS>")]
    [Description("Budget: the longest a run may live from creation, in seconds.")]
    public long? WallClockSeconds { get; init; }

    [CommandOption("--max-sub-workflow-depth <DEPTH>")]
    [Description("Budget: how deep sub-workflows may nest.")]
    public long? MaxSubWorkflowDepth { get; init; }

    [CommandOption("--retry-after-ceiling-seconds <SECONDS>")]
    [Description("Budget: the ceiling a step's declared retryAfter delay is clamped to, in seconds.")]
    public long? RetryAfterCeilingSeconds { get; init; }

    [CommandOption("--step-timeout-seconds <SECONDS>")]
    [Description("Budget: the longest a single step's request may take, in seconds.")]
    public long? StepTimeoutSeconds { get; init; }

    [CommandOption("--max-response-bytes <BYTES>")]
    [Description("Budget: the largest response body a single step may read, in bytes.")]
    public long? MaxResponseBytes { get; init; }

    /// <summary>Gets the limits these settings name.</summary>
    public BudgetLimits Limits => new(this.MaxSteps, this.WallClockSeconds, this.MaxSubWorkflowDepth, this.RetryAfterCeilingSeconds, this.StepTimeoutSeconds, this.MaxResponseBytes);
}

/// <summary>Settings for creating a governed environment. The deployment stamps the creator's internal tenant tag onto
/// managementTags and grants the creator administration (§7.7).</summary>
internal sealed class EnvironmentCreateSettings : EnvironmentBudgetSettings
{
    [CommandOption("--display-name <TEXT>")]
    [Description("An optional human-friendly display name (defaults to the name).")]
    public string? DisplayName { get; init; }

    [CommandOption("--description <TEXT>")]
    [Description("An optional human description.")]
    public string? Description { get; init; }

    [CommandOption("--manage <KEY=VALUE>")]
    [Description("A management tag scoping who may manage and see the environment, e.g. --manage team=ops (repeatable).")]
    public ILookup<string, string>? ManagementTags { get; init; }
}

/// <summary>Settings for updating an environment's mutable metadata. Only the supplied fields change (a PATCH-style
/// merge); the name and created-* audit are immutable. An administrator may re-tag the managementTags reach scope.</summary>
internal sealed class EnvironmentUpdateSettings : EnvironmentBudgetSettings
{
    [CommandOption("--clear-budget")]
    [Description("Remove the environment's budget override, so its runs take the deployment's ceiling. Budget options given with it start from nothing.")]
    public bool ClearBudget { get; init; }

    [CommandOption("--display-name <TEXT>")]
    [Description("Replace the display name (preserved if omitted).")]
    public string? DisplayName { get; init; }

    [CommandOption("--description <TEXT>")]
    [Description("Replace the description (preserved if omitted).")]
    public string? Description { get; init; }

    [CommandOption("--manage <KEY=VALUE>")]
    [Description("Re-tag who may manage and see the environment, e.g. --manage team=ops (repeatable). Replaces the non-internal management tags; preserved if omitted; the reserved 'sys:' prefix is not permitted.")]
    public ILookup<string, string>? ManagementTags { get; init; }
}

internal sealed class EnvironmentListCommand : AsyncCommand<EnvironmentListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Display name");
            table.AddColumn("Description");
            table.AddColumn("Created by");
            table.AddColumn("Created");
            List<string>? jsonItems = asJson ? [] : null;

            // Walk every keyset page (the store pages server-side) so the table shows the complete reach-visible set.
            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListEnvironmentsResponse response = await client.ListEnvironmentsAsync(
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.EnvironmentSummary summary in list.Environments.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(summary.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)summary.Name),
                                Markup.Escape(summary.DisplayName.IsNotUndefined() ? (string)summary.DisplayName : "—"),
                                Markup.Escape(summary.Description.IsNotUndefined() ? (string)summary.Description : "—"),
                                Markup.Escape((string)summary.CreatedBy),
                                Markup.Escape(EnvironmentCommandHelpers.FormatDate(summary.CreatedAt)));
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

            if (jsonItems is not null)
            {
                return Output.Print($"{{\"environments\":[{string.Join(",", jsonItems)}]}}");
            }

            IAnsiConsole console = OperatorCommandHelpers.CreateConsole();
            console.Write(table);
            return 0;
        }
    }
}

internal sealed class EnvironmentGetCommand : AsyncCommand<EnvironmentNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetEnvironmentResponse response = await client.GetEnvironmentAsync(settings.Name, cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Settings for showing an environment's execution budget.</summary>
internal sealed class EnvironmentBudgetShowSettings : EnvironmentNameSettings
{
    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class EnvironmentBudgetCommand : AsyncCommand<EnvironmentBudgetShowSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentBudgetShowSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetEnvironmentExecutionBudgetResponse response = await client.GetEnvironmentExecutionBudgetAsync(settings.Name, cancellationToken);
            bool json = string.Equals(settings.Output, "json", StringComparison.OrdinalIgnoreCase);
            return response.MatchResult(budget => json ? Output.Print(budget.ToString()) : RenderTable(budget), Output.Problem, Output.Unexpected);
        }
    }

    // One row per limit: what the environment authored, what a run started now is held to, and what the deployment
    // allows. An empty Override cell is a limit the environment leaves to the ceiling.
    private static int RenderTable(Models.EnvironmentExecutionBudget budget)
    {
        IAnsiConsole console = OperatorCommandHelpers.CreateConsole();
        BudgetLimits authored = BudgetLimits.From(budget.Override);
        BudgetLimits effective = BudgetLimits.From(budget.Effective);
        BudgetLimits ceiling = BudgetLimits.From(budget.Ceiling);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Limit");
        table.AddColumn("Override");
        table.AddColumn("Effective");
        table.AddColumn("Ceiling");
        foreach (BudgetLimits.Row row in BudgetLimits.Rows)
        {
            table.AddRow(row.Label, Markup.Escape(row.Format(authored)), Markup.Escape(row.Format(effective)), Markup.Escape(row.Format(ceiling)));
        }

        console.Write(table);
        return 0;
    }
}

internal sealed class EnvironmentCreateCommand : AsyncCommand<EnvironmentCreateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentCreateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using CreateEnvironmentResponse response = await client.CreateEnvironmentAsync(EnvironmentCommandHelpers.BuildWrite(settings), cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentUpdateCommand : AsyncCommand<EnvironmentUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // PATCH-style: read the current environment and overlay only the supplied fields, so the operator names just
            // what moved. The server's update is a full replace of the mutable metadata, so the merge happens here.
            (string? DisplayName, string? Description, BudgetLimits Budget) snapshot;
            await using (GetEnvironmentResponse current = await client.GetEnvironmentAsync(settings.Name, cancellationToken))
            {
                if (current.StatusCode != 200)
                {
                    return current.MatchResult(_ => 0, Output.Problem, Output.Unexpected);
                }

                snapshot = current.MatchResult(
                    summary => (
                        summary.DisplayName.IsNotUndefined() ? (string)summary.DisplayName : null,
                        summary.Description.IsNotUndefined() ? (string)summary.Description : null,
                        BudgetLimits.From(summary.ExecutionBudget)),
                    _ => default,
                    _ => default);
            }

            string? displayName = settings.DisplayName ?? snapshot.DisplayName;
            string? description = settings.Description ?? snapshot.Description;

            // managementTags carry forward server-side when omitted (a re-tag replaces them), so unlike display
            // name / description it needs no snapshot merge — send it only when --manage is supplied.
            //
            // The budget is replace-or-carry on the server: omitted, the stored override stands; supplied, it replaces
            // the override whole. So naming one limit must not drop the others, and the limits named here are laid
            // over the ones already authored. --clear-budget starts from nothing, and an override with no limits is
            // how the API says there is none.
            BudgetLimits? budget = settings.ClearBudget
                ? settings.Limits
                : settings.Limits.IsEmpty ? null : settings.Limits.Over(snapshot.Budget);
            Models.EnvironmentUpdate.Source body = EnvironmentCommandHelpers.BuildUpdate(displayName, description, settings.ManagementTags, budget);
            await using UpdateEnvironmentResponse response = await client.UpdateEnvironmentAsync(settings.Name, body, cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentDeleteCommand : AsyncCommand<EnvironmentNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteEnvironmentResponse response = await client.DeleteEnvironmentAsync(settings.Name, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted environment '{settings.Name}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentAdminListCommand : AsyncCommand<EnvironmentNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ListEnvironmentAdministratorsResponse response = await client.ListEnvironmentAdministratorsAsync(settings.Name, cancellationToken);
            return response.MatchResult(
                list => AdministratorCommandHelpers.RenderTable(list, $"environment {settings.Name}"),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentAdminAddCommand : AsyncCommand<EnvironmentAdminMemberSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentAdminMemberSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.GranteeKind.Source kind = settings.Kind;
            Models.JsonString.Source value = settings.Value;
            await using AddEnvironmentAdministratorResponse response = await client.AddEnvironmentAdministratorAsync(settings.Name, Models.GranteeReference.Build(kind: kind, value: value), cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentAdminTransferCommand : AsyncCommand<EnvironmentAdminTransferSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentAdminTransferSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.AdministratorSetWrite.Source body = AdministratorCommandHelpers.SetWrite(settings.Administrators!);
            await using TransferEnvironmentAdministrationResponse response = await client.TransferEnvironmentAdministrationAsync(settings.Name, body, cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class EnvironmentAdminRemoveCommand : AsyncCommand<EnvironmentAdminRemoveSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, EnvironmentAdminRemoveSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiEnvironmentsClient client) = await settings.CreateEnvironmentsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RemoveEnvironmentAdministratorResponse response = await client.RemoveEnvironmentAdministratorAsync(settings.Name, settings.Digest, cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Settings for adding a single environment administrator, named as the grantee the server resolves to its
/// exact identity (ADR 0008): a well-known kind and a value (e.g. <c>team acme</c>), never a raw internal tag.</summary>
internal sealed class EnvironmentAdminMemberSettings : EnvironmentNameSettings
{
    [CommandArgument(1, "<kind>")]
    [Description("The grantee kind: person, team, role or workflow.")]
    public string Kind { get; init; } = string.Empty;

    [CommandArgument(2, "<value>")]
    [Description("The grantee value (a subject id, a team or role name, or a workflow id).")]
    public string Value { get; init; } = string.Empty;
}

/// <summary>Settings for removing an environment administrator by its stable identity <c>digest</c> (the removal key
/// shown by <c>environments administrators list</c>).</summary>
internal sealed class EnvironmentAdminRemoveSettings : EnvironmentNameSettings
{
    [CommandArgument(1, "<digest>")]
    [Description("The identity digest of the administrator to remove (copy it from the administrators list).")]
    public string Digest { get; init; } = string.Empty;
}

/// <summary>Settings for replacing the whole environment administrator set. Each <c>--admin kind=value</c> names one
/// new administrator grantee the server resolves; a kind may repeat to name several.</summary>
internal sealed class EnvironmentAdminTransferSettings : EnvironmentNameSettings
{
    [CommandOption("--admin <KIND=VALUE>")]
    [Description("A new administrator grantee, e.g. --admin team=acme (repeat to name several; at least one required).")]
    public ILookup<string, string>? Administrators { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => this.Administrators?.Any() != true
            ? Spectre.Console.ValidationResult.Error("at least one --admin <kind=value> is required.")
            : base.Validate();
}

/// <summary>Shared rendering and request-body construction for the environments commands.</summary>
internal static class EnvironmentCommandHelpers
{
    public static string FormatDate(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "—";

    public static Models.EnvironmentCreate.Source BuildWrite(EnvironmentCreateSettings s)
        => new((ref Models.EnvironmentCreate.Builder b) =>
        {
            Models.JsonString.Source displayName = s.DisplayName is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonString.Source description = s.Description is { } desc ? (Models.JsonString.Source)desc : default;
            b.Create(
                name: s.Name,
                description: description,
                displayName: displayName,
                executionBudget: s.Limits.IsEmpty ? default(Models.ExecutionBudget.Source) : s.Limits.ToSource(),
                managementTags: WriteManagementTags(s.ManagementTags));
        });

    public static Models.EnvironmentUpdate.Source BuildUpdate(string? displayName, string? description, ILookup<string, string>? managementTags, BudgetLimits? budget = null)
        => new((ref Models.EnvironmentUpdate.Builder b) =>
        {
            Models.JsonString.Source displayNameSource = displayName is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonString.Source descriptionSource = description is { } desc ? (Models.JsonString.Source)desc : default;
            b.Create(
                description: descriptionSource,
                displayName: displayNameSource,
                executionBudget: budget is { } limits ? limits.ToSource() : default(Models.ExecutionBudget.Source),
                managementTags: WriteUpdateManagementTags(managementTags));
        });

    private static Models.EnvironmentCreate.EnvironmentSecurityTagArray.Source WriteManagementTags(ILookup<string, string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        return new Models.EnvironmentCreate.EnvironmentSecurityTagArray.Source((ref Models.EnvironmentCreate.EnvironmentSecurityTagArray.Builder ab) =>
        {
            foreach (IGrouping<string, string> group in tags)
            {
                foreach (string value in group)
                {
                    ab.AddItem(new Models.EnvironmentSecurityTag.Source((ref Models.EnvironmentSecurityTag.Builder tb) => tb.Create(group.Key, value)));
                }
            }
        });
    }

    private static Models.EnvironmentUpdate.EnvironmentSecurityTagArray.Source WriteUpdateManagementTags(ILookup<string, string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        return new Models.EnvironmentUpdate.EnvironmentSecurityTagArray.Source((ref Models.EnvironmentUpdate.EnvironmentSecurityTagArray.Builder ab) =>
        {
            foreach (IGrouping<string, string> group in tags)
            {
                foreach (string value in group)
                {
                    ab.AddItem(new Models.EnvironmentSecurityTag.Source((ref Models.EnvironmentSecurityTag.Builder tb) => tb.Create(group.Key, value)));
                }
            }
        });
    }
}