// <copyright file="ArazzoTelemetry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// OpenTelemetry-compliant instrumentation for Arazzo workflow execution.
/// </summary>
/// <remarks>
/// <para>
/// Register with the OpenTelemetry pipeline using:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(b =&gt; b.AddSource(ArazzoTelemetry.ActivitySourceName))
///     .WithMetrics(b =&gt; b.AddMeter(ArazzoTelemetry.MeterName));
/// </code>
/// </para>
/// <para>
/// All instruments are zero-cost when no listener is attached: <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <see langword="null"/> without a listener, and counter/histogram operations are no-ops
/// without a <see cref="MeterListener"/>. Built on <see cref="System.Diagnostics"/> only — no
/// dependency on any OpenTelemetry package.
/// </para>
/// </remarks>
public static class ArazzoTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource"/> name. Use with <c>AddSource("Corvus.Arazzo")</c>.
    /// </summary>
    public const string ActivitySourceName = "Corvus.Arazzo";

    /// <summary>
    /// The <see cref="Meter"/> name. Use with <c>AddMeter("Corvus.Arazzo")</c>.
    /// </summary>
    public const string MeterName = "Corvus.Arazzo";

    /// <summary>The span/measurement tag carrying a run id.</summary>
    public const string RunIdTag = "corvus.arazzo.run_id";

    /// <summary>The span/measurement tag carrying a workflow id.</summary>
    public const string WorkflowIdTag = "corvus.arazzo.workflow_id";

    /// <summary>The span/measurement tag carrying the identity that performed a control-plane action (for audit).</summary>
    public const string ActorTag = "corvus.arazzo.actor";

    /// <summary>The span/measurement tag carrying the <c>ResumeMode</c> of a resume action.</summary>
    public const string ResumeModeTag = "corvus.arazzo.resume_mode";

    /// <summary>The span tag carrying the outcome of a control-plane action (e.g. <c>resumed</c>, <c>not-faulted</c>, <c>conflict</c>).</summary>
    public const string OutcomeTag = "corvus.arazzo.outcome";

    /// <summary>The span/measurement tag carrying a run's lifecycle status.</summary>
    public const string StatusTag = "corvus.arazzo.status";

    /// <summary>The tag naming why a runner refused to advance a run it claimed (ADR 0065 decisions 4 and 6): the anchor fault that fired, or <c>integrity</c> for a row that did not verify.</summary>
    public const string RefusalTag = "corvus.arazzo.refusal";

    /// <summary>The span tag carrying a run's telemetry correlation id (the W3C trace id captured at creation).</summary>
    public const string CorrelationIdTag = "corvus.arazzo.correlation_id";

    /// <summary>The span/measurement tag carrying a catalog version's base workflow id.</summary>
    public const string BaseWorkflowIdTag = "corvus.arazzo.base_workflow_id";

    /// <summary>The span/measurement tag carrying a catalog version number.</summary>
    public const string VersionNumberTag = "corvus.arazzo.version_number";

    /// <summary>The span tag carrying the disclosure tier a step-journal read reached (design §14/§860): <c>full</c> (the
    /// payloads were disclosed), <c>redacted</c> (a sensitive version's payloads were withheld from a caller below the
    /// stronger grant), or <c>refused</c> (the run was out of read reach or absent). The read-access audit signal.</summary>
    public const string JournalDisclosureTag = "corvus.arazzo.journal_disclosure";

    /// <summary>The span tag carrying the kind of resource a governance action targeted (design §850): e.g.
    /// <c>access-request</c>, <c>credential</c>, <c>security-binding</c>, <c>runner</c>, <c>environment</c>. Paired with
    /// <see cref="TargetIdTag"/> it names <em>which</em> resource a governance audit concerns, uniformly across surfaces.</summary>
    public const string TargetKindTag = "corvus.arazzo.target_kind";

    /// <summary>The span tag carrying the id (or name) of the resource a governance action targeted (design §850) — an
    /// identifier only, never a payload. Paired with <see cref="TargetKindTag"/>.</summary>
    public const string TargetIdTag = "corvus.arazzo.target_id";

    /// <summary>The measurement tag carrying the governance action name (design §850), e.g. <c>access-request.approve</c>
    /// or <c>runner.revoke</c> — the same value as the audit span's name. Dimensions the governance-decision counter.</summary>
    public const string ActionTag = "corvus.arazzo.action";

    /// <summary>The span/measurement tag carrying the tenant (owner group) the actor of a governance action acts in
    /// (ADR 0038), read from the actor's own stamped identity. Absent when the deployment stamps no owner group.</summary>
    public const string TenantTag = "corvus.arazzo.tenant";

    /// <summary>The span/measurement tag carrying the deployment environment a governance action is scoped to (ADR 0038):
    /// a run start, a schedule run, an environment or runner mutation. Absent for an action that is not environment-scoped.</summary>
    public const string EnvironmentTag = "corvus.arazzo.environment";

    private static readonly string Version =
        typeof(ArazzoTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> for distributed tracing of workflow execution.
    /// </summary>
    /// <remarks>
    /// The engine emits a workflow span (root) with a child span per step; sub-workflow steps nest
    /// their own workflow span, and operation/message requests nest beneath the step.
    /// </remarks>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, Version);

    /// <summary>
    /// Gets the <see cref="Meter"/> for workflow execution metrics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName, Version);

    /// <summary>
    /// Gets the counter for workflows started.
    /// </summary>
    public static Counter<long> WorkflowsStarted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.started", "{workflow}", "Workflows started");

    /// <summary>
    /// Gets the counter for workflows that completed successfully.
    /// </summary>
    public static Counter<long> WorkflowsCompleted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.completed", "{workflow}", "Workflows completed successfully");

    /// <summary>
    /// Gets the counter for workflows that faulted (terminal-but-recoverable failure).
    /// </summary>
    public static Counter<long> WorkflowsFaulted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.faulted", "{workflow}", "Workflows that faulted");

    /// <summary>
    /// Gets the counter of runs a runner declined to advance because the row it loaded did not verify or the tenant
    /// anchor refused it (ADR 0065 decisions 4 and 6): a rollback, a substitution, a replay, or a claim on a run the
    /// tenant recorded as finished. Tagged by <see cref="RefusalTag"/> with the fault that fired.
    /// </summary>
    public static Counter<long> WorkflowsRefused { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.refused", "{workflow}", "Runs a runner refused to advance on integrity or anchor grounds");

    /// <summary>
    /// Gets the counter for steps executed.
    /// </summary>
    public static Counter<long> StepsExecuted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.steps.executed", "{step}", "Steps executed");

    /// <summary>
    /// Gets the counter for step retry attempts.
    /// </summary>
    public static Counter<long> StepRetries { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.steps.retries", "{retry}", "Step retry attempts");

    /// <summary>
    /// Gets the counter for control-flow transfers (<c>goto</c> success/failure actions).
    /// </summary>
    public static Counter<long> Gotos { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.gotos", "{goto}", "Control-flow goto transfers");

    // There is deliberately no workflow.duration or step.duration histogram. A durable run re-enters the executor
    // at its cursor on each advance (ADR 0019), so an in-process timer would measure a single advance, not the
    // end-to-end duration its name would imply. End-to-end timing is carried by the per-workflow and per-step trace
    // spans (the durable executor re-establishes the original trace via the run's correlation id), and the cost of
    // persisting an advance is the checkpoint.duration histogram below.

    /// <summary>
    /// Gets the counter for runs an operator resumed through the control plane (tagged with the resume mode).
    /// </summary>
    public static Counter<long> WorkflowsResumed { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.resumed", "{workflow}", "Faulted runs resumed via the control plane");

    /// <summary>
    /// Gets the counter for runs an operator cancelled through the control plane.
    /// </summary>
    public static Counter<long> WorkflowsCancelled { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.cancelled", "{workflow}", "Runs cancelled via the control plane");

    /// <summary>
    /// Gets the counter for runs that suspended awaiting a durable timer or correlated message (Tier 2).
    /// </summary>
    public static Counter<long> WorkflowsSuspended { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.suspended", "{workflow}", "Runs suspended awaiting a timer or message");

    /// <summary>
    /// Gets the counter for terminal runs reaped by a control-plane purge.
    /// </summary>
    public static Counter<long> WorkflowsPurged { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.purged", "{workflow}", "Terminal runs reaped via the control plane");

    /// <summary>
    /// Gets the counter for runs an operator deleted individually through the control plane.
    /// </summary>
    public static Counter<long> WorkflowsDeleted { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.workflows.deleted", "{workflow}", "Runs deleted individually via the control plane");

    /// <summary>
    /// Gets the histogram measuring how long persisting a checkpoint takes, in seconds.
    /// </summary>
    public static Histogram<double> CheckpointDuration { get; } =
        Meter.CreateHistogram<double>("corvus.arazzo.checkpoint.duration", "s", "Duration of persisting a run checkpoint");

    /// <summary>
    /// Gets the counter for source credentials rotated through the control plane (a rotation = changing the secret
    /// reference of a binding). A rotation rate that falls to zero is a governance signal (design §850).
    /// </summary>
    public static Counter<long> CredentialsRotated { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.credentials.rotated", "{credential}", "Source credentials rotated via the control plane");

    /// <summary>
    /// Gets the counter for governance decisions recorded through the control plane (design §850), dimensioned by
    /// <see cref="ActionTag">action</see> and <see cref="OutcomeTag">outcome</see>. Every governance audit increments it,
    /// so decision rates — approvals, denials, revocations, refusals — are queryable per action without bespoke counters.
    /// </summary>
    public static Counter<long> GovernanceDecisions { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.governance.decisions", "{decision}", "Governance decisions recorded via the control plane");

    /// <summary>
    /// Gets the counter for audit records the audit sink refused (ADR 0069), dimensioned by <see cref="ActionTag">action</see>.
    /// Each one is a governance action that was applied and left no record in the chain, so any non-zero rate is an alert.
    /// </summary>
    public static Counter<long> AuditAppendFailures { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.governance.audit.append_failures", "{record}", "Audit records the audit sink refused");

    /// <summary>
    /// Gets the counter for audit chain heads that could not be signed or stored (ADR 0069). While it rises the chain's
    /// unsigned window is growing: records are still chained, and nothing yet vouches for the newest of them.
    /// </summary>
    public static Counter<long> AuditHeadFailures { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.governance.audit.head_failures", "{head}", "Audit chain heads that could not be signed or stored");

    /// <summary>
    /// Gets the counter for reads that were refused with a non-disclosing not-found (ADR 0070), dimensioned by
    /// <see cref="ActionTag">action</see> and, where present, by tenant. It counts every refusal, those over a subject's
    /// bound included, which the audit chain does not take one by one: a probe's full rate is here.
    /// </summary>
    public static Counter<long> ReadRefusals { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.governance.read.refusals", "{refusal}", "Reads refused with a non-disclosing not-found");

    /// <summary>
    /// Gets the counter for reads that disclose no payload (ADR 0070, tier three): lists, searches, counts and index-row
    /// gets, dimensioned by <see cref="ActionTag">action</see>, <see cref="OutcomeTag">outcome</see> and, where present,
    /// tenant. They are most of the traffic, so they are metered and not recorded: a tenant reading far more than it
    /// usually does shows here.
    /// </summary>
    public static Counter<long> Reads { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.governance.reads", "{read}", "Reads that disclose no payload: lists, searches, counts and index-row gets");

    /// <summary>
    /// Gets the counter for authentications (ADR 0071), dimensioned by <c>corvus.arazzo.auth.scheme</c>,
    /// <see cref="OutcomeTag">outcome</see> (<c>success</c>, <c>failure</c>, <c>none</c> for a request that carried no
    /// credential) and, for a failure, <c>corvus.arazzo.auth.reason</c>. It counts every failure, those over an
    /// address's bound included, which the audit chain does not take one by one: the rate of a credential-stuffing run
    /// is here.
    /// </summary>
    public static Counter<long> Authentications { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.authentications", "{authentication}", "Authentications, by scheme, outcome and failure reason");

    /// <summary>
    /// Gets the counter for runners pruned from the registry because their heartbeat went stale (ADR 0029). A rising
    /// rate is a fleet that is dying or a network that is losing heartbeats.
    /// </summary>
    public static Counter<long> RunnersPruned { get; } =
        Meter.CreateCounter<long>("corvus.arazzo.runners.pruned", "{runner}", "Runners pruned for a stale heartbeat");
}