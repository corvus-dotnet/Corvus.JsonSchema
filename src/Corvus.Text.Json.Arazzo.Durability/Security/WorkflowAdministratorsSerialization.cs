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
    public static byte[] SerializeNew(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (baseWorkflowId, administrators, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => WorkflowAdministrators.WriteNew(writer, c.Id, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes a brand-new administration record into a pooled buffer the returned document owns (no GC array),
    /// for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes
    /// (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="administrators">The administrator identities (at least one).</param>
    /// <param name="actor">The materializing identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<WorkflowAdministrators> SerializeNewDoc(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<WorkflowAdministrators, (string Id, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (baseWorkflowId, administrators, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => WorkflowAdministrators.WriteNew(writer, c.Id, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes the carried-forward update (new administrator set) to owned JSON bytes, for a byte[]-leaf driver.
    /// The backend has already parsed the existing record (and checked the etag via <c>existing.EtagValue</c>).</summary>
    /// <param name="existing">The stored record, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="administrators">The new administrator identities (at least one).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeUpdated(in WorkflowAdministrators existing, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (Current: existing, administrators, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowAdministrators Current, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes the carried-forward update into a pooled buffer the returned document owns (no GC array), for a
    /// driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes
    /// (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="existing">The stored record, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="administrators">The new administrator identities (at least one).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<WorkflowAdministrators> SerializeUpdatedDoc(in WorkflowAdministrators existing, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<WorkflowAdministrators, (WorkflowAdministrators Current, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, administrators, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowAdministrators Current, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Administrators, c.Actor, c.At, c.Tag));
}