// <copyright file="DurableRunPauseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the §18 additive durable pause seam: a durable run given a <see cref="WorkflowPauseConfig"/> for an
/// advance suspends at the configured stop point (after-each-step or a breakpoint cursor) as a
/// <see cref="WorkflowRunStatus.Suspended"/> run carrying a <see cref="WorkflowWaitKind.Pause"/> wait with no
/// wake trigger — so the worker's due-timer and awaiting-message queries both skip it — while a run with no
/// pause configured runs exactly as before.
/// </summary>
[TestClass]
public class DurableRunPauseTests
{
    // The onboard 2-step workflow (verbatim from HostedWorkflowResumerTests, a proven-runnable fixture):
    // createAccount is state index 0, verifyIdentity is state index 1. The durable executor checkpoints at the
    // NEXT cursor after a step, so a checkpoint at cursor 1 fires after createAccount (before verifyIdentity),
    // a checkpoint at cursor 2 fires after verifyIdentity, and state 2 (past the last step) completes the run.
    private const string TwoStepWorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Onboard", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "api", "url": "./api.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "onboard",
              "inputs": { "type": "object", "properties": { "email": { "type": "string" } } },
              "steps": [
                {
                  "stepId": "createAccount",
                  "operationId": "createAccount",
                  "outputs": { "accountId": "$response.body#/accountId" }
                },
                {
                  "stepId": "verifyIdentity",
                  "operationId": "verifyIdentity",
                  "parameters": [ { "name": "accountId", "in": "path", "value": "$steps.createAccount.outputs.accountId" } ],
                  "requestBody": { "contentType": "application/json", "payload": { "fullName": "Ada Lovelace" } },
                  "outputs": { "score": "$response.body#/score", "applicant": "$response.body#/applicant" }
                }
              ]
            }
          ]
        }
        """;

    private const string TwoStepOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Api", "version": "1.0.0" },
          "paths": {
            "/accounts": {
              "post": {
                "operationId": "createAccount",
                "responses": { "201": { "description": "created", "content": { "application/json": { "schema": { "type": "object", "properties": { "accountId": { "type": "string" } } } } } } }
              }
            },
            "/accounts/{accountId}/identity": {
              "post": {
                "operationId": "verifyIdentity",
                "parameters": [ { "name": "accountId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "requestBody": { "content": { "application/json": { "schema": { "type": "object", "properties": { "fullName": { "type": "string" } } } } } },
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "score": { "type": "number" }, "applicant": { "type": "object", "properties": { "fullName": { "type": "string" }, "country": { "type": "string" } } } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task After_each_step_pause_advances_one_step_per_resume_then_runs_to_completion()
    {
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);

        var afterEachStep = new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>());

        // Advance 1: from cursor 0, createAccount runs, then the after-each-step pause fires at cursor 1.
        (await AdvanceAsync(runStore, id, resume, afterEachStep)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 1);
        transport.Requests.Count.ShouldBe(1);

        // Advance 2: from cursor 1, verifyIdentity runs, then the after-each-step pause fires at cursor 2.
        (await AdvanceAsync(runStore, id, resume, afterEachStep)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 2);
        transport.Requests.Count.ShouldBe(2);

        // Advance 3: no pause — the run runs off the end of the step list and completes.
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Completed);
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed.ShouldNotBeNull();
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_breakpoint_pauses_at_the_breakpoint_cursor_and_nothing_past_it_ran()
    {
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);

        // A breakpoint on cursor 1 (before verifyIdentity): the run pauses there, having run only createAccount —
        // not before (cursor 0 is the start cursor, guarded out) and not after (verifyIdentity has not run).
        var breakpoint = new WorkflowPauseConfig(AfterEachStep: false, new HashSet<int> { 1 });
        (await AdvanceAsync(runStore, id, resume, breakpoint)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 1);
        transport.Requests.Count.ShouldBe(1);

        // Resuming with the same breakpoint (nothing at cursor 2) runs off the end to completion.
        (await AdvanceAsync(runStore, id, resume, breakpoint)).ShouldBe(WorkflowRunResultKind.Completed);
        transport.Requests.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_paused_run_is_invisible_to_the_worker_wait_queries()
    {
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);
        (await AdvanceAsync(runStore, id, resume, new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>())))
            .ShouldBe(WorkflowRunResultKind.Suspended);

        // The run IS genuinely suspended in the store, with a Pause wait...
        using (WorkflowRun? paused = await WorkflowRun.ResumeAsync(runStore, id))
        {
            paused.ShouldNotBeNull();
            paused!.Status.ShouldBe(WorkflowRunStatus.Suspended);
            paused.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Pause);
        }

        // ...yet neither wait-index query surfaces it: a Pause wait projects neither DueAt nor AwaitingChannel.
        var index = (IWorkflowWaitIndex)runStore;
        (await CollectAsync(index.QueryDueAsync(DateTimeOffset.MaxValue, default))).ShouldBeEmpty();
        (await CollectAsync(index.QueryAwaitingAsync("any-channel", null, default))).ShouldBeEmpty();

        // And the worker leaves it alone — a resumer that throws if the worker ever re-enters the paused run.
        var worker = new WorkflowWorker(runStore, "test-worker");
        WorkflowResumer mustNotRun = (run, ct) => throw new InvalidOperationException("the worker resumed a paused run");
        (await worker.ResumeDueTimersAsync(mustNotRun, default)).ShouldBe(0);
        (await worker.DeliverMessageAsync("any-channel", null, default, mustNotRun, default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task A_run_with_no_pause_config_runs_straight_to_completion()
    {
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);

        // No SetPause on the advance: the run behaves exactly as an ordinary durable run and completes.
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Completed);

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed.ShouldNotBeNull();
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);
        transport.Requests.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_persisted_pause_config_is_honoured_by_a_reload_that_never_calls_SetPause()
    {
        // §18 slice R1: the pause configuration is PERSISTED, so a claiming runner (a different process) that only
        // reloads the checkpoint — never calling SetPause — still honours the same stops.
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);

        // Advance 1 configures after-each-step and pauses at cursor 1, persisting the config into the checkpoint.
        (await AdvanceAsync(runStore, id, resume, new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>())))
            .ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 1);

        // Advance 2 passes NO pause config (as a claiming runner would): the PERSISTED config still fires, so the
        // run single-steps to cursor 2 rather than running to completion.
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 2);
        transport.Requests.Count.ShouldBe(2);

        // Advance 3 (still no SetPause) runs off the end — cursor 2 is past the last step — to completion.
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Completed);
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed.ShouldNotBeNull();
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_resume_requested_paused_run_becomes_claimable_a_runner_advances_it_and_the_marker_clears()
    {
        // §18 slice R1: the CONTROL PLANE marks a paused run resume-claimable (no execution); a SEPARATE runner
        // then surfaces it via QueryClaimable, claims it, applies the persisted config, advances it, and the
        // marker clears — so it is not perpetually re-claimable.
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);
        var dispatch = (IWorkflowDispatchIndex)runStore;

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);
        var afterEachStep = new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>());

        // Pause at cursor 1. A plainly-paused run is NOT dispatch-claimable (as an interactive debug pause is today).
        (await AdvanceAsync(runStore, id, resume, afterEachStep)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 1);
        (await ClaimableAsync(dispatch, versionId)).ShouldNotContain(id);

        // The control plane marks it resume-claimable WITHOUT executing anything: it stays Suspended + Pause, and is
        // now surfaced as claimable.
        await RequestResumeAsync(runStore, id, afterEachStep);
        await AssertPausedAtAsync(runStore, id, cursor: 1);
        (await ClaimableAsync(dispatch, versionId)).ShouldContain(id);

        // A separate runner claims + advances applying the PERSISTED config (never SetPause): it pauses at cursor 2
        // and the marker clears, so the run is not re-surfaced while merely paused.
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 2);
        (await ClaimableAsync(dispatch, versionId)).ShouldNotContain(id);
        transport.Requests.Count.ShouldBe(2);

        // Only the NEXT explicit resume makes it claimable again; resuming with no further stops runs to completion.
        await RequestResumeAsync(runStore, id, pause: null);
        (await ClaimableAsync(dispatch, versionId)).ShouldContain(id);
        (await AdvanceAsync(runStore, id, resume, pause: null)).ShouldBe(WorkflowRunResultKind.Completed);
        (await ClaimableAsync(dispatch, versionId)).ShouldNotContain(id);
    }

    [TestMethod]
    public async Task The_WorkflowDispatcher_actually_claims_and_advances_a_resume_requested_run()
    {
        // §18 slice R1: the REAL WorkflowDispatcher (not just the store's QueryClaimable) must ADMIT a
        // resume-requested Suspended run and drive it through the resumer. The run the dispatcher constructs
        // loads the PERSISTED pause config (the dispatcher never calls SetPause) and honours it, then the marker
        // clears so the run is not re-dispatched. A plainly-paused run (never marked) is never dispatched.
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();
        MockApiTransport transport = Transport();
        using var loader = new WorkflowExecutorLoader();
        WorkflowResumer resume = Resumer(catalog, loader, transport);
        var dispatcher = new WorkflowDispatcher(runStore, "runner-1", runnerEnvironment: "development");
        var dispatch = (IWorkflowDispatchIndex)runStore;

        WorkflowRunId id = await EnqueueAsync(runStore, versionId);
        var afterEachStep = new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>());

        // Pause at cursor 1 (persisting the config). A plainly-paused run is NOT dispatched by the real dispatcher.
        (await AdvanceAsync(runStore, id, resume, afterEachStep)).ShouldBe(WorkflowRunResultKind.Suspended);
        await AssertPausedAtAsync(runStore, id, cursor: 1);
        (await dispatcher.DispatchClaimableAsync([versionId], resume, default)).ShouldBe(0);

        // The control plane marks it resume-claimable; the dispatcher now claims AND advances it, applying the
        // PERSISTED config (it constructs the run itself and never calls SetPause), pausing at cursor 2.
        await RequestResumeAsync(runStore, id, afterEachStep);
        (await dispatcher.DispatchClaimableAsync([versionId], resume, default)).ShouldBe(1);
        await AssertPausedAtAsync(runStore, id, cursor: 2);
        transport.Requests.Count.ShouldBe(2);

        // The marker cleared on the advance's first checkpoint, so a second dispatch is a no-op.
        (await ClaimableAsync(dispatch, versionId)).ShouldNotContain(id);
        (await dispatcher.DispatchClaimableAsync([versionId], resume, default)).ShouldBe(0);
    }

    private static async Task<(InMemoryWorkflowCatalogStore Catalog, string VersionId)> CatalogAsync()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        byte[] package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(TwoStepWorkflowJson),
            [new("api", Encoding.UTF8.GetBytes(TwoStepOpenApi))]);
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("onboard", package, Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        ((bool)version.Runnable).ShouldBeTrue();
        return (catalog, (string)version.Ref.WorkflowId);
    }

    private static MockApiTransport Transport()
    {
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Post, "/accounts", 201, """{"accountId":"acc-42"}""");
        transport.SetResponse(OperationMethod.Post, "/accounts/{accountId}/identity", 200, """{"score":0.92,"applicant":{"fullName":"Ada Lovelace","country":"GB"}}""");
        return transport;
    }

    private static WorkflowResumer Resumer(InMemoryWorkflowCatalogStore catalog, WorkflowExecutorLoader loader, MockApiTransport transport)
    {
        var resumer = new HostedWorkflowResumer(
            catalog,
            loader,
            (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));
        return resumer.AsResumer();
    }

    private static async Task<WorkflowRunId> EnqueueAsync(IWorkflowStateStore runStore, string versionId)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", versionId, inputs.RootElement, environment: "development");
        await run.EnqueueAsync(default);
        return run.Id;
    }

    // One advance: load a fresh run instance (as a worker would), optionally set the per-advance pause config,
    // and drive it through the resumer.
    private static async Task<WorkflowRunResultKind> AdvanceAsync(IWorkflowStateStore runStore, WorkflowRunId id, WorkflowResumer resume, WorkflowPauseConfig? pause)
    {
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(runStore, id);
        run.ShouldNotBeNull();
        if (pause is { } p)
        {
            run!.SetPause(p);
        }

        return await resume(run!, default);
    }

    // The control-plane mark-resume-claimable step: load the paused run and stamp the resume-requested marker
    // (persisting the requested pause config) WITHOUT executing — no resumer is driven here.
    private static async Task RequestResumeAsync(IWorkflowStateStore runStore, WorkflowRunId id, WorkflowPauseConfig? pause)
    {
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(runStore, id);
        run.ShouldNotBeNull();
        await run!.RequestResumeAsync(pause, default);
    }

    private static async Task<List<WorkflowRunId>> ClaimableAsync(IWorkflowDispatchIndex dispatch, string versionId)
        => await CollectAsync(dispatch.QueryClaimableAsync([versionId], DateTimeOffset.UtcNow, default));

    private static async Task AssertPausedAtAsync(IWorkflowStateStore runStore, WorkflowRunId id, int cursor)
    {
        using WorkflowRun? paused = await WorkflowRun.ResumeAsync(runStore, id);
        paused.ShouldNotBeNull();
        paused!.Status.ShouldBe(WorkflowRunStatus.Suspended);
        paused.Cursor.ShouldBe(cursor);
        paused.Wait.ShouldNotBeNull();
        paused.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Pause);
    }

    private static async Task<List<WorkflowRunId>> CollectAsync(IAsyncEnumerable<WorkflowRunId> source)
    {
        var ids = new List<WorkflowRunId>();
        await foreach (WorkflowRunId id in source)
        {
            ids.Add(id);
        }

        return ids;
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");
}