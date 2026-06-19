// <copyright file="AdministratorsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command that targets a base workflow id's administrator set.</summary>
internal class BaseWorkflowIdSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id whose administrator set is being managed.")]
    public string BaseWorkflowId { get; init; } = string.Empty;
}

/// <summary>Settings for listing a base id's administrators.</summary>
internal sealed class AdministratorListSettings : BaseWorkflowIdSettings
{
    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for adding or removing a single administrator identity, named by the deployment-mapped grant
/// <c>{dimension, value}</c> (e.g. <c>tenant acme</c>) — never a raw internal tag.</summary>
internal sealed class AdministratorMemberSettings : BaseWorkflowIdSettings
{
    [CommandArgument(1, "<dimension>")]
    [Description("The identity dimension (e.g. tenant, workflow).")]
    public string Dimension { get; init; } = string.Empty;

    [CommandArgument(2, "<value>")]
    [Description("The identity value (e.g. the tenant id).")]
    public string Value { get; init; } = string.Empty;
}

/// <summary>Settings for replacing the whole administrator set. Each <c>--admin dimension=value</c> names one new
/// administrator; a dimension may repeat to name several administrators in the same dimension.</summary>
internal sealed class AdministratorTransferSettings : BaseWorkflowIdSettings
{
    [CommandOption("--admin <DIMENSION=VALUE>")]
    [Description("A new administrator identity, e.g. --admin tenant=acme (repeat to name several; at least one required).")]
    public ILookup<string, string>? Administrators { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => this.Administrators?.Any() != true
            ? Spectre.Console.ValidationResult.Error("at least one --admin <dimension=value> is required.")
            : base.Validate();
}

internal sealed class AdministratorListCommand : AsyncCommand<AdministratorListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AdministratorListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAdministratorsClient client) = await settings.CreateAdministratorsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ListAdministratorsResponse response = await client.ListAdministratorsAsync(settings.BaseWorkflowId, cancellationToken);
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            return response.MatchResult(
                list => asJson ? Output.Print(list.ToString()) : AdministratorCommandHelpers.RenderTable(list, settings.BaseWorkflowId),
                Output.Unexpected);
        }
    }
}

internal sealed class AdministratorAddCommand : AsyncCommand<AdministratorMemberSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AdministratorMemberSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAdministratorsClient client) = await settings.CreateAdministratorsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // Build the member body inline at the call (the value field is taken by `in`, so the Build result is consumed
            // directly rather than returned from a helper). The interim CLI path names a single {dimension, value} grant.
            Models.JsonString.Source value = settings.Value;
            Models.JsonString.Source dimension = settings.Dimension;
            await using AddAdministratorResponse response = await client.AddAdministratorAsync(settings.BaseWorkflowId, Models.AdministratorMemberWrite.Build(value: value, dimension: dimension), cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AdministratorRemoveCommand : AsyncCommand<AdministratorMemberSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AdministratorMemberSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAdministratorsClient client) = await settings.CreateAdministratorsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RemoveAdministratorResponse response = await client.RemoveAdministratorAsync(settings.BaseWorkflowId, settings.Dimension, settings.Value, cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AdministratorTransferCommand : AsyncCommand<AdministratorTransferSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AdministratorTransferSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAdministratorsClient client) = await settings.CreateAdministratorsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.AdministratorSetWrite.Source body = AdministratorCommandHelpers.SetWrite(settings.Administrators!);
            await using TransferAdministrationResponse response = await client.TransferAdministrationAsync(settings.BaseWorkflowId, body, cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared rendering and request-body construction for the administrators commands.</summary>
internal static class AdministratorCommandHelpers
{
    public static Models.AdministratorIdentity.Source Identity(string dimension, string value)
        => new((ref Models.AdministratorIdentity.Builder b) => b.Create(dimension, value));

    public static Models.AdministratorSetWrite.Source SetWrite(ILookup<string, string> administrators)
        => new((ref Models.AdministratorSetWrite.Builder b) => b.Create(
            administrators: new Models.AdministratorSetWrite.AdministratorIdentityArray.Source((ref Models.AdministratorSetWrite.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (IGrouping<string, string> group in administrators)
                {
                    foreach (string value in group)
                    {
                        ab.AddItem(Identity(group.Key, value));
                    }
                }
            })));

    public static int RenderTable(Models.AdministratorList list, string baseWorkflowId)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });

        var table = new Table().Border(TableBorder.Rounded);
        table.Title = new TableTitle(Markup.Escape($"administrators of {baseWorkflowId}"));
        table.AddColumn("Dimension");
        table.AddColumn("Value");

        int count = 0;
        foreach (Models.AdministratorIdentity identity in list.Administrators.EnumerateArray())
        {
            table.AddRow(Markup.Escape((string)identity.DimensionValue), Markup.Escape((string)identity.Value));
            count++;
        }

        if (count == 0)
        {
            console.MarkupLine($"[dim]No administration established for {Markup.Escape(baseWorkflowId)}.[/]");
            return 0;
        }

        console.Write(table);
        return 0;
    }
}