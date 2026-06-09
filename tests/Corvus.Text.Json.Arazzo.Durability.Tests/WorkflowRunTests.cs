// <copyright file="WorkflowRunTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="WorkflowRun"/>: a fresh run's empty defaults, staging products and checkpointing,
/// and the Tier-1 crash-and-resume cycle — a run advanced, dropped mid-flight, then re-entered from its last
/// checkpoint with its cursor, step outputs, retry counts and correlation register restored.
/// </summary>
[TestClass]
public sealed class WorkflowRunTests
{
    private static readonly TestTimeProvider Time = new(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void A_fresh_run_starts_empty()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        using var run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, Time);

        run.Cursor.ShouldBe(0);
        run.Status.ShouldBe(WorkflowRunStatus.Pending);
        run.Etag.IsNone.ShouldBeTrue();
        run.CorrelationTokens.ShouldBeEmpty();
        run.GetRetryCount("getPet").ShouldBe(0);
        run.TryGetStepOutputs("getPet", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Staging_undefined_outputs_is_ignored()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, Time);

        run.SetStepOutputs("noop", default);

        run.TryGetStepOutputs("noop", out _).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Checkpoint_then_resume_restores_state()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "order-42";
        byte[] token = Encoding.UTF8.GetBytes("corr-9");

        // Run advances one step, then "crashes" (the run object and its workspace are dropped).
        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{ "inputs": { "petId": 7 }, "getPet": { "status": "available" } }"""u8.ToArray()))
        {
            using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement.GetProperty("inputs"u8), Time);
            run.SetStepOutputs("getPet", doc.RootElement.GetProperty("getPet"u8));
            run.SetRetryCount("getPet", 1);
            run.CorrelationTokens["orderRef"] = token;

            await run.CheckpointAsync(cursor: 1, default);

            run.Cursor.ShouldBe(1);
            run.Status.ShouldBe(WorkflowRunStatus.Running);
            run.Etag.IsNone.ShouldBeFalse();
        }

        // A worker re-enters from the durable checkpoint.
        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, Time);

        resumed.ShouldNotBeNull();
        resumed.Cursor.ShouldBe(1);
        resumed.WorkflowId.ShouldBe("wf");
        resumed.Status.ShouldBe(WorkflowRunStatus.Running);
        resumed.GetRetryCount("getPet").ShouldBe(1);
        resumed.CorrelationTokens["orderRef"].ShouldBe(token);
        resumed.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
        resumed.TryGetStepOutputs("getPet", out JsonElement getPet).ShouldBeTrue();
        getPet.GetProperty("status"u8).GetString().ShouldBe("available");
    }

    [TestMethod]
    public async Task A_resumed_run_advances_and_completes()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "order-43";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{ "inputs": {}, "stepA": { "a": 1 } }"""u8.ToArray()))
        {
            using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement.GetProperty("inputs"u8), Time);
            run.SetStepOutputs("stepA", doc.RootElement.GetProperty("stepA"u8));
            await run.CheckpointAsync(cursor: 1, default);
        }

        using (WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, Time))
        {
            resumed.ShouldNotBeNull();
            using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{ "result": "done" }"""u8.ToArray());
            resumed.SetStepOutputs("stepB", doc2.RootElement);
            await resumed.CheckpointAsync(cursor: 2, default);
            await resumed.CompleteAsync(doc2.RootElement, default);
            resumed.Status.ShouldBe(WorkflowRunStatus.Completed);
        }

        using WorkflowRun? final = await WorkflowRun.ResumeAsync(store, id, Time);
        final.ShouldNotBeNull();
        final.Status.ShouldBe(WorkflowRunStatus.Completed);
        final.TryGetStepOutputs("stepA", out _).ShouldBeTrue();
        final.TryGetStepOutputs("stepB", out _).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Resume_of_an_unknown_run_returns_null()
    {
        var store = new InMemoryWorkflowStateStore();

        (await WorkflowRun.ResumeAsync(store, "nope", Time)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_resumed_run_conflicts_when_a_newer_writer_won()
    {
        var store = new InMemoryWorkflowStateStore();
        WorkflowRunId id = "order-44";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray()))
        {
            using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, Time);
            await run.CheckpointAsync(cursor: 1, default);
        }

        // Two workers both load the same checkpoint (e.g. a partition hand-off race).
        using WorkflowRun? workerA = await WorkflowRun.ResumeAsync(store, id, Time);
        using WorkflowRun? workerB = await WorkflowRun.ResumeAsync(store, id, Time);

        await workerA!.CheckpointAsync(cursor: 2, default);

        // Worker B is now stale; its next checkpoint must be rejected by optimistic concurrency.
        await Should.ThrowAsync<WorkflowConflictException>(async () => await workerB!.CheckpointAsync(cursor: 2, default));
    }
}