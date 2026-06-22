// <copyright file="SecurityPolicySerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="ISecurityPolicyStore"/> implementations: every backend persists a
/// rule/binding as the same Corvus.Text.Json document, so the "write a new record" / "carry an existing record forward
/// under a new definition" steps live here once rather than being re-spelled per backend.
/// </summary>
/// <remarks>
/// <para>Two write shapes, chosen by what the backend's driver binds:</para>
/// <list type="bullet">
/// <item>The <c>byte[]</c> form (<see cref="SerializeNewRule"/> / <see cref="SerializeUpdatedRule"/>) for drivers whose
/// parameter is an exact array (Microsoft.Data.Sqlite, Mongo, NATS, Azure Table) and the in-memory reference. The store
/// binds the array and then returns a pooled document <em>copy</em> of it.</item>
/// <item>The <b>owned-document</b> form (<see cref="SerializeNewRuleOwned"/> / <see cref="SerializeUpdatedRuleOwned"/>)
/// for drivers that bind a <see cref="ReadOnlyMemory{T}"/> / stream (SqlServer, Postgres, MySql, Redis): the document is
/// serialized once into a pooled buffer the <em>returned</em> <see cref="ParsedJsonDocument{T}"/> owns, with the written
/// UTF-8 handed back for the bind — so the bytes are persisted and returned with no GC document array and no second copy.
/// The store binds the bytes during the (awaited) write and returns the document; it disposes the document on a write
/// failure.</item>
/// </list>
/// <para>The update variants take the existing record as the already-parsed model — each backend parses the stored bytes
/// at its own leaf the leanest way (non-copying over the driver's own array) — check the optimistic-concurrency etag, and
/// carry the immutable audit fields forward.</para>
/// </remarks>
public static class SecurityPolicySerialization
{
    /// <summary>Serializes a brand-new rule to owned JSON bytes (pooled scratch, no detached clone) for a byte[]-leaf driver.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="draft">The draft rule carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNewRule(string name, SecurityRuleDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (name, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Name, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityRuleDocument.WriteNew(writer, c.Name, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Serializes a brand-new rule into a pooled buffer the returned document owns (no GC array), for a driver
    /// that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes —
    /// <c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c> — during the (awaited) write, returns the document on
    /// success, and disposes it on a write failure.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="draft">The draft rule carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<SecurityRuleDocument> SerializeNewRuleDoc(string name, SecurityRuleDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<SecurityRuleDocument, (string Name, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (name, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Name, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityRuleDocument.WriteNew(writer, c.Name, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Checks the etag and serializes the carried-forward update to owned JSON bytes, for a byte[]-leaf driver.</summary>
    /// <param name="existing">The stored rule, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>rule</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft rule carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdatedRule(in SecurityRuleDocument existing, string kind, string id, WorkflowEtag expectedEtag, SecurityRuleDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        EnsureEtag(kind, id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityRuleDocument Current, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the carried-forward update into a pooled buffer the returned document owns
    /// (no GC array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's
    /// bytes (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="existing">The stored rule, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>rule</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft rule carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<SecurityRuleDocument> SerializeUpdatedRuleDoc(in SecurityRuleDocument existing, string kind, string id, WorkflowEtag expectedEtag, SecurityRuleDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        EnsureEtag(kind, id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<SecurityRuleDocument, (SecurityRuleDocument Current, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityRuleDocument Current, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Serializes a brand-new binding to owned JSON bytes (pooled scratch, no detached clone) for a byte[]-leaf driver.</summary>
    /// <param name="id">The assigned binding id.</param>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNewBinding(string id, SecurityBindingDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityBindingDocument.WriteNew(writer, c.Id, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Serializes a brand-new binding into a pooled buffer the returned document owns (no GC array), for a driver
    /// that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's bytes
    /// (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="id">The assigned binding id.</param>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<SecurityBindingDocument> SerializeNewBindingDoc(string id, SecurityBindingDocument draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<SecurityBindingDocument, (string Id, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (id, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityBindingDocument.WriteNew(writer, c.Id, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Checks the etag and serializes the carried-forward update to owned JSON bytes, for a byte[]-leaf driver.</summary>
    /// <param name="existing">The stored binding, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>binding</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft binding carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdatedBinding(in SecurityBindingDocument existing, string kind, string id, WorkflowEtag expectedEtag, SecurityBindingDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        EnsureEtag(kind, id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityBindingDocument Current, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the carried-forward update into a pooled buffer the returned document owns
    /// (no GC array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream. The store binds the document's
    /// bytes (<c>JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory</c>) during the write and returns the document.</summary>
    /// <param name="existing">The stored binding, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>binding</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft binding carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<SecurityBindingDocument> SerializeUpdatedBindingDoc(in SecurityBindingDocument existing, string kind, string id, WorkflowEtag expectedEtag, SecurityBindingDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        EnsureEtag(kind, id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<SecurityBindingDocument, (SecurityBindingDocument Current, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityBindingDocument Current, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored rule's etag NON-COPYING over the caller's owned bytes (no detached clone, no pooled copy) —
    /// for the delete concurrency check. The <c>byte[]</c> parameter keeps the method-group delegate conversion the
    /// backends pass to their generic delete helper.</summary>
    /// <param name="document">The stored rule's current UTF-8 JSON bytes (the driver's own array, alive for this call).</param>
    /// <returns>The rule's current etag (its <see cref="string"/> value outlives the parsed document).</returns>
    public static WorkflowEtag RuleEtagOf(byte[] document)
    {
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(document.AsMemory());
        return current.RootElement.EtagValue;
    }

    /// <summary>Reads a stored binding's etag NON-COPYING over the caller's owned bytes (no detached clone, no pooled copy) —
    /// for the delete concurrency check.</summary>
    /// <param name="document">The stored binding's current UTF-8 JSON bytes (the driver's own array, alive for this call).</param>
    /// <returns>The binding's current etag (its <see cref="string"/> value outlives the parsed document).</returns>
    public static WorkflowEtag BindingEtagOf(byte[] document)
    {
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(document.AsMemory());
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="SecurityPolicyConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="kind">The record kind for the conflict message.</param>
    /// <param name="id">The record identity for the conflict message.</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored record's current etag.</param>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }
}