// <copyright file="NativeBuildJobSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>
/// Shared, pooled serialization for the <see cref="INativeBuildJobStore"/> implementations: every backend persists a job as
/// the same Corvus.Text.Json document, so "enqueue a new job" / "carry an existing job forward under a claim or completion"
/// live here once rather than per backend. Each method builds through a pooled scratch buffer
/// (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver persists; the completion
/// variant parses the existing bytes through a pooled, disposed document to check the etag and carry immutable fields.
/// Mirrors the availability-request serialization.
/// </summary>
public static class NativeBuildJobSerialization
{
    /// <summary>Serializes a brand-new (Queued) job to owned JSON bytes.</summary>
    /// <param name="id">The assigned job id.</param>
    /// <param name="draft">The draft job carrying the target-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string id, NativeBuildJob draft, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, draft, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, NativeBuildJob Draft, DateTimeOffset At, WorkflowEtag Tag) c)
                => NativeBuildJob.WriteNew(writer, c.Id, c.Draft, c.At, c.Tag));

    /// <summary>Serializes a brand-new job into a pooled buffer the returned document owns (no GC array), for a driver that
    /// binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="id">The assigned job id.</param>
    /// <param name="draft">The draft job carrying the target-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<NativeBuildJob> SerializeNewDoc(string id, NativeBuildJob draft, DateTimeOffset createdAt, WorkflowEtag etag)

        // The generated Create() (via the entity's CreateNew) realises the stamped document in one pooled pass.
        => NativeBuildJob.CreateNew(id, draft, createdAt, etag);

    /// <summary>Serializes a claimed (Building) copy of an existing job to owned JSON bytes — the Queued -> Building
    /// transition. Not under optimistic concurrency: the store claims atomically under its own guard.</summary>
    /// <param name="existing">The stored job, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="claimedBy">The worker claiming the job.</param>
    /// <param name="startedAt">The instant the build started.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeClaimed(in NativeBuildJob existing, string claimedBy, DateTimeOffset startedAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (Current: existing, claimedBy, startedAt, etag),
            static (Utf8JsonWriter writer, in (NativeBuildJob Current, string ClaimedBy, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteClaimed(writer, c.ClaimedBy, c.At, c.Tag));

    /// <summary>Serializes a claimed (Building) copy of an existing job into a pooled buffer the returned document owns (no
    /// GC array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored job, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="claimedBy">The worker claiming the job.</param>
    /// <param name="startedAt">The instant the build started.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<NativeBuildJob> SerializeClaimedDoc(in NativeBuildJob existing, string claimedBy, DateTimeOffset startedAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<NativeBuildJob, (NativeBuildJob Current, string ClaimedBy, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, claimedBy, startedAt, etag),
            static (Utf8JsonWriter writer, in (NativeBuildJob Current, string ClaimedBy, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteClaimed(writer, c.ClaimedBy, c.At, c.Tag));

    /// <summary>Checks the etag and serializes the completed record to owned JSON bytes, for a byte[]-leaf driver — the
    /// Building -> Ready | Failed transition.</summary>
    /// <param name="existing">The stored job, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The job id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="completion">The completion to apply.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="NativeBuildJobConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeCompletion(in NativeBuildJob existing, string id, WorkflowEtag expectedEtag, NativeBuildJobCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, completion, completedAt, etag),
            static (Utf8JsonWriter writer, in (NativeBuildJob Current, NativeBuildJobCompletion Comp, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteCompletion(writer, c.Comp, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the completed record into a pooled buffer the returned document owns (no GC
    /// array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored job, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The job id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="completion">The completion to apply.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="NativeBuildJobConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<NativeBuildJob> SerializeCompletionDoc(in NativeBuildJob existing, string id, WorkflowEtag expectedEtag, NativeBuildJobCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<NativeBuildJob, (NativeBuildJob Current, NativeBuildJobCompletion Comp, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, completion, completedAt, etag),
            static (Utf8JsonWriter writer, in (NativeBuildJob Current, NativeBuildJobCompletion Comp, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteCompletion(writer, c.Comp, c.At, c.Tag));
    }

    /// <summary>Reads a stored job's etag NON-COPYING over the caller's owned bytes (no detached clone, no pooled copy) —
    /// for a completion concurrency check. The <c>byte[]</c> parameter keeps the method-group delegate conversion the
    /// backends pass to their generic helpers.</summary>
    /// <param name="document">The stored job's current UTF-8 JSON bytes (the driver's own array, alive for this call).</param>
    /// <returns>The job's current etag (its <see cref="string"/> value outlives the parsed document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(document.AsMemory());
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="NativeBuildJobConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="id">The job id (for the conflict message).</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored record's current etag.</param>
    /// <exception cref="NativeBuildJobConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new NativeBuildJobConflictException(id, expected);
        }
    }
}