// <copyright file="EnvironmentRunnerAuthorizationTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>The lifecycle state of an <see cref="EnvironmentRunnerAuthorization"/> (design §5.5).</summary>
public enum RunnerAuthorizationStatus
{
    /// <summary>Awaiting an environment administrator's decision; the runner is not yet dispatchable for the environment.</summary>
    Pending,

    /// <summary>Authorized; the runner is dispatchable for the environment.</summary>
    Authorized,

    /// <summary>Revoked by an environment administrator; the runner is no longer dispatchable for the environment.</summary>
    Revoked,
}

/// <summary>A decision applied to an authorization (a status transition).</summary>
/// <param name="Status">The status to set — <see cref="RunnerAuthorizationStatus.Authorized"/> or <see cref="RunnerAuthorizationStatus.Revoked"/>.</param>
/// <param name="Reason">An optional note recorded with the decision.</param>
public readonly record struct RunnerAuthorizationDecision(
    RunnerAuthorizationStatus Status,
    string? Reason = null);

/// <summary>A filter over environment-runner authorizations (all criteria optional; an absent criterion matches anything).</summary>
/// <param name="Status">Only authorizations in this state.</param>
/// <param name="Environment">Only authorizations targeting this environment (ordinal); the environment queue. <see langword="null"/> matches anything.</param>
/// <param name="AdministeredEnvironments">The approver inbox (design §5.5/§7.8): only authorizations targeting one of these
/// environments — the set the caller administers, resolved server-side from the reverse administration index (a genuine
/// server-derived leaf). <see langword="null"/> matches anything; a caller administering nothing is short-circuited to an
/// empty page before the store, so the set is never empty here.</param>
public readonly record struct RunnerAuthorizationQuery(
    RunnerAuthorizationStatus? Status = null,
    string? Environment = null,
    IReadOnlyList<string>? AdministeredEnvironments = null)
{
    /// <summary>Whether a row's environment passes the approver-inbox filter: <see langword="true"/> when no administered set
    /// is constrained, or when the set contains <paramref name="rowEnvironment"/> (ordinal). The shared client-side
    /// membership test for stores that filter in memory (the in-memory store and the KV/table backends, which scan their
    /// keyset index and apply this per row).</summary>
    /// <param name="rowEnvironment">The candidate row's environment.</param>
    /// <returns><see langword="true"/> if the row is admitted by the administered-set filter.</returns>
    public bool MatchesAdministeredSet(string rowEnvironment)
    {
        if (this.AdministeredEnvironments is not { } administered)
        {
            return true;
        }

        foreach (string administeredEnvironment in administered)
        {
            if (string.Equals(administeredEnvironment, rowEnvironment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>The wire (persisted) names for <see cref="RunnerAuthorizationStatus"/>.</summary>
public static class RunnerAuthorizationStatusNames
{
    /// <summary>The persisted name for <see cref="RunnerAuthorizationStatus.Pending"/>.</summary>
    public const string Pending = "Pending";

    /// <summary>The persisted name for <see cref="RunnerAuthorizationStatus.Authorized"/>.</summary>
    public const string Authorized = "Authorized";

    /// <summary>The persisted name for <see cref="RunnerAuthorizationStatus.Revoked"/>.</summary>
    public const string Revoked = "Revoked";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(RunnerAuthorizationStatus status) => status switch
    {
        RunnerAuthorizationStatus.Pending => Pending,
        RunnerAuthorizationStatus.Authorized => Authorized,
        RunnerAuthorizationStatus.Revoked => Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Thrown when an environment-runner authorization's expected etag no longer matches (an optimistic-concurrency
/// conflict) — so two administrators cannot double-decide the same authorization.</summary>
public sealed class RunnerAuthorizationConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RunnerAuthorizationConflictException"/> class.</summary>
    /// <param name="environment">The conflicting authorization's environment.</param>
    /// <param name="runnerId">The conflicting authorization's runner id.</param>
    /// <param name="expectedEtag">The caller's expected etag.</param>
    public RunnerAuthorizationConflictException(string environment, string runnerId, WorkflowEtag expectedEtag)
        : base($"The runner authorization for environment '{environment}' and runner '{runnerId}' was modified concurrently (expected etag '{expectedEtag.Value}').")
    {
        this.Environment = environment;
        this.RunnerId = runnerId;
        this.ExpectedEtag = expectedEtag;
    }

    /// <summary>Gets the conflicting authorization's environment.</summary>
    public string Environment { get; }

    /// <summary>Gets the conflicting authorization's runner id.</summary>
    public string RunnerId { get; }

    /// <summary>Gets the caller's expected etag.</summary>
    public WorkflowEtag ExpectedEtag { get; }
}