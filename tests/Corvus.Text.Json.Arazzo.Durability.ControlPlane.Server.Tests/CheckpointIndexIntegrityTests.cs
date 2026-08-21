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
/// the workflow id and the security tags that decide who may see and claim the run — so a save that changes them is a
/// runner re-pointing its own run at another tenant, not a runner reporting progress. The environment is stronger
/// still: it is the run's ADDRESS (decision 9), so the body's claim is checked structurally against the addressed
/// environment on EVERY save, first save included — no save can establish, or move to, an environment other than the
/// one the run is addressed at.
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
/// the server-side comparison is the only thing standing in the way, and no store backend performs it: all the
/// backends write the submitted index verbatim.
/// </para>
/// </remarks>
[TestClass]
public sealed class CheckpointIndexIntegrityTests
{
    private static readonly WorkflowRunId Run = new("run-1");
    private static readonly WorkflowRunAddress Address = new("production", Run);

    [TestMethod]
    public async Task A_save_may_not_move_the_run_to_another_environment()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");
        (await SaveAsync(coordinator, established, 1)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);

        byte[] moved = Checkpoint(sequence: 2, environment: "victim-tenant", workflowId: "onboard");
        CheckpointSaveResult result = await SaveAsync(coordinator, moved, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);

        // Refusing is only half of it. The stored row must still say what it said, or the refusal reported a failure
        // the store had already accepted.
        WorkflowCheckpoint stored = (await store.LoadAsync(Address, default))!.Value;
        WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8, out string? storedEnvironment);
        storedEnvironment.ShouldBe("production");
    }

    [TestMethod]
    public async Task The_first_save_may_not_claim_an_environment_other_than_the_addressed_one()
    {
        // ADR 0065 decision 9: the environment is the run's address, checked structurally on EVERY save. Under the
        // old first-write identity pin a fresh run's first save established whatever environment it claimed; under
        // the composite address there is nothing for the body to establish — the address already says where the run
        // lives, and a first save claiming anywhere else is the same re-pointing attack one save later.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] first = Checkpoint(sequence: 1, environment: "victim-tenant", workflowId: "onboard");
        CheckpointSaveResult result = await SaveAsync(coordinator, first, 1);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        (await store.LoadAsync(Address, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_save_may_not_change_the_workflow_the_run_is_of()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");
        await SaveAsync(coordinator, established, 1);

        byte[] rewritten = Checkpoint(sequence: 2, environment: "production", workflowId: "payroll");
        CheckpointSaveResult result = await SaveAsync(coordinator, rewritten, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Utf8).WorkflowId.ShouldBe("onboard");
    }

    [TestMethod]
    public async Task A_save_may_not_retag_the_run()
    {
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] established = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard", securityTags: Tags("sys:tenant", "acme"));
        await SaveAsync(coordinator, established, 1);

        byte[] retagged = Checkpoint(sequence: 2, environment: "production", workflowId: "onboard", securityTags: Tags("sys:tenant", "globex"));
        CheckpointSaveResult result = await SaveAsync(coordinator, retagged, 2);

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
        await SaveAsync(coordinator, first, 1);

        byte[] advanced = Checkpoint(sequence: 2, environment: "production", workflowId: "onboard", status: WorkflowRunStatus.Completed, cursor: 3);
        CheckpointSaveResult result = await SaveAsync(coordinator, advanced, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Utf8).Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task The_first_save_establishes_the_workflow_and_tags_it_carries()
    {
        // A fresh run's first checkpoint is what sets its workflow id and security tags — there is nothing to compare
        // them against yet. Its environment it does NOT set: that is the address (decision 9), and the first save is
        // applied because its claim MATCHES the addressed environment, never because it established it.
        var store = new InMemoryWorkflowStateStore();
        var coordinator = new WorkflowCheckpointCoordinator(store);

        byte[] first = Checkpoint(sequence: 1, environment: "production", workflowId: "onboard");

        (await SaveAsync(coordinator, first, 1)).Outcome.ShouldBe(CheckpointSaveOutcome.Applied);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Utf8, out string? storedEnvironment).WorkflowId.ShouldBe("onboard");
        storedEnvironment.ShouldBe("production");
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
        await store.SaveAsync(Address, established, Project(established), WorkflowEtag.None, default);

        (await coordinator.LoadAsync(Address, default)).ShouldNotBeNull();

        byte[] moved = Checkpoint(sequence: 2, environment: "victim-tenant", workflowId: "onboard");
        CheckpointSaveResult result = await SaveAsync(coordinator, moved, 2);

        result.Outcome.ShouldBe(CheckpointSaveOutcome.Rejected);
        WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(Address, default))!.Value.Utf8, out string? storedEnvironment);
        storedEnvironment.ShouldBe("production");
    }

    // One projection serves the index and the body's environment claim, exactly as the handlers make it.
    private static ValueTask<CheckpointSaveResult> SaveAsync(WorkflowCheckpointCoordinator coordinator, byte[] checkpoint, long sequence)
    {
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(checkpoint, out string? claimedEnvironment);
        return coordinator.SaveAsync(Address, checkpoint, index, claimedEnvironment, sequence, default);
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