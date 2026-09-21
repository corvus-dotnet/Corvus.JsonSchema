// <copyright file="ControlPlaneAuditSinkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
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
    private const string ReadOutputs = "runs:read runs:outputs:read";
    private const string JournalRun = "0a000000000000000000000000000001";

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

        // A sink with nothing to sign its heads is not enough either: an alteration at the chain's tail would show nowhere.
        ArgumentException unsigned = await Should.ThrowAsync<ArgumentException>(async () => await StartAsync(mode, new GovernanceAuditor(sink: new InMemoryAuditSink())));
        unsigned.ParamName.ShouldBe("auditor");
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
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"audit-env","displayName":"Audit"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/audit-env", """{"displayName":"Audit (edited)"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/audit-env", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        byte[] stored = sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem());
        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored));
        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(4);

        // The chain's open record, then one record to an action.
        string[] lines = Encoding.UTF8.GetString(stored).Split('\n', StringSplitOptions.RemoveEmptyEntries)[1..];
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
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)));
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

    [TestMethod]
    public async Task A_head_that_cannot_be_signed_degrades_health_and_the_request_still_succeeds()
    {
        var signer = new FlakySigner(Signer()) { Failing = true };
        await using var auditor = new GovernanceAuditor(sink: new InMemoryAuditSink(), headSigner: signer, headOptions: new AuditHeadOptions(1, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"first-env","displayName":"First"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        auditor.Health.IsHealthy.ShouldBeFalse();
        auditor.Health.FailuresSinceSuccess.ShouldBe(0);
        auditor.Health.HeadFailuresSinceSigned.ShouldBe(1);
        HealthCheckResult degraded = await new AuditSinkHealthCheck(auditor).CheckHealthAsync(new HealthCheckContext());
        degraded.Status.ShouldBe(HealthStatus.Unhealthy);
        degraded.Description!.ShouldContain("unsigned window");

        // The key service recovers, and the next record's head is signed over everything unsigned.
        signer.Failing = false;
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"second-env","displayName":"Second"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);
        auditor.Health.IsHealthy.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Each_signed_head_is_published_as_an_anchor_span_outside_the_sink()
    {
        var heads = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ArazzoTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == GovernanceAuditor.AnchorActivityName)
                {
                    lock (heads)
                    {
                        heads.Add(activity);
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        var sink = new InMemoryAuditSink();
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"anchor-env","displayName":"Anchor"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        string chainId = sink.ChainIds.ShouldHaveSingleItem();
        Activity anchor;
        lock (heads)
        {
            anchor = heads.Single(a => (string?)a.GetTagItem("corvus.arazzo.audit.chain") == chainId);
        }

        anchor.GetTagItem("corvus.arazzo.audit.sequence").ShouldBe(2L);
        anchor.GetTagItem("corvus.arazzo.audit.key_id").ShouldBe("audit-test");
        ((string)anchor.GetTagItem("corvus.arazzo.audit.signature")!).ShouldNotBeNullOrEmpty();

        // The anchor, held outside the sink, is one the stored chain holds.
        var published = new AuditHead(chainId, 2, (string)anchor.GetTagItem("corvus.arazzo.audit.previous_hash")!, (string)anchor.GetTagItem("corvus.arazzo.audit.algorithm")!, "audit-test", (string)anchor.GetTagItem("corvus.arazzo.audit.signature")!);
        (await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(chainId)), new AuditChainVerificationOptions { Anchor = published })).IsIntact.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_journal_read_is_a_read_record_in_the_chain_with_its_disclosure_tier()
    {
        var sink = new InMemoryAuditSink();
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);
        await SeedRunWithJournalAsync(host, JournalRun);

        HttpResponseMessage read = await host.SendAsync(HttpMethod.Get, $"/runs/{JournalRun}/steps", ReadOutputs);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Get, $"/runs/{new string('e', 32)}/steps", ReadOutputs)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        byte[] stored = sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem());
        (await AuditChainVerifier.VerifyAsync(new MemoryStream(stored))).IsIntact.ShouldBeTrue();
        List<Stj.JsonElement> reads = [.. Encoding.UTF8.GetString(stored).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => Stj.JsonDocument.Parse(l).RootElement).Where(r => r.GetProperty("kind").GetString() == "read")];
        reads.Count.ShouldBe(2);
        reads[0].GetProperty("action").GetString().ShouldBe("run.journal.read");
        reads[0].GetProperty("targetKind").GetString().ShouldBe("run");
        reads[0].GetProperty("targetId").GetString().ShouldBe(JournalRun);
        reads[0].GetProperty("disclosure").GetString().ShouldBe("full");
        reads[0].GetProperty("actor").GetString().ShouldNotBeNullOrEmpty();

        // The refusal is the probe: it names what was asked for, and says nothing was disclosed.
        reads[1].GetProperty("targetId").GetString().ShouldBe(new string('e', 32));
        reads[1].GetProperty("disclosure").GetString().ShouldBe("refused");

        // A read record names the target and the tier and never what was read.
        Encoding.UTF8.GetString(stored).ShouldNotContain("two");
    }

    [TestMethod]
    public async Task A_payload_read_whose_record_the_sink_refuses_discloses_nothing_and_a_refusal_is_answered_as_it_was()
    {
        var sink = new SwitchableSink();
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);
        await SeedRunWithJournalAsync(host, JournalRun);
        (await host.SendAsync(HttpMethod.Get, $"/runs/{JournalRun}/steps", ReadOutputs)).StatusCode.ShouldBe(HttpStatusCode.OK);

        sink.Failing = true;
        HttpResponseMessage refused = await host.SendAsync(HttpMethod.Get, $"/runs/{JournalRun}/steps", ReadOutputs);

        refused.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        string body = await refused.Content.ReadAsStringAsync();
        using (Stj.JsonDocument problem = Stj.JsonDocument.Parse(body))
        {
            problem.RootElement.GetProperty("type").GetString().ShouldBe("https://corvus-oss.org/arazzo/control-plane/problems/audit-read-record-failed");
            problem.RootElement.GetProperty("detail").GetString()!.ShouldContain("nothing was disclosed");
        }

        body.ShouldNotContain("stepA");
        auditor.Health.IsHealthy.ShouldBeFalse();

        // A refusal discloses nothing, so it is never failed by the sink: it is answered as it would have been.
        (await host.SendAsync(HttpMethod.Get, $"/runs/{new string('e', 32)}/steps", ReadOutputs)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        sink.Failing = false;
        (await host.SendAsync(HttpMethod.Get, $"/runs/{JournalRun}/steps", ReadOutputs)).StatusCode.ShouldBe(HttpStatusCode.OK);
        auditor.Health.IsHealthy.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_read_refused_with_a_not_found_is_a_refusal_record_and_nothing_else_is()
    {
        var sink = new InMemoryAuditSink();
        await using var auditor = new GovernanceAuditor(sink: sink, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"real-env","displayName":"Real"}""", Write)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Found, and so not a refusal; a mutation that finds nothing is not a read; a list names no resource.
        (await host.SendAsync(HttpMethod.Get, "/environments/real-env", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/no-such-env", Write)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendAsync(HttpMethod.Get, "/environments", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendAsync(HttpMethod.Get, "/environments/secret-env", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Stj.JsonElement refusal = ReadRecords(sink).ShouldHaveSingleItem();
        refusal.GetProperty("action").GetString().ShouldBe("getEnvironment");
        refusal.GetProperty("targetKind").GetString().ShouldBe("environments");
        refusal.GetProperty("targetId").GetString().ShouldBe("secret-env");
        refusal.GetProperty("disclosure").GetString().ShouldBe("refused");
        refusal.GetProperty("actor").GetString().ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task An_enumeration_is_recorded_up_to_its_bound_and_then_as_a_count()
    {
        var sink = new InMemoryAuditSink();
        var clock = new ManualClock(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));
        await using var auditor = new GovernanceAuditor(sink: sink, timeProvider: clock, headSigner: Signer(), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)), refusalLimiter: new RefusalRecordLimiter(perSubject: 3, window: TimeSpan.FromMinutes(1)));
        await using Host host = await StartAsync(ControlPlaneSecurityMode.Scoped, auditor);

        for (int i = 0; i < 10; i++)
        {
            (await host.SendAsync(HttpMethod.Get, $"/environments/guess-{i}", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Ten probes, three records: the chain is not the enumeration's to fill.
        ReadRecords(sink).Count.ShouldBe(3);

        // The prober goes quiet. The window turns, the sweep runs, and what was not recorded one by one is recorded as a
        // count against the subject that made it.
        clock.Advance(TimeSpan.FromSeconds(61));
        clock.Tick();
        List<Stj.JsonElement> reads = [];
        for (int i = 0; i < 100 && reads.Count < 4; i++)
        {
            await Task.Delay(20);
            reads = ReadRecords(sink);
        }

        reads.Count.ShouldBe(4);
        reads[3].GetProperty("action").GetString().ShouldBe("read.refusals-suppressed");
        reads[3].GetProperty("disclosure").GetString().ShouldBe("suppressed");
        reads[3].GetProperty("suppressed").GetInt64().ShouldBe(7);
        reads[3].GetProperty("actor").GetString().ShouldBe(reads[0].GetProperty("actor").GetString());

        (await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem())))).IsIntact.ShouldBeTrue();
    }

    private static List<Stj.JsonElement> ReadRecords(InMemoryAuditSink sink)
        => [.. sink.ChainIds.SelectMany(id => Encoding.UTF8.GetString(sink.Snapshot(id)).Split('\n', StringSplitOptions.RemoveEmptyEntries)).Select(l => Stj.JsonDocument.Parse(l).RootElement).Where(r => r.GetProperty("kind").GetString() == "read")];

    private static async Task SeedRunWithJournalAsync(Host host, string runId)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "stepA": { "a": 1 }, "stepB": { "b": "two" } }"""u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(host.Store, runId, "wf", default, "development", TimeProvider.System);
        run.SetStepOutputs("stepA", doc.RootElement.GetProperty("stepA"u8));
        run.SetStepOutputs("stepB", doc.RootElement.GetProperty("stepB"u8));
        await run.CheckpointAsync(cursor: 2, default);
    }

    private static EcdsaExecutorPackageSigner Signer()
        => new(System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256), "audit-test");

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

        return new Host(app, app.GetTestClient(), store);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;

        public void Tick()
        {
            foreach (ManualTimer timer in this.timers.ToArray())
            {
                timer.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            this.timers.Add(timer);
            return timer;
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;

            public void Fire()
            {
                if (!this.disposed)
                {
                    callback(state);
                }
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() => this.disposed = true;

            public ValueTask DisposeAsync()
            {
                this.disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FlakySigner(IExecutorPackageSigner inner) : IExecutorPackageSigner
    {
        public bool Failing { get; set; }

        public ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
            => this.Failing ? throw new System.Security.Cryptography.CryptographicException("the key service is unreachable") : inner.SignAsync(manifestUtf8, cancellationToken);
    }

    private sealed class SwitchableSink : IAuditSink
    {
        public InMemoryAuditSink Inner { get; } = new();

        public bool Failing { get; set; }

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken) => this.Inner.OpenLastChainAsync(writerId, cancellationToken);

        public async ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
            => new Chain(this, await this.Inner.CreateChainAsync(writerId, chainId, cancellationToken));

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

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public InMemoryWorkflowStateStore Store => store;

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