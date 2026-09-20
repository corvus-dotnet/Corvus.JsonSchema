// <copyright file="ControlPlaneAuditSinkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The audit sink at the control plane (ADR 0069): a secured posture does not start without one, every governance
/// action's record is in the chain, and a record the sink refuses fails the request that made it while the action
/// stands. Reads are never gated on it.
/// </summary>
[TestClass]
public sealed class ControlPlaneAuditSinkTests
{
    private const string Write = "environments:write";
    private const string Read = "environments:read";

    [TestMethod]
    [DataRow(ControlPlaneSecurityMode.Scoped)]
    [DataRow(ControlPlaneSecurityMode.RowSecurityOnly)]
    [DataRow(ControlPlaneSecurityMode.ScopesOnly)]
    public async Task A_secured_posture_does_not_start_without_an_audit_sink(ControlPlaneSecurityMode mode)
    {
        ArgumentException none = await Should.ThrowAsync<ArgumentException>(async () => await StartAsync(mode, auditor: null));
        none.ParamName.ShouldBe("auditor");
        none.Message.ShouldContain(mode.ToString());

        // An auditor that only logs is not a sink: the log is what ADR 0069 says evaporates.
        ArgumentException logOnly = await Should.ThrowAsync<ArgumentException>(async () => await StartAsync(mode, new GovernanceAuditor()));
        logOnly.ParamName.ShouldBe("auditor");
    }

    [TestMethod]
    public async Task The_open_posture_starts_without_an_audit_sink_and_its_mutations_succeed()
    {
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Open, auditor: null);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"dev-env","displayName":"Dev"}""", scope: null)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task Every_governance_action_is_a_record_in_the_chain_refusals_included()
    {
        var sink = new InMemoryAuditSink();
        await using var auditor = new GovernanceAuditor(sink: sink);
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"audit-env","displayName":"Audit"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/audit-env", """{"displayName":"Audit (edited)"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/audit-env", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        byte[] stored = sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem());
        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored));
        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(3);

        string[] lines = Encoding.UTF8.GetString(stored).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using Stj.JsonDocument created = Stj.JsonDocument.Parse(lines[0]);
        created.RootElement.GetProperty("kind").GetString().ShouldBe("mutation");
        created.RootElement.GetProperty("action").GetString().ShouldBe("environment.create");
        created.RootElement.GetProperty("targetKind").GetString().ShouldBe("environment");
        created.RootElement.GetProperty("targetId").GetString().ShouldBe("audit-env");
        created.RootElement.GetProperty("outcome").GetString().ShouldBe("created");
        created.RootElement.GetProperty("actor").GetString().ShouldNotBeNullOrEmpty();
        lines.Select(l => Stj.JsonDocument.Parse(l).RootElement.GetProperty("outcome").GetString()).ShouldBe(["created", "updated", "deleted"]);
    }

    [TestMethod]
    public async Task A_record_the_sink_refuses_fails_the_request_and_the_action_stands()
    {
        var sink = new SwitchableSink();
        await using var auditor = new GovernanceAuditor(sink: sink);
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"first-env","displayName":"First"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        auditor.Health.IsHealthy.ShouldBeTrue();

        sink.Failing = true;
        HttpResponseMessage refused = await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"second-env","displayName":"Second"}""", Write);

        refused.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        refused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using (Stj.JsonDocument problem = Stj.JsonDocument.Parse(await refused.Content.ReadAsStringAsync()))
        {
            problem.RootElement.GetProperty("type").GetString().ShouldBe("https://corvus-oss.org/arazzo/control-plane/problems/audit-record-failed");
            problem.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
            problem.RootElement.GetProperty("detail").GetString()!.ShouldContain("was applied");
        }

        auditor.Health.IsHealthy.ShouldBeFalse();
        auditor.Health.FailuresSinceSuccess.ShouldBe(1);
        auditor.Health.LastFailureAt.ShouldNotBeNull();
        HealthCheckResult unhealthy = await new AuditSinkHealthCheck(auditor).CheckHealthAsync(new HealthCheckContext());
        unhealthy.Status.ShouldBe(HealthStatus.Unhealthy);
        unhealthy.Data["failuresSinceSuccess"].ShouldBe(1L);

        // The action stands, and a read is not gated on the sink: the environment the failed request created is there
        // to be read while the sink is still down.
        (await host.SendAsync(HttpMethod.Get, "/environments/second-env", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The sink heals: the next action is recorded, in a new chain that says which chain it continues.
        sink.Failing = false;
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"third-env","displayName":"Third"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        auditor.Health.IsHealthy.ShouldBeTrue();
        (await new AuditSinkHealthCheck(auditor).CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);

        sink.Inner.ChainIds.Count.ShouldBe(2);
        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Inner.Snapshot(sink.Inner.ChainIds[1])));
        next.IsIntact.ShouldBeTrue();
        next.ContinuesChain.ShouldBe(sink.Inner.ChainIds[0]);
    }

    private static async Task<Host> StartAsync(ControlPlaneSecurityMode mode, GovernanceAuditor? auditor)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        try
        {
            app.UseAuthentication();
            app.UseAuthorization();
            bool rowSecured = mode is ControlPlaneSecurityMode.Scoped or ControlPlaneSecurityMode.RowSecurityOnly;
            app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), mode, rowSecurity: rowSecured ? new TenantPolicy() : null, auditor: auditor);
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new Host(app, app.GetTestClient());
    }

    private sealed class SwitchableSink : IAuditSink
    {
        public InMemoryAuditSink Inner { get; } = new();

        public bool Failing { get; set; }

        public async ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
            => new Chain(this, await this.Inner.CreateChainAsync(chainId, cancellationToken));

        private sealed class Chain(SwitchableSink owner, IAuditChainStream chain) : IAuditChainStream
        {
            public ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
                => owner.Failing ? throw new IOException("the audit store is unreachable") : chain.AppendAsync(line, cancellationToken);

            public ValueTask DisposeAsync() => chain.DisposeAsync();
        }
    }

    private sealed class TenantPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [new SecurityTag("sys:tenant", "acme")];
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}