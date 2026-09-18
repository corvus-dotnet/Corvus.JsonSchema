// <copyright file="RunnerClientRoundTripTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The generated client driving the generated server over real HTTP, terminating into a real store. Both sides come
/// from one contract, so this is what proves they agree — and it is the shape a runner actually runs in: claim, resume
/// the run through the client's checkpoint store, advance it, release.
/// </summary>
[TestClass]
public sealed class RunnerClientRoundTripTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string AbsentRun = "badcafebadcafebadcafebadcafe0000";
    private const string Production = Fixture.Production;
    private const string Version = Fixture.Version;

    private static readonly DateTimeOffset T0 = Fixture.T0;

    [TestMethod]
    public async Task A_runner_claims_a_run_and_reads_its_checkpoint_through_the_client()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);

        RunnerClaim? claimed = await fixture.Client.TryClaimAsync([Version]);

        claimed.ShouldNotBeNull();
        claimed!.Value.RunId.ShouldBe(new WorkflowRunId(Run1));
        claimed.Value.WorkflowId.ShouldBe(Version);
        claimed.Value.Environment.ShouldBe(Production);
        claimed.Value.LeaseEpoch.ShouldBeGreaterThan(0);
        claimed.Value.LeaseExpiresAt.ShouldBe(T0 + TimeSpan.FromMinutes(1));

        // The run resumes through the client's checkpoint store exactly as it would over a database-backed one.
        WorkflowCheckpoint? loaded = await fixture.Client.Checkpoints.LoadAsync(claimed.Value.Address, default);
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
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        byte[] advanced = Fixture.Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2);
        await fixture.Client.Checkpoints.SaveAsync(claimed.Address, advanced, WorkflowCheckpointSerializer.ProjectIndex(advanced), WorkflowEtag.None, default);

        // Read back from the STORE, not the API: the point is that the write reached the real thing.
        WorkflowCheckpoint? stored = await fixture.Store.LoadAsync(claimed.Address, default);
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
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        byte[] advanced = Fixture.Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2);
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(advanced);
        await fixture.Client.Checkpoints.SaveAsync(claimed.Address, advanced, index, WorkflowEtag.None, default);

        CheckpointSupersededException refused = await Should.ThrowAsync<CheckpointSupersededException>(
            async () => await fixture.Client.Checkpoints.SaveAsync(claimed.Address, advanced, index, WorkflowEtag.None, default));

        refused.ProposedSequence.ShouldBe(2);
        refused.AcceptedSequence.ShouldBe(3);
    }

    [TestMethod]
    public async Task A_run_that_honours_its_budget_faults_itself_over_the_wire_and_is_never_refused()
    {
        // ADR 0068, the cooperative half over the real API: a run driven through the runner's checkpoint store spends
        // its one unit of fuel, then faults itself before the next attempt. The control plane applies that save (the
        // journal is inside the fuel), so the runner sees its own budget signal and never the 409.
        await using Fixture fixture = await Fixture.StartAsync();
        var oneStep = new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero);
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending, budget: oneStep);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        // The run reads the fixture's clock, so its age is judged against the seeded creation time and only fuel is in play.
        using WorkflowRun run = (await WorkflowRun.ResumeAsync(fixture.Client.Checkpoints, claimed.Address, new RunnerApiFixture.TestClock(Fixture.T0)))!;
        await run.BeginStepAsync("only", default);
        run.RecordStep("only", WorkflowStepStatus.Succeeded, 1, Fixture.T0, Fixture.T0);
        await run.CheckpointAsync(0, default);

        Exception unwound = await Should.ThrowAsync<Exception>(async () => await run.BeginStepAsync("only", default));
        unwound.ShouldNotBeOfType<RunBudgetExhaustedException>();
        unwound.GetType().Name.ShouldBe("WorkflowBudgetExhaustedException");

        WorkflowCheckpoint stored = (await fixture.Store.LoadAsync(claimed.Address, default))!.Value;
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(stored.Utf8);
        state.StepJournal!.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_save_past_the_budget_is_raised_as_exhausted_and_the_run_is_recorded_as_faulted()
    {
        // ADR 0068 over the wire: the control plane refuses the save with the budget-exhausted problem type, the client
        // raises it as its own exception (not as a superseded save the runner would resend), and the store holds the
        // faulted run the control plane authored.
        await using Fixture fixture = await Fixture.StartAsync();
        var oneStep = new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero);
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending, budget: oneStep);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        byte[] overBudget = Fixture.Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2, budget: oneStep, journalEntries: 2);
        RunBudgetExhaustedException refused = await Should.ThrowAsync<RunBudgetExhaustedException>(
            async () => await fixture.Client.Checkpoints.SaveAsync(claimed.Address, overBudget, WorkflowCheckpointSerializer.ProjectIndex(overBudget), WorkflowEtag.None, default));

        refused.RunId.ShouldBe(new WorkflowRunId(Run1));
        refused.Message.ShouldContain(ExecutionBudgetFault.Fuel);

        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(claimed.Address, default))!.Value.Utf8);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);

        // The lease is still the client's to hand back, and the faulted run is offered to nobody afterwards.
        await fixture.Client.ReleaseAsync(claimed.Address, default);
        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_lease_renews_and_keeps_its_epoch()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        DateTimeOffset extended = await fixture.Client.RenewAsync(claimed.Address, TimeSpan.FromMinutes(5));

        extended.ShouldBe(T0 + TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(5));

        // The client threads the same token across the renewal, so the run keeps working.
        (await fixture.Client.Checkpoints.LoadAsync(claimed.Address, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_lapsed_lease_is_raised_as_lost_rather_than_silently_renewed()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        await Should.ThrowAsync<RunnerLeaseLostException>(async () => await fixture.Client.RenewAsync(claimed.Address));

        // Having lost it, the client stops presenting it: the next operation fails without a round trip.
        await Should.ThrowAsync<RunnerLeaseLostException>(async () => await fixture.Client.Checkpoints.LoadAsync(claimed.Address, default));
    }

    [TestMethod]
    public async Task A_released_run_is_claimable_by_a_peer_at_once()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        RunnerClaim claimed = (await fixture.Client.TryClaimAsync([Version]))!.Value;

        await fixture.Client.ReleaseAsync(claimed.Address);

        (await fixture.PeerClient.TryClaimAsync([Version])).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Releasing_a_run_the_client_does_not_hold_does_nothing()
    {
        // So a runner can release in a finally without first working out whether it still holds the lease.
        await using Fixture fixture = await Fixture.StartAsync();

        await Should.NotThrowAsync(async () => await fixture.Client.ReleaseAsync(new WorkflowRunAddress(Production, new WorkflowRunId(AbsentRun))));
    }

    [TestMethod]
    public async Task Operating_on_a_run_this_client_never_claimed_is_refused_without_a_round_trip()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);
        await fixture.Client.TryClaimAsync([Version]);

        // The peer holds no lease for this run, so it has nothing to present and never asks.
        await Should.ThrowAsync<RunnerLeaseLostException>(
            async () => await fixture.PeerClient.Checkpoints.LoadAsync(new WorkflowRunAddress(Production, new WorkflowRunId(Run1)), default));
    }
}