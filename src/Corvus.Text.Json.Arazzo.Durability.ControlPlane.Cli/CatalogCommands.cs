// <copyright file="CatalogCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Assembles and resolves catalog package envelopes from local files for the offline <c>pack</c>/<c>verify</c>
/// and the inline <c>add</c>.</summary>
internal static class CatalogPackageFiles
{
    /// <summary>Builds a canonical package envelope from an Arazzo workflow file and its referenced sources.</summary>
    public static byte[] BuildFromWorkflow(string workflowPath, IReadOnlyDictionary<string, string> explicitSources)
    {
        byte[] workflowBytes = File.ReadAllBytes(workflowPath);
        List<KeyValuePair<string, byte[]>> sources = ResolveSources(workflowPath, workflowBytes, explicitSources);
        return CatalogPackage.Build(workflowBytes, sources);
    }

    /// <summary>Resolves each <c>sourceDescriptions</c> entry to its document bytes, preferring <paramref name="explicitSources"/>
    /// then the entry's <c>url</c> as a path relative to the workflow file.</summary>
    public static List<KeyValuePair<string, byte[]>> ResolveSources(string workflowPath, byte[] workflowBytes, IReadOnlyDictionary<string, string> explicitSources)
    {
        var resolved = new List<KeyValuePair<string, byte[]>>();
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(workflowPath)) ?? ".";
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(workflowBytes);
        if (!document.RootElement.TryGetProperty("sourceDescriptions"u8, out JsonElement descriptions)
            || descriptions.ValueKind != JsonValueKind.Array)
        {
            return resolved;
        }

        foreach (JsonElement description in descriptions.EnumerateArray())
        {
            if (!description.TryGetProperty("name"u8, out JsonElement nameElement) || nameElement.GetString() is not { Length: > 0 } name)
            {
                continue;
            }

            string? type = description.TryGetProperty("type"u8, out JsonElement typeElement) ? typeElement.GetString() : null;
            string? path = explicitSources.TryGetValue(name, out string? overridePath) ? overridePath : ResolveUrlPath(baseDir, description);
            if (path is null || !File.Exists(path))
            {
                if (string.Equals(type, "arazzo", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                throw new FileNotFoundException($"Source '{name}' could not be resolved to a local file; provide it with --source {name}=<path>.");
            }

            resolved.Add(new KeyValuePair<string, byte[]>(name, File.ReadAllBytes(path)));
        }

        return resolved;
    }

    /// <summary>Parses repeated <c>--source name=path</c> options into a name→path map.</summary>
    public static Dictionary<string, string> ParseSourceArgs(string[] sourceArgs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string arg in sourceArgs)
        {
            int eq = arg.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                throw new ArgumentException($"--source must be in the form name=path; got '{arg}'.");
            }

            map[arg[..eq]] = arg[(eq + 1)..];
        }

        return map;
    }

    private static string? ResolveUrlPath(string baseDir, JsonElement description)
    {
        if (!description.TryGetProperty("url"u8, out JsonElement urlElement) || urlElement.GetString() is not { Length: > 0 } url)
        {
            return null;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            url = url["file:".Length..].TrimStart('/');
        }

        return Path.IsPathRooted(url) ? url : Path.Combine(baseDir, url);
    }
}

/// <summary>A command that targets a single catalog version by base id + version number.</summary>
internal class CatalogVersionSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id (without any -vN suffix).")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(1, "<version>")]
    [Description("The 1-based version number.")]
    public int Version { get; init; }
}

internal sealed class CatalogPackSettings : CommandSettings
{
    [CommandOption("--workflow <PATH>")]
    [Description("Path to the Arazzo workflow document.")]
    public string? Workflow { get; init; }

    [CommandOption("--source <NAME=PATH>")]
    [Description("Override a sourceDescriptions document (repeat per source). Otherwise each source url is resolved relative to the workflow file.")]
    public string[] Sources { get; init; } = [];

