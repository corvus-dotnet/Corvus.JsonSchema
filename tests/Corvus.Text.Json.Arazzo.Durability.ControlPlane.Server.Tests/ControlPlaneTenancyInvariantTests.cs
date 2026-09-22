// <copyright file="ControlPlaneTenancyInvariantTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The write-time tenancy invariant (ADR 0065, phase A). Phase A carries no checkpoint cryptography, so it leaves the
/// control plane the sole custodian of every tenant's plaintext. The gate against deploying on phase A alone cannot be
/// a startup flag, because multi-tenancy is emergent from the data rather than declared in configuration, so it is a
/// data invariant on the writes that introduce an owner group.
/// </summary>
/// <remarks>
/// The rule differs by whether the mode isolates reach, because the gate's premise is that encryption compensates for
/// shared infrastructure, and that premise holds only where reach isolation already prevents a cross-owner read
/// through the API.
/// </remarks>
[TestClass]
public sealed class ControlPlaneTenancyInvariantTests
{
    [TestMethod]
    public async Task ScopesOnly_refuses_a_second_owner_group_outright()
    {
        // Unrestricted reach means the second group reads the first's runs through the governance API whatever is
        // encrypted at rest, so keys cannot buy the isolation the gate is protecting. Refused outright, not gated.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage refused = await host.PostAsync("/environments", """{"name":"staging"}""", "zeus");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("tenancy-invariant");
    }

