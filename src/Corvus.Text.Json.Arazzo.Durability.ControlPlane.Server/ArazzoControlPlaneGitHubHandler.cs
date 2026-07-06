// <copyright file="ArazzoControlPlaneGitHubHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

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
    private readonly IWorkspaceWorkflowStore? workspaceStore;
    private readonly ISourceStore? sources;
    private readonly string actor;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneGitHubHandler"/> class.</summary>
    /// <param name="broker">The deployment's GitHub broker; <see langword="null"/> refuses every operation (fails closed).</param>
    /// <param name="access">Resolves the caller's identity (the token-custody key).</param>
    /// <param name="httpContext">Reads the authenticated principal in the modes whose access binding carries none (ScopesOnly).</param>
    /// <param name="subjectClaimType">The claim naming the authenticated subject (the custody key's fallback dimension).</param>
    /// <param name="workspaceStore">The working-copy store the Git round-trip (§4.7 pull/commit) reads and saves.</param>
    /// <param name="sources">The source registry (resolves registry attachments at commit).</param>
    /// <param name="actor">The audit actor recorded on pull saves.</param>
    /// <param name="timeProvider">The clock (attachment audit stamps).</param>
    internal ArazzoControlPlaneGitHubHandler(
        GitHubBroker? broker,
        ControlPlaneAccess access,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext = null,
        string subjectClaimType = "sub",
        IWorkspaceWorkflowStore? workspaceStore = null,
        ISourceStore? sources = null,
        string actor = "control-plane",
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(subjectClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        this.broker = broker;
        this.access = access;
        this.httpContext = httpContext;
        this.subjectClaimType = subjectClaimType;
        this.workspaceStore = workspaceStore;
        this.sources = sources;
        this.actor = actor;
        this.timeProvider = timeProvider ?? TimeProvider.System;
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

    /// <inheritdoc/>
    public async ValueTask<ListRepoBranchesResult> HandleListRepoBranchesAsync(ListRepoBranchesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return ListRepoBranchesResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        string owner = (string)parameters.Owner;
        string repo = (string)parameters.Repo;
        if (this.PrincipalKey() is not { } principal || !github.IsConnected(principal))
        {
            return ListRepoBranchesResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The caller has no GitHub session; begin the sign-in first."), workspace);
        }

        (GitHubBroker.ReadOutcome outcome, string? defaultBranch, ParsedJsonDocument<JsonElement>? payload) = await github.ListBranchesAsync(principal, owner, repo, cancellationToken).ConfigureAwait(false);
        if (outcome == GitHubBroker.ReadOutcome.NotConnected)
        {
            payload?.Dispose();
            return ListRepoBranchesResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The GitHub session is no longer valid; begin the sign-in again."), workspace);
        }

        if (outcome != GitHubBroker.ReadOutcome.Success || payload is null)
        {
            payload?.Dispose();
            return ListRepoBranchesResult.NotFound(
                Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}' does not exist, or is outside the user ∩ installation intersection."), workspace);
        }

        using (payload)
        {
            ParsedJsonDocument<Models.GitHubBranchList> body = WriteBranchList(defaultBranch, payload.RootElement);
            workspace.TakeOwnership(body);
            return ListRepoBranchesResult.Ok(Models.GitHubBranchList.From(body.RootElement), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ListRepoCommitsResult> HandleListRepoCommitsAsync(ListRepoCommitsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return ListRepoCommitsResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        string owner = (string)parameters.Owner;
        string repo = (string)parameters.Repo;
        if (this.PrincipalKey() is not { } principal || !github.IsConnected(principal))
        {
            return ListRepoCommitsResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The caller has no GitHub session; begin the sign-in first."), workspace);
        }

        string? sha = parameters.Sha.IsNotUndefined() ? (string)parameters.Sha : null;
        string? path = parameters.Path.IsNotUndefined() ? (string)parameters.Path : null;
        int page = parameters.Page.IsNotUndefined() ? Math.Max(1, (int)parameters.Page) : 1;
        int perPage = parameters.PerPage.IsNotUndefined() ? Math.Clamp((int)parameters.PerPage, 1, 100) : 30;
        (GitHubBroker.ReadOutcome outcome, ParsedJsonDocument<JsonElement>? payload) = await github.ListCommitsAsync(principal, owner, repo, sha, path, page, perPage, cancellationToken).ConfigureAwait(false);
        if (outcome == GitHubBroker.ReadOutcome.NotConnected)
        {
            payload?.Dispose();
            return ListRepoCommitsResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The GitHub session is no longer valid; begin the sign-in again."), workspace);
        }

        if (outcome != GitHubBroker.ReadOutcome.Success || payload is null)
        {
            payload?.Dispose();
            return ListRepoCommitsResult.NotFound(
                Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}' does not exist, or is outside the user ∩ installation intersection."), workspace);
        }

        using (payload)
        {
            ParsedJsonDocument<Models.GitHubCommitList> body = WriteCommitList(payload.RootElement, perPage);
            workspace.TakeOwnership(body);
            return ListRepoCommitsResult.Ok(Models.GitHubCommitList.From(body.RootElement), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CreateRepoBranchResult> HandleCreateRepoBranchAsync(CreateRepoBranchParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } github)
        {
            return CreateRepoBranchResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        string owner = (string)parameters.Owner;
        string repo = (string)parameters.Repo;
        if (this.PrincipalKey() is not { } principal || !github.IsConnected(principal))
        {
            return CreateRepoBranchResult.Conflict(
                Problem("github-not-connected", "GitHub not connected", 409, "The caller has no GitHub session; begin the sign-in first."), workspace);
        }

        string name = (string)parameters.Body.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return CreateRepoBranchResult.BadRequest(
                Problem("github-branch-invalid", "Invalid branch name", 400, "Provide a branch name."), workspace);
        }

        string? from = parameters.Body.FromValue.IsNotUndefined() ? (string)parameters.Body.FromValue : null;
        (GitHubBroker.WriteOutcome outcome, string? sha) = await github.CreateBranchAsync(principal, owner, repo, name, from, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case GitHubBroker.WriteOutcome.NotConnected:
                return CreateRepoBranchResult.Conflict(
                    Problem("github-not-connected", "GitHub not connected", 409, "The GitHub session is no longer valid; begin the sign-in again."), workspace);
            case GitHubBroker.WriteOutcome.NotFound:
                return CreateRepoBranchResult.NotFound(
                    Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}'{(from is null ? string.Empty : $" or its branch '{from}'")} does not exist, or is outside the user ∩ installation intersection."), workspace);
            case GitHubBroker.WriteOutcome.Refused:
                return CreateRepoBranchResult.Conflict(
                    Problem("github-branch-exists", "Branch not created", 409, $"GitHub refused creating '{name}' — most often the name is already taken."), workspace);
        }

        ParsedJsonDocument<Models.GitHubBranch> created = PersistedJson.ToPooledDocument<Models.GitHubBranch, (string Name, string? Sha)>(
            (name, sha),
            static (Utf8JsonWriter writer, in (string Name, string? Sha) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString("name"u8, s.Name);
                if (s.Sha is not null)
                {
                    writer.WriteString("sha"u8, s.Sha);
                }

                writer.WriteEndObject();
            });
        workspace.TakeOwnership(created);
        return CreateRepoBranchResult.Created(Models.GitHubBranch.From(created.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PullWorkingCopyResult> HandlePullWorkingCopyAsync(PullWorkingCopyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        if (this.broker is not { } github || this.workspaceStore is not { } store)
        {
            return PullWorkingCopyResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        if (this.PrincipalKey() is not { } principal)
        {
            return PullWorkingCopyResult.BadRequest(
                Problem("github-identity-unresolvable", "Identity unresolvable", 400, "A GitHub session binds to the calling principal, but no stable principal identity resolves for this caller."), workspace);
        }

        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return PullWorkingCopyResult.NotFound(
                Problem("working-copy-not-found", "Working copy not found", 404, $"No working copy '{id}' exists, or it is outside your reach."), workspace);
        }

        JsonElement root = (JsonElement)w.RootElement;
        if (!root.TryGetProperty("gitBinding"u8, out JsonElement binding) || binding.ValueKind != JsonValueKind.Object)
        {
            return PullWorkingCopyResult.BadRequest(NotBoundProblem(id), workspace);
        }

        (string owner, string repo, string branch, string path) = ReadBinding(binding);

        // An explicit ref makes the pull the git-history ROLLBACK: every bound file is fetched at
        // that commit instead of the branch head. The binding itself is unchanged — the next
        // commit still writes to the bound branch, recording the rollback as a new commit.
        string reference = parameters.Body.Ref.IsNotUndefined() && (string)parameters.Body.Ref is { Length: > 0 } requestedRef ? requestedRef : branch;

        // Fetch everything FIRST — a pull applies fully or not at all. Every pooled payload stays
        // alive until the one draft write below completes.
        (GitHubBroker.ReadOutcome documentOutcome, byte[]? documentBytes) = await this.FetchFileAsync(github, principal, owner, repo, path, reference, cancellationToken).ConfigureAwait(false);
        if (MapPullFetch(documentOutcome, owner, repo, path, workspace) is { } documentRefusal)
        {
            return documentRefusal;
        }

        var specs = new List<(string Name, ParsedJsonDocument<JsonElement> Document)>();
        var scenarioDocs = new List<ParsedJsonDocument<JsonElement>>();
        try
        {
            using var pulledDocument = ParsedJsonDocument<JsonElement>.Parse(documentBytes!);
            if (binding.TryGetProperty("specPaths"u8, out JsonElement specPaths) && specPaths.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> spec in specPaths.EnumerateObject())
                {
                    string specName;
                    using (UnescapedUtf8JsonString name = spec.Utf8NameSpan)
                    {
                        specName = System.Text.Encoding.UTF8.GetString(name.Span);
                    }

                    string specPath = spec.Value.GetString() ?? string.Empty;
                    (GitHubBroker.ReadOutcome outcome, byte[]? bytes) = await this.FetchFileAsync(github, principal, owner, repo, specPath, reference, cancellationToken).ConfigureAwait(false);
                    if (MapPullFetch(outcome, owner, repo, specPath, workspace) is { } specRefusal)
                    {
                        return specRefusal;
                    }

                    specs.Add((specName, ParsedJsonDocument<JsonElement>.Parse(bytes!)));
                }
            }

            bool scenariosBound = binding.TryGetProperty("scenariosDir"u8, out JsonElement scenariosDir) && scenariosDir.GetString() is { Length: > 0 };
            if (scenariosBound)
            {
                string dir = scenariosDir.GetString()!.TrimEnd('/');
                (GitHubBroker.ReadOutcome outcome, ParsedJsonDocument<JsonElement>? listing) = await github.BrowseAsync(principal, owner, repo, dir, reference, cancellationToken).ConfigureAwait(false);
                using ParsedJsonDocument<JsonElement>? heldListing = listing;
                if (MapPullFetch(outcome, owner, repo, dir, workspace) is { } dirRefusal)
                {
                    return dirRefusal;
                }

                if (listing!.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in listing.RootElement.EnumerateArray())
                    {
                        if (entry.TryGetProperty("type"u8, out JsonElement type) && type.ValueEquals("file")
                            && entry.TryGetProperty("name"u8, out JsonElement fileName) && fileName.GetString() is { } n
                            && n.EndsWith(".scenario.json", StringComparison.OrdinalIgnoreCase))
                        {
                            (GitHubBroker.ReadOutcome fileOutcome, byte[]? bytes) = await this.FetchFileAsync(github, principal, owner, repo, $"{dir}/{n}", reference, cancellationToken).ConfigureAwait(false);
                            if (MapPullFetch(fileOutcome, owner, repo, $"{dir}/{n}", workspace) is { } fileRefusal)
                            {
                                return fileRefusal;
                            }

                            scenarioDocs.Add(ParsedJsonDocument<JsonElement>.Parse(bytes!));
                        }
                    }
                }
            }

            // One etag-guarded save: the pulled document, the replacement inline attachment set (only
            // when specs are bound), and the replacement scenario set (only when the directory is bound).
            using ParsedJsonDocument<JsonElement>? sourcesDoc = specs.Count > 0 ? this.WriteInlineAttachments(specs) : null;
            using ParsedJsonDocument<JsonElement>? scenariosDoc = scenariosBound ? WriteScenarioSet(scenarioDocs) : null;
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft(
                default,
                default,
                default,
                pulledDocument.RootElement,
                default,
                sourcesDoc is { } s ? s.RootElement : default,
                SecurityTagSet.Empty,
                scenarios: scenariosDoc is { } sc ? sc.RootElement : default);
            ParsedJsonDocument<WorkspaceWorkflow>? saved = await store.UpdateAsync(id, draft.RootElement, new WorkflowEtag((string)parameters.Body.ExpectedEtag), this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (saved is not { } updated)
            {
                return PullWorkingCopyResult.NotFound(
                    Problem("working-copy-not-found", "Working copy not found", 404, $"No working copy '{id}' exists, or it is outside your reach."), workspace);
            }

            workspace.TakeOwnership(updated);
            return PullWorkingCopyResult.Ok(Models.WorkingCopy.From(updated.RootElement), workspace);
        }
        catch (WorkspaceWorkflowConflictException ex)
        {
            return PullWorkingCopyResult.Conflict(
                Problem("save-conflict", "Save conflict", 409, ex.Message + " Re-fetch and pull against the fresh state."), workspace);
        }
        catch (JsonException)
        {
            return PullWorkingCopyResult.BadRequest(
                Problem("github-content-invalid", "Content invalid", 400, $"A bound file in '{owner}/{repo}@{reference}' is not valid JSON; fix it in the repository and pull again."), workspace);
        }
        finally
        {
            foreach ((string _, ParsedJsonDocument<JsonElement> doc) in specs)
            {
                doc.Dispose();
            }

            foreach (ParsedJsonDocument<JsonElement> doc in scenarioDocs)
            {
                doc.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CommitWorkingCopyResult> HandleCommitWorkingCopyAsync(CommitWorkingCopyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        if (this.broker is not { } github || this.workspaceStore is not { } store)
        {
            return CommitWorkingCopyResult.BadRequest(NotBrokeredProblem(), workspace);
        }

        if (this.PrincipalKey() is not { } principal)
        {
            return CommitWorkingCopyResult.BadRequest(
                Problem("github-identity-unresolvable", "Identity unresolvable", 400, "A GitHub session binds to the calling principal, but no stable principal identity resolves for this caller."), workspace);
        }

        if (!parameters.Body.Message.IsNotUndefined() || ((string)parameters.Body.Message).Length == 0)
        {
            return CommitWorkingCopyResult.BadRequest(
                Problem("invalid-commit", "Invalid commit", 400, "A commit requires a 'message'."), workspace);
        }

        string message = (string)parameters.Body.Message;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return CommitWorkingCopyResult.NotFound(
                Problem("working-copy-not-found", "Working copy not found", 404, $"No working copy '{id}' exists, or it is outside your reach."), workspace);
        }

        JsonElement root = (JsonElement)w.RootElement;
        if (!root.TryGetProperty("gitBinding"u8, out JsonElement binding) || binding.ValueKind != JsonValueKind.Object)
        {
            return CommitWorkingCopyResult.BadRequest(NotBoundProblem(id), workspace);
        }

        (string owner, string repo, string branch, string path) = ReadBinding(binding);

        // The files, in deterministic order: the document, the bound specs, the scenario files.
        List<(string Path, byte[] Bytes)>? files = await this.CollectCommitFilesAsync(root, binding, path, cancellationToken).ConfigureAwait(false);
        if (files is null)
        {
            return CommitWorkingCopyResult.BadRequest(
                Problem("github-spec-unresolvable", "Spec unresolvable", 400, "A bound specPaths entry names no attached (or resolvable registry) source on this working copy."), workspace);
        }

        // Contents PUTs, each read-sha-then-write, authored as the signed-in user (§4.7): the broker
        // composes no author/committer — GitHub stamps the token's user.
        var written = new List<(string Path, string? Sha)>();
        foreach ((string filePath, byte[] bytes) in files)
        {
            (GitHubBroker.ReadOutcome shaOutcome, ParsedJsonDocument<JsonElement>? current) = await github.BrowseAsync(principal, owner, repo, filePath, branch, cancellationToken).ConfigureAwait(false);
            string? existingSha = null;
            if (shaOutcome == GitHubBroker.ReadOutcome.NotConnected)
            {
                current?.Dispose();
                return CommitWorkingCopyResult.Conflict(NotConnectedProblem(), workspace);
            }

            if (shaOutcome == GitHubBroker.ReadOutcome.Success && current is not null)
            {
                using (current)
                {
                    if (current.RootElement.ValueKind == JsonValueKind.Object && current.RootElement.TryGetProperty("sha"u8, out JsonElement sha))
                    {
                        existingSha = sha.GetString();
                    }
                }
            }

            (GitHubBroker.WriteOutcome outcome, string? contentSha) = await github.PutContentAsync(principal, owner, repo, filePath, branch, message, bytes, existingSha, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case GitHubBroker.WriteOutcome.NotConnected:
                    return CommitWorkingCopyResult.Conflict(NotConnectedProblem(), workspace);
                case GitHubBroker.WriteOutcome.NotFound:
                    return CommitWorkingCopyResult.NotFound(
                        Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}@{branch}' is not reachable through the user ∩ installation intersection."), workspace);
                case GitHubBroker.WriteOutcome.Refused:
                    return CommitWorkingCopyResult.Conflict(
                        Problem("github-commit-refused", "Commit refused", 409, $"GitHub refused writing '{filePath}' (a concurrent change on '{branch}'?). Pull, reconcile, and commit again."), workspace);
                default:
                    written.Add((filePath, contentSha));
                    break;
            }
        }

        // Optionally open the pull request FROM the bound branch (draft → the review flow).
        (long Number, string Url)? pullRequest = null;
        if (parameters.Body.PullRequest.IsNotUndefined())
        {
            var pr = (JsonElement)parameters.Body.PullRequest;
            string baseBranch = pr.TryGetProperty("base"u8, out JsonElement b) ? b.GetString() ?? string.Empty : string.Empty;
            string title = pr.TryGetProperty("title"u8, out JsonElement t) && t.GetString() is { Length: > 0 } declared ? declared : message;
            bool draft = pr.TryGetProperty("draft"u8, out JsonElement d) && d.ValueKind == JsonValueKind.True;
            (GitHubBroker.WriteOutcome outcome, long number, string? url) = await github.CreatePullRequestAsync(principal, owner, repo, branch, baseBranch, title, draft, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case GitHubBroker.WriteOutcome.NotConnected:
                    return CommitWorkingCopyResult.Conflict(NotConnectedProblem(), workspace);
                case GitHubBroker.WriteOutcome.NotFound:
                    return CommitWorkingCopyResult.NotFound(
                        Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}' is not reachable through the user ∩ installation intersection."), workspace);
                case GitHubBroker.WriteOutcome.Refused:
                    return CommitWorkingCopyResult.Conflict(
                        Problem("github-pr-refused", "Pull request refused", 409, $"GitHub refused the pull request from '{branch}' onto '{baseBranch}' — one may already exist for this branch."), workspace);
                default:
                    pullRequest = (number, url ?? string.Empty);
                    break;
            }
        }

        ParsedJsonDocument<Models.GitCommitResult> body = WriteCommitResult(written, pullRequest);
        workspace.TakeOwnership(body);
        return CommitWorkingCopyResult.Ok(Models.GitCommitResult.From(body.RootElement), workspace);
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
    private static ParsedJsonDocument<Models.GitHubBranchList> WriteBranchList(string? defaultBranch, in JsonElement payload)
    {
        return PersistedJson.ToPooledDocument<Models.GitHubBranchList, (string? DefaultBranch, JsonElement Branches)>(
            (defaultBranch, payload),
            static (Utf8JsonWriter writer, in (string? DefaultBranch, JsonElement Branches) s) =>
            {
                writer.WriteStartObject();
                if (s.DefaultBranch is not null)
                {
                    writer.WriteString("defaultBranch"u8, s.DefaultBranch);
                }

                writer.WriteStartArray("branches"u8);
                if (s.Branches.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement branch in s.Branches.EnumerateArray())
                    {
                        if (!branch.TryGetProperty("name"u8, out JsonElement name))
                        {
                            continue;
                        }

                        writer.WriteStartObject();
                        writer.WritePropertyName("name"u8);
                        name.WriteTo(writer);
                        if (branch.TryGetProperty("commit"u8, out JsonElement commit) && commit.TryGetProperty("sha"u8, out JsonElement sha))
                        {
                            writer.WritePropertyName("sha"u8);
                            sha.WriteTo(writer);
                        }

                        if (branch.TryGetProperty("protected"u8, out JsonElement prot) && (prot.ValueKind == JsonValueKind.True || prot.ValueKind == JsonValueKind.False))
                        {
                            writer.WritePropertyName("protected"u8);
                            prot.WriteTo(writer);
                        }

                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    /// <summary>Projects GitHub's commits array to the contract's <c>GitHubCommitList</c>: sha, the
    /// message's first line, the author's display name, and the author date. A full page implies
    /// another may follow (GitHub's Link header is not surfaced through the pooled read).</summary>
    private static ParsedJsonDocument<Models.GitHubCommitList> WriteCommitList(in JsonElement payload, int perPage)
    {
        return PersistedJson.ToPooledDocument<Models.GitHubCommitList, (JsonElement Commits, int PerPage)>(
            (payload, perPage),
            static (Utf8JsonWriter writer, in (JsonElement Commits, int PerPage) s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("commits"u8);
                int count = 0;
                if (s.Commits.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement commit in s.Commits.EnumerateArray())
                    {
                        if (!commit.TryGetProperty("sha"u8, out JsonElement sha))
                        {
                            continue;
                        }

                        count++;
                        writer.WriteStartObject();
                        writer.WritePropertyName("sha"u8);
                        sha.WriteTo(writer);
                        if (commit.TryGetProperty("commit"u8, out JsonElement detail) && detail.ValueKind == JsonValueKind.Object)
                        {
                            if (detail.TryGetProperty("message"u8, out JsonElement message) && message.GetString() is { } m)
                            {
                                int newline = m.IndexOf('\n', StringComparison.Ordinal);
                                writer.WriteString("message"u8, newline < 0 ? m : m[..newline]);
                            }

                            if (detail.TryGetProperty("author"u8, out JsonElement author) && author.ValueKind == JsonValueKind.Object)
                            {
                                if (author.TryGetProperty("name"u8, out JsonElement name) && name.ValueKind == JsonValueKind.String)
                                {
                                    writer.WritePropertyName("author"u8);
                                    name.WriteTo(writer);
                                }

                                if (author.TryGetProperty("date"u8, out JsonElement date) && date.ValueKind == JsonValueKind.String)
                                {
                                    writer.WritePropertyName("date"u8);
                                    date.WriteTo(writer);
                                }
                            }
                        }

                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndArray();
                writer.WriteBoolean("hasMore"u8, count >= s.PerPage);
                writer.WriteEndObject();
            });
    }

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

    // The binding's coordinates (validated present by the contract's required set).
    private static (string Owner, string Repo, string Branch, string Path) ReadBinding(in JsonElement binding)
        => (binding.TryGetProperty("owner"u8, out JsonElement o) ? o.GetString() ?? string.Empty : string.Empty,
            binding.TryGetProperty("repo"u8, out JsonElement r) ? r.GetString() ?? string.Empty : string.Empty,
            binding.TryGetProperty("branch"u8, out JsonElement b) ? b.GetString() ?? string.Empty : string.Empty,
            binding.TryGetProperty("path"u8, out JsonElement p) ? p.GetString() ?? string.Empty : string.Empty);

    // Maps a pull-phase fetch outcome to its refusal (null = success, keep going). Nothing is
    // partially applied: any missing bound file refuses the whole pull.
    private static PullWorkingCopyResult? MapPullFetch(GitHubBroker.ReadOutcome outcome, string owner, string repo, string path, JsonWorkspace workspace)
        => outcome switch
        {
            GitHubBroker.ReadOutcome.Success => null,
            GitHubBroker.ReadOutcome.NotConnected => PullWorkingCopyResult.Conflict(NotConnectedProblem(), workspace),
            _ => PullWorkingCopyResult.NotFound(
                Problem("github-content-not-found", "Not found", 404, $"'{owner}/{repo}/{path}' does not exist on the bound branch, or is outside the user ∩ installation intersection."), workspace),
        };

    // One file's content through the contents proxy (base64-decoded); NotFound when the path is a
    // directory where a file was expected.
    private async ValueTask<(GitHubBroker.ReadOutcome Outcome, byte[]? Bytes)> FetchFileAsync(GitHubBroker github, string principal, string owner, string repo, string path, string branch, CancellationToken cancellationToken)
    {
        (GitHubBroker.ReadOutcome outcome, ParsedJsonDocument<JsonElement>? payload) = await github.BrowseAsync(principal, owner, repo, path, branch, cancellationToken).ConfigureAwait(false);
        if (outcome != GitHubBroker.ReadOutcome.Success || payload is null)
        {
            payload?.Dispose();
            return (outcome, null);
        }

        using (payload)
        {
            JsonElement p = payload.RootElement;
            if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("content"u8, out JsonElement content) || content.GetString() is not { } base64)
            {
                return (GitHubBroker.ReadOutcome.NotFound, null);
            }

            return (GitHubBroker.ReadOutcome.Success, Convert.FromBase64String(base64));
        }
    }

    // The replacement inline attachment set a pull writes: one inline entry per bound spec, typed by
    // the document's own top-level field, audit-stamped as the pull.
    private ParsedJsonDocument<JsonElement> WriteInlineAttachments(List<(string Name, ParsedJsonDocument<JsonElement> Document)> specs)
    {
        return PersistedJson.ToPooledDocument<JsonElement, (List<(string Name, ParsedJsonDocument<JsonElement> Document)> Specs, string Actor, DateTimeOffset At)>(
            (specs, this.actor, this.timeProvider.GetUtcNow()),
            static (Utf8JsonWriter writer, in (List<(string Name, ParsedJsonDocument<JsonElement> Document)> Specs, string Actor, DateTimeOffset At) s) =>
            {
                writer.WriteStartArray();
                foreach ((string name, ParsedJsonDocument<JsonElement> document) in s.Specs)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name"u8, name);
                    writer.WriteString("kind"u8, "inline"u8);
                    if (DetectType(document.RootElement) is { } type)
                    {
                        writer.WriteString("type"u8, type);
                    }

                    writer.WritePropertyName("document"u8);
                    document.RootElement.WriteTo(writer);
                    writer.WriteString("attachedBy"u8, s.Actor);
                    writer.WriteString("attachedAt"u8, s.At);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            });
    }

    // The replacement scenario set a pull writes (the <name>.scenario.json values, in listing order).
    private static ParsedJsonDocument<JsonElement> WriteScenarioSet(List<ParsedJsonDocument<JsonElement>> scenarios)
    {
        return PersistedJson.ToPooledDocument<JsonElement, List<ParsedJsonDocument<JsonElement>>>(
            scenarios,
            static (Utf8JsonWriter writer, in List<ParsedJsonDocument<JsonElement>> all) =>
            {
                writer.WriteStartArray();
                foreach (ParsedJsonDocument<JsonElement> scenario in all)
                {
                    scenario.RootElement.WriteTo(writer);
                }

                writer.WriteEndArray();
            });
    }

    // The files a commit writes, in deterministic order: the document, each bound spec (inline
    // attachments, or registry attachments re-resolved), then one <name>.scenario.json per scenario.
    // Null when a bound spec cannot be resolved (the commit refuses rather than writing a subset).
    private async ValueTask<List<(string Path, byte[] Bytes)>?> CollectCommitFilesAsync(JsonElement root, JsonElement binding, string documentPath, CancellationToken cancellationToken)
    {
        var files = new List<(string Path, byte[] Bytes)>();
        root.TryGetProperty("document"u8, out JsonElement document);
        files.Add((documentPath, PersistedJson.ToArray(document, static (Utf8JsonWriter writer, in JsonElement d) => d.WriteTo(writer))));

        if (binding.TryGetProperty("specPaths"u8, out JsonElement specPaths) && specPaths.ValueKind == JsonValueKind.Object)
        {
            root.TryGetProperty("sources"u8, out JsonElement attachments);
            foreach (JsonProperty<JsonElement> spec in specPaths.EnumerateObject())
            {
                string specName;
                using (UnescapedUtf8JsonString name = spec.Utf8NameSpan)
                {
                    specName = System.Text.Encoding.UTF8.GetString(name.Span);
                }

                string specPath = spec.Value.GetString() ?? string.Empty;
                JsonElement attachment = WorkspaceSourceJson.FindAttachment(attachments, specName);
                if (attachment.TryGetProperty("document"u8, out JsonElement inline) && inline.ValueKind == JsonValueKind.Object)
                {
                    files.Add((specPath, PersistedJson.ToArray(inline, static (Utf8JsonWriter writer, in JsonElement d) => d.WriteTo(writer))));
                    continue;
                }

                if (this.sources is { } registry
                    && attachment.TryGetProperty("sourceName"u8, out JsonElement sn) && sn.GetString() is { Length: > 0 } registryName)
                {
                    using ParsedJsonDocument<RegisteredSource>? registered = await registry.GetAsync(registryName, this.access.Current(), cancellationToken).ConfigureAwait(false);
                    if (registered is { } resolved)
                    {
                        var registeredDocument = (JsonElement)resolved.RootElement.Document;
                        files.Add((specPath, PersistedJson.ToArray(registeredDocument, static (Utf8JsonWriter writer, in JsonElement d) => d.WriteTo(writer))));
                        continue;
                    }
                }

                return null;
            }
        }

        if (binding.TryGetProperty("scenariosDir"u8, out JsonElement scenariosDir) && scenariosDir.GetString() is { Length: > 0 } dir
            && root.TryGetProperty("scenarios"u8, out JsonElement scenarios) && scenarios.ValueKind == JsonValueKind.Array)
        {
            string trimmed = dir.TrimEnd('/');
            foreach (JsonElement scenario in scenarios.EnumerateArray())
            {
                if (scenario.TryGetProperty("name"u8, out JsonElement n) && n.GetString() is { Length: > 0 } scenarioName)
                {
                    files.Add(($"{trimmed}/{scenarioName}.scenario.json", PersistedJson.ToArray(scenario, static (Utf8JsonWriter writer, in JsonElement s) => s.WriteTo(writer))));
                }
            }
        }

        return files;
    }

    // The commit response: what was written, plus the pull request when one was opened.
    private static ParsedJsonDocument<Models.GitCommitResult> WriteCommitResult(List<(string Path, string? Sha)> written, (long Number, string Url)? pullRequest)
    {
        return PersistedJson.ToPooledDocument<Models.GitCommitResult, (List<(string Path, string? Sha)> Files, (long Number, string Url)? Pr)>(
            (written, pullRequest),
            static (Utf8JsonWriter writer, in (List<(string Path, string? Sha)> Files, (long Number, string Url)? Pr) s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("files"u8);
                foreach ((string path, string? sha) in s.Files)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path"u8, path);
                    if (sha is not null)
                    {
                        writer.WriteString("sha"u8, sha);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                if (s.Pr is { } pr)
                {
                    writer.WriteStartObject("pullRequest"u8);
                    writer.WriteNumber("number"u8, pr.Number);
                    writer.WriteString("url"u8, pr.Url);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            });
    }

    private static string? DetectType(in JsonElement document)
        => document.TryGetProperty("openapi"u8, out _) ? "openapi"
        : document.TryGetProperty("asyncapi"u8, out _) ? "asyncapi"
        : document.TryGetProperty("arazzo"u8, out _) ? "arazzo"
        : null;

    private static Models.ProblemDetails.Source NotBoundProblem(string id)
        => Problem("github-not-bound", "Not Git-bound", 400, $"Working copy '{id}' has no gitBinding; save one first (§4.7).");

    private static Models.ProblemDetails.Source NotConnectedProblem()
        => Problem("github-not-connected", "GitHub not connected", 409, "The caller has no GitHub session (or it is no longer valid); begin the sign-in first.");

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