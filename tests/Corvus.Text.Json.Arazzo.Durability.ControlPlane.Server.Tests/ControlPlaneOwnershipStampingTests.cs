// <copyright file="ControlPlaneOwnershipStampingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
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
/// Ownership is stamped independently of the reach policy (ADR 0065, phase A). Reach and ownership are different
/// questions: a deployment may decline to isolate reach and still need to know which owner group created a row. The
/// earlier form of this derived ownership solely from the row-security policy, which ADR 0016 forbids in
/// <see cref="ControlPlaneSecurityMode.ScopesOnly"/> — so ownership was undeterminable in a mode that authenticates,
/// and the tenancy invariant naming that mode could not be evaluated there at all.
/// </summary>
/// <remarks>
/// These tests read ownership through its consequence rather than through the stored tag. Create-grants-admin (§7.7)
/// stamps the creator's resolved identity as the environment's sole administrator, so "the deployment knows who owns
/// this" is observable as "a different owner group cannot administer it". Asserting the tag directly would pass on a
/// tag that no gate ever consults.
/// </remarks>
[TestClass]
public sealed class ControlPlaneOwnershipStampingTests
{
    [TestMethod]
    public async Task An_owner_claim_establishes_administration_where_reach_is_unrestricted()
    {
        // ScopesOnly authenticates and grants System reach. The creator's owner group comes from the principal, so
        // create-grants-admin has an identity to grant and the creator can administer what it made.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.PutAsync("/environments/production", """{"displayName":"Production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_second_owner_group_cannot_administer_the_first_group_s_environment()
    {
        // The load-bearing half. Reach is unrestricted, so the second group SEES the environment — that is the mode's
        // documented posture. What ownership buys is that seeing it is not administering it.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.GetAsync("/environments/production", "zeus")).StatusCode.ShouldBe(
            HttpStatusCode.OK, "ScopesOnly does not isolate reach, so the second group can still read the row");
        (await host.PutAsync("/environments/production", """{"displayName":"Seized"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.DeleteAsync("/environments/production", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_re_tag_cannot_drop_the_owner_group()
    {
        // ADR 0065 requires ownership immutability asserted rather than assumed from the schema's documentation. The
        // path that could actually move it is a re-tag by the one caller who passes the governance gate: management
        // tags are replace-or-carry, so an update supplying only user labels must not overwrite the owner stamp with
        // them. Asserting instead that a NON-administrator cannot move ownership would pass vacuously, since that
        // caller is refused before any tag is read.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.PutAsync("/environments/production", """{"displayName":"Renamed","managementTags":[{"key":"team","value":"platform"}]}""", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // The owner stamp survived the re-tag, so acme still administers it and zeus still does not.
        (await host.PutAsync("/environments/production", """{"displayName":"Still ours"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.PutAsync("/environments/production", """{"displayName":"Seized"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task A_client_supplied_owner_tag_cannot_forge_an_owner_group()
    {
        // The claim carrying ownership is a trust boundary, so it takes the reserved internal prefix. A caller that
        // supplies the prefixed tag itself is rejected outright rather than quietly joining another owner group.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage forged = await host.PostAsync(
            "/environments",
            """{"name":"seized","managementTags":[{"key":"sys:tenant","value":"acme"}]}""",
            "zeus");

        forged.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.PutAsync("/environments/production", """{"displayName":"Seized"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task An_authenticated_principal_carrying_no_owner_claim_has_no_owner_group()
    {
        // The documented limit, pinned so it cannot drift silently. A deployment that authenticates but publishes no
        // owner claim cannot tell owner groups apart, so it stamps none and establishes no administration — every
        // caller is then equally a non-administrator. This is the posture the tenancy invariant reports as vacuous;
        // making it stamp the subject instead would make every individual user a tenant.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", tenant: null)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.PutAsync("/environments/production", """{"displayName":"x"}""", tenant: null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task The_owner_stamp_describes_back_as_a_grant()
    {
        // Stamping an owner group falsified the premise the identity conventions short-circuit on ("no policy means no
        // internal tags"), and the members that read them — is this key internal, strip its prefix, describe it as a
        // grant — would each have answered as though nothing were stamped. Whoami runs the stamp back through that
        // whole path, so a stamp the deployment no longer recognises as its own shows up here as an empty identity.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        using Stj.JsonDocument me = Stj.JsonDocument.Parse(
            await (await host.GetAsync("/identity/whoami", "acme")).Content.ReadAsStringAsync());

        Stj.JsonElement identity = me.RootElement.GetProperty("identity");
        identity.GetArrayLength().ShouldBe(1);
        identity[0].GetProperty("dimension").GetString().ShouldBe("tenant", "the reserved prefix is stripped for the operator-facing view");
        identity[0].GetProperty("value").GetString().ShouldBe("acme");
    }

    [TestMethod]
    public async Task A_configured_reach_policy_still_owns_the_stamp()
    {
        // The fallback must not displace a deployment that HAS a policy: Scoped resolves ownership through the policy
        // exactly as before, so the two paths cannot disagree about who owns a row.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, new PolicyStampsFixedOwner());

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // The policy stamps a fixed owner for EVERY principal, so the second group administers it too. That is the
        // policy's decision, and the fallback has not overridden it with the differing claim value.
        (await host.PutAsync("/environments/production", """{"displayName":"Shared"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<Host> StartAsync(ControlPlaneSecurityMode mode, ControlPlaneRowSecurityPolicy? rowSecurity = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(TenantAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TenantAuthHandler>(TenantAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), mode, rowSecurity: rowSecurity);
        await app.StartAsync();

        return new Host(app, app.GetTestClient());
    }

    /// <summary>A policy that stamps one fixed owner group for every principal, so a test can tell "the policy decided"
    /// apart from "the claim fallback decided" — the two produce different owners for the same caller.</summary>
    private sealed class PolicyStampsFixedOwner : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
            => [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", "everyone")];
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> GetAsync(string path, string? tenant)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Get, path), tenant);

        public Task<HttpResponseMessage> DeleteAsync(string path, string? tenant)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), tenant);

        public Task<HttpResponseMessage> PostAsync(string path, string body, string? tenant)
            => this.SendAsync(Json(HttpMethod.Post, path, body), tenant);

        public Task<HttpResponseMessage> PutAsync(string path, string body, string? tenant)
            => this.SendAsync(Json(HttpMethod.Put, path, body), tenant);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private static HttpRequestMessage Json(HttpMethod method, string path, string body)
            => new(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? tenant)
        {
            using (request)
            {
                request.Headers.Add(TenantAuthHandler.ScopeHeader, "authenticated");
                if (tenant is not null)
                {
                    request.Headers.Add(TenantAuthHandler.TenantHeader, tenant);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class TenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Tenant";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey(ScopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}