// <copyright file="SecurityPolicyConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when an optimistic-concurrency update/delete against an <see cref="ISecurityPolicyStore"/> fails because
/// the caller's expected <see cref="WorkflowEtag"/> no longer matches the stored record — the caller must reload
/// and retry. Maps to HTTP 409 at the control-plane surface.
/// </summary>
public sealed class SecurityPolicyConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SecurityPolicyConflictException"/> class.</summary>
    /// <param name="kind">The record kind (e.g. <c>rule</c> or <c>binding</c>).</param>
    /// <param name="id">The record identifier.</param>
    /// <param name="expected">The caller's stale expected etag.</param>
    public SecurityPolicyConflictException(string kind, string id, WorkflowEtag expected)
        : base($"Concurrency conflict updating {kind} '{id}': expected etag {expected} no longer matches.")
    {
        this.Kind = kind;
        this.Id = id;
        this.Expected = expected;
    }

    /// <summary>Gets the record kind (e.g. <c>rule</c> or <c>binding</c>).</summary>
    public string Kind { get; }

    /// <summary>Gets the record identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the caller's stale expected etag.</summary>
    public WorkflowEtag Expected { get; }
}