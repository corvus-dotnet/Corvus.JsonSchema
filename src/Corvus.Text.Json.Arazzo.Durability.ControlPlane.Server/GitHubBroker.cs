// <copyright file="GitHubBroker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The GitHub API surface of the designer's Git round-trip (workflow-designer design §4.7):
/// proxied contents reads, commits, branches, and pull requests, every one running AS the
/// signed-in user (their GitHub-held git identity; reach is whatever the signed-in user can see,
/// the OAuth model — so a source document anywhere the user can read is reachable). The
/// authorize/callback/custody machinery is the shared connected-provider broker (ADR 0052):
/// GitHub is provider #1 (<see cref="GitHubBrokerOptions.ToProviderEntry"/>) under
/// <see cref="ProviderBroker"/>, so one GitHub sign-in serves the Git panel and the source-fetch
/// pane alike, and the kit never sees a GitHub credential.
/// </summary>
/// <remarks>
/// <para><strong>Enterprise.</strong> GitHub Enterprise Server is configuration
/// (<see cref="GitHubBrokerOptions.BaseUrl"/>/<see cref="GitHubBrokerOptions.ApiBaseUrl"/>), not
/// new design.</para>
/// </remarks>
public sealed class GitHubBroker
{
    // The folded provider entry's name (ADR 0052): the shared broker's custody/state rows for
    // GitHub key on this, whichever surface (Git panel or fetch pane) began the sign-in.
    private const string ProviderName = "github";

    private readonly HttpClient client;
    private readonly GitHubBrokerOptions options;
    private readonly ProviderBroker providers;

