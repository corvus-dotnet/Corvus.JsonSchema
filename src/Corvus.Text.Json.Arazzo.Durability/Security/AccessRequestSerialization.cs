// <copyright file="AccessRequestSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="IAccessRequestStore"/> implementations: every backend persists a
/// request as the same Corvus.Text.Json document, so "write a new request" / "carry an existing request forward under
/// a decision" live here once rather than per backend. Each method builds through a pooled scratch buffer
/// (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver persists; the decision
/// variant parses the existing bytes through a pooled, disposed document to check the etag and carry immutable fields.
/// </summary>
public static class AccessRequestSerialization
{
    /// <summary>Serializes a brand-new (Pending) request to owned JSON bytes.</summary>
    /// <param name="id">The assigned request id.</param>
    /// <param name="definition">The request content.</param>
    /// <param name="actor">The creating identity (the requester; audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string id, AccessRequestDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, definition, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, AccessRequestDefinition Def, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => AccessRequest.WriteNew(writer, c.Id, c.Def, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored request (pooled), checks the etag, and serializes the decided record.</summary>
    /// <param name="existing">The stored request's current UTF-8 JSON bytes.</param>
    /// <param name="id">The request id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="actor">The deciding identity (audit).</param>
    /// <param name="decidedAt">The decision timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="AccessRequestConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeDecision(ReadOnlySpan<byte> existing, string id, WorkflowEtag expectedEtag, AccessRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<AccessRequest> current = PersistedJson.ToPooledDocument<AccessRequest>(existing);
        EnsureEtag(id, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, decision, actor, decidedAt, etag),
            static (Utf8JsonWriter writer, in (AccessRequest Current, AccessRequestDecision Dec, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteDecision(writer, c.Dec, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored request's etag through a pooled, disposed document — for a delete/decision concurrency check.</summary>
    /// <param name="document">The stored request's current UTF-8 JSON bytes.</param>
    /// <returns>The request's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<AccessRequest> current = PersistedJson.ToPooledDocument<AccessRequest>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="AccessRequestConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="id">The request id (for the conflict message).</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored record's current etag.</param>
    /// <exception cref="AccessRequestConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new AccessRequestConflictException(id, expected);
        }
    }
}