// <copyright file="CheckpointAnchoringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The tenant anchor as the lease holder runs it (ADR 0065 decision 6): the sealing store, given a tenant anchor
/// store, evaluates the decision table on every open and stages every save with the tenant before it is
/// dispatched, so a rollback, a substitution or a replay by the control plane is a fault at the next open rather
/// than an advance, and a plain crash at any point is resolved by the table without one.
/// </summary>
[TestClass]
public sealed class CheckpointAnchoringTests
{
    private const string Production = "production";
    private const string Development = "development";
    private const string RunId = "00000000000000000000000000000a01";
    private const long Epoch = 7;
    private const ulong Incarnation = 1;
    private static readonly DateTimeOffset T0 = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();
    private static readonly WorkflowRunAddress ProductionRun = new(Production, new WorkflowRunId(RunId));

    [TestMethod]
    public async Task The_genesis_row_is_sequence_zero_and_a_run_enqueued_twice_is_refused()
    {
        // The control plane's genesis row is the origin of the series the anchor commits to (decision 6): sequence 0
        // by definition, with no lease epoch, and the runner's first save is 1.
        var inner = new InMemoryWorkflowStateStore();
        await EnqueueGenesisAsync(inner);

        WorkflowCheckpoint genesis = (await inner.LoadAsync(ProductionRun, default))!.Value;
        WorkflowCheckpointSerializer.TryProject(genesis.Row, out CheckpointProjection projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(0);
        projection.Epoch.ShouldBeNull();
        projection.Incarnation.ShouldBeNull();

        using WorkflowRun? run = await WorkflowRun.ResumeAsync(inner, ProductionRun, leaseEpoch: Epoch);
        await run!.CheckpointAsync(1, default);
        WorkflowCheckpointSerializer.TryProject((await inner.LoadAsync(ProductionRun, default))!.Value.Row, out projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(1);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await run.EnqueueAsync(default))).Message.ShouldContain("genesis");
    }

    [TestMethod]
    public async Task The_first_claim_creates_the_record_and_each_save_is_staged_before_it_is_dispatched()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        AnchorDigest genesisDigest = DigestOfStored(inner);

        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            (await anchors.ReadAsync(Production, RunId, default)).ShouldBeNull("the open decides the first claim; the create is written with the first save");

            await run.CheckpointAsync(1, default);
            AnchorRecord afterFirst = (await anchors.ReadAsync(Production, RunId, default))!.Value;
            afterFirst.State.ShouldBe(AnchorState.Live);
            afterFirst.Committed.ShouldBe(new AnchorMark(Key(), 0, genesisDigest), "the create committed to the genesis row");
            afterFirst.Pending.ShouldBe(new AnchorMark(Key(), 1, DigestOfStored(inner)), "the first save is staged at its own digest, over the row exactly as the store holds it");
            afterFirst.EpochHighWater.ShouldBe(Key());
            afterFirst.ReanchorCounter.ShouldBe(0UL);

            // Mid-advance, the acknowledged save stays staged and the next save promotes it in the same write that
            // stages its own mark: one round trip to the tenant per checkpoint.
            await run.CheckpointAsync(2, default);
            AnchorRecord afterSecond = (await anchors.ReadAsync(Production, RunId, default))!.Value;
            afterSecond.Committed.ShouldBe(afterFirst.Pending!.Value);
            afterSecond.Pending.ShouldBe(new AnchorMark(Key(), 2, DigestOfStored(inner)));

