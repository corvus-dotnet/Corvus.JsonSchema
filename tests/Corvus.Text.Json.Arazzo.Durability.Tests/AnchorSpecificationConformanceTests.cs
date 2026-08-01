// <copyright file="AnchorSpecificationConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The conformance suite for ADR 0065's normative tenant-anchor specification. Nine rounds of adversarial
/// review closed that state machine by inspection; this suite is what keeps it closed after every future edit,
/// which is the failure mode that produced most of those rounds.
/// </summary>
/// <remarks>
/// Each test corresponds to one of the specification's numbered conformance assertions. The two properties the
/// review verified by hand — the table's totality and first-match correctness over the whole state product, and
/// the acceptance clauses' mutual exclusivity with invariant preservation — are asserted here by exhaustive
/// enumeration and randomized sequences rather than by argument.
/// </remarks>
[TestClass]
public sealed class AnchorSpecificationConformanceTests
{
    private const ulong Attested = 7;
    private const string Run = "0123456789abcdef0123456789abcdef";
    private const string Environment = "acme-prod";

    private static readonly AnchorDigest D0 = Digest(0);
    private static readonly AnchorDigest D1 = Digest(1);
    private static readonly AnchorDigest D2 = Digest(2);
    private static readonly AnchorOrderingKey K1 = new(Attested, 1);
    private static readonly AnchorOrderingKey K2 = new(Attested, 2);

    // ── Assertion 1: the acceptance predicate, accepting and rejecting cases ─────────────────────────────────

