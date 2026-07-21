// <copyright file="WorkflowStepStatus.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The outcome a durable run records for a step in its per-step journal (ADR 0050). It is serialized by name, so
/// it is stable across any reordering of the enum.
/// </summary>
public enum WorkflowStepStatus
{
    /// <summary>The step executed and completed successfully.</summary>
    Succeeded,

    /// <summary>The step failed on its final attempt, faulting the run.</summary>
    Faulted,

    /// <summary>The step was skipped by a skip resume.</summary>
    Skipped,
}