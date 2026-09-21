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
}