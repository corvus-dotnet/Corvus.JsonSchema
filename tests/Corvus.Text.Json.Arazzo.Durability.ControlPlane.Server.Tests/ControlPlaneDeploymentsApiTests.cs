// <copyright file="ControlPlaneDeploymentsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
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
/// Tests the control-plane deployments API (ADR 0055): the read-only observation of a workflow version's serverless
/// deployments per (environment, runtime target) over <c>/catalog/{id}/versions/{n}/deployments…</c>. The deploy itself
/// runs on the runner (ADR 0059); this surface only reports the state the control plane records, so the tests seed the
/// deployment store directly (there is no enqueue endpoint) and exercise the REST read vertical: list, count, poll a
/// target, status filtering, and reach/scope gating.
/// </summary>
[TestClass]
public sealed class ControlPlaneDeploymentsApiTests
{
    private const string Read = "catalog:read";

    [TestMethod]
    public async Task A_versions_deployments_are_listed_polled_and_counted()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // One target reaches Deployed (carrying a function URL); a second target stays Queued. Seed the Deployed target
        // first: the claim primitive takes the oldest queued deployment across the store, so no other queued target must
        // exist when it is claimed.
        await host.SeedDeployedAsync("checkout", 1, "production", "linux-x64", "https://checkout-prod.example/invoke");
        await host.SeedQueuedAsync("checkout", 1, "production", "linux-arm64");

        // Poll the Deployed target → 200, Deployed, carrying its function URL.
        using (Stj.JsonDocument deployed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments/production/linux-x64", Read, "acme")))
        {
            deployed.RootElement.GetProperty("status").GetString().ShouldBe("Deployed");
            deployed.RootElement.GetProperty("baseWorkflowId").GetString().ShouldBe("checkout");
            deployed.RootElement.GetProperty("versionNumber").GetInt32().ShouldBe(1);
            deployed.RootElement.GetProperty("environment").GetString().ShouldBe("production");
            deployed.RootElement.GetProperty("runtimeIdentifier").GetString().ShouldBe("linux-x64");
            deployed.RootElement.GetProperty("functionUrl").GetString().ShouldBe("https://checkout-prod.example/invoke");
        }

        // Poll the Queued target → 200, Queued, no function URL yet.
        using (Stj.JsonDocument queued = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments/production/linux-arm64", Read, "acme")))
        {
            queued.RootElement.GetProperty("status").GetString().ShouldBe("Queued");
            queued.RootElement.TryGetProperty("functionUrl", out _).ShouldBeFalse();
        }

        // List → the two targets (order-independent: same-tick seeds tie-break by id).
        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments", Read, "acme")))
        {
            list.RootElement.GetProperty("deployments").EnumerateArray()
                .Select(e => e.GetProperty("runtimeIdentifier").GetString())
                .OrderBy(r => r, StringComparer.Ordinal)
                .ShouldBe(["linux-arm64", "linux-x64"]);
        }

        // Count → 2, uncapped.
        using (Stj.JsonDocument count = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments/count", Read, "acme")))
        {
            count.RootElement.GetProperty("count").GetInt32().ShouldBe(2);
            count.RootElement.GetProperty("capped").GetBoolean().ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task The_list_and_count_filter_by_status()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");
        await host.SeedDeployedAsync("checkout", 1, "production", "linux-x64", "https://checkout-prod.example/invoke");
        await host.SeedQueuedAsync("checkout", 1, "production", "linux-arm64");

        // One Deployed, one Queued, none Failed.
        using (Stj.JsonDocument deployed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments?status=Deployed", Read, "acme")))
        {
            deployed.RootElement.GetProperty("deployments").GetArrayLength().ShouldBe(1);
        }

        using (Stj.JsonDocument queued = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments?status=Queued", Read, "acme")))
        {
            queued.RootElement.GetProperty("deployments").GetArrayLength().ShouldBe(1);
        }

        using (Stj.JsonDocument failed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments?status=Failed", Read, "acme")))
        {
            failed.RootElement.GetProperty("deployments").GetArrayLength().ShouldBe(0);
        }

        using (Stj.JsonDocument countDeployed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments/count?status=Deployed", Read, "acme")))
        {
            countDeployed.RootElement.GetProperty("count").GetInt32().ShouldBe(1);
        }
    }

    [TestMethod]
    public async Task An_unknown_version_or_target_is_not_found()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // List for a non-existent version → 404.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/99/deployments", Read, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Poll a target that has no deployment → 404.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments/production/linux-x64", Read, "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_read_scope_is_enforced()
    {
        await using Scoped host = await StartAsync();
        await host.SeedVersionAsync("checkout", "acme");

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/catalog/checkout/versions/1/deployments", null, "acme")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");

        // The catalog skips its §13 catalog-time credential gate (credentials: null), so a source-less version adds freely.
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());
        var deployments = new InMemoryWorkflowDeploymentStore();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeTenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantAuthHandler>(ScopeTenantAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), workflowDeploymentStore: deployments);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), catalog, deployments);
    }

    /// <summary>Maps the bearer-free test headers to a deployment identity, with full read reach.</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, SecuredWorkflowCatalog catalog, InMemoryWorkflowDeploymentStore deployments) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope, tenant);

        public async Task SeedVersionAsync(string workflowId, string tenant)
        {
            SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)]);
            await catalog.AddAsync(Package(workflowId), new CatalogOwner("Team", "team@example.com", null, null), default, identity, default);
        }

        /// <summary>Seeds a deployment that reaches Deployed carrying a function URL. Seed this before any other queued
        /// target: the claim primitive takes the oldest queued deployment across the store.</summary>
        public async Task SeedDeployedAsync(string workflowId, int versionNumber, string environment, string runtimeIdentifier, string functionUrl)
        {
            using (ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(workflowId, versionNumber, environment, runtimeIdentifier))
            {
                (await deployments.EnqueueAsync(draft.RootElement, "seed", default)).Dispose();
            }

            using ParsedJsonDocument<WorkflowDeployment>? claimed = await deployments.ClaimNextQueuedAsync("seed-worker", TimeSpan.FromMinutes(5), default);
            claimed.ShouldNotBeNull();
            string id = claimed.RootElement.IdValue;
            WorkflowEtag etag = claimed.RootElement.EtagValue;
            (await deployments.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, FunctionUrl: functionUrl), etag, default))?.Dispose();
        }

        /// <summary>Seeds a deployment that stays Queued (no worker claims it here).</summary>
        public async Task SeedQueuedAsync(string workflowId, int versionNumber, string environment, string runtimeIdentifier)
        {
            using ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(workflowId, versionNumber, environment, runtimeIdentifier);
            (await deployments.EnqueueAsync(draft.RootElement, "seed", default)).Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private static ReadOnlyMemory<byte> Package(string workflowId)
            => CatalogPackage.Build(Workflow(workflowId), []);

        private static byte[] Workflow(string workflowId)
            => Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope, string tenant)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeTenantAuthHandler.ScopeHeader, scope);
                    request.Headers.Add(ScopeTenantAuthHandler.TenantHeader, tenant);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeTenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesTenant";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues scopes))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", scopes.ToString()));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}