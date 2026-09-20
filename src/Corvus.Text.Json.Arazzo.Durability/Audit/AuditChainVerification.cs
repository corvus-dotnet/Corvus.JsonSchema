// <copyright file="AuditChainVerification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What verifying one audit chain found (ADR 0069).</summary>
/// <param name="Break">The first break, or <see cref="AuditChainBreak.None"/> for an intact chain.</param>
/// <param name="BreakLine">The 1-based line the first break is on, or 0 for an intact chain.</param>
/// <param name="ChainId">The chain's id, from its first record, or <see langword="null"/> where no record was read.</param>
/// <param name="RecordCount">The number of records verified before the first break.</param>
/// <param name="LastHash">The hash of the last verified record, or <see langword="null"/> where none was verified.</param>
/// <param name="ContinuesChain">The id of the earlier chain the first record says this chain continues, if it says so.</param>
/// <param name="ContinuesHash">The hash, in that earlier chain, the first record says this chain continues from.</param>
/// <param name="ContainsExpectedHash">Whether a verified record has the hash the caller asked after. <see langword="false"/> where the caller asked after none.</param>
public readonly record struct AuditChainVerification(
    AuditChainBreak Break,
    long BreakLine,
    string? ChainId,
    long RecordCount,
    string? LastHash,
    string? ContinuesChain,
    string? ContinuesHash,
    bool ContainsExpectedHash)
{
    /// <summary>Gets a value indicating whether every line verified.</summary>
    public bool IsIntact => this.Break == AuditChainBreak.None;
}