    /// <summary>Initializes a new instance of the <see cref="GitHubBroker"/> class.</summary>
    /// <param name="client">The outbound HTTP client (a deployment configures its handler/egress policy).</param>
    /// <param name="options">The App registration and endpoints.</param>
    /// <param name="providers">The shared provider broker; it must carry the folded <c>github</c>
    /// entry (<see cref="GitHubBrokerOptions.ToProviderEntry"/>), which owns the sign-in and custody.</param>
    public GitHubBroker(HttpClient client, GitHubBrokerOptions options, ProviderBroker providers)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providers);
        options.Validate();
        if (!providers.TryGetProvider(ProviderName, out _))
        {
            throw new ArgumentException($"The provider broker carries no '{ProviderName}' entry; register GitHubBrokerOptions.ToProviderEntry() with it.", nameof(providers));
        }

        this.client = client;
        this.options = options;
        this.providers = providers;
    }

    /// <summary>The outcome kinds a proxied GitHub read can report.</summary>
    public enum ReadOutcome
    {
        /// <summary>The read succeeded; the payload document is present.</summary>
        Success,

        /// <summary>The principal has no (valid) GitHub session.</summary>
        NotConnected,

        /// <summary>The resource is not reachable by the signed-in user (non-disclosing).</summary>
        NotFound,
    }

    /// <summary>The outcome kinds a proxied GitHub write can report.</summary>
    public enum WriteOutcome
    {
        /// <summary>The write succeeded.</summary>
        Success,

        /// <summary>The principal has no (valid) GitHub session.</summary>
        NotConnected,

        /// <summary>The target is not reachable by the signed-in user (non-disclosing).</summary>
        NotFound,

        /// <summary>GitHub refused the write (a stale sha, a validation error, or an already-existing pull request).</summary>
        Refused,
    }

    /// <summary>
    /// Begins the sign-in through the shared provider broker's folded <c>github</c> entry: a
    /// single-use, principal-bound state and the authorize URL the kit opens in a popup.
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The authorize URL and the embedded state.</returns>
    public async ValueTask<(string AuthorizeUrl, string State)> BeginAuthAsync(string principalKey, CancellationToken cancellationToken)
    {
        (ProviderBroker.BeginOutcome outcome, string? authorizeUrl, string? state) = await this.providers.BeginAuthAsync(ProviderName, principalKey, cancellationToken).ConfigureAwait(false);

        // The ctor proved the github entry is registered, and it carries explicit endpoints (no
        // discovery), so begin cannot fail here.
        return outcome == ProviderBroker.BeginOutcome.Success
            ? (authorizeUrl!, state!)
            : throw new InvalidOperationException($"Beginning the GitHub sign-in reported {outcome}.");
    }

    /// <summary>
    /// Completes the flow through the shared provider broker: validates and consumes the state
    /// (single-use), exchanges the code server-side (resolving the App client secret only for the
    /// exchange), and stores the user token for the state's principal.
    /// </summary>
    /// <param name="state">The state from the callback query.</param>
    /// <param name="code">The authorization code from the callback query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public ValueTask<ProviderBroker.CompleteOutcome> CompleteAuthAsync(string state, string code, CancellationToken cancellationToken)
        => this.providers.CompleteAuthAsync(ProviderName, state, code, cancellationToken);

    /// <summary>Whether the principal currently holds a token (without touching GitHub).</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <returns><see langword="true"/> if a token is stored.</returns>
    public bool IsConnected(string principalKey) => this.providers.IsConnected(principalKey, ProviderName);

    /// <summary>Drops the principal's token. Idempotent.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    public void Disconnect(string principalKey) => this.providers.Disconnect(principalKey, ProviderName);

    /// <summary>Reads the signed-in user (<c>GET /user</c>) on the principal's token.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the GitHub payload (caller disposes).</returns>
    public ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> GetUserAsync(string principalKey, CancellationToken cancellationToken)
        => this.GetAsync(principalKey, "/user", cancellationToken);

    /// <summary>Reads a first page of the repositories the signed-in user can reach (<c>GET /user/repos</c>, most
    /// recently pushed first) — the OAuth model's session listing, a picker seed rather than the full reach: any
    /// visible repository stays addressable directly by owner/repo whether listed here or not.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="pageSize">The page size (GitHub caps at 100).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the GitHub payload (caller disposes).</returns>
    public ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> GetUserRepositoriesAsync(string principalKey, int pageSize, CancellationToken cancellationToken)
        => this.GetAsync(principalKey, $"/user/repos?per_page={pageSize}&sort=pushed", cancellationToken);

    /// <summary>Searches repositories for the pickers' typeahead (<c>GET /search/repositories</c>) on the
    /// principal's token. An owner-qualified query (<c>owner/prefix</c>) scopes to that user or organisation's
    /// repositories by name — including public repositories the session's own listing never contains; a bare
    /// term searches by name across what the user can see.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="query">The typed query (<c>owner/prefix</c> or a bare term).</param>
    /// <param name="pageSize">The maximum matches to return (a pickers-sized page).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the GitHub search payload (caller disposes).</returns>
    public ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> SearchRepositoriesAsync(string principalKey, string query, int pageSize, CancellationToken cancellationToken)
    {
        int slash = query.IndexOf('/');
        string composed = slash < 0
            ? $"{query} in:name"
            : slash + 1 < query.Length
                ? $"{query[(slash + 1)..]} in:name user:{query[..slash]}"
                : $"user:{query[..slash]}";
        return this.GetAsync(principalKey, $"/search/repositories?q={Uri.EscapeDataString(composed)}&per_page={pageSize}", cancellationToken);
    }

    /// <summary>Proxies a contents read (<c>GET /repos/{owner}/{repo}/contents/{path}?ref=</c>) on the principal's token.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="path">The path to browse (empty for the root).</param>
    /// <param name="reference">The branch/tag/commit, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the GitHub payload (caller disposes) — an array for a directory, an object for a file.</returns>
    public ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> BrowseAsync(string principalKey, string owner, string repo, string? path, string? reference, CancellationToken cancellationToken)
    {
        string escapedPath = string.Join('/', (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        string query = string.IsNullOrEmpty(reference) ? string.Empty : $"?ref={Uri.EscapeDataString(reference)}";
        return this.GetAsync(principalKey, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{escapedPath}{query}", cancellationToken);
    }

    /// <summary>
    /// Creates or updates one file on a branch (<c>PUT /repos/{owner}/{repo}/contents/{path}</c>) on the
    /// principal's token. The body carries message/branch/content (plus the current sha for an update) and
    /// — deliberately — <strong>no author/committer</strong>: GitHub stamps the token's user, so the commit
    /// is authored as the signed-in user's GitHub-held git identity (§4.7).
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="path">The file path.</param>
    /// <param name="branch">The branch to write on.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="content">The file content.</param>
    /// <param name="existingSha">The current blob sha (an update), or <see langword="null"/> (a create).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the written blob's sha.</returns>
    public async ValueTask<(WriteOutcome Outcome, string? ContentSha)> PutContentAsync(
        string principalKey, string owner, string repo, string path, string branch, string message, ReadOnlyMemory<byte> content, string? existingSha, CancellationToken cancellationToken)
    {
        string? token = await this.CurrentTokenAsync(principalKey, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return (WriteOutcome.NotConnected, null);
        }

        string escapedPath = string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        byte[] body = PersistedJson.ToArray(
            (Message: message, Branch: branch, Content: content, Sha: existingSha),
            static (Utf8JsonWriter writer, in (string Message, string Branch, ReadOnlyMemory<byte> Content, string? Sha) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString("message"u8, s.Message);
                writer.WriteString("branch"u8, s.Branch);
                writer.WriteString("content"u8, Convert.ToBase64String(s.Content.Span));
                if (s.Sha is not null)
                {
                    writer.WriteString("sha"u8, s.Sha);
                }

                writer.WriteEndObject();
            });

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{this.options.ApiBaseUrl!.TrimEnd('/')}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{escapedPath}")
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } },
        };
        AddGitHubHeaders(request, token);
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            this.providers.Disconnect(principalKey, ProviderName);
            return (WriteOutcome.NotConnected, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (WriteOutcome.NotFound, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (WriteOutcome.Refused, null);
        }

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
        string? sha = document.RootElement.TryGetProperty("content"u8, out JsonElement written) && written.TryGetProperty("sha"u8, out JsonElement shaElement)
            ? shaElement.GetString()
            : null;
        return (WriteOutcome.Success, sha);
    }

    /// <summary>Opens a pull request (<c>POST /repos/{owner}/{repo}/pulls</c>) on the principal's token.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="head">The branch the changes are on (the working copy's bound branch).</param>
    /// <param name="baseBranch">The branch the pull request targets.</param>
    /// <param name="title">The pull request title.</param>
    /// <param name="draft">Open as a draft.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the pull request number and URL.</returns>
    public async ValueTask<(WriteOutcome Outcome, long Number, string? Url)> CreatePullRequestAsync(
        string principalKey, string owner, string repo, string head, string baseBranch, string title, bool draft, CancellationToken cancellationToken)
    {
        string? token = await this.CurrentTokenAsync(principalKey, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return (WriteOutcome.NotConnected, 0, null);
        }

        byte[] body = PersistedJson.ToArray(
            (Title: title, Head: head, Base: baseBranch, Draft: draft),
            static (Utf8JsonWriter writer, in (string Title, string Head, string Base, bool Draft) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString("title"u8, s.Title);
                writer.WriteString("head"u8, s.Head);
                writer.WriteString("base"u8, s.Base);
                writer.WriteBoolean("draft"u8, s.Draft);
                writer.WriteEndObject();
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{this.options.ApiBaseUrl!.TrimEnd('/')}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/pulls")
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } },
        };
        AddGitHubHeaders(request, token);
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            this.providers.Disconnect(principalKey, ProviderName);
            return (WriteOutcome.NotConnected, 0, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (WriteOutcome.NotFound, 0, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (WriteOutcome.Refused, 0, null);
        }

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
        long number = document.RootElement.TryGetProperty("number"u8, out JsonElement n) && n.ValueKind == JsonValueKind.Number ? n.GetInt64() : 0;
        string? url = document.RootElement.TryGetProperty("html_url"u8, out JsonElement u) ? u.GetString() : null;
        return (WriteOutcome.Success, number, url);
    }

    /// <summary>
    /// Lists a repository's branches plus its default branch (<c>GET /repos/{owner}/{repo}</c> +
    /// <c>GET /repos/{owner}/{repo}/branches</c>) on the principal's token — the Git dialog's branch picker.
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome, the default branch, and on success the branches payload (caller disposes).</returns>
    public async ValueTask<(ReadOutcome Outcome, string? DefaultBranch, ParsedJsonDocument<JsonElement>? Branches)> ListBranchesAsync(string principalKey, string owner, string repo, CancellationToken cancellationToken)
    {
        (ReadOutcome repoOutcome, ParsedJsonDocument<JsonElement>? repoPayload) = await this.GetAsync(
            principalKey, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}", cancellationToken).ConfigureAwait(false);
        if (repoOutcome != ReadOutcome.Success || repoPayload is not { } repoDoc)
        {
            return (repoOutcome, null, null);
        }

        string? defaultBranch;
        using (repoDoc)
        {
            defaultBranch = repoDoc.RootElement.TryGetProperty("default_branch"u8, out JsonElement d) ? d.GetString() : null;
        }

        (ReadOutcome outcome, ParsedJsonDocument<JsonElement>? branches) = await this.GetAsync(
            principalKey, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/branches?per_page=100", cancellationToken).ConfigureAwait(false);
        return outcome != ReadOutcome.Success ? (outcome, null, null) : (ReadOutcome.Success, defaultBranch, branches);
    }

    /// <summary>
    /// Lists one page of a repository's commit history (<c>GET /repos/{owner}/{repo}/commits</c>) on the
    /// principal's token, newest first — the Git pane's history browser. Scope with <paramref name="sha"/>
    /// (the bound branch) and <paramref name="path"/> (the bound document) to see the commits that touched
    /// the working copy.
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="sha">The branch/tag/commit to start from, if any.</param>
    /// <param name="path">Only commits touching this path, if any.</param>
    /// <param name="page">The 1-based page.</param>
    /// <param name="perPage">The page size.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the GitHub commits array (caller disposes).</returns>
    public ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> ListCommitsAsync(
        string principalKey, string owner, string repo, string? sha, string? path, int page, int perPage, CancellationToken cancellationToken)
    {
        var query = new System.Text.StringBuilder("?per_page=").Append(perPage).Append("&page=").Append(page);
        if (!string.IsNullOrEmpty(sha))
        {
            query.Append("&sha=").Append(Uri.EscapeDataString(sha));
        }

        if (!string.IsNullOrEmpty(path))
        {
            query.Append("&path=").Append(Uri.EscapeDataString(path));
        }

        return this.GetAsync(principalKey, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/commits{query}", cancellationToken);
    }

    /// <summary>
    /// Creates a branch from a base branch's head (<c>POST /repos/{owner}/{repo}/git/refs</c>) on the
    /// principal's token. Creating a ref makes no commit and composes no identity (§4.7).
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="name">The new branch name (without <c>refs/heads/</c>).</param>
    /// <param name="from">The base branch, or <see langword="null"/> for the repository's default branch.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome (Refused = the name is taken) and, on success, the new head sha.</returns>
    public async ValueTask<(WriteOutcome Outcome, string? Sha)> CreateBranchAsync(string principalKey, string owner, string repo, string name, string? from, CancellationToken cancellationToken)
    {
        string? baseBranch = from;
        if (string.IsNullOrEmpty(baseBranch))
        {
            (ReadOutcome repoOutcome, string? defaultBranch, ParsedJsonDocument<JsonElement>? branchesDoc) = await this.ListBranchesAsync(principalKey, owner, repo, cancellationToken).ConfigureAwait(false);
            branchesDoc?.Dispose();
            if (repoOutcome != ReadOutcome.Success || defaultBranch is null)
            {
                return (repoOutcome == ReadOutcome.NotConnected ? WriteOutcome.NotConnected : WriteOutcome.NotFound, null);
            }

            baseBranch = defaultBranch;
        }

        (ReadOutcome refOutcome, ParsedJsonDocument<JsonElement>? refPayload) = await this.GetAsync(
            principalKey, $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/git/ref/heads/{Uri.EscapeDataString(baseBranch)}", cancellationToken).ConfigureAwait(false);
        if (refOutcome != ReadOutcome.Success || refPayload is not { } refDoc)
        {
            return (refOutcome == ReadOutcome.NotConnected ? WriteOutcome.NotConnected : WriteOutcome.NotFound, null);
        }

        string? baseSha;
        using (refDoc)
        {
            baseSha = refDoc.RootElement.TryGetProperty("object"u8, out JsonElement obj) && obj.TryGetProperty("sha"u8, out JsonElement sha) ? sha.GetString() : null;
        }

        if (baseSha is null)
        {
            return (WriteOutcome.NotFound, null);
        }

        string? token = await this.CurrentTokenAsync(principalKey, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return (WriteOutcome.NotConnected, null);
        }

        byte[] body = PersistedJson.ToArray(
            (Name: name, Sha: baseSha),
            static (Utf8JsonWriter writer, in (string Name, string Sha) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString("ref"u8, $"refs/heads/{s.Name}");
                writer.WriteString("sha"u8, s.Sha);
                writer.WriteEndObject();
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{this.options.ApiBaseUrl!.TrimEnd('/')}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/git/refs")
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } },
        };
        AddGitHubHeaders(request, token);
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            this.providers.Disconnect(principalKey, ProviderName);
            return (WriteOutcome.NotConnected, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (WriteOutcome.NotFound, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            // 422 "Reference already exists" and friends: the caller renders the refusal.
            return (WriteOutcome.Refused, null);
        }

        return (WriteOutcome.Success, baseSha);
    }

    private async ValueTask<(ReadOutcome Outcome, ParsedJsonDocument<JsonElement>? Payload)> GetAsync(string principalKey, string pathAndQuery, CancellationToken cancellationToken)
    {
        string? token = await this.CurrentTokenAsync(principalKey, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return (ReadOutcome.NotConnected, null);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{this.options.ApiBaseUrl!.TrimEnd('/')}{pathAndQuery}");
        AddGitHubHeaders(request, token);
        using HttpResponseMessage response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // The token no longer works (revoked, App uninstalled): drop it so status reads disconnected.
            this.providers.Disconnect(principalKey, ProviderName);
            return (ReadOutcome.NotConnected, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            // 403/404 and everything else read as not-found: non-disclosing, like the rest of the surface.
            return (ReadOutcome.NotFound, null);
        }

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return (ReadOutcome.Success, ParsedJsonDocument<JsonElement>.Parse(payload));
    }

    // The valid access token for the principal, from the shared provider broker's custody
    // (refresh-aware). Null when the principal has no session (or the refresh fails — the shared
    // broker drops the session).
    private ValueTask<string?> CurrentTokenAsync(string principalKey, CancellationToken cancellationToken)
        => this.providers.CurrentAccessTokenAsync(principalKey, ProviderName, cancellationToken);

    private static void AddGitHubHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.TryAddWithoutValidation("User-Agent", "arazzo-control-plane");
    }
}

/// <summary>The GitHub App registration a deployment brokers (workflow-designer design §4.7).</summary>
public sealed class GitHubBrokerOptions
{
    /// <summary>Gets the GitHub web origin (GitHub Enterprise Server is configuration, not new design).</summary>
    public string? BaseUrl { get; init; } = "https://github.com";

    /// <summary>Gets the GitHub API origin.</summary>
    public string? ApiBaseUrl { get; init; } = "https://api.github.com";

    /// <summary>Gets the App's OAuth client id.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the secret REFERENCE (e.g. <c>env://GITHUB_APP_SECRET</c>) the deployment's
    /// resolver dereferences at exchange time; the secret itself is never configured or held.</summary>
    public string? ClientSecretRef { get; init; }

    /// <summary>Gets the deployment's externally visible callback URL (the <c>/github/auth/callback</c>
    /// endpoint as the browser reaches it) — GitHub redirects here.</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>Throws when a required field is missing.</summary>
    /// <exception cref="ArgumentException">A required option is absent.</exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(this.BaseUrl) || string.IsNullOrEmpty(this.ApiBaseUrl) || string.IsNullOrEmpty(this.ClientId) || string.IsNullOrEmpty(this.ClientSecretRef) || string.IsNullOrEmpty(this.CallbackUrl))
        {
            throw new ArgumentException("A GitHub broker requires BaseUrl, ApiBaseUrl, ClientId, ClientSecretRef, and CallbackUrl.");
        }
    }

    /// <summary>
    /// Folds this App registration into the connected-provider registry as the <c>github</c> entry
    /// (ADR 0052): explicit authorize/token endpoints (GitHub has no OIDC discovery), the App's
    /// client registration, and host coverage derived from <see cref="BaseUrl"/> (plus
    /// <c>*.githubusercontent.com</c> for github.com, where raw content is served). Register the
    /// result with the deployment's <see cref="ProviderBroker"/> and hand that broker to
    /// <see cref="GitHubBroker"/> — one GitHub sign-in then serves the Git panel and the fetch pane.
    /// </summary>
    /// <returns>The provider entry.</returns>
    public ConnectedProviderOptions ToProviderEntry()
    {
        this.Validate();
        string baseUrl = this.BaseUrl!.TrimEnd('/');
        string host = new Uri(baseUrl).Host;
        var hosts = new List<string> { host, "*." + host };
        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            hosts.Add("*.githubusercontent.com");
        }

        return new ConnectedProviderOptions
        {
            Name = "github",
            DisplayName = "GitHub",
            AuthorizeEndpoint = $"{baseUrl}/login/oauth/authorize",
            TokenEndpoint = $"{baseUrl}/login/oauth/access_token",
            ClientId = this.ClientId,
            ClientSecretRef = this.ClientSecretRef,
            Scopes = "repo read:user user:email",
            CallbackUrl = this.CallbackUrl,
            Hosts = hosts,
        };
    }
}