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
/// The opaque token from a previous page's <see cref="WorkflowRunPage.NextPageToken"/> as its JSON value, or undefined
/// for the first page. Carried bytes-native: the backend decodes it straight from the request UTF-8 (no managed token
/// string). Stores page by ascending run id (keyset), so the token encodes the last run id of the previous page.
/// </param>
/// <param name="CreatedAfter">Restrict to runs created at or after this instant (inclusive), if set.</param>
/// <param name="CreatedBefore">Restrict to runs created strictly before this instant (exclusive), if set.</param>
/// <param name="UpdatedAfter">Restrict to runs last updated at or after this instant (inclusive), if set.</param>
/// <param name="UpdatedBefore">Restrict to runs last updated strictly before this instant (exclusive), if set.</param>
/// <param name="CorrelationId">Restrict to runs with this telemetry correlation id (exact match), if set.</param>
/// <param name="Tags">Restrict to runs carrying every one of these tags (AND), if set.</param>
/// <param name="Security">A row-authorization filter restricting results to runs whose security tags satisfy the principal's rule(s) (§14.2); <see langword="null"/> is unrestricted.</param>
/// <param name="RunId">Restrict to the run with exactly this id, if set — the point lookup the management client
/// resolves a bare run id through (ADR 0065 §9), so a run's visibility by id is decided by the SAME predicate as its
/// visibility in the list. Composes with every other filter; <see cref="Security"/> always applies. Naming a run id is
/// at least as explicit as naming a reserved workflow id, so the §18/#896 browse exclusion of <c>$draft</c>/<c>$schedule</c>
/// runs does not apply to a point lookup.</param>
public readonly record struct WorkflowQuery(
    WorkflowRunStatus? Status = null,
    string? WorkflowId = null,
    int Limit = 100,
    JsonString ContinuationToken = default,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null,
    string? CorrelationId = null,
    TagSet Tags = default,
    SecurityFilter? Security = null,
    string? RunId = null);

/// <summary>One run in a <see cref="WorkflowRunPage"/>: its full <c>(environment, runId)</c> address and the
/// indexed projection (ADR 0065 decision 9 — the address, not a bare id, is how a listing row is operated on).</summary>
/// <param name="Address">The run's address.</param>
/// <param name="Index">The indexed projection (status, workflow id, timestamps, wait/fault summary).</param>
public readonly record struct WorkflowRunListing(WorkflowRunAddress Address, WorkflowRunIndexEntry Index)
{
    /// <summary>Gets the run id half of <see cref="Address"/>.</summary>
    public WorkflowRunId Id => this.Address.RunId;
}