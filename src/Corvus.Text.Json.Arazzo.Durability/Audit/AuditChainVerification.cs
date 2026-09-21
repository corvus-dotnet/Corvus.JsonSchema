// <copyright file="AuditChainVerification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What verifying one audit chain found (ADR 0069).</summary>
/// <param name="Break">The first break, or <see cref="AuditChainBreak.None"/> for an intact chain.</param>
/// <param name="BreakLine">The 1-based line the first break is on, or 0 for an intact chain and for <see cref="AuditChainBreak.AnchorNotFound"/>, which is a property of the whole chain.</param>
/// <param name="ChainId">The chain's id, from its first record, or <see langword="null"/> where no record was read.</param>
/// <param name="RecordCount">The number of records verified before the first break, heads included.</param>
/// <param name="LastHash">The hash of the last verified record, or <see langword="null"/> where none was verified.</param>
/// <param name="ContinuesChain">The id of the earlier chain the first record says this chain continues, if it says so.</param>
/// <param name="ContinuesHash">The hash, in that earlier chain, the first record says this chain continues from.</param>
/// <param name="ContainsExpectedHash">Whether a verified record has the hash the caller asked after. <see langword="false"/> where the caller asked after none.</param>
/// <param name="HeadCount">The number of heads among the verified records.</param>
/// <param name="HeadSignaturesChecked">Whether the heads' signatures were checked, which takes a trust store. Without one a head is only a well-formed record.</param>
/// <param name="HoldsAnchor">Whether a verified head is the anchor the caller supplied. <see langword="false"/> where the caller supplied none. It is reported whatever the break, since a chain that tears after the anchor still holds it.</param>
/// <param name="UnsignedTailCount">The number of verified records after the last head: the records no signature vouches for. The whole chain, where it holds no head.</param>
/// <param name="Writer">The id of the writer the chain belongs to, from its open record, or <see langword="null"/> where no record was read.</param>
public readonly record struct AuditChainVerification(
    AuditChainBreak Break,
    long BreakLine,
    string? ChainId,
    long RecordCount,
    string? LastHash,
    string? ContinuesChain,
    string? ContinuesHash,
    bool ContainsExpectedHash,
    long HeadCount,
    bool HeadSignaturesChecked,
    bool HoldsAnchor,
    long UnsignedTailCount,
    string? Writer)
{
    /// <summary>Gets a value indicating whether every line verified.</summary>
    public bool IsIntact => this.Break == AuditChainBreak.None;
}