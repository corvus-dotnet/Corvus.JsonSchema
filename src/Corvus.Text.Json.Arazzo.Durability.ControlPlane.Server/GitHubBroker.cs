// <copyright file="GitHubBroker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Brokers a GitHub App's user-to-server flow for the designer (workflow-designer design §4.7):
/// the control plane holds the OAuth App credentials and each principal's user token — the
/// kit never sees a GitHub credential, and every user-initiated GitHub action runs AS the
/// signed-in user (their GitHub-held git identity; reach is whatever the signed-in user can see,
/// the OAuth model — so a source document anywhere the user can read is reachable).
/// </summary>
/// <remarks>
/// <para><strong>Custody.</strong> Tokens are keyed by control-plane principal and unreachable
/// from any other principal's session (<see cref="IGitHubTokenStore"/>; in-memory by default, a
/// deployment may substitute an encrypted-at-rest store). The OAuth <c>state</c> is single-use,
/// short-lived, and principal-bound: the callback is a top-level browser navigation that carries
/// no bearer token, so the state IS the authentication of that request.</para>
/// <para><strong>Enterprise.</strong> GitHub Enterprise Server is configuration
/// (<see cref="GitHubBrokerOptions.BaseUrl"/>/<see cref="GitHubBrokerOptions.ApiBaseUrl"/>), not
/// new design.</para>
/// </remarks>
public sealed class GitHubBroker
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(60);

    private readonly HttpClient client;
    private readonly GitHubBrokerOptions options;
    private readonly ISecretResolver secrets;
    private readonly TimeProvider timeProvider;
    private readonly IGitHubTokenStore tokens;
    private readonly ConcurrentDictionary<string, PendingAuth> pending = new();

    /// <summary>Initializes a new instance of the <see cref="GitHubBroker"/> class.</summary>
    /// <param name="client">The outbound HTTP client (a deployment configures its handler/egress policy).</param>
    /// <param name="options">The App registration and endpoints.</param>
    /// <param name="secrets">Resolves the App client secret reference at exchange time (never held).</param>
    /// <param name="tokens">The per-principal token store; <see langword="null"/> uses the in-memory (session-lifetime) store.</param>
    /// <param name="timeProvider">The clock (tests inject a fake).</param>
    public GitHubBroker(HttpClient client, GitHubBrokerOptions options, ISecretResolver secrets, IGitHubTokenStore? tokens = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        options.Validate();
        this.client = client;
        this.options = options;
        this.secrets = secrets;
        this.tokens = tokens ?? new InMemoryGitHubTokenStore();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The outcome kinds completing an authorization can report.</summary>
    public enum CompleteOutcome
    {
        /// <summary>The token exchanged and stored for the state's principal.</summary>
        Success,

        /// <summary>The state is unknown, expired, or already used.</summary>
        InvalidState,

        /// <summary>GitHub refused the code exchange.</summary>
        ExchangeFailed,
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
    /// Mints a single-use, principal-bound state and composes the authorize URL the kit opens in a
    /// popup. The state authenticates the callback (a top-level navigation carries no bearer
    /// token), so it is unguessable (256-bit) and short-lived.
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <returns>The authorize URL and the embedded state.</returns>
    public (string AuthorizeUrl, string State) BeginAuth(string principalKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(principalKey);
        this.PrunePending();
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        this.pending[state] = new PendingAuth(principalKey, this.timeProvider.GetUtcNow() + StateLifetime);
        string authorizeUrl = $"{this.options.BaseUrl!.TrimEnd('/')}/login/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(this.options.ClientId!)}" +
            $"&redirect_uri={Uri.EscapeDataString(this.options.CallbackUrl!)}" +
            $"&scope={Uri.EscapeDataString("repo read:user user:email")}" +
            $"&state={Uri.EscapeDataString(state)}";
        return (authorizeUrl, state);
    }

    /// <summary>
    /// Completes the flow: validates and consumes the state (single-use), exchanges the code
    /// server-side (resolving the App client secret only for the exchange), and stores the user
    /// token for the state's principal.
    /// </summary>
    /// <param name="state">The state from the callback query.</param>
    /// <param name="code">The authorization code from the callback query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async ValueTask<CompleteOutcome> CompleteAuthAsync(string state, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(state) || !this.pending.TryRemove(state, out PendingAuth auth) || auth.ExpiresAt < this.timeProvider.GetUtcNow())
        {
            return CompleteOutcome.InvalidState;
        }

        GitHubToken? token = await this.ExchangeAsync(("code", code), cancellationToken).ConfigureAwait(false);
        if (token is not { } t)
        {
            return CompleteOutcome.ExchangeFailed;
        }

        this.tokens.Set(auth.PrincipalKey, t);
        return CompleteOutcome.Success;
    }

    /// <summary>Whether the principal currently holds a token (without touching GitHub).</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <returns><see langword="true"/> if a token is stored.</returns>
    public bool IsConnected(string principalKey) => this.tokens.Get(principalKey) is not null;

    /// <summary>Drops the principal's token. Idempotent.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    public void Disconnect(string principalKey) => this.tokens.Remove(principalKey);

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
            this.tokens.Remove(principalKey);
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
            this.tokens.Remove(principalKey);
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
            this.tokens.Remove(principalKey);
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
            this.tokens.Remove(principalKey);
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

    // The valid access token for the principal, refreshing an expiring one when a refresh token is
    // held. Null when the principal has no session (or the refresh fails — the session is dropped).
    private async ValueTask<string?> CurrentTokenAsync(string principalKey, CancellationToken cancellationToken)
    {
        GitHubToken? stored = this.tokens.Get(principalKey);
        if (stored is not { } current)
        {
            return null;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (current.ExpiresAt is not { } expires || expires - TokenRefreshSkew > now)
        {
            return current.AccessToken;
        }

        if (current.RefreshToken is null || (current.RefreshExpiresAt is { } refreshExpires && refreshExpires <= now))
        {
            this.tokens.Remove(principalKey);
            return null;
        }

        GitHubToken? refreshed = await this.ExchangeAsync(("refresh_token", current.RefreshToken), cancellationToken).ConfigureAwait(false);
        if (refreshed is not { } fresh)
        {
            this.tokens.Remove(principalKey);
            return null;
        }

        this.tokens.Set(principalKey, fresh);
        return fresh.AccessToken;
    }

    // One exchange against /login/oauth/access_token: an authorization code (code) or a refresh
    // grant (refresh_token). The App client secret resolves here and is scrubbed after the call.
    private async ValueTask<GitHubToken?> ExchangeAsync((string Name, string Value) grant, CancellationToken cancellationToken)
    {
        using SecretMaterial secret = await this.secrets.ResolveAsync(SecretRef.Parse(this.options.ClientSecretRef!), cancellationToken).ConfigureAwait(false);
        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", this.options.ClientId!),
            new("client_secret", secret.Reveal()),
            new(grant.Name, grant.Value),
        };
        if (grant.Name == "refresh_token")
        {
            form.Add(new("grant_type", "refresh_token"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{this.options.BaseUrl!.TrimEnd('/')}/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "arazzo-control-plane");
        using HttpResponseMessage? response = await this.SendExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        if (response?.IsSuccessStatusCode != true)
        {
            return null;
        }

        byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("access_token"u8, out JsonElement accessToken) || accessToken.GetString() is not { Length: > 0 } access)
        {
            return null;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = root.TryGetProperty("expires_in"u8, out JsonElement expiresIn) && expiresIn.ValueKind == JsonValueKind.Number
            ? now + TimeSpan.FromSeconds(expiresIn.GetInt32())
            : null;
        string? refresh = root.TryGetProperty("refresh_token"u8, out JsonElement refreshToken) ? refreshToken.GetString() : null;
        DateTimeOffset? refreshExpiresAt = root.TryGetProperty("refresh_token_expires_in"u8, out JsonElement refreshIn) && refreshIn.ValueKind == JsonValueKind.Number
            ? now + TimeSpan.FromSeconds(refreshIn.GetInt32())
            : null;
        return new GitHubToken(access, expiresAt, refresh, refreshExpiresAt);
    }

    // A transport-level failure reaching github.com (DNS, proxy, TLS — e.g. a CA store that cannot chain
    // github.com's certificate) is an exchange FAILURE, not an unhandled 500: the caller maps a null to the
    // typed github-exchange-failed problem, so the operator sees an actionable message instead of a blank error.
    private async ValueTask<HttpResponseMessage?> SendExchangeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static void AddGitHubHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.TryAddWithoutValidation("User-Agent", "arazzo-control-plane");
    }

    private void PrunePending()
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, PendingAuth> entry in this.pending)
        {
            if (entry.Value.ExpiresAt < now)
            {
                this.pending.TryRemove(entry.Key, out _);
            }
        }
    }

    private readonly record struct PendingAuth(string PrincipalKey, DateTimeOffset ExpiresAt);
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
}