    [CommandOption("--out <PATH>")]
    [Description("Write the canonical package envelope here (default: stdout).")]
    public string? Out { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrEmpty(this.Workflow)
            ? Spectre.Console.ValidationResult.Error("--workflow <path> is required.")
            : Spectre.Console.ValidationResult.Success();
}

internal sealed class CatalogVerifySettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Path to a package envelope file to verify.")]
    public string Package { get; init; } = string.Empty;

    [CommandOption("--expect-hash <HEX>")]
    [Description("Fail unless the package's canonical SHA-256 equals this value (e.g. a catalog version's reported hash).")]
    public string? ExpectHash { get; init; }
}

internal sealed class CatalogUnpackSettings : CommandSettings
{
    [CommandArgument(0, "<package>")]
    [Description("Path to a package envelope file to unpack.")]
    public string Package { get; init; } = string.Empty;

    [CommandOption("--out-dir <DIR>")]
    [Description("Directory to extract the workflow document and sources into.")]
    public string? OutDir { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrEmpty(this.OutDir)
            ? Spectre.Console.ValidationResult.Error("--out-dir <dir> is required.")
            : Spectre.Console.ValidationResult.Success();
}

internal sealed class CatalogAddSettings : RunsSettings
{
    [CommandOption("--package <PATH>")]
    [Description("Path to a pre-built package envelope to upload.")]
    public string? Package { get; init; }

    [CommandOption("--workflow <PATH>")]
    [Description("Path to the Arazzo workflow document to build and upload (alternative to --package).")]
    public string? Workflow { get; init; }

    [CommandOption("--source <NAME=PATH>")]
    [Description("Override a sourceDescriptions document when building from --workflow (repeat per source).")]
    public string[] Sources { get; init; } = [];

    [CommandOption("--owner-name <NAME>")]
    [Description("The accountable owner's display name.")]
    public string? OwnerName { get; init; }

    [CommandOption("--owner-email <EMAIL>")]
    [Description("The owner's contact email.")]
    public string? OwnerEmail { get; init; }

    [CommandOption("--owner-team <TEAM>")]
    [Description("The owning team/group (optional).")]
    public string? OwnerTeam { get; init; }

    [CommandOption("--owner-url <URL>")]
    [Description("A link for the owner, e.g. a runbook (optional).")]
    public string? OwnerUrl { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("A free-form tag (repeat to add several).")]
    public string[] Tags { get; init; } = [];

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(this.Package) == string.IsNullOrEmpty(this.Workflow))
        {
            return Spectre.Console.ValidationResult.Error("Provide exactly one of --package <path> or --workflow <path>.");
        }

        if (string.IsNullOrEmpty(this.OwnerName) || string.IsNullOrEmpty(this.OwnerEmail))
        {
            return Spectre.Console.ValidationResult.Error("--owner-name and --owner-email are required.");
        }

        return base.Validate();
    }
}

internal sealed class CatalogSearchSettings : RunsSettings
{
    [CommandOption("-q|--query <TEXT>")]
    [Description("Free-text over title/description (case-insensitive contains).")]
    public string? Query { get; init; }

    [CommandOption("--base-workflow-id <ID>")]
    [Description("Restrict to versions of this base workflow id.")]
    public string? BaseWorkflowId { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("Restrict to versions carrying this tag (repeat to require several, AND-matched).")]
    public string[] Tags { get; init; } = [];

    [CommandOption("--status <STATUS>")]
    [Description("Restrict to versions in this status (Active/Obsolete).")]
    public string? Status { get; init; }

    [CommandOption("--owner <TEXT>")]
    [Description("Restrict to versions whose owner name or email contains this value.")]
    public string? Owner { get; init; }

    [CommandOption("--limit <N>")]
    [Description("Maximum versions per page.")]
    public int? Limit { get; init; }

