// <copyright file="RunnerQuotaHoldTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The runner side of ADR 0065 decision 3's bounded exemption, proved against a real server that really refuses. A
/// quota refusal is the one non-2xx a runner waits out rather than failing the advance for, and the exemption is
/// bounded because an unbounded one would be a silent stall primitive.
/// </summary>
/// <remarks>
/// The waits are configured down to milliseconds so the bounds can be asserted without the tests taking as long as a
/// production hold would. What is under test is the bounding, not the duration.
/// </remarks>
[TestClass]
public sealed class RunnerQuotaHoldTests
{
    private static RunnerQuotaHoldOptions FastHold(int attempts = 4, int maxHoldMs = 500) => new()
    {
        MaximumAttempts = attempts,
        MaximumHold = TimeSpan.FromMilliseconds(maxHoldMs),
        MaximumSingleWait = TimeSpan.FromMilliseconds(20),
        DefaultWait = TimeSpan.FromMilliseconds(5),
    };

    [TestMethod]
    public async Task A_refusal_is_waited_out_and_the_advance_continues()
    {
        // The whole reason the exemption exists: a chatty but legitimate workflow must not be faulted mid-advance after
        // its external calls have already landed. Two tokens per second refills within the hold's allowance, so the
        // save that is refused once still lands.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync(
            quotas: o =>
            {
                o.RunnerCheckpoints = new RunnerQuotaLimit(200, 1);
                o.TenantCheckpoints = RunnerQuotaLimit.None;
            },
            hold: FastHold());

        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claim = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        // The claim itself spends nothing on the checkpoint counter, so the first load takes the single burst token and
        // the save that follows is refused until the bucket refills.
        await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default);

        byte[] next = RunnerApiFixture.Checkpoint(claim.RunId.Value, WorkflowRunStatus.Running, sequence: 2);
        WorkflowEtag etag = await fixture.Client.Checkpoints.SaveAsync(
            claim.RunId,
            next,
            WorkflowCheckpointSerializer.ProjectIndex(next),
            WorkflowEtag.None,
            default);

        etag.ShouldNotBe(WorkflowEtag.None);
    }

    [TestMethod]
    public async Task A_persistent_refusal_fails_the_advance_rather_than_holding_forever()
    {
        // The bound. A fabricated refusal is indistinguishable from a real one, the background renewer keeps the lease
        // alive so the run never fails over, and a quota hold raises no audit event — so an unbounded hold would let a
        // control plane stall a run silently while it holds unsaved side effects. This is the loud ending.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync(
            quotas: o =>
            {
                // A rate low enough that nothing refills within the hold's whole allowance.
                o.RunnerCheckpoints = new RunnerQuotaLimit(0.001, 1);
                o.TenantCheckpoints = RunnerQuotaLimit.None;
            },
            hold: FastHold());

        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claim = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default);

        byte[] next = RunnerApiFixture.Checkpoint(claim.RunId.Value, WorkflowRunStatus.Running, sequence: 2);
        RunnerQuotaExhaustedException failed = await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.SaveAsync(
                claim.RunId,
                next,
                WorkflowCheckpointSerializer.ProjectIndex(next),
                WorkflowEtag.None,
                default));

        // It names what it was refused by, so an operator reading the fault knows which limit to look at.
        failed.Quota.ShouldBe("checkpoint-rate/runner");
        failed.Counter.ShouldBe(RunnerApiFixture.Runner);
    }

    [TestMethod]
    public async Task The_allowance_is_spent_across_the_advance_not_per_request()
    {
        // A per-request allowance would let a server multiply the stall by however many operations an advance makes.
        // With a single attempt permitted for the whole advance, the load spends it and the save has none left.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync(
            quotas: o =>
            {
                o.RunnerCheckpoints = new RunnerQuotaLimit(0.001, 1);
                o.TenantCheckpoints = RunnerQuotaLimit.None;
            },
            hold: FastHold(attempts: 1));

        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claim = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        // The first load takes the only token. The second is refused, held once, refused again, and fails.
        await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default);
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default));

        // That single attempt belonged to the advance, so a save now has none left and fails without holding at all.
        byte[] next = RunnerApiFixture.Checkpoint(claim.RunId.Value, WorkflowRunStatus.Running, sequence: 2);
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.SaveAsync(
                claim.RunId,
                next,
                WorkflowCheckpointSerializer.ProjectIndex(next),
                WorkflowEtag.None,
                default));
    }

    [TestMethod]
    public async Task A_new_claim_starts_a_fresh_allowance()
    {
        // The budget is the advance's, and the lease marks the advance. A runner that exhausted its allowance on one run
        // must not be crippled on the next, or one overloaded moment would take the runner out of service entirely.
        //
        // Whether the allowance reset is not visible in the outcome: with this refill rate both advances fail either
        // way. What distinguishes them is whether the second one WAITED before failing, so that is what is measured. The
        // single wait is set long enough that the margin below is a lower bound on a deliberate delay rather than a race
        // with the scheduler.
        var hold = new RunnerQuotaHoldOptions
        {
            MaximumAttempts = 1,
            MaximumHold = TimeSpan.FromSeconds(5),
            MaximumSingleWait = TimeSpan.FromMilliseconds(400),
            DefaultWait = TimeSpan.FromMilliseconds(400),
        };

        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync(
            quotas: o =>
            {
                o.RunnerCheckpoints = new RunnerQuotaLimit(0.001, 1);
                o.TenantCheckpoints = RunnerQuotaLimit.None;
            },
            hold: hold);

        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);

        RunnerClaim first = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        // The one burst token goes to this load; everything after it is refused.
        await fixture.Client.Checkpoints.LoadAsync(first.RunId, default);
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.LoadAsync(first.RunId, default));

        // A third attempt on the SAME advance has no allowance left, so it fails without waiting at all.
        long beforeSpent = Stopwatch.GetTimestamp();
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.LoadAsync(first.RunId, default));
        TimeSpan spent = Stopwatch.GetElapsedTime(beforeSpent);

        // Releasing ends the advance, and with it the allowance it had spent.
        await fixture.Client.ReleaseAsync(first.RunId);

        // The released run becomes claimable again and comes back, which makes this the stronger case: the allowance is
        // keyed by the same run, so a fresh hold proves the budget was genuinely reset rather than that a different key
        // happened to have its own.
        RunnerClaim second = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        long beforeFresh = Stopwatch.GetTimestamp();
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.LoadAsync(second.RunId, default));
        TimeSpan fresh = Stopwatch.GetElapsedTime(beforeFresh);

        // The new advance held once before failing; the exhausted one did not hold at all.
        fresh.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(200));
        spent.ShouldBeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [TestMethod]
    public async Task A_quota_refusal_is_not_a_lost_lease()
    {
        // The two refusals mean opposite things: a lost lease means stop, a quota means wait. Confusing them would have
        // a runner abandon a run it still holds every time the deployment got busy.
        await using RunnerApiFixture fixture = await RunnerApiFixture.StartAsync(
            quotas: o =>
            {
                o.RunnerCheckpoints = new RunnerQuotaLimit(0.001, 1);
                o.TenantCheckpoints = RunnerQuotaLimit.None;
            },
            hold: FastHold(attempts: 1));

        await fixture.SeedAsync("run-1", WorkflowRunStatus.Pending);
        RunnerClaim claim = (await fixture.Client.TryClaimAsync([RunnerApiFixture.Version])).ShouldNotBeNull();

        await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default);
        await Should.ThrowAsync<RunnerQuotaExhaustedException>(
            async () => await fixture.Client.Checkpoints.LoadAsync(claim.RunId, default));

        // The lease was never forgotten, so a renewal still works: the run is still this runner's.
        DateTimeOffset renewed = await fixture.Client.RenewAsync(claim.RunId);
        renewed.ShouldBeGreaterThan(RunnerApiFixture.T0);
    }
}