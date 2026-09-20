// <copyright file="GovernanceAuditor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Records a governance action (ADR 0038, ADR 0069): a span named for the action on the
/// <see cref="ArazzoTelemetry.ActivitySource"/>, an audit-grade structured log, the governance-decision counter, and,
/// where the deployment has an audit sink, one record appended to this control plane's audit chain. Every governance
/// mutation goes through it: access-request decisions, credential custody, grant and rule authoring (by an operator, the
/// bootstrap, or the approval service), runner authorization, promotion, administrator transfers, and run start.
/// </summary>
/// <remarks>
/// <para>
/// A deployment has one auditor. The host builds it and hands the same instance to the bootstrap and to the control
/// plane, so everything one process records is one chain. The secured postures refuse to start without a sink.
/// </para>
/// <para>
/// Payload-safe by construction: the method accepts only an action name, an <see cref="AuditSubject"/>, a target kind
/// and id, an outcome label and an environment name, all controlled vocabulary or identifiers, never a workflow payload
/// or a secret. A caller cannot route a step output or a credential value through it.
/// </para>
/// <para>
/// A refused governance action is recorded too (an attempted-access signal): the security control firing, a requester
/// trying to decide their own request, a caller lacking administration, an author elevating themselves, is exactly what
/// a security audit wants to see, so the outcome carries the refusal (for example <c>refused-own-request</c>).
/// </para>
/// <para>
/// The record is appended after the action commits, and the append is awaited. When the sink refuses it the call throws
/// <see cref="AuditAppendException"/>: the action stands, and the request that made it fails, because going on
/// unrecorded is the gap the sink exists to close. Reads and run execution never come here.
/// </para>
/// </remarks>
public sealed class GovernanceAuditor : IAsyncDisposable
{
    private readonly AuditChainWriter? chain;

    /// <summary>Initializes a new instance of the <see cref="GovernanceAuditor"/> class.</summary>
    /// <param name="logger">The audit logger, if the host wired one (the span is emitted regardless).</param>
    /// <param name="sink">The audit sink, or <see langword="null"/> for a deployment that keeps no audit chain (the <c>Open</c> development posture only).</param>
    /// <param name="timeProvider">The clock records are stamped from (defaults to the system clock).</param>
    public GovernanceAuditor(ILogger? logger = null, IAuditSink? sink = null, TimeProvider? timeProvider = null)
    {
        this.Logger = logger;
        this.chain = sink is null ? null : new AuditChainWriter(sink, timeProvider);
        this.Health = new AuditSinkHealth(timeProvider ?? TimeProvider.System);
    }

    /// <summary>Gets an auditor with no logger and no sink: the span and the counter only.</summary>
    public static GovernanceAuditor None { get; } = new();

    /// <summary>Gets the audit logger, which the read-side audit also writes to.</summary>
    public ILogger? Logger { get; }

    /// <summary>Gets a value indicating whether the auditor appends to an audit sink.</summary>
    public bool HasSink => this.chain is not null;

    /// <summary>Gets what the last append to the sink came to, for a host's health check.</summary>
    public AuditSinkHealth Health { get; }

    /// <summary>Records a governance action.</summary>
    /// <param name="action">The action name, also the span name (for example <c>access-request.approve</c>). Stable, controlled vocabulary.</param>
    /// <param name="actor">The caller who performed the action: its canonical subject and the tenant it acts in.</param>
    /// <param name="targetKind">The kind of resource the action targeted (for example <c>access-request</c>).</param>
    /// <param name="targetId">The id (or name) of the resource the action targeted, an identifier only, never a payload.</param>
    /// <param name="outcome">The outcome of the action (for example <c>granted</c>, <c>denied</c>, <c>revoked</c>, <c>refused-own-request</c>).</param>
    /// <param name="environment">The deployment environment the action is scoped to (a run start, a schedule run, an environment or runner mutation), or <see langword="null"/> for an action that is not environment-scoped.</param>
    /// <param name="cancellationToken">A cancellation token. Cancelling an append abandons the chain, so a request's own token is not passed here.</param>
    /// <returns>A task that completes when the action is recorded.</returns>
    /// <exception cref="AuditAppendException">The audit sink refused the record. The action stands.</exception>
    public ValueTask MutationAsync(string action, AuditSubject actor, string targetKind, string targetId, string outcome, string? environment = null, CancellationToken cancellationToken = default)
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

        this.Logger?.LogInformation(
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

        return this.chain is null
            ? ValueTask.CompletedTask
            : this.AppendAsync(new AuditEntry(action, subject, tenant, targetKind, targetId, outcome, environment), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.chain?.DisposeAsync() ?? ValueTask.CompletedTask;

    private async ValueTask AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await this.chain!.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (AuditAppendException ex)
        {
            this.Health.Failed();
            ArazzoTelemetry.AuditAppendFailures.Add(1, new KeyValuePair<string, object?>(ArazzoTelemetry.ActionTag, entry.Action));
            this.Logger?.LogError(
                ex,
                "Audit: the record of {Action} on {TargetKind} {TargetId} by {Actor} (outcome {Outcome}) could not be appended to the audit sink. The action stands and is not in the chain.",
                entry.Action,
                entry.TargetKind,
                entry.TargetId,
                entry.Actor,
                entry.Outcome);
            throw;
        }

        this.Health.Succeeded();
    }
}