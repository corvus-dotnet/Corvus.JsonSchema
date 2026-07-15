// <copyright file="ControlPlaneIdentityApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the control-plane identity layer (§16.5.4): <c>/identity/whoami</c> and <c>/identity/capabilities</c> are open
/// to any authenticated caller; <c>/identity/grantees</c> (the resolved-grantee typeahead) is gated by
/// <c>administrators:read</c>. Identities are described as <c>{dimension, value}</c> grants, never raw internal tags.
/// </summary>
[TestClass]
public sealed class ControlPlaneIdentityApiTests
{
    private const string AdminRead = "administrators:read";
    private const string AnyAuth = "runs:read"; // any scope authenticates; whoami/capabilities require no specific scope

    [TestMethod]
    public async Task Whoami_returns_the_callers_identity_as_grants()
    {
        await using Scoped host = await StartAsync();
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/whoami", AnyAuth, "acme"));
        Identity(doc).ShouldBe(["tenant=acme"]);
    }

    [TestMethod]
    public async Task Capabilities_reports_the_resolvable_grantee_kinds()
    {
        await using Scoped host = await StartAsync();
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/capabilities", AnyAuth, "acme"));
        doc.RootElement.GetProperty("granteeKinds").EnumerateArray().Select(k => k.GetString()).Order().ShouldBe(["team", "workflow"]);
        doc.RootElement.GetProperty("directorySearch").GetBoolean().ShouldBeFalse();
    }

