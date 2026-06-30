// <copyright file="EnvironmentRunnerAuthorizationSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// Shared, pooled serialization for the <see cref="IEnvironmentRunnerAuthorizationStore"/> implementations: every backend
/// persists an authorization as the same Corvus.Text.Json document, so "write a new Pending authorization" / "carry an
/// existing authorization forward under a decision" live here once rather than per backend. Each method builds through a
/// pooled scratch buffer (<see cref="PersistedJson.ToArray{TContext}"/>) and returns the owned UTF-8 bytes the driver
/// persists; the decision variant parses the existing bytes through a pooled, disposed document to check the etag and carry
/// immutable fields. Mirrors <see cref="Availability.AvailabilityRequestSerialization"/>.
/// </summary>
public static class EnvironmentRunnerAuthorizationSerialization
{
    /// <summary>Serializes a brand-new (Pending) authorization to owned JSON bytes (used by
    /// <see cref="IEnvironmentRunnerAuthorizationStore.EnsurePendingAsync"/>): writes the content keys plus the stamped
    /// status/server audit fields directly, without an intermediate draft.</summary>
    /// <param name="environment">The environment the runner asks to serve.</param>
    /// <param name="runnerId">The runner the authorization applies to.</param>
    /// <param name="actor">The creating identity (the runner; audit).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializePending(string environment, string runnerId, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft = EnvironmentRunnerAuthorization.Draft(environment, runnerId);
        return PersistedJson.ToArray(
            (Draft: draft.RootElement, actor, createdAt, etag),
            static (Utf8JsonWriter writer, in (EnvironmentRunnerAuthorization Draft, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => EnvironmentRunnerAuthorization.WriteNew(writer, c.Draft, c.Actor, c.At, c.Tag));
    }

    /// <summary>Checks the etag and serializes the decided record to owned JSON bytes, for a byte[]-leaf driver.</summary>
    /// <param name="current">The stored authorization, already parsed by the backend leaf (read synchronously here).</param>
    /// <param name="decision">The decision to apply.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> overwrites unconditionally).</param>
    /// <param name="actor">The deciding identity (an environment administrator; audit).</param>
    /// <param name="decidedAt">The decision timestamp.</param>
    /// <param name="newEtag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    /// <exception cref="RunnerAuthorizationConflictException">The expected etag no longer matches.</exception>
    public static byte[] SerializeDecision(in EnvironmentRunnerAuthorization current, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, DateTimeOffset decidedAt, WorkflowEtag newEtag)
    {
        EnsureEtag(current.EnvironmentValue, current.RunnerIdValue, expectedEtag, current.EtagValue);
        return PersistedJson.ToArray(
            (Current: current, decision, actor, decidedAt, newEtag),
            static (Utf8JsonWriter writer, in (EnvironmentRunnerAuthorization Current, RunnerAuthorizationDecision Dec, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => c.Current.WriteDecision(writer, c.Dec, c.Actor, c.At, c.Tag));
    }

    /// <summary>Throws <see cref="RunnerAuthorizationConflictException"/> when a non-<see cref="WorkflowEtag.None"/> expected etag no longer matches.</summary>
    /// <param name="environment">The authorization's environment (for the conflict message).</param>
    /// <param name="runnerId">The authorization's runner id (for the conflict message).</param>
    /// <param name="expected">The caller's expected etag.</param>
    /// <param name="actual">The stored record's current etag.</param>
    /// <exception cref="RunnerAuthorizationConflictException">The expected etag no longer matches.</exception>
    public static void EnsureEtag(string environment, string runnerId, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new RunnerAuthorizationConflictException(environment, runnerId, expected);
        }
    }
}