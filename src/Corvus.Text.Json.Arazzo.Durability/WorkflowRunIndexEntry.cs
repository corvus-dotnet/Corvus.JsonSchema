// <copyright file="WorkflowRunIndexEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The handful of projected fields a store indexes alongside the opaque checkpoint bytes. The runtime
/// projects this entry on every save; a backend never parses the checkpoint — it stores the bytes by id and
/// indexes these fields — which is what keeps each backend a thin adapter.
/// </summary>
/// <remarks>
/// The same projection serves the Tier-2 wait index (find runs that are <em>due</em> or <em>awaiting</em> a
/// correlation) and the control-plane visibility queries (find runs by status / workflow / error), so one
/// index answers timers, message wakeups, and operator queries alike. For Tier 1 (crash recovery) the entry
/// is stored but not queried.
/// </remarks>
/// <param name="WorkflowId">The id of the workflow this run executes.</param>
/// <param name="Status">The run's lifecycle status.</param>
/// <param name="CreatedAt">When the run was first created.</param>
/// <param name="UpdatedAt">When this checkpoint was written.</param>
/// <param name="DueAt">When a suspended run's durable timer fires, if it is waiting on one (Tier 2).</param>
/// <param name="AwaitingChannel">The channel a suspended run is awaiting a message on, if any (Tier 2).</param>
/// <param name="AwaitingCorrelationId">The correlation id a suspended run is awaiting, if any (Tier 2).</param>
/// <param name="ErrorType">The error type of a faulted run, if any.</param>
/// <param name="CorrelationId">The run-wide telemetry correlation id (the W3C trace id) set at creation, if any.</param>
/// <param name="Tags">The free-form tags applied to the run at creation, if any.</param>
/// <param name="SecurityTags">The security tags (KVP labels) applied to the run at creation, if any — the input to tag-based row authorization (§14.2), distinct from the free-form <paramref name="Tags"/>.</param>
public readonly record struct WorkflowRunIndexEntry(
    string WorkflowId,
    WorkflowRunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DueAt = null,
    string? AwaitingChannel = null,
    string? AwaitingCorrelationId = null,
    string? ErrorType = null,
    string? CorrelationId = null,
    TagSet Tags = default,
    IReadOnlyList<SecurityTag>? SecurityTags = null);