            // A save that rests the run is promoted at once, so the committed mark is the last checkpoint before the
            // run waits, and a rollback of it is a rollback rather than a lost acknowledgement.
            await run.SuspendForTimerAsync(2, TimeSpan.FromMinutes(1), default);
            AnchorRecord afterRest = (await anchors.ReadAsync(Production, RunId, default))!.Value;
            afterRest.Committed.ShouldBe(new AnchorMark(Key(), 3, DigestOfStored(inner)));
            afterRest.Pending.ShouldBeNull();
        }

        // The digest is over the submitted bytes and never the control-plane region.
        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        CheckpointDigest.ForSubmitted(CheckpointRow.SubmittedBytes(stored.Row).Span).ShouldBe(DigestOfStored(inner));
        CheckpointDigest.ForSubmitted(stored.Row.Span).ShouldNotBe(DigestOfStored(inner));
    }

    [TestMethod]
    public async Task A_finishing_save_promotes_and_finalizes_and_every_later_claim_is_refused()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.CheckpointAsync(1, default);
            await run.CompleteAsync(default, default);
        }

        AnchorRecord closed = (await anchors.ReadAsync(Production, RunId, default))!.Value;
        closed.State.ShouldBe(AnchorState.Terminal);
        closed.Disposition.ShouldBe(AnchorDisposition.Completed);
        closed.Pending.ShouldBeNull();
        closed.Committed.Sequence.ShouldBe(2UL);

        // Row 1, whatever the store says: the finished row, and the genesis row the control plane kept and re-presents.
        CheckpointAnchorException refused = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default));
        refused.Decision!.Value.Row.ShouldBe(1);
        refused.Decision.Value.Outcome.ShouldBe(AnchorOpenOutcome.RefuseClaim);
        refused.ReAnchorable.ShouldBeFalse();

        var replay = new InMemoryWorkflowStateStore();
        await EnqueueGenesisAsync(replay);
        WorkflowCheckpoint genesis = (await replay.LoadAsync(ProductionRun, default))!.Value;
        WorkflowCheckpoint current = (await inner.LoadAsync(ProductionRun, default))!.Value;
        await inner.SaveAsync(ProductionRun, genesis.Row, WorkflowCheckpointSerializer.ProjectIndex(genesis.Row), current.Etag, default);
        (await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default))).Decision!.Value.Row.ShouldBe(1, "a completed run replayed from its genesis row is refused, not started again");
    }

    [TestMethod]
    public async Task A_save_whose_acknowledgement_was_lost_is_promoted_at_the_next_open_and_one_that_did_not_land_is_discarded()
    {
        // The two ordinary crash shapes around a dispatch (rows 9 and 11). Each leaves the mark staged: the store
        // threw after the row landed, or before it did, and neither is an abandonment.
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, _) = await AnchoredAsync();

        var lostAcknowledgement = new FailingStore(inner, failAfterWrite: true);
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(new SealingCheckpointStore(lostAcknowledgement, Ring(), anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await Should.ThrowAsync<WorkflowConflictException>(async () => await run.CheckpointAsync(1, default));
        }

        AnchorRecord staged = (await anchors.ReadAsync(Production, RunId, default))!.Value;
        staged.Pending!.Value.Sequence.ShouldBe(1UL, "a 409 is not an abandonment: the mark stays staged");
        WorkflowCheckpointSerializer.TryProject((await inner.LoadAsync(ProductionRun, default))!.Value.Row, out CheckpointProjection landed).ShouldBeTrue();
        landed.Sequence.ShouldBe(1, "the row landed");

        using (WorkflowRun resumed = (await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            resumed.Cursor.ShouldBe(1, "row 9: the staged save is what the store holds, so it is promoted and the run proceeds from it");
            AnchorRecord promoted = (await anchors.ReadAsync(Production, RunId, default))!.Value;
            promoted.Committed.ShouldBe(staged.Pending!.Value);
            promoted.Pending.ShouldBeNull();

            var neverLanded = new FailingStore(inner, failAfterWrite: false);
            using WorkflowRun again = (await WorkflowRun.ResumeAsync(new SealingCheckpointStore(neverLanded, Ring(), anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!;
            await Should.ThrowAsync<WorkflowConflictException>(async () => await again.CheckpointAsync(2, default));
        }

        (await anchors.ReadAsync(Production, RunId, default))!.Value.Pending!.Value.Sequence.ShouldBe(2UL, "staged before the dispatch that then failed");
        using (WorkflowRun resumed = (await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            resumed.Cursor.ShouldBe(1, "row 11: the store still holds the committed row, so the staged mark is discarded and the run proceeds from what landed");
            AnchorRecord discarded = (await anchors.ReadAsync(Production, RunId, default))!.Value;
            discarded.Committed.Sequence.ShouldBe(1UL);
            discarded.Pending.ShouldBeNull();
        }
    }

    [TestMethod]
    public async Task A_save_staged_over_an_unacknowledged_one_is_refused_before_it_is_dispatched()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, _) = await AnchoredAsync();
        var failing = new FailingStore(inner, failAfterWrite: false);
        using WorkflowRun run = (await WorkflowRun.ResumeAsync(new SealingCheckpointStore(failing, Ring(), anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!;
        await Should.ThrowAsync<WorkflowConflictException>(async () => await run.CheckpointAsync(1, default));
        failing.Writes.ShouldBe(1);

        // Re-preparing over an outstanding save would replace the record of what is in flight (the spec's writer
        // obligation on Prepare), so the run must re-open instead.
        CheckpointAnchorException refused = await Should.ThrowAsync<CheckpointAnchorException>(async () => await run.CheckpointAsync(2, default));
        refused.Decision.ShouldBeNull();
        refused.Message.ShouldContain("Prepare");
        failing.Writes.ShouldBe(1, "nothing was dispatched");
    }

    [TestMethod]
    public async Task A_writer_holding_a_stale_record_is_refused_before_it_dispatches()
    {
        // Two processes hold the run: the first opened it, then the second opened it and saved. The first's next
        // save decides against a record the tenant no longer holds, so the store's compare-and-swap refuses it and
        // nothing reaches the control plane. The lease is what should have stopped this; the anchor is the backstop.
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore first) = await AnchoredAsync();
        using WorkflowRun stale = (await WorkflowRun.ResumeAsync(first, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!;
        using (WorkflowRun current = (await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch + 1, incarnation: Incarnation))!)
        {
            await current.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        CheckpointAnchorException refused = await Should.ThrowAsync<CheckpointAnchorException>(async () => await stale.CheckpointAsync(1, default));
        refused.Message.ShouldContain("rejected");
        WorkflowCheckpointSerializer.TryProject((await inner.LoadAsync(ProductionRun, default))!.Value.Row, out CheckpointProjection projection).ShouldBeTrue();
        projection.Epoch.ShouldBe(Epoch + 1, "the stale writer's row never reached the store");
        (await anchors.ReadAsync(Production, RunId, default))!.Value.Committed.Sequence.ShouldBe(1UL);
    }

    [TestMethod]
    public async Task A_rollback_by_the_control_plane_is_a_hard_fault_that_is_not_re_anchorable()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        WorkflowCheckpoint older = (await inner.LoadAsync(ProductionRun, default))!.Value;
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(2, TimeSpan.FromMinutes(1), default);
        }

        // The control plane puts the older row back, MAC intact, digest intact: everything the row can say about
        // itself is true. Only the tenant's record says the run had moved on.
        WorkflowCheckpoint current = (await inner.LoadAsync(ProductionRun, default))!.Value;
        await inner.SaveAsync(ProductionRun, older.Row, WorkflowCheckpointSerializer.ProjectIndex(older.Row), current.Etag, default);

        CheckpointAnchorException fault = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default));
        fault.Decision!.Value.Row.ShouldBe(13);
        fault.Decision.Value.Fault.ShouldBe(AnchorFaultKind.Rollback);
        fault.ReAnchorable.ShouldBeFalse("within one incarnation a rollback has no legitimate cause");
        fault.Message.ShouldContain("Rollback");
    }

    [TestMethod]
    public async Task A_different_genuine_row_at_the_committed_sequence_is_a_substitution()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        // The same checkpoint re-encrypted by a party holding the key (a second genuine branch, or the same one
        // re-sealed): the MAC verifies, the payload opens, and the digest differs.
        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        Ring().TryGet(Production, out RunnerEnvironmentKeys keys);
        byte[] resealed = SealingCheckpointStore.Seal(SealingCheckpointStore.Open(stored.Row, ProductionRun, keys), ProductionRun, keys);
        await inner.SaveAsync(ProductionRun, resealed, WorkflowCheckpointSerializer.ProjectIndex(resealed), stored.Etag, default);

        CheckpointAnchorException fault = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default));
        fault.Decision!.Value.Row.ShouldBe(8);
        fault.Decision.Value.Fault.ShouldBe(AnchorFaultKind.Substitution);
        fault.ReAnchorable.ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_control_plane_region_write_leaves_the_digest_alone()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        // The control plane cancels: its own region, joined at read time, outside the submitted bytes (decision 7).
        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        byte[] cancelled = CheckpointRow.WithControlPlaneRegion(stored.Row.Span, new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(T0)).ToUtf8());
        await inner.SaveAsync(ProductionRun, cancelled, WorkflowCheckpointSerializer.ProjectIndex(cancelled), stored.Etag, default);

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation);
        resumed.ShouldNotBeNull("row 7: the runner's bytes are what the tenant committed to");
        resumed!.Status.ShouldBe(WorkflowRunStatus.Cancelled);
    }

    [TestMethod]
    public async Task A_missing_anchor_beyond_genesis_and_an_unattested_environment_both_refuse()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        // Row 4: the tenant lost the record for a run that has advanced. Re-anchorable, since a lost anchor is the
        // primary case a signed re-anchor exists for.
        var lost = new InMemoryTenantAnchorStore();
        await lost.AttestIncarnationAsync(Production, Incarnation, default);
        CheckpointAnchorException fault = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, lost).LoadAsync(ProductionRun, default));
        fault.Decision!.Value.Row.ShouldBe(4);
        fault.Decision.Value.Fault.ShouldBe(AnchorFaultKind.AnchorLost);
        fault.ReAnchorable.ShouldBeTrue();

        // No attestation at all: nothing opens and nothing is staged, whatever the store holds.
        CheckpointAnchorException unattested = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, new InMemoryTenantAnchorStore()).LoadAsync(ProductionRun, default));
        unattested.Decision.ShouldBeNull();
        unattested.Message.ShouldContain("attested");
    }

    [TestMethod]
    public async Task A_row_carrying_an_incarnation_the_tenant_has_not_attested_faults()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        // A save carrying an incarnation other than the attested one is the writer's obligation to refuse, before any
        // dispatch (A5).
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(Fresh(inner, anchors), ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation + 1))!)
        {
            (await Should.ThrowAsync<CheckpointAnchorException>(async () => await run.CheckpointAsync(2, default))).Message.ShouldContain("incarnation");
        }

        // And a stored row whose region carries one is row 5a at the next open: the backstop for a store that
        // could not enforce A5 itself.
        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        Ring().TryGet(Production, out RunnerEnvironmentKeys keys);
        byte[] foreign = SealingCheckpointStore.Seal(Row(ProductionRun, sequence: 1, incarnation: Incarnation + 1), ProductionRun, keys);
        await inner.SaveAsync(ProductionRun, foreign, WorkflowCheckpointSerializer.ProjectIndex(foreign), stored.Etag, default);
        CheckpointAnchorException fault = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default));
        fault.Decision!.Value.Row.ShouldBe(51);
        fault.Decision.Value.Fault.ShouldBe(AnchorFaultKind.UnattestedIncarnation);
        fault.Message.ShouldContain("5a");
    }

    [TestMethod]
    public async Task A_row_whose_mac_does_not_verify_is_the_tables_unreadable_row()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, ProductionRun, leaseEpoch: Epoch, incarnation: Incarnation))!)
        {
            await run.SuspendForTimerAsync(1, TimeSpan.FromMinutes(1), default);
        }

        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        byte[] tampered = [.. stored.Row.Span];
        tampered[CheckpointRow.Parse(tampered).Mac.Start.Value] ^= 0x01;
        await inner.SaveAsync(ProductionRun, tampered, WorkflowCheckpointSerializer.ProjectIndex(tampered), stored.Etag, default);

        CheckpointAnchorException fault = await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default));
        fault.Decision!.Value.Row.ShouldBe(5);
        fault.Decision.Value.Fault.ShouldBe(AnchorFaultKind.Unreadable);
        fault.ReAnchorable.ShouldBeTrue("an older runner meeting a newer framing is a legitimate cause");

        // A clear row beyond genesis is unreadable too: it carries no MAC and is not the row the control plane wrote
        // before any claim.
        byte[] clear = Row(ProductionRun, sequence: 1, epoch: null);
        await inner.SaveAsync(ProductionRun, clear, WorkflowCheckpointSerializer.ProjectIndex(clear), (await inner.LoadAsync(ProductionRun, default))!.Value.Etag, default);
        (await Should.ThrowAsync<CheckpointAnchorException>(async () => await Fresh(inner, anchors).LoadAsync(ProductionRun, default))).Decision!.Value.Row.ShouldBe(5);
    }

    [TestMethod]
    public async Task One_save_is_in_flight_per_run()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, _) = await AnchoredAsync();
        var gated = new GatedStore(inner);
        var store = new SealingCheckpointStore(gated, Ring(), anchors);
        (await store.LoadAsync(ProductionRun, default)).ShouldNotBeNull();
        byte[] first = Row(ProductionRun, sequence: 1);
        byte[] second = Row(ProductionRun, sequence: 2);

        Task<WorkflowEtag> saveFirst = store.SaveAsync(ProductionRun, first, WorkflowCheckpointSerializer.ProjectIndex(first), WorkflowEtag.None, default).AsTask();
        if (await Task.WhenAny(gated.Entered.Task, saveFirst) == saveFirst)
        {
            // The save ended before it reached the store: surface why rather than wait for an entry that is not coming.
            await saveFirst;
            Assert.Fail("the first save never reached the store");
        }
        Task<WorkflowEtag> saveSecond = store.SaveAsync(ProductionRun, second, WorkflowCheckpointSerializer.ProjectIndex(second), WorkflowEtag.None, default).AsTask();
        await Task.Delay(50);
        gated.Writes.ShouldBe(1, "the second save waits for the first's acknowledgement rather than racing it");
        (await anchors.ReadAsync(Production, RunId, default))!.Value.Pending!.Value.Sequence.ShouldBe(1UL);

        gated.Release.SetResult();
        await saveFirst;
        await saveSecond;
        gated.Writes.ShouldBe(2);
        AnchorRecord record = (await anchors.ReadAsync(Production, RunId, default))!.Value;
        record.Committed.Sequence.ShouldBe(1UL);
        record.Pending!.Value.Sequence.ShouldBe(2UL);
    }

    [TestMethod]
    public async Task An_environment_off_the_ring_is_not_anchored_and_a_save_without_a_grant_is_refused()
    {
        (InMemoryWorkflowStateStore inner, InMemoryTenantAnchorStore anchors, SealingCheckpointStore store) = await AnchoredAsync();
        store.IsAnchored(Production).ShouldBeTrue();
        store.IsAnchored(Development).ShouldBeFalse();

        var developmentRun = new WorkflowRunAddress(Development, new WorkflowRunId(RunId));
        using (WorkflowRun run = WorkflowRun.CreateNew(inner, developmentRun.RunId, "petWorkflow", default, Development))
        {
            await run.EnqueueAsync(default);
        }

        using (WorkflowRun? run = await WorkflowRun.ResumeAsync(store, developmentRun))
        {
            await run!.CheckpointAsync(1, default);
        }

        (await anchors.ReadAsync(Development, RunId, default)).ShouldBeNull("nothing is anchored for an environment the runner admits clear and holds no key for");

        // An anchored save carries the grant it is staged under: no epoch, no stage, no dispatch.
        using WorkflowRun? ungranted = await WorkflowRun.ResumeAsync(store, ProductionRun);
        (await Should.ThrowAsync<CheckpointAnchorException>(async () => await ungranted!.CheckpointAsync(1, default))).Message.ShouldContain("epoch");
        WorkflowCheckpointSerializer.TryProject((await inner.LoadAsync(ProductionRun, default))!.Value.Row, out CheckpointProjection still).ShouldBeTrue();
        still.Sequence.ShouldBe(0);
    }

    // The control plane's side: a run created and enqueued in the clear, before any runner has claimed.
    private static async ValueTask EnqueueGenesisAsync(IWorkflowCheckpointStore controlPlane)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":7}"""u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(controlPlane, ProductionRun.RunId, "petWorkflow", inputs.RootElement, Production);
        await run.EnqueueAsync(default);
    }

    private static async ValueTask<(InMemoryWorkflowStateStore Inner, InMemoryTenantAnchorStore Anchors, SealingCheckpointStore Store)> AnchoredAsync()
    {
        var inner = new InMemoryWorkflowStateStore();
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Production, Incarnation, default);
        await EnqueueGenesisAsync(inner);
        return (inner, anchors, Fresh(inner, anchors));
    }

    // A new sealing store over the same tenant state: what a runner process that just started, or a peer that took
    // the lease over, holds.
    private static SealingCheckpointStore Fresh(IWorkflowCheckpointStore inner, ITenantAnchorStore anchors) => new(inner, Ring(), anchors);

    private static AnchorOrderingKey Key() => new(Incarnation, (ulong)Epoch);

    private static AnchorDigest DigestOfStored(InMemoryWorkflowStateStore inner)
    {
        WorkflowCheckpoint stored = inner.LoadAsync(ProductionRun, default).AsTask().GetAwaiter().GetResult()!.Value;
        return CheckpointDigest.ForSubmitted(CheckpointRow.SubmittedBytes(stored.Row).Span);
    }

    private static RunnerKeyRing Ring()
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Production, "k1", envelopeMac);
        // Development is admitted clear (decision 10): served, unanchored, with no key; nothing else is served at all.
        return RunnerKeyRing.From(
            new Dictionary<string, RunnerEnvironmentKeys>
            {
                [Production] = new("k1", PayloadKey, envelopeMac, Sealed: true),
            },
            Development);
    }

    private static byte[] Row(WorkflowRunAddress address, long sequence, long? epoch = Epoch, ulong? incarnation = Incarnation)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                address.RunId,
                address.Environment,
                "petWorkflow",
                WorkflowRunStatus.Running,
                (int)sequence,
                sequence,
                Epoch: epoch,
                T0,
                T0,
                CorrelationId: null,
                RerunOf: null,
                default,
                default,
                [],
                false,
                null,
                null,
                Incarnation: incarnation),
            retryCounters,
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            inputs: default,
            stepOutputs,
            outputs: default,
            []);
    }

    // A store whose next save fails: before the row lands, or after it (the acknowledgement lost).
    private sealed class FailingStore(IWorkflowCheckpointStore inner, bool failAfterWrite) : IWorkflowCheckpointStore
    {
        public int Writes { get; private set; }

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken) => inner.LoadAsync(address, cancellationToken);

        // Not async: the interface takes the index by `in`, which an async method may not.
        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => this.SaveCoreAsync(address, checkpointRow, index, expected, cancellationToken);

        private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
        {
            this.Writes++;
            if (failAfterWrite)
            {
                await inner.SaveAsync(address, checkpointRow, index, expected, cancellationToken);
            }

            throw new WorkflowConflictException(address, expected);
        }
    }

    // A store whose first save blocks until released, so a second save's ordering can be observed.
    private sealed class GatedStore(IWorkflowCheckpointStore inner) : IWorkflowCheckpointStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Writes { get; private set; }

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken) => inner.LoadAsync(address, cancellationToken);

        // Not async: the interface takes the index by `in`, which an async method may not.
        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
            => this.SaveCoreAsync(address, checkpointRow, index, cancellationToken);

        private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointRow, WorkflowRunIndexEntry index, CancellationToken cancellationToken)
        {
            if (++this.Writes == 1)
            {
                this.Entered.TrySetResult();
                await this.Release.Task;
            }

            WorkflowCheckpoint? current = await inner.LoadAsync(address, cancellationToken);
            return await inner.SaveAsync(address, checkpointRow, index, current?.Etag ?? WorkflowEtag.None, cancellationToken);
        }
    }
}