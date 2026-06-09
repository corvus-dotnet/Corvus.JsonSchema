// <copyright file="WorkflowRunId.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The opaque, stable identity of a single workflow run — the key under which its checkpoint is stored.
/// </summary>
/// <remarks>
/// A run id is supplied by the host (it is the host's correlation handle for the run), not generated here, so
/// callers can derive it from a business key (an order id, a saga id) and make resumption idempotent. The
/// underlying value is treated as an opaque string by every backend.
/// </remarks>
/// <param name="Value">The underlying identity string.</param>
public readonly record struct WorkflowRunId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <summary>Converts a string to a <see cref="WorkflowRunId"/>.</summary>
    /// <param name="value">The identity string.</param>
    public static implicit operator WorkflowRunId(string value) => new(value);
}