// <copyright file="ControlPlaneAdministratorsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// Tests the control-plane workflow-administration management API (§15): a base id's administrator set over
/// <c>/administrators</c>, gated by the <c>administrators:read</c>/<c>administrators:write</c> scopes. An administrator
/// is a deployment-stamped identity named operator-side by the grant <c>{dimension, value}</c> it maps to; the set is
/// governed by current-administrator membership, is never orphanable, and is non-disclosing (unknown base id and
/// not-an-administrator are both 403).
/// </summary>
[TestClass]
public sealed class ControlPlaneAdministratorsApiTests
{
    private const string Write = "administrators:write";
    private const string Read = "administrators:read";
    private const string Acme = "acme";
    private const string Globex = "globex";

    [TestMethod]
    public async Task An_administrator_set_lists_adds_removes_and_transfers()
    {
        await using Scoped host = await StartAsync();

        // acme establishes the base id by publishing version 1, becoming its sole administrator.
        await EstablishAsync(host.Catalog, "flow", Acme);

        // The founder lists as the single administrator, described back as the grant it maps from.
        using (Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/flow", Read, Acme)))
        {
            Grants(listed).ShouldBe(["tenant=acme"]);
        }

        // acme adds globex as a co-administrator (idempotent membership add).
        using (Stj.JsonDocument added = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Acme)))
        {
            Grants(added).Order().ShouldBe(["tenant=acme", "tenant=globex"]);
        }

