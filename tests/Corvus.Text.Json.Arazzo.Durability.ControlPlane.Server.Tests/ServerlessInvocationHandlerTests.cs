// <copyright file="ServerlessInvocationHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
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
            runner.CheckpointHandler,
            ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] outcome = await handler.HandleAsync(Invocation(Run1, runner.CheckpointBaseUrl), default);

        // The function reported the run completed...
        Encoding.UTF8.GetString(outcome).ShouldBe("""{"outcome":"Completed"}""");

        // ...and its terminal checkpoint landed in the store through the live checkpoint endpoints.
        WorkflowCheckpoint stored = (await runner.Store.LoadAsync(new WorkflowRunAddress("development", new WorkflowRunId(Run1)), default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Row).Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_not_dispatchable_run_is_a_benign_empty_outcome()
    {
        await using Runner runner = await Runner.StartAsync();
        await SeedSuspendedRun(runner.Store, Run1, "wf");

        var workflow = new CompletingHostedWorkflow("wf");
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(workflow), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

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

        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

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
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] body = Encoding.UTF8.GetBytes($$"""{"checkpointUrl":"{{runner.CheckpointBaseUrl}}"}""");
        await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
    }

    [TestMethod]
    public async Task Rejects_an_invocation_missing_the_checkpoint_url()
    {
        await using Runner runner = await Runner.StartAsync();
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] body = Encoding.UTF8.GetBytes($$"""{"runId":"{{Run1}}","environment":"development"}""");
        await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
    }

    [TestMethod]
    public async Task Rejects_an_invocation_missing_the_environment()
    {
        // The environment is half the run's address (ADR 0065 decision 9) and is required at this ingress: an
        // invocation without one cannot address the run's checkpoints at all.
        await using Runner runner = await Runner.StartAsync();
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] body = Encoding.UTF8.GetBytes($$"""{"runId":"{{Run1}}","checkpointUrl":"{{runner.CheckpointBaseUrl}}"}""");
        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
        refusal.Message.ShouldContain("environment");
    }

    [TestMethod]
    public async Task Rejects_an_invocation_whose_environment_is_outside_the_grammar()
    {
        await using Runner runner = await Runner.StartAsync();
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] body = Encoding.UTF8.GetBytes($$"""{"runId":"{{Run1}}","environment":"Not_A_Label","checkpointUrl":"{{runner.CheckpointBaseUrl}}"}""");
        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
        refusal.Message.ShouldContain("grammar");
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        var resolver = new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf"));
        using var handler = new HttpClientHandler();

        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(null!, NoTransports, handler, Origins));
        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(resolver, null!, handler, Origins));
        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(resolver, NoTransports, null!, Origins));

        // The checkpoint origins are required: a function with no list has nowhere it may load a run from.
        Should.Throw<ArgumentNullException>(() => new ServerlessInvocationHandler(resolver, NoTransports, handler, null!));
    }

    [TestMethod]
    public async Task An_invocation_naming_a_checkpoint_origin_the_function_was_not_deployed_with_is_refused_untouched()
    {
        // P1-11 of the 2026-08-07 audit: the function took any absolute checkpointUrl, so a caller could point it at a
        // checkpoint surface of their own and have it advance what it found there with the function's source credentials.
        await using Runner runner = await Runner.StartAsync();
        await SeedPendingRun(runner.Store, Run1, "wf");
        var handler = new ServerlessInvocationHandler(
            new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")),
            NoTransports,
            runner.CheckpointHandler,
            ServerlessCheckpointOrigins.Parse("https://runner.example/"));

        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(
            async () => await handler.HandleAsync(Invocation(Run1, runner.CheckpointBaseUrl), default));
        refusal.Message.ShouldContain("checkpoint origin");

        // Nothing was loaded or advanced: the run is as it was seeded.
        WorkflowCheckpoint stored = (await runner.Store.LoadAsync(new WorkflowRunAddress("development", new WorkflowRunId(Run1)), default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Row).Status.ShouldBe(WorkflowRunStatus.Pending);
    }

    private static readonly ServerlessCheckpointOrigins Origins = ServerlessCheckpointOrigins.Parse("https://runner.example/");

    [TestMethod]
    public async Task An_invocation_carrying_no_checkpoint_token_is_refused_before_anything_is_loaded()
    {
        // V-42 of the 2026-08-07 audit, ADR 0062. The token used to be optional on the function side, a leftover of the
        // design the ADR removed: the surface refuses every callback without one, so a tokenless invocation could only
        // load a run it could never save. It is refused at the parse, before the checkpoint surface is touched.
        await using Runner runner = await Runner.StartAsync();
        await SeedPendingRun(runner.Store, Run1, "wf");
        var handler = new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new CompletingHostedWorkflow("wf")), NoTransports, runner.CheckpointHandler, ServerlessCheckpointOrigins.Parse(runner.CheckpointBaseUrl));

        byte[] body = Encoding.UTF8.GetBytes($$"""{"runId":"{{Run1}}","environment":"development","checkpointUrl":"{{runner.CheckpointBaseUrl}}"}""");
        ArgumentException refusal = await Should.ThrowAsync<ArgumentException>(async () => await handler.HandleAsync(body, default));
        refusal.Message.ShouldContain("checkpointToken");

        WorkflowCheckpoint stored = (await runner.Store.LoadAsync(new WorkflowRunAddress("development", new WorkflowRunId(Run1)), default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Row).Status.ShouldBe(WorkflowRunStatus.Pending);
    }

    private static WorkflowTransports NoTransports(WorkflowDescriptor descriptor, SecurityTagSet tags) => EmptyTransports;

    // The invocation carries the run-scoped checkpoint token the dispatcher minted (ADR 0062), which the handler sets as
    // Authorization: Bearer on its per-invocation checkpoint client. Without it the surface refuses every callback.
    private static byte[] Invocation(string runId, string checkpointUrl)
    {
        string token = CheckpointToken.Issue(CheckpointSecret, new WorkflowRunAddress("development", new WorkflowRunId(runId)), DateTimeOffset.UtcNow.AddMinutes(10));
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
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", administrators: new InMemoryWorkflowAdministratorStore());

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