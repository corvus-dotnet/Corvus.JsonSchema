// <copyright file="WorkflowDeploymentSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Publishing;

/// <summary>
/// Shared, pooled serialization for the <see cref="IWorkflowDeploymentStore"/> implementations: every backend persists a
/// deployment as the same Corvus.Text.Json document, so "enqueue a new deployment" / "carry an existing deployment forward
/// under a claim or completion" live here once rather than per backend. Each method builds through a pooled scratch buffer
/// (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver persists; the completion
/// variant parses the existing bytes through a pooled, disposed document to check the etag and carry immutable fields.
/// Mirrors the availability-request serialization.
/// </summary>
public static class WorkflowDeploymentSerialization
{
    /// <summary>Serializes a brand-new (Queued) deployment to owned JSON bytes.</summary>
    /// <param name="id">The assigned deployment id.</param>
    /// <param name="draft">The draft deployment carrying the target-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeNew(string id, WorkflowDeployment draft, DateTimeOffset createdAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (id, draft, createdAt, etag),
            static (Utf8JsonWriter writer, in (string Id, WorkflowDeployment Draft, DateTimeOffset At, WorkflowEtag Tag) c)
                => WorkflowDeployment.WriteNew(writer, c.Id, c.Draft, c.At, c.Tag));

    /// <summary>Serializes a brand-new deployment into a pooled buffer the returned document owns (no GC array), for a driver
    /// that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="id">The assigned deployment id.</param>
    /// <param name="draft">The draft deployment carrying the target-content as JSON values (read bytes-to-bytes).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<WorkflowDeployment> SerializeNewDoc(string id, WorkflowDeployment draft, DateTimeOffset createdAt, WorkflowEtag etag)

        // The generated Create() (via the entity's CreateNew) realises the stamped document in one pooled pass.
        => WorkflowDeployment.CreateNew(id, draft, createdAt, etag);

    /// <summary>Serializes a claimed (Deploying) copy of an existing deployment to owned JSON bytes — the Queued -> Deploying
    /// transition. Not under optimistic concurrency: the store claims atomically under its own guard.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="claimedBy">The worker claiming the deployment.</param>
    /// <param name="startedAt">The instant the deploy started.</param>
    /// <param name="leaseExpiresAt">The instant the claiming worker's advisory lease expires (ADR 0056).</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeClaimed(in WorkflowDeployment existing, string claimedBy, DateTimeOffset startedAt, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (Current: existing, claimedBy, startedAt, leaseExpiresAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, string ClaimedBy, DateTimeOffset At, DateTimeOffset Lease, WorkflowEtag Tag) c)
                => c.Current.WriteClaimed(writer, c.ClaimedBy, c.At, c.Lease, c.Tag));

    /// <summary>Serializes a claimed (Deploying) copy of an existing deployment into a pooled buffer the returned document
    /// owns (no GC array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="claimedBy">The worker claiming the deployment.</param>
    /// <param name="startedAt">The instant the deploy started.</param>
    /// <param name="leaseExpiresAt">The instant the claiming worker's advisory lease expires (ADR 0056).</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    public static ParsedJsonDocument<WorkflowDeployment> SerializeClaimedDoc(in WorkflowDeployment existing, string claimedBy, DateTimeOffset startedAt, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
        => PersistedJson.ToPooledDocument<WorkflowDeployment, (WorkflowDeployment Current, string ClaimedBy, DateTimeOffset At, DateTimeOffset Lease, WorkflowEtag Tag)>(
            (existing, claimedBy, startedAt, leaseExpiresAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, string ClaimedBy, DateTimeOffset At, DateTimeOffset Lease, WorkflowEtag Tag) c)
                => c.Current.WriteClaimed(writer, c.ClaimedBy, c.At, c.Lease, c.Tag));

    /// <summary>Checks the etag and serializes the completed record to owned JSON bytes, for a byte[]-leaf driver — the
    /// Deploying -> Deployed | Failed transition.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The deployment id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="completion">The completion to apply.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeCompletion(in WorkflowDeployment existing, string id, WorkflowEtag expectedEtag, WorkflowDeploymentCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, completion, completedAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, WorkflowDeploymentCompletion Comp, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteCompletion(writer, c.Comp, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the completed record into a pooled buffer the returned document owns (no GC
    /// array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The deployment id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="completion">The completion to apply.</param>
    /// <param name="completedAt">The completion timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<WorkflowDeployment> SerializeCompletionDoc(in WorkflowDeployment existing, string id, WorkflowEtag expectedEtag, WorkflowDeploymentCompletion completion, DateTimeOffset completedAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<WorkflowDeployment, (WorkflowDeployment Current, WorkflowDeploymentCompletion Comp, DateTimeOffset At, WorkflowEtag Tag)>(
            (existing, completion, completedAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, WorkflowDeploymentCompletion Comp, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteCompletion(writer, c.Comp, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes a lease-renewed copy of a Deploying deployment to owned JSON bytes — the
    /// heartbeat that keeps a running deploy's lease live (ADR 0056). The etag is the ownership token: a stale expected etag
    /// means the deployment was reclaimed, so the renewal conflicts.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The deployment id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> renews unconditionally).</param>
    /// <param name="leaseExpiresAt">The new instant the lease expires.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeRenewal(in WorkflowDeployment existing, string id, WorkflowEtag expectedEtag, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToArray(
            (Current: existing, leaseExpiresAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, DateTimeOffset Lease, WorkflowEtag Tag) c)
                => c.Current.WriteRenewedLease(writer, c.Lease, c.Tag));
    }

    /// <summary>Checks the etag and serializes a lease-renewed copy of a Deploying deployment into a pooled buffer the
    /// returned document owns (no GC array), for a driver that binds a <see cref="ReadOnlyMemory{T}"/> / stream.</summary>
    /// <param name="existing">The stored deployment, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="id">The deployment id (for a conflict message).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> renews unconditionally).</param>
    /// <param name="leaseExpiresAt">The new instant the lease expires.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The pooled document that owns the persisted bytes.</returns>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    public static ParsedJsonDocument<WorkflowDeployment> SerializeRenewalDoc(in WorkflowDeployment existing, string id, WorkflowEtag expectedEtag, DateTimeOffset leaseExpiresAt, WorkflowEtag etag)
    {
        EnsureEtag(id, expectedEtag, existing.EtagValue);
        return PersistedJson.ToPooledDocument<WorkflowDeployment, (WorkflowDeployment Current, DateTimeOffset Lease, WorkflowEtag Tag)>(
            (existing, leaseExpiresAt, etag),
            static (Utf8JsonWriter writer, in (WorkflowDeployment Current, DateTimeOffset Lease, WorkflowEtag Tag) c)
                => c.Current.WriteRenewedLease(writer, c.Lease, c.Tag));
    }

    /// <summary>Reads a stored deployment's etag NON-COPYING over the caller's owned bytes (no detached clone, no pooled copy)
    /// — for a completion concurrency check. The <c>byte[]</c> parameter keeps the method-group delegate conversion the
    /// backends pass to their generic helpers.</summary>
    /// <param name="document">The stored deployment's current UTF-8 JSON bytes (the driver's own array, alive for this call).</param>
    /// <returns>The deployment's current etag (its <see cref="string"/> value outlives the parsed document).</returns>
    public static WorkflowEtag EtagOf(byte[] document)
    {
        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(document.AsMemory());
        return current.RootElement.EtagValue;
    }

    /// <summary>Throws <see cref="WorkflowDeploymentConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="id">The deployment id (for the conflict message).</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored record's current etag.</param>
    /// <exception cref="WorkflowDeploymentConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new WorkflowDeploymentConflictException(id, expected);
        }
    }
}