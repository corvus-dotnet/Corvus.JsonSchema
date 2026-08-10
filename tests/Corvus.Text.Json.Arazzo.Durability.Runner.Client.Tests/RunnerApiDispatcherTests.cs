// <copyright file="RunnerApiDispatcherTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The dispatch cycle a runner without store credentials actually runs (ADR 0065). The store is present in these tests
/// only so the API has something real to terminate into. The dispatcher never sees it.
/// </summary>
[TestClass]
public sealed class RunnerApiDispatcherTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private const string Run3 = "00112233445566778899aabbccddeeff";
    private const string Version = Fixture.Version;

    [TestMethod]
    public async Task Every_claimable_run_is_dispatched_and_the_executor_sees_the_real_checkpoint()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        await fixture.SeedAsync(Run2, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        List<WorkflowRunId> seen = [];
        int dispatched = await dispatcher.DispatchClaimableAsync([Version], async (run, ct) =>
        {
            seen.Add(run.Id);

            // The run arrived resumed from the checkpoint the API served, not from an empty shell.
            run.WorkflowId.ShouldBe(Version);
            return await SuspendAsync(fixture, run, ct);
        }, default);

        dispatched.ShouldBe(2);
        seen.ShouldBe([new WorkflowRunId(Run1), new WorkflowRunId(Run2)], ignoreOrder: true);
    }

    [TestMethod]
    public async Task An_idle_runner_dispatches_nothing()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        int dispatched = await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("nothing was claimable"), default);

        dispatched.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_runner_that_hosts_none_of_the_claimable_versions_is_offered_nothing()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        int dispatched = await dispatcher.DispatchClaimableAsync(["some-other-workflow-v1"], (_, _) => throw new InvalidOperationException("this runner cannot execute that version"), default);

        dispatched.ShouldBe(0);

        // And the run is untouched, so a runner that can execute it still gets it.
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_pass_stops_at_the_maximum_rather_than_draining_the_queue()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        await fixture.SeedAsync(Run2, WorkflowRunStatus.Pending);
        await fixture.SeedAsync(Run3, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client) { MaximumRunsPerPass = 2 };

        int dispatched = await dispatcher.DispatchClaimableAsync([Version], (run, ct) => SuspendAsync(fixture, run, ct), default);

        dispatched.ShouldBe(2);

        // What it did not take is still claimable, not stranded mid-claim.
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_run_that_comes_back_claimable_without_advancing_ends_the_pass()
    {
        // An executor that yields without checkpointing leaves the run exactly as it found it, so releasing makes it
        // claimable again at once. Claiming one run at a time, the pass would otherwise take the same run over and
        // over up to its bound, doing no work and starving every other run in the queue.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        int advances = 0;
        int dispatched = await dispatcher.DispatchClaimableAsync([Version], (_, _) =>
        {
            advances++;
            return ValueTask.FromResult(WorkflowRunResultKind.Suspended);
        }, default);

        advances.ShouldBe(1);
        dispatched.ShouldBe(1);

        // The pass ended without holding on to it, so the run is still there for the next pass or another runner.
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Every_dispatched_run_is_released_so_a_peer_can_take_it_next()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        await dispatcher.DispatchClaimableAsync([Version], (_, _) => ValueTask.FromResult(WorkflowRunResultKind.Suspended), default);

        // No clock advance: the lease was given up, not waited out.
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_run_whose_executor_throws_is_released_rather_than_left_leased()
    {
        // The reason release belongs in a finally. A runner that crashed mid-advance and held its lease would strand
        // the run for the whole lease duration, and this is the failure most likely to happen in production.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        // It propagates, exactly as the store-backed dispatcher does: an executor fault is the runner's to handle.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("boom"), default));

        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_run_whose_lease_lapsed_mid_advance_is_not_counted_as_dispatched()
    {
        // A lease lost to a lapse is not an error the runner can act on: the run belongs to whoever holds it now, and
        // this runner's writes were refused rather than applied. It moves on to the next claim.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);

        int dispatched = await dispatcher.DispatchClaimableAsync([Version], async (run, ct) =>
        {
            // The advance overruns the lease, and a peer takes the run before this one writes.
            fixture.Clock.Advance(TimeSpan.FromMinutes(2));
            (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();

            byte[] advanced = Fixture.Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2);
            await fixture.Client.Checkpoints.SaveAsync(run.Id, advanced, WorkflowCheckpointSerializer.ProjectIndex(advanced), WorkflowEtag.None, ct);
            return WorkflowRunResultKind.Suspended;
        }, default);

        dispatched.ShouldBe(0);

        // The peer's claim stands: releasing in the finally must not have taken the run off it.
        WorkflowCheckpoint? stored = await fixture.Store.LoadAsync(new WorkflowRunId(Run1), default);
        WorkflowCheckpointSerializer.TryReadSequence(stored!.Value.Utf8, out long sequence).ShouldBeTrue();
        sequence.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_cancelled_pass_stops_claiming()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        await fixture.SeedAsync(Run2, WorkflowRunStatus.Pending);
        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        using var cancellation = new CancellationTokenSource();

        int dispatched = await dispatcher.DispatchClaimableAsync([Version], async (run, ct) =>
        {
            WorkflowRunResultKind kind = await SuspendAsync(fixture, run, ct);
            await cancellation.CancelAsync();
            return kind;
        }, cancellation.Token);

        dispatched.ShouldBe(1);

        // Cancellation is a shutdown, and a shutdown gives its lease back rather than stranding the run for the TTL.
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    // What a real executor does on the way out: checkpoint, which moves the run out of the claimable set. The
    // dispatcher's behaviour differs sharply depending on whether an advance actually happened, so a test that
    // skipped this would be testing the degenerate path while claiming to test the normal one.
    private static async ValueTask<WorkflowRunResultKind> SuspendAsync(Fixture fixture, WorkflowRun run, CancellationToken cancellationToken)
    {
        byte[] advanced = Fixture.Checkpoint(run.Id.Value, WorkflowRunStatus.Suspended, sequence: 2);
        await fixture.Client.Checkpoints.SaveAsync(run.Id, advanced, WorkflowCheckpointSerializer.ProjectIndex(advanced), WorkflowEtag.None, cancellationToken);
        return WorkflowRunResultKind.Suspended;
    }
}