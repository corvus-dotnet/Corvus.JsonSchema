// <copyright file="AuditChainStanding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>Where one chain stands once it is read beside the other chains of its sink (ADR 0069).</summary>
public enum AuditChainStanding
{
    /// <summary>The chain verifies, and any chain it says it continues is present and holds the hash it names.</summary>
    Verified,

    /// <summary>
    /// The chain ends in a torn line, and a later chain continues from its last whole record. This is what a failed
    /// append leaves: the writer abandoned the chain and said so in the next. The whole records stand.
    /// </summary>
    AbandonedAndContinued,

    /// <summary>
    /// The chain holds no whole record. It is what a failed first append leaves, and no later chain names it, so nothing
    /// ties it to the evidence either way. It is reported and does not fail the set.
    /// </summary>
    Empty,

    /// <summary>The chain fails verification on its own: see its <see cref="AuditChainVerification.Break"/>.</summary>
    Broken,

    /// <summary>The chain says it continues a chain that is not among those given: that chain was removed, or was not supplied.</summary>
    PredecessorMissing,

    /// <summary>The chain says it continues from a hash its predecessor does not hold: the predecessor was cut short or rewritten.</summary>
    ContinuationNotFound,
}