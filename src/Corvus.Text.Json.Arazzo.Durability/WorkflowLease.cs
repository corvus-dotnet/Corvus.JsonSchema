// <copyright file="WorkflowLease.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An advisory single-owner lease on a run, so that only one worker resumes (and advances) a given run at a
/// time. A lease is acquired before a worker re-enters a suspended or crashed run and released when it
/// yields; it has a TTL so a crashed holder's lease expires and another worker can take over.
/// </summary>
/// <remarks>
/// The lease is advisory: it coordinates well-behaved workers but the authoritative guard against a
/// double-advance is still the <see cref="WorkflowEtag"/> on save. Each backend maps the lease to its native
/// primitive (a blob lease, a Postgres advisory lock, SQL Server's <c>sp_getapplock</c>, a Redis key with an
/// expiry).
/// </remarks>
/// <param name="RunId">The run the lease is held on.</param>
/// <param name="Owner">The opaque identity of the worker that holds the lease.</param>
/// <param name="Token">An opaque token proving ownership, presented when releasing the lease.</param>
/// <param name="ExpiresAt">The instant after which the lease is no longer valid and may be re-acquired.</param>
public readonly record struct WorkflowLease(WorkflowRunId RunId, string Owner, string Token, DateTimeOffset ExpiresAt);