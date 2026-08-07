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
/// <para>
/// The lease is advisory: it coordinates well-behaved workers but the authoritative guard against a
/// double-advance is still the <see cref="WorkflowEtag"/> on save. Each backend maps the lease to its native
/// primitive (a blob lease, a Postgres advisory lock, SQL Server's <c>sp_getapplock</c>, a Redis key with an
/// expiry).
/// </para>
/// <para>
/// The same type is used two ways, and <see cref="Epoch"/> is what separates them. A lease the store
/// <em>returns</em> is a statement of fact and always carries the grant's epoch. A lease a caller
/// <em>presents</em> is a predicate — the run, the owner and the token it claims to hold — and states no epoch,
/// because the store is the only thing that knows one. That asymmetry is the whole of the ADR 0065 §6 rule: the
/// caller supplies the claim, the store supplies the truth, and the caller's copy is checked against it rather
/// than believed.
/// </para>
/// </remarks>
/// <param name="RunId">The run the lease is held on.</param>
/// <param name="Owner">The opaque identity of the worker that holds the lease.</param>
/// <param name="Token">An opaque token proving ownership, presented when releasing the lease.</param>
/// <param name="ExpiresAt">The instant after which the lease is no longer valid and may be re-acquired.</param>
/// <param name="Epoch">
/// The grant's epoch: a per-run counter the store mints, strictly increasing over every grant of that run's lease
/// and persisted with it, so it survives the holder handing the run back, the lease lapsing, and the process that
/// granted it exiting. Zero on a presented lease, meaning no epoch is claimed; the store never mints zero, so a
/// stated epoch and an unstated one are never confused.
/// </param>
public readonly record struct WorkflowLease(WorkflowRunId RunId, string Owner, string Token, DateTimeOffset ExpiresAt, long Epoch = 0);