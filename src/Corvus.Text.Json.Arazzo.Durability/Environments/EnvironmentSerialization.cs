// <copyright file="EnvironmentSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Shared, pooled serialization for the <see cref="IEnvironmentStore"/> implementations: every backend persists an
/// environment as the same Corvus.Text.Json document, so the "write a new environment" / "carry an existing environment
/// forward under updated metadata" steps live here once rather than being re-spelled per backend. Each method builds the
/// document through a pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8
/// bytes the driver persists; the update variant parses the existing bytes through a pooled, disposed document to check
/// the optimistic-concurrency etag and carry the immutable identity/audit fields forward.
/// </summary>
public static class EnvironmentSerialization
{
    /// <summary>Serializes a brand-new environment to owned JSON bytes (pooled scratch, no detached clone) — the draft's
    /// operator content is carried bytes-to-bytes; the server-stamped audit/concurrency fields are added here.</summary>
    /// <param name="draft">The draft environment carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The creating identity (audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(in Environment draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (draft, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (Environment Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => Environment.WriteNew(writer, c.Draft, c.Actor, c.At, c.Tag));

    /// <summary>Parses the stored environment (pooled), checks the etag, and serializes the carried-forward update.</summary>
    /// <param name="existing">The stored environment's current UTF-8 JSON bytes.</param>
    /// <param name="name">The environment name for a conflict message.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The updating identity (audit).</param>
    /// <param name="updatedAt">The update timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="EnvironmentConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeUpdated(ReadOnlySpan<byte> existing, string name, WorkflowEtag expectedEtag, in Environment draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(existing);
        EnsureEtag(name, expectedEtag, current.RootElement.EtagValue);
        return PersistedJson.ToArray(
            (Current: current.RootElement, draft, actor, updatedAt, etag),
            static (Utf8JsonWriter writer, in (Environment Current, Environment Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteUpdated(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Reads a stored environment's etag through a pooled, disposed document (no detached clone) — for the delete concurrency check.</summary>
    /// <param name="document">The stored environment's current UTF-8 JSON bytes.</param>
    /// <returns>The environment's current etag (its <see cref="string"/> value outlives the pooled document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<Environment> current = PersistedJson.ToPooledDocument<Environment>(document);
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="EnvironmentConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="name">The environment name for the conflict message.</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored environment's current etag.</param>
    /// <exception cref="EnvironmentConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string name, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new EnvironmentConflictException(name, expected);
        }
    }
}