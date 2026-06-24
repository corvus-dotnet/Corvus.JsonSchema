// <copyright file="SecurityCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command targeting a single security rule by name.</summary>
internal class SecurityRuleNameSettings : RunsSettings
{
    [CommandArgument(0, "<ruleName>")]
    [Description("The security rule name.")]
    public string RuleName { get; init; } = string.Empty;
}

/// <summary>Settings for creating a security rule.</summary>
internal sealed class SecurityRuleCreateSettings : RunsSettings
{
    [CommandArgument(0, "<name>")]
    [Description("The rule's unique name.")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("--expression <EXPR>")]
    [Description("The rule expression in the security-rule grammar, e.g. \"tenant == $claim.tenant\".")]
    public string Expression { get; init; } = string.Empty;

    [CommandOption("--description <TEXT>")]
    public string? Description { get; init; }
}

/// <summary>Settings for replacing an existing security rule.</summary>
internal sealed class SecurityRuleUpdateSettings : SecurityRuleNameSettings
{
    [CommandOption("--expression <EXPR>")]
    [Description("The new rule expression in the security-rule grammar.")]
    public string Expression { get; init; } = string.Empty;

    [CommandOption("--description <TEXT>")]
    public string? Description { get; init; }
}

/// <summary>Settings for a command targeting a single binding by id.</summary>
internal class SecurityBindingIdSettings : RunsSettings
{
    [CommandArgument(0, "<bindingId>")]
    [Description("The security binding id.")]
    public string BindingId { get; init; } = string.Empty;
}

/// <summary>Settings for creating or replacing a binding. Each verb grant is <c>unrestricted</c> or a comma-separated rule list.</summary>
internal class SecurityBindingWriteSettings : RunsSettings
{
    [CommandOption("--claim-type <TYPE>")]
    [Description("The principal claim type to key on ('*' matches any authenticated principal).")]
    public string ClaimType { get; init; } = string.Empty;

    [CommandOption("--claim-value <VALUE>")]
    [Description("The required claim value; omit to match any value of the claim type.")]
    public string? ClaimValue { get; init; }

    [CommandOption("--read <GRANT>")]
    [Description("Read grant: 'unrestricted' or a comma-separated list of rule names; omit to grant nothing.")]
    public string? Read { get; init; }

    [CommandOption("--write <GRANT>")]
    [Description("Write grant: 'unrestricted' or a comma-separated list of rule names; omit to grant nothing.")]
    public string? Write { get; init; }

    [CommandOption("--purge <GRANT>")]
    [Description("Purge grant: 'unrestricted' or a comma-separated list of rule names; omit to grant nothing.")]
    public string? Purge { get; init; }

    [CommandOption("--order <N>")]
    [Description("Resolution order (ascending). Default 0.")]
    public int Order { get; init; }

    [CommandOption("--description <TEXT>")]
    public string? Description { get; init; }
}

/// <summary>Settings for replacing a binding (id argument plus the write options).</summary>
internal sealed class SecurityBindingUpdateSettings : SecurityBindingWriteSettings
{
    [CommandArgument(0, "<bindingId>")]
    [Description("The security binding id.")]
    public string BindingId { get; init; } = string.Empty;
}

internal sealed class SecurityRuleListCommand : AsyncCommand<RunsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunsSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ListSecurityRulesResponse response = await client.ListSecurityRulesAsync(cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Unexpected);
        }
    }
}

