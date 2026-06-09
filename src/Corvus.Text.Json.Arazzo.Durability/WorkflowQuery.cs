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
/// <param name="Limit">The maximum number of runs to return.</param>
public readonly record struct WorkflowQuery(
    WorkflowRunStatus? Status = null,
    string? WorkflowId = null,
    int Limit = 100);

/// <summary>One run in a <see cref="WorkflowRunPage"/>: its id and the indexed projection.</summary>
/// <param name="Id">The run id.</param>
/// <param name="Index">The indexed projection (status, workflow id, timestamps, wait/fault summary).</param>
public readonly record struct WorkflowRunListing(WorkflowRunId Id, WorkflowRunIndexEntry Index);

/// <summary>A page of runs matching a <see cref="WorkflowQuery"/>.</summary>
/// <param name="Runs">The matching runs (at most <see cref="WorkflowQuery.Limit"/>).</param>
public readonly record struct WorkflowRunPage(IReadOnlyList<WorkflowRunListing> Runs);