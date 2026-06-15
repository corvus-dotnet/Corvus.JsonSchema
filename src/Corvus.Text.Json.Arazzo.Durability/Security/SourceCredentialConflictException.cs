// <copyright file="SourceCredentialConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when an optimistic-concurrency update/delete against an <see cref="ISourceCredentialStore"/> fails because
/// the caller's expected <see cref="WorkflowEtag"/> no longer matches the stored binding — the caller must reload and
/// retry. Maps to HTTP 409 at the control-plane surface.
/// </summary>
public sealed class SourceCredentialConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SourceCredentialConflictException"/> class.</summary>
    /// <param name="key">The binding key (<c>sourceName@environment</c>).</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public SourceCredentialConflictException(string key, WorkflowEtag expected)
        : base($"Concurrency conflict updating source credential '{key}': expected etag {expected} no longer matches.")
    {
        this.Key = key;
        this.Expected = expected;
    }

    /// <summary>Gets the binding key (<c>sourceName@environment</c>).</summary>
    public string Key { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}