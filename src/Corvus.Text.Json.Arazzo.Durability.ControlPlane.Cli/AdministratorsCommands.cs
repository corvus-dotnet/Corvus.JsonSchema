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

/// <summary>Settings for adding a single administrator, named as the grantee the server resolves to its exact identity
/// (ADR 0008): a well-known kind and a value (e.g. <c>team acme</c>), never a raw internal tag.</summary>
internal sealed class AdministratorMemberSettings : BaseWorkflowIdSettings
{
    [CommandArgument(1, "<kind>")]
    [Description("The grantee kind: person, team, role or workflow.")]
    public string Kind { get; init; } = string.Empty;

    [CommandArgument(2, "<value>")]
    [Description("The grantee value (a subject id, a team or role name, or a workflow id).")]
    public string Value { get; init; } = string.Empty;
}

/// <summary>Settings for removing an administrator by its stable identity <c>digest</c> (the removal key shown by
/// <c>administrators list</c>) — administration is removed by identity, not by re-presenting raw tags.</summary>
internal sealed class AdministratorRemoveSettings : BaseWorkflowIdSettings
{
    [CommandArgument(1, "<digest>")]
    [Description("The identity digest of the administrator to remove (copy it from `administrators list`).")]
    public string Digest { get; init; } = string.Empty;
}

/// <summary>Settings for replacing the whole administrator set. Each <c>--admin kind=value</c> names one new
/// administrator grantee the server resolves; a kind may repeat to name several administrators of that kind.</summary>
internal sealed class AdministratorTransferSettings : BaseWorkflowIdSettings
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
            // Build the grantee body inline at the call (the fields are taken by `in`, so the Build result is consumed
            // directly rather than returned from a helper). The server resolves the grantee to its identity (ADR 0008).
            Models.GranteeKind.Source kind = settings.Kind;
            Models.JsonString.Source value = settings.Value;
            await using AddAdministratorResponse response = await client.AddAdministratorAsync(settings.BaseWorkflowId, Models.GranteeReference.Build(kind: kind, value: value), cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class AdministratorRemoveCommand : AsyncCommand<AdministratorRemoveSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AdministratorRemoveSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiAdministratorsClient client) = await settings.CreateAdministratorsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RemoveAdministratorResponse response = await client.RemoveAdministratorAsync(settings.BaseWorkflowId, settings.Digest, cancellationToken);
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
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared rendering and request-body construction for the administrators commands.</summary>
internal static class AdministratorCommandHelpers
{
    public static Models.GranteeReference.Source Grantee(string kind, string value)
        => new((ref Models.GranteeReference.Builder b) => b.Create(kind: kind, value: value));

    public static Models.AdministratorSetWrite.Source SetWrite(ILookup<string, string> administrators)
        => new((ref Models.AdministratorSetWrite.Builder b) => b.Create(
            administrators: new Models.AdministratorSetWrite.GranteeReferenceArray.Source((ref Models.AdministratorSetWrite.GranteeReferenceArray.Builder ab) =>
            {
                foreach (IGrouping<string, string> group in administrators)
                {
                    foreach (string value in group)
                    {
                        ab.AddItem(Grantee(group.Key, value));
                    }
                }
            })));

    public static int RenderTable(Models.AdministratorList list, string baseWorkflowId)
    {
        IAnsiConsole console = OperatorCommandHelpers.CreateConsole();

        var table = new Table().Border(TableBorder.Rounded);
        table.Title = new TableTitle(Markup.Escape($"administrators of {baseWorkflowId}"));
        table.AddColumn("Digest");
        table.AddColumn("Identity");
        table.AddColumn("Kind");
        table.AddColumn("Label");

        int count = 0;
        foreach (Models.AdministratorGrant grant in list.Administrators.EnumerateArray())
        {
            // The identity is the resolved {dimension, value} grants it maps from (a multi-tag grantee shows several);
            // the digest is the stable removal key for `administrators remove <baseWorkflowId> <digest>`.
            IEnumerable<string> grants = grant.Identity.EnumerateArray()
                .Select(i => $"{(string)i.DimensionValue}={(string)i.Value}");
            string kind = grant.Kind.IsNotUndefined() ? (string)grant.Kind : string.Empty;
            string label = grant.Label.IsNotUndefined() ? (string)grant.Label : string.Empty;
            table.AddRow(
                Markup.Escape((string)grant.Digest),
                Markup.Escape(string.Join(", ", grants)),
                Markup.Escape(kind),
                Markup.Escape(label));
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