// <copyright file="ArazzoControlPlaneGitHubHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The brokered GitHub API (workflow-designer design §4.7): begin/complete the App's
/// user-to-server sign-in, the caller's session status (identity + installations + reachable
/// repositories), and proxied contents reads for the open/import dialogs. Fails closed when the
/// deployment brokers no GitHub App. Token custody is the broker's, keyed by control-plane
/// principal; the callback authenticates by its single-use state (a top-level navigation carries
/// no bearer token).
/// </summary>
public sealed class ArazzoControlPlaneGitHubHandler : IApiGithubHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // Session-status caps: at most this many installations, each listing at most one page of
    // repositories. The import dialog is a picker, not a mirror; deep listings go through browse.
    private const int MaxInstallations = 10;

    private readonly GitHubBroker? broker;
    private readonly ControlPlaneAccess access;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext;
    private readonly string subjectClaimType;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneGitHubHandler"/> class.</summary>
    /// <param name="broker">The deployment's GitHub broker; <see langword="null"/> refuses every operation (fails closed).</param>
    /// <param name="access">Resolves the caller's identity (the token-custody key).</param>
    /// <param name="httpContext">Reads the authenticated principal in the modes whose access binding carries none (ScopesOnly).</param>
    /// <param name="subjectClaimType">The claim naming the authenticated subject (the custody key's fallback dimension).</param>
    internal ArazzoControlPlaneGitHubHandler(GitHubBroker? broker, ControlPlaneAccess access, Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext = null, string subjectClaimType = "sub")
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(subjectClaimType);
        this.broker = broker;
        this.access = access;
        this.httpContext = httpContext;
        this.subjectClaimType = subjectClaimType;
    }

    /// <inheritdoc/>
    public ValueTask<BeginGitHubAuthResult> HandleBeginGitHubAuthAsync(BeginGitHubAuthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return ValueTask.FromResult(BeginGitHubAuthResult.BadRequest(NotBrokeredProblem(), workspace));
        }

        if (this.PrincipalKey() is not { } principal)
        {
            return ValueTask.FromResult(BeginGitHubAuthResult.BadRequest(
                Problem("github-identity-unresolvable", "Identity unresolvable", 400, "A GitHub session binds to the calling principal, but no stable principal identity resolves for this caller."), workspace));
        }

        (string authorizeUrl, string state) = github.BeginAuth(principal);
        return ValueTask.FromResult(BeginGitHubAuthResult.Ok(
            new((ref Models.GitHubAuthStart.Builder b) => b.Create(authorizeUrl: authorizeUrl, state: state)), workspace));
    }

    /// <inheritdoc/>
    public async ValueTask<CompleteGitHubAuthResult> HandleCompleteGitHubAuthAsync(CompleteGitHubAuthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return CompleteGitHubAuthResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        GitHubBroker.CompleteOutcome outcome = await github.CompleteAuthAsync((string)parameters.State, (string)parameters.Code, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            GitHubBroker.CompleteOutcome.Success => CompleteGitHubAuthResult.Ok(),
            GitHubBroker.CompleteOutcome.InvalidState => CompleteGitHubAuthResult.BadRequest(
                Problem("github-invalid-state", "Invalid state", 400, "The state is unknown, expired, or already used; begin the sign-in again."), workspace),
            _ => CompleteGitHubAuthResult.BadRequest(
                Problem("github-exchange-failed", "Exchange failed", 400, "GitHub refused the code exchange; begin the sign-in again."), workspace),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<GetGitHubStatusResult> HandleGetGitHubStatusAsync(GetGitHubStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return GetGitHubStatusResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        string? principal = this.PrincipalKey();
        if (principal is null)
        {
            return Disconnected(workspace);
        }

        (GitHubBroker.ReadOutcome userOutcome, ParsedJsonDocument<JsonElement>? userDoc) = await github.GetUserAsync(principal, cancellationToken).ConfigureAwait(false);
        if (userOutcome != GitHubBroker.ReadOutcome.Success || userDoc is null)
        {
            userDoc?.Dispose();
            return Disconnected(workspace);
        }

        // The user's installations and, per installation, the repositories the user ∩ installation
        // intersection grants. Every payload document stays alive through the single pooled write.
        var installations = new List<(ParsedJsonDocument<JsonElement> Installation, ParsedJsonDocument<JsonElement>? Repositories)>();
        try
        {
            using (userDoc)
            {
                (GitHubBroker.ReadOutcome outcome, ParsedJsonDocument<JsonElement>? installationsDoc) = await github.GetInstallationsAsync(principal, cancellationToken).ConfigureAwait(false);
                using ParsedJsonDocument<JsonElement>? heldInstallations = installationsDoc;
                if (outcome == GitHubBroker.ReadOutcome.Success && installationsDoc is not null
                    && installationsDoc.RootElement.TryGetProperty("installations"u8, out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement installation in list.EnumerateArray())
                    {
                        if (installations.Count >= MaxInstallations
                            || !installation.TryGetProperty("id"u8, out JsonElement id) || id.ValueKind != JsonValueKind.Number)
                        {
                            continue;
                        }

                        // An owned copy: the parent installations document disposes before the write.
                        var owned = ParsedJsonDocument<JsonElement>.Parse(PersistedJson.ToArray(installation, static (Utf8JsonWriter writer, in JsonElement i) => i.WriteTo(writer)));
                        (GitHubBroker.ReadOutcome repoOutcome, ParsedJsonDocument<JsonElement>? repositories) = await github.GetInstallationRepositoriesAsync(principal, id.GetInt64(), cancellationToken).ConfigureAwait(false);
                        installations.Add((owned, repoOutcome == GitHubBroker.ReadOutcome.Success ? repositories : null));
                    }
                }

                ParsedJsonDocument<Models.GitHubStatus> body = WriteStatus(userDoc.RootElement, installations);
                workspace.TakeOwnership(body);
                return GetGitHubStatusResult.Ok(Models.GitHubStatus.From(body.RootElement), workspace);
            }
        }
        finally
        {
            foreach ((ParsedJsonDocument<JsonElement> installation, ParsedJsonDocument<JsonElement>? repositories) in installations)
            {
                installation.Dispose();
                repositories?.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<DeleteGitHubSessionResult> HandleDeleteGitHubSessionAsync(DeleteGitHubSessionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return ValueTask.FromResult(DeleteGitHubSessionResult.BadRequest(NotBrokeredProblem(), workspace));
        }

        if (this.PrincipalKey() is { } principal)
        {
            github.Disconnect(principal);
        }

        return ValueTask.FromResult(DeleteGitHubSessionResult.NoContent());
    }

    /// <inheritdoc/>
    public async ValueTask<BrowseRepoResult> HandleBrowseRepoAsync(BrowseRepoParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return BrowseRepoResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        string owner = (string)parameters.Owner;
        string repo = (string)parameters.Repo;
        if (this.PrincipalKey() is not { } principal || !github.IsConnected(principal))
        {
            return BrowseRepoResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The caller has no GitHub session; begin the sign-in first."), workspace);
        }

        string? path = parameters.Path.IsNotUndefined() ? (string)parameters.Path : null;
        string? reference = parameters.Ref.IsNotUndefined() ? (string)parameters.Ref : null;
        (GitHubBroker.ReadOutcome outcome, ParsedJsonDocument<JsonElement>? payload) = await github.BrowseAsync(principal, owner, repo, path, reference, cancellationToken).ConfigureAwait(false);
        if (outcome == GitHubBroker.ReadOutcome.NotConnected)
        {
            payload?.Dispose();
            return BrowseRepoResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The GitHub session is no longer valid; begin the sign-in again."), workspace);
        }

        if (outcome != GitHubBroker.ReadOutcome.Success || payload is null)
        {
            payload?.Dispose();
            return BrowseRepoResult.NotFound(
                Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}{(string.IsNullOrEmpty(path) ? string.Empty : "/" + path)}' does not exist, or is outside the user ∩ installation intersection."), workspace);
        }

        using (payload)
        {
            ParsedJsonDocument<Models.GitHubBrowseResult> body = WriteBrowse(payload.RootElement);
            workspace.TakeOwnership(body);
            return BrowseRepoResult.Ok(Models.GitHubBrowseResult.From(body.RootElement), workspace);
        }
    }

    // Projects GET /user (+ installations + per-installation repositories) into the contract's
    // GitHubStatus in one pooled write, field-selecting from the GitHub payloads bytes-native.
    private static ParsedJsonDocument<Models.GitHubStatus> WriteStatus(
        in JsonElement user, List<(ParsedJsonDocument<JsonElement> Installation, ParsedJsonDocument<JsonElement>? Repositories)> installations)
    {
        return PersistedJson.ToPooledDocument<Models.GitHubStatus, (JsonElement User, List<(ParsedJsonDocument<JsonElement> Installation, ParsedJsonDocument<JsonElement>? Repositories)> Installations)>(
            (user, installations),
            static (Utf8JsonWriter writer, in (JsonElement User, List<(ParsedJsonDocument<JsonElement> Installation, ParsedJsonDocument<JsonElement>? Repositories)> Installations) s) =>
            {
                writer.WriteStartObject();
                writer.WriteBoolean("connected"u8, true);
                WriteStringIfPresent(writer, "login"u8, s.User, "login"u8);
                WriteStringIfPresent(writer, "name"u8, s.User, "name"u8);
                WriteStringIfPresent(writer, "avatarUrl"u8, s.User, "avatar_url"u8);
                writer.WriteStartArray("installations"u8);
                foreach ((ParsedJsonDocument<JsonElement> installationDoc, ParsedJsonDocument<JsonElement>? repositoriesDoc) in s.Installations)
                {
                    JsonElement installation = installationDoc.RootElement;
                    writer.WriteStartObject();
                    if (installation.TryGetProperty("id"u8, out JsonElement id))
                    {
                        writer.WritePropertyName("id"u8);
                        id.WriteTo(writer);
                    }

                    if (installation.TryGetProperty("account"u8, out JsonElement account) && account.ValueKind == JsonValueKind.Object)
                    {
                        WriteStringIfPresent(writer, "account"u8, account, "login"u8);
                    }

                    writer.WriteStartArray("repositories"u8);
                    if (repositoriesDoc is not null
                        && repositoriesDoc.RootElement.TryGetProperty("repositories"u8, out JsonElement repositories) && repositories.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement repository in repositories.EnumerateArray())
                        {
                            WriteRepository(writer, repository);
                        }
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    private static void WriteRepository(Utf8JsonWriter writer, in JsonElement repository)
    {
        writer.WriteStartObject();
        if (repository.TryGetProperty("owner"u8, out JsonElement owner) && owner.ValueKind == JsonValueKind.Object)
        {
            WriteStringIfPresent(writer, "owner"u8, owner, "login"u8);
        }

        WriteStringIfPresent(writer, "name"u8, repository, "name"u8);
        WriteStringIfPresent(writer, "fullName"u8, repository, "full_name"u8);
        WriteStringIfPresent(writer, "defaultBranch"u8, repository, "default_branch"u8);
        if (repository.TryGetProperty("private"u8, out JsonElement isPrivate) && isPrivate.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            writer.WriteBoolean("private"u8, isPrivate.ValueKind == JsonValueKind.True);
        }

        writer.WriteEndObject();
    }

    // Projects a GitHub contents payload — an array for a directory, an object for a file — into
    // the contract's GitHubBrowseResult in one pooled write.
    private static ParsedJsonDocument<Models.GitHubBrowseResult> WriteBrowse(in JsonElement payload)
    {
        return PersistedJson.ToPooledDocument<Models.GitHubBrowseResult, JsonElement>(
            payload,
            static (Utf8JsonWriter writer, in JsonElement p) =>
            {
                writer.WriteStartObject();
                if (p.ValueKind == JsonValueKind.Array)
                {
                    writer.WriteString("kind"u8, "dir"u8);
                    writer.WriteStartArray("entries"u8);
                    foreach (JsonElement entry in p.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("type"u8, out JsonElement type) || !(type.ValueEquals("file") || type.ValueEquals("dir")))
                        {
                            continue; // symlinks/submodules are not browsable content
                        }

                        writer.WriteStartObject();
                        WriteStringIfPresent(writer, "name"u8, entry, "name"u8);
                        WriteStringIfPresent(writer, "path"u8, entry, "path"u8);
                        writer.WritePropertyName("type"u8);
                        type.WriteTo(writer);
                        if (entry.TryGetProperty("size"u8, out JsonElement size) && size.ValueKind == JsonValueKind.Number)
                        {
                            writer.WritePropertyName("size"u8);
                            size.WriteTo(writer);
                        }

                        WriteStringIfPresent(writer, "sha"u8, entry, "sha"u8);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteString("kind"u8, "file"u8);
                    writer.WriteStartObject("file"u8);
                    WriteStringIfPresent(writer, "name"u8, p, "name"u8);
                    WriteStringIfPresent(writer, "path"u8, p, "path"u8);
                    WriteStringIfPresent(writer, "sha"u8, p, "sha"u8);
                    if (p.TryGetProperty("size"u8, out JsonElement size) && size.ValueKind == JsonValueKind.Number)
                    {
                        writer.WritePropertyName("size"u8);
                        size.WriteTo(writer);
                    }

                    WriteStringIfPresent(writer, "encoding"u8, p, "encoding"u8);
                    WriteStringIfPresent(writer, "content"u8, p, "content"u8);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            });
    }

    private static void WriteStringIfPresent(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement source, ReadOnlySpan<byte> sourceName)
    {
        if (source.TryGetProperty(sourceName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    private static GetGitHubStatusResult Disconnected(JsonWorkspace workspace)
        => GetGitHubStatusResult.Ok(new((ref Models.GitHubStatus.Builder b) => b.Create(connected: false)), workspace);

    private static Models.ProblemDetails.Source NotBrokeredProblem()
        => Problem("github-not-brokered", "GitHub not brokered", 400, "This deployment brokers no GitHub App.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    // The token-custody key (§4.7): the deployment-stamped identity dimensions first (stable across
    // sessions), composed with the authenticated subject; null when neither resolves — a GitHub
    // session cannot bind without a principal to bind to.
    private string? PrincipalKey()
    {
        ClaimsPrincipal? principal = this.access.CurrentPrincipal ?? this.httpContext?.HttpContext?.User;
        string? subject = principal?.FindFirst(this.subjectClaimType)?.Value ?? principal?.Identity?.Name;
        IReadOnlyList<SecurityTag> tags = this.access.InternalTags();
        if (tags.Count == 0 && string.IsNullOrEmpty(subject))
        {
            return null;
        }

        var key = new System.Text.StringBuilder();
        foreach (SecurityTag tag in tags)
        {
            key.Append(tag.Key).Append('=').Append(tag.Value).Append(';');
        }

        if (!string.IsNullOrEmpty(subject))
        {
            key.Append("sub=").Append(subject);
        }

        return key.ToString();
    }
}