internal sealed class SecurityRuleGetCommand : AsyncCommand<SecurityRuleNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityRuleNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetSecurityRuleResponse response = await client.GetSecurityRuleAsync(settings.RuleName, cancellationToken);
            return response.MatchResult(rule => Output.Print(rule.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityRuleCreateCommand : AsyncCommand<SecurityRuleCreateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityRuleCreateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.SecurityRuleCreate.Source body = new((ref Models.SecurityRuleCreate.Builder b) =>
            {
                Models.JsonString.Source description = settings.Description is { } d ? (Models.JsonString.Source)d : default;
                b.Create(expression: settings.Expression, name: settings.Name, description: description);
            });
            await using CreateSecurityRuleResponse response = await client.CreateSecurityRuleAsync(body, cancellationToken);
            return response.MatchResult(rule => Output.Print(rule.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityRuleUpdateCommand : AsyncCommand<SecurityRuleUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityRuleUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.SecurityRuleUpdate.Source body = new((ref Models.SecurityRuleUpdate.Builder b) =>
            {
                Models.JsonString.Source description = settings.Description is { } d ? (Models.JsonString.Source)d : default;
                b.Create(expression: settings.Expression, description: description);
            });
            await using UpdateSecurityRuleResponse response = await client.UpdateSecurityRuleAsync(settings.RuleName, body, cancellationToken);
            return response.MatchResult(rule => Output.Print(rule.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityRuleDeleteCommand : AsyncCommand<SecurityRuleNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityRuleNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteSecurityRuleResponse response = await client.DeleteSecurityRuleAsync(settings.RuleName, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted security rule '{settings.RuleName}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityBindingListCommand : AsyncCommand<RunsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunsSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using ListSecurityBindingsResponse response = await client.ListSecurityBindingsAsync(cancellationToken);
            return response.MatchResult(list => Output.Print(list.ToString()), Output.Unexpected);
        }
    }
}

internal sealed class SecurityBindingGetCommand : AsyncCommand<SecurityBindingIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityBindingIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetSecurityBindingResponse response = await client.GetSecurityBindingAsync(settings.BindingId, cancellationToken);
            return response.MatchResult(binding => Output.Print(binding.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityBindingCreateCommand : AsyncCommand<SecurityBindingWriteSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityBindingWriteSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using CreateSecurityBindingResponse response = await client.CreateSecurityBindingAsync(SecurityCommandHelpers.BuildBinding(settings), cancellationToken);
            return response.MatchResult(binding => Output.Print(binding.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityBindingUpdateCommand : AsyncCommand<SecurityBindingUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityBindingUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using UpdateSecurityBindingResponse response = await client.UpdateSecurityBindingAsync(settings.BindingId, SecurityCommandHelpers.BuildBinding(settings), cancellationToken);
            return response.MatchResult(binding => Output.Print(binding.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SecurityBindingDeleteCommand : AsyncCommand<SecurityBindingIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SecurityBindingIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSecurityClient client) = await settings.CreateSecurityClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteSecurityBindingResponse response = await client.DeleteSecurityBindingAsync(settings.BindingId, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted security binding '{settings.BindingId}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared construction of the binding request body from CLI options.</summary>
internal static class SecurityCommandHelpers
{
    public static Models.SecurityBindingWrite.Source BuildBinding(SecurityBindingWriteSettings settings)
        => new((ref Models.SecurityBindingWrite.Builder b) =>
        {
            Models.JsonString.Source claimValue = settings.ClaimValue is { } cv ? (Models.JsonString.Source)cv : default;
            Models.JsonString.Source description = settings.Description is { } d ? (Models.JsonString.Source)d : default;
            b.Create(
                claimType: settings.ClaimType,
                claimValue: claimValue,
                read: Grant(settings.Read),
                write: Grant(settings.Write),
                purge: Grant(settings.Purge),
                order: settings.Order,
                description: description);
        });

    // "unrestricted" → full reach; "a,b" → rule names ANDed; null/empty → no grant (default).
    private static Models.VerbGrant.Source Grant(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return default;
        }

        if (string.Equals(spec, "unrestricted", StringComparison.OrdinalIgnoreCase))
        {
            return new Models.VerbGrant.Source((ref Models.VerbGrant.Builder b) => b.Create(unrestricted: true));
        }

        string[] names = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new Models.VerbGrant.Source((ref Models.VerbGrant.Builder b) => b.Create(
            ruleNames: new Models.VerbGrant.JsonStringArray.Source((ref Models.VerbGrant.JsonStringArray.Builder ab) =>
            {
                foreach (string name in names)
                {
                    ab.AddItem(name);
                }
            })));
    }
}