    [CommandOption("--page-token <TOKEN>")]
    [Description("Continuation token from a previous page's nextPageToken.")]
    public string? PageToken { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class CatalogVersionsSettings : RunsSettings
{
    [CommandArgument(0, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandOption("--limit <N>")]
    [Description("Maximum versions per page.")]
    public int? Limit { get; init; }

    [CommandOption("--page-token <TOKEN>")]
    [Description("Continuation token from a previous page's nextPageToken.")]
    public string? PageToken { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class CatalogDownloadSettings : CatalogVersionSettings
{
    [CommandOption("--out <PATH>")]
    [Description("Write the document here (default: stdout).")]
    public string? Out { get; init; }
}

internal sealed class CatalogSourceSettings : CatalogVersionSettings
{
    [CommandArgument(2, "<sourceName>")]
    [Description("The source document name (from the version's sources list).")]
    public string SourceName { get; init; } = string.Empty;

    [CommandOption("--out <PATH>")]
    [Description("Write the document here (default: stdout).")]
    public string? Out { get; init; }
}

internal sealed class CatalogUpdateSettings : CatalogVersionSettings
{
    [CommandOption("--owner-name <NAME>")]
    [Description("Replace the owner's display name.")]
    public string? OwnerName { get; init; }

    [CommandOption("--owner-email <EMAIL>")]
    [Description("Replace the owner's contact email.")]
    public string? OwnerEmail { get; init; }

    [CommandOption("--owner-team <TEAM>")]
    [Description("Replace the owning team/group.")]
    public string? OwnerTeam { get; init; }

    [CommandOption("--owner-url <URL>")]
    [Description("Replace the owner link.")]
    public string? OwnerUrl { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("Replace the tag set (repeat to set several).")]
    public string[] Tags { get; init; } = [];

    [CommandOption("--status <STATUS>")]
    [Description("Set the lifecycle status (Active/Obsolete).")]
    public string? Status { get; init; }
}

internal sealed class CatalogPackCommand : Command<CatalogPackSettings>
{
    protected override int Execute(CommandContext context, CatalogPackSettings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.Workflow))
        {
            Console.Error.WriteLine($"--workflow not found: {settings.Workflow}");
            return 1;
        }

        try
        {
            byte[] envelope = CatalogPackageFiles.BuildFromWorkflow(settings.Workflow!, CatalogPackageFiles.ParseSourceArgs(settings.Sources));
            if (settings.Out is { } outPath)
            {
                File.WriteAllBytes(outPath, envelope);
                Console.WriteLine($"Wrote package ({envelope.Length} bytes) to {outPath}.");
            }
            else
            {
                Console.Out.Write(System.Text.Encoding.UTF8.GetString(envelope));
                Console.Out.Write('\n');
            }

            return 0;
        }
        catch (Exception ex) when (ex is FileNotFoundException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

internal sealed class CatalogVerifyCommand : Command<CatalogVerifySettings>
{
    protected override int Execute(CommandContext context, CatalogVerifySettings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.Package))
        {
            Console.Error.WriteLine($"package not found: {settings.Package}");
            return 1;
        }

        CatalogPackageValidation validation = CatalogPackage.Validate(File.ReadAllBytes(settings.Package));
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
        console.MarkupLine($"[dim]base workflow id:[/] {Markup.Escape(validation.BaseWorkflowId ?? "—")}");
        console.MarkupLine($"[dim]canonical sha-256:[/] {Markup.Escape(validation.Hash)}");
        console.MarkupLine($"[dim]sources:[/] {validation.Sources.Count}");

        bool ok = validation.IsValid;
        foreach (string issue in validation.Issues)
        {
            console.MarkupLine($"[red]✗[/] {Markup.Escape(issue)}");
        }

        if (settings.ExpectHash is { Length: > 0 } expected && !string.Equals(expected, validation.Hash, StringComparison.OrdinalIgnoreCase))
        {
            console.MarkupLine($"[red]✗[/] hash mismatch: expected {Markup.Escape(expected)}");
            ok = false;
        }

        if (ok)
        {
            console.MarkupLine("[green]✓[/] package is valid.");
        }

        return ok ? 0 : 1;
    }
}

internal sealed class CatalogUnpackCommand : Command<CatalogUnpackSettings>
{
    protected override int Execute(CommandContext context, CatalogUnpackSettings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.Package))
        {
            Console.Error.WriteLine($"package not found: {settings.Package}");
            return 1;
        }

        (byte[] workflow, IReadOnlyList<KeyValuePair<string, byte[]>> sources) unpacked;
        try
        {
            unpacked = CatalogPackage.Unpack(File.ReadAllBytes(settings.Package));
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        string outDir = settings.OutDir!;
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "workflow.json"), unpacked.workflow);
        if (unpacked.sources.Count > 0)
        {
            string sourcesDir = Path.Combine(outDir, "sources");
            Directory.CreateDirectory(sourcesDir);
            foreach (KeyValuePair<string, byte[]> source in unpacked.sources)
            {
                File.WriteAllBytes(Path.Combine(sourcesDir, source.Key + ".json"), source.Value);
            }
        }

        Console.WriteLine($"Unpacked workflow.json + {unpacked.sources.Count} source(s) to {outDir}.");
        return 0;
    }
}

internal sealed class CatalogAddCommand : AsyncCommand<CatalogAddSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogAddSettings settings, CancellationToken cancellationToken)
    {
        byte[] envelope;
        try
        {
            envelope = settings.Package is { } packagePath
                ? File.ReadAllBytes(packagePath)
                : CatalogPackageFiles.BuildFromWorkflow(settings.Workflow!, CatalogPackageFiles.ParseSourceArgs(settings.Sources));
        }
        catch (Exception ex) when (ex is FileNotFoundException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var package = new BinaryPartData((stream, ct) => { stream.Write(envelope); return ValueTask.CompletedTask; }, "application/json", "package.json");

        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // The body's Source values are ref structs, so they must be built (and consumed) without an
            // intervening await — construct them here, immediately before the call.
            Models.PostCatalogBody.JsonStringArray.Source tags = default;
            if (settings.Tags.Length > 0)
            {
                string[] tagValues = settings.Tags;
                tags = new Models.PostCatalogBody.JsonStringArray.Source((ref Models.PostCatalogBody.JsonStringArray.Builder ab) =>
                {
                    foreach (string tag in tagValues)
                    {
                        ab.AddItem(tag);
                    }
                });
            }

            Models.PostCatalogBody.Source body = Models.PostCatalogBody.Build(BuildOwner(settings.OwnerName!, settings.OwnerEmail!, settings.OwnerTeam, settings.OwnerUrl), default, tags);
            await using AddCatalogVersionResponse response = await client.AddCatalogVersionAsync(body, package, cancellationToken, validationMode: ValidationMode.None);
            return response.MatchResult(
                summary => Output.Print(summary.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }

    private static Models.CatalogOwner.Source BuildOwner(string name, string email, string? team, string? url)
        => new((ref Models.CatalogOwner.Builder b) =>
        {
            Models.JsonString.Source teamSource = team is { } t ? (Models.JsonString.Source)t : default;
            Models.JsonIri.Source urlSource = url is { } u ? (Models.JsonIri.Source)u : default;
            b.Create(email: email, name: name, team: teamSource, url: urlSource);
        });
}

internal sealed class CatalogSearchCommand : AsyncCommand<CatalogSearchSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogSearchSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.JsonString.Source query = settings.Query is { } q ? (Models.JsonString.Source)q : default;
            Models.JsonString.Source baseWorkflowId = settings.BaseWorkflowId is { } b ? (Models.JsonString.Source)b : default;
            Models.CatalogStatus.Source status = settings.Status is { } s ? (Models.CatalogStatus.Source)s : default;
            Models.JsonString.Source owner = settings.Owner is { } o ? (Models.JsonString.Source)o : default;
            Models.PageLimit.Source limit = settings.Limit is { } l ? (Models.PageLimit.Source)l : default;
            Models.JsonString.Source pageToken = settings.PageToken is { } p ? (Models.JsonString.Source)p : default;

            Models.TagList.Source tag = default;
            if (settings.Tags.Length > 0)
            {
                string[] tagValues = settings.Tags;
                tag = new Models.TagList.Source((ref Models.TagList.Builder ab) =>
                {
                    foreach (string t in tagValues)
                    {
                        ab.AddItem(t);
                    }
                });
            }

            await using SearchCatalogResponse response = await client.SearchCatalogAsync(query, baseWorkflowId, workflowIdPrefix: default, tag, status, owner, limit, pageToken, cancellationToken);
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            return response.MatchResult(
                page => asJson ? Output.Print(page.ToString()) : RenderVersions(page),
                Output.Unexpected);
        }
    }

    internal static int RenderVersions(Models.CatalogPage page)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Workflow");
        table.AddColumn("Ver");
        table.AddColumn("Title");
        table.AddColumn("Status");
        table.AddColumn("Owner");
        table.AddColumn("Hash");
        table.AddColumn("Tags");

        foreach (Models.CatalogVersionSummary summary in page.Versions.EnumerateArray())
        {
            string tags = "—";
            if (summary.Tags.IsNotUndefined())
            {
                var labels = new List<string>();
                foreach (Models.JsonString tag in summary.Tags.EnumerateArray())
                {
                    labels.Add((string)tag);
                }

                if (labels.Count > 0)
                {
                    tags = string.Join(", ", labels);
                }
            }

            string hash = (string)summary.Hash;
            table.AddRow(
                Markup.Escape((string)summary.WorkflowId),
                Markup.Escape(summary.VersionNumber.ToString()),
                Markup.Escape((string)summary.Title),
                Markup.Escape((string)summary.Status),
                Markup.Escape(summary.Owner.Name.IsNotUndefined() ? (string)summary.Owner.Name : "—"),
                Markup.Escape(hash.Length > 12 ? hash[..12] : hash),
                Markup.Escape(tags));
        }

        console.Write(table);
        if (page.NextPageToken.IsNotUndefined())
        {
            console.MarkupLine($"[dim]next page token:[/] {Markup.Escape((string)page.NextPageToken)}");
        }

        return 0;
    }
}

