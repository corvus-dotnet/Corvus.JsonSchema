// <copyright file="AnchorOpen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>What a runner does with a run after evaluating the anchor decision table (ADR 0065).</summary>
public enum AnchorOpenOutcome
{
    /// <summary>No such run. Benign.</summary>
    NotFound,

    /// <summary>First claim of a fresh run: write the genesis anchor record, then proceed.</summary>
    Create,

    /// <summary>Proceed with the advance.</summary>
    Proceed,

    /// <summary>Promote the staged mark, then proceed.</summary>
    PromoteThenProceed,

    /// <summary>Discard the staged mark, then proceed.</summary>
    DiscardThenProceed,

    /// <summary>The run finished. Refuse the claim.</summary>
    RefuseClaim,

    /// <summary>Fail closed. See <see cref="AnchorOpenDecision.Fault"/> for which condition fired.</summary>
    HardFault,
}

/// <summary>Which fault condition the anchor decision table detected (ADR 0065).</summary>
public enum AnchorFaultKind
{
    /// <summary>No fault.</summary>
    None,

    /// <summary>An anchor entry is missing for a run whose store row is beyond genesis.</summary>
    AnchorLost,

    /// <summary>The store row does not parse, or its runner MAC does not verify.</summary>
    Unreadable,

    /// <summary>The anchor holds a run whose store row has vanished.</summary>
    RollbackToNothing,

    /// <summary>A row at a known sequence carries an unexpected digest.</summary>
    Substitution,

    /// <summary>A staged sequence carries an unexpected digest — the displaced-holder race.</summary>
    Divergence,

    /// <summary>The store row is behind the committed mark.</summary>
    Rollback,

    /// <summary>The store row is ahead of everything the anchor knows about.</summary>
    AnchorLostWrite,

    /// <summary>A mark carries an incarnation the tenant has not attested.</summary>
    UnattestedIncarnation,
}

/// <summary>The store row as the runner reads it, after parsing and verifying the runner MAC (ADR 0065).</summary>
/// <param name="Present">Whether a row exists at all.</param>
/// <param name="Readable">Whether it parsed and its runner MAC verified.</param>
/// <param name="Sequence">The sequence, taken from the MAC-verified region — never from the unauthenticated projection.</param>
/// <param name="Digest">The digest computed over the pinned submitted-bytes layout.</param>
/// <param name="RegionIncarnation">The incarnation from the MAC-verified region; ignored at genesis, which has no region.</param>
public readonly record struct AnchorStoreRow(bool Present, bool Readable, ulong Sequence, AnchorDigest Digest, ulong RegionIncarnation = 0)
{
    /// <summary>Gets a row that is not there.</summary>
    public static AnchorStoreRow Absent => new(false, true, 0, default);

    /// <summary>Gets a row that exists but does not parse or whose MAC fails.</summary>
    public static AnchorStoreRow Unreadable => new(true, false, 0, default);

    /// <summary>
    /// Creates the genesis row. It is control-plane-written from initiator-sealed input, so it has no runner
    /// region and cannot carry a runner MAC: "no MAC" is its correct state, not a failure, and its authenticator
    /// is the initiator signature.
    /// </summary>
    /// <param name="digest">The genesis digest, over that row's own pinned layout.</param>
    /// <param name="incarnation">The attested incarnation the run was created under.</param>
    /// <returns>The row.</returns>
    public static AnchorStoreRow Genesis(AnchorDigest digest, ulong incarnation) => new(true, true, 0, digest, incarnation);

    /// <summary>Creates a readable row beyond genesis.</summary>
    /// <param name="sequence">The sequence from the MAC-verified region.</param>
    /// <param name="digest">The computed digest.</param>
    /// <param name="incarnation">The incarnation from the MAC-verified region.</param>
    /// <returns>The row.</returns>
    public static AnchorStoreRow At(ulong sequence, AnchorDigest digest, ulong incarnation = 0) => new(true, true, sequence, digest, incarnation);
}

/// <summary>The table row that matched, and what it decided (ADR 0065).</summary>
/// <param name="Row">The 1-based table row number, for diagnosis and for conformance assertions.</param>
/// <param name="Outcome">What the runner does.</param>
/// <param name="Fault">Which fault fired, when the outcome is a hard fault.</param>
/// <param name="ReAnchorable">Whether an operator-signed re-anchor may recover this fault.</param>
public readonly record struct AnchorOpenDecision(int Row, AnchorOpenOutcome Outcome, AnchorFaultKind Fault, bool ReAnchorable);

