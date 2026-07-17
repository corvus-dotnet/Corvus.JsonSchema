// <copyright file="SensitiveReadAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>The disclosure tier a high-sensitivity read reached — the read-access audit signal (design §14/§860).</summary>
internal enum JournalDisclosure
{
    /// <summary>The step-output payloads were disclosed in full (the caller held the stronger grant).</summary>
    Full,

    /// <summary>A sensitive version's payloads were withheld (redacted) from a caller below the stronger grant.</summary>
    Redacted,

    /// <summary>The run was out of the caller's read reach, or absent — nothing was disclosed (a non-disclosing 404).</summary>
    Refused,
}

/// <summary>
/// Emits the read-access audit for a high-sensitivity read (design §860): a <c>workflow.journal.read</c> span on the
/// <see cref="ArazzoTelemetry.ActivitySource"/> plus an audit-grade structured log — so <em>who</em> read <em>which</em>
/// run's step journal, and at which <see cref="JournalDisclosure">disclosure tier</see>, leaves a trace in the deployment's
/// telemetry/log pipeline (where security audits are retained and queried). Governance <em>mutations</em> already emit
/// audit spans; this closes the gap that a sensitive <em>read</em> left none. The span is zero-cost when unobserved.
/// </summary>
/// <remarks>Reusable by any future high-sensitivity read: pass the caller (actor), the resource (run + workflow), and the
/// tier reached. The audit is best-effort observability, not a durable store (design §860 decision).</remarks>
internal static class SensitiveReadAudit
{
    /// <summary>Audits a step-journal read.</summary>
    /// <param name="logger">The audit logger, if the host wired one (the span is emitted regardless).</param>
    /// <param name="actor">The caller who read the journal (the audited subject).</param>
    /// <param name="runId">The run whose journal was read.</param>
    /// <param name="workflowId">The run's versioned workflow id (empty when the run was not resolved, e.g. a refused read).</param>
    /// <param name="disclosure">The disclosure tier the read reached.</param>
    public static void JournalRead(ILogger? logger, string actor, string runId, string workflowId, JournalDisclosure disclosure)
    {
        string tier = disclosure switch
        {
            JournalDisclosure.Full => "full",
            JournalDisclosure.Redacted => "redacted",
            _ => "refused",
        };

        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.journal.read"))
        {
            activity?.SetTag(ArazzoTelemetry.ActorTag, actor);
            activity?.SetTag(ArazzoTelemetry.RunIdTag, runId);
            if (!string.IsNullOrEmpty(workflowId))
            {
                activity?.SetTag(ArazzoTelemetry.WorkflowIdTag, workflowId);
            }

            activity?.SetTag(ArazzoTelemetry.JournalDisclosureTag, tier);
        }

        logger?.LogInformation(
            "Audit: {Actor} read the step journal of run {RunId} ({WorkflowId}); disclosure {JournalDisclosure}.",
            actor,
            runId,
            string.IsNullOrEmpty(workflowId) ? "unknown" : workflowId,
            tier);
    }
}