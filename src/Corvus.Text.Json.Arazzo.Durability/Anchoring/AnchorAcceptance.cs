// <copyright file="AnchorAcceptance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>Which clause of the anchor acceptance predicate admitted a write, or that none did (ADR 0065).</summary>
public enum AnchorWriteKind
{
    /// <summary>No clause admits the write; the compare-and-swap refuses it.</summary>
    Rejected,

    /// <summary>Create-if-absent: the first claim of a run writes its genesis mark.</summary>
    Create,

    /// <summary>Stage the next save.</summary>
    Prepare,

    /// <summary>Commit the staged save.</summary>
    Promote,

    /// <summary>The fused steady state: commit the previous save and stage the next in one write.</summary>
    PromoteAndPrepare,

    /// <summary>Drop a staged save that did not land.</summary>
    Discard,

    /// <summary>Close the record; claims are refused thereafter.</summary>
    Finalize,

    /// <summary>Operator-signed disposal of a run whose open hard-faults, when a staged save is outstanding.</summary>
    Abandon,

    /// <summary>Operator-signed recovery. The sole write exempt from the high-water floor.</summary>
    ReAnchor,
}

/// <summary>
/// The anchor's acceptance predicate (ADR 0065, "Normative specification: the tenant anchor"), as a pure
/// function. This is the whole of what a tenant anchor store enforces: a compare-and-swap over the whole
/// record, evaluated against the stored record. It is deliberately not a policy engine — it verifies no
/// signature and reads no second key.
/// </summary>
/// <remarks>
/// <para>Two obligations therefore sit with the caller rather than here. A <see cref="AnchorWriteKind.ReAnchor"/>
/// write carries an operator signature the <em>runner</em> verifies before applying it. And the incarnation
/// conjuncts are the writing runner's obligation, because <c>attestedIncarnation</c> is a second key in the
/// tenant's store: where a store supports a two-key conditional write it may enforce them, and where it does
/// not, a violation is caught at the next open by the decision table's incarnation test.</para>
/// </remarks>
public static class AnchorAcceptance
{
    /// <summary>
    /// Classifies a proposed whole-record write against the stored record, returning the clause that admits it
    /// or <see cref="AnchorWriteKind.Rejected"/>. Exactly one clause can admit any given write; the clauses are
    /// mutually exclusive by construction.
    /// </summary>
    /// <param name="stored">The stored record, or <see langword="null"/> when none exists.</param>
    /// <param name="proposed">The proposed replacement record.</param>
    /// <param name="attestedIncarnation">The environment's tenant-attested incarnation.</param>
    /// <returns>The admitting clause, or <see cref="AnchorWriteKind.Rejected"/>.</returns>
    public static AnchorWriteKind Classify(in AnchorRecord? stored, in AnchorRecord proposed, ulong attestedIncarnation)
    {
        if (stored is not { } r)
        {
            // A lost anchor is the primary re-anchor case, so ReAnchor must admit an absent record — otherwise
            // table rows 4 and 5 promise a recovery no clause can perform.
            if (IsCreate(proposed, attestedIncarnation))
            {
                return AnchorWriteKind.Create;
            }

            return IsReAnchorFromAbsent(proposed, attestedIncarnation) ? AnchorWriteKind.ReAnchor : AnchorWriteKind.Rejected;
        }

        // Record identity is immutable across every write; each clause is a whole-record replace.
        if (!string.Equals(proposed.RunId, r.RunId, StringComparison.Ordinal)
            || !string.Equals(proposed.EnvironmentId, r.EnvironmentId, StringComparison.Ordinal))
        {
            return AnchorWriteKind.Rejected;
        }

        if (IsReAnchor(r, proposed, attestedIncarnation))
        {
            return AnchorWriteKind.ReAnchor;
        }

        if (IsFinalize(r, proposed))
        {
            return AnchorWriteKind.Finalize;
        }

        if (IsAbandon(r, proposed))
        {
            return AnchorWriteKind.Abandon;
        }

        if (IsPrepare(r, proposed, attestedIncarnation))
        {
            return AnchorWriteKind.Prepare;
        }

        if (IsPromoteAndPrepare(r, proposed, attestedIncarnation))
        {
            return AnchorWriteKind.PromoteAndPrepare;
        }

        if (IsPromote(r, proposed))
        {
            return AnchorWriteKind.Promote;
        }

        if (IsDiscard(r, proposed))
        {
            return AnchorWriteKind.Discard;
        }

        return AnchorWriteKind.Rejected;
    }

    // Create(W, R) : R absent. Fully constrained — an unconstrained create-if-absent would itself be the bare
    // committed write the promote rules exist to remove.
    private static bool IsCreate(in AnchorRecord w, ulong attestedIncarnation)
        => w.Committed.Sequence == 0
        && w.Committed.Key.Incarnation == attestedIncarnation
        && w.EpochHighWater == w.Committed.Key
        && w.Pending is null
        && w.State == AnchorState.Live
        && w.Disposition == AnchorDisposition.None
        && w.ReanchorCounter == 0;

    // Prepare(W, R) : stage a save when none is outstanding.
    private static bool IsPrepare(in AnchorRecord r, in AnchorRecord w, ulong attestedIncarnation)
        => w.Committed == r.Committed
        && w.Pending is { } p
        && p.Sequence == r.Committed.Sequence + 1
        && p.Key >= r.EpochHighWater
        && p.Key.Incarnation == attestedIncarnation
        && w.EpochHighWater == p.Key
        && w.State == AnchorState.Live
        && r.State == AnchorState.Live
        && w.ReanchorCounter == r.ReanchorCounter;

