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

    /// <summary>Quarantined by an environment administrator: temporarily excluded from new dispatch (a faulted runner) while
    /// its in-flight runs drain; reinstated to <see cref="Authorized"/> without re-registration. Not dispatchable.</summary>
    Quarantined,

    /// <summary>Revoked by an environment administrator (a compromised runner): permanently removed from dispatch and its
    /// in-flight work fenced. Returning to service requires a deliberate re-authorization. Not dispatchable.</summary>
    Revoked,
}

/// <summary>What a store must do with an already-existing authorization when a runner (re-)registers presenting a machine
/// principal (design §16.4) — the result of <see cref="EnvironmentRunnerAuthorizationSerialization.ClassifyRegistration"/>.
/// Every outcome is decided string-free and neither writes to the store; the store acts on the result.</summary>
public enum RegistrationOutcome
{
    /// <summary>Return the existing record unchanged (no write): the presented principal matches the bound one (steady-state
    /// re-registration), no principal is presented, or the row carries no bound principal (an administrator pre-authorized it).</summary>
    Unchanged,

    /// <summary>A different machine principal is already bound to this runnerId: the store must throw
    /// <see cref="RunnerPrincipalConflictException"/> (the registration is refused, mapped to a 409).</summary>
    PrincipalConflict,
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
    /// <summary>Whether a row passes the approver-inbox filter: <see langword="true"/> when no administered set is
    /// constrained, or when the set contains the row's environment. String-free — the candidate environment strings'
    /// bytes are compared against the row's JSON value (no environment string is realised from the document). The shared
    /// client-side membership test for stores that filter in memory (the in-memory store and the KV/table backends, which
    /// scan their keyset index and apply this per row).</summary>
    /// <param name="authorization">The candidate row.</param>
    /// <returns><see langword="true"/> if the row is admitted by the administered-set filter.</returns>
    public bool MatchesAdministeredSet(in EnvironmentRunnerAuthorization authorization)
    {
        if (this.AdministeredEnvironments is not { } administered)
        {
            return true;
        }

        foreach (string administeredEnvironment in administered)
        {
            if (authorization.EnvironmentEquals(administeredEnvironment))
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

    /// <summary>The persisted name for <see cref="RunnerAuthorizationStatus.Quarantined"/>.</summary>
    public const string Quarantined = "Quarantined";

    /// <summary>The persisted name for <see cref="RunnerAuthorizationStatus.Revoked"/>.</summary>
    public const string Revoked = "Revoked";

    /// <summary>Gets the persisted wire name for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(RunnerAuthorizationStatus status) => status switch
    {
        RunnerAuthorizationStatus.Pending => Pending,
        RunnerAuthorizationStatus.Authorized => Authorized,
        RunnerAuthorizationStatus.Quarantined => Quarantined,
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

/// <summary>Thrown when a runner registration presents a machine principal (design §16.4) that differs from the one already
/// bound to the same <c>(environment, runnerId)</c> — one authenticated machine cannot take over a runnerId a different
/// machine principal already proved ownership of. Distinct from <see cref="RunnerAuthorizationConflictException"/> (an etag
/// race); the control-plane registration endpoint maps this to a 409 with its own problem type.</summary>
public sealed class RunnerPrincipalConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RunnerPrincipalConflictException"/> class.</summary>
    /// <param name="environment">The environment the runner registered for.</param>
    /// <param name="runnerId">The runner id already bound to a different principal.</param>
    /// <param name="presentedPrincipal">The principal the conflicting registration presented.</param>
    public RunnerPrincipalConflictException(string environment, string runnerId, string presentedPrincipal)
        : base($"Runner '{runnerId}' in environment '{environment}' is already bound to a different machine principal; the presented principal '{presentedPrincipal}' cannot take it over.")
    {
        this.Environment = environment;
        this.RunnerId = runnerId;
        this.PresentedPrincipal = presentedPrincipal;
    }

    /// <summary>Gets the environment the runner registered for.</summary>
    public string Environment { get; }

    /// <summary>Gets the runner id already bound to a different principal.</summary>
    public string RunnerId { get; }

    /// <summary>Gets the principal the conflicting registration presented.</summary>
    public string PresentedPrincipal { get; }
}