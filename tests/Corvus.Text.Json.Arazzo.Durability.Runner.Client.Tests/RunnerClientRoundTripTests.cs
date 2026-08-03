// <copyright file="RunnerClientRoundTripTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The generated client driving the generated server over real HTTP, terminating into a real store. Both sides come
/// from one contract, so this is what proves they agree — and it is the shape a runner actually runs in: claim, resume
/// the run through the client's checkpoint store, advance it, release.
/// </summary>
[TestClass]
public sealed class RunnerClientRoundTripTests
{
    private const string Runner = "runner-a";
    private const string Peer = "runner-b";
    private const string Production = "production";
    private const string Version = "adopt-v3";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_runner_claims_a_run_and_reads_its_checkpoint_through_the_client()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);

        RunnerClaim? claimed = await fixture.Client.TryClaimAsync([Version]);

        claimed.ShouldNotBeNull();
        claimed!.Value.RunId.ShouldBe(new WorkflowRunId("run-1"));
        claimed.Value.WorkflowId.ShouldBe(Version);
        claimed.Value.Environment.ShouldBe(Production);
        claimed.Value.LeaseEpoch.ShouldBeGreaterThan(0);
        claimed.Value.LeaseExpiresAt.ShouldBe(T0 + TimeSpan.FromMinutes(1));

        // The run resumes through the client's checkpoint store exactly as it would over a database-backed one.
        WorkflowCheckpoint? loaded = await fixture.Client.Checkpoints.LoadAsync(claimed.Value.RunId, default);
        loaded.ShouldNotBeNull();
        WorkflowCheckpointSerializer.ProjectIndex(loaded!.Value.Utf8).WorkflowId.ShouldBe(Version);
    }

    [TestMethod]
    public async Task An_idle_runner_is_told_nothing_is_claimable()
    {
        await using Fixture fixture = await Fixture.StartAsync();

        (await fixture.Client.TryClaimAsync([Version])).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_checkpoint_saved_through_the_client_is_durable_in_the_store()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        byte[] advanced = Checkpoint("run-1", WorkflowRunStatus.Running, sequence: 2);
        await fixture.Client.Checkpoints.SaveAsync(claimed.RunId, advanced, WorkflowCheckpointSerializer.ProjectIndex(advanced), WorkflowEtag.None, default);

        // Read back from the STORE, not the API: the point is that the write reached the real thing.
        WorkflowCheckpoint? stored = await fixture.Store.LoadAsync(claimed.RunId, default);
        stored.ShouldNotBeNull();
        WorkflowCheckpointSerializer.ProjectIndex(stored!.Value.Utf8).Status.ShouldBe(WorkflowRunStatus.Running);
        WorkflowCheckpointSerializer.TryReadSequence(stored.Value.Utf8, out long sequence).ShouldBeTrue();
        sequence.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_superseded_save_is_raised_rather_than_reported_as_durable()
    {
        // The one failure the save operation exists to make impossible. A client that swallowed this would leave the
        // runner believing a checkpoint is durable when the store never took it.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        byte[] advanced = Checkpoint("run-1", WorkflowRunStatus.Running, sequence: 2);
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(advanced);
        await fixture.Client.Checkpoints.SaveAsync(claimed.RunId, advanced, index, WorkflowEtag.None, default);

        CheckpointSupersededException refused = await Should.ThrowAsync<CheckpointSupersededException>(
            async () => await fixture.Client.Checkpoints.SaveAsync(claimed.RunId, advanced, index, WorkflowEtag.None, default));

        refused.ProposedSequence.ShouldBe(2);
        refused.AcceptedSequence.ShouldBe(3);
    }

    [TestMethod]
    public async Task A_lease_renews_and_keeps_its_epoch()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        DateTimeOffset extended = await fixture.Client.RenewAsync(claimed.RunId, TimeSpan.FromMinutes(5));

        extended.ShouldBe(T0 + TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(5));

        // The client threads the same token across the renewal, so the run keeps working.
        (await fixture.Client.Checkpoints.LoadAsync(claimed.RunId, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_lapsed_lease_is_raised_as_lost_rather_than_silently_renewed()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        await Should.ThrowAsync<RunnerLeaseLostException>(async () => await fixture.Client.RenewAsync(claimed.RunId));

        // Having lost it, the client stops presenting it: the next operation fails without a round trip.
        await Should.ThrowAsync<RunnerLeaseLostException>(async () => await fixture.Client.Checkpoints.LoadAsync(claimed.RunId, default));
    }

    [TestMethod]
    public async Task A_released_run_is_claimable_by_a_peer_at_once()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        await fixture.Client.ReleaseAsync(claimed.RunId);

        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Releasing_a_run_the_client_does_not_hold_does_nothing()
    {
        // So a runner can release in a finally without first working out whether it still holds the lease.
        await using Fixture fixture = await Fixture.StartAsync();

        await Should.NotThrowAsync(async () => await fixture.Client.ReleaseAsync(new WorkflowRunId("never")));
    }

    [TestMethod]
    public async Task Operating_on_a_run_this_client_never_claimed_is_refused_without_a_round_trip()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        await fixture.Client.TryClaimAsync([Version]);

        // The peer holds no lease for this run, so it has nothing to present and never asks.
        await Should.ThrowAsync<RunnerLeaseLostException>(
            async () => await fixture.PeerClient.Checkpoints.LoadAsync(new WorkflowRunId("run-1"), default));
    }

    private static byte[] Checkpoint(string runId, WorkflowRunStatus status, long sequence)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId(runId),
            Version,
            status,
            cursor: 0,
            sequence,
            T0,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            environment: Production,
            updatedAt: T0);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient runnerHttp;
        private readonly HttpClient peerHttp;
        private readonly HttpClientTransport runnerTransport;
        private readonly HttpClientTransport peerTransport;

        private Fixture(
            WebApplication app,
            InMemoryWorkflowStateStore store,
            TestClock clock,
            HttpClient runnerHttp,
            HttpClientTransport runnerTransport,
            ArazzoRunnerClient client,
            HttpClient peerHttp,
            HttpClientTransport peerTransport,
            ArazzoRunnerClient peer)
        {
            this.app = app;
            this.Store = store;
            this.Clock = clock;
            this.runnerHttp = runnerHttp;
            this.runnerTransport = runnerTransport;
            this.Client = client;
            this.peerHttp = peerHttp;
            this.peerTransport = peerTransport;
            this.PeerClient = peer;
        }

        public InMemoryWorkflowStateStore Store { get; }

        public TestClock Clock { get; }

        public ArazzoRunnerClient Client { get; }

        public ArazzoRunnerClient PeerClient { get; }

        public static async Task<Fixture> StartAsync()
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
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Principal", out Microsoft.Extensions.Primitives.StringValues principal))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", principal.ToString())], "test"));
                }

                await next(context);
            });

            app.MapArazzoRunnerApi(store, bindings, requireAuthorization: false, timeProvider: clock);
            await app.StartAsync();

            (HttpClient runnerHttp, HttpClientTransport runnerTransport, ArazzoRunnerClient runner) = Connect(app, Runner);
            (HttpClient peerHttp, HttpClientTransport peerTransport, ArazzoRunnerClient peer) = Connect(app, Peer);
            return new Fixture(app, store, clock, runnerHttp, runnerTransport, runner, peerHttp, peerTransport, peer);
        }

        public async ValueTask SeedAsync(string runId, WorkflowRunStatus status)
        {
            byte[] checkpoint = Checkpoint(runId, status, sequence: 1);
            await this.Store.SaveAsync(
                new WorkflowRunId(runId),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Client.DisposeAsync();
            await this.PeerClient.DisposeAsync();
            await this.runnerTransport.DisposeAsync();
            await this.peerTransport.DisposeAsync();
            this.runnerHttp.Dispose();
            this.peerHttp.Dispose();
            await this.app.DisposeAsync();
        }

        // One transport per principal, because the principal is what the server scopes every operation by: this is how
        // a deployment's two runners differ, so the test's two clients differ the same way.
        private static (HttpClient Http, HttpClientTransport Transport, ArazzoRunnerClient Client) Connect(WebApplication app, string principal)
        {
            HttpClient http = app.GetTestClient();
            http.DefaultRequestHeaders.Add("X-Test-Principal", principal);
            var transport = new HttpClientTransport(http);
            return (http, transport, new ArazzoRunnerClient(transport));
        }
    }
}