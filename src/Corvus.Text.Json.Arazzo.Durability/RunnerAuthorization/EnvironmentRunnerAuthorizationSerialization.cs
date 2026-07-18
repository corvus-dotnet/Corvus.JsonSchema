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
    /// <param name="principal">The trusted machine principal that authenticated the registration (design §16.4), stamped as
    /// the record's <c>principal</c> when non-<see langword="null"/>; <see langword="null"/> for an administrator
    /// pre-authorizing a runnerId (no runner has proven ownership yet).</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializePending(string environment, string runnerId, string actor, string? principal, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft = EnvironmentRunnerAuthorization.Draft(environment, runnerId);
        return PersistedJson.ToArray(
            (Draft: draft.RootElement, actor, principal, createdAt, etag),
            static (Utf8JsonWriter writer, in (EnvironmentRunnerAuthorization Draft, string Actor, string? Principal, DateTimeOffset At, WorkflowEtag Tag) c)
                => EnvironmentRunnerAuthorization.WriteNew(writer, c.Draft, c.Actor, c.Principal, c.At, c.Tag));
    }

    /// <summary>Classifies what a store must do with an <em>already-existing</em> authorization when a runner (re-)registers
    /// presenting <paramref name="principal"/> (design §16.4). Entirely string-free (no principal string is realised from the
    /// stored document): the common steady-state re-registration compares the bound principal's bytes and returns
    /// <see cref="RegistrationOutcome.Unchanged"/> without touching the store.</summary>
    /// <param name="current">The stored authorization (already parsed by the backend).</param>
    /// <param name="principal">The machine principal the registration presents, or <see langword="null"/> for a path that does
    /// not carry one (an administrator pre-authorizing, or a caller that does not bind identity) — always
    /// <see cref="RegistrationOutcome.Unchanged"/>.</param>
    /// <returns><see cref="RegistrationOutcome.Unchanged"/> to return the existing record as-is (no write); or
    /// <see cref="RegistrationOutcome.PrincipalConflict"/> when a <em>different</em> principal is already bound — the store
    /// must throw <see cref="RunnerPrincipalConflictException"/>.</returns>
    /// <remarks>A row with no bound principal (an administrator pre-authorized it by runnerId) is left unchanged even when a
    /// principal is presented: pre-authorization is the administrator's deliberate name-based allow-listing, and binding is
    /// reserved for the runner's own first (self-registering) creation. So every existing-row path stays read-only.</remarks>
    public static RegistrationOutcome ClassifyRegistration(in EnvironmentRunnerAuthorization current, string? principal)
    {
        if (principal is null || !current.HasPrincipal || current.PrincipalEquals(principal))
        {
            return RegistrationOutcome.Unchanged;
        }

        return RegistrationOutcome.PrincipalConflict;
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