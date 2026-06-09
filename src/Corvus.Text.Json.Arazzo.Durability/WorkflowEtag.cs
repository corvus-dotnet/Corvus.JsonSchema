// <copyright file="WorkflowEtag.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An opaque optimistic-concurrency token for a stored checkpoint. A save succeeds only if the caller's
/// expected etag still matches the store's current one; otherwise the store reports a conflict and the
/// caller must reload, so a horizontally-scaled fleet never double-advances a run.
/// </summary>
/// <remarks>
/// The value is opaque: each backend maps it to its native primitive (a blob ETag, a Postgres <c>xmin</c> or
/// version column, a SQL Server <c>rowversion</c>, a Cosmos <c>_etag</c>). <see cref="None"/> is the
/// sentinel a caller passes when it expects the run not to exist yet (a create), and is the etag a
/// <see cref="WorkflowRunId"/> with no stored checkpoint reports.
/// </remarks>
/// <param name="Value">The opaque token, or <see langword="null"/> for <see cref="None"/>.</param>
public readonly record struct WorkflowEtag(string? Value)
{
    /// <summary>Gets the sentinel etag meaning "no checkpoint exists yet" (used as the expected etag for a create).</summary>
    public static WorkflowEtag None => default;

    /// <summary>Gets a value indicating whether this is the <see cref="None"/> sentinel.</summary>
    public bool IsNone => this.Value is null;

    /// <inheritdoc/>
    public override string ToString() => this.Value ?? "(none)";
}