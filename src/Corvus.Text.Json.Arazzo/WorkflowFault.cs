// <copyright file="WorkflowFault.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The record of a terminal-but-recoverable failure that put a durable run into
/// <see cref="WorkflowRunStatus.Faulted"/> (plan §9.2): which step failed, on which attempt, with what
/// error, and when. It is persisted in the checkpoint so an operator (or an automated policy) can inspect
/// and remediate the run rather than losing the work already done.
/// </summary>
/// <param name="StepId">The id of the step that failed.</param>
/// <param name="Attempt">The attempt number on which it failed (1-based).</param>
/// <param name="Error">A description of the failure.</param>
/// <param name="At">When the fault occurred.</param>
public readonly record struct WorkflowFault(
    string StepId,
    int Attempt,
    string Error,
    DateTimeOffset At);