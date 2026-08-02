// <copyright file="ControlPlaneEnvironmentKeysApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability.Environments;
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
/// The environment key-registration API (ADR 0065) over <c>/environments/{name}/keys</c>. A generation names the
/// payload-key id a sealed environment's checkpoint payloads carry, and the public seal key run-start inputs are
/// wrapped to. Governed by the environment's own administrators, and gated on a proof of possession rather than on
/// the caller merely asserting a key exists.
/// </summary>
[TestClass]
public sealed class ControlPlaneEnvironmentKeysApiTests
{
    [TestMethod]
    public async Task An_administrator_registers_a_generation_and_reads_it_back()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using Stj.JsonDocument registered = await ReadJsonAsync(
            await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(key, "production", "k1"), "acme"));

        registered.RootElement.GetProperty("keyId").GetString().ShouldBe("k1");
        registered.RootElement.GetProperty("state").GetString().ShouldBe("Active");
        registered.RootElement.GetProperty("registeredBy").GetString().ShouldBe("acme");

        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys", "acme"));
        listed.RootElement.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("keyId").GetString()).ShouldBe(["k1"]);
    }

    [TestMethod]
    public async Task A_registration_whose_signature_does_not_verify_is_refused()
    {
        // Without this the gate is satisfiable by typing a string, and the tenancy invariant would pass on a
        // deployment where nothing whatever is protected.
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa presented = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string body = Registration(presented, "production", "k1", signWith: other);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", body, "acme")).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_registration_signed_for_another_environment_is_refused()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");
        await CreateEnvironmentAsync(host, "staging", "acme");

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string signedForStaging = Registration(key, "staging", "k1");

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", signedForStaging, "acme")).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Replaying_a_registration_returns_the_existing_generation()
    {
        // Replay is deliberately not an error: the signed tuple determines the effect, which is what removes the
        // need for a server-side nonce store.
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string body = Registration(key, "production", "k1");

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", body, "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", body, "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys", "acme"));
        listed.RootElement.GetProperty("keys").GetArrayLength().ShouldBe(1, "a replay must not add a second generation");
    }

    [TestMethod]
    public async Task Registering_as_a_non_administrator_is_forbidden_and_out_of_reach_is_not_found()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string body = Registration(key, "production", "k1");

        // 'contoso' can see the environment (the harness policy grants full read reach) but does not administer it.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", body, "contoso")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        // An environment that does not exist is not found, and says nothing more.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/absent/keys", Registration(key, "absent", "k1"), "acme")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Retiring_a_generation_records_it_rather_than_removing_it_and_is_idempotent()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(key, "production", "k1"), "acme");

        using Stj.JsonDocument retired = await ReadJsonAsync(
            await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys/k1/retirement", """{"reason":"rotated"}""", "acme"));
        retired.RootElement.GetProperty("state").GetString().ShouldBe("Retired");
        retired.RootElement.GetProperty("retiredBy").GetString().ShouldBe("acme");
        retired.RootElement.GetProperty("reason").GetString().ShouldBe("rotated");

        // Idempotent, and still present: a checkpoint written under it stays attributable.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/keys/k1/retirement", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys", "acme"));
        listed.RootElement.GetProperty("keys").GetArrayLength().ShouldBe(1);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/keys/absent/retirement", "acme")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task A_rotation_leaves_the_retired_generation_and_adds_the_new_one()
    {
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(first, "production", "k1"), "acme");
        await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys/k1/retirement", "{}", "acme");
        await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(second, "production", "k2"), "acme");

        using Stj.JsonDocument all = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys", "acme"));
        all.RootElement.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("keyId").GetString()).ShouldBe(["k1", "k2"]);

        using Stj.JsonDocument active = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys?state=Active", "acme"));
        active.RootElement.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("keyId").GetString()).ShouldBe(["k2"]);
    }

    [TestMethod]
    public async Task Registering_a_key_does_not_disturb_the_environment_and_a_rename_does_not_drop_it()
    {
        // The two directions of the write-path trap, proved through the real API rather than the writer alone.
        await using Scoped host = await StartAsync();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production","displayName":"Production","description":"The live one."}""", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", Registration(key, "production", "k1"), "acme");

        using Stj.JsonDocument environment = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production", "acme"));
        environment.RootElement.GetProperty("displayName").GetString().ShouldBe("Production");
        environment.RootElement.GetProperty("description").GetString().ShouldBe("The live one.");

        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"displayName":"Renamed"}""", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument listed = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/keys", "acme"));
        listed.RootElement.GetProperty("keys").GetArrayLength().ShouldBe(1, "an unrelated environment update must not drop the generations");
    }

    [TestMethod]
    public async Task A_base64_signature_whose_text_reads_as_a_number_is_not_rejected()
    {
        // Regression. sealPublicKey and signature were declared "format": "byte", which OpenAPI 3.0 reads as base64
        // but a 2020-12 schema reads as the NUMERIC byte format. The generated validator therefore parsed the base64
        // TEXT as a number and asserted it fits 0-255, so a legitimate registration was refused whenever its
        // signature happened to start with digits reading past 255 — about one registration in twelve, at random,
        // while asserting nothing whatever about the other eleven. This is a body that was refused.
        await using Scoped host = await StartAsync();
        await CreateEnvironmentAsync(host, "production", "acme");

        HttpResponseMessage response = await host.SendJsonAsync(HttpMethod.Post, "/environments/production/keys", NumericLookingRegistration, "acme");

        // The signing instant is long past, so this is refused as stale — but on the freshness check, never on the
        // shape of its base64.
        string detail = await response.Content.ReadAsStringAsync();
        detail.Contains("failed schema validation", StringComparison.Ordinal).ShouldBeFalse(detail);
    }

    // A captured registration whose signature begins "9mYF..." — the exact body the numeric byte-range assertion
    // refused. Kept verbatim rather than regenerated, so the regression is pinned by a value known to trip it.
    private const string NumericLookingRegistration = """
        {"keyId":"k2","sealPublicKey":"MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE2+qQlT7NBno+HipZW0d89svzT6A8nXY3oidOPofha9HijMRg4OUvVs5NRssODYx6VVBsRB66dJmMUmt9Sn6xuw==","algorithm":"ES256","notBefore":"2026-08-02T13:02:20.2691245+00:00","signature":"9mYFqtMCr/iu3JlKe4e6lOHA5Jm9X1A7isg9EvS+akOAOuMH4FlBJ0dqQTD/0k95qTKWo2rAn/YuHUHjPX4c2g=="}
        """;

    private static string Registration(ECDsa key, string environment, string keyId, ECDsa? signWith = null)
    {
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow;
        byte[] tuple = new byte[EnvironmentKeyPossession.MaxTupleLength(environment, keyId, spki.Length)];
        int written = EnvironmentKeyPossession.WriteSignedTuple(tuple, environment, keyId, spki, notBefore);
        byte[] signature = (signWith ?? key).SignData(tuple.AsSpan(0, written), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $$"""
            {"keyId":"{{keyId}}","sealPublicKey":"{{Convert.ToBase64String(spki)}}","algorithm":"ES256","notBefore":"{{notBefore:O}}","signature":"{{Convert.ToBase64String(signature)}}"}
            """;
    }

    private static async Task CreateEnvironmentAsync(Scoped host, string name, string tenant)
        => (await host.SendJsonAsync(HttpMethod.Post, "/environments", $$"""{"name":"{{name}}"}""", tenant))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync()
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
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy());
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient());
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

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}