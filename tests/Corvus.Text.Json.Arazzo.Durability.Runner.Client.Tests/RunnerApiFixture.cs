// <copyright file="RunnerApiFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The runner API served over real HTTP into a real store, with two clients that differ the way a deployment's two
/// runners differ: by principal. Nothing here is a stand-in. The generated server, the generated client and the store
/// are the production ones, so a test that passes against this fixture is evidence about the deployed thing.
/// </summary>
internal sealed class RunnerApiFixture : IAsyncDisposable
{
    public const string Runner = "runner-a";
    public const string Peer = "runner-b";
    public const string Production = "production";
    public const string Version = "adopt-v3";

    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly WebApplication app;
    private readonly HttpClient runnerHttp;
    private readonly HttpClient peerHttp;
    private readonly HttpClientTransport runnerTransport;
    private readonly HttpClientTransport peerTransport;

    private RunnerApiFixture(
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

    public static async Task<RunnerApiFixture> StartAsync()
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
        return new RunnerApiFixture(app, store, clock, runnerHttp, runnerTransport, runner, peerHttp, peerTransport, peer);
    }

    public static byte[] Checkpoint(string runId, WorkflowRunStatus status, long sequence, WorkflowWait? wait = null, string? workflowId = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId(runId),
            workflowId ?? Version,
            status,
            cursor: 0,
            sequence,
            T0,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            wait: wait,
            environment: Production,
            updatedAt: T0);
    }

    /// <summary>Seeds a run suspended on a wait, which is the only state a timer or a message can resume.</summary>
    public ValueTask SeedWaitingAsync(string runId, WorkflowWait wait, string? workflowId = null)
        => this.SaveAsync(runId, Checkpoint(runId, WorkflowRunStatus.Suspended, sequence: 1, wait, workflowId));

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

    private async ValueTask SaveAsync(string runId, byte[] checkpoint)
        => await this.Store.SaveAsync(new WorkflowRunId(runId), checkpoint, WorkflowCheckpointSerializer.ProjectIndex(checkpoint), WorkflowEtag.None, default);

    internal sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}