// <copyright file="IWorkflowCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The opaque checkpoint read/write core of the durability store: key/value by run id with optimistic
/// concurrency via an etag, and nothing more. This is the whole store surface a durable
/// <see cref="IWorkflowRun"/> needs — it only ever loads its checkpoint and saves it — so a checkpoint-only
/// host implements just this, not the leasing and deletion a dispatching runner adds in
/// <see cref="IWorkflowStateStore"/>. The serverless function-side store (which proxies these two operations
/// to the dispatching runner over HTTP so a baked function binds no database SDK) implements exactly this.
/// </summary>
/// <remarks>
/// A backend never parses the checkpoint — it stores the opaque bytes by id at an etag and indexes the few
/// projected <see cref="WorkflowRunIndexEntry"/> fields — so each adapter stays thin.
/// </remarks>
public interface IWorkflowCheckpointStore
{
    /// <summary>
    /// Creates or updates a run's checkpoint under optimistic concurrency.
    /// </summary>
    /// <param name="id">The run id.</param>
    /// <param name="checkpointUtf8">The opaque serialized checkpoint document (UTF-8 JSON).</param>
    /// <param name="index">The projected fields to index alongside the bytes.</param>
    /// <param name="expected">
    /// The etag the caller last read; pass <see cref="WorkflowEtag.None"/> to create a run that must not yet
    /// exist. The save fails with <see cref="WorkflowConflictException"/> if the store's current etag differs.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The new etag to use as <paramref name="expected"/> on the next save.</returns>
    /// <exception cref="WorkflowConflictException">The stored etag did not match <paramref name="expected"/>.</exception>
    ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken);

    /// <summary>Loads a run's checkpoint and the etag it was read at.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The checkpoint, or <see langword="null"/> if no run with that id exists.</returns>
    ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken);
}