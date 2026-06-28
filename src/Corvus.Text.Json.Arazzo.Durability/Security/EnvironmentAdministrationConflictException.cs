// <copyright file="EnvironmentAdministrationConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when an optimistic-concurrency change to an environment's administration record (design §7.7) fails because the
/// caller's expected <see cref="WorkflowEtag"/> no longer matches the stored state — a concurrent administrator change won
/// the race, so the caller must reload and retry. Maps to HTTP 409 at the control-plane surface. Mirrors
/// <see cref="WorkflowAdministrationConflictException"/>.
/// </summary>
public sealed class EnvironmentAdministrationConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EnvironmentAdministrationConflictException"/> class.</summary>
    /// <param name="environmentName">The environment whose administration change conflicted.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public EnvironmentAdministrationConflictException(string environmentName, WorkflowEtag expected)
        : base($"Concurrency conflict changing administration of environment '{environmentName}': expected etag {expected} no longer matches.")
    {
        this.EnvironmentName = environmentName;
        this.Expected = expected;
    }

    /// <summary>Gets the environment whose administration change conflicted.</summary>
    public string EnvironmentName { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}