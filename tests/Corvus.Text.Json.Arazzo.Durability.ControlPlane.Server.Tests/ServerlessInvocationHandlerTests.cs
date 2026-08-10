// <copyright file="ServerlessInvocationHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Serverless;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Proves the vendor-neutral <see cref="ServerlessInvocationHandler"/> core drives a whole serverless advance against
/// the runner's <em>real</em> 6b checkpoint surface: it parses an invocation, points a per-invocation checkpoint store
/// at the <c>checkpointUrl</c> it carries, restores and advances the run through the baked host, and the run's terminal
/// checkpoint lands back in the store through the live <c>GET/POST /runs/{id}/checkpoint</c> endpoints — the whole
/// function↔runner loop, in process, without the cloud runtime.
/// </summary>
[TestClass]
public sealed class ServerlessInvocationHandlerTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private static readonly WorkflowTransports EmptyTransports =
        new(ImmutableDictionary<string, IApiTransport>.Empty, WorkflowTransports.NoMessageTransports);

    private static readonly byte[] CheckpointSecret = RandomNumberGenerator.GetBytes(CheckpointToken.MinimumSecretBytes);

    [TestMethod]
    public async Task Advances_a_run_and_checkpoints_it_back_through_the_live_runner_surface()
    {
        await using Runner runner = await Runner.StartAsync();
        await SeedPendingRun(runner.Store, Run1, "wf");

        var handler = new ServerlessInvocationHandler(
            new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")),
            NoTransports,
            runner.CheckpointHandler);

        byte[] outcome = await handler.HandleAsync(Invocation(Run1, runner.CheckpointBaseUrl), default);

        // The function reported the run completed...
        Encoding.UTF8.GetString(outcome).ShouldBe("""{"outcome":"Completed"}""");

        // ...and its terminal checkpoint landed in the store through the live checkpoint endpoints.
        WorkflowCheckpoint stored = (await runner.Store.LoadAsync(Run1, default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8).Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_not_dispatchable_run_is_a_benign_empty_outcome()
    {
        await using Runner runner = await Runner.StartAsync();
        await SeedSuspendedRun(runner.Store, Run1, "wf");

        var workflow = new CompletingHostedWorkflow("wf");
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(workflow), NoTransports, runner.CheckpointHandler);

        byte[] outcome = await handler.HandleAsync(Invocation(Run1, runner.CheckpointBaseUrl), default);

        // A run merely waiting (no resume request) is not advanced: an empty outcome (the backend reads it as a benign
        // Suspended), and the workflow never ran.
        Encoding.UTF8.GetString(outcome).ShouldBe("{}");
        workflow.Ran.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Rejects_an_invocation_whose_run_id_is_outside_the_grammar()
    {
        await using Runner runner = await Runner.StartAsync();
        await SeedPendingRun(runner.Store, "run-1", "wf");

        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler);

        // The invocation body arrives from outside the function's trust boundary (the dispatch fabric), so the
        // run-id grammar is validated at the parse (ADR 0065 §9: at every ingress, before any store touch) — the
        // handler must refuse rather than advance the seeded run.
        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(
            async () => await handler.HandleAsync(Invocation("run-1", runner.CheckpointBaseUrl), default));
        refusal.Message.ShouldContain("32 lowercase hex");
    }

    [TestMethod]
    public async Task Rejects_an_invocation_missing_the_run_id()
    {
        await using Runner runner = await Runner.StartAsync();
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler);

        byte[] body = Encoding.UTF8.GetBytes($$"""{"checkpointUrl":"{{runner.CheckpointBaseUrl}}"}""");
        await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
    }

    [TestMethod]
    public async Task Rejects_an_invocation_missing_the_checkpoint_url()
    {
        await using Runner runner = await Runner.StartAsync();
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler);

        byte[] body = Encoding.UTF8.GetBytes($$"""{"runId":"{{Run1}}"}""");
        await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        var resolver = new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf"));
        using var handler = new HttpClientHandler();

        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(null!, NoTransports, handler));
        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(resolver, null!, handler));
        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(resolver, NoTransports, null!));
    }

    private static WorkflowTransports NoTransports(WorkflowDescriptor descriptor, SecurityTagSet tags) => EmptyTransports;

    // The invocation carries the run-scoped checkpoint token the dispatcher minted (ADR 0062), which the handler sets as
    // Authorization: Bearer on its per-invocation checkpoint client. Without it the surface refuses every callback.
    private static byte[] Invocation(string runId, string checkpointUrl)
    {
        string token = CheckpointToken.Issue(CheckpointSecret, runId, DateTimeOffset.UtcNow.AddMinutes(10));
        return Encoding.UTF8.GetBytes($$"""{"runId":"{{runId}}","environment":"development","checkpointUrl":"{{checkpointUrl}}","checkpointToken":"{{token}}"}""");
    }

    private static async Task SeedPendingRun(IWorkflowStateStore store, string runId, string workflowId)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, "development");
        await run.EnqueueAsync(default);
    }

    private static async Task SeedSuspendedRun(IWorkflowStateStore store, string runId, string workflowId)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, "development");
        await run.SuspendForTimerAsync(cursor: 1, TimeSpan.FromMinutes(5), default);
    }

    // A minimal in-hand executor that completes the run (persisting a terminal checkpoint through the run's store — here
    // the HTTP checkpoint store — so the advance is observable back in the runner's store).
    private sealed class CompletingHostedWorkflow(string workflowId) : IHostedWorkflow
    {
        public bool Ran { get; private set; }

        public WorkflowDescriptor Descriptor { get; } = new(workflowId, [], []);

        public async ValueTask<WorkflowRunResultKind> RunAsync(
            IReadOnlyDictionary<string, IApiTransport> apiTransports,
            IReadOnlyDictionary<string, IMessageTransport> messageTransports,
            JsonWorkspace workspace,
            JsonElement inputs,
            IWorkflowRun run,
            CancellationToken cancellationToken)
        {
            this.Ran = true;
            await run.CompleteAsync(default, cancellationToken).ConfigureAwait(false);
            return WorkflowRunResultKind.Completed;
        }
    }

    // A minimal control-plane host exposing the 6b checkpoint surface over a TestServer, plus the raw message handler a
    // serverless function's checkpoint client runs over and the base address it targets.
    private sealed class Runner(WebApplication app, InMemoryWorkflowStateStore store, HttpMessageHandler checkpointHandler, string checkpointBaseUrl) : IAsyncDisposable
    {
        public InMemoryWorkflowStateStore Store { get; } = store;

        public HttpMessageHandler CheckpointHandler { get; } = checkpointHandler;

        public string CheckpointBaseUrl { get; } = checkpointBaseUrl;

        public static async Task<Runner> StartAsync()
        {
            var store = new InMemoryWorkflowStateStore();
            var management = new SecuredWorkflowManagement(store, "ops");
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            WebApplication app = builder.Build();
            app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Open, workflowStateStore: store, checkpointSecret: CheckpointSecret);
            await app.StartAsync();

            TestServer server = app.GetTestServer();
            return new Runner(app, store, server.CreateHandler(), server.BaseAddress.ToString());
        }

        public async ValueTask DisposeAsync()
        {
            this.CheckpointHandler.Dispose();
            await app.DisposeAsync();
        }
    }
}