// <copyright file="AuditEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// What an audit record says happened (ADR 0038): the fields a caller supplies. The chain writer adds the chain id, the
/// sequence, the timestamp and the chain hash. Every field is controlled vocabulary or an identifier, never a workflow
/// payload or a secret, which is the property ADR 0069 preserves by recording nothing new.
/// </summary>
/// <param name="Action">The action name (for example <c>access-request.approve</c>).</param>
/// <param name="Actor">The canonical subject that performed the action.</param>
/// <param name="Tenant">The owner group the actor acts in, or <see langword="null"/> where none is resolved.</param>
/// <param name="TargetKind">The kind of resource the action targeted.</param>
/// <param name="TargetId">The id or name of the resource the action targeted.</param>
/// <param name="Outcome">The outcome, a refusal included (for example <c>granted</c>, <c>refused-own-request</c>). For a read it is the disclosure tier (for example <c>full</c>, <c>redacted</c>, <c>refused</c>).</param>
/// <param name="Environment">The environment the action is scoped to, or <see langword="null"/> where it is not environment-scoped.</param>
/// <param name="Kind">Whether the entry is a mutation or a read.</param>
/// <param name="Suppressed">For a read entry that reports refusals a subject's window counted and did not append, how many; otherwise zero.</param>
public readonly record struct AuditEntry(string Action, string Actor, string? Tenant, string TargetKind, string TargetId, string Outcome, string? Environment, AuditEntryKind Kind = AuditEntryKind.Mutation, long Suppressed = 0);