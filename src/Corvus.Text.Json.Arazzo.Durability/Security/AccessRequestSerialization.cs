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
    /// <param name="draft">The draft request carrying the create-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (the requester; audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string id, AccessRequest draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, AccessRequest Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => AccessRequest.WriteNew(writer, c.Id, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Serializes a brand-new request into a pooled buffer the returned document owns (no GC array), for a driver
    /// that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes
    /// (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="id">The assigned request id.</param>
    /// <param name="draft">The draft request carrying the create-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (the requester; audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<AccessRequest> SerializeNewDoc(string id, AccessRequest draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<AccessRequest, (string Id, AccessRequest Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (id, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, AccessRequest Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => AccessRequest.WriteNew(writer, c.Id, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Checks the etag and serializes the decided record to owned JSON bytes, for a byte[]-leaf driver.</summary>
    /// <param name="existing">The stored request, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The request id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="actor">The deciding identity (audit).</param>
    /// <param name="decidedAt">The decision timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="AccessRequestConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeDecision(in AccessRequest existing, string id, WorkflowEtag expectedEtag, AccessRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, decision, actor, decidedAt, etag),
            static (Utf8JsonWriter writer, in (AccessRequest Current, AccessRequestDecision Dec, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteDecision(writer, c.Dec, c.Actor, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the decided record into a pooled buffer the returned document owns (no GC
    /// array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes
    /// (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="existing">The stored request, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The request id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="actor">The deciding identity (audit).</param>
    /// <param name="decidedAt">The decision timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="AccessRequestConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<AccessRequest> SerializeDecisionDoc(in AccessRequest existing, string id, WorkflowEtag expectedEtag, AccessRequestDecision decision, string actor, DateTimeOffset decidedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<AccessRequest, (AccessRequest Current, AccessRequestDecision Dec, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, decision, actor, decidedAt, etag),
            static (Utf8JsonWriter writer, in (AccessRequest Current, AccessRequestDecision Dec, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteDecision(writer, c.Dec, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored request's etag NON-COPYING over the caller's owned bytes (no detached clone, no pooled copy)
    /// — for a delete/decision concurrency check. The <c>byte[]</c> parameter keeps the method-group delegate conversion
    /// the backends pass to their generic helpers.</summary>
    /// <param name="document">The stored request's current UTF-8 JSON bytes (the driver's own array, alive for this call).</param>
    /// <returns>The request's current etag (its <see cref="string"/> value outlives the parsed document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(document.AsMemory());
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