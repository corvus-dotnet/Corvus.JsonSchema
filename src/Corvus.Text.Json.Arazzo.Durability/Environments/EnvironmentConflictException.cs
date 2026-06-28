// <copyright file="EnvironmentConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Thrown when an optimistic-concurrency update/delete against an <see cref="IEnvironmentStore"/> fails because the
/// caller's expected <see cref="WorkflowEtag"/> no longer matches the stored environment — the caller must reload and
/// retry. Maps to HTTP 409 at the control-plane surface.
/// </summary>
public sealed class EnvironmentConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EnvironmentConflictException"/> class.</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public EnvironmentConflictException(string name, WorkflowEtag expected)
        : base($"Concurrency conflict updating environment '{name}': expected etag {expected} no longer matches.")
    {
        this.Name = name;
        this.Expected = expected;
    }

    /// <summary>Gets the environment name.</summary>
    public string Name { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}