    // Promote(W, R) : commit the staged save. Matches on key, sequence AND digest.
    private static bool IsPromote(in AnchorRecord r, in AnchorRecord w)
        => r.Pending is { } rp
        && w.Committed == rp
        && w.Pending is null
        && w.EpochHighWater == r.EpochHighWater
        && w.State == AnchorState.Live
        && r.State == AnchorState.Live
        && w.ReanchorCounter == r.ReanchorCounter;

    // PromoteAndPrepare(W, R) : the fused steady state, which is what makes the one-round-trip budget hold.
    private static bool IsPromoteAndPrepare(in AnchorRecord r, in AnchorRecord w, ulong attestedIncarnation)
        => r.Pending is { } rp
        && w.Committed == rp
        && w.Pending is { } wp
        && wp.Sequence == rp.Sequence + 1
        && wp.Key >= r.EpochHighWater
        && wp.Key.Incarnation == attestedIncarnation
        && w.EpochHighWater == wp.Key
        && w.State == AnchorState.Live
        && r.State == AnchorState.Live
        && w.ReanchorCounter == r.ReanchorCounter;

    // Discard(W, R) : drop a staged save that did not land.
    private static bool IsDiscard(in AnchorRecord r, in AnchorRecord w)
        => r.Pending is not null
        && w.Committed == r.Committed
        && w.Pending is null
        && w.EpochHighWater == r.EpochHighWater
        && w.State == AnchorState.Live
        && r.State == AnchorState.Live
        && w.ReanchorCounter == r.ReanchorCounter;

    // Finalize(W, R) : close the record. Requires no outstanding pending — promote first.
    private static bool IsFinalize(in AnchorRecord r, in AnchorRecord w)
        => r.Pending is null
        && w.State == AnchorState.Terminal
        && w.Disposition == AnchorDisposition.Completed
        && w.Committed == r.Committed
        && w.Pending is null
        && w.EpochHighWater == r.EpochHighWater
        && w.ReanchorCounter == r.ReanchorCounter;

    // Abandon(W, R) : Finalize's counterpart when a staged save is outstanding. The fault rows that motivate it
    // all carry a pending, and a same-incarnation substitution is deliberately not re-anchorable — so without
    // this clause no legal write could clear it and the record would stay Live forever.
    private static bool IsAbandon(in AnchorRecord r, in AnchorRecord w)
        => r.Pending is not null
        && r.State == AnchorState.Live
        && w.State == AnchorState.Terminal
        && w.Disposition == AnchorDisposition.Abandoned
        && w.Committed == r.Committed
        && w.Pending is null
        && w.EpochHighWater == r.EpochHighWater
        && w.ReanchorCounter == r.ReanchorCounter;

    // ReAnchor over an absent record: recovery from a LOST anchor, where there is no counter to increment.
    // Distinguished from Create by the counter, not by inferring intent from the sequence: a recovered record
    // starts at 1. The counter cannot fence a replay in this case — the record that held it is what was lost —
    // so the fence here is the operator attestation itself, rate-limited and audited.
    private static bool IsReAnchorFromAbsent(in AnchorRecord w, ulong attestedIncarnation)
        => w.State == AnchorState.Live
        && w.Disposition == AnchorDisposition.None
        && w.ReanchorCounter == 1
        && w.Committed.Key == new AnchorOrderingKey(attestedIncarnation, 0)
        && w.EpochHighWater == w.Committed.Key
        && w.Pending is null
        && w.Committed.Sequence > 0;   // table row 3 covers a missing anchor at genesis with Create

    // ReAnchor(W, R) : operator-signed recovery, exempt from the floor. The epoch component pins to 0 because an
    // incarnation change resets the control plane's counter; carrying the old epoch forward would floor the run
    // above every grant the post-restore control plane can issue.
    private static bool IsReAnchor(in AnchorRecord r, in AnchorRecord w, ulong attestedIncarnation)
        => r.State == AnchorState.Live
        && w.State == AnchorState.Live
        && w.ReanchorCounter == r.ReanchorCounter + 1
        && w.Disposition == AnchorDisposition.None
        && w.Committed.Key == new AnchorOrderingKey(attestedIncarnation, 0)
        && w.EpochHighWater == w.Committed.Key
        && w.Pending is null;

    /// <summary>
    /// Checks invariants A1–A4 over a stored record. A5 (the incarnation bound) is the writer's obligation and
    /// is checked separately by <see cref="SatisfiesIncarnationBound"/>.
    /// </summary>
    /// <param name="record">The record to check.</param>
    /// <returns>Whether A1–A4 hold.</returns>
    public static bool SatisfiesInvariants(in AnchorRecord record)
    {
        // A1: committed.key is at or below the high-water mark.
        if (record.Committed.Key > record.EpochHighWater)
        {
            return false;
        }

        if (record.Pending is { } pending)
        {
            // A4: a terminal record carries no pending.
            if (record.State == AnchorState.Terminal)
            {
                return false;
            }

            // A2: the pending sequence is exactly one beyond committed.
            if (pending.Sequence != record.Committed.Sequence + 1)
            {
                return false;
            }

            // A3: committed.key <= pending.key <= epochHighWater.
            if (record.Committed.Key > pending.Key || pending.Key > record.EpochHighWater)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks invariant A5: no mark carries an incarnation beyond the attested one.</summary>
    /// <param name="record">The record to check.</param>
    /// <param name="attestedIncarnation">The environment's tenant-attested incarnation.</param>
    /// <returns>Whether A5 holds.</returns>
    public static bool SatisfiesIncarnationBound(in AnchorRecord record, ulong attestedIncarnation)
        => record.Committed.Key.Incarnation <= attestedIncarnation
        && (record.Pending is not { } pending || pending.Key.Incarnation <= attestedIncarnation);
}