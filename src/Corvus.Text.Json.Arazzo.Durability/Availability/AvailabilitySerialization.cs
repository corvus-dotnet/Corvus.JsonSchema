// <copyright file="AvailabilitySerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// Shared, pooled serialization for the <see cref="IAvailabilityStore"/> implementations: every backend persists an
/// availability entry as the same Corvus.Text.Json document, so the "write a new entry" step lives here once rather than
/// being re-spelled per backend. AvailabilityEntry has no mutable state, so there is no update variant — an entry is created
/// once and deleted to withdraw.
/// </summary>
public static class AvailabilitySerialization
{
    /// <summary>Serializes a brand-new availability entry to owned JSON bytes (pooled scratch, no detached clone) — the
    /// draft's key is carried bytes-to-bytes; the server-stamped audit/concurrency fields are added here.</summary>
    /// <param name="draft">The draft entry carrying the key as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(in AvailabilityEntry draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (AvailabilityEntry Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => AvailabilityEntry.WriteNew(writer, c.Draft, c.Actor, c.At, c.Tag));
}