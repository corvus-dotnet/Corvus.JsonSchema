// <copyright file="CheckpointIndexIntegrityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The control plane's half of ADR 0065's mutual distrust: it trusts the runner with the run's working state and not
/// with the run's identity. The index a save carries is projected from the runner's own submitted bytes, and it holds
/// the environment, the workflow id and the security tags that decide who may see and claim the run — so a save that
/// changes them is a runner re-pointing its own run at another tenant, not a runner reporting progress.
/// </summary>
/// <remarks>
/// <para>
/// The attack the refusal prevents: a runner saves its run with a different <c>environment</c>, releases the lease, and
/// the victim environment's runner claims what is now, by the index, one of its own runs — then executes
/// attacker-authored state with the victim's credentials against the victim's sources. The same primitive re-tags a run
/// into the platform group, or out of its owner's reach so they cannot see it.
/// </para>
/// <para>
/// ADR 0065's eventual answer is the runner MAC over the runner-authored region, which is phase B. Until that exists
/// the server-side comparison is the only thing standing in the way, and no store backend performs it: all nine write
/// the submitted index verbatim.
/// </para>
/// </remarks>
[TestClass]
public sealed class CheckpointIndexIntegrityTests
{
    private static readonly WorkflowRunId Run = new("run-1");

    [TestMethod]
    public async Task A_save_may_not_move_the_run_to_another_environment()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");
        (await coordinator.SaveAsync(Run, established, Project(established), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        byte[] moved = Checkpoint(sequence: 2, environment: "victim-tenant", workflowId: "onboard");
        CheckpointSaveResult result = await coordinator.SaveAsync(Run, moved, Project(moved), 2, default);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);

        // Refusing is only half of it. The stored row must still say what it said, or the refusal reported a failure
        // the store had already accepted.
        WorkflowCheckpoint stored = (await store.LoadAsync(Run, default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8).Environment.ShouldBe("production");
    }

    [TestMethod]
    public async Task A_save_may_not_change_the_workflow_the_run_is_of()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");
        await coordinator.SaveAsync(Run, established, Project(established), 1, default);

        byte[] rewritten = Checkpoint(sequence: 2, environment: "production", workflowId: "payroll");
        CheckpointSaveResult result = await coordinator.SaveAsync(Run, rewritten, Project(rewritten), 2, default);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Run, default))!.Value.Utf8).WorkflowId.ShouldBe("onboard");
    }

    [TestMethod]
    public async Task A_save_may_not_retag_the_run()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard", securityTags: Tags("sys:tenant", "acme"));
        await coordinator.SaveAsync(Run, established, Project(established), 1, default);

        byte[] retagged = Checkpoint(sequence: 2, environment: "production", workflowId: "onboard", securityTags: Tags("sys:tenant", "globex"));
        CheckpointSaveResult result = await coordinator.SaveAsync(Run, retagged, Project(retagged), 2, default);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
    }

    [TestMethod]
    public async Task A_save_that_only_advances_the_run_is_applied()
    {
        // The point of the check is to leave ordinary progress alone: status, cursor and timings are exactly what a
        // runner is trusted to report, and a guard that refused them would refuse every real advance.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] first = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard", status: WorkflowRunStatus.Running, cursor: 0);
        await coordinator.SaveAsync(Run, first, Project(first), 1, default);

        byte[] advanced = Checkpoint(sequence: 2, environment: "production", workflowId: "onboard", status: WorkflowRunStatus.Completed, cursor: 3);
        CheckpointSaveResult result = await coordinator.SaveAsync(Run, advanced, Project(advanced), 2, default);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Run, default))!.Value.Utf8).Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task The_first_save_establishes_the_identity_it_carries()
    {
        // Nothing to compare against yet, so a run's first checkpoint is what sets its environment and workflow.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] first = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");

        (await coordinator.SaveAsync(Run, first, Project(first), 1, default)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Run, default))!.Value.Utf8).Environment.ShouldBe("production");
    }

    [TestMethod]
    public async Task Identity_is_checked_against_the_store_even_when_a_load_preceded_the_save()
    {
        // The warm path: a load seeds the coordinator's per-run state, and the save that follows does not re-read the
        // row. The identity has to have been captured at that point, or the check silently applies only to cold slots —
        // which is to say only to the path an attacker is least likely to be on.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");
        await store.SaveAsync(Run, established, Project(established), WorkflowEtag.None, default);

        (await coordinator.LoadAsync(Run, default)).ShouldNotBeNull();

        byte[] moved = Checkpoint(sequence: 2, environment: "victim-tenant", workflowId: "onboard");
        CheckpointSaveResult result = await coordinator.SaveAsync(Run, moved, Project(moved), 2, default);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Run, default))!.Value.Utf8).Environment.ShouldBe("production");
    }

    private static SecurityTagSet Tags(string key, string value)
        => SecurityTagSet.FromJsonStringOrEmpty($$"""[{"key":"{{key}}","value":"{{value}}"}]""");

    private static WorkflowRunIndexEntry Project(byte[] checkpoint) => WorkflowCheckpointSerializer.ProjectIndex(checkpoint);

    private static byte[] Checkpoint(
        long sequence,
        string environment,
        string workflowId,
        WorkflowRunStatus status = WorkflowRunStatus.Running,
        int cursor = 0,
        SecurityTagSet securityTags = default)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            Run,
            workflowId,
            status,
            cursor,
            sequence,
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            securityTags: securityTags,
            environment: environment,
            updatedAt: new DateTimeOffset(2026, 3, 4, 5, 10, 0, TimeSpan.Zero));
    }
}