// <copyright file="ControlPlaneKeyRetirementInvariantTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;
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

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The tenancy invariant's symmetric refusal on key retirement (ADR 0065, phase A). The invariant is write-time on
/// creation, so without this an operator registers a key, onboards a second owner group past the gate, and then
/// retires the key, arriving at exactly the state the gate exists to refuse by a route the gate never sees.
/// </summary>
/// <remarks>
/// The predicate is "at least one ACTIVE generation", not "at least one generation". An environment whose only
/// generation is retired holds nothing that can protect a payload, so a test that only ever counted generations would
/// pass while the deployment was unprotected.
/// </remarks>
[TestClass]
public sealed class ControlPlaneKeyRetirementInvariantTests
{
    [TestMethod]
    public async Task Retiring_the_last_active_generation_is_refused_once_a_second_owner_group_exists()
    {
        await using Host host = await StartAsync();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");
        (await host.PostAsync("/environments/production/keys", Registration(key, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // One owner group so far: retirement would be allowed here. Introduce the second, then try. The create gate
        // refuses a second owner group outright until ADR 0065 phase B, so the second group is seeded straight into the
        // ledger, as a deployment that once held two would carry it.
        await SeedSecondOwnerGroupAsync(host);

        HttpResponseMessage refused = await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("environment-key-last-active");
    }

    [TestMethod]
    public async Task Retiring_the_last_active_generation_is_allowed_while_one_owner_group_exists()
    {
        // The discriminating half: without it the test above passes on a handler that refuses every retirement.
        await using Host host = await StartAsync();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");
        await CreateEnvironmentAsync(host, "staging", "acme");
        (await host.PostAsync("/environments/production/keys", Registration(key, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_rotation_is_allowed_with_a_second_owner_group_present()
    {
        // Retiring a generation that is NOT the last active one is a rotation, and refusing it would make key rotation
        // impossible for exactly the multi-tenant deployments that most need it.
        await using Host host = await StartAsync();
        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa second = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");
        await host.PostAsync("/environments/production/keys", Registration(first, "production", "k1"), "acme");
        await host.PostAsync("/environments/production/keys", Rotation(second, "production", "k2", first, "k1"), "acme");
        await SeedSecondOwnerGroupAsync(host);

        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...and the one now left is the last active one, so it is held.
        (await host.PostAsync("/environments/production/keys/k2/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task A_retired_generation_does_not_count_as_the_other_active_one()
    {
        // The ACTIVE predicate at the retirement gate. With k1 retired and k2 active, k2 IS the last active
        // generation, so retiring it must be refused even though the environment still holds two generations.
        await using Host host = await StartAsync();
        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa second = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");
        await host.PostAsync("/environments/production/keys", Registration(first, "production", "k1"), "acme");
        await host.PostAsync("/environments/production/keys", Rotation(second, "production", "k2", first, "k1"), "acme");
        await SeedSecondOwnerGroupAsync(host);

        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.PostAsync("/environments/production/keys/k2/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task The_two_gates_compose_so_an_unsealed_multi_tenant_state_is_unreachable()
    {
        // The create gate refuses a second owner group whatever the sealing state, and the retirement gate refuses
        // unsealing while the ledger holds a second owner group. Neither ordering reaches "two owner groups, nothing
        // sealed", which is the state phase A exists to keep out. Asserted through the API rather than argued in prose.
        await using Host host = await StartAsync();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");

        // Order one: try to onboard the second group first. Refused.
        (await host.PostAsync("/environments", """{"name":"staging"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Order two: seal, then try again. Still refused, since a registered key protects nothing until phase B.
        (await host.PostAsync("/environments/production/keys", Registration(key, "production", "k1"), "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.PostAsync("/environments", """{"name":"staging"}""", "zeus")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // And a deployment whose ledger does hold two groups cannot unseal: the retirement gate is the symmetric half.
        await SeedSecondOwnerGroupAsync(host);
        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Retiring_an_already_retired_generation_stays_idempotent()
    {
        // The refusal must not turn the idempotent replay into a conflict: a client retrying a retirement it already
        // completed would otherwise see a failure for a state it had itself asked for and reached.
        await using Host host = await StartAsync();
        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa second = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await CreateEnvironmentAsync(host, "production", "acme");
        await host.PostAsync("/environments/production/keys", Registration(first, "production", "k1"), "acme");
        await host.PostAsync("/environments/production/keys", Rotation(second, "production", "k2", first, "k1"), "acme");
        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await SeedSecondOwnerGroupAsync(host);

        (await host.PostAsync("/environments/production/keys/k1/retirement", "{}", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public void The_platform_marker_cannot_come_from_a_draft()
    {
        // The marker excludes a row from the owner-group count, so the whole of its value is that no request can
        // produce it. The API-facing draft factory omits it; only the installer's DraftPlatform emits it.
        using ParsedJsonDocument<Environment> tenant = Environment.Draft("production", null, null, SecurityTagSet.Empty);
        using ParsedJsonDocument<Environment> platform = Environment.DraftPlatform("system", null, null, SecurityTagSet.Empty);

        TenantEnvironmentSealing.IsPlatform(tenant.RootElement).ShouldBeFalse();
        TenantEnvironmentSealing.IsPlatform(platform.RootElement).ShouldBeTrue();
    }

    // A rotation (ADR 0065 decision 12): the new key's own possession proof plus the predecessor's signature over the
    // rotation tuple, made with the outgoing private seal half.
    private static string Rotation(ECDsa key, string environment, string keyId, ECDsa predecessor, string predecessorKeyId, ECDsa? linkSignedBy = null)
    {
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        byte[] tuple = new byte[EnvironmentKeyPossession.MaxTupleLength(environment, keyId, spki.Length)];
        int written = EnvironmentKeyPossession.WriteSignedTuple(tuple, environment, keyId, spki, notBefore);
        byte[] signature = key.SignData(tuple.AsSpan(0, written), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        byte[] link = EnvironmentKeyRotation.Sign(linkSignedBy ?? predecessor, environment, predecessorKeyId, keyId, spki);

        return $$"""
            {"keyId":"{{keyId}}","sealPublicKey":"{{Convert.ToBase64String(spki)}}","algorithm":"ES256","notBefore":"{{notBefore:O}}","signature":"{{Convert.ToBase64String(signature)}}","predecessorKeyId":"{{predecessorKeyId}}","rotationSignature":"{{Convert.ToBase64String(link)}}"}
            """;
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

    private static async Task CreateEnvironmentAsync(Host host, string name, string tenant)
        => (await host.PostAsync("/environments", $$"""{"name":"{{name}}"}""", tenant)).StatusCode.ShouldBe(HttpStatusCode.Created);

    // Commits a second owner group to the tenancy ledger directly, as the deployment would carry it had it been admitted:
    // the create gate refuses a second group outright until ADR 0065 phase B, and the retirement gate reads the ledger.
    private static async Task SeedSecondOwnerGroupAsync(Host host)
    {
        using ParsedJsonDocument<TenancyLedger>? ledger = await host.Environments.GetTenancyLedgerAsync(default);
        (await host.Environments.TryCommitTenancyLedgerAsync(ledger?.RootElement ?? default, "zeus"u8.ToArray(), "ops", default)).ShouldBeTrue();
    }

    private static async Task<Host> StartAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        var environments = new InMemoryEnvironmentStore();

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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), environmentStore: environments, auditor: GovernanceAuditor.CreateInMemory());
        await app.StartAsync();

        return new Host(app, app.GetTestClient(), environments);
    }

    /// <summary>Full reach for every caller (so an administrator of one environment can still be refused on the
    /// tenancy invariant rather than on visibility), with the owner group taken from the caller's tenant claim.</summary>
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryEnvironmentStore environments) : IAsyncDisposable
    {
        public InMemoryEnvironmentStore Environments => environments;

        public Task<HttpResponseMessage> PostAsync(string path, string body, string tenant)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string tenant)
        {
            using (request)
            {
                request.Headers.Add(TenantAuthHandler.ScopeHeader, "authenticated");
                request.Headers.Add(TenantAuthHandler.TenantHeader, tenant);
                HttpResponseMessage response = await client.SendAsync(request);

                // Drain the body before returning. The server writes the response from pooled documents the workspace
                // owns, and TestServer streams it, so leaving it unread lets the next request reuse buffers this one is
                // still serializing from — which surfaces as an unrelated test failing intermittently.
                await response.Content.LoadIntoBufferAsync();
                return response;
            }
        }
    }

    private sealed class TenantAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "RetirementTenant";
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