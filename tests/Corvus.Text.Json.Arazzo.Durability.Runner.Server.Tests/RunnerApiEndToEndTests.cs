// <copyright file="RunnerApiEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Stj = System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Drives the generated runner API over real HTTP: a run is claimed, its checkpoint is loaded and saved under the
/// granted lease, and the refusals are exercised on the wire rather than through the coordinator. This is what proves
/// the contract and the implementation agree, since everything between them is generated from the contract.
/// </summary>
[TestClass]
public sealed class RunnerApiEndToEndTests
{
    private const string Runner = "runner-a";
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string AbsentRun = "badcafebadcafebadcafebadcafe0000";
    private const string Peer = "runner-b";
    private const string Production = "production";
    private const string Version = "adopt-v3";
    private const string LeaseHeader = "X-Arazzo-Lease";
    private const string SequenceHeader = "X-Arazzo-Checkpoint-Seq";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task The_runner_apis_refusals_are_records_naming_the_principal_and_a_success_leaves_none()
    {
        var sink = new InMemoryAuditSink();
        await using GovernanceAuditor auditor = GovernanceAuditor.CreateInMemory(sink);
        await using Host host = await Host.StartAsync(auditor: auditor);
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);

        // A claim that succeeds, and a checkpoint read under the lease it granted, are not refusals.
        string lease = await host.ClaimLeaseAsync(Runner);
        (await host.LoadCheckpointAsync(Runner, Run1, lease)).IsSuccessStatusCode.ShouldBeTrue();
        Refusals(sink).ShouldBeEmpty();

        // ADR 0071: the seam stops being silent. A peer presenting another runner's lease, and a request that names no
        // machine principal at all.
        (await host.LoadCheckpointAsync(Peer, Run1, lease)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.ClaimAnonymouslyAsync()).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        List<Stj.JsonElement> refusals = Refusals(sink);
        refusals.Count.ShouldBe(2);
        refusals[0].GetProperty("kind").GetString().ShouldBe("mutation");
        refusals[0].GetProperty("action").GetString().ShouldBe("runner.loadCheckpoint");
        refusals[0].GetProperty("actor").GetString().ShouldBe(Peer);
        refusals[0].GetProperty("targetKind").GetString().ShouldBe("run");
        refusals[0].GetProperty("targetId").GetString().ShouldBe(Run1);
        refusals[0].GetProperty("outcome").GetString().ShouldBe("refused-conflict");
        refusals[1].GetProperty("action").GetString().ShouldBe("runner.claimRun");
        refusals[1].GetProperty("actor").GetString().ShouldBe("(no machine principal)");
        refusals[1].GetProperty("outcome").GetString().ShouldBe("refused-no-principal");

