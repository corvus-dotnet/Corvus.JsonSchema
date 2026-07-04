// <copyright file="WorkspaceWorkflowSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// Shared, pooled serialization for the <see cref="IWorkspaceWorkflowStore"/> implementations: every backend persists a working copy as
/// the same Corvus.Text.Json document, so the "write a new working copy" / "carry an existing working copy forward under updated
/// metadata" steps live here once rather than being re-spelled per backend. Each method builds the document through a
/// pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver
/// persists; the update variant parses the existing bytes through a pooled, disposed document to check the
/// optimistic-concurrency etag and carry the immutable identity/audit fields (and, when the update omits it, the
/// document) forward.
/// </summary>
public static class WorkspaceWorkflowSerialization
{
    /// <summary>Serializes a brand-new working copy to owned JSON bytes (pooled scratch, no detached clone) — the draft's
    /// operator content (the document included) is carried bytes-to-bytes; the server-stamped audit/concurrency fields
    /// are added here.</summary>
    /// <param name="draft">The draft working copy carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="id">The server-minted working-copy id.</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(in WorkspaceWorkflow draft, string id, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (draft, id, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (WorkspaceWorkflow Draft, string Id, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => WorkspaceWorkflow.WriteNew(writer, c.Draft, c.Id, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored working copy (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored working copy's current UTF-8 JSON bytes.</param>
    /// <param name="id">The working-copy id for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="WorkspaceWorkflowConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdated(ReadOnlySpan<byte> existing, string id, WorkflowEtag expectedEtag, in WorkspaceWorkflow draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<WorkspaceWorkflow> current = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(existing);
        EnsureEtag(id, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (WorkspaceWorkflow Current, WorkspaceWorkflow Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored working copy's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored working copy's current UTF-8 JSON bytes.</param>
    /// <returns>The working copy's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<WorkspaceWorkflow> current = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="WorkspaceWorkflowConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="id">The working-copy id for the conflict message.</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored working copy's current etag.</param>
    /// <exception cref="WorkspaceWorkflowConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new WorkspaceWorkflowConflictException(id, expected);
        }
    }
}