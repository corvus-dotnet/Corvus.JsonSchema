// <copyright file="WorkflowQuery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A visibility query over the runs a store knows about (plan §11): filter by lifecycle status and/or
/// workflow id, capped at <see cref="Limit"/>. The same index that answers Tier-2 wakeups answers these
/// operator queries — one mechanism, not two.
/// </summary>
/// <param name="Status">Restrict to runs in this status, if set.</param>
/// <param name="WorkflowId">Restrict to runs of this workflow, if set.</param>
/// <param name="Limit">The maximum number of runs to return in this page.</param>
/// <param name="ContinuationToken">
/// The opaque token from a previous page's <see cref="WorkflowRunPage.ContinuationToken"/> to resume from, or
/// <see langword="null"/> for the first page. Stores page by ascending run id (keyset), so the token encodes the
/// last run id of the previous page.
/// </param>
/// <param name="CreatedAfter">Restrict to runs created at or after this instant (inclusive), if set.</param>
/// <param name="CreatedBefore">Restrict to runs created strictly before this instant (exclusive), if set.</param>
/// <param name="UpdatedAfter">Restrict to runs last updated at or after this instant (inclusive), if set.</param>
/// <param name="UpdatedBefore">Restrict to runs last updated strictly before this instant (exclusive), if set.</param>
/// <param name="CorrelationId">Restrict to runs with this telemetry correlation id (exact match), if set.</param>
/// <param name="Tags">Restrict to runs carrying every one of these tags (AND), if set.</param>
public readonly record struct WorkflowQuery(
    WorkflowRunStatus? Status = null,
    string? WorkflowId = null,
    int Limit = 100,
    string? ContinuationToken = null,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null,
    string? CorrelationId = null,
    IReadOnlyList<string>? Tags = null);

/// <summary>One run in a <see cref="WorkflowRunPage"/>: its id and the indexed projection.</summary>
/// <param name="Id">The run id.</param>
/// <param name="Index">The indexed projection (status, workflow id, timestamps, wait/fault summary).</param>
public readonly record struct WorkflowRunListing(WorkflowRunId Id, WorkflowRunIndexEntry Index);

/// <summary>A page of runs matching a <see cref="WorkflowQuery"/>.</summary>
/// <param name="Runs">The matching runs (at most <see cref="WorkflowQuery.Limit"/>), ordered by ascending run id.</param>
/// <param name="ContinuationToken">
/// The opaque token to pass as the next query's <see cref="WorkflowQuery.ContinuationToken"/> to fetch the next
/// page, or <see langword="null"/> when this is the last page.
/// </param>
public readonly record struct WorkflowRunPage(IReadOnlyList<WorkflowRunListing> Runs, string? ContinuationToken = null);