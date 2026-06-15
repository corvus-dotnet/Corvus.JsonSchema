// <copyright file="WorkflowAdministratorsSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="IWorkflowAdministratorStore"/> implementations: every backend persists
/// an administration record as the same Corvus.Text.Json document, so the "write a new record" / "carry an existing record
/// forward under a new administrator set" steps live here once rather than per backend. Each method builds the document through
/// a pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver
/// persists; the update variant parses the existing bytes (pooled, disposed) to carry the immutable creation audit
/// forward.
/// </summary>
public static class WorkflowAdministratorsSerialization
{
    /// <summary>Serializes a brand-new administration record to owned JSON bytes (pooled scratch, no detached clone).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="administrators">The administrator identities (at least one).</param>
    /// <param name="actor">The materializing identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (baseWorkflowId, administrators, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, IReadOnlyList<SecurityTagSet> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => WorkflowAdministrators.WriteNew(writer, c.Id, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored record (pooled) and serializes the carried-forward update with a new administrator set.</summary>
    /// <param name="existing">The stored record's current UTF-8 JSON bytes.</param>
    /// <param name="administrators">The new administrator identities (at least one).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeUpdated(ReadOnlySpan<byte> existing, IReadOnlyList<SecurityTagSet> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<WorkflowAdministrators> current = PersistedJson.ToPooledDocument<WorkflowAdministrators>(existing);
        return PersistedJson.ToArray(
            (Current: current.RootElement, administrators, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowAdministrators Current, IReadOnlyList<SecurityTagSet> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Administrators, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored record's etag through a pooled, disposed document (no detached clone).</summary>
    /// <param name="document">The stored record's current UTF-8 JSON bytes.</param>
    /// <returns>The record's current etag.</returns>
    public static WorkflowEtag EtagOf(ReadOnlySpan<byte> document)
    {
        using ParsedJsonDocument<WorkflowAdministrators> current = PersistedJson.ToPooledDocument<WorkflowAdministrators>(document);
        return current.RootElement.EtagValue;
    }
}