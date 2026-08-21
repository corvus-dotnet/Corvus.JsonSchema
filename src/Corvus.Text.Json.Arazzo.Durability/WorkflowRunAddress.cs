// <copyright file="WorkflowRunAddress.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A run's full address: the environment it is pinned to and its run id. The primary key of every run-addressed
/// store operation is this composite, at every ingress and in every backend (ADR 0065 decision 9) — keyed by run
/// id alone, a caller-chosen id would be a cross-tenant handle, and either collision branch an existence oracle
/// over another tenant's runs.
/// </summary>
/// <remarks>
/// <para>
/// The environment name is validated against the DNS-label grammar here as well as at the contract ingress: the
/// name is half the key in the delimited-key backends (Redis, NATS KV, Azure Table), so an out-of-grammar name is
/// an aliasing surface, never merely a bad label. The run id half deliberately mirrors
/// <see cref="WorkflowRunId"/>: the 32-hex grammar is enforced at every ingress, not in the constructor, so
/// in-process fixtures keep their loose ids.
/// </para>
/// <para>
/// Addresses order by environment first, then run id, both ordinal — the keyset order every listing pages by.
/// </para>
/// </remarks>
public readonly record struct WorkflowRunAddress
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowRunAddress"/> struct.</summary>
    /// <param name="environment">The environment the run is pinned to. Must satisfy the environment-name grammar.</param>
    /// <param name="runId">The run id.</param>
    /// <exception cref="ArgumentException"><paramref name="environment"/> is outside the environment-name grammar.</exception>
    public WorkflowRunAddress(string environment, WorkflowRunId runId)
    {
        if (!Environments.EnvironmentName.IsWellFormed(environment))
        {
            throw ThrowHelper.GetEnvironmentNameOutsideGrammarException(environment, nameof(environment));
        }

        this.Environment = environment;
        this.RunId = runId;
    }

    /// <summary>Gets the environment the run is pinned to.</summary>
    public string Environment { get; }

    /// <summary>Gets the run id.</summary>
    public WorkflowRunId RunId { get; }

    /// <summary>Compares two addresses in keyset order: environment first, then run id, both ordinal.</summary>
    /// <param name="left">The first address.</param>
    /// <param name="right">The second address.</param>
    /// <returns>Negative when <paramref name="left"/> orders first, positive when it orders last, zero when equal.</returns>
    public static int Compare(in WorkflowRunAddress left, in WorkflowRunAddress right)
    {
        int byEnvironment = string.CompareOrdinal(left.Environment, right.Environment);
        return byEnvironment != 0 ? byEnvironment : string.CompareOrdinal(left.RunId.Value, right.RunId.Value);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{this.Environment}/{this.RunId.Value}";
}