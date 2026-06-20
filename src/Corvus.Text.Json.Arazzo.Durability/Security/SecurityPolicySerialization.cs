// <copyright file="SecurityPolicySerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="ISecurityPolicyStore"/> implementations: every backend persists a
/// rule/binding as the same Corvus.Text.Json document, so the "write a new record" / "carry an existing record forward
/// under a new definition" steps live here once rather than being re-spelled per backend. Each method builds the
/// document through a pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned
/// UTF-8 bytes the driver persists; the update variants parse the existing bytes through a pooled, disposed document to
/// check the optimistic-concurrency etag and carry the immutable audit fields forward.
/// </summary>
public static class SecurityPolicySerialization
{
    /// <summary>Serializes a brand-new rule to owned JSON bytes (pooled scratch, no detached clone).</summary>
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

    /// <summary>Parses the stored rule (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored rule's current UTF-8 JSON bytes.</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>rule</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft rule carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdatedRule(ReadOnlySpan<byte> existing, string kind, string id, WorkflowEtag expectedEtag, SecurityRuleDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<SecurityRuleDocument> current = PersistedJson.ToPooledDocument<SecurityRuleDocument>(existing);
        EnsureEtag(kind, id, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityRuleDocument Current, SecurityRuleDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Serializes a brand-new binding to owned JSON bytes (pooled scratch, no detached clone).</summary>
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

    /// <summary>Parses the stored binding (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored binding's current UTF-8 JSON bytes.</param>
    /// <param name="kind">The record kind for a conflict message (e.g. <c>binding</c>).</param>
    /// <param name="id">The record identity for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft binding carrying the new operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdatedBinding(ReadOnlySpan<byte> existing, string kind, string id, WorkflowEtag expectedEtag, SecurityBindingDocument draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<SecurityBindingDocument> current = PersistedJson.ToPooledDocument<SecurityBindingDocument>(existing);
        EnsureEtag(kind, id, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SecurityBindingDocument Current, SecurityBindingDocument Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored rule's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored rule's current UTF-8 JSON bytes.</param>
    /// <returns>The rule's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag RuleEtagOf(byte[] document)
    {
        using ParsedJsonDocument<SecurityRuleDocument> current = PersistedJson.ToPooledDocument<SecurityRuleDocument>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Reads a stored binding's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored binding's current UTF-8 JSON bytes.</param>
    /// <returns>The binding's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag BindingEtagOf(byte[] document)
    {
        using ParsedJsonDocument<SecurityBindingDocument> current = PersistedJson.ToPooledDocument<SecurityBindingDocument>(document);
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