    [TestMethod]
    public async Task Grantees_search_returns_observed_identities_resolved_to_grants()
    {
        var observed = new InMemoryObservedIdentityStore();
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("alpha"), Str("Alpha"), Tenant("alpha"), true, "test", default);
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("beta"), Str("Beta"), Tenant("beta"), true, "test", default);

        await using Scoped host = await StartAsync(observed);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=al", AdminRead, "acme"));

        Stj.JsonElement[] grantees = [.. doc.RootElement.GetProperty("grantees").EnumerateArray()];
        grantees.Length.ShouldBe(1); // only "alpha" matches the "al" prefix
        grantees[0].GetProperty("value").GetString().ShouldBe("alpha");
        grantees[0].GetProperty("kind").GetString().ShouldBe("team");
        grantees[0].GetProperty("source").GetString().ShouldBe("observed");
        grantees[0].GetProperty("complete").GetBoolean().ShouldBeTrue();
        GrantsOf(grantees[0]).ShouldBe(["tenant=alpha"]); // identity described back as {dimension,value}, prefix stripped
    }

    [TestMethod]
    public async Task Grantees_search_requires_administrators_read()
    {
        await using Scoped host = await StartAsync();

        // No scope → unauthenticated → 401; an unrelated scope → 403 (lacks administrators:read).
        (await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=al", null, "acme")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=al", AnyAuth, "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Adding_an_administrator_records_it_as_a_searchable_grantee()
    {
        await using Scoped host = await StartAsync();

        // acme establishes the workflow (sole administrator), then adds {tenant, gamma} as a co-administrator.
        await EstablishAsync(host.Catalog, "flow", "acme");
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"tenant","value":"gamma"}""", "administrators:write", "acme"))
            .EnsureSuccessStatusCode();

        // The write hook recorded gamma, so it now resolves through the grantee typeahead.
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=gamma", AdminRead, "acme"));
        Stj.JsonElement[] grantees = [.. doc.RootElement.GetProperty("grantees").EnumerateArray()];
        grantees.Length.ShouldBe(1);
        grantees[0].GetProperty("value").GetString().ShouldBe("gamma");
        grantees[0].GetProperty("kind").GetString().ShouldBe("team");
        GrantsOf(grantees[0]).ShouldBe(["tenant=gamma"]);
    }

    [TestMethod]
    public async Task A_per_person_grant_is_reported_incomplete_so_the_picker_can_warn()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", "acme");

        // A per-person (sub) administrator: its single-tag mapping is NOT the whole stamped identity, so complete:false.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"dimension":"sub","value":"alice"}""", "administrators:write", "acme"))
            .EnsureSuccessStatusCode();

        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=alice", AdminRead, "acme"));
        Stj.JsonElement g = doc.RootElement.GetProperty("grantees").EnumerateArray().Single();
        g.GetProperty("kind").GetString().ShouldBe("person");
        g.GetProperty("complete").GetBoolean().ShouldBeFalse(); // honest: a single-tag person mapping may be partial (§17.2)
    }

    [TestMethod]
    public async Task Grantees_search_is_reach_filtered_so_an_admin_cannot_enumerate_other_tenants()
    {
        var observed = new InMemoryObservedIdentityStore();
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("acme-team"), Str("Acme"), SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), true, "test", default);
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("globex-team"), Str("Globex"), SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]), true, "test", default);

        await using Scoped host = await StartAsync(observed, scopedReach: true);

        // The acme admin searches ALL grantees (empty prefix) — with reach scoping it sees only acme's identity, never
        // globex's (the F1 cross-tenant disclosure is closed; §17.1).
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees", AdminRead, "acme"));
        Stj.JsonElement[] grantees = [.. doc.RootElement.GetProperty("grantees").EnumerateArray()];
        grantees.Length.ShouldBe(1);
        grantees[0].GetProperty("value").GetString().ShouldBe("acme-team");
    }

    [TestMethod]
    public async Task Grantees_search_via_directory_projects_resolved_principals_bytes_to_bytes()
    {
        // A directory that resolves span-constructed principals (the bytes-to-bytes adapter path), so the handler's
        // context-threaded projection writes each grantee's value/label straight from the owned UTF-8 into the response.
        var directory = new FakeDirectory(
            new ResolvedPrincipal(GranteeKind.Team, "acme-team"u8, "Acme Team"u8, hasLabel: true, Tenant("acme")),
            new ResolvedPrincipal(GranteeKind.Team, "acme-ops"u8, "Acme Ops"u8, hasLabel: true, Tenant("acme")));

        await using Scoped host = await StartAsync(directory: directory);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=acme-t&source=directory&kind=team", AdminRead, "acme"));

        Stj.JsonElement[] grantees = [.. doc.RootElement.GetProperty("grantees").EnumerateArray()];
        grantees.Length.ShouldBe(1); // only "acme-team" matches the "acme-t" prefix
        grantees[0].GetProperty("value").GetString().ShouldBe("acme-team");
        grantees[0].GetProperty("label").GetString().ShouldBe("Acme Team");
        grantees[0].GetProperty("kind").GetString().ShouldBe("team");
        grantees[0].GetProperty("source").GetString().ShouldBe("directory");
        grantees[0].GetProperty("complete").GetBoolean().ShouldBeTrue(); // a directory full-resolution is complete (§17.2)
        GrantsOf(grantees[0]).ShouldBe(["tenant=acme"]); // identity described back as {dimension,value}, prefix stripped
    }

    [TestMethod]
    public async Task Grantees_search_with_no_source_merges_directory_and_observed_when_a_directory_is_configured()
    {
        // Directory: a person and a team; observed: the SAME team (a (kind, value) collision — directory preferred)
        // plus a distinct one. With no source parameter and a directory configured, the search defaults to `merged`.
        var directory = new FakeDirectory(
            new ResolvedPrincipal(GranteeKind.Person, "alice"u8, "Alice"u8, hasLabel: true, Tenant("acme")),
            new ResolvedPrincipal(GranteeKind.Team, "acme-team"u8, "Acme Team"u8, hasLabel: true, Tenant("acme")));
        var observed = new InMemoryObservedIdentityStore();
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("acme-team"), Str("Acme (seen)"), Tenant("acme"), true, "test", default);
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("acme-ops"), Str("Acme Ops"), Tenant("acme"), true, "test", default);

        await using Scoped host = await StartAsync(observed, directory: directory);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=a", AdminRead, "acme"));

        string[] results = [.. doc.RootElement.GetProperty("grantees").EnumerateArray()
            .Select(g => $"{g.GetProperty("kind").GetString()}:{g.GetProperty("value").GetString()}@{g.GetProperty("source").GetString()}")];
        results.ShouldContain("person:alice@directory");
        results.ShouldContain("team:acme-team@directory");
        results.ShouldContain("team:acme-ops@observed");
        results.ShouldNotContain("team:acme-team@observed"); // the collision resolves directory-preferred
        doc.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse("merged is one bounded, unpaged result set");
    }

    [TestMethod]
    public async Task Grantees_search_via_directory_with_no_kind_searches_every_searchable_kind()
    {
        // Previously an explicit directory search with no kind defaulted to Person only, hiding teams and roles.
        var directory = new FakeDirectory(
            new ResolvedPrincipal(GranteeKind.Person, "alice"u8, "Alice"u8, hasLabel: true, Tenant("acme")),
            new ResolvedPrincipal(GranteeKind.Team, "acme-team"u8, "Acme Team"u8, hasLabel: true, Tenant("acme")),
            new ResolvedPrincipal(GranteeKind.Role, "auditor"u8, "Auditor"u8, hasLabel: true, Tenant("acme")));

        await using Scoped host = await StartAsync(directory: directory);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=a&source=directory", AdminRead, "acme"));

        doc.RootElement.GetProperty("grantees").EnumerateArray()
            .Select(g => g.GetProperty("kind").GetString()).Order().ShouldBe(["person", "role", "team"]);
    }

    [TestMethod]
    public async Task An_all_kinds_directory_search_returns_the_kinds_that_resolve_when_one_kind_fails()
    {
        // The live-Keycloak shape: the service account may search users and groups but not realm roles, so the role
        // leg fails. The kinds that DID resolve are returned — a partial directory is not "unreachable".
        var directory = new KindFailingDirectory(
            GranteeKind.Role,
            new ResolvedPrincipal(GranteeKind.Person, "alice"u8, "Alice"u8, hasLabel: true, Tenant("acme")),
            new ResolvedPrincipal(GranteeKind.Team, "acme-team"u8, "Acme Team"u8, hasLabel: true, Tenant("acme")));

        await using Scoped host = await StartAsync(directory: directory);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=a&source=directory", AdminRead, "acme"));

        doc.RootElement.GetProperty("grantees").EnumerateArray()
            .Select(g => g.GetProperty("kind").GetString()).Order().ShouldBe(["person", "team"]);
    }

    [TestMethod]
    public async Task A_merged_search_degrades_to_observed_results_when_the_directory_fails()
    {
        var observed = new InMemoryObservedIdentityStore();
        await observed.SeenAsync(ObservedIdentity.GranteeKind.EnumValues.Team, Str("acme-team"), Str("Acme"), Tenant("acme"), true, "test", default);

        await using Scoped host = await StartAsync(observed, directory: new BrokenDirectory());
        HttpResponseMessage response = await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=acme", AdminRead, "acme");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument doc = await ReadJsonAsync(response);
        Stj.JsonElement g = doc.RootElement.GetProperty("grantees").EnumerateArray().Single();
        g.GetProperty("value").GetString().ShouldBe("acme-team");
        g.GetProperty("source").GetString().ShouldBe("observed");
    }

    [TestMethod]
    public async Task An_explicit_directory_search_reports_a_directory_failure_as_a_bad_gateway_problem()
    {
        await using Scoped host = await StartAsync(directory: new BrokenDirectory());
        HttpResponseMessage response = await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=acme&source=directory", AdminRead, "acme");
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        using Stj.JsonDocument doc = await ReadJsonAsync(response);
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(502);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Directory unavailable");
    }

    [TestMethod]
    public async Task Grantees_search_via_directory_omits_an_absent_label()
    {
        // A principal with no display label (hasLabel: false) must omit the label property, not emit an empty string.
        var directory = new FakeDirectory(
            new ResolvedPrincipal(GranteeKind.Team, "acme-team"u8, default, hasLabel: false, Tenant("acme")));

        await using Scoped host = await StartAsync(directory: directory);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/identity/grantees?q=acme&source=directory&kind=team", AdminRead, "acme"));

        Stj.JsonElement g = doc.RootElement.GetProperty("grantees").EnumerateArray().Single();
        g.GetProperty("value").GetString().ShouldBe("acme-team");
        g.TryGetProperty("label", out _).ShouldBeFalse();
    }

    private static async Task EstablishAsync(SecuredWorkflowCatalog catalog, string workflowId, string founder)
    {
        SecurityTagSet founderIdentity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", founder)]);
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        await catalog.AddAsync(CatalogPackage.Build(workflow, []), new CatalogOwner("Team", "team@example.com", null, null), default, founderIdentity, default);
    }

    // The observed-store seam carries the JSON value (reified only at the store's key leaf); a test builds one from a
    // managed string by parsing the JSON string literal (the test values contain no characters needing escaping).
    private static JsonString Str(string value) => JsonString.ParseValue($"\"{value}\"");

    private static SecurityTagSet Tenant(string tenant)
        => SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);

    private static IEnumerable<string> Identity(Stj.JsonDocument document)
        => document.RootElement.GetProperty("identity").EnumerateArray()
            .Select(a => $"{a.GetProperty("dimension").GetString()}={a.GetProperty("value").GetString()}");

    private static IEnumerable<string> GrantsOf(Stj.JsonElement grantee)
        => grantee.GetProperty("identity").EnumerateArray()
            .Select(a => $"{a.GetProperty("dimension").GetString()}={a.GetProperty("value").GetString()}");

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(IObservedIdentityStore? observed = null, bool scopedReach = false, IPrincipalDirectory? directory = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(scopedReach), observedIdentityStore: observed, principalDirectory: directory);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    /// <summary>A minimal scoped policy that stamps the principal's <c>tenant</c> claim as <c>sys:tenant</c> (so whoami
    /// resolves) and declares the team/workflow grantee kinds resolvable. With <paramref name="scopedReach"/>, it also
    /// resolves a read reach over <c>tenant == $claim.tenant</c>, so grantee search is reach-filtered (§17.1).</summary>
    private sealed class TenantIdentityPolicy(bool scopedReach = false) : ControlPlaneRowSecurityPolicy
    {
        public override IReadOnlyList<GranteeKind> SupportedGranteeKinds => [GranteeKind.Team, GranteeKind.Workflow];

        public override AccessContext Resolve(ClaimsPrincipal? principal)
        {
            if (!scopedReach)
            {
                return AccessContext.System;
            }

            string? tenant = principal?.FindFirst("tenant")?.Value;
            return AccessContext.Uniform(new SecurityFilter(
                [SecurityRule.Compile("tenant == $claim.tenant")],
                new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = string.IsNullOrEmpty(tenant) ? [] : [tenant] }));
        }

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    // A stub directory: a prefix match over a fixed set of span-constructed principals (the bytes-to-bytes adapter path).
    private sealed class FakeDirectory(params ResolvedPrincipal[] principals) : IPrincipalDirectory
    {
        public ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<ResolvedPrincipal> matches = [.. principals.Where(p => p.Kind == kind && p.Value.StartsWith(query, StringComparison.Ordinal)).Take(limit)];
            return new ValueTask<IReadOnlyList<ResolvedPrincipal>>(matches);
        }
    }

    // A directory whose backend is unreachable: every search fails the way a real adapter does (the shared base type),
    // so the handler's degrade (merged) / 502 (explicit) paths are exercised.
    private sealed class BrokenDirectory : IPrincipalDirectory
    {
        public ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
            => throw new PrincipalDirectoryException("the directory returned 403 (Forbidden).");
    }

    // A directory where ONE kind's backing resource fails (the live-Keycloak service-account shape) while the others
    // resolve, so the all-kinds sweep's per-kind degrade is exercised.
    private sealed class KindFailingDirectory(GranteeKind failing, params ResolvedPrincipal[] principals) : IPrincipalDirectory
    {
        public ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
        {
            if (kind == failing)
            {
                throw new PrincipalDirectoryException($"the directory returned 403 (Forbidden) searching {kind}.");
            }

            IReadOnlyList<ResolvedPrincipal> matches = [.. principals.Where(p => p.Kind == kind && p.Value.StartsWith(query, StringComparison.Ordinal)).Take(limit)];
            return new ValueTask<IReadOnlyList<ResolvedPrincipal>>(matches);
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog) : IAsyncDisposable
    {
        public SecuredWorkflowCatalog Catalog => catalog;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string? identity = null)
            => Send(new HttpRequestMessage(method, path), scope, identity);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope, string? identity = null)
            => Send(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope, identity);

        private Task<HttpResponseMessage> Send(HttpRequestMessage request, string? scope, string? identity)
        {
            if (scope is not null)
            {
                request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
            }

            if (identity is not null)
            {
                request.Headers.Add(ScopeAuthHandler.IdentityHeader, identity);
            }

            return client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";
        public const string IdentityHeader = "X-Identity";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            if (this.Request.Headers.TryGetValue(IdentityHeader, out Microsoft.Extensions.Primitives.StringValues who) && !string.IsNullOrEmpty(who.ToString()))
            {
                identity.AddClaim(new Claim("tenant", who.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}