// <copyright file="AuditEntryKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What an audit entry is evidence of (ADR 0038, ADR 0070).</summary>
public enum AuditEntryKind
{
    /// <summary>A governance mutation, a refused one included.</summary>
    Mutation,

    /// <summary>A read, or an attempt at one: a payload disclosed at some tier, or a read refused.</summary>
    Read,

    /// <summary>
    /// An authentication that failed (ADR 0071). The entry's fields are read as: <see cref="AuditEntry.Action"/> the
    /// scheme, <see cref="AuditEntry.Outcome"/> the reason, <see cref="AuditEntry.TargetId"/> the remote address,
    /// <see cref="AuditEntry.Actor"/> the subject where the result named one and empty where it did not, and
    /// <see cref="AuditEntry.Tenant"/> the issuer where it named one.
    /// </summary>
    Authentication,
}