// <copyright file="CredentialsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command that targets a single binding by its (sourceName, environment) key.</summary>
internal class CredentialKeySettings : RunsSettings
{
    [CommandArgument(0, "<sourceName>")]
    [Description("The Arazzo source description name the credential authenticates calls to.")]
    public string SourceName { get; init; } = string.Empty;

    [CommandArgument(1, "<environment>")]
    [Description("The deployment environment the binding applies to (e.g. production).")]
    public string Environment { get; init; } = string.Empty;
}

/// <summary>Settings for the status-aware credential listing.</summary>
internal sealed class CredentialListSettings : RunsSettings
{
    [CommandOption("--status <STATUS>")]
    [Description("Restrict to bindings in this lifecycle status: valid, expiring (expiringSoon), or expired.")]
    public string? Status { get; init; }

    [CommandOption("--source <NAME>")]
    [Description("Restrict to bindings for this source name.")]
    public string? Source { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for creating a binding. Every value is a non-secret <em>reference</em> (a SecretRef of the form
/// <c>scheme://locator[#version]</c>) or non-secret metadata — never secret material, so nothing here leaks into the
/// process table or shell history. The framework parses each <c>key=value</c> option into a strongly-typed map.</summary>
internal sealed class CredentialCreateSettings : CredentialKeySettings
{
    [CommandOption("--auth-kind <KIND>")]
    [Description("The authentication kind the source uses (apiKey, bearer, basic, oauth2ClientCredentials, ...).")]
    public string AuthKind { get; init; } = string.Empty;

    [CommandOption("--ref <NAME=SECRETREF>")]
    [Description("A role-named secret reference, e.g. --ref value=keyvault://petstore-key#3 (repeat for several roles).")]
    public IReadOnlyDictionary<string, string>? Refs { get; init; }

    [CommandOption("--config <KEY=VALUE>")]
    [Description("A non-secret configuration entry, e.g. --config parameterName=X-Api-Key (repeatable).")]
    public IReadOnlyDictionary<string, string>? Config { get; init; }

    [CommandOption("--grant <DIMENSION=VALUE>")]
    [Description("A usage grant naming a workflow identity that may use the binding, e.g. --grant workflow=nightly-reconcile (repeatable).")]
    public ILookup<string, string>? Grants { get; init; }

    [CommandOption("--manage <KEY=VALUE>")]
    [Description("A management tag scoping who may administer the binding, e.g. --manage team=ops (repeatable).")]
    public ILookup<string, string>? ManagementTags { get; init; }

    [CommandOption("--expires-at <RFC3339>")]
    [Description("When the referenced secret expires (drives the derived status). Non-secret metadata.")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [CommandOption("--description <TEXT>")]
    public string? Description { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(this.AuthKind))
        {
            return Spectre.Console.ValidationResult.Error("--auth-kind <kind> is required.");
        }

        return this.Refs is not { Count: > 0 }
            ? Spectre.Console.ValidationResult.Error("at least one --ref <name=secretref> is required.")
            : base.Validate();
    }
}

/// <summary>Settings for updating a binding. Only the supplied fields change (a PATCH-style merge); unspecified ones are
/// preserved from the current binding. Changing a reference is how a credential is "rotated" — the operator writes the
/// new secret version to their store out of band, then re-points the binding here (<c>rotatedAt</c> is stamped
/// automatically). Management tags and usage grants are immutable across updates.</summary>
internal sealed class CredentialUpdateSettings : CredentialKeySettings
{
    [CommandOption("--auth-kind <KIND>")]
    [Description("Replace the authentication kind (preserved if omitted).")]
    public string? AuthKind { get; init; }

    [CommandOption("--ref <NAME=SECRETREF>")]
    [Description("Set the secret reference for a role, e.g. --ref value=keyvault://petstore-key#4 (repeatable); other roles are preserved.")]
    public IReadOnlyDictionary<string, string>? Refs { get; init; }

    [CommandOption("--config <KEY=VALUE>")]
    [Description("Set a non-secret configuration entry (repeatable); other entries are preserved.")]
    public IReadOnlyDictionary<string, string>? Config { get; init; }

    [CommandOption("--expires-at <RFC3339>")]
    [Description("Replace the expiry (preserved if omitted).")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [CommandOption("--rotated-at <RFC3339>")]
    [Description("Override the rotation timestamp (defaults to now when a --ref changes).")]
    public DateTimeOffset? RotatedAt { get; init; }

    [CommandOption("--description <TEXT>")]
    [Description("Replace the description (preserved if omitted).")]
    public string? Description { get; init; }
}

internal sealed class CredentialListCommand : AsyncCommand<CredentialListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CredentialListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCredentialsClient client) = await settings.CreateCredentialsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            string? wantStatus = CredentialCommandHelpers.NormalizeStatus(settings.Status);

            // A status-first table — the operator's real question is "what's about to break?". Status is
            // colour-coded and a footer counts the expiring/expired bindings; --status / --source filter client-side.
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Source");
            table.AddColumn("Environment");
            table.AddColumn("AuthKind");
            table.AddColumn("Status");
            table.AddColumn("Expires");
            table.AddColumn("Grants");
            List<string>? jsonItems = asJson ? [] : null;
            int expiring = 0;
            int expired = 0;

            // Walk every keyset page (the store pages server-side) so the table shows the complete set.
            string? pageToken = null;
            do
            {
                string? next = null;
                await using ListCredentialsResponse response = await client.ListCredentialsAsync(
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.CredentialBindingSummary summary in list.Credentials.EnumerateArray())
                        {
                            string status = (string)summary.CredentialStatus;
                            string source = (string)summary.SourceName;
                            if ((wantStatus is not null && !string.Equals(status, wantStatus, StringComparison.Ordinal))
                                || (settings.Source is { } s && !string.Equals(source, s, StringComparison.Ordinal)))
                            {
                                continue;
                            }

                            if (jsonItems is not null)
                            {
                                jsonItems.Add(summary.ToString());
                                continue;
                            }

                            if (string.Equals(status, "expiringSoon", StringComparison.Ordinal))
                            {
                                expiring++;
                            }
                            else if (string.Equals(status, "expired", StringComparison.Ordinal))
                            {
                                expired++;
                            }

                            table.AddRow(
                                Markup.Escape(source),
                                Markup.Escape((string)summary.Environment),
                                Markup.Escape((string)summary.AuthKind),
                                CredentialCommandHelpers.StatusMarkup(status),
                                Markup.Escape(CredentialCommandHelpers.FormatDate(summary.ExpiresAt)),
                                Markup.Escape(CredentialCommandHelpers.GrantSummary(summary)));
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
                return Output.Print($"{{\"credentials\":[{string.Join(",", jsonItems)}]}}");
            }

            IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
            console.Write(table);
            if (expiring > 0 || expired > 0)
            {
                console.MarkupLine($"[yellow]{expiring} expiring soon[/], [red]{expired} expired[/].");
            }

            return 0;
        }
    }
}

internal sealed class CredentialGetCommand : AsyncCommand<CredentialKeySettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CredentialKeySettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCredentialsClient client) = await settings.CreateCredentialsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetCredentialResponse response = await client.GetCredentialAsync(settings.SourceName, settings.Environment, cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class CredentialCreateCommand : AsyncCommand<CredentialCreateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CredentialCreateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCredentialsClient client) = await settings.CreateCredentialsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using CreateCredentialResponse response = await client.CreateCredentialAsync(CredentialCommandHelpers.BuildWrite(settings), cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class CredentialUpdateCommand : AsyncCommand<CredentialUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CredentialUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCredentialsClient client) = await settings.CreateCredentialsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // PATCH-style: read the current binding (references + non-secret metadata only) and overlay just the supplied
            // changes, so the operator names only what moved. The server's update is a full replace, so we merge here.
            CredentialCommandHelpers.Snapshot snapshot;
            await using (GetCredentialResponse current = await client.GetCredentialAsync(settings.SourceName, settings.Environment, cancellationToken))
            {
                if (current.StatusCode != 200)
                {
                    return current.MatchResult(_ => 0, Output.Problem, Output.Unexpected);
                }

                snapshot = current.MatchResult(
                    summary => CredentialCommandHelpers.Capture(summary),
                    _ => default,
                    _ => default);
            }

            Models.CredentialBindingUpdate.Source body = CredentialCommandHelpers.BuildUpdate(snapshot, settings);
            await using UpdateCredentialResponse response = await client.UpdateCredentialAsync(settings.SourceName, settings.Environment, body, cancellationToken);
            return response.MatchResult(summary => Output.Print(summary.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class CredentialDeleteCommand : AsyncCommand<CredentialKeySettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CredentialKeySettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCredentialsClient client) = await settings.CreateCredentialsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteCredentialResponse response = await client.DeleteCredentialAsync(settings.SourceName, settings.Environment, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted credential binding '{settings.SourceName}@{settings.Environment}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared rendering and request-body construction for the credential commands. The command-line parsing of
/// <c>key=value</c> options and timestamps is handled by Spectre's strongly-typed binding (dictionaries, lookups, and
/// <see cref="DateTimeOffset"/>), so there is no hand-rolled splitting here.</summary>
internal static class CredentialCommandHelpers
{
    /// <summary>The non-secret subset of a binding the update command reads to merge changes onto (no document
    /// lifetime — plain captured values, dates read through the typed conversion).</summary>
    internal readonly record struct Snapshot(
        string AuthKind,
        List<(string Name, string Ref)> Refs,
        List<(string Key, string Value)> Config,
        string? Description,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset? RotatedAt);

    // Map the operator-friendly status words to the API's tokens; null = no filter.
    public static string? NormalizeStatus(string? status) => status?.ToLowerInvariant() switch
    {
        null or "" => null,
        "valid" => "valid",
        "expiring" or "expiringsoon" => "expiringSoon",
        "expired" => "expired",
        _ => status,
    };

    public static string StatusMarkup(string status) => status switch
    {
        "valid" => "[green]valid[/]",
        "expiringSoon" => "[yellow]expiringSoon[/]",
        "expired" => "[red]expired[/]",
        _ => Markup.Escape(status),
    };

    public static string FormatDate(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "—";

    public static string GrantSummary(Models.CredentialBindingSummary summary)
    {
        if (!summary.UsageGrants.IsNotUndefined())
        {
            return "—";
        }

        var grants = new List<string>();
        foreach (Models.CredentialUsageGrant grant in summary.UsageGrants.EnumerateArray())
        {
            grants.Add($"{(string)grant.DimensionValue}={(string)grant.Value}");
        }

        return grants.Count > 0 ? string.Join(", ", grants) : "—";
    }

    public static Models.CredentialBindingWrite.Source BuildWrite(CredentialCreateSettings s)
        => new((ref Models.CredentialBindingWrite.Builder b) =>
        {
            Models.JsonString.Source description = s.Description is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonDateTime.Source expiresAt = s.ExpiresAt is { } e ? (Models.JsonDateTime.Source)e : default;
            b.Create(
                authKind: s.AuthKind,
                environment: s.Environment,
                secretRefs: WriteRefs(s.Refs),
                sourceName: s.SourceName,
                config: WriteConfig(s.Config),
                description: description,
                expiresAt: expiresAt,
                managementTags: WriteManagementTags(s.ManagementTags),
                rotatedAt: default,
                usageGrants: WriteUsageGrants(s.Grants));
        });

    public static Snapshot Capture(Models.CredentialBindingSummary summary)
    {
        var refs = new List<(string, string)>();
        foreach (Models.SecretReference reference in summary.SecretRefs.EnumerateArray())
        {
            refs.Add(((string)reference.Name, (string)reference.Ref));
        }

        var config = new List<(string, string)>();
        if (summary.Config.IsNotUndefined())
        {
            foreach (Models.CredentialConfigEntry entry in summary.Config.EnumerateArray())
            {
                config.Add(((string)entry.Key, (string)entry.Value));
            }
        }

        return new Snapshot(
            (string)summary.AuthKind,
            refs,
            config,
            summary.Description.IsNotUndefined() ? (string)summary.Description : null,
            ReadDate(summary.ExpiresAt),
            ReadDate(summary.RotatedAt));
    }

    public static Models.CredentialBindingUpdate.Source BuildUpdate(Snapshot current, CredentialUpdateSettings s)
    {
        List<(string Name, string Ref)> refs = Merge(current.Refs, s.Refs, out bool refsChanged);
        List<(string Key, string Value)> config = Merge(current.Config, s.Config, out _);

        string authKind = s.AuthKind ?? current.AuthKind;
        string? description = s.Description ?? current.Description;

        return new Models.CredentialBindingUpdate.Source((ref Models.CredentialBindingUpdate.Builder b) =>
        {
            Models.JsonString.Source descriptionSource = description is { } d ? (Models.JsonString.Source)d : default;

            // A new --expires-at replaces; otherwise the current expiry is carried forward as a typed instant.
            Models.JsonDateTime.Source expiresAt = s.ExpiresAt is { } e ? (Models.JsonDateTime.Source)e
                : current.ExpiresAt is { } ea ? (Models.JsonDateTime.Source)ea
                : default;

            // A reference change is a rotation: stamp rotatedAt = now unless the operator pinned it explicitly.
            Models.JsonDateTime.Source rotatedAt = s.RotatedAt is { } r ? (Models.JsonDateTime.Source)r
                : refsChanged ? (Models.JsonDateTime.Source)DateTimeOffset.UtcNow
                : current.RotatedAt is { } ra ? (Models.JsonDateTime.Source)ra
                : default;

            b.Create(
                authKind: authKind,
                secretRefs: UpdateRefs(refs),
                config: UpdateConfig(config),
                description: descriptionSource,
                expiresAt: expiresAt,
                rotatedAt: rotatedAt);
        });
    }

    private static DateTimeOffset? ReadDate(Models.JsonDateTime value)
        => value.IsNotUndefined() ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset() : null;

    // Overlay a supplied key→value map onto a current ordered list, replacing by key and appending new keys; preserves
    // entries the operator did not mention. Reports whether anything actually changed.
    private static List<(string Key, string Value)> Merge(List<(string Key, string Value)> current, IReadOnlyDictionary<string, string>? overrides, out bool changed)
    {
        changed = false;
        var merged = new List<(string Key, string Value)>(current);
        if (overrides is null)
        {
            return merged;
        }

        foreach ((string key, string value) in overrides)
        {
            int index = merged.FindIndex(e => string.Equals(e.Key, key, StringComparison.Ordinal));
            if (index < 0)
            {
                merged.Add((key, value));
                changed = true;
            }
            else if (!string.Equals(merged[index].Value, value, StringComparison.Ordinal))
            {
                merged[index] = (key, value);
                changed = true;
            }
        }

        return merged;
    }

    private static Models.CredentialBindingWrite.SecretReferenceArray.Source WriteRefs(IReadOnlyDictionary<string, string>? refs)
        => new((ref Models.CredentialBindingWrite.SecretReferenceArray.Builder ab) =>
        {
            if (refs is null)
            {
                return;
            }

            foreach ((string name, string reference) in refs)
            {
                ab.AddItem(new Models.SecretReference.Source((ref Models.SecretReference.Builder rb) => rb.Create(name, reference)));
            }
        });

    private static Models.CredentialBindingUpdate.SecretReferenceArray.Source UpdateRefs(List<(string Name, string Ref)> refs)
        => new((ref Models.CredentialBindingUpdate.SecretReferenceArray.Builder ab) =>
        {
            foreach ((string name, string reference) in refs)
            {
                ab.AddItem(new Models.SecretReference.Source((ref Models.SecretReference.Builder rb) => rb.Create(name, reference)));
            }
        });

    private static Models.CredentialBindingWrite.CredentialConfigEntryArray.Source WriteConfig(IReadOnlyDictionary<string, string>? config)
    {
        if (config is not { Count: > 0 })
        {
            return default;
        }

        return new Models.CredentialBindingWrite.CredentialConfigEntryArray.Source((ref Models.CredentialBindingWrite.CredentialConfigEntryArray.Builder ab) =>
        {
            foreach ((string key, string value) in config)
            {
                ab.AddItem(new Models.CredentialConfigEntry.Source((ref Models.CredentialConfigEntry.Builder eb) => eb.Create(key, value)));
            }
        });
    }

    private static Models.CredentialBindingUpdate.CredentialConfigEntryArray.Source UpdateConfig(List<(string Key, string Value)> config)
    {
        if (config.Count == 0)
        {
            return default;
        }

        return new Models.CredentialBindingUpdate.CredentialConfigEntryArray.Source((ref Models.CredentialBindingUpdate.CredentialConfigEntryArray.Builder ab) =>
        {
            foreach ((string key, string value) in config)
            {
                ab.AddItem(new Models.CredentialConfigEntry.Source((ref Models.CredentialConfigEntry.Builder eb) => eb.Create(key, value)));
            }
        });
    }

    private static Models.CredentialBindingWrite.CredentialSecurityTagArray.Source WriteManagementTags(ILookup<string, string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        return new Models.CredentialBindingWrite.CredentialSecurityTagArray.Source((ref Models.CredentialBindingWrite.CredentialSecurityTagArray.Builder ab) =>
        {
            foreach (IGrouping<string, string> group in tags)
            {
                foreach (string value in group)
                {
                    ab.AddItem(new Models.CredentialSecurityTag.Source((ref Models.CredentialSecurityTag.Builder tb) => tb.Create(group.Key, value)));
                }
            }
        });
    }

    private static Models.CredentialBindingWrite.CredentialUsageGrantArray.Source WriteUsageGrants(ILookup<string, string>? grants)
    {
        if (grants is null)
        {
            return default;
        }

        return new Models.CredentialBindingWrite.CredentialUsageGrantArray.Source((ref Models.CredentialBindingWrite.CredentialUsageGrantArray.Builder ab) =>
        {
            foreach (IGrouping<string, string> group in grants)
            {
                foreach (string value in group)
                {
                    ab.AddItem(new Models.CredentialUsageGrant.Source((ref Models.CredentialUsageGrant.Builder gb) => gb.Create(group.Key, value)));
                }
            }
        });
    }
}