// <copyright file="SourcesCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for a command that targets a single source by name.</summary>
internal class SourceNameSettings : RunsSettings
{
    [CommandArgument(0, "<name>")]
    [Description("The source's registered name (its stable identity, e.g. petstore).")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Settings for listing sources.</summary>
internal sealed class SourceListSettings : RunsSettings
{
    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

/// <summary>Settings for registering a source. The deployment stamps the creator's internal tenant tag onto
/// managementTags (§7.6). The document is read from a file (the OpenAPI/AsyncAPI description).</summary>
internal sealed class SourceRegisterSettings : SourceNameSettings
{
    [CommandOption("--type <TYPE>")]
    [Description("The source document kind: openapi or asyncapi.")]
    public string Type { get; init; } = string.Empty;

    [CommandOption("--document <FILE>")]
    [Description("Path to the source's OpenAPI/AsyncAPI document (a JSON file).")]
    public string Document { get; init; } = string.Empty;

    [CommandOption("--display-name <TEXT>")]
    [Description("An optional human-friendly display name (defaults to the name).")]
    public string? DisplayName { get; init; }

    [CommandOption("--description <TEXT>")]
    [Description("An optional human description.")]
    public string? Description { get; init; }

    [CommandOption("--manage <KEY=VALUE>")]
    [Description("A management tag scoping who may manage and see the source, e.g. --manage team=ops (repeatable).")]
    public ILookup<string, string>? ManagementTags { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        if (this.Type is not ("openapi" or "asyncapi"))
        {
            return Spectre.Console.ValidationResult.Error("--type must be 'openapi' or 'asyncapi'.");
        }

        if (string.IsNullOrWhiteSpace(this.Document))
        {
            return Spectre.Console.ValidationResult.Error("--document <FILE> is required.");
        }

        return !File.Exists(this.Document)
            ? Spectre.Console.ValidationResult.Error($"The document file '{this.Document}' does not exist.")
            : base.Validate();
    }
}

/// <summary>Settings for updating a source. Only the supplied fields change (a PATCH-style merge); the name, type,
/// management-tag reach scope, and created-* audit are immutable. The document is rotated only when a new
/// <c>--document</c> file is supplied — otherwise the stored document is kept.</summary>
internal sealed class SourceUpdateSettings : SourceNameSettings
{
    [CommandOption("--document <FILE>")]
    [Description("Path to a replacement OpenAPI/AsyncAPI document (the stored document is kept if omitted).")]
    public string? Document { get; init; }

    [CommandOption("--display-name <TEXT>")]
    [Description("Replace the display name (preserved if omitted).")]
    public string? DisplayName { get; init; }

    [CommandOption("--description <TEXT>")]
    [Description("Replace the description (preserved if omitted).")]
    public string? Description { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => this.Document is { } file && !File.Exists(file)
            ? Spectre.Console.ValidationResult.Error($"The document file '{file}' does not exist.")
            : base.Validate();
}

internal sealed class SourceListCommand : AsyncCommand<SourceListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SourceListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSourcesClient client) = await settings.CreateSourcesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Type");
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
                await using ListSourcesResponse response = await client.ListSourcesAsync(
                    pageToken: pageToken is { } token ? (Models.JsonString.Source)token : default,
                    cancellationToken: cancellationToken);
                int rc = response.MatchResult(
                    list =>
                    {
                        next = list.NextPageToken.IsNotUndefined() ? (string)list.NextPageToken : null;
                        foreach (Models.SourceSummary summary in list.Sources.EnumerateArray())
                        {
                            if (jsonItems is not null)
                            {
                                jsonItems.Add(summary.ToString());
                                continue;
                            }

                            table.AddRow(
                                Markup.Escape((string)summary.Name),
                                Markup.Escape((string)summary.Type),
                                Markup.Escape(summary.DisplayName.IsNotUndefined() ? (string)summary.DisplayName : "—"),
                                Markup.Escape(summary.Description.IsNotUndefined() ? (string)summary.Description : "—"),
                                Markup.Escape((string)summary.CreatedBy),
                                Markup.Escape(SourceCommandHelpers.FormatDate(summary.CreatedAt)));
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
                return Output.Print($"{{\"sources\":[{string.Join(",", jsonItems)}]}}");
            }

            IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
            console.Write(table);
            return 0;
        }
    }
}

internal sealed class SourceGetCommand : AsyncCommand<SourceNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SourceNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSourcesClient client) = await settings.CreateSourcesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetSourceResponse response = await client.GetSourceAsync(settings.Name, cancellationToken);
            return response.MatchResult(source => Output.Print(source.ToString()), Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SourceRegisterCommand : AsyncCommand<SourceRegisterSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SourceRegisterSettings settings, CancellationToken cancellationToken)
    {
        byte[] documentBytes = await File.ReadAllBytesAsync(settings.Document, cancellationToken);
        Models.SourceDocument document = Models.SourceDocument.ParseValue(documentBytes);

        (HttpClient http, HttpClientTransport transport, ApiSourcesClient client) = await settings.CreateSourcesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using RegisterSourceResponse response = await client.RegisterSourceAsync(SourceCommandHelpers.BuildWrite(settings, document), cancellationToken);
            return response.MatchResult(source => Output.Print(source.ToString()), Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SourceUpdateCommand : AsyncCommand<SourceUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SourceUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSourcesClient client) = await settings.CreateSourcesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // PATCH-style for the mutable metadata: the server REPLACES display name/description, so read the current source
            // and overlay only the supplied fields. The document is carried forward by the server when omitted, so it is
            // re-sent only when a new --document file is supplied (a rotation) — never re-uploaded just to rename.
            (string? DisplayName, string? Description) snapshot;
            await using (GetSourceResponse current = await client.GetSourceAsync(settings.Name, cancellationToken))
            {
                if (current.StatusCode != 200)
                {
                    return current.MatchResult(_ => 0, Output.Problem, Output.Unexpected);
                }

                snapshot = current.MatchResult(
                    source => (
                        source.DisplayName.IsNotUndefined() ? (string)source.DisplayName : null,
                        source.Description.IsNotUndefined() ? (string)source.Description : null),
                    _ => default,
                    _ => default);
            }

            string? displayName = settings.DisplayName ?? snapshot.DisplayName;
            string? description = settings.Description ?? snapshot.Description;

            Models.SourceUpdate.Source body;
            if (settings.Document is { } file)
            {
                byte[] documentBytes = await File.ReadAllBytesAsync(file, cancellationToken);
                Models.SourceDocument document = Models.SourceDocument.ParseValue(documentBytes);
                body = SourceCommandHelpers.BuildUpdate(displayName, description, document, hasDocument: true);
            }
            else
            {
                body = SourceCommandHelpers.BuildUpdate(displayName, description, default, hasDocument: false);
            }

            await using UpdateSourceResponse response = await client.UpdateSourceAsync(settings.Name, body, cancellationToken);
            return response.MatchResult(source => Output.Print(source.ToString()), Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

internal sealed class SourceDeleteCommand : AsyncCommand<SourceNameSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SourceNameSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiSourcesClient client) = await settings.CreateSourcesClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteSourceResponse response = await client.DeleteSourceAsync(settings.Name, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted source '{settings.Name}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}

/// <summary>Shared rendering and request-body construction for the sources commands.</summary>
internal static class SourceCommandHelpers
{
    public static string FormatDate(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "—";

    public static Models.SourceWrite.Source BuildWrite(SourceRegisterSettings s, Models.SourceDocument document)
        => new((ref Models.SourceWrite.Builder b) =>
        {
            Models.JsonString.Source displayName = s.DisplayName is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonString.Source description = s.Description is { } desc ? (Models.JsonString.Source)desc : default;
            b.Create(
                document: document,
                name: s.Name,
                type: (Models.SourceType.Source)s.Type,
                description: description,
                displayName: displayName,
                managementTags: WriteManagementTags(s.ManagementTags));
        });

    public static Models.SourceUpdate.Source BuildUpdate(string? displayName, string? description, Models.SourceDocument document, bool hasDocument)
        => new((ref Models.SourceUpdate.Builder b) =>
        {
            Models.JsonString.Source displayNameSource = displayName is { } d ? (Models.JsonString.Source)d : default;
            Models.JsonString.Source descriptionSource = description is { } desc ? (Models.JsonString.Source)desc : default;
            Models.SourceDocument.Source documentSource = hasDocument ? document : default;
            b.Create(description: descriptionSource, displayName: displayNameSource, document: documentSource);
        });

    private static Models.SourceWrite.SourceSecurityTagArray.Source WriteManagementTags(ILookup<string, string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        return new Models.SourceWrite.SourceSecurityTagArray.Source((ref Models.SourceWrite.SourceSecurityTagArray.Builder ab) =>
        {
            foreach (IGrouping<string, string> group in tags)
            {
                foreach (string value in group)
                {
                    ab.AddItem(new Models.SourceSecurityTag.Source((ref Models.SourceSecurityTag.Builder tb) => tb.Create(group.Key, value)));
                }
            }
        });
    }
}