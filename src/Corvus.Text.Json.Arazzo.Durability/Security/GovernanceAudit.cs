// <copyright file="GovernanceAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Emits the read-across audit for a governance action (ADR 0038): a span named for the action on the
/// <see cref="ArazzoTelemetry.ActivitySource"/> plus an audit-grade structured log, so <em>who</em> changed
/// <em>which</em> governed resource, in which tenant and environment, and with what <em>outcome</em>, leaves a retained
/// trace in the deployment's telemetry and log pipeline. Every governance mutation goes through it: access-request
/// decisions, credential custody, grant and rule authoring (by an operator, the bootstrap, or the approval service),
/// runner authorization, promotion, administrator transfers, and run start.
/// </summary>
/// <remarks>
/// <para>
/// Payload-safe by construction: the method accepts only an action name, an <see cref="AuditSubject"/>, a target kind
/// and id, an outcome label and an environment name, all controlled vocabulary or identifiers, never a workflow payload
/// or a secret. A caller cannot route a step output or a credential value through it. The span is zero-cost when no
/// listener is attached, and the log is a no-op when the host wired no logger; the audit is best-effort observability,
/// not a durable store.
/// </para>
/// <para>
/// A refused governance action is audited too (an attempted-access signal): the security control firing, a requester
/// trying to decide their own request, a caller lacking administration, an author elevating themselves, is exactly what
/// a security audit wants to see, so the outcome carries the refusal (for example <c>refused-own-request</c>).
/// </para>
/// </remarks>
public static class GovernanceAudit
{
    /// <summary>Audits a governance action.</summary>
    /// <param name="logger">The audit logger, if the host wired one (the span is emitted regardless).</param>
    /// <param name="action">The action name, also the span name (for example <c>access-request.approve</c>). Stable, controlled vocabulary.</param>
    /// <param name="actor">The caller who performed the action: its canonical subject and the tenant it acts in.</param>
    /// <param name="targetKind">The kind of resource the action targeted (for example <c>access-request</c>).</param>
    /// <param name="targetId">The id (or name) of the resource the action targeted, an identifier only, never a payload.</param>
    /// <param name="outcome">The outcome of the action (for example <c>granted</c>, <c>denied</c>, <c>revoked</c>, <c>refused-own-request</c>).</param>
    /// <param name="environment">The deployment environment the action is scoped to (a run start, a schedule run, an environment or runner mutation), or <see langword="null"/> for an action that is not environment-scoped.</param>
    public static void Mutation(ILogger? logger, string action, in AuditSubject actor, string targetKind, string targetId, string outcome, string? environment = null)
    {
        string subject = actor.Subject;
        string? tenant = actor.OwnerGroup;
        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity(action))
        {
            if (activity is not null)
            {
                activity.SetTag(ArazzoTelemetry.ActorTag, subject);
                activity.SetTag(ArazzoTelemetry.TargetKindTag, targetKind);
                activity.SetTag(ArazzoTelemetry.TargetIdTag, targetId);
                activity.SetTag(ArazzoTelemetry.OutcomeTag, outcome);
                if (tenant is not null)
                {
                    activity.SetTag(ArazzoTelemetry.TenantTag, tenant);
                }

                if (environment is not null)
                {
                    activity.SetTag(ArazzoTelemetry.EnvironmentTag, environment);
                }
            }
        }

        logger?.LogInformation(
            "Audit: {Actor} (tenant {Tenant}) performed {Action} on {TargetKind} {TargetId} in environment {Environment}; outcome {Outcome}.",
            subject,
            tenant ?? "-",
            action,
            targetKind,
            targetId,
            environment ?? "-",
            outcome);

        // The governance-decision rate counter, dimensioned by action and outcome and, where present, by tenant and
        // environment, so decision rates (approvals, denials, revocations, refusals) are queryable per action and per
        // tenant without a bespoke counter each.
        var tags = new TagList
        {
            { ArazzoTelemetry.ActionTag, action },
            { ArazzoTelemetry.OutcomeTag, outcome },
        };
        if (tenant is not null)
        {
            tags.Add(ArazzoTelemetry.TenantTag, tenant);
        }

        if (environment is not null)
        {
            tags.Add(ArazzoTelemetry.EnvironmentTag, environment);
        }

        ArazzoTelemetry.GovernanceDecisions.Add(1, tags);
    }
}