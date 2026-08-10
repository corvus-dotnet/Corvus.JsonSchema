// <copyright file="ScheduleRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Schedules;

/// <summary>
/// What a schedule id resolves to: the deployment environment the schedule is pinned to and the scheduler
/// run that embodies it. The pair is the registry's whole payload — everything else about a schedule lives
/// in the scheduler run's checkpoint.
/// </summary>
/// <param name="Environment">The deployment environment the schedule (and its fired runs) are pinned to.</param>
/// <param name="RunId">The scheduler run's id (the schedule's derived address, ADR 0065 §9).</param>
public readonly record struct ScheduleRegistration(string Environment, WorkflowRunId RunId);