/// <summary>One principal's brokered user-to-server token.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="ExpiresAt">When the access token expires (absent: non-expiring).</param>
/// <param name="RefreshToken">The refresh token, when the App issues expiring tokens.</param>
/// <param name="RefreshExpiresAt">When the refresh token expires.</param>
public readonly record struct GitHubToken(string AccessToken, DateTimeOffset? ExpiresAt, string? RefreshToken, DateTimeOffset? RefreshExpiresAt);

/// <summary>
/// Per-principal custody for brokered GitHub tokens (workflow-designer design §4.7): one
/// principal's token is unreachable from any other principal's session. The default is in-memory
/// (session-lifetime); a deployment may substitute an encrypted-at-rest store (KMS ref).
/// </summary>
public interface IGitHubTokenStore
{
    /// <summary>Gets the principal's token, if any.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    GitHubToken? Get(string principalKey);

    /// <summary>Stores (replaces) the principal's token.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    /// <param name="token">The token.</param>
    void Set(string principalKey, GitHubToken token);

    /// <summary>Removes the principal's token. Idempotent.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    void Remove(string principalKey);
}

/// <summary>The in-memory (session-lifetime) <see cref="IGitHubTokenStore"/>.</summary>
public sealed class InMemoryGitHubTokenStore : IGitHubTokenStore
{
    private readonly ConcurrentDictionary<string, GitHubToken> tokens = new();

    /// <inheritdoc/>
    public GitHubToken? Get(string principalKey) => this.tokens.TryGetValue(principalKey, out GitHubToken token) ? token : null;

    /// <inheritdoc/>
    public void Set(string principalKey, GitHubToken token) => this.tokens[principalKey] = token;

    /// <inheritdoc/>
    public void Remove(string principalKey) => this.tokens.TryRemove(principalKey, out _);
}