        // The lease token is what holds a run, and it is in no record.
        string chain = System.Text.Encoding.UTF8.GetString(sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem()));
        chain.ShouldNotContain(lease);
    }

    [TestMethod]
    public async Task A_runner_hammering_its_quota_is_recorded_up_to_its_bound_and_then_as_a_count()
    {
        var sink = new InMemoryAuditSink();
        await using var auditor = new GovernanceAuditor(
            sink: sink,
            headSigner: new Corvus.Text.Json.Arazzo.Execution.EcdsaExecutorPackageSigner(System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256), "audit-test"),
            headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)),
            refusalLimiter: new RefusalRecordLimiter(perSubject: 2, window: TimeSpan.FromHours(1)));
        await using Host host = await Host.StartAsync(quotaOptions: new RunnerQuotaOptions { RunnerClaims = new RunnerQuotaLimit(1, 1) }, auditor: auditor);

        (await host.ClaimAsync(Runner)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        for (int i = 0; i < 6; i++)
        {
            (await host.ClaimAsync(Runner)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        List<Stj.JsonElement> refusals = Refusals(sink);
        refusals.Count.ShouldBe(2);
        refusals.ShouldAllBe(r => r.GetProperty("outcome").GetString() == "refused-quota" && r.GetProperty("actor").GetString() == Runner);
    }

    [TestMethod]
    public async Task A_runner_api_that_authenticates_its_callers_does_not_map_without_an_audit_sink()
    {
        ArgumentException none = await Should.ThrowAsync<ArgumentException>(async () => await Host.StartAsync(requireAuthorization: true));
        none.ParamName.ShouldBe("auditor");

        ArgumentException logOnly = await Should.ThrowAsync<ArgumentException>(async () => await Host.StartAsync(requireAuthorization: true, auditor: new GovernanceAuditor()));
        logOnly.ParamName.ShouldBe("auditor");
    }

    private static List<Stj.JsonElement> Refusals(InMemoryAuditSink sink)
        => [.. sink.ChainIds.SelectMany(id => System.Text.Encoding.UTF8.GetString(sink.Snapshot(id)).Split('\n', StringSplitOptions.RemoveEmptyEntries)).Select(l => Stj.JsonDocument.Parse(l).RootElement).Where(r => r.TryGetProperty("outcome", out Stj.JsonElement o) && o.GetString()!.StartsWith("refused", StringComparison.Ordinal))];

    [TestMethod]
    public async Task A_claim_answers_the_run_its_workflow_environment_and_lease()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);

        using HttpResponseMessage response = await host.ClaimAsync(Runner);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("runId").GetString().ShouldBe(Run1);
        body.RootElement.GetProperty("workflowId").GetString().ShouldBe(Version);
        body.RootElement.GetProperty("environment").GetString().ShouldBe(Production);

        Stj.JsonElement lease = body.RootElement.GetProperty("lease");
        lease.GetProperty("token").GetString().ShouldNotBeNullOrEmpty();
        lease.GetProperty("epoch").GetInt64().ShouldBeGreaterThan(0);
        lease.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(T0 + TimeSpan.FromMinutes(1));
    }

    [TestMethod]
    public async Task An_idle_runner_gets_no_content_rather_than_an_error()
    {
        await using Host host = await Host.StartAsync();

        using HttpResponseMessage response = await host.ClaimAsync(Runner);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task A_checkpoint_round_trips_under_the_granted_lease()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage loaded = await host.LoadCheckpointAsync(Runner, Run1, lease);
        loaded.StatusCode.ShouldBe(HttpStatusCode.OK);
        loaded.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        SequenceOf(loaded).ShouldBe(1);
        (await loaded.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(0);

        // The store has persisted sequence 1, so the next save must propose exactly 2.
        using HttpResponseMessage saved = await host.SaveCheckpointAsync(Runner, Run1, lease, Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(lease)), 2);
        saved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        SequenceOf(saved).ShouldBe(2);
    }

    [TestMethod]
    public async Task A_checkpoint_whose_region_carries_another_epoch_than_the_lease_is_refused()
    {
        // ADR 0065 decision 6, the phase-B half: the runner region carries the lease epoch independently of the
        // header, and a row minted under another grant, or under none, is not written under this one.
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage above = await host.SaveCheckpointAsync(Runner, Run1, lease, Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(lease) + 1), 2);
        above.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using HttpResponseMessage none = await host.SaveCheckpointAsync(Runner, Run1, lease, Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2), 2);
        none.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Nothing was written by either: the save under the grant's own epoch still lands as sequence 2.
        using HttpResponseMessage saved = await host.SaveCheckpointAsync(Runner, Run1, lease, Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(lease)), 2);
        saved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task A_superseded_save_is_refused_and_names_the_sequence_it_would_accept()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);
        byte[] checkpoint = Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(lease));
        (await host.SaveCheckpointAsync(Runner, Run1, lease, checkpoint, 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A byte-identical resend of a sequence already persisted. Reporting it as durable would be indistinguishable
        // from a write that landed, which is the one failure this operation exists to make impossible.
        using HttpResponseMessage resent = await host.SaveCheckpointAsync(Runner, Run1, lease, checkpoint, 2);

        resent.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        resent.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using Stj.JsonDocument problem = Stj.JsonDocument.Parse(await resent.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("type").GetString().ShouldBe("https://corvus-oss.org/arazzo/runner/problems/checkpoint-superseded");
        problem.RootElement.GetProperty("acceptedSequence").GetInt64().ShouldBe(3);
    }

    [TestMethod]
    public async Task An_operation_on_a_run_the_principal_does_not_hold_is_refused_as_a_lost_lease()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        // A peer presenting the token, and anyone naming a run that does not exist, get the same answer: the surface
        // cannot be used to learn which runs another tenant holds.
        using HttpResponseMessage peersRun = await host.LoadCheckpointAsync(Peer, Run1, lease);
        using HttpResponseMessage noSuchRun = await host.LoadCheckpointAsync(Peer, AbsentRun, lease);

        peersRun.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        noSuchRun.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using Stj.JsonDocument problem = Stj.JsonDocument.Parse(await peersRun.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("type").GetString().ShouldBe("https://corvus-oss.org/arazzo/runner/problems/lease-lost");
    }

    [TestMethod]
    public async Task A_renewal_extends_the_lease_and_keeps_its_epoch()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);

        using HttpResponseMessage claim = await host.ClaimAsync(Runner);
        using Stj.JsonDocument claimed = Stj.JsonDocument.Parse(await claim.Content.ReadAsStringAsync());
        Stj.JsonElement granted = claimed.RootElement.GetProperty("lease");
        string lease = granted.GetProperty("token").GetString()!;

        using HttpResponseMessage renewal = await host.RenewLeaseAsync(Runner, Run1, lease, 300);

        renewal.StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument renewed = Stj.JsonDocument.Parse(await renewal.Content.ReadAsStringAsync());
        renewed.RootElement.GetProperty("token").GetString().ShouldBe(lease);
        renewed.RootElement.GetProperty("epoch").GetInt64().ShouldBe(granted.GetProperty("epoch").GetInt64());
        renewed.RootElement.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(T0 + TimeSpan.FromMinutes(5));
    }

    [TestMethod]
    public async Task A_renewal_of_a_lease_that_is_not_current_is_refused()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        using HttpResponseMessage renewal = await host.RenewLeaseAsync(Runner, Run1, lease, 300);

        renewal.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task A_release_hands_the_run_back_for_the_next_claim()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage released = await host.ReleaseLeaseAsync(Runner, Run1, lease);
        released.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Another runner takes it immediately rather than waiting out the lease.
        using HttpResponseMessage reclaimed = await host.ClaimAsync(Peer);
        reclaimed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_release_by_a_principal_that_does_not_hold_the_lease_changes_nothing()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        // The same answer, because the postcondition holds for the peer either way — and the holder keeps its run.
        using HttpResponseMessage released = await host.ReleaseLeaseAsync(Peer, Run1, lease);
        released.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await host.LoadCheckpointAsync(Runner, Run1, lease)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_body_that_is_not_a_checkpoint_is_refused_before_the_store_is_touched()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage saved = await host.SaveCheckpointAsync(Runner, Run1, lease, [1, 2, 3], 2);

        saved.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The stored row is untouched: still the seeded checkpoint at sequence 1.
        using HttpResponseMessage loaded = await host.LoadCheckpointAsync(Runner, Run1, lease);
        SequenceOf(loaded).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_save_whose_body_sequence_differs_from_the_header_is_refused()
    {
        // H40 / ADR 0065 decision 6: the accept rule runs off the header while the coordinator re-seeds the
        // persisted sequence from the stored body, so the two must be the same number. A body claiming 5 under an
        // accepted header of 2 is a malformed save, refused before the store is touched.
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage saved = await host.SaveCheckpointAsync(Runner, Run1, lease, Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 5, epoch: LeaseEpoch(lease)), 2);

        saved.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The stored row is untouched: still the seeded checkpoint at sequence 1.
        using HttpResponseMessage loaded = await host.LoadCheckpointAsync(Runner, Run1, lease);
        SequenceOf(loaded).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_checkpoint_over_the_deployments_cap_is_refused()
    {
        // The cap bounds what one request can make the server rent, so a body over it is refused rather than buffered.
        await using Host host = await Host.StartAsync(new RunnerApiOptions { MaximumCheckpointBytes = 64 });
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        using HttpResponseMessage saved = await host.SaveCheckpointAsync(Runner, Run1, lease, new byte[4096], 2);

        saved.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        // Nothing was written: the stored row is still the seeded checkpoint at sequence 1.
        using HttpResponseMessage loaded = await host.LoadCheckpointAsync(Runner, Run1, lease);
        SequenceOf(loaded).ShouldBe(1);
    }

    private static long SequenceOf(HttpResponseMessage response)
        => long.Parse(response.Headers.GetValues(SequenceHeader).Single(), CultureInfo.InvariantCulture);

    // The epoch the lease was granted with: the runner writes it into its region, and the API checks it against the
    // grant (ADR 0065 decision 6).
    private static long LeaseEpoch(string lease)
        => RunnerLeaseToken.TryParse(lease, out long epoch, out _) ? epoch : 0;

    private static byte[] Checkpoint(string runId, WorkflowRunStatus status, long sequence, long? epoch = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                new WorkflowRunId(runId),
                Production,
                Version,
                status,
                0,
                sequence,
                Epoch: epoch,
                T0,
                T0,
                null,
                null,
                default,
                default,
                [],
                false,
                null,
                null),
            retryCounters,
            new Dictionary<string, byte[]>(),
            default,
            stepOutputs,
            default,
            []);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store, TestClock clock) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public TestClock Clock { get; } = clock;

        public static async Task<Host> StartAsync(RunnerApiOptions? options = null, RunnerQuotaOptions? quotaOptions = null, GovernanceAuditor? auditor = null, bool requireAuthorization = false)
        {
            var clock = new TestClock(T0);
            var store = new InMemoryWorkflowStateStore(clock);
            var bindings = new DeclaredRunnerEnvironmentBindings(new Dictionary<string, IReadOnlyList<string>>
            {
                [Runner] = [Production],
                [Peer] = [Production],
            });

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddHttpContextAccessor();

            WebApplication app = builder.Build();

            // The principal a real deployment authenticates is supplied here by the test's own header, so the handlers
            // run against exactly the identity they would in production without standing up an identity provider.
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Principal", out Microsoft.Extensions.Primitives.StringValues principal))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", principal.ToString())], "test"));
                }

                await next(context);
            });

            app.MapArazzoRunnerApi(store, new InMemoryWorkflowCatalogStore(), new InMemoryAvailabilityStore(), bindings, options, requireAuthorization: requireAuthorization, timeProvider: clock, quotaOptions: quotaOptions, auditor: auditor);
            await app.StartAsync();

            return new Host(app, app.GetTestClient(), store, clock);
        }

        public async ValueTask SeedAsync(string runId, WorkflowRunStatus status)
        {
            byte[] checkpoint = Checkpoint(runId, status, sequence: 1);
            await store.SaveAsync(
                new WorkflowRunAddress(Production, new WorkflowRunId(runId)),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        public Task<HttpResponseMessage> ClaimAnonymouslyAsync()
            => this.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/claims") { Content = JsonContent.Create(new { hostedVersions = new[] { Version } }) });

        public Task<HttpResponseMessage> ClaimAsync(string principal)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/claims")
            {
                Content = JsonContent.Create(new { hostedVersions = new[] { Version } }),
            };

            return this.SendAsync(request, principal);
        }

        public async Task<string> ClaimLeaseAsync(string principal)
        {
            using HttpResponseMessage response = await this.ClaimAsync(principal);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument body = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("lease").GetProperty("token").GetString()!;
        }

        public Task<HttpResponseMessage> LoadCheckpointAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/environments/{Production}/runs/{runId}/checkpoint");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> SaveCheckpointAsync(string principal, string runId, string lease, byte[] body, long sequence)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/environments/{Production}/runs/{runId}/checkpoint")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(LeaseHeader, lease);
            request.Headers.Add(SequenceHeader, sequence.ToString(CultureInfo.InvariantCulture));
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> RenewLeaseAsync(string principal, string runId, string lease, int leaseSeconds)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/environments/{Production}/runs/{runId}/lease/renewal")
            {
                Content = JsonContent.Create(new { leaseSeconds }),
            };
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> ReleaseLeaseAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/environments/{Production}/runs/{runId}/lease");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string principal)
        {
            request.Headers.Add("X-Test-Principal", principal);
            return this.Client.SendAsync(request);
        }
    }
}