// <copyright file="SourceCredentialSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Shared, pooled serialization for the <see cref="ISourceCredentialStore"/> implementations: every backend persists a
/// binding as the same Corvus.Text.Json document, so the "write a new binding" / "carry an existing binding forward
/// under a new definition" steps live here once rather than being re-spelled per backend. Each method builds the
/// document through a pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8
/// bytes the driver persists; the update variant parses the existing bytes through a pooled, disposed document to check
/// the optimistic-concurrency etag and carry the immutable identity/audit fields forward.
/// </summary>
public static class SourceCredentialSerialization
{
    /// <summary>Serializes a brand-new binding to owned JSON bytes (pooled scratch, no detached clone) — the draft's
    /// operator content is carried bytes-to-bytes; id and the server-stamped audit/concurrency fields are added here.</summary>
    /// <param name="id">The assigned binding id.</param>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string id, in SourceCredentialBinding draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, SourceCredentialBinding Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SourceCredentialBinding.WriteNew(writer, c.Draft, c.Id, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored binding (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored binding's current UTF-8 JSON bytes.</param>
    /// <param name="key">The binding key (<c>sourceName@environment</c>) for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdated(ReadOnlySpan<byte> existing, string key, WorkflowEtag expectedEtag, in SourceCredentialBinding draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(existing);
        EnsureEtag(key, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (SourceCredentialBinding Current, SourceCredentialBinding Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored binding's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored binding's current UTF-8 JSON bytes.</param>
    /// <returns>The binding's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="SourceCredentialConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="key">The binding key for the conflict message.</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored binding's current etag.</param>
    /// <exception cref="SourceCredentialConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string key, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SourceCredentialConflictException(key, expected);
        }
    }
}