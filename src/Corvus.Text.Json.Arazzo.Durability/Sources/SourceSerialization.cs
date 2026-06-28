// <copyright file="SourceSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// Shared, pooled serialization for the <see cref="ISourceStore"/> implementations: every backend persists a source as
/// the same Corvus.Text.Json document, so the "write a new source" / "carry an existing source forward under updated
/// metadata" steps live here once rather than being re-spelled per backend. Each method builds the document through a
/// pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver
/// persists; the update variant parses the existing bytes through a pooled, disposed document to check the
/// optimistic-concurrency etag and carry the immutable identity/audit fields (and, when the update omits it, the
/// document) forward.
/// </summary>
public static class SourceSerialization
{
    /// <summary>Serializes a brand-new source to owned JSON bytes (pooled scratch, no detached clone) — the draft's
    /// operator content (the document included) is carried bytes-to-bytes; the server-stamped audit/concurrency fields
    /// are added here.</summary>
    /// <param name="draft">The draft source carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(in RegisteredSource draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (RegisteredSource Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => RegisteredSource.WriteNew(writer, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored source (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored source's current UTF-8 JSON bytes.</param>
    /// <param name="name">The source name for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="SourceConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdated(ReadOnlySpan<byte> existing, string name, WorkflowEtag expectedEtag, in RegisteredSource draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<RegisteredSource> current = PersistedJson.ToPooledDocument<RegisteredSource>(existing);
        EnsureEtag(name, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (RegisteredSource Current, RegisteredSource Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored source's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored source's current UTF-8 JSON bytes.</param>
    /// <returns>The source's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<RegisteredSource> current = PersistedJson.ToPooledDocument<RegisteredSource>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="SourceConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="name">The source name for the conflict message.</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored source's current etag.</param>
    /// <exception cref="SourceConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string name, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SourceConflictException(name, expected);
        }
    }
}