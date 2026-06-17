// <copyright file="Commands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Corvus.Text.Json.Patch;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings shared by every command: how to reach (and authenticate to) the control plane.</summary>
internal class RunsSettings : CommandSettings
{
    [CommandOption("--server <URL>")]
    [Description("Control-plane base URL — origin plus any base path (or env ARAZZO_RUNS_SERVER), e.g. https://host:8080 or https://host/arazzo/v1.")]
    public string? Server { get; init; }

    [CommandOption("--token <TOKEN>")]
    [Description("OAuth2 bearer access token (or env ARAZZO_RUNS_TOKEN).")]
    public string? Token { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => this.ResolveServer() is null
            ? Spectre.Console.ValidationResult.Error("--server <url> is required (or set ARAZZO_RUNS_SERVER).")
            : Spectre.Console.ValidationResult.Success();

    /// <summary>Builds the API client (and the HTTP client / transport it owns) for this invocation, resolving the
    /// access token from the <c>--token</c> flag, the environment, or the login cache (refreshing if stale).</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiRunsClient Client)> CreateClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiRunsClient(transport));
    }

    /// <summary>Builds the catalog API client (and the HTTP client / transport it owns) for this invocation, resolving
    /// the access token from the <c>--token</c> flag, the environment, or the login cache (refreshing if stale).</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and catalog API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiCatalogClient Client)> CreateCatalogClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiCatalogClient(transport));
    }

    /// <summary>Builds the security API client (and the HTTP client / transport it owns) for this invocation.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and security API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiSecurityClient Client)> CreateSecurityClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiSecurityClient(transport));
    }

    /// <summary>Builds the source-credential API client (and the HTTP client / transport it owns) for this invocation.
    /// The credential surface manages <em>references</em> and non-secret metadata only — never secret material.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and credentials API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiCredentialsClient Client)> CreateCredentialsClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiCredentialsClient(transport));
    }

    /// <summary>Builds the workflow-administration API client (and the HTTP client / transport it owns) for this invocation.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and administrators API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiAdministratorsClient Client)> CreateAdministratorsClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiAdministratorsClient(transport));
    }

    /// <summary>Builds the access-request API client (and the HTTP client / transport it owns) for this invocation.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP client, transport, and access-request API client. Dispose the HTTP client and transport.</returns>
    public async Task<(HttpClient Http, HttpClientTransport Transport, ApiAccessRequestsClient Client)> CreateAccessRequestsClientAsync(CancellationToken cancellationToken)
    {
        string? token = await TokenSource.ResolveAsync(this.Token, cancellationToken).ConfigureAwait(false);
        HttpClient http = this.CreateHttpClient();
        var transport = new HttpClientTransport(http, token is null ? null : new BearerTokenAuthentication(token));
        return (http, transport, new ApiAccessRequestsClient(transport));
    }

    private string? ResolveServer() => this.Server ?? Environment.GetEnvironmentVariable("ARAZZO_RUNS_SERVER");

    /// <summary>
    /// Builds the HTTP client for this invocation, adapting to the control plane's base path. The generated
    /// client builds <em>absolute</em> operation paths (e.g. <c>/catalog</c>); when <c>--server</c> carries a
    /// base path (e.g. <c>https://host/arazzo/v1</c>) a <see cref="BasePathHandler"/> prepends it, so the CLI
    /// works wherever a deployment mounts the API (at the root, or under any prefix the server's base URL gives).
    /// </summary>
    /// <returns>The HTTP client (whose handler chain it owns and disposes).</returns>
    private HttpClient CreateHttpClient()
    {
        var server = new Uri(this.ResolveServer()!, UriKind.Absolute);
        string basePath = server.AbsolutePath.TrimEnd('/');
        HttpMessageHandler handler = new HttpClientHandler();
        if (basePath.Length > 0)
        {
            handler = new BasePathHandler(basePath) { InnerHandler = handler };
        }

        return new HttpClient(handler) { BaseAddress = new Uri(server.GetLeftPart(UriPartial.Authority)) };
    }
}