internal sealed class CatalogVersionsCommand : AsyncCommand<CatalogVersionsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogVersionsSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.PageLimit.Source limit = settings.Limit is { } l ? (Models.PageLimit.Source)l : default;
            Models.JsonString.Source pageToken = settings.PageToken is { } p ? (Models.JsonString.Source)p : default;
            await using ListCatalogVersionsResponse response = await client.ListCatalogVersionsAsync(settings.BaseWorkflowId, limit, pageToken, cancellationToken);
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            return response.MatchResult(
                page => asJson ? Output.Print(page.ToString()) : CatalogSearchCommand.RenderVersions(page),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogGetCommand : AsyncCommand<CatalogVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetCatalogVersionResponse response = await client.GetCatalogVersionAsync(settings.BaseWorkflowId, settings.Version, cancellationToken);
            return response.MatchResult(
                summary => Output.Print(summary.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogPackageCommand : AsyncCommand<CatalogDownloadSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogDownloadSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetCatalogPackageResponse response = await client.GetCatalogPackageAsync(settings.BaseWorkflowId, settings.Version, cancellationToken);
            return await response.MatchResult(
                stream => WriteStreamAsync(stream, settings.Out, cancellationToken),
                problem => Task.FromResult(Output.Problem(problem)),
                code => Task.FromResult(Output.Unexpected(code)));
        }
    }

    internal static async Task<int> WriteStreamAsync(Stream? stream, string? outPath, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            Console.Error.WriteLine("The response carried no content.");
            return 1;
        }

        if (outPath is { } path)
        {
            await using FileStream file = File.Create(path);
            await stream.CopyToAsync(file, cancellationToken);
            Console.WriteLine($"Wrote package to {path}.");
        }
        else
        {
            await using Stream stdout = Console.OpenStandardOutput();
            await stream.CopyToAsync(stdout, cancellationToken);
        }

        return 0;
    }

    internal static int WriteDocument(string json, string? outPath)
    {
        if (outPath is { } path)
        {
            File.WriteAllText(path, json);
            Console.WriteLine($"Wrote {json.Length} bytes to {path}.");
            return 0;
        }

        Console.WriteLine(json);
        return 0;
    }
}