/// <summary>
/// The anchor decision table (ADR 0065, "Normative specification: the tenant anchor"), as a pure function.
/// Rows are evaluated in order and the first match is taken; the table is total over
/// <c>{Terminal, missing, committed-only, committed+pending} × {absent, unreadable, genesis, any sequence} ×
/// {digest matches, differs}</c>.
/// </summary>
public static class AnchorOpen
{
    /// <summary>
    /// Evaluates the table for one run.
    /// </summary>
    /// <param name="anchor">The tenant anchor record, or <see langword="null"/> when there is none.</param>
    /// <param name="row">The store row, already parsed and MAC-verified (or flagged unreadable).</param>
    /// <param name="attestedIncarnation">The environment's tenant-attested incarnation, which decides whether a
    /// backwards move is explained by an attested restore and is therefore recoverable.</param>
    /// <returns>The matching row and its decision.</returns>
    public static AnchorOpenDecision Evaluate(in AnchorRecord? anchor, in AnchorStoreRow row, ulong attestedIncarnation)
    {
        // Row 1: a finished run is refused whatever the store says.
        if (anchor is { State: AnchorState.Terminal })
        {
            return new(1, AnchorOpenOutcome.RefuseClaim, AnchorFaultKind.None, false);
        }

        if (anchor is not { } a)
        {
            // Row 2: no anchor, no row — benign.
            if (!row.Present)
            {
                return new(2, AnchorOpenOutcome.NotFound, AnchorFaultKind.None, false);
            }

            // Row 5 precedes the sequence-based rows: an unreadable row has no trustworthy sequence.
            if (!row.Readable)
            {
                return new(5, AnchorOpenOutcome.HardFault, AnchorFaultKind.Unreadable, true);
            }

            // Row 3: first claim of a fresh run.
            if (row.Sequence == 0)
            {
                return new(3, AnchorOpenOutcome.Create, AnchorFaultKind.None, false);
            }

            // Row 4: an anchor was lost for a run that has advanced.
            return new(4, AnchorOpenOutcome.HardFault, AnchorFaultKind.AnchorLost, true);
        }

        if (row.Present && !row.Readable)
        {
            return new(5, AnchorOpenOutcome.HardFault, AnchorFaultKind.Unreadable, true);
        }

        // Row 5a: the backstop for A5, which the acceptance predicate leaves as the writer's obligation. Without
        // it a record whose incarnation exceeds the attested value proceeds while its high-water mark sits above
        // every ordering key an honest Prepare can build — a permanent per-run stall.
        if (a.Committed.Key.Incarnation > attestedIncarnation
            || (row.Present && row.Sequence > 0 && row.RegionIncarnation != attestedIncarnation))
        {
            return new(51, AnchorOpenOutcome.HardFault, AnchorFaultKind.UnattestedIncarnation, true);
        }

        // A backwards move is explained when a restore has been attested since this anchor was last written.
        bool restored = a.Committed.Key.Incarnation < attestedIncarnation;

        // Row 6: the anchor holds a run whose row has vanished.
        if (!row.Present)
        {
            return new(6, AnchorOpenOutcome.HardFault, AnchorFaultKind.RollbackToNothing, restored);
        }

        if (a.Pending is { } pending)
        {
            if (row.Sequence == pending.Sequence)
            {
                // Rows 9 and 10: the staged save landed, or something else did.
                return row.Digest == pending.Digest
                    ? new(9, AnchorOpenOutcome.PromoteThenProceed, AnchorFaultKind.None, false)
                    : new(10, AnchorOpenOutcome.HardFault, AnchorFaultKind.Divergence, true);
            }

            if (row.Sequence == a.Committed.Sequence)
            {
                // Rows 11 and 12: the staged save did not land, or the committed row was substituted.
                return row.Digest == a.Committed.Digest
                    ? new(11, AnchorOpenOutcome.DiscardThenProceed, AnchorFaultKind.None, false)
                    : new(12, AnchorOpenOutcome.HardFault, AnchorFaultKind.Substitution, restored);
            }
        }
        else if (row.Sequence == a.Committed.Sequence)
        {
            // Rows 7 and 8: the steady state, or a substitution at the committed coordinate.
            return row.Digest == a.Committed.Digest
                ? new(7, AnchorOpenOutcome.Proceed, AnchorFaultKind.None, false)
                : new(8, AnchorOpenOutcome.HardFault, AnchorFaultKind.Substitution, restored);
        }

        // Row 13: the row is behind what the tenant committed to.
        if (row.Sequence < a.Committed.Sequence)
        {
            return new(13, AnchorOpenOutcome.HardFault, AnchorFaultKind.Rollback, restored);
        }

        // Row 14: the row is ahead of everything the anchor knows about.
        return new(14, AnchorOpenOutcome.HardFault, AnchorFaultKind.AnchorLostWrite, true);
    }
}