/// <summary>
/// Mounts each request under the control plane's base path. The generated client produces absolute operation
/// paths (<c>/runs</c>, <c>/catalog</c>, …) resolved against the origin; this prepends the base path that the
/// <c>--server</c> URL carries (e.g. <c>/arazzo/v1</c>), preserving the query — so the CLI adapts to wherever the
/// deployment serves the API rather than assuming it sits at the origin root.
/// </summary>
internal sealed class BasePathHandler(string basePath) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is { IsAbsoluteUri: true } uri)
        {
            request.RequestUri = new UriBuilder(uri) { Path = basePath + uri.AbsolutePath }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Settings for a command that targets a single run by id.</summary>
internal class RunIdSettings : RunsSettings
{
    [CommandArgument(0, "<runId>")]
    [Description("The run id.")]
    public string RunId { get; init; } = string.Empty;
}

internal sealed class ListSettings : RunsSettings
{
    [CommandOption("--status <STATUS>")]
    [Description("Restrict to runs in this lifecycle status (Pending/Running/Suspended/Completed/Cancelled/Faulted).")]
    public string? Status { get; init; }

    [CommandOption("--workflow-id <ID>")]
    [Description("Restrict to runs of this workflow.")]
    public string? WorkflowId { get; init; }

    [CommandOption("--created-after <RFC3339>")]
    [Description("Restrict to runs created at or after this instant (inclusive).")]
    public string? CreatedAfter { get; init; }

    [CommandOption("--created-before <RFC3339>")]
    [Description("Restrict to runs created strictly before this instant (exclusive).")]
    public string? CreatedBefore { get; init; }

    [CommandOption("--updated-after <RFC3339>")]
    [Description("Restrict to runs last updated at or after this instant (inclusive).")]
    public string? UpdatedAfter { get; init; }

    [CommandOption("--updated-before <RFC3339>")]
    [Description("Restrict to runs last updated strictly before this instant (exclusive).")]
    public string? UpdatedBefore { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description("Restrict to runs carrying this tag (repeat to require several, AND-matched).")]
    public string[] Tags { get; init; } = [];

    [CommandOption("--correlation-id <ID>")]
    [Description("Restrict to runs with this telemetry correlation id (exact match).")]
    public string? CorrelationId { get; init; }

    [CommandOption("--limit <N>")]
    [Description("Maximum runs per page.")]
    public int? Limit { get; init; }

    [CommandOption("--page-token <TOKEN>")]
    [Description("Continuation token from a previous page's nextPageToken.")]
    public string? PageToken { get; init; }

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class ResumeSettings : RunIdSettings
{
    [CommandOption("--mode <MODE>")]
    [Description("RetryFaultedStep (default), Rewind, Skip or StatePatch.")]
    [DefaultValue("RetryFaultedStep")]
    public string Mode { get; init; } = "RetryFaultedStep";

    [CommandOption("--target-cursor <N>")]
    [Description("Rewind: the cursor to rewind to (required). Skip: the cursor to resume at (default faulted + 1).")]
    public int? TargetCursor { get; init; }

    [CommandOption("--skip-outputs-file <PATH>")]
    [Description("Skip: path to a JSON file of outputs to record for the skipped step.")]
    public string? SkipOutputsFile { get; init; }

    [CommandOption("--patch-file <PATH>")]
    [Description("StatePatch: path to a file containing an RFC 6902 JSON Patch array.")]
    public string? PatchFile { get; init; }
}

internal sealed class CancelSettings : RunIdSettings
{
    [CommandOption("--reason <TEXT>")]
    [Description("An operator-supplied reason for the cancellation (recorded for audit).")]
    public string? Reason { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrEmpty(this.Reason)
            ? Spectre.Console.ValidationResult.Error("--reason <text> is required for cancel.")
            : base.Validate();
}

internal sealed class PurgeSettings : RunsSettings
{
    [CommandOption("--older-than <RFC3339>")]
    [Description("Reap terminal runs last updated strictly before this instant.")]
    public string? OlderThan { get; init; }

    [CommandOption("--limit <N>")]
    [Description("Maximum runs to delete in one call.")]
    public int? Limit { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrEmpty(this.OlderThan)
            ? Spectre.Console.ValidationResult.Error("--older-than <rfc3339-timestamp> is required for purge.")
            : base.Validate();
}

internal sealed class ListCommand : AsyncCommand<ListSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.WorkflowRunStatus.Source status = default;
            if (settings.Status is { } s)
            {
                status = s;
            }

            Models.JsonString.Source workflowId = default;
            if (settings.WorkflowId is { } w)
            {
                workflowId = w;
            }

            Models.JsonDateTime.Source createdAfter = default;
            if (settings.CreatedAfter is { } ca)
            {
                createdAfter = ca;
            }

            Models.JsonDateTime.Source createdBefore = default;
            if (settings.CreatedBefore is { } cb)
            {
                createdBefore = cb;
            }

            Models.JsonDateTime.Source updatedAfter = default;
            if (settings.UpdatedAfter is { } ua)
            {
                updatedAfter = ua;
            }

            Models.JsonDateTime.Source updatedBefore = default;
            if (settings.UpdatedBefore is { } ub)
            {
                updatedBefore = ub;
            }

            Models.TagList.Source tag = default;
            if (settings.Tags.Length > 0)
            {
                string[] tagValues = settings.Tags;
                tag = new Models.TagList.Source((ref Models.TagList.Builder arrayBuilder) =>
                {
                    foreach (string t in tagValues)
                    {
                        arrayBuilder.AddItem(t);
                    }
                });
            }

            Models.JsonString.Source correlationId = default;
            if (settings.CorrelationId is { } cid)
            {
                correlationId = cid;
            }

            Models.PageLimit.Source limit = default;
            if (settings.Limit is { } l)
            {
                limit = l;
            }

            Models.JsonString.Source pageToken = default;
            if (settings.PageToken is { } p)
            {
                pageToken = p;
            }

            await using ListRunsResponse response = await client.ListRunsAsync(status, workflowId, createdAfter, createdBefore, updatedAfter, updatedBefore, tag, correlationId, limit, pageToken, cancellationToken);
            bool asJson = settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase);
            return response.MatchResult(
                page => asJson ? Output.Print(page.ToString()) : RenderTable(page),
                Output.Problem,
                Output.Unexpected);
        }
    }

    // Render a human-readable table to the current Console.Out (bound explicitly so output stays
    // correct under redirection / capture). Run ids etc. are escaped against Spectre markup.
    private static int RenderTable(Models.WorkflowRunPage page)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Id");
        table.AddColumn("Status");
        table.AddColumn("Workflow");
        table.AddColumn("Updated");
        table.AddColumn("Correlation");
        table.AddColumn("Tags");

        foreach (Models.WorkflowRunSummary summary in page.Runs.EnumerateArray())
        {
            string correlationId = summary.CorrelationId.IsNotUndefined() ? (string)summary.CorrelationId : "—";
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

            table.AddRow(
                Markup.Escape((string)summary.Id),
                Markup.Escape((string)summary.Status),
                Markup.Escape((string)summary.WorkflowId),
                Markup.Escape((string)summary.UpdatedAt),
                Markup.Escape(correlationId),
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

internal sealed class GetCommand : AsyncCommand<RunIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetRunResponse response = await client.GetRunAsync(settings.RunId, cancellationToken);
            return response.MatchResult(
                detail => Output.Print(detail.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class ResumeCommand : AsyncCommand<ResumeSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ResumeSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // Each mode builds its union variant from native values via the generated Build()/CreateBuilder()
            // factories — no hand-composed JSON. The file-valued modes (Skip outputs / StatePatch) load the
            // operator's JSON document and materialize the variant into a workspace so the immutable value
            // (not a captured ref) feeds the client.
            switch (settings.Mode)
            {
                case "RetryFaultedStep":
                {
                    await using ResumeRunResponse response = await client.ResumeRunAsync(
                        settings.RunId,
                        Models.ResumeRequest.Build(static (ref Models.RetryFaultedStepResume.Builder b) => b.Create()),
                        cancellationToken);
                    return RenderResume(response);
                }

                case "Rewind":
                {
                    if (settings.TargetCursor is not { } targetCursor)
                    {
                        Console.Error.WriteLine("--target-cursor <n> is required for --mode Rewind.");
                        return 1;
                    }

                    await using ResumeRunResponse response = await client.ResumeRunAsync(
                        settings.RunId,
                        Models.RewindResume.Build(targetCursor),
                        cancellationToken);
                    return RenderResume(response);
                }

                case "Skip":
                    return await ResumeSkipAsync(client, settings, cancellationToken);

                case "StatePatch":
                    return await ResumeStatePatchAsync(client, settings, cancellationToken);

                default:
                    Console.Error.WriteLine($"Unknown --mode '{settings.Mode}'. Use RetryFaultedStep, Rewind, Skip or StatePatch.");
                    return 1;
            }
        }
    }

    private static async Task<int> ResumeSkipAsync(ApiRunsClient client, ResumeSettings settings, CancellationToken cancellationToken)
    {
        if (settings.SkipOutputsFile is { } outputsFile)
        {
            if (!File.Exists(outputsFile))
            {
                Console.Error.WriteLine($"--skip-outputs-file not found: {outputsFile}");
                return 1;
            }

            using ParsedJsonDocument<JsonElement> outputs = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(outputsFile));
            await using ResumeRunResponse response = await client.ResumeRunAsync(
                settings.RunId,
                Models.SkipResume.Build(
                    skipOutputs: outputs.RootElement,
                    targetCursor: settings.TargetCursor is { } c ? (Models.JsonInt32.Source)c : default),
                cancellationToken);
            return RenderResume(response);
        }

        await using ResumeRunResponse noOutputs = await client.ResumeRunAsync(
            settings.RunId,
            Models.SkipResume.Build(targetCursor: settings.TargetCursor is { } cursor ? (Models.JsonInt32.Source)cursor : default),
            cancellationToken);
        return RenderResume(noOutputs);
    }

    private static async Task<int> ResumeStatePatchAsync(ApiRunsClient client, ResumeSettings settings, CancellationToken cancellationToken)
    {
        if (settings.PatchFile is not { } patchFile)
        {
            Console.Error.WriteLine("--patch-file <path> is required for --mode StatePatch.");
            return 1;
        }

        if (!File.Exists(patchFile))
        {
            Console.Error.WriteLine($"--patch-file not found: {patchFile}");
            return 1;
        }

        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(patchFile));

        // Reject a non-conformant patch up front (RFC 6902, via the JSON Patch document's own schema)
        // rather than sending a malformed body the server would refuse.
        JsonPatchDocument patchDocument = document.RootElement;
        if (!patchDocument.EvaluateSchema())
        {
            Console.Error.WriteLine($"--patch-file is not a conformant RFC 6902 JSON Patch document: {patchFile}");
            return 1;
        }

        Models.StatePatchResume.JsonObjectArray patch = document.RootElement;
        await using ResumeRunResponse response = await client.ResumeRunAsync(
            settings.RunId,
            Models.StatePatchResume.Build(patch),
            cancellationToken);
        return RenderResume(response);
    }

    private static int RenderResume(ResumeRunResponse response) => response.MatchResult(
        detail => Output.Print(detail.ToString()),
        Output.Problem,
        Output.Problem,
        Output.Problem,
        Output.Unexpected);
}

