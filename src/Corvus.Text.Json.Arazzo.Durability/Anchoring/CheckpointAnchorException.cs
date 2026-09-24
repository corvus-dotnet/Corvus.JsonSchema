// <copyright file="CheckpointAnchorException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The tenant anchor refused to let a run open or advance (ADR 0065 decision 6): the decision table matched a row
/// whose outcome is a refusal or a hard fault, the anchor store rejected a write no conforming writer produces, or the
/// environment has no attested incarnation. The run cannot be advanced; the lease is given back and the run is left
/// as it is, for the tenant operator to dispose of envelope-only or, once a re-anchor can be applied, to recover.
/// </summary>
public sealed class CheckpointAnchorException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="CheckpointAnchorException"/> class.</summary>
    /// <param name="address">The run.</param>
    /// <param name="decision">The table row that matched, or <see langword="null"/> for a refusal outside the table.</param>
    /// <param name="message">The message.</param>
    public CheckpointAnchorException(in WorkflowRunAddress address, AnchorOpenDecision? decision, string message)
        : base(message)
    {
        this.Address = address;
        this.Decision = decision;
    }

    /// <summary>Gets the run the anchor refused.</summary>
    public WorkflowRunAddress Address { get; }

    /// <summary>Gets the decision-table row that matched, when the refusal came from the table.</summary>
    public AnchorOpenDecision? Decision { get; }

    /// <summary>Gets whether an operator-signed re-anchor may recover the run.</summary>
    public bool ReAnchorable => this.Decision is { ReAnchorable: true };
}