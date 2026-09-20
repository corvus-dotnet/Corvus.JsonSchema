// <copyright file="AuditHeadOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// How often an audit chain's head is signed (ADR 0069). The cadence bounds the unsigned window: the records at a
/// chain's tail whose alteration or removal no signature yet shows.
/// </summary>
/// <param name="RecordsPerHead">The number of unsigned records after which a head is signed.</param>
/// <param name="Interval">How long a record may stay unsigned before a head is signed, however few records there are.</param>
public readonly record struct AuditHeadOptions(int RecordsPerHead, TimeSpan Interval)
{
    /// <summary>Gets the default cadence: a head every 64 records or every 60 seconds, whichever comes first.</summary>
    public static AuditHeadOptions Default { get; } = new(64, TimeSpan.FromSeconds(60));
}