internal sealed class CancelCommand : AsyncCommand<CancelSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancelSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using CancelRunResponse response = await client.CancelRunAsync(
                settings.RunId,
                Models.CancelRequest.Build(settings.Reason!),
                cancellationToken);
            return response.MatchResult(
                detail => Output.Print(detail.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class DeleteCommand : AsyncCommand<RunIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteRunResponse response = await client.DeleteRunAsync(settings.RunId, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted run '{settings.RunId}'.");
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

internal sealed class PurgeCommand : AsyncCommand<PurgeSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, PurgeSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiRunsClient client) = await settings.CreateClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            Models.JsonDateTime.Source olderThan = settings.OlderThan!;
            Models.PageLimit.Source limit = default;
            if (settings.Limit is { } l)
            {
                limit = l;
            }

            await using PurgeRunsResponse response = await client.PurgeRunsAsync(olderThan, limit, cancellationToken);
            return response.MatchResult(
                result => Output.Print(result.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class LoginSettings : CommandSettings
{
    [CommandOption("--authority <URL>")]
    [Description("OIDC issuer/authority base URL (or env ARAZZO_RUNS_AUTHORITY); discovery at {authority}/.well-known/openid-configuration.")]
    public string? Authority { get; init; }

    [CommandOption("--client-id <ID>")]
    [Description("OAuth2 client id (or env ARAZZO_RUNS_CLIENT_ID).")]
    public string? ClientId { get; init; }

    [CommandOption("--scope <SCOPES>")]
    [Description("Space-separated scopes to request.")]
    [DefaultValue("openid offline_access runs:read runs:write runs:purge")]
    public string Scope { get; init; } = "openid offline_access runs:read runs:write runs:purge";

    [CommandOption("--use-device-code")]
    [Description("Use the device authorization grant (RFC 8628) instead of a browser loopback redirect (RFC 8252).")]
    public bool UseDeviceCode { get; init; }

    public string? ResolveAuthority() => this.Authority ?? Environment.GetEnvironmentVariable("ARAZZO_RUNS_AUTHORITY");

    public string? ResolveClientId() => this.ClientId ?? Environment.GetEnvironmentVariable("ARAZZO_RUNS_CLIENT_ID");

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        if (this.ResolveAuthority() is null)
        {
            return Spectre.Console.ValidationResult.Error("--authority <url> is required (or set ARAZZO_RUNS_AUTHORITY).");
        }

        return this.ResolveClientId() is null
            ? Spectre.Console.ValidationResult.Error("--client-id <id> is required (or set ARAZZO_RUNS_CLIENT_ID).")
            : Spectre.Console.ValidationResult.Success();
    }
}

internal sealed class LoginCommand : AsyncCommand<LoginSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, LoginSettings settings, CancellationToken cancellationToken)
    {
        var config = new OAuthConfig(settings.ResolveAuthority()!, settings.ResolveClientId()!, settings.Scope);
        try
        {
            TokenSet token = await OAuthFlows.LoginAsync(config, settings.UseDeviceCode, cancellationToken);
            TokenCache.Save(token);
            Console.WriteLine($"Signed in. Access token cached (expires {token.ExpiresAtUtc:u}).");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine($"Login failed: {ex.Message}");
            return 1;
        }
    }
}

internal sealed class LogoutCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        TokenCache.Clear();
        Console.WriteLine("Signed out (cached token removed).");
        return 0;
    }
}

/// <summary>Console output helpers shared by the commands: results to stdout (JSON), errors to stderr.</summary>
internal static class Output
{
    public static int Print(string json)
    {
        Console.WriteLine(json);
        return 0;
    }

    public static int Problem(Models.ProblemDetails problem)
    {
        Console.Error.WriteLine(problem.ToString());
        return 1;
    }

    public static int Unexpected(int statusCode)
    {
        Console.Error.WriteLine($"Unexpected response status {statusCode}.");
        return 1;
    }
}

/// <summary>Adds a static OAuth2 bearer token to each outgoing request.</summary>
internal sealed class BearerTokenAuthentication(string token) : IHttpAuthenticationProvider
{
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return ValueTask.CompletedTask;
    }
}