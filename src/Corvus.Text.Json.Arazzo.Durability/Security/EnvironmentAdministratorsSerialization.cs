// <copyright file="EnvironmentAdministratorsSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="IEnvironmentAdministratorStore"/> implementations: every backend
/// persists an environment administration record as the same Corvus.Text.Json document, so the "write a new record" /
/// "carry an existing record forward under a new administrator set" steps live here once rather than per backend. Each
/// method builds the document through a pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns
/// the owned UTF-8 bytes the driver persists; the update variant carries the immutable creation audit forward. Mirrors
/// <see cref="WorkflowAdministratorsSerialization"/>.
/// </summary>
public static class EnvironmentAdministratorsSerialization
{
    /// <summary>Serializes a brand-new administration record to owned JSON bytes (pooled scratch, no detached clone).</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="administrators">The administrator identities (at least one).</param>
    /// <param name="actor">The materializing identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (environmentName, administrators, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Name, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => EnvironmentAdministrators.WriteNew(writer, c.Name, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes a brand-new administration record into a pooled buffer the returned document owns (no GC array),
    /// for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="administrators">The administrator identities (at least one).</param>
    /// <param name="actor">The materializing identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<EnvironmentAdministrators> SerializeNewDoc(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<EnvironmentAdministrators, (string Name, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (environmentName, administrators, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Name, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => EnvironmentAdministrators.WriteNew(writer, c.Name, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes the carried-forward update (new administrator set) to owned JSON bytes, for a byte[]-leaf driver.
    /// The backend has already parsed the existing record (and checked the etag via <c>existing.EtagValue</c>).</summary>
    /// <param name="existing">The stored record, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="administrators">The new administrator identities (at least one).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeUpdated(in EnvironmentAdministrators existing, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (Current: existing, administrators, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (EnvironmentAdministrators Current, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Administrators, c.Actor, c.At, c.Tag));

    /// <summary>Serializes the carried-forward update into a pooled buffer the returned document owns (no GC array), for a
    /// driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored record, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="administrators">The new administrator identities (at least one).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<EnvironmentAdministrators> SerializeUpdatedDoc(in EnvironmentAdministrators existing, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<EnvironmentAdministrators, (EnvironmentAdministrators Current, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, administrators, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (EnvironmentAdministrators Current, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> Administrators, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Administrators, c.Actor, c.At, c.Tag));
}