        // globex, now an administrator, removes acme by its identity digest — the set never empties because globex remains.
        using (Stj.JsonDocument removed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, $"/administrators/flow/members/{Digest(Acme)}", Write, Globex)))
        {
            Grants(removed).ShouldBe(["tenant=globex"]);
        }

        // globex transfers administration to a fresh set (handing it back to acme).
        using (Stj.JsonDocument transferred = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[{"kind":"team","value":"acme"}]}""", Write, Globex)))
        {
            Grants(transferred).ShouldBe(["tenant=acme"]);
        }
    }

    [TestMethod]
    public async Task A_non_administrator_is_refused()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // globex is not an administrator: adding (or transferring, or removing) is refused, non-disclosingly.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Globex))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Delete, $"/administrators/flow/members/{Digest(Acme)}", Write, Globex))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task An_unknown_base_id_is_refused_identically_to_a_non_administrator()
    {
        await using Scoped host = await StartAsync();

        // No administration established for 'ghost': a mutation is a 403, not a 404 — membership is non-disclosing.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/ghost/members", """{"kind":"team","value":"globex"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Listing an unknown base id is an empty set (no administration), not an error.
        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/ghost", Read, Acme));
        Grants(listed).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_last_administrator_cannot_be_removed()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // acme is the sole administrator: removing itself would orphan the workflow — refused (409).
        (await host.SendAsync(HttpMethod.Delete, $"/administrators/flow/members/{Digest(Acme)}", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Removing_a_non_member_is_an_idempotent_no_op()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // acme is an administrator; globex is not. Removing globex changes nothing and returns the unchanged set (200).
        using Stj.JsonDocument removed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, $"/administrators/flow/members/{Digest(Globex)}", Write, Acme));
        Grants(removed).ShouldBe(["tenant=acme"]);
    }

    [TestMethod]
    public async Task A_transfer_requires_at_least_one_administrator()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // An empty administrator set is rejected by the schema (minItems) before the handler runs.
        (await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[]}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_control_plane_over_a_catalog_without_an_administrator_store_does_not_start()
    {
        // ADR 0007: administration is the explicit record. A catalog without the store publishes nothing and answers no
        // administration read, so mapping the control plane over one is refused rather than deriving an owner from
        // version 1, which is what this host did until the fallback was deleted.
        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(() => StartAsync(withAdministratorStore: false));

        refusal.ParamName.ShouldBe("catalog");
        refusal.Message.ShouldContain("has no administrator store");
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        // No scope at all → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/administrators/flow", null, Acme)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Read, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/administrators/flow", Write, Acme)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_grant_whose_resolved_identity_collides_with_another_grantee_is_refused()
    {
        // A deployment whose identity mapping is NOT unique: the grantee values "real" and "alias" both resolve to the
        // same sys: identity. With an observed-identity store wired, the collision guard (§16.5.4) must refuse the second.
        await using Scoped host = await StartAsync(observed: new InMemoryObservedIdentityStore(), policy: new CollidingIdentityPolicy());
        await EstablishAsync(host.Catalog, "flow", Acme);

        // acme records a co-administrator grantee "real" (→ the shared identity), succeeding and seeding the typeahead.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"real"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // "alias" is a DIFFERENT grantee value that resolves to the SAME identity as "real" — naming it would author an
        // ambiguous grant (the grant would silently also admit "real"), so it is refused (409), not merged.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"alias"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // A genuinely distinct grantee resolves to its own identity and is unaffected.
        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"distinct"}""", Write, Acme))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Granting_an_identity_that_subsumes_an_existing_grantee_returns_a_non_blocking_broadening_advisory()
    {
        // An existing PERSON grantee whose resolved identity {sys:tenant=acme, sys:sub=alice} STRICTLY contains the team
        // identity {sys:tenant=acme}, seeded into the observed-identity typeahead store.
        var observed = new InMemoryObservedIdentityStore();
        await observed.SeenAsync(
            ObservedIdentity.GranteeKind.EnumValues.Person,
            JsonString.ParseValue("\"alice\""),
            JsonString.ParseValue("\"Alice\""),
            SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "acme"), new SecurityTag(SecurityShell.DefaultInternalPrefix + "sub", "alice")]),
            true,
            "administrator",
            default);

        await using Scoped host = await StartAsync(observed: observed, policy: new TenantIdentityPolicy());
        await EstablishAsync(host.Catalog, "flow", Acme);

        // Granting the narrower team identity {sys:tenant=acme} broadens administration to also admit alice (whose identity
        // contains it). The add SUCCEEDS (non-blocking, unlike the set-equal collision 409) and the response carries a
        // broadeningAdvisory naming the subsumed grantee.
        using Stj.JsonDocument added = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"acme"}""", Write, Acme));
        Stj.JsonElement advisory = added.RootElement.GetProperty("broadeningAdvisory");
        advisory.GetProperty("message").GetString().ShouldNotBeNullOrEmpty();
        advisory.GetProperty("subsumesGrantees").EnumerateArray()
            .Select(g => $"{g.GetProperty("kind").GetString()}:{g.GetProperty("value").GetString()}")
            .ShouldBe(["person:alice"]);

        // Granting a distinct identity that subsumes no existing grantee omits the advisory entirely.
        using Stj.JsonDocument globex = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Acme));
        globex.RootElement.TryGetProperty("broadeningAdvisory", out _).ShouldBeFalse();
    }

    // Each administrator is now a resolved-identity grant {digest, identity:[{dimension,value}], kind?, label?}; flatten its
    // identity grants to dimension=value strings (each administrator in these tests is a single-grant identity).
    [TestMethod]
    public async Task A_grantee_is_resolved_by_the_server_through_the_directory_on_add_and_transfer()
    {
        // ADR 0008: a write names a grantee ({kind, value}); the identity stored is the one the SERVER resolves. With a
        // directory configured, a team resolves to the directory's full identity (issuer + tenant), which the policy's own
        // kind-to-dimension mapping (sys:tenant only) could not produce, so the stored identity proves the directory leg ran.
        var directory = new FakeDirectory(new ResolvedPrincipal(GranteeKind.Team, "globex", "Globex", DirectoryIdentity("globex")));
        await using Scoped host = await StartAsync(directory: directory);
        await EstablishAsync(host.Catalog, "flow", Acme);

        using (Stj.JsonDocument added = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Acme)))
        {
            Grants(added).Order().ShouldBe(["iss=https://idp.example.com", "tenant=acme", "tenant=globex"]);
        }

        using (Stj.JsonDocument transferred = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[{"kind":"team","value":"globex"}]}""", Write, Acme)))
        {
            Grants(transferred).Order().ShouldBe(["iss=https://idp.example.com", "tenant=globex"]);
        }
    }

    [TestMethod]
    public async Task A_client_supplied_identity_is_never_stored()
    {
        // The write shape carries no identity: an identity a client smuggles into the body is ignored, and the grantee it
        // names is resolved by the server. Here the smuggled identity is acme's own, which would have made the add a no-op.
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        using Stj.JsonDocument added = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post,
            "/administrators/flow/members",
            """{"kind":"team","value":"globex","identity":[{"dimension":"tenant","value":"acme"}],"complete":true,"dimension":"tenant"}""",
            Write,
            Acme));
        Grants(added).Order().ShouldBe(["tenant=acme", "tenant=globex"]);
    }

    [TestMethod]
    public async Task An_unreachable_directory_refuses_the_write_rather_than_guessing_the_identity()
    {
        // The directory would have resolved the grantee; when it cannot be reached the server does NOT fall back to its own
        // coarser mapping (which would store a different identity from the one the picker showed): the write is refused
        // with the same 502 the explicit directory search reports, and the administrator set is unchanged.
        await using Scoped host = await StartAsync(directory: new BrokenDirectory());
        await EstablishAsync(host.Catalog, "flow", Acme);

        HttpResponseMessage refused = await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Acme);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        using (Stj.JsonDocument problem = await ReadJsonAsync(refused))
        {
            problem.RootElement.GetProperty("title").GetString().ShouldBe("Directory unavailable");
        }

        (await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[{"kind":"team","value":"globex"}]}""", Write, Acme)).StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/administrators/flow", Read, Acme));
        Grants(listed).ShouldBe(["tenant=acme"]);

        // A workflow is never directory-resolved, so it still resolves through the policy while the directory is down.
        using Stj.JsonDocument workflowAdded = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"workflow","value":"nightly"}""", Write, Acme));
        Grants(workflowAdded).Order().ShouldBe(["tenant=acme", "workflow=nightly"]);
    }

    // A directory identity richer than the policy's single-tag mapping: the issuer the directory stamps plus the tenant.
    private static SecurityTagSet DirectoryIdentity(string tenant)
        => SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "iss", "https://idp.example.com"), new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);

    private sealed class FakeDirectory(params ResolvedPrincipal[] principals) : IPrincipalDirectory
    {
        public ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<ResolvedPrincipal> matches = [.. principals.Where(p => p.Kind == kind && p.Value.StartsWith(query, StringComparison.Ordinal)).Take(limit)];
            return new ValueTask<IReadOnlyList<ResolvedPrincipal>>(matches);
        }
    }

    private sealed class BrokenDirectory : IPrincipalDirectory
    {
        public ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
            => throw new PrincipalDirectoryException("the directory returned 403 (Forbidden).");
    }

    private static IEnumerable<string> Grants(Stj.JsonDocument document)
        => document.RootElement.GetProperty("administrators").EnumerateArray()
            .SelectMany(a => a.GetProperty("identity").EnumerateArray()
                .Select(g => $"{g.GetProperty("dimension").GetString()}={g.GetProperty("value").GetString()}"));

    // The stable removal key: the digest of the administrator's internal resolved identity (the policy maps the grant
    // {tenant, value} to sys:tenant=value), matching what the list/add responses hand back.
    private static string Digest(string tenant)
        => SecurityIdentityDigest.Compute(SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]))!;

    [TestMethod]
    public async Task Workflow_administration_changes_emit_governance_audit_spans()
    {
        // §850: administrator-set changes reassign governance authority — adding a co-administrator, removing one, and
        // transferring the set are each a governance event with who changed which workflow's administration.
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using Scoped host = await StartAsync();
        await EstablishAsync(host.Catalog, "flow", Acme);

        (await host.SendJsonAsync(HttpMethod.Post, "/administrators/flow/members", """{"kind":"team","value":"globex"}""", Write, Acme)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, $"/administrators/flow/members/{Digest(Acme)}", Write, Globex)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendJsonAsync(HttpMethod.Put, "/administrators/flow", """{"administrators":[{"kind":"team","value":"acme"}]}""", Write, Globex)).StatusCode.ShouldBe(HttpStatusCode.OK);

        audit.Events("flow").ShouldBe([("workflow.add-administrator", "added"), ("workflow.remove-administrator", "removed"), ("workflow.transfer-administration", "transferred")]);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    // Publishes version 1 of a base id stamped with the founder's deployment identity (sys:tenant=<founder>), so the
    // founder becomes its sole administrator — mirroring what a real submitter's stamped identity would carry.
    private static async Task EstablishAsync(SecuredWorkflowCatalog catalog, string workflowId, string founder)
    {
        SecurityTagSet founderIdentity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", founder)]);
        await catalog.AddAsync(Package(workflowId), new CatalogOwner("Team", "team@example.com", null, null), default, founderIdentity, default);
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        return CatalogPackage.Build(workflow, []);
    }

    private static async Task<Scoped> StartAsync(bool withAdministratorStore = true, IObservedIdentityStore? observed = null, ControlPlaneRowSecurityPolicy? policy = null, IPrincipalDirectory? directory = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(
            new InMemoryWorkflowCatalogStore(),
            store,
            "ops",
            credentials: null,
            administrators: withAdministratorStore ? new InMemoryWorkflowAdministratorStore() : null);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddArazzoAuthenticationTelemetry();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: policy ?? new TenantIdentityPolicy(), observedIdentityStore: observed, principalDirectory: directory, auditor: GovernanceAuditor.CreateInMemory());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    /// <summary>A minimal scoped policy: an operator (full reach), with the principal's <c>tenant</c> claim stamped as
    /// the deployment identity <c>sys:tenant=&lt;tenant&gt;</c>, so a caller is recognized as an administrator and the
    /// base class's grantee mapping (a <c>team</c> grantee resolves to sys:tenant=value) round-trips.</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    /// <summary>A non-unique mapping for the collision test: the grantee values <c>real</c> and <c>alias</c> both resolve
    /// to the same identity (<c>sys:tenant=shared</c>); every other value resolves distinctly. The caller's <c>tenant</c>
    /// claim is stamped as its identity, exactly like <see cref="TenantIdentityPolicy"/>.</summary>
    private sealed class CollidingIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }

        public override SecurityTagSet ResolveGranteeIdentity(GranteeKind kind, string value)
        {
            // The collision is introduced by remapping the value, then delegating to the default kind-to-dimension mapping.
            string resolved = value is "real" or "alias" ? "shared" : value;
            return base.ResolveGranteeIdentity(kind, resolved);
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog) : IAsyncDisposable
    {
        public SecuredWorkflowCatalog Catalog => catalog;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope, identity);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope, string? identity = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope, identity);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope, string? identity)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
                }

                if (identity is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.IdentityHeader, identity);
                }

                return await client.SendAsync(request);
            }
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