// <copyright file="ControlPlaneAvailabilityRequestsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
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
/// Tests the control-plane availability-request ("promotion request") API (§7.8) over <c>/availabilityRequests</c>: a
/// principal who cannot make a version available directly raises a request, the target environment's administrators see it
/// in their inbox and approve (making the version available, readiness-gated) or deny, and the requester may withdraw their
/// own. Authorized by the per-environment administrator gate (and a requester check), not a global capability scope.
/// </summary>
[TestClass]
public sealed class ControlPlaneAvailabilityRequestsApiTests
{
    [TestMethod]
    public async Task A_request_is_submitted_seen_in_the_inbox_and_approved_making_the_version_available()
    {
        await using Scoped host = await StartAsync();

        // acme provisions 'production' (granting itself administration) and a source-less version (ready by vacuity).
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // globex (not an administrator of 'production') requests that checkout v1 be made available there → 201, pending.
        string id;
        using (Stj.JsonDocument submitted = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", """{"baseWorkflowId":"checkout","versionNumber":1,"environment":"production"}""", "globex")))
        {
            submitted.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
            submitted.RootElement.GetProperty("environment").GetString().ShouldBe("production");
            id = submitted.RootElement.GetProperty("id").GetString()!;
        }

        // globex sees it in their own list ("mine"); acme sees it in the approver inbox (scope=queue).
        using (Stj.JsonDocument mine = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/availabilityRequests", "globex")))
        {
            mine.RootElement.GetProperty("availabilityRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldBe([id]);
        }

        using (Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/availabilityRequests?scope=queue", "acme")))
        {
            inbox.RootElement.GetProperty("availabilityRequests").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldBe([id]);
        }

        // acme approves → 200, Approved.
        using (Stj.JsonDocument approved = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme")))
        {
            approved.RootElement.GetProperty("status").GetString().ShouldBe("Approved");
        }

        // The version is now available in 'production'.
        using Stj.JsonDocument byEnv = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/availability", "acme"));
        Stj.JsonElement entry = byEnv.RootElement.GetProperty("availability").EnumerateArray().Single();
        entry.GetProperty("baseWorkflowId").GetString().ShouldBe("checkout");
        entry.GetProperty("versionNumber").GetInt32().ShouldBe(1);
    }

    [TestMethod]
    public async Task A_request_can_be_denied_by_an_environment_administrator()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        string id = await host.SubmitAsync("checkout", 1, "production", "globex");
        using Stj.JsonDocument denied = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/deny", "acme"));
        denied.RootElement.GetProperty("status").GetString().ShouldBe("Denied");

        // The version was NOT made available.
        using Stj.JsonDocument byEnv = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/availability", "acme"));
        byEnv.RootElement.GetProperty("availability").EnumerateArray().ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_requester_never_decides_their_own_request_even_as_an_administrator()
    {
        await using Scoped host = await StartAsync();

        // acme administers 'production' (creator becomes administrator) and then raises a request ITSELF - the
        // administrator-requester case the independent-decision bar exists for.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");
        string id = await host.SubmitAsync("checkout", 1, "production", "acme");

        // Approve and deny are both refused 403 own-request: a decision must come from a second administrator.
        HttpResponseMessage approve = await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme");
        approve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using (Stj.JsonDocument problem = await ReadJsonAsync(approve))
        {
            problem.RootElement.GetProperty("type").GetString()!.ShouldContain("own-request");
        }

        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/deny", "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The request is untouched by the refused decisions (still Pending), and the requester's own exit - withdraw -
        // still works.
        using (Stj.JsonDocument withdrawn = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/withdraw", "acme")))
        {
            withdrawn.RootElement.GetProperty("status").GetString().ShouldBe("Withdrawn");
        }
    }

    [TestMethod]
    public async Task Submitting_stamps_the_requester_display_name_when_the_principal_resolves_one()
    {
        await using Scoped host = await StartAsync();

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // A principal whose identity resolves a display name gets it stamped at submit, so the approver queue reads as a
        // person, and the stamp survives into the approver's queue view verbatim.
        using (Stj.JsonDocument named = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", """{"baseWorkflowId":"checkout","versionNumber":1,"environment":"production"}""", "globex", name: "Wanda Reconcile")))
        {
            named.RootElement.GetProperty("requesterLabel").GetString().ShouldBe("Wanda Reconcile");
        }

        using (Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/availabilityRequests?scope=queue", "acme")))
        {
            inbox.RootElement.GetProperty("availabilityRequests").EnumerateArray().Single().GetProperty("requesterLabel").GetString().ShouldBe("Wanda Reconcile");
        }

        // A principal with no resolvable name (a bare service principal) omits the property — absent, not null or empty.
        using (Stj.JsonDocument unnamed = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", """{"baseWorkflowId":"checkout","versionNumber":1,"environment":"production"}""", "initech")))
        {
            unnamed.RootElement.TryGetProperty("requesterLabel", out _).ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task Only_the_requester_can_withdraw_and_only_an_administrator_can_decide()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        string id = await host.SubmitAsync("checkout", 1, "production", "globex");

        // A non-administrator cannot approve (403); a non-requester cannot withdraw (403).
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/withdraw", "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The requester withdraws → 200, Withdrawn; deciding the withdrawn request then conflicts (409).
        using (Stj.JsonDocument withdrawn = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/withdraw", "globex")))
        {
            withdrawn.RootElement.GetProperty("status").GetString().ShouldBe("Withdrawn");
        }

        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Approval_is_readiness_gated()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A version referencing a source with no credential in 'production' is not ready.
        await host.SeedVersionAsync("billing", "acme", "payments");
        string id = await host.SubmitAsync("billing", 1, "production", "globex");

        // Approval is blocked until the source is ready (409), then succeeds (200) once a credential exists.
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/credentials",
            """{"sourceName":"payments","environment":"production","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://payments#1"}]}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Approval_is_evidence_gated_where_the_environment_requires_it()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production","requireEvidence":true}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // An unevidenced version: approval is refused (409, evidence-required) even with credentials ready (source-less).
        await host.SeedVersionAsync("checkout", "acme");
        string id = await host.SubmitAsync("checkout", 1, "production", "globex");
        HttpResponseMessage refused = await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{id}/approve", "acme");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using (Stj.JsonDocument problem = await ReadJsonAsync(refused))
        {
            problem.RootElement.GetProperty("type").GetString()!.ShouldEndWith("evidence-required");
        }

