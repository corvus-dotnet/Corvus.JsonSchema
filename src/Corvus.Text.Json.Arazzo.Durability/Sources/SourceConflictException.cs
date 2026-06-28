// <copyright file="SourceConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// Thrown when an optimistic-concurrency update/delete against an <see cref="ISourceStore"/> fails because the caller's
/// expected <see cref="WorkflowEtag"/> no longer matches the stored source — the caller must reload and retry. Maps to
/// HTTP 409 at the control-plane surface.
/// </summary>
public sealed class SourceConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SourceConflictException"/> class.</summary>
    /// <param name="name">The source name.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public SourceConflictException(string name, WorkflowEtag expected)
        : base($"Concurrency conflict updating source '{name}': expected etag {expected} no longer matches.")
    {
        this.Name = name;
        this.Expected = expected;
    }

    /// <summary>Gets the source name.</summary>
    public string Name { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}