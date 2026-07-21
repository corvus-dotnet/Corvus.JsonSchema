// <copyright file="WorkflowStepJournalEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// One entry in a durable run's per-step journal (ADR 0050): payload-free metadata attesting that a step
/// executed, with what outcome, on which attempt, and over what time window. It carries no request or response
/// exchanges, so the checkpoint stays small and the journal is payload-safe.
/// </summary>
/// <param name="StepId">The id of the step the entry records.</param>
/// <param name="Status">The step's outcome.</param>
/// <param name="Attempt">The 1-based attempt number the entry records.</param>
/// <param name="StartedAt">When the step's execution began.</param>
/// <param name="EndedAt">When the step's execution ended.</param>
public readonly record struct WorkflowStepJournalEntry(
    string StepId,
    WorkflowStepStatus Status,
    int Attempt,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);