        // A green-suite version approves — the same request flow, now evidence-ready.
        await host.SeedEvidencedVersionAsync("payments", "acme");
        string green = await host.SubmitAsync("payments", 1, "production", "globex");
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{green}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Submitting_requires_authentication_and_an_existing_environment_and_version()
    {
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        // Unauthenticated → 401.
        (await host.SendAnonymousAsync(HttpMethod.Get, "/availabilityRequests")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // An unknown environment or version → 400 (the contract has no 404 on submit).
        (await host.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", """{"baseWorkflowId":"checkout","versionNumber":1,"environment":"nowhere"}""", "globex")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", """{"baseWorkflowId":"checkout","versionNumber":99,"environment":"production"}""", "globex")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Promotion_request_decisions_and_the_own_request_refusal_emit_governance_audit_spans()
    {
        // §850: the promotion-request decisions (approve/deny) are audited with who decided which request, and the
        // independent-decision refusal — a requester deciding their own promotion request — leaves a trace too.
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await host.SeedVersionAsync("checkout", "acme");

        string approved = await host.SubmitAsync("checkout", 1, "production", "globex");
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{approved}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        string denied = await host.SubmitAsync("checkout", 1, "production", "globex");
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{denied}/deny", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // acme raises its own request, then tries to approve it — refused by the independent-decision bar.
        string own = await host.SubmitAsync("checkout", 1, "production", "acme");
        (await host.SendAsync(HttpMethod.Post, $"/availabilityRequests/{own}/approve", "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        audit.Outcomes(approved).ShouldBe(["approved"]);
        audit.Outcomes(denied).ShouldBe(["denied"]);
        audit.Outcomes(own).ShouldBe(["refused-own-request"]);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");

        // credentials: null skips the §13 catalog-time gate so a version may reference an as-yet-uncredentialed source —
        // the §7.8 readiness gate at approval time is what we exercise.
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeTenantSubAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantSubAuthHandler>(ScopeTenantSubAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog);
    }

    // Maps X-Tenant to both the deployment governance identity (sys:tenant=<t>) and the requester subject (sub=<t>), with
    // full read reach, so create-grants-admin + the administrator gate AND the createdBy / "mine" requester key are driven
    // per caller.
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), tenant);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string tenant, string? name = null)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant, name);

        public async Task<HttpResponseMessage> SendAnonymousAsync(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path);
            return await client.SendAsync(request);
        }

        public async Task<string> SubmitAsync(string baseWorkflowId, int versionNumber, string environment, string tenant)
        {
            HttpResponseMessage response = await this.SendJsonAsync(HttpMethod.Post, "/availabilityRequests", $$"""{"baseWorkflowId":"{{baseWorkflowId}}","versionNumber":{{versionNumber}},"environment":"{{environment}}"}""", tenant);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            using Stj.JsonDocument document = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("id").GetString()!;
        }

        public async Task SeedVersionAsync(string workflowId, string tenant, params string[] sourceNames)
        {
            SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);
            await catalog.AddAsync(Package(workflowId, sourceNames), new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);
        }

        // Seeds a source-less version whose package embeds a green attested suite (§4.6 publish evidence).
        public async Task SeedEvidencedVersionAsync(string workflowId, string tenant)
        {
            SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);
            byte[] scenarios = Encoding.UTF8.GetBytes("""[{"name":"happy","expect":{"outcome":"completed"}}]""");
            byte[] evidence = Encoding.UTF8.GetBytes("""{"packageHash":"seed","engineVersion":"test","at":"2026-07-05T00:00:00Z","suite":{"total":1,"passed":1,"failed":0},"scenarios":[{"name":"happy","passed":true,"outcome":"completed"}]}""");
            ReadOnlyMemory<byte> package = CatalogPackage.Build(Workflow(workflowId), [], scenarios, evidence);
            await catalog.AddAsync(package, new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private static ReadOnlyMemory<byte> Package(string workflowId, string[] sourceNames)
        {
            byte[] sourceDoc = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Source","version":"1.0"},"paths":{}}""");
            var sources = sourceNames
                .Select(n => new KeyValuePair<string, byte[]>(n, sourceDoc))
                .ToList();
            return CatalogPackage.Build(Workflow(workflowId, sourceNames), sources);
        }

        private static byte[] Workflow(string workflowId, string[]? sourceNames = null)
        {
            string descriptions = string.Join(",", (sourceNames ?? []).Select(n => $$"""{ "name": "{{n}}", "url": "./{{n}}.json", "type": "openapi" }"""));
            return Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [{{descriptions}}],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string tenant, string? name = null)
        {
            using (request)
            {
                // Any X-Scopes value authenticates (the availability-request endpoints require authentication, not a
                // specific scope); X-Tenant becomes both the governance identity and the requester subject.
                request.Headers.Add(ScopeTenantSubAuthHandler.ScopeHeader, "authenticated");
                request.Headers.Add(ScopeTenantSubAuthHandler.TenantHeader, tenant);
                if (name is not null)
                {
                    request.Headers.Add(ScopeTenantSubAuthHandler.NameHeader, name);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeTenantSubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesTenantSub";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";
        public const string NameHeader = "X-Name";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey(ScopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // The presence of X-Scopes authenticates; the caller is granted the full capability-scope set the harness's
            // scoped endpoints (/environments, /credentials) require — the availability-request endpoints themselves are
            // auth-only, and the authorization actually under test is the per-tenant administrator / requester gate.
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:write availability:read availability:write credentials:write"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            // An optional display name (X-Name) drives Identity.Name, mirroring an IdP that resolves one; its absence
            // mirrors a bare service principal.
            if (this.Request.Headers.TryGetValue(NameHeader, out Microsoft.Extensions.Primitives.StringValues name))
            {
                identity.AddClaim(new Claim(ClaimsIdentity.DefaultNameClaimType, name.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}