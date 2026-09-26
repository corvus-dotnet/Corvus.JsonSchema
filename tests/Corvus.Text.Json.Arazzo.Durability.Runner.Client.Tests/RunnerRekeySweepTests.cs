// <copyright file="RunnerRekeySweepTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The re-key sweep (ADR 0065 decision 12): resting runs sealed under an older generation are carried to the one the
/// runner writes under, a blinded wait is re-parked under the new index, a held run is left alone, and nothing about
/// a run's progress changes.
/// </summary>
[TestClass]
public sealed class RunnerRekeySweepTests
{
    private const string Older = "k2";
    private const string Newer = "k3";
    private const string Channel = "kyc.verdict";
    private const string Parked = "0b0000000000000000000000000000a1";
    private const string Timed = "0b0000000000000000000000000000a2";
    private const string Held = "0b0000000000000000000000000000a3";
    private const string Finished = "0b0000000000000000000000000000a4";
    private static readonly byte[] OlderKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 6)).ToArray();
    private static readonly byte[] NewerKey = Enumerable.Range(0, 32).Select(i => (byte)(90 - i)).ToArray();

    [TestMethod]
    public async Task The_sweep_re_seals_resting_runs_under_the_write_generation_and_re_parks_a_blinded_wait()
    {
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        RunnerKeyRing ring = Ring();
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: ring,
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([Older, Newer]) });
        var worker = new RunnerApiWorker(fixture.Client);

        // Four runs written under the OLDER generation: one parked on a message, one on a timer, one whose lease a
        // peer holds, one finished.
        ring.SelectWriteGeneration(Fixture.Production, Older);
        foreach (string runId in new[] { Parked, Timed, Held, Finished })
        {
            await fixture.SeedGenesisWaitingAsync(runId, WorkflowWait.Timer(Fixture.T0));
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        (await worker.ResumeDueTimersAsync([Fixture.Version], Advance, default)).ShouldBe(4);
        var olderBlinder = new WaitIndexBlinder(Fixture.Production, Older, OlderKey);
        foreach (string runId in new[] { Parked, Timed, Held, Finished })
        {
            CheckpointIntegrity.KeyIdOf((await fixture.Store.LoadAsync(Address(runId), default))!.Value.Row.Span).ShouldBe(Older);
        }

        WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Parked), default))!.Value.Row).AwaitingChannel.ShouldBe(olderBlinder.Blind(Channel, "acct-42"));
        DateTimeOffset? dueBefore = WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Timed), default))!.Value.Row).DueAt;
        dueBefore.ShouldNotBeNull();
        (await fixture.Store.AcquireLeaseAsync(Address(Held), "someone-else", TimeSpan.FromMinutes(10), default)).ShouldNotBeNull();

        // The runner has moved to the NEWER generation. One pass carries the parked and timed runs across, leaves the
        // held one for later and the finished one alone.
        ring.SelectWriteGeneration(Fixture.Production, Newer);
        var sweep = new RunnerRekeySweep(fixture.Client);
        int resealed = await sweep.SweepAsync(default);
        resealed.ShouldBe(2);

        var newerBlinder = new WaitIndexBlinder(Fixture.Production, Newer, NewerKey);
        WorkflowCheckpoint parked = (await fixture.Store.LoadAsync(Address(Parked), default))!.Value;
        CheckpointIntegrity.KeyIdOf(parked.Row.Span).ShouldBe(Newer);
        WorkflowCheckpointSerializer.ProjectIndex(parked.Row).AwaitingChannel.ShouldBe(newerBlinder.Blind(Channel, "acct-42"), "re-parked under the new index");
        WorkflowCheckpointSerializer.ProjectIndex(parked.Row).Status.ShouldBe(WorkflowRunStatus.Suspended);
        WorkflowCheckpoint timed = (await fixture.Store.LoadAsync(Address(Timed), default))!.Value;
        CheckpointIntegrity.KeyIdOf(timed.Row.Span).ShouldBe(Newer);
        WorkflowCheckpointSerializer.ProjectIndex(timed.Row).DueAt.ShouldBe(dueBefore, "a timer keeps its due time");
        CheckpointIntegrity.KeyIdOf((await fixture.Store.LoadAsync(Address(Held), default))!.Value.Row.Span).ShouldBe(Older, "a held run is never preempted");
        CheckpointIntegrity.KeyIdOf((await fixture.Store.LoadAsync(Address(Finished), default))!.Value.Row.Span).ShouldBe(Older, "a finished run is never re-opened");
        WorkflowLease? free = await fixture.Store.AcquireLeaseAsync(Address(Parked), "someone-else", TimeSpan.FromMinutes(1), default);
        free.ShouldNotBeNull("the sweep gave the lease back");
        await fixture.Store.ReleaseLeaseAsync(free!.Value, default);

        // The re-parked run still wakes on its message, now under the new generation's index alone, and a second
        // pass finds nothing left to do.
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"verdict":"approved"}"""u8.ToArray());
        (await worker.DeliverMessageAsync(Channel, "acct-42", payload.RootElement, [Fixture.Version], Complete, default)).ShouldBe(1);
        WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Parked), default))!.Value.Row).Status.ShouldBe(WorkflowRunStatus.Completed);
        (await sweep.SweepAsync(default)).ShouldBe(0);

        static async ValueTask<WorkflowRunResultKind> Advance(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            switch (run.Id.Value)
            {
                case Parked:
                    await run.SuspendForMessageAsync(run.Cursor, Channel, "acct-42", cancellationToken);
                    return WorkflowRunResultKind.Suspended;
                case Finished:
                    await run.CompleteAsync(default, cancellationToken);
                    return WorkflowRunResultKind.Completed;
                default:
                    await run.SuspendForTimerAsync(run.Cursor, TimeSpan.FromHours(1), cancellationToken);
                    return WorkflowRunResultKind.Suspended;
            }
        }

        static async ValueTask<WorkflowRunResultKind> Complete(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CompleteAsync(default, cancellationToken);
            return WorkflowRunResultKind.Completed;
        }
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));

    // Two generations held, oldest first, no pin: the fixture's control plane advertises nothing to check, so the
    // ring's own selection decides what is written under.
    private static RunnerKeyRing Ring()
    {
        byte[] olderMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(OlderKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, Older, olderMac);
        byte[] newerMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(NewerKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, Newer, newerMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = RunnerEnvironmentKeys.Holding([new RunnerGenerationKeys(Older, OlderKey, olderMac), new RunnerGenerationKeys(Newer, NewerKey, newerMac)], @sealed: true),
        });
    }
}