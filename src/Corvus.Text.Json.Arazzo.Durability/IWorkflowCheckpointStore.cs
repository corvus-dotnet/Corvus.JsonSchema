// <copyright file="IWorkflowCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The opaque checkpoint read/write core of the durability store: key/value by the run's
/// <see cref="WorkflowRunAddress"/> — the <c>(environment, runId)</c> composite that is the run's primary key at
/// every ingress and in every backend (ADR 0065 decision 9) — with optimistic
/// concurrency via an etag, and nothing more. This is the whole store surface a durable
/// <see cref="IWorkflowRun"/> needs — it only ever loads its checkpoint and saves it — so a checkpoint-only
/// host implements just this, not the leasing and deletion a dispatching runner adds in
/// <see cref="IWorkflowStateStore"/>. The serverless function-side store (which proxies these two operations
/// to the dispatching runner over HTTP so a baked function binds no database SDK) implements exactly this.
/// </summary>
/// <remarks>
/// A backend never parses the checkpoint — it stores the opaque bytes by address at an etag and indexes the few
/// projected <see cref="WorkflowRunIndexEntry"/> fields — so each adapter stays thin. Keying by run id alone
/// would make a caller-chosen id a cross-tenant handle: the same run id in two environments names two distinct
/// runs, and neither is visible from, nor collides with, the other.
/// </remarks>
public interface IWorkflowCheckpointStore
{
    /// <summary>
    /// Creates or updates a run's checkpoint under optimistic concurrency.
    /// </summary>
    /// <param name="address">The run's <c>(environment, runId)</c> address.</param>
    /// <param name="checkpointUtf8">The opaque serialized checkpoint document (UTF-8 JSON).</param>
    /// <param name="index">The projected fields to index alongside the bytes.</param>
    /// <param name="expected">
    /// The etag the caller last read; pass <see cref="WorkflowEtag.None"/> to create a run that must not yet
    /// exist. The save fails with <see cref="WorkflowConflictException"/> if the store's current etag differs.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The new etag to use as <paramref name="expected"/> on the next save.</returns>
    /// <exception cref="WorkflowConflictException">The stored etag did not match <paramref name="expected"/>. The
    /// collision is evaluated only at the addressed environment: a run holding the same id in another environment
    /// is invisible here, so neither collision branch is an existence oracle over another tenant's runs.</exception>
    ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunAddress address,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken);

    /// <summary>Loads a run's checkpoint and the etag it was read at.</summary>
    /// <param name="address">The run's <c>(environment, runId)</c> address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The checkpoint, or <see langword="null"/> if no run exists at that address.</returns>
    ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken);
}