internal sealed class CatalogWorkflowCommand : AsyncCommand<CatalogDownloadSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogDownloadSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetCatalogWorkflowResponse response = await client.GetCatalogWorkflowAsync(settings.BaseWorkflowId, settings.Version, cancellationToken);
            return response.MatchResult(
                document => CatalogPackageCommand.WriteDocument(document.ToString(), settings.Out),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogSourceCommand : AsyncCommand<CatalogSourceSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogSourceSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetCatalogSourceResponse response = await client.GetCatalogSourceAsync(settings.BaseWorkflowId, settings.Version, settings.SourceName, cancellationToken);
            return response.MatchResult(
                document => CatalogPackageCommand.WriteDocument(document.ToString(), settings.Out),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogUpdateCommand : AsyncCommand<CatalogUpdateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogUpdateSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.CatalogStatus.Source status = settings.Status is { } s ? (Models.CatalogStatus.Source)s : default;
            Models.CatalogOwner.Source owner = default;
            if (settings.OwnerName is { } name && settings.OwnerEmail is { } email)
            {
                owner = BuildOwner(name, email, settings.OwnerTeam, settings.OwnerUrl);
            }

            Models.CatalogMetadataPatch.JsonStringArray.Source tags = default;
            if (settings.Tags.Length > 0)
            {
                string[] tagValues = settings.Tags;
                tags = new Models.CatalogMetadataPatch.JsonStringArray.Source((ref Models.CatalogMetadataPatch.JsonStringArray.Builder ab) =>
                {
                    foreach (string t in tagValues)
                    {
                        ab.AddItem(t);
                    }
                });
            }

            Models.CatalogMetadataPatch.Source body = Models.CatalogMetadataPatch.Build(owner, status, tags);
            await using UpdateCatalogVersionResponse response = await client.UpdateCatalogVersionAsync(settings.BaseWorkflowId, settings.Version, body, cancellationToken);
            return response.MatchResult(
                summary => Output.Print(summary.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }

    internal static Models.CatalogOwner.Source BuildOwner(string name, string email, string? team, string? url)
        => new((ref Models.CatalogOwner.Builder b) =>
        {
            Models.JsonString.Source teamSource = team is { } t ? (Models.JsonString.Source)t : default;
            Models.JsonIri.Source urlSource = url is { } u ? (Models.JsonIri.Source)u : default;
            b.Create(email: email, name: name, team: teamSource, url: urlSource);
        });
}

internal sealed class CatalogObsoleteCommand : AsyncCommand<CatalogVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.CatalogMetadataPatch.Source body = Models.CatalogMetadataPatch.Build(status: (Models.CatalogStatus.Source)"Obsolete");
            await using UpdateCatalogVersionResponse response = await client.UpdateCatalogVersionAsync(settings.BaseWorkflowId, settings.Version, body, cancellationToken);
            return response.MatchResult(
                summary => Output.Print(summary.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogDeleteCommand : AsyncCommand<CatalogVersionSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CatalogVersionSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteCatalogVersionResponse response = await client.DeleteCatalogVersionAsync(settings.BaseWorkflowId, settings.Version, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted version {settings.Version} of '{settings.BaseWorkflowId}'.");
                return 0;
            }

            return response.MatchResult(
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class CatalogPurgeCommand : AsyncCommand<RunsSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunsSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient client) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using PurgeCatalogResponse response = await client.PurgeCatalogAsync(cancellationToken);
            return response.MatchResult(
                result => Output.Print(result.ToString()),
                Output.Unexpected);
        }
    }
}