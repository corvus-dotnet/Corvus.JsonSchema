// <copyright file="ControlPlaneGitHubApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the brokered GitHub API (workflow-designer design §4.7) against a loopback stub GitHub:
/// the user-to-server sign-in (single-use principal-bound state; the callback authenticates by
/// state, not bearer), per-principal token custody (one caller's session is unreachable from
/// another's), the session status projection, proxied contents reads, and the fails-closed posture
/// when a deployment brokers no App.
/// </summary>
[TestClass]
public sealed class ControlPlaneGitHubApiTests
{
    private const string GoodCode = "good-code";
    private const string StubToken = "user-to-server-token";

    [TestMethod]
    public async Task The_sign_in_flow_connects_a_principal_and_serves_status_and_contents()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        // Begin (authenticated): the authorize URL points at the configured GitHub with a state.
        HttpResponseMessage begun = await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", "ada");
        begun.StatusCode.ShouldBe(HttpStatusCode.OK);
        string state;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await begun.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("authorizeUrl").GetString()!.ShouldStartWith($"{github.Url}/login/oauth/authorize");
            state = doc.RootElement.GetProperty("state").GetString()!;
        }

        // The callback is a top-level navigation: NO bearer — the single-use state IS the authentication.
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Status: the signed-in identity and a first page of the repositories the user can reach.
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeTrue();
            session.RootElement.GetProperty("login").GetString().ShouldBe("octo");
            Stj.JsonElement repository = session.RootElement.GetProperty("repositories")[0];
            repository.GetProperty("fullName").GetString().ShouldBe("acme-org/specs");
            repository.GetProperty("defaultBranch").GetString().ShouldBe("main");
        }

        // Browse: a directory lists entries; a file carries base64 content.
        using (Stj.JsonDocument dir = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents", "workspace:read", "ada")))
        {
            dir.RootElement.GetProperty("kind").GetString().ShouldBe("dir");
            dir.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ShouldBe(["flows", "petstore.json"]);
        }

        using (Stj.JsonDocument file = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents?path=petstore.json", "workspace:read", "ada")))
        {
            file.RootElement.GetProperty("kind").GetString().ShouldBe("file");
            string content = file.RootElement.GetProperty("file").GetProperty("content").GetString()!;
            Encoding.UTF8.GetString(Convert.FromBase64String(content)).ShouldContain("openapi");
        }

        // Disconnect: idempotent; the session reads disconnected afterwards.
        (await host.SendAsync(HttpMethod.Delete, "/github/session", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task Token_custody_is_per_principal_and_the_state_is_single_use()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // ada is connected; bob is NOT — custody keys by principal, so ada's session is unreachable.
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "bob")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }

        (await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/contents", "workspace:read", "bob"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The state is single-use: replaying the callback refuses; garbage refuses.
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAnonymousAsync("/github/auth/callback?code=x&state=nonsense"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A refused exchange (bad code) does not connect the principal.
        string second = await host.BeginAsync("bob");
        (await host.SendAnonymousAsync($"/github/auth/callback?code=wrong&state={Uri.EscapeDataString(second)}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using (Stj.JsonDocument session = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "bob")))
        {
            session.RootElement.GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task A_bound_working_copy_commits_and_pulls_authored_as_the_user()
    {
        const string boundDoc = """{"arazzo":"1.1.0","info":{"title":"Adopt","version":"1.0.0"},"sourceDescriptions":[{"name":"petstore","url":"./petstore.json","type":"openapi"}],"workflows":[{"workflowId":"adopt","steps":[]}]}""";
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A working copy with an inline spec attachment and one scenario, then bound to a branch.
        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"name":"adopt","document":{{boundDoc}}}""", "workspace:write", "ada");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string id;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            id = doc.RootElement.GetProperty("id").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/petstore", """{"document":{"openapi":"3.1.0","info":{"title":"Pets","version":"1"},"paths":{}}}""", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/happy", """{"name":"happy","expect":{"outcome":"completed"}}""", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.OK);

        string etag = await host.EtagAsync(id);
        HttpResponseMessage bound = await host.SendJsonAsync(
            HttpMethod.Put, $"/workspace/workflows/{id}",
            $$"""{"document":{{boundDoc}},"expectedEtag":"{{etag}}","gitBinding":{"owner":"acme-org","repo":"specs","branch":"feature/adopt","path":"flows/adopt.arazzo.json","specPaths":{"petstore":"specs/petstore.json"},"scenariosDir":"scenarios/adopt"} }""",
            "workspace:write", "ada");
        bound.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument saved = Stj.JsonDocument.Parse(await bound.Content.ReadAsStringAsync()))
        {
            saved.RootElement.GetProperty("gitBinding").GetProperty("branch").GetString().ShouldBe("feature/adopt");
        }

        // Commit: document + bound spec + scenario file, in deterministic order, with an optional draft PR.
        HttpResponseMessage committed = await host.SendJsonAsync(
            HttpMethod.Post, $"/workspace/workflows/{id}/git/commit",
            """{"message":"sync from designer","pullRequest":{"base":"main","draft":true}}""",
            "workspace:write", "ada");
        committed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument result = Stj.JsonDocument.Parse(await committed.Content.ReadAsStringAsync()))
        {
            result.RootElement.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString())
                .ShouldBe(["flows/adopt.arazzo.json", "specs/petstore.json", "scenarios/adopt/happy.scenario.json"]);
            result.RootElement.GetProperty("pullRequest").GetProperty("url").GetString()!.ShouldContain("/pull/");
        }

        // The §4.7 identity rule, on the wire: every write carries message/branch/content and NO
        // author/committer — GitHub stamps the signed-in user's git identity, not one we compose.
        github.ContentPuts.Count.ShouldBe(3);
        foreach ((string _, string body) in github.ContentPuts)
        {
            using Stj.JsonDocument put = Stj.JsonDocument.Parse(body);
            put.RootElement.TryGetProperty("author", out _).ShouldBeFalse("the control plane composes no git author (§4.7)");
            put.RootElement.TryGetProperty("committer", out _).ShouldBeFalse("the control plane composes no git committer (§4.7)");
            put.RootElement.GetProperty("branch").GetString().ShouldBe("feature/adopt");
            put.RootElement.GetProperty("message").GetString().ShouldBe("sync from designer");
        }

        using (Stj.JsonDocument pr = Stj.JsonDocument.Parse(github.PullRequests.ShouldHaveSingleItem()))
        {
            pr.RootElement.GetProperty("head").GetString().ShouldBe("feature/adopt");
            pr.RootElement.GetProperty("base").GetString().ShouldBe("main");
            pr.RootElement.GetProperty("draft").GetBoolean().ShouldBeTrue();
        }

        // Repo-side edits: the document title changes and a second scenario appears; pull refreshes both
        // under the etag guard.
        github.Files["flows/adopt.arazzo.json"] = Encoding.UTF8.GetBytes(boundDoc.Replace("\"title\":\"Adopt\"", "\"title\":\"Adopt v2\""));
        github.Files["scenarios/adopt/sad.scenario.json"] = Encoding.UTF8.GetBytes("""{"name":"sad","expect":{"outcome":"faulted"}}""");
        etag = await host.EtagAsync(id);
        HttpResponseMessage pulled = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/git/pull", $$"""{"expectedEtag":"{{etag}}"}""", "workspace:write", "ada");
        pulled.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument refreshed = Stj.JsonDocument.Parse(await pulled.Content.ReadAsStringAsync()))
        {
            refreshed.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Adopt v2");
        }

        using (Stj.JsonDocument scenarios = Stj.JsonDocument.Parse(await (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/scenarios", "{}", "workspace:read", "ada")).Content.ReadAsStringAsync()))
        {
            scenarios.RootElement.GetProperty("scenarios").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["happy", "sad"]);
        }

        // A stale etag conflicts; an unbound copy refuses; a caller without a session conflicts.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/git/pull", $$"""{"expectedEtag":"{{etag}}"}""", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/git/commit", """{"message":"x"}""", "workspace:write", "bob")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        HttpResponseMessage unboundCreated = await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"name":"unbound","document":{{boundDoc}}}""", "workspace:write", "ada");
        string unboundId;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await unboundCreated.Content.ReadAsStringAsync()))
        {
            unboundId = doc.RootElement.GetProperty("id").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{unboundId}/git/commit", """{"message":"x"}""", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Commit_history_lists_paged_and_a_pull_at_ref_rolls_back()
    {
        const string boundDoc = """{"arazzo":"1.1.0","info":{"title":"Adopt","version":"1.0.0"},"workflows":[{"workflowId":"adopt","steps":[{"stepId":"a","operationId":"x"},{"stepId":"b","operationId":"y"}]}]}""";
        const string historicDoc = """{"arazzo":"1.1.0","info":{"title":"Adopt","version":"1.0.0"},"workflows":[{"workflowId":"adopt","steps":[{"stepId":"a","operationId":"x"}]}]}""";
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);
        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // One page of history, projected to sha/message/author/date; a full page implies more.
        using (Stj.JsonDocument list = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/commits?sha=main&path=flows/adopt.arazzo.json&perPage=2", "workspace:read", "ada")))
        {
            list.RootElement.GetProperty("commits").GetArrayLength().ShouldBe(2);
            list.RootElement.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
            list.RootElement.GetProperty("commits")[0].GetProperty("sha").GetString().ShouldBe("sha-head-3");
            list.RootElement.GetProperty("commits")[0].GetProperty("message").GetString().ShouldBe("Route failures to review");
            list.RootElement.GetProperty("commits")[0].GetProperty("author").GetString().ShouldBe("Octo Cat");
            list.RootElement.GetProperty("commits")[0].GetProperty("date").GetString().ShouldBe("2026-07-04T10:00:00Z");
        }

        // The scoping params flow to GitHub verbatim (branch + bound path).
        github.CommitQueries.ShouldHaveSingleItem().ShouldContain("sha=main");
        github.CommitQueries[0].ShouldContain("path=flows%2Fadopt.arazzo.json");

        using (Stj.JsonDocument list = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/commits?perPage=2&page=2", "workspace:read", "ada")))
        {
            list.RootElement.GetProperty("commits").GetArrayLength().ShouldBe(1);
            list.RootElement.GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        }

        // A caller without a session conflicts.
        (await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/commits", "workspace:read", "bob")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Rollback: a pull carrying ref fetches the bound files at THAT commit; the binding is
        // unchanged, so the next commit records the rollback as a new commit on the branch.
        github.Files["flows/adopt.arazzo.json"] = Encoding.UTF8.GetBytes(boundDoc);
        github.FilesAt["sha-old-1"] = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["flows/adopt.arazzo.json"] = Encoding.UTF8.GetBytes(historicDoc) };
        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"name":"adopt","document":{{boundDoc}}}""", "workspace:write", "ada");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string id;
        using (Stj.JsonDocument doc = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            id = doc.RootElement.GetProperty("id").GetString()!;
        }

        string etag = await host.EtagAsync(id);
        (await host.SendJsonAsync(
            HttpMethod.Put, $"/workspace/workflows/{id}",
            $$"""{"document":{{boundDoc}},"expectedEtag":"{{etag}}","gitBinding":{"owner":"acme-org","repo":"specs","branch":"main","path":"flows/adopt.arazzo.json"} }""",
            "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.OK);

        etag = await host.EtagAsync(id);
        HttpResponseMessage rolledBack = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/git/pull", $$"""{"expectedEtag":"{{etag}}","ref":"sha-old-1"}""", "workspace:write", "ada");
        rolledBack.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument refreshed = Stj.JsonDocument.Parse(await rolledBack.Content.ReadAsStringAsync()))
        {
            refreshed.RootElement.GetProperty("document").GetProperty("workflows")[0].GetProperty("steps").GetArrayLength().ShouldBe(1, "the pull fetched the commit's state, not the head");
            refreshed.RootElement.GetProperty("gitBinding").GetProperty("branch").GetString().ShouldBe("main", "a rollback does not rebind");
        }

        // A plain pull (no ref) still reads the head.
        etag = await host.EtagAsync(id);
        HttpResponseMessage headPull = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/git/pull", $$"""{"expectedEtag":"{{etag}}"}""", "workspace:write", "ada");
        headPull.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument refreshed = Stj.JsonDocument.Parse(await headPull.Content.ReadAsStringAsync()))
        {
            refreshed.RootElement.GetProperty("document").GetProperty("workflows")[0].GetProperty("steps").GetArrayLength().ShouldBe(2);
        }
    }

    [TestMethod]
    public async Task Branches_list_and_create_through_the_brokered_session()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);
        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // The picker's list: the branches and the default branch.
        using (Stj.JsonDocument list = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/branches", "workspace:read", "ada")))
        {
            list.RootElement.GetProperty("defaultBranch").GetString().ShouldBe("main");
            list.RootElement.GetProperty("branches")[0].GetProperty("name").GetString().ShouldBe("main");
            list.RootElement.GetProperty("branches")[0].GetProperty("sha").GetString().ShouldBe("sha-main");
        }

        // Create from the default branch's head: a ref, no commit, no composed identity.
        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/github/repos/acme-org/specs/branches", """{"name":"feature/adopt"}""", "workspace:write", "ada");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument branch = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            branch.RootElement.GetProperty("name").GetString().ShouldBe("feature/adopt");
            branch.RootElement.GetProperty("sha").GetString().ShouldBe("sha-main");
        }

        github.Branches.ContainsKey("feature/adopt").ShouldBeTrue();

        // A taken name refuses with its own problem type; an unknown base 404s.
        HttpResponseMessage duplicate = await host.SendJsonAsync(HttpMethod.Post, "/github/repos/acme-org/specs/branches", """{"name":"feature/adopt"}""", "workspace:write", "ada");
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.Content.ReadAsStringAsync()).ShouldContain("github-branch-exists");
        (await host.SendJsonAsync(HttpMethod.Post, "/github/repos/acme-org/specs/branches", """{"name":"x","from":"ghost"}""", "workspace:write", "ada"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // No session: fails closed like every other brokered call.
        (await host.SendAsync(HttpMethod.Get, "/github/repos/acme-org/specs/branches", "workspace:read", "bob"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task The_repository_typeahead_searches_on_the_callers_token_and_requires_a_session()
    {
        await using StubGitHub github = await StubGitHub.StartAsync();
        await using Scoped host = await StartAsync(github.Url);

        // No session yet: the typeahead refuses rather than searching anonymously.
        (await host.SendAsync(HttpMethod.Get, "/github/repos/search?query=dotnet%2Frun", "workspace:read", "ada"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        string state = await host.BeginAsync("ada");
        (await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument results = await ReadAsync(await host.SendAsync(HttpMethod.Get, "/github/repos/search?query=dotnet%2Frun", "workspace:read", "ada"));
        Stj.JsonElement repository = results.RootElement.GetProperty("repositories")[0];
        repository.GetProperty("fullName").GetString().ShouldBe("dotnet/runtime");
        repository.GetProperty("defaultBranch").GetString().ShouldBe("main");
    }

    [TestMethod]
    public async Task A_transport_failure_reaching_github_reports_exchange_failed_not_500()
    {
        // GitHub unreachable from the control plane (DNS, proxy, TLS — e.g. an overridden CA store that cannot
        // chain github.com's certificate): the callback must map to the typed github-exchange-failed problem,
        // not escape as an unhandled 500 whose only follow-up is a doomed invalid-state refresh.
        await using Scoped host = await StartAsync("http://127.0.0.1:1");
        string state = await host.BeginAsync("ada");

        HttpResponseMessage callback = await host.SendAnonymousAsync($"/github/auth/callback?code={GoodCode}&state={Uri.EscapeDataString(state)}");
        callback.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await callback.Content.ReadAsStringAsync();
        body.ShouldContain("github-exchange-failed");
        body.ShouldContain("could not be reached");
    }

    [TestMethod]
    public async Task A_deployment_without_a_broker_fails_closed_and_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync(gitHubUrl: null);

        // No broker wired: every operation refuses.
        (await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendAsync(HttpMethod.Get, "/github/session", "workspace:read", "ada")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Scope gating: beginning the sign-in needs workspace:write.
        (await host.SendAsync(HttpMethod.Post, "/github/auth", "workspace:read", "ada")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Stj.JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Scoped> StartAsync(string? gitHubUrl)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

        GitHubBroker? broker = null;
        if (gitHubUrl is not null)
        {
            Environment.SetEnvironmentVariable("GITHUB_TEST_APP_SECRET", "app-secret");
            var gitHubOptions = new GitHubBrokerOptions
            {
                BaseUrl = gitHubUrl,
                ApiBaseUrl = gitHubUrl,
                ClientId = "app-client-id",
                ClientSecretRef = "env://GITHUB_TEST_APP_SECRET",
                CallbackUrl = "http://localhost/github/auth/callback",
            };

            // GitHub folds in as provider #1 (ADR 0052): the shared ProviderBroker owns the
            // sign-in/custody machinery, and the GitHubBroker keeps the API surface over it.
            var oauthClient = new HttpClient();
            var providers = new ProviderBroker(oauthClient, [gitHubOptions.ToProviderEntry()], new SecretResolverBuilder().AddEnvironment().Build());
            broker = new GitHubBroker(oauthClient, gitHubOptions, providers);
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeSubAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeSubAuthHandler>(ScopeSubAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(
            management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly,
            gitHubBroker: broker);
        await app.StartAsync();
        return new Scoped(app, app.GetTestClient());
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public async Task<string> BeginAsync(string subject)
        {
            HttpResponseMessage begun = await this.SendAsync(HttpMethod.Post, "/github/auth", "workspace:write", subject);
            begun.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await begun.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("state").GetString()!;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string scopes, string subject)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(ScopeSubAuthHandler.ScopeHeader, scopes);
            request.Headers.Add(ScopeSubAuthHandler.SubjectHeader, subject);
            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string scopes, string subject)
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add(ScopeSubAuthHandler.ScopeHeader, scopes);
            request.Headers.Add(ScopeSubAuthHandler.SubjectHeader, subject);
            return await client.SendAsync(request);
        }

        public async Task<string> EtagAsync(string id)
        {
            HttpResponseMessage fetched = await this.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", "workspace:read", "ada");
            fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("etag").GetString()!;
        }

        public async Task<HttpResponseMessage> SendAnonymousAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class ScopeSubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesSub";
        public const string ScopeHeader = "X-Scopes";
        public const string SubjectHeader = "X-Sub";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues scopes))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", scopes.ToString()));
            if (this.Request.Headers.TryGetValue(SubjectHeader, out Microsoft.Extensions.Primitives.StringValues subject))
            {
                identity.AddClaim(new Claim("sub", subject.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    /// <summary>A loopback stub GitHub: the token exchange, the user/repos reads, and a mutable
    /// repository (contents read/write + pulls) whose write bodies are captured verbatim — the §4.7
    /// author/committer-omission rule is asserted on the wire.</summary>
    private sealed class StubGitHub : IAsyncDisposable
    {
        private WebApplication? app;

        public string Url { get; private set; } = string.Empty;

        public System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Files { get; } = new();

        public List<(string Path, string Body)> ContentPuts { get; } = [];

        public List<string> PullRequests { get; } = [];

        public System.Collections.Concurrent.ConcurrentDictionary<string, string> Branches { get; } = new() { ["main"] = "sha-main" };

        public List<(string Sha, string Message, string Author, string Date)> Commits { get; } =
        [
            ("sha-head-3", "Route failures to review", "Octo Cat", "2026-07-04T10:00:00Z"),
            ("sha-mid-2", "Add the review step", "Priya Ops", "2026-07-01T10:00:00Z"),
            ("sha-old-1", "Initial workflow", "Octo Cat", "2026-06-27T10:00:00Z"),
        ];

        public List<string> CommitQueries { get; } = [];

        /// <summary>Historic content per commit sha: a contents read carrying <c>?ref=</c> serves the
        /// file at THAT commit when the sha carries an override — the rollback/compare data.</summary>
        public System.Collections.Concurrent.ConcurrentDictionary<string, Dictionary<string, byte[]>> FilesAt { get; } = new();

        public static async Task<StubGitHub> StartAsync()
        {
            var stub = new StubGitHub();
            stub.Files["petstore.json"] = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0"}""");
            stub.Files["flows/existing.arazzo.json"] = Encoding.UTF8.GetBytes("""{"arazzo":"1.1.0"}""");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");

            app.MapPost("/login/oauth/access_token", async (HttpContext context) =>
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                if (form["client_id"] != "app-client-id" || form["client_secret"] != "app-secret")
                {
                    return Results.Json(new { error = "incorrect_client_credentials" });
                }

                if (form["code"] == GoodCode || form["refresh_token"] == "refresh-1")
                {
                    return Results.Json(new
                    {
                        access_token = StubToken,
                        expires_in = 28800,
                        refresh_token = "refresh-1",
                        refresh_token_expires_in = 15897600,
                        token_type = "bearer",
                    });
                }

                return Results.Json(new { error = "bad_verification_code" });
            });

            app.MapGet("/user", (HttpContext context) => Authorized(context)
                ? Results.Json(new { login = "octo", name = "Octo Cat", avatar_url = "https://example.test/octo.png" })
                : Results.Unauthorized());

            app.MapGet("/search/repositories", (HttpContext context) => Authorized(context)
                ? Results.Json(new { total_count = 1, items = new[] { new { name = "runtime", full_name = "dotnet/runtime", owner = new { login = "dotnet" }, default_branch = "main", @private = false } } })
                : Results.Unauthorized());

            app.MapGet("/user/repos", (HttpContext context) => Authorized(context)
                ? Results.Json(new[] { new { name = "specs", full_name = "acme-org/specs", owner = new { login = "acme-org" }, default_branch = "main", @private = true } })
                : Results.Unauthorized());

            app.MapGet("/repos/acme-org/specs", (HttpContext context) => Authorized(context)
                ? Results.Json(new { name = "specs", full_name = "acme-org/specs", default_branch = "main" })
                : Results.Unauthorized());

            app.MapGet("/repos/acme-org/specs/branches", (HttpContext context) => Authorized(context)
                ? Results.Json(stub.Branches.OrderBy(b => b.Key).Select(b => new { name = b.Key, commit = new { sha = b.Value }, @protected = b.Key == "main" }).ToArray())
                : Results.Unauthorized());

            app.MapGet("/repos/acme-org/specs/git/ref/heads/{*branch}", (HttpContext context, string branch) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                return stub.Branches.TryGetValue(branch, out string? sha)
                    ? Results.Json(new { @ref = $"refs/heads/{branch}", @object = new { sha } })
                    : Results.NotFound();
            });

            app.MapPost("/repos/acme-org/specs/git/refs", async (HttpContext context) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                using Stj.JsonDocument body = await Stj.JsonDocument.ParseAsync(context.Request.Body);
                string reference = body.RootElement.GetProperty("ref").GetString()!;
                string sha = body.RootElement.GetProperty("sha").GetString()!;
                string name = reference["refs/heads/".Length..];
                return stub.Branches.TryAdd(name, sha)
                    ? Results.Json(new { @ref = reference, @object = new { sha } }, statusCode: 201)
                    : Results.UnprocessableEntity(new { message = "Reference already exists" });
            });

            app.MapGet("/repos/acme-org/specs/commits", (HttpContext context) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                stub.CommitQueries.Add(context.Request.QueryString.Value ?? string.Empty);
                int perPage = int.TryParse(context.Request.Query["per_page"], out int pp) ? pp : 30;
                int page = int.TryParse(context.Request.Query["page"], out int pg) ? pg : 1;
                return Results.Json(stub.Commits
                    .Skip((page - 1) * perPage)
                    .Take(perPage)
                    .Select(c => new { sha = c.Sha, commit = new { message = c.Message, author = new { name = c.Author, date = c.Date } } })
                    .ToArray());
            });

            app.MapGet("/repos/acme-org/specs/contents/{*path}", (HttpContext context, string? path) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                string key = path ?? string.Empty;
                string? reference = context.Request.Query["ref"];
                if (!string.IsNullOrEmpty(reference)
                    && stub.FilesAt.TryGetValue(reference, out Dictionary<string, byte[]>? historic)
                    && historic.TryGetValue(key, out byte[]? historicFile))
                {
                    return Results.Json(new
                    {
                        name = key.Split('/').Last(),
                        path = key,
                        sha = ShaOf(historicFile),
                        size = historicFile.Length,
                        encoding = "base64",
                        content = Convert.ToBase64String(historicFile),
                    });
                }

                if (stub.Files.TryGetValue(key, out byte[]? file))
                {
                    return Results.Json(new
                    {
                        name = key.Split('/').Last(),
                        path = key,
                        sha = ShaOf(file),
                        size = file.Length,
                        encoding = "base64",
                        content = Convert.ToBase64String(file),
                    });
                }

                // A directory: list the entries directly under it (files and one-level dirs).
                string prefix = key.Length == 0 ? string.Empty : key + "/";
                var names = new SortedDictionary<string, bool>(StringComparer.Ordinal);
                foreach (string candidate in stub.Files.Keys)
                {
                    if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string rest = candidate[prefix.Length..];
                    int slash = rest.IndexOf('/', StringComparison.Ordinal);
                    names[slash < 0 ? rest : rest[..slash]] = slash < 0;
                }

                if (names.Count == 0)
                {
                    return Results.NotFound();
                }

                return Results.Json(names.Select(n => new
                {
                    name = n.Key,
                    path = prefix + n.Key,
                    type = n.Value ? "file" : "dir",
                    size = n.Value ? stub.Files[prefix + n.Key].Length : 0,
                    sha = n.Value ? ShaOf(stub.Files[prefix + n.Key]) : "dir",
                }).ToArray());
            });

            app.MapPut("/repos/acme-org/specs/contents/{*path}", async (HttpContext context, string path) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                using var reader = new StreamReader(context.Request.Body);
                string body = await reader.ReadToEndAsync();
                stub.ContentPuts.Add((path, body));
                using Stj.JsonDocument document = Stj.JsonDocument.Parse(body);
                byte[] content = Convert.FromBase64String(document.RootElement.GetProperty("content").GetString()!);
                bool existed = stub.Files.ContainsKey(path);
                if (existed)
                {
                    // An update requires the current sha (the contents-API concurrency check).
                    if (!document.RootElement.TryGetProperty("sha", out Stj.JsonElement sha) || sha.GetString() != ShaOf(stub.Files[path]))
                    {
                        return Results.Conflict();
                    }
                }

                stub.Files[path] = content;
                return Results.Json(new { content = new { sha = ShaOf(content), path } }, statusCode: existed ? 200 : 201);
            });

            app.MapPost("/repos/acme-org/specs/pulls", async (HttpContext context) =>
            {
                if (!Authorized(context))
                {
                    return Results.Unauthorized();
                }

                using var reader = new StreamReader(context.Request.Body);
                string body = await reader.ReadToEndAsync();
                stub.PullRequests.Add(body);
                return Results.Json(new { number = 41 + stub.PullRequests.Count, html_url = $"https://github.example/acme-org/specs/pull/{41 + stub.PullRequests.Count}" }, statusCode: 201);
            });

            await app.StartAsync();
            stub.app = app;
            stub.Url = app.Urls.First().TrimEnd('/');
            return stub;
        }

        public async ValueTask DisposeAsync()
        {
            if (this.app is not null)
            {
                await this.app.DisposeAsync();
            }
        }

        private static bool Authorized(HttpContext context)
            => context.Request.Headers.Authorization.ToString() == $"Bearer {StubToken}";

        private static string ShaOf(byte[] content)
            => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(content)).ToLowerInvariant();
    }
}