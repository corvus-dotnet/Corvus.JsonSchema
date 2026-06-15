// <copyright file="ControlPlaneRowSecurityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
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
/// Tests control-plane row security (§14.2/§14.3): a deployment-supplied <see cref="ControlPlaneRowSecurityPolicy"/>
/// scopes every list/search and purge to the rows the principal may see and reports a row in another tenant as
/// not found (never forbidden, so its existence is not disclosed). A principal with no tenant claim (a service
/// operator) is unrestricted.
/// </summary>
[TestClass]
public sealed class ControlPlaneRowSecurityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Listing_runs_is_scoped_to_the_principals_tenant()
    {
        await using Scoped host = await StartAsync();
        await SeedRunAsync(host.Store, "run-acme", host.Clock, new SecurityTag("tenant", "acme"));
        await SeedRunAsync(host.Store, "run-globex", host.Clock, new SecurityTag("tenant", "globex"));
        await SeedRunAsync(host.Store, "run-untagged", host.Clock);

        using Stj.JsonDocument doc = await ReadJsonAsync(await host.GetAsync("/runs", tenant: "acme"));

        doc.RootElement.GetProperty("runs").EnumerateArray().Select(r => r.GetProperty("id").GetString())
            .ShouldBe(["run-acme"]);
    }

    [TestMethod]
    public async Task An_operator_with_no_tenant_claim_sees_every_run()
    {
        await using Scoped host = await StartAsync();
        await SeedRunAsync(host.Store, "run-acme", host.Clock, new SecurityTag("tenant", "acme"));
        await SeedRunAsync(host.Store, "run-globex", host.Clock, new SecurityTag("tenant", "globex"));

        using Stj.JsonDocument doc = await ReadJsonAsync(await host.GetAsync("/runs", tenant: null));

        doc.RootElement.GetProperty("runs").GetArrayLength().ShouldBe(2);
    }

    [TestMethod]
    public async Task A_run_in_another_tenant_is_reported_as_not_found()
    {
        await using Scoped host = await StartAsync();
        await SeedRunAsync(host.Store, "run-globex", host.Clock, new SecurityTag("tenant", "globex"));

        // acme may not see globex's run: it is a 404 (non-disclosing), not a 403.
        (await host.GetAsync("/runs/run-globex", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // …but the operator (and the owning tenant) can.
        (await host.GetAsync("/runs/run-globex", tenant: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.GetAsync("/runs/run-globex", tenant: "globex")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Cancelling_another_tenants_run_is_not_found_and_does_not_mutate_it()
    {
        await using Scoped host = await StartAsync();
        await FaultRunAsync(host.Store, "run-globex", host.Clock, new SecurityTag("tenant", "globex"));

        HttpResponseMessage cancelled = await host.PostAsync("/runs/run-globex/cancel", """{"reason":"nope"}""", tenant: "acme");

        // The gate is applied before mutating, so the run is untouched.
        cancelled.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.GetAsync("/runs/run-globex", tenant: null));
        doc.RootElement.GetProperty("status").GetString().ShouldBe("Faulted");
    }

    [TestMethod]
    public async Task Purge_reaps_only_the_principals_tenant_runs()
    {
        await using Scoped host = await StartAsync();
        await CompleteRunAsync(host.Store, "done-acme", host.Clock, new SecurityTag("tenant", "acme"));
        await CompleteRunAsync(host.Store, "done-globex", host.Clock, new SecurityTag("tenant", "globex"));

        string olderThan = Uri.EscapeDataString((T0 + TimeSpan.FromHours(1)).ToString("O"));
        var request = new HttpRequestMessage(new HttpMethod("PURGE"), $"/runs?olderThan={olderThan}");
        HttpResponseMessage response = await host.SendAsync(request, tenant: "acme");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(response))
        {
            doc.RootElement.GetProperty("purgedCount").GetInt32().ShouldBe(1);
        }

        // acme's run is gone; globex's survives the tenant-scoped purge.
        (await host.GetAsync("/runs/done-acme", tenant: null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.GetAsync("/runs/done-globex", tenant: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Read_reach_and_write_reach_are_independent()
    {
        // A principal that may read everything but write nothing: a run is gettable (200), but cancelling/deleting
        // it is forbidden (403) — the existence was already disclosed by the read, so it is not masked as 404.
        await using Scoped host = await StartAsync(new ReadAnyWriteNonePolicy());
        await FaultRunAsync(host.Store, "run-1", host.Clock, new SecurityTag("tenant", "acme"));

        (await host.GetAsync("/runs/run-1", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.PostAsync("/runs/run-1/cancel", """{"reason":"x"}""", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/runs/run-1");
        (await host.SendAsync(delete, tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The run is untouched by the denied writes — still readable and still Faulted.
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.GetAsync("/runs/run-1", tenant: "acme"));
        doc.RootElement.GetProperty("status").GetString().ShouldBe("Faulted");

        // Catalog writes are gated the same way: the version is readable (200) but not updatable (403).
        await host.Catalog.AddAsync(Package("flow"), Owner, default, [new SecurityTag("tenant", "acme")], default);
        (await host.GetAsync("/catalog/flow/versions/1", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var patch = new HttpRequestMessage(HttpMethod.Patch, "/catalog/flow/versions/1")
        {
            Content = new StringContent("""{ "status": "Obsolete" }""", Encoding.UTF8, "application/json"),
        };
        (await host.SendAsync(patch, tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Searching_the_catalog_is_scoped_to_the_principals_tenant()
    {
        await using Scoped host = await StartAsync();
        await host.Catalog.AddAsync(Package("acme-flow"), Owner, default, [new SecurityTag("tenant", "acme")], default);
        await host.Catalog.AddAsync(Package("globex-flow"), Owner, default, [new SecurityTag("tenant", "globex")], default);

        using Stj.JsonDocument doc = await ReadJsonAsync(await host.GetAsync("/catalog", tenant: "acme"));

        // The catalog projects the base id "acme-flow" to the versioned workflow id "acme-flow-v1".
        doc.RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => v.GetProperty("workflowId").GetString())
            .ShouldBe(["acme-flow-v1"]);
    }

    [TestMethod]
    public async Task A_catalog_version_in_another_tenant_is_reported_as_not_found()
    {
        await using Scoped host = await StartAsync();
        await host.Catalog.AddAsync(Package("globex-flow"), Owner, default, [new SecurityTag("tenant", "globex")], default);

        (await host.GetAsync("/catalog/globex-flow/versions/1", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.GetAsync("/catalog/globex-flow/versions/1", tenant: "globex")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The package and source documents are gated the same way.
        (await host.GetAsync("/catalog/globex-flow/versions/1/package", tenant: "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly CatalogOwner Owner = new("Team", "team@example.com");

    private static byte[] Package(string id)
        => WorkflowPackage.Pack(Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}"""), []);

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task SeedRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock, params SecurityTag[] security)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock, securityTags: security);
        await run.EnqueueAsync(default);
    }

    private static async Task CompleteRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock, params SecurityTag[] security)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock, securityTags: security);
        await run.CompleteAsync(default, default);
    }

    private static async Task FaultRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock, params SecurityTag[] security)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock, securityTags: security);
        await run.FaultAsync("step1", attempt: 1, "boom", default);
    }

    private static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
    {
        await run.CompleteAsync(default, ct);
        return WorkflowRunResultKind.Completed;
    }

    private static async Task<Scoped> StartAsync(ControlPlaneRowSecurityPolicy? policy = null)
    {
        var clock = new FixedClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var management = new WorkflowManagementClient(store, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(clock), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // The deployment owns authentication: a header-driven scheme stands in, carrying the tenant claim the
        // row-security policy resolves. IHttpContextAccessor lets the policy read the current principal.
        builder.Services
            .AddAuthentication(TenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TenantAuthHandler>(TenantAuthHandler.SchemeName, _ => { });
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), rowSecurity: policy ?? new TenantRowSecurityPolicy());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), store, catalog, clock);
    }

    private sealed class Scoped(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store, IWorkflowCatalogClient catalog, TimeProvider clock) : IAsyncDisposable
    {
        public InMemoryWorkflowStateStore Store => store;

        public IWorkflowCatalogClient Catalog => catalog;

        public TimeProvider Clock => clock;

        public Task<HttpResponseMessage> GetAsync(string path, string? tenant)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Get, path), tenant);

        public Task<HttpResponseMessage> PostAsync(string path, string body, string? tenant)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant);

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? tenant)
        {
            using (request)
            {
                if (tenant is not null)
                {
                    request.Headers.Add(TenantAuthHandler.TenantHeader, tenant);
                }

                return await client.SendAsync(request);
            }
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// A test authentication scheme: an <c>X-Tenant</c> header authenticates the caller and carries its tenant as
    /// a <c>tenant</c> claim. No header means a service operator (an authenticated principal with no tenant).
    /// </summary>
    private sealed class TenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Tenant";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(SchemeName);
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                identity.AddClaim(new Claim("tenant", values.ToString()));
            }

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    /// <summary>
    /// A deployment row-security policy: a principal carrying a <c>tenant</c> claim is scoped to rows tagged with
    /// that tenant (<c>tenant == $claim.tenant</c>); a principal with no tenant claim is an unrestricted operator.
    /// </summary>
    /// <summary>Read everything, write/purge nothing — exercises independent per-verb reach (§14.2).</summary>
    private sealed class ReadAnyWriteNonePolicy : ControlPlaneRowSecurityPolicy
    {
        // A rule comparing two distinct literals is never satisfied, so it denies every row.
        private static readonly SecurityFilter DenyAll = new(
            [SecurityRule.Compile("'x' == 'y'")],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

        public override AccessContext Resolve(ClaimsPrincipal? principal) => new(readReach: null, writeReach: DenyAll, purgeReach: DenyAll);
    }

    private sealed class TenantRowSecurityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            if (tenant is null)
            {
                return AccessContext.System;
            }

            var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["tenant"] = [tenant] };
            return AccessContext.Uniform(new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], claims));
        }
    }
}