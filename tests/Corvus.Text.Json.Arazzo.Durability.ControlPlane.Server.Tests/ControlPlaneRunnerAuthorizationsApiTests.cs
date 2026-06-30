// <copyright file="ControlPlaneRunnerAuthorizationsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
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
/// Tests the control-plane runner-authorization API (design §5.5) over <c>/environments/{name}/runners</c> and
/// <c>/runnerAuthorizations</c>: a runner enters <c>Pending</c> on self-registration (seeded directly into the store, since
/// registration is not one of these endpoints) and is dispatchable only once an administrator of the target environment
/// authorizes it; authorization is revocable. The roster list and the approver inbox span the environments the caller
/// administers. Authorized by the per-environment administrator gate (200/403/404/409), not a global capability scope.
/// </summary>
[TestClass]
public sealed class ControlPlaneRunnerAuthorizationsApiTests
{
    [TestMethod]
    public async Task Authorizing_a_pending_runner_as_an_administrator_makes_it_authorized()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions 'production', granting itself administration of it.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument authorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        authorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        authorized.RootElement.GetProperty("runnerId").GetString().ShouldBe("runner-1");
        authorized.RootElement.GetProperty("decidedBy").GetString().ShouldBe("acme");
    }

    [TestMethod]
    public async Task Authorizing_an_already_authorized_runner_is_idempotent()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A second authorize returns the existing Authorized record unchanged.
        using Stj.JsonDocument again = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        again.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Authorizing_as_a_non_administrator_of_the_environment_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions (and administers) 'production'; globex administers nothing here.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Authorizing_for_an_unknown_environment_is_not_found()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // 'nowhere' does not exist / is outside reach → 404 (before the runner record is consulted).
        (await host.SendAsync(HttpMethod.Post, "/environments/nowhere/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Authorizing_a_runner_that_never_registered_is_not_found()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // The environment exists and acme administers it, but no Pending record exists for 'runner-unknown' → 404.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-unknown/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Revoking_an_authorized_runner_as_an_administrator_makes_it_revoked()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument revoked = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme"));
        revoked.RootElement.GetProperty("status").GetString().ShouldBe("Revoked");
    }

    [TestMethod]
    public async Task Listing_an_environments_runners_as_an_administrator_lists_the_seeded_runner_and_filters_by_status()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-pending", "runner", default);
        await runnerAuth.EnsurePendingAsync("production", "runner-authorized", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Authorize one of the two so the status filter has something to discriminate.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-authorized/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The unfiltered roster lists both runners.
        using (Stj.JsonDocument all = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/runners", "acme")))
        {
            all.RootElement.GetProperty("authorizations").EnumerateArray().Select(r => r.GetProperty("runnerId").GetString()).OrderBy(id => id).ShouldBe(["runner-authorized", "runner-pending"]);
        }

        // ?status=Pending filters to the still-pending runner only.
        using Stj.JsonDocument pending = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/runners?status=Pending", "acme"));
        Stj.JsonElement entry = pending.RootElement.GetProperty("authorizations").EnumerateArray().Single();
        entry.GetProperty("runnerId").GetString().ShouldBe("runner-pending");
        entry.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [TestMethod]
    public async Task Listing_an_environments_runners_as_a_non_administrator_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Get, "/environments/production/runners", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task The_approver_inbox_returns_pending_authorizations_for_the_environments_the_caller_administers()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme administers 'production'; the inbox (no environment, defaulting to Pending) surfaces its pending runner.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/runnerAuthorizations", "acme"));
        Stj.JsonElement entry = inbox.RootElement.GetProperty("authorizations").EnumerateArray().Single();
        entry.GetProperty("environment").GetString().ShouldBe("production");
        entry.GetProperty("runnerId").GetString().ShouldBe("runner-1");
        entry.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [TestMethod]
    public async Task The_approver_inbox_is_empty_for_a_caller_who_administers_nothing()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions (and administers) 'production'; globex administers nothing, so its inbox is empty.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/runnerAuthorizations", "globex"));
        inbox.RootElement.GetProperty("authorizations").EnumerateArray().ShouldBeEmpty();
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(IEnvironmentRunnerAuthorizationStore runnerAuthorizations)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), environmentRunnerAuthorizationStore: runnerAuthorizations);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
    }

    // Maps X-Tenant to both the deployment governance identity (sys:tenant=<t>) and the requester subject (sub=<t>), with
    // full read reach, so create-grants-admin + the administrator gate AND the decidedBy audit actor are driven per caller.
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), tenant);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string tenant)
        {
            using (request)
            {
                // Any X-Scopes value authenticates (the runner-authorization endpoints require authentication, not a
                // specific scope); X-Tenant becomes both the governance identity and the deciding subject.
                request.Headers.Add(ScopeTenantSubAuthHandler.ScopeHeader, "authenticated");
                request.Headers.Add(ScopeTenantSubAuthHandler.TenantHeader, tenant);
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey(ScopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // The presence of X-Scopes authenticates; the caller is granted the full capability-scope set the harness's
            // scoped endpoints require (creating an environment and authorize/revoke need environments:write; the runner
            // roster list needs environments:read) — the authorization actually under test is the per-tenant environment
            // administrator gate, not which scope is held.
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write availability:read availability:write credentials:write"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}