    [TestMethod]
    public void Create_is_accepted_only_fully_constrained()
    {
        AnchorRecord genesis = Genesis();

        AnchorAcceptance.Classify(null, genesis, Attested).ShouldBe(AnchorWriteKind.Create);

        // Each constraint is load-bearing: an unconstrained create-if-absent would itself be the bare committed
        // write the promote rules exist to remove.
        // A non-zero sequence with the create-shaped counter is neither a create nor a recovery.
        AnchorAcceptance.Classify(null, genesis with { Committed = genesis.Committed with { Sequence = 5 } }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(null, genesis with { State = AnchorState.Terminal }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(null, genesis with { ReanchorCounter = 1 }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(null, genesis with { Pending = new AnchorMark(K1, 1, D1) }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(null, genesis with { EpochHighWater = K2 }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(null, genesis with { Committed = genesis.Committed with { Key = new AnchorOrderingKey(Attested + 1, 0) } }, Attested).ShouldBe(AnchorWriteKind.Rejected);
    }

    [TestMethod]
    public void A_bare_committed_advance_is_never_accepted()
    {
        // The single most important rejection: one write must not be able to move committed to an arbitrary
        // value, because the next open would see the store below it and hard-fault the run permanently.
        AnchorRecord stored = Genesis();
        AnchorRecord bare = stored with { Committed = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };

        AnchorAcceptance.Classify(stored, bare, Attested).ShouldBe(AnchorWriteKind.Rejected);
    }

    [TestMethod]
    public void Prepare_promote_discard_and_the_fused_write_are_accepted_and_out_of_order_staging_is_not()
    {
        AnchorRecord stored = Genesis();
        AnchorMark staged = new(K1, 1, D1);

        AnchorRecord prepared = stored with { Pending = staged, EpochHighWater = K1 };
        AnchorAcceptance.Classify(stored, prepared, Attested).ShouldBe(AnchorWriteKind.Prepare);

        // A2 is enforced at the predicate: a pending must be exactly one beyond committed.
        AnchorAcceptance.Classify(stored, stored with { Pending = new AnchorMark(K1, 2, D1), EpochHighWater = K1 }, Attested).ShouldBe(AnchorWriteKind.Rejected);

        // The floor: a pending below the high-water mark is refused (the displaced-holder fence).
        AnchorRecord high = stored with { EpochHighWater = K2 };
        AnchorAcceptance.Classify(high, high with { Pending = new AnchorMark(K1, 1, D1) }, Attested).ShouldBe(AnchorWriteKind.Rejected);

        AnchorRecord promoted = prepared with { Committed = staged, Pending = null };
        AnchorAcceptance.Classify(prepared, promoted, Attested).ShouldBe(AnchorWriteKind.Promote);

        // Promote matches on key, sequence AND digest — a promote of a different digest is not a promote.
        AnchorAcceptance.Classify(prepared, prepared with { Committed = new AnchorMark(K1, 1, D2), Pending = null }, Attested).ShouldBe(AnchorWriteKind.Rejected);

        AnchorRecord discarded = prepared with { Pending = null };
        AnchorAcceptance.Classify(prepared, discarded, Attested).ShouldBe(AnchorWriteKind.Discard);

        // The fused steady state, which is what makes the guide's one-round-trip budget hold.
        AnchorRecord fused = prepared with { Committed = staged, Pending = new AnchorMark(K2, 2, D2), EpochHighWater = K2 };
        AnchorAcceptance.Classify(prepared, fused, Attested).ShouldBe(AnchorWriteKind.PromoteAndPrepare);
    }

    [TestMethod]
    public void Finalize_requires_no_outstanding_pending()
    {
        AnchorRecord stored = Genesis();
        AnchorAcceptance.Classify(stored, stored with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed }, Attested).ShouldBe(AnchorWriteKind.Finalize);

        // Terminal disposal is two writes: promote, then finalize. Finalizing over a pending would silently drop
        // a checkpoint whose acknowledgement was merely lost — and because Abandon has the same record SHAPE, the
        // disposition is what keeps the store able to tell them apart.
        AnchorRecord prepared = stored with { Pending = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };
        AnchorAcceptance.Classify(prepared, prepared with { State = AnchorState.Terminal, Pending = null, Disposition = AnchorDisposition.Completed }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(stored, stored with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Abandoned }, Attested).ShouldBe(AnchorWriteKind.Rejected);
    }

    [TestMethod]
    public void The_record_identity_is_immutable_across_every_write()
    {
        AnchorRecord stored = Genesis();

        AnchorAcceptance.Classify(stored, stored with { RunId = new string('f', 32), State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed }, Attested).ShouldBe(AnchorWriteKind.Rejected);
        AnchorAcceptance.Classify(stored, stored with { EnvironmentId = "rival", State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed }, Attested).ShouldBe(AnchorWriteKind.Rejected);
    }

    // ── Assertion 2: invariants hold after every accepted write ──────────────────────────────────────────────

    [TestMethod]
    public void Invariants_hold_after_every_accepted_write_over_a_randomized_sequence()
    {
        var random = new Random(20260801);
        AnchorRecord? stored = null;
        ulong attested = Attested;

        for (int step = 0; step < 20_000; step++)
        {
            AnchorRecord proposed = ProposeArbitrary(random, stored, attested);
            AnchorWriteKind kind = AnchorAcceptance.Classify(stored, proposed, attested);
            if (kind == AnchorWriteKind.Rejected)
            {
                continue;
            }

            AnchorAcceptance.SatisfiesInvariants(proposed).ShouldBeTrue($"A1-A4 violated after an accepted {kind} at step {step}");
            AnchorAcceptance.SatisfiesIncarnationBound(proposed, attested).ShouldBeTrue($"A5 violated after an accepted {kind} at step {step}");
            stored = proposed;

            // An attested restore advances the incarnation; the record must stay consistent across it.
            if (random.Next(500) == 0)
            {
                attested++;
            }
        }
    }

    // ── Assertion 3: the table is total, and the first match is the specified outcome ────────────────────────

    [TestMethod]
    public void The_decision_table_is_total_over_the_whole_state_product()
    {
        foreach (AnchorRecord? anchor in AnchorStates())
        {
            foreach (AnchorStoreRow row in StoreStates())
            {
                foreach (ulong attested in new ulong[] { Attested, Attested + 1 })
                {
                    AnchorOpenDecision decision = AnchorOpen.Evaluate(anchor, row, attested);

                    decision.Row.ShouldBeInRange(1, 51);
                    (decision.Outcome == AnchorOpenOutcome.HardFault)
                        .ShouldBe(decision.Fault != AnchorFaultKind.None, $"row {decision.Row} disagrees about whether it faulted");
                    if (decision.Outcome != AnchorOpenOutcome.HardFault)
                    {
                        decision.ReAnchorable.ShouldBeFalse($"row {decision.Row} is not a fault, so re-anchoring is meaningless");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void A_terminal_record_refuses_the_claim_whatever_the_store_says()
    {
        // Row 1 precedes everything: a re-presented or tampered row for a finished run is a routine refusal,
        // not a security incident on every claim attempt.
        AnchorRecord terminal = Genesis() with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed };

        foreach (AnchorStoreRow row in StoreStates())
        {
            AnchorOpen.Evaluate(terminal, row, Attested).ShouldBe(new AnchorOpenDecision(1, AnchorOpenOutcome.RefuseClaim, AnchorFaultKind.None, false));
        }
    }

    [TestMethod]
    public void The_steady_state_rows_decide_as_specified()
    {
        AnchorRecord committed = Genesis() with { Committed = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };
        AnchorRecord prepared = committed with { Pending = new AnchorMark(K2, 2, D2), EpochHighWater = K2 };

        AnchorOpen.Evaluate(null, AnchorStoreRow.Absent, Attested).Row.ShouldBe(2);
        AnchorOpen.Evaluate(null, AnchorStoreRow.Genesis(D0, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Create);
        AnchorOpen.Evaluate(null, AnchorStoreRow.At(3, D1, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.AnchorLost);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.Unreadable, Attested).Fault.ShouldBe(AnchorFaultKind.Unreadable);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.Absent, Attested).Fault.ShouldBe(AnchorFaultKind.RollbackToNothing);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Proceed);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(1, D2, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.Substitution);
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(2, D2, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.PromoteThenProceed);
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(2, D0, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.Divergence);
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.DiscardThenProceed);
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(1, D0, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.Substitution);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.Genesis(D0, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.Rollback);
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(9, D0, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.AnchorLostWrite);
    }

    [TestMethod]
    public void An_attested_restore_is_recoverable_and_a_same_incarnation_rollback_is_not()
    {
        // The `restored` qualifier is what stops an attested restore bricking every run in the environment,
        // while keeping the same four rows closed within one incarnation.
        AnchorRecord committed = Genesis() with { Committed = new AnchorMark(K1, 4, D1), EpochHighWater = K1 };

        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(2, D0, Attested), Attested).ReAnchorable.ShouldBeFalse();
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(2, D0, Attested), Attested + 1).ReAnchorable.ShouldBeTrue();

        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(4, D2, Attested), Attested).ReAnchorable.ShouldBeFalse();
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(4, D2, Attested), Attested + 1).ReAnchorable.ShouldBeTrue();

        AnchorOpen.Evaluate(committed, AnchorStoreRow.Absent, Attested).ReAnchorable.ShouldBeFalse();
        AnchorOpen.Evaluate(committed, AnchorStoreRow.Absent, Attested + 1).ReAnchorable.ShouldBeTrue();
    }

    // ── Assertion 5: re-anchor replay and terminal resurrection are refused ──────────────────────────────────

    [TestMethod]
    public void A_replayed_re_anchor_is_rejected_and_a_terminal_run_cannot_be_resurrected()
    {
        AnchorRecord stored = Genesis() with { Committed = new AnchorMark(K1, 3, D1), EpochHighWater = K1, ReanchorCounter = 4 };
        AnchorOrderingKey reset = new(Attested, 0);
        AnchorRecord reanchored = stored with { Committed = new AnchorMark(reset, 3, D2), EpochHighWater = reset, Pending = null, ReanchorCounter = 5 };

        AnchorAcceptance.Classify(stored, reanchored, Attested).ShouldBe(AnchorWriteKind.ReAnchor);

        // Replaying that same signed record against the advanced store is refused on the counter.
        AnchorAcceptance.Classify(reanchored, reanchored, Attested).ShouldBe(AnchorWriteKind.Rejected);

        // And a re-anchor may not resurrect a finished run.
        AnchorRecord terminal = stored with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed };
        AnchorAcceptance.Classify(terminal, reanchored, Attested).ShouldBe(AnchorWriteKind.Rejected);
    }

    [TestMethod]
    public void A_re_anchor_resets_the_epoch_so_the_next_prepare_is_accepted()
    {
        // An incarnation change resets the control plane's epoch counter. Carrying the pre-restore epoch forward
        // would floor the run above every grant the post-restore control plane can issue.
        ulong restored = Attested + 1;
        AnchorRecord stored = Genesis() with { Committed = new AnchorMark(new AnchorOrderingKey(Attested, 47), 3, D1), EpochHighWater = new AnchorOrderingKey(Attested, 47) };
        AnchorOrderingKey reset = new(restored, 0);
        AnchorRecord reanchored = stored with { Committed = new AnchorMark(reset, 3, D2), EpochHighWater = reset, ReanchorCounter = 1 };

        AnchorAcceptance.Classify(stored, reanchored, restored).ShouldBe(AnchorWriteKind.ReAnchor);

        AnchorOrderingKey firstGrantAfterRestore = new(restored, 1);
        AnchorRecord next = reanchored with { Pending = new AnchorMark(firstGrantAfterRestore, 4, D0), EpochHighWater = firstGrantAfterRestore };
        AnchorAcceptance.Classify(reanchored, next, restored).ShouldBe(AnchorWriteKind.Prepare);
    }

    // ── Assertion 6: a 409 resolves by re-open, never by discard ─────────────────────────────────────────────

    [TestMethod]
    public void A_superseded_save_resolves_by_re_open_not_by_discard()
    {
        // The runner byte-identically resends a save whose acknowledgement was lost and receives a 409. Treating
        // that as abandonment and discarding would leave the store one ahead of the anchor — row 14, a hard
        // fault on a merely dropped response.
        AnchorRecord prepared = Genesis() with { Pending = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };

        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.PromoteThenProceed);

        AnchorRecord wronglyDiscarded = prepared with { Pending = null };
        AnchorOpen.Evaluate(wronglyDiscarded, AnchorStoreRow.At(1, D1, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.AnchorLostWrite);
    }

    // ── Assertion 7: every crash point recovers without a fault ──────────────────────────────────────────────

    [TestMethod]
    public void Every_crash_point_between_create_and_finalize_recovers_without_a_fault()
    {
        AnchorRecord genesis = Genesis();
        AnchorMark staged = new(K1, 1, D1);
        AnchorRecord prepared = genesis with { Pending = staged, EpochHighWater = K1 };
        AnchorRecord promoted = prepared with { Committed = staged, Pending = null };

        // Crash between Create and the first Prepare: the store is still at genesis.
        AnchorOpen.Evaluate(genesis, AnchorStoreRow.Genesis(D0, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Proceed);

        // Crash between Prepare and dispatch: the save never left.
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.Genesis(D0, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.DiscardThenProceed);

        // Crash between dispatch and acknowledgement: the save landed, the ack did not.
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.PromoteThenProceed);

        // Crash between acknowledgement and Promote: identical anchor state, same resolution.
        AnchorOpen.Evaluate(prepared, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.PromoteThenProceed);

        // Crash between Promote and Finalize: the terminal checkpoint is committed and the record is still Live.
        AnchorOpen.Evaluate(promoted, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Proceed);

        // Crash during ReAnchor: it is one atomic whole-record write, so either it applied or it did not.
        AnchorOrderingKey reset = new(Attested, 0);
        AnchorRecord reanchored = promoted with { Committed = new AnchorMark(reset, 1, D1), EpochHighWater = reset, ReanchorCounter = 1 };
        AnchorOpen.Evaluate(reanchored, AnchorStoreRow.At(1, D1, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Proceed);
    }

    [TestMethod]
    public void The_genesis_row_is_readable_without_a_runner_mac()
    {
        // It is control-plane-written from initiator-sealed input, so it has no runner region and cannot carry a
        // runner MAC. Treating that as unreadable would hard-fault the crash point between Create and the first
        // Prepare, which assertion 7 requires to resolve cleanly.
        AnchorOpen.Evaluate(null, AnchorStoreRow.Genesis(D0, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Create);
        AnchorOpen.Evaluate(Genesis(), AnchorStoreRow.Genesis(D0, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.Proceed);
    }

    [TestMethod]
    public void An_unattested_incarnation_faults_the_open()
    {
        // The backstop for A5, which the acceptance predicate leaves as the writer's obligation. Without it the
        // record proceeds while its high-water mark sits above every key an honest Prepare can build.
        AnchorRecord ahead = Genesis() with
        {
            Committed = new AnchorMark(new AnchorOrderingKey(Attested + 5, 1), 1, D1),
            EpochHighWater = new AnchorOrderingKey(Attested + 5, 1),
        };
        AnchorOpenDecision decision = AnchorOpen.Evaluate(ahead, AnchorStoreRow.At(1, D1, Attested), Attested);
        decision.Fault.ShouldBe(AnchorFaultKind.UnattestedIncarnation);
        decision.ReAnchorable.ShouldBeTrue();

        // And a store row whose region names an incarnation the tenant has not attested.
        AnchorRecord committed = Genesis() with { Committed = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };
        AnchorOpen.Evaluate(committed, AnchorStoreRow.At(1, D1, Attested + 3), Attested).Fault.ShouldBe(AnchorFaultKind.UnattestedIncarnation);
    }

    [TestMethod]
    public void Abandon_disposes_of_a_faulted_run_that_finalize_cannot_reach()
    {
        // The fault rows that motivate Abandon all carry an outstanding pending, and a same-incarnation
        // substitution is deliberately not re-anchorable — so without its own clause no legal write could ever
        // clear it and the record would stay Live forever.
        AnchorRecord faulted = Genesis() with { Pending = new AnchorMark(K1, 1, D1), EpochHighWater = K1 };
        AnchorOpen.Evaluate(faulted, AnchorStoreRow.At(1, D2, Attested), Attested).Fault.ShouldBe(AnchorFaultKind.Divergence);

        AnchorAcceptance.Classify(faulted, faulted with { State = AnchorState.Terminal, Pending = null, Disposition = AnchorDisposition.Abandoned }, Attested).ShouldBe(AnchorWriteKind.Abandon);
        AnchorOpen.Evaluate(faulted with { State = AnchorState.Terminal, Pending = null, Disposition = AnchorDisposition.Abandoned }, AnchorStoreRow.At(1, D2, Attested), Attested).Outcome.ShouldBe(AnchorOpenOutcome.RefuseClaim);
    }

    [TestMethod]
    public void A_lost_anchor_is_recoverable_by_re_anchor()
    {
        // Rows 4 and 5 promise re-anchorability for a missing record; the clause must therefore admit an absent
        // one, where there is no counter to increment.
        AnchorOpen.Evaluate(null, AnchorStoreRow.At(4, D1, Attested), Attested).ReAnchorable.ShouldBeTrue();

        AnchorOrderingKey reset = new(Attested, 0);
        AnchorRecord recovered = Genesis() with { Committed = new AnchorMark(reset, 4, D1), EpochHighWater = reset, ReanchorCounter = 1 };
        AnchorAcceptance.Classify(null, recovered, Attested).ShouldBe(AnchorWriteKind.ReAnchor);

        // A create is still a create: the two absent-record clauses are distinguished by the sequence.
        AnchorAcceptance.Classify(null, Genesis(), Attested).ShouldBe(AnchorWriteKind.Create);
    }

    private static AnchorRecord Genesis()
        => new(Run, Environment, AnchorState.Live, new AnchorOrderingKey(Attested, 0), new AnchorMark(new AnchorOrderingKey(Attested, 0), 0, D0), null, 0);

    private static AnchorDigest Digest(byte seed)
    {
        Span<byte> bytes = stackalloc byte[SHA256.HashSizeInBytes];
        bytes.Fill(seed);
        return new AnchorDigest(bytes);
    }

    private static IEnumerable<AnchorRecord?> AnchorStates()
    {
        AnchorRecord committed = Genesis() with { Committed = new AnchorMark(K1, 3, D1), EpochHighWater = K1 };
        yield return null;
        yield return committed;
        yield return committed with { Pending = new AnchorMark(K2, 4, D2), EpochHighWater = K2 };
        yield return committed with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed };
    }

    private static IEnumerable<AnchorStoreRow> StoreStates()
    {
        yield return AnchorStoreRow.Absent;
        yield return AnchorStoreRow.Unreadable;
        yield return AnchorStoreRow.Genesis(D0, Attested);
        foreach (ulong sequence in new ulong[] { 2, 3, 4, 5, 9 })
        {
            yield return AnchorStoreRow.At(sequence, D1, Attested);
            yield return AnchorStoreRow.At(sequence, D2, Attested);
            yield return AnchorStoreRow.At(sequence, D0, Attested);
        }
    }

    // Proposes an arbitrary next record: mostly legal transitions, with enough noise that illegal ones are
    // exercised too. Whatever the predicate accepts must preserve the invariants.
    private static AnchorRecord ProposeArbitrary(Random random, in AnchorRecord? stored, ulong attested)
    {
        if (stored is not { } r)
        {
            return Genesis() with { Committed = new AnchorMark(new AnchorOrderingKey(attested, 0), 0, Digest((byte)random.Next(4))), EpochHighWater = new AnchorOrderingKey(attested, 0) };
        }

        AnchorOrderingKey higher = new(attested, r.EpochHighWater.Epoch + (ulong)random.Next(1, 3));
        AnchorDigest digest = Digest((byte)random.Next(4));

        return random.Next(8) switch
        {
            0 => r with { Pending = new AnchorMark(higher, r.Committed.Sequence + 1, digest), EpochHighWater = higher },
            1 => r.Pending is { } p ? r with { Committed = p, Pending = null } : r,
            2 => r.Pending is { } p2 ? r with { Committed = p2, Pending = new AnchorMark(higher, p2.Sequence + 1, digest), EpochHighWater = higher } : r,
            3 => r with { Pending = null },
            4 => r with { State = AnchorState.Terminal, Pending = null, Disposition = r.Pending is null ? AnchorDisposition.Completed : AnchorDisposition.Abandoned },
            5 => r with { Committed = new AnchorMark(higher, r.Committed.Sequence + (ulong)random.Next(3), digest), EpochHighWater = higher },
            6 => r with { Committed = new AnchorMark(new AnchorOrderingKey(attested, 0), r.Committed.Sequence, digest), EpochHighWater = new AnchorOrderingKey(attested, 0), ReanchorCounter = r.ReanchorCounter + 1, Pending = null },
            _ => r with { Pending = new AnchorMark(higher, r.Committed.Sequence + (ulong)random.Next(1, 4), digest), EpochHighWater = higher },
        };
    }
}