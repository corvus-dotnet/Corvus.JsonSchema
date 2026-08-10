// <copyright file="ControlPlaneCheckpointSurfaceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of how <see cref="ControlPlaneEndpointExtensions.MapArazzoControlPlane"/> wires the serverless checkpoint
/// surface: that the ADR 0062 run-scoped token is actually applied, that the surface is absent rather than open when no
/// secret is configured, and that the checkpoint coordinator is the host's single instance so ADR 0065 decision 6's
/// per-run interlock is per run rather than per component.
/// </summary>
/// <remarks>
/// These assertions exercise the control rather than assert it exists. The token primitive is already proven in
/// <c>CheckpointTokenTests</c>; what is proven here is that the control plane reaches it, which is the property that
/// was missing.
/// </remarks>
[TestClass]
public sealed class ControlPlaneCheckpointSurfaceTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private const string SeqHeader = "X-Arazzo-Checkpoint-Seq";
    private static readonly WorkflowRunId Run = new(Run1);
    private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(CheckpointToken.MinimumSecretBytes);

    [TestMethod]
    public async Task A_save_with_no_token_is_refused()
    {
        await using Host host = await Host.StartAsync(Secret);

        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, RealCheckpoint(), sequence: 1, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.Store.LoadAsync(Run, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_load_with_no_token_is_refused()
    {
        await using Host host = await Host.StartAsync(Secret);

        HttpResponseMessage response = await host.GetCheckpointAsync(Run.Value, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task A_token_minted_for_another_run_does_not_authorise_this_one()
    {
        await using Host host = await Host.StartAsync(Secret);
        string other = CheckpointToken.Issue(Secret, Run2, DateTimeOffset.UtcNow.AddMinutes(10));

        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, RealCheckpoint(), sequence: 1, token: other);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.Store.LoadAsync(Run, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_valid_token_authorises_the_run_it_was_minted_for()
    {
        await using Host host = await Host.StartAsync(Secret);
        string token = CheckpointToken.Issue(Secret, Run.Value, DateTimeOffset.UtcNow.AddMinutes(10));
        byte[] checkpoint = RealCheckpoint();

        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, checkpoint, sequence: 1, token: token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(checkpoint);
    }

    [TestMethod]
    public async Task Without_a_secret_the_surface_is_absent_rather_than_open()
    {
        // ADR 0016 forbids an insecure-by-omission posture, and ADR 0062 names the unwired surface as the gap to close.
        // A deployment that configures no checkpoint secret gets no checkpoint surface, not an unauthenticated one.
        await using Host host = await Host.StartAsync(checkpointSecret: default);

        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, RealCheckpoint(), sequence: 1, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.Store.LoadAsync(Run, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_secret_shorter_than_the_minimum_fails_the_mapping()
    {
        // A weak key is caught where it is configured, not on the first callback that presents a token signed with it.
        await Should.ThrowAsync<ArgumentException>(async () => await Host.StartAsync(new byte[CheckpointToken.MinimumSecretBytes - 1]));
    }

    [TestMethod]
    public async Task The_surface_terminates_into_the_coordinator_the_host_supplied()
    {
        // ADR 0065 decision 6: "the interlock is per run, not per component". The control plane and the runner API both
        // author checkpoints for the same run, so a host that maps both must give them ONE coordinator — two would hold
        // one gate each and let two honest components dispatch concurrently for the same run.
        //
        // Instance identity is proven through behaviour rather than reflection: the coordinator caches per-run state, so
        // a save through a coordinator whose slot is already seeded reads the store once (the save's own load), while a
        // save through a second, unseeded instance reads it twice.
        var store = new CountingCheckpointStore(new InMemoryWorkflowStateStore());
        var coordinator = new WorkflowCheckpointCoordinator(store);
        await using Host host = await Host.StartAsync(Secret, store, coordinator);

        // Seed the supplied coordinator's slot for this run, and take the load count that seeding cost.
        byte[] initial = RealCheckpoint();
        await store.SaveAsync(Run, initial, WorkflowCheckpointSerializer.ProjectIndex(initial), WorkflowEtag.None, default);
        (await coordinator.LoadAsync(Run, default)).ShouldNotBeNull();
        int loadsAfterSeeding = store.Loads;

        string token = CheckpointToken.Issue(Secret, Run.Value, DateTimeOffset.UtcNow.AddMinutes(10));
        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, RealCheckpoint(cursor: 1, sequence: 2), sequence: 2, token: token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The save found the slot already seeded, so it re-read nothing. A privately constructed coordinator would have
        // had no slot for this run and would have re-read the row to seed one.
        store.Loads.ShouldBe(loadsAfterSeeding);
    }

    private static byte[] RealCheckpoint(int cursor = 0, long sequence = 1)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            Run,
            "petWorkflow",
            WorkflowRunStatus.Running,
            cursor,
            sequence,
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            updatedAt: new DateTimeOffset(2026, 3, 4, 5, 10, 0, TimeSpan.Zero));
    }

    // Counts the store reads the coordinator makes, which is what distinguishes a seeded slot from a fresh one.
    private sealed class CountingCheckpointStore(InMemoryWorkflowStateStore inner) : IWorkflowCheckpointStore
    {
        private int loads;

        public int Loads => Volatile.Read(ref this.loads);

        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => inner.SaveAsync(id, checkpointUtf8, index, expected, cancellationToken);

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.loads);
            return inner.LoadAsync(id, cancellationToken);
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, IWorkflowCheckpointStore store) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public IWorkflowCheckpointStore Store { get; } = store;

        public static async Task<Host> StartAsync(
            ReadOnlyMemory<byte> checkpointSecret,
            IWorkflowCheckpointStore? checkpointStore = null,
            WorkflowCheckpointCoordinator? checkpoints = null)
        {
            var state = new InMemoryWorkflowStateStore();
            var management = new SecuredWorkflowManagement(state, "ops");
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), state, "ops");

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddHttpContextAccessor();

            WebApplication app = builder.Build();
            app.MapArazzoControlPlane(
                management,
                catalog,
                new InMemoryRunnerRegistry(),
                ControlPlaneSecurityMode.Open,
                workflowStateStore: state,
                checkpointSecret: checkpointSecret,
                checkpoints: checkpoints);
            await app.StartAsync();

            return new Host(app, app.GetTestClient(), checkpointStore ?? state);
        }

        public Task<HttpResponseMessage> GetCheckpointAsync(string runId, string? token)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/runs/{runId}/checkpoint"), token);

        public Task<HttpResponseMessage> PostCheckpointAsync(string runId, byte[] body, long sequence, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/runs/{runId}/checkpoint")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(SeqHeader, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return this.SendAsync(request, token);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? token)
        {
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return this.Client.SendAsync(request);
        }
    }
}