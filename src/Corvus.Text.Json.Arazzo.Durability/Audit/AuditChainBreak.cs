// <copyright file="AuditChainBreak.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>How an audit chain first fails verification (ADR 0069).</summary>
public enum AuditChainBreak
{
    /// <summary>The chain is intact: every record is well formed, numbered in order, and linked to the one before it.</summary>
    None,

    /// <summary>
    /// The last line has no line feed, so its record was not wholly stored. This is what a failed append leaves, and the
    /// writer abandons such a chain; it is also what truncating a chain mid-record leaves.
    /// </summary>
    TornTail,

    /// <summary>A line is not JSON, or is not an audit record by its schema.</summary>
    MalformedRecord,

    /// <summary>A record names a different chain from the chain's first record.</summary>
    ForeignRecord,

    /// <summary>A record's sequence is not the one after the record before it: a record was removed, added or reordered.</summary>
    SequenceGap,

    /// <summary>A record's previous-hash is not the hash of the record before it: that record, or this one, was altered.</summary>
    HashMismatch,
}