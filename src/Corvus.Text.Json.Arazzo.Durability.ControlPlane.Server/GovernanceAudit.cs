// <copyright file="GovernanceAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Emits the read-across audit for a governance action (design §850): a span named for the action on the
/// <see cref="ArazzoTelemetry.ActivitySource"/> plus an audit-grade structured log — so <em>who</em> changed
/// <em>which</em> governed resource, and with what <em>outcome</em>, leaves a retained trace in the deployment's
/// telemetry/log pipeline. This generalizes <see cref="SensitiveReadAudit"/> from the sensitive-read case to every
/// governance mutation (access-request decisions, credential custody, grant/rule authoring, runner authorization,
/// promotion, administrator transfers).
/// </summary>
/// <remarks>
/// <para>
/// Payload-safe by construction: the method accepts only an action name, an actor, a target kind and id, and an
/// outcome label — all controlled vocabulary or identifiers, never a workflow payload or a secret. A caller cannot
/// route a step output or a credential value through it. The span is zero-cost when no listener is attached, and the
/// log is a no-op when the host wired no logger; the audit is best-effort observability, not a durable store.
/// </para>
/// <para>
/// A refused governance action is audited too (an attempted-access signal): the security control firing — a requester
/// trying to decide their own request (the independent-decision refusal), a caller lacking administration — is exactly
/// what a security audit wants to see, so the outcome carries the refusal (e.g. <c>refused-own-request</c>).
/// </para>
/// </remarks>
internal static class GovernanceAudit
{
    /// <summary>Audits a governance action.</summary>
    /// <param name="logger">The audit logger, if the host wired one (the span is emitted regardless).</param>
    /// <param name="action">The action name — also the span name (e.g. <c>access-request.approve</c>). Stable, controlled vocabulary.</param>
    /// <param name="actor">The caller who performed the action (the audited subject).</param>
    /// <param name="targetKind">The kind of resource the action targeted (e.g. <c>access-request</c>).</param>
    /// <param name="targetId">The id (or name) of the resource the action targeted — an identifier only, never a payload.</param>
    /// <param name="outcome">The outcome of the action (e.g. <c>granted</c>, <c>denied</c>, <c>revoked</c>, <c>refused-own-request</c>).</param>
    public static void Mutation(ILogger? logger, string action, string actor, string targetKind, string targetId, string outcome)
    {
        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity(action))
        {
            activity?.SetTag(ArazzoTelemetry.ActorTag, actor);
            activity?.SetTag(ArazzoTelemetry.TargetKindTag, targetKind);
            activity?.SetTag(ArazzoTelemetry.TargetIdTag, targetId);
            activity?.SetTag(ArazzoTelemetry.OutcomeTag, outcome);
        }

        logger?.LogInformation(
            "Audit: {Actor} performed {Action} on {TargetKind} {TargetId}; outcome {Outcome}.",
            actor,
            action,
            targetKind,
            targetId,
            outcome);
    }
}