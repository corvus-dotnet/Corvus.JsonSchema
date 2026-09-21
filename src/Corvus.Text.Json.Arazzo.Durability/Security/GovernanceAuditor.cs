// <copyright file="GovernanceAuditor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Execution;
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
    /// <summary>The name of the span a signed head is published as.</summary>
    public const string AnchorActivityName = "audit.head";

    private readonly AuditChainWriter? chain;
    private readonly RefusalRecordLimiter refusals;
    private readonly RefusalRecordLimiter authenticationFailures;
    private readonly TimeProvider timeProvider;
    private readonly ITimer? refusalSweep;

    /// <summary>Initializes a new instance of the <see cref="GovernanceAuditor"/> class.</summary>
    /// <param name="logger">The audit logger, if the host wired one (the span is emitted regardless).</param>
    /// <param name="sink">The audit sink, or <see langword="null"/> for a deployment that keeps no audit chain (the <c>Open</c> development posture only).</param>
    /// <param name="timeProvider">The clock records are stamped from and the head cadence runs on (defaults to the system clock).</param>
    /// <param name="headSigner">The audit's own signer, under a key that signs nothing else, or <see langword="null"/> for a chain with no signed heads. The secured postures require one.</param>
    /// <param name="headOptions">The head cadence (defaults to <see cref="AuditHeadOptions.Default"/>).</param>
    /// <param name="refusalLimiter">The bound on how many refusal records one subject can append to the chain (defaults to 60 a minute).</param>
    /// <param name="authenticationFailureLimiter">The bound on how many authentication failure records one remote address can append to the chain (defaults to 60 a minute).</param>
    /// <param name="writerId">This instance's audit writer id, stable across its restarts: 1 to 63 lowercase ASCII letters, digits or hyphens. Defaults to the machine's name.</param>
    public GovernanceAuditor(ILogger? logger = null, IAuditSink? sink = null, TimeProvider? timeProvider = null, IExecutorPackageSigner? headSigner = null, AuditHeadOptions? headOptions = null, string? writerId = null, RefusalRecordLimiter? refusalLimiter = null, RefusalRecordLimiter? authenticationFailureLimiter = null)
    {
        this.Logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.refusals = refusalLimiter ?? new RefusalRecordLimiter();
        this.authenticationFailures = authenticationFailureLimiter ?? new RefusalRecordLimiter();
        this.Health = new AuditSinkHealth(timeProvider ?? TimeProvider.System);
        this.HasHeadSigner = sink is not null && headSigner is not null;
        this.chain = sink is null
            ? null
            : new AuditChainWriter(sink, timeProvider, headSigner: headSigner, headOptions: headOptions, onHeadSigned: this.PublishAnchor, onHeadFailed: this.HeadFailed, writerId: writerId, onResumed: this.Resumed);

        // The sweep is what records a flood's suppressed count when the subject that made it never asks again.
        if (this.chain is not null)
        {
            this.refusalSweep = this.timeProvider.CreateTimer(static state => ((GovernanceAuditor)state!).OnRefusalSweep(), this, this.refusals.WindowLength, this.refusals.WindowLength);
        }
    }

    /// <summary>Gets an auditor with no logger and no sink: the span and the counter only.</summary>
    public static GovernanceAuditor None { get; } = new();

    /// <summary>Gets the audit logger, which the read-side audit also writes to.</summary>
    public ILogger? Logger { get; }

    /// <summary>Gets a value indicating whether the auditor appends to an audit sink.</summary>
    public bool HasSink => this.chain is not null;

    /// <summary>Gets a value indicating whether the auditor signs its chain's heads.</summary>
    public bool HasHeadSigner { get; }

    /// <summary>Gets what the last append to the sink came to, for a host's health check.</summary>
    public AuditSinkHealth Health { get; }

    /// <summary>
    /// Creates an auditor over an in-memory sink, signing heads with a key made for it and thrown away with it. It is for
    /// tests and for trying the platform out: the sink is in the process it records and nobody holds the key's public
    /// half, so what it keeps is not evidence.
    /// </summary>
    /// <param name="logger">The audit logger, if any.</param>
    /// <returns>The auditor.</returns>
    public static GovernanceAuditor CreateInMemory(ILogger? logger = null)
        => CreateInMemory(new InMemoryAuditSink(), logger);

    /// <summary>Creates an auditor over an in-memory sink the caller holds, so that a test can read what was recorded. See <see cref="CreateInMemory(ILogger?)"/>.</summary>
    /// <param name="sink">The sink to record into.</param>
    /// <param name="logger">The audit logger, if any.</param>
    /// <returns>The auditor.</returns>
    public static GovernanceAuditor CreateInMemory(InMemoryAuditSink sink, ILogger? logger = null)
        => new(logger, sink, headSigner: new EcdsaExecutorPackageSigner(ECDsa.Create(ECCurve.NamedCurves.nistP256), "in-memory-audit-key"));

    /// <summary>
    /// Reads this instance's last audit chain back from the sink and opens the next, continuing it, under a head signed
    /// at once. A host calls it when it starts, so that whatever tail its last process left unsigned is frozen then. An
    /// auditor that is never started does the same at its first record. It does nothing where there is no sink.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the new chain is open.</returns>
    /// <exception cref="AuditAppendException">The sink could not be read, or refused the new chain.</exception>
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
        => this.chain?.ResumeAsync(cancellationToken) ?? ValueTask.CompletedTask;

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

    /// <summary>
    /// Records a read, or an attempt at one (ADR 0070): who read which resource, at which disclosure tier. The caller emits
    /// whatever span suits the surface; this writes the audit log and, where there is a sink, the chain's record.
    /// </summary>
    /// <param name="action">The read's name (for example <c>run.journal.read</c>). Stable, controlled vocabulary.</param>
    /// <param name="actor">The caller: its canonical subject and the tenant it acts in.</param>
    /// <param name="targetKind">The kind of resource read.</param>
    /// <param name="targetId">The id of the resource read, or asked for. An identifier only, never what was read.</param>
    /// <param name="disclosure">The disclosure tier (for example <c>full</c>, <c>redacted</c>, <c>refused</c>).</param>
    /// <param name="failClosed">
    /// <see langword="true"/> for a read a caller is about to be given the payload of, and can ask for again: the control
    /// plane's API disclosures. Its record comes first and the read fails closed: when the sink refuses the record the
    /// call throws and the caller discloses nothing, because a payload read that left no record is the event the
    /// read-side audit exists for. <see langword="false"/> for a refusal, and for a disclosure made in the course of
    /// running a workflow, a runner resolving a secret, since run execution is never gated on the sink (ADR 0069): the
    /// failure counts, degrades health, and the caller goes on as it would have.
    /// </param>
    /// <param name="environment">The environment the read is scoped to, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the read is recorded.</returns>
    /// <exception cref="AuditAppendException">The sink refused the record of a read that discloses a payload. Nothing has been disclosed.</exception>
    public ValueTask ReadAsync(string action, AuditSubject actor, string targetKind, string targetId, string disclosure, bool failClosed, string? environment = null)
    {
        this.Logger?.LogInformation(
            "Audit: {Actor} (tenant {Tenant}) read {TargetKind} {TargetId} ({Action}) in environment {Environment}; disclosure {Disclosure}.",
            actor.Subject,
            actor.OwnerGroup ?? "-",
            targetKind,
            targetId,
            action,
            environment ?? "-",
            disclosure);

        return this.chain is null
            ? ValueTask.CompletedTask
            : this.AppendReadAsync(new AuditEntry(action, actor.Subject, actor.OwnerGroup, targetKind, targetId, disclosure, environment, AuditEntryKind.Read), failClosed);
    }

    /// <summary>
    /// Records a read that was refused with a non-disclosing not-found (ADR 0070, tier two): the probe, by the actor, for
    /// the id it asked after. It never fails the request. Every refusal counts on
    /// <see cref="ArazzoTelemetry.ReadRefusals"/>; a subject's refusals are appended to the chain up to its bound in a
    /// window, and past it they are counted, and the count recorded when the window turns.
    /// </summary>
    /// <param name="action">The read that was refused.</param>
    /// <param name="actor">The caller.</param>
    /// <param name="targetKind">The kind of resource asked after.</param>
    /// <param name="targetId">The id asked after. An identifier only.</param>
    /// <param name="environment">The environment the read is scoped to, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the refusal is recorded, or counted.</returns>
    public async ValueTask RefusedReadAsync(string action, AuditSubject actor, string targetKind, string targetId, string? environment = null)
    {
        var tags = new TagList { { ArazzoTelemetry.ActionTag, action } };
        if (actor.OwnerGroup is { } tenant)
        {
            tags.Add(ArazzoTelemetry.TenantTag, tenant);
        }

        ArazzoTelemetry.ReadRefusals.Add(1, tags);

        bool admitted = this.refusals.Admit(actor.Subject, this.timeProvider.GetUtcNow(), out long suppressedBefore, out string recordedAs);
        if (suppressedBefore > 0)
        {
            await this.SuppressedAsync(recordedAs, suppressedBefore).ConfigureAwait(false);
        }

        if (admitted)
        {
            await this.ReadAsync(action, actor, targetKind, targetId, "refused", failClosed: false, environment).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records an authentication that failed (ADR 0071): the scheme, the reason and where it came from, and never any
    /// token material. It never fails the request. A failed authentication has no subject to be trusted, so failures are
    /// bounded for each remote address: appended to the chain up to the bound in a window, and past it counted, with the
    /// count recorded when the window turns. The caller counts every failure on
    /// <see cref="ArazzoTelemetry.Authentications"/>.
    /// </summary>
    /// <param name="scheme">The authentication scheme.</param>
    /// <param name="reason">Why it failed. Controlled vocabulary.</param>
    /// <param name="remoteAddress">The address the request came from.</param>
    /// <param name="subject">The subject, where the result named one; otherwise <see langword="null"/>.</param>
    /// <param name="issuer">The issuer, where the result named one; otherwise <see langword="null"/>.</param>
    /// <returns>A task that completes when the failure is recorded, or counted.</returns>
    public async ValueTask AuthenticationFailedAsync(string scheme, string reason, string remoteAddress, string? subject = null, string? issuer = null)
    {
        this.Logger?.LogWarning(
            "Audit: authentication failed for scheme {Scheme} from {RemoteAddress}: {Reason} (issuer {Issuer}, subject {Subject}).",
            scheme,
            remoteAddress,
            reason,
            issuer ?? "-",
            subject ?? "-");

        bool admitted = this.authenticationFailures.Admit(remoteAddress, this.timeProvider.GetUtcNow(), out long suppressedBefore, out string recordedAs);
        if (suppressedBefore > 0)
        {
            await this.AuthenticationFailuresSuppressedAsync(recordedAs, suppressedBefore).ConfigureAwait(false);
        }

        if (admitted && this.chain is not null)
        {
            await this.AppendReadAsync(new AuditEntry(scheme, subject ?? string.Empty, issuer, "remote", remoteAddress, reason, null, AuditEntryKind.Authentication), failClosed: false).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.refusalSweep is not null)
        {
            await this.refusalSweep.DisposeAsync().ConfigureAwait(false);
        }

        if (this.chain is not null)
        {
            await this.chain.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OnRefusalSweep() => _ = this.SweepRefusalsAsync();

    private async Task SweepRefusalsAsync()
    {
        try
        {
            var flushed = new List<KeyValuePair<string, long>>();
            this.refusals.Sweep(this.timeProvider.GetUtcNow(), flushed);
            foreach (KeyValuePair<string, long> subject in flushed)
            {
                await this.SuppressedAsync(subject.Key, subject.Value).ConfigureAwait(false);
            }

            flushed.Clear();
            this.authenticationFailures.Sweep(this.timeProvider.GetUtcNow(), flushed);
            foreach (KeyValuePair<string, long> address in flushed)
            {
                await this.AuthenticationFailuresSuppressedAsync(address.Key, address.Value).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
            // The auditor was disposed between the tick and the append.
        }
    }

    private ValueTask AuthenticationFailuresSuppressedAsync(string remoteAddress, long count)
    {
        this.Logger?.LogWarning("Audit: {Count} failed authentications from {RemoteAddress} were over its bound and were counted, not recorded one by one.", count, remoteAddress);
        return this.chain is null
            ? ValueTask.CompletedTask
            : this.AppendReadAsync(new AuditEntry("(any)", string.Empty, null, "remote", remoteAddress, "suppressed", null, AuditEntryKind.Authentication, count), failClosed: false);
    }

    // What the chain did not take one by one, it takes as a count: the subject, and how many of its refusals a window
    // suppressed. Like any refusal record it never fails anything.
    private ValueTask SuppressedAsync(string subject, long count)
    {
        this.Logger?.LogWarning("Audit: {Count} refused reads by {Actor} were over its bound and were counted, not recorded one by one.", count, subject);
        return this.chain is null
            ? ValueTask.CompletedTask
            : this.AppendReadAsync(new AuditEntry("read.refusals-suppressed", subject, null, "subject", subject, "suppressed", null, AuditEntryKind.Read, count), failClosed: false);
    }

    // A signed head is published outside the sink, through the span and the log, so that a collector holds anchors the
    // sink's owner cannot rewrite: a chain rewritten after this point cannot reproduce the head published here.
    private void PublishAnchor(AuditHead head)
    {
        this.Health.HeadSigned();
        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity(AnchorActivityName))
        {
            if (activity is not null)
            {
                activity.SetTag("corvus.arazzo.audit.chain", head.ChainId);
                activity.SetTag("corvus.arazzo.audit.sequence", head.Sequence);
                activity.SetTag("corvus.arazzo.audit.previous_hash", head.PreviousHash);
                activity.SetTag("corvus.arazzo.audit.algorithm", head.Algorithm);
                activity.SetTag("corvus.arazzo.audit.key_id", head.KeyId);
                activity.SetTag("corvus.arazzo.audit.signature", head.Signature);
            }
        }

        this.Logger?.LogInformation(
            "Audit anchor: chain {Chain} head at sequence {Sequence} over {PreviousHash}, signed {Algorithm} by {KeyId}: {Signature}",
            head.ChainId,
            head.Sequence,
            head.PreviousHash,
            head.Algorithm,
            head.KeyId,
            head.Signature);
    }

    // What the last process left behind. A torn last line is what a crash leaves and is continued from without comment
    // beyond the record of it. Anything else means the chain in the sink is not the chain that was written, which is for
    // the operator to look at; recording goes on, continued from the last record that verifies.
    private void Resumed(AuditChainVerification last)
    {
        if (last.Break is AuditChainBreak.None or AuditChainBreak.TornTail)
        {
            this.Logger?.LogInformation(
                "Audit: continuing chain {Chain} from its record {Records} ({Unsigned} of them after its last head, now frozen by the new chain's head).",
                last.ChainId,
                last.RecordCount,
                last.UnsignedTailCount);
        }
        else
        {
            this.Logger?.LogError(
                "Audit: this writer's last chain {Chain} FAILS verification at line {Line} ({Break}). It is continued from its last record that verifies, record {Records}. Verify the sink with arazzo-runs audit verify.",
                last.ChainId,
                last.BreakLine,
                last.Break,
                last.RecordCount);
        }
    }

    private void HeadFailed(Exception exception)
    {
        this.Health.HeadFailed();
        ArazzoTelemetry.AuditHeadFailures.Add(1);
        this.Logger?.LogError(exception, "Audit: the chain's head could not be signed or stored. Records are still chained, and the newest of them are not yet vouched for by a signature. The next cadence tick tries again.");
    }

    private async ValueTask AppendReadAsync(AuditEntry entry, bool failClosed)
    {
        try
        {
            await this.AppendAsync(entry, CancellationToken.None).ConfigureAwait(false);
        }
        catch (AuditAppendException ex)
        {
            // The failure is already counted, logged and on the health state. A refusal is answered as it would have
            // been; a disclosure is not made.
            if (failClosed)
            {
                throw ThrowHelper.GetAuditReadAppendFailedException(ex.InnerException ?? ex);
            }
        }
    }

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
                entry.Kind == AuditEntryKind.Read
                    ? "Audit: the record of the read {Action} of {TargetKind} {TargetId} by {Actor} (disclosure {Outcome}) could not be appended to the audit sink. A read that discloses a payload is refused; a refusal is answered as it was."
                    : "Audit: the record of {Action} on {TargetKind} {TargetId} by {Actor} (outcome {Outcome}) could not be appended to the audit sink. The action stands and is not in the chain.",
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