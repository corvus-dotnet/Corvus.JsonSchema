// <copyright file="WorkflowScheduleInput.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The inputs of a durable schedule run (#896), generated from <c>Schemas/WorkflowScheduleInput.json</c>. A
/// schedule is a durable run of the built-in <see cref="ScheduleHostedWorkflow"/>; this is that run's inputs —
/// the cadence and the target workflow each occurrence starts — persisted with the run and read on every resume.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/WorkflowScheduleInput.json")]
public readonly partial struct WorkflowScheduleInput
{
    /// <summary>Gets the schedule's stable identity (part of each fire's idempotency key).</summary>
    public string ScheduleIdValue => (string)this.ScheduleId;

    /// <summary>Gets the cron expression driving occurrences.</summary>
    public string CronValue => (string)this.Cron;

    /// <summary>Gets the IANA time zone id the cadence is expressed in (defaults to <c>UTC</c>).</summary>
    public string TimeZoneValue => (string)this.TimeZone;

    /// <summary>Gets a value indicating whether the cron expression carries a leading seconds field.</summary>
    public bool IncludeSecondsValue => (bool)this.IncludeSeconds;

    /// <summary>Gets the versioned workflow id each occurrence starts a run of.</summary>
    public string TargetWorkflowIdValue => (string)this.TargetWorkflowId;
}