    [TestMethod]
    public async Task The_same_owner_group_may_always_create_another_environment()
    {
        // The discriminating half. Without it, a gate that refused every second environment would pass the test above
        // while making the product unusable.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.PostAsync("/environments", """{"name":"staging"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.PostAsync("/environments", """{"name":"dev"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task Scoped_refuses_a_second_owner_group_outright()
    {
        // Reach is isolated here, so encryption would be a meaningful compensation. It is not built (ADR 0065 phase B),
        // so the gate is absolute in this mode too until it is.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, new TenantIdentityPolicy());

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage refused = await host.PostAsync("/environments", """{"name":"staging"}""", "zeus");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("tenancy-invariant");
    }

    [TestMethod]
    public async Task A_registered_key_does_not_admit_a_second_owner_group()
    {
        // V-38 of the 2026-08-07 audit. The gate used to admit a second owner group once every tenant-owned environment
        // held an active registered key. Registering a key is metadata, and no code encrypts a payload under it, so the
        // deployment was onboarding a second tenant while the control plane held every tenant's plaintext.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, new TenantIdentityPolicy());
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.PostAsync("/environments/production/keys", Registration(key, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage refused = await host.PostAsync("/environments", """{"name":"staging"}""", "zeus");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        string problem = await refused.Content.ReadAsStringAsync();
        problem.ShouldContain("tenancy-invariant");
        problem.ShouldContain("A registered key does not change that");
    }

    [TestMethod]
    public async Task Open_has_no_owner_group_to_distinguish()
    {
        // No authentication means no owner group, so the invariant is vacuous by construction rather than by being
        // switched off. Pinned so that a later change cannot start refusing writes in the development posture.
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Open);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.PostAsync("/environments", """{"name":"staging"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task The_platform_environment_is_outside_the_count()
    {
        // Decision 10 makes the platform's own environment permanently unsealed, so counting it would refuse every
        // second-tenant onboarding forever. It is excluded by its marker, which no request body can produce.
        var environments = new InMemoryEnvironmentStore();
        using (ParsedJsonDocument<Environment> platform = Environment.DraftPlatform("system", "System", null, SecurityTagSet.Empty))
        {
            (await environments.AddAsync(platform.RootElement, "installer", default)).Dispose();
        }

        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, new TenantIdentityPolicy(), environments);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.PostAsync("/environments/production/keys", Registration(key, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The platform environment carries no owner group, so acme above was the deployment's first group and not its
        // second, which is what admitted it. A second group is refused whatever the platform environment holds, and
        // whatever key production registered.
        (await host.PostAsync("/environments", """{"name":"staging"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Two_simultaneous_creates_introducing_different_owner_groups_cannot_both_commit()
    {
        // The race the tenancy ledger exists to close, driven deterministically rather than by hoping two threads
        // interleave. A second create for a DIFFERENT owner group is run to completion inside the first one's
        // compare-and-swap, so both requests decided against the same empty deployment. The first must then lose its
        // swap, decide again against the state the second left, and be refused.
        Host? host = null;
        var interleaved = new InterleavedTenancyStore(
            new InMemoryEnvironmentStore(),
            async () => (await host!.PostAsync("/environments", """{"name":"staging"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Created));

        host = await StartAsync(ControlPlaneSecurityMode.ScopesOnly, environments: interleaved);
        await using (host)
        {
            HttpResponseMessage refused = await host.PostAsync("/environments", """{"name":"production"}""", "acme");

            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await refused.Content.ReadAsStringAsync()).ShouldContain("tenancy-invariant");
            interleaved.Interleaved.ShouldBeTrue();

            // And the losing request left nothing behind: one owner group, one environment.
            using EnvironmentPage page = await interleaved.ListAsync(AccessContext.System, 100, default, default);
            page.Environments.Select(e => e.NameValue).ShouldBe(["staging"]);
        }
    }

    [TestMethod]
    public async Task An_admitted_owner_group_is_re_evaluated_on_its_next_create()
    {
        // Admission is recorded before the environment is written, so a create that fails afterwards leaves a group in
        // the ledger with nothing behind it. That is only safe because being in the ledger is not a standing exemption:
        // the rule runs on every write, so a group admitted earlier is refused exactly when a new one would be. The
        // create gate admits no second group any more, so the second is seeded into the ledger as a deployment that
        // once held two would carry it.
        var environments = new InMemoryEnvironmentStore();
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, new TenantIdentityPolicy(), environments);

        (await host.PostAsync("/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        using (ParsedJsonDocument<TenancyLedger>? ledger = await environments.GetTenancyLedgerAsync(default))
        {
            (await environments.TryCommitTenancyLedgerAsync(ledger?.RootElement ?? default, "zeus"u8.ToArray(), "ops", default)).ShouldBeTrue();
        }

        // Neither group is exempt now: the first is re-evaluated on its next create and refused like the second.
        (await host.PostAsync("/environments", """{"name":"staging"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.PostAsync("/environments", """{"name":"dev"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static string Registration(ECDsa key, string environment, string keyId)
    {
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        byte[] tuple = new byte[EnvironmentKeyPossession.MaxTupleLength(environment, keyId, spki.Length)];
        int written = EnvironmentKeyPossession.WriteSignedTuple(tuple, environment, keyId, spki, notBefore);
        byte[] signature = key.SignData(tuple.AsSpan(0, written), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $$"""
            {"keyId":"{{keyId}}","sealPublicKey":"{{Convert.ToBase64String(spki)}}","algorithm":"ES256","notBefore":"{{notBefore:O}}","signature":"{{Convert.ToBase64String(signature)}}"}
            """;
    }

    private static async Task<Host> StartAsync(
        ControlPlaneSecurityMode mode, ControlPlaneRowSecurityPolicy? rowSecurity = null, IEnvironmentStore? environments = null)
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
        builder.Services.AddArazzoAuthenticationTelemetry();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), mode, rowSecurity: rowSecurity, environmentStore: environments, auditor: GovernanceAuditor.CreateInMemory());
        await app.StartAsync();

        return new Host(app, app.GetTestClient());
    }

    // Runs another request to completion inside the FIRST tenancy compare-and-swap, so two creates provably decided
    // against the same state. Everything else delegates, so the deployment under test is the real one.
    private sealed class InterleavedTenancyStore(IEnvironmentStore inner, Func<Task> interleave) : IEnvironmentStore
    {
        private int armed = 1;

        public bool Interleaved => this.armed == 0;

        public async ValueTask<bool> TryCommitTenancyLedgerAsync(TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref this.armed, 0) == 1)
            {
                await interleave();
            }

            return await inner.TryCommitTenancyLedgerAsync(current, admitting, actor, cancellationToken);
        }

        public ValueTask<ParsedJsonDocument<TenancyLedger>?> GetTenancyLedgerAsync(CancellationToken cancellationToken)
            => inner.GetTenancyLedgerAsync(cancellationToken);

        public ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
            => inner.AddAsync(draft, actor, cancellationToken);

        public ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
            => inner.GetAsync(name, context, cancellationToken);

        public ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
            => inner.ListAsync(context, limit, pageToken, cancellationToken);

        public ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
            => inner.UpdateAsync(name, draft, expectedEtag, actor, context, cancellationToken);

        public ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
            => inner.DeleteAsync(name, expectedEtag, context, cancellationToken);
    }

    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public async Task<HttpResponseMessage> PostAsync(string path, string body, string tenant)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add(TenantAuthHandler.ScopeHeader, "authenticated");
            request.Headers.Add(TenantAuthHandler.TenantHeader, tenant);
            HttpResponseMessage response = await client.SendAsync(request);
            await response.Content.LoadIntoBufferAsync();
            return response;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class TenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TenancyTenant";
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