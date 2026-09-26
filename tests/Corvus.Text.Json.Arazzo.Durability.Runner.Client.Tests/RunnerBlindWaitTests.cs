// <copyright file="RunnerBlindWaitTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// Blind wait indexes over the real runner API (ADR 0065 decision 4): a sealed environment's run parks on the blind
/// index of its channel and correlation id, the store and the control plane hold neither in the clear, and only the
/// runner that holds the key can name the message that wakes it.
/// </summary>
[TestClass]
public sealed class RunnerBlindWaitTests
{
    private const string Correlated = "0123456789abcdef0123456789abcdef";
    private const string ChannelOnly = "fedcba9876543210fedcba9876543210";
    private const string KeyId = "k2";
    private const string Channel = "kyc.verdict";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 5)).ToArray();

    [TestMethod]
    public async Task A_sealed_runs_wait_is_blinded_and_only_the_matching_delivery_wakes_it()
    {
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedGenesisWaitingAsync(Correlated, WorkflowWait.Timer(Fixture.T0));
        await fixture.SeedGenesisWaitingAsync(ChannelOnly, WorkflowWait.Timer(Fixture.T0));
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"verdict":"approved"}"""u8.ToArray());
        var worker = new RunnerApiWorker(fixture.Client);

        // The first advance, by a due timer, parks one run on (kyc.verdict, acct-42) and the other on kyc.verdict alone.
        (await worker.ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(2);

        // Neither the channel nor the account id reaches the store: the rows carry the blind indexes, in the channel
        // column with nothing in the correlation column, as the runner's own blinder computes them.
        var blinder = new WaitIndexBlinder(Fixture.Production, KeyId, PayloadKey);
        WorkflowRunIndexEntry correlated = WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Correlated), default))!.Value.Row);
        correlated.AwaitingChannel.ShouldBe(blinder.Blind(Channel, "acct-42"));
        correlated.AwaitingCorrelationId.ShouldBeNull();
        WorkflowRunIndexEntry channelOnly = WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(ChannelOnly), default))!.Value.Row);
        channelOnly.AwaitingChannel.ShouldBe(blinder.BlindChannelOnly(Channel));
        foreach (string runId in new[] { Correlated, ChannelOnly })
        {
            WorkflowCheckpoint stored = (await fixture.Store.LoadAsync(Address(runId), default))!.Value;
            string envelope = RunnerRegionText(stored.Row);
            envelope.ShouldNotContain(Channel);
            envelope.ShouldNotContain("acct-42");
        }

        // A message for another account wakes nobody correlated, and a message with no correlation id wakes only the
        // channel-only waiter (the decision-4 residue: it no longer broadcasts to every correlated waiter).
        (await worker.DeliverMessageAsync(Channel, "acct-99", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(1, "the channel-only waiter wakes on any correlated delivery");
        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(1, "and on a delivery with no correlation");
        WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Correlated), default))!.Value.Row).Status.ShouldBe(WorkflowRunStatus.Suspended, "the correlated waiter is still waiting");

        // The matching delivery wakes the correlated waiter, and the channel-only waiter beside it.
        (await worker.DeliverMessageAsync(Channel, "acct-42", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(2);

        // A peer holding no key sweeps by channel and finds nothing in the sealed environment.
        (await new RunnerApiWorker(fixture.PeerClient).DeliverMessageAsync(Channel, "acct-42", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(0);

        static async ValueTask<WorkflowRunResultKind> Park(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            await run.SuspendForMessageAsync(run.Cursor, Channel, run.Id.Value == Correlated ? "acct-42" : null, cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }
    }

    [TestMethod]
    public async Task A_run_parked_under_an_older_generation_still_wakes_on_delivery_after_the_runner_moved_to_a_newer_one()
    {
        // ADR 0065 decision 12: the run parked its wait under k2; the runner now writes under k3 but still holds k2,
        // so a delivery queries the index under both generations, wakes the run, and its next wait is parked under k3.
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        byte[] newerPayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(150 - i)).ToArray();
        byte[] olderMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, olderMac);
        byte[] newerMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(newerPayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, "k3", newerMac);
        RunnerKeyRing ring = RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = RunnerEnvironmentKeys.Holding([new RunnerGenerationKeys(KeyId, PayloadKey, olderMac), new RunnerGenerationKeys("k3", newerPayloadKey, newerMac)], @sealed: true),
        });
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: ring,
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId, "k3"]) });
        await fixture.SeedGenesisWaitingAsync(Correlated, WorkflowWait.Timer(Fixture.T0));
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"verdict":"approved"}"""u8.ToArray());
        var worker = new RunnerApiWorker(fixture.Client);

        // Parked under k2, the generation the runner wrote under at the time.
        ring.SelectWriteGeneration(Fixture.Production, KeyId);
        (await worker.ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(1);
        var older = new WaitIndexBlinder(Fixture.Production, KeyId, PayloadKey);
        WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Correlated), default))!.Value.Row).AwaitingChannel.ShouldBe(older.Blind(Channel, "acct-42"));

        // The runner has moved to k3. The delivery still finds the run under k2's index, and the run parks again
        // under k3's.
        ring.SelectWriteGeneration(Fixture.Production, "k3");
        (await worker.DeliverMessageAsync(Channel, "acct-42", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(1);
        var newer = new WaitIndexBlinder(Fixture.Production, "k3", newerPayloadKey);
        WorkflowCheckpoint moved = (await fixture.Store.LoadAsync(Address(Correlated), default))!.Value;
        CheckpointIntegrity.KeyIdOf(moved.Row.Span).ShouldBe("k3");
        WorkflowCheckpointSerializer.ProjectIndex(moved.Row).AwaitingChannel.ShouldBe(newer.Blind(Channel, "acct-42"));
        (await worker.DeliverMessageAsync(Channel, "acct-42", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(1, "and wakes under k3 too");

        static async ValueTask<WorkflowRunResultKind> Park(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            await run.SuspendForMessageAsync(run.Cursor, Channel, "acct-42", cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }
    }

    [TestMethod]
    public async Task A_run_offered_under_a_wait_the_message_was_not_claimed_by_is_handed_back_without_it()
    {
        // The control plane answers the index query; a run it offers whose own region says it parked on a different
        // index is not given the message (decision 4's re-check).
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedGenesisWaitingAsync(Correlated, WorkflowWait.Timer(Fixture.T0));
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"verdict":"approved"}"""u8.ToArray());
        var worker = new RunnerApiWorker(fixture.Client);
        (await worker.ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(1);

        // The control plane relabels the index column so the row answers a query for another account's message. The
        // MAC'd region still says acct-42.
        var blinder = new WaitIndexBlinder(Fixture.Production, KeyId, PayloadKey);
        WorkflowCheckpoint parked = (await fixture.Store.LoadAsync(Address(Correlated), default))!.Value;
        WorkflowRunIndexEntry relabelled = WorkflowCheckpointSerializer.ProjectIndex(parked.Row) with { AwaitingChannel = blinder.Blind(Channel, "acct-99") };
        await fixture.Store.SaveAsync(Address(Correlated), parked.Row, relabelled, parked.Etag, default);

        (await worker.DeliverMessageAsync(Channel, "acct-99", payload.RootElement, [Fixture.Version], Park, default)).ShouldBe(0, "offered, checked, handed back");
        WorkflowCheckpointSerializer.ProjectIndex((await fixture.Store.LoadAsync(Address(Correlated), default))!.Value.Row).Status.ShouldBe(WorkflowRunStatus.Suspended);
        (await fixture.Store.AcquireLeaseAsync(Address(Correlated), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");

        static async ValueTask<WorkflowRunResultKind> Park(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.SuspendForMessageAsync(run.Cursor + 1, Channel, "acct-42", cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));

    private static string RunnerRegionText(ReadOnlyMemory<byte> row)
        => System.Text.Encoding.UTF8.GetString(row.Span[CheckpointRow.Parse(row.Span).RunnerRegion]);

    private static RunnerKeyRing Ring()
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, Sealed: true),
        });
    }
}