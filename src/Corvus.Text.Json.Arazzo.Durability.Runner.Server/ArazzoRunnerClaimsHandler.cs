// <copyright file="ArazzoRunnerClaimsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Implements the runner API's claim operation: the only way a runner is given work.
/// </summary>
public sealed class ArazzoRunnerClaimsHandler : IApiClaimsHandler
{
    private readonly RunnerRunCoordinator coordinator;
    private readonly RunnerPrincipalAccessor principals;
    private readonly RunnerQuotaGate quotas;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClaimsHandler"/> class.</summary>
    /// <param name="coordinator">The store-facing coordinator.</param>
    /// <param name="principals">Reads the authenticated machine principal from the current request.</param>
    /// <param name="quotas">Meters the deployment's claim quota.</param>
    public ArazzoRunnerClaimsHandler(RunnerRunCoordinator coordinator, RunnerPrincipalAccessor principals, RunnerQuotaGate quotas)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(principals);
        ArgumentNullException.ThrowIfNull(quotas);

        this.coordinator = coordinator;
        this.principals = principals;
        this.quotas = quotas;
    }

    /// <inheritdoc/>
    public async ValueTask<ClaimRunResult> HandleClaimRunAsync(ClaimRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ClaimRunResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        // A claim returns the run's row, so it is a bulk read path in its own right and not merely a scheduling call
        // (ADR 0065). The batch cap bounds one request; this bounds their rate.
        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Claim, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return ClaimRunResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        // The hosted versions are realised as strings here and nowhere earlier: this is the leaf where they become the
        // dispatch index's query, and every backend's query takes strings.
        ClaimRequest.HostedVersionsEntityArray hostedVersions = parameters.Body.HostedVersions;
        var hosted = new List<string>(hostedVersions.GetArrayLength());
        foreach (ClaimRequest.HostedVersionsEntityArray.HostedVersionsEntity version in hostedVersions.EnumerateArray())
        {
            hosted.Add((string)version);
        }

        TimeSpan? requestedLease = parameters.Body.LeaseSeconds.IsNotUndefined()
            ? TimeSpan.FromSeconds((long)parameters.Body.LeaseSeconds)
            : null;

        ClaimedRunRecord? claimed = await this.coordinator.TryClaimAsync(principal, hosted, requestedLease, cancellationToken).ConfigureAwait(false);
        if (claimed is not { } run)
        {
            // Nothing claimable is the common case for an idle runner, and is not an error.
            return ClaimRunResult.NoContent();
        }

        return ClaimRunResult.Ok(
            ClaimedRun.Build(
                environment: run.Environment,
                lease: LeaseGrant.Build(epoch: run.Lease.Epoch, expiresAt: run.Lease.ExpiresAt, token: run.Lease.Token),
                runId: run.RunId.Value,
                workflowId: run.WorkflowId),
            workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ClaimDueTimersResult> HandleClaimDueTimersAsync(ClaimDueTimersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ClaimDueTimersResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        // A claim returns the run's row, so it is a bulk read path in its own right and not merely a scheduling call
        // (ADR 0065). The batch cap bounds one request; this bounds their rate.
        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Claim, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return ClaimDueTimersResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        List<string> hosted = Realise(parameters.Body.HostedVersions);
        IReadOnlyList<ClaimedRunRecord> claims = await this.coordinator.ClaimDueAsync(
            principal,
            hosted,
            Limit(parameters.Body.Limit),
            Lease(parameters.Body.LeaseSeconds),
            cancellationToken).ConfigureAwait(false);

        return ClaimDueTimersResult.Ok(Project(claims), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ClaimAwaitingMessageResult> HandleClaimAwaitingMessageAsync(ClaimAwaitingMessageParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ClaimAwaitingMessageResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        // A claim returns the run's row, so it is a bulk read path in its own right and not merely a scheduling call
        // (ADR 0065). The batch cap bounds one request; this bounds their rate.
        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Claim, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return ClaimAwaitingMessageResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        List<string> hosted = Realise(parameters.Body.HostedVersions);

        // An absent correlation id is a wildcard over the channel: it matches every run awaiting it, whatever
        // correlation each is waiting for. It is passed through as null so the store applies that rule, rather than
        // being turned into a value here.
        string? correlationId = parameters.Body.CorrelationId.IsNotUndefined()
            ? (string)parameters.Body.CorrelationId
            : null;

        IReadOnlyList<ClaimedRunRecord> claims = await this.coordinator.ClaimAwaitingAsync(
            principal,
            (string)parameters.Body.Channel,
            correlationId,
            hosted,
            Limit(parameters.Body.Limit),
            Lease(parameters.Body.LeaseSeconds),
            cancellationToken).ConfigureAwait(false);

        return ClaimAwaitingMessageResult.Ok(Project(claims), workspace);
    }

    // Threaded rather than captured: the claims are the builder's context, so projecting a sweep allocates no closure
    // however many runs it returns.
    private static ClaimedRuns.Source<IReadOnlyList<ClaimedRunRecord>> Project(IReadOnlyList<ClaimedRunRecord> claims)
        => ClaimedRuns.Build(
            in claims,
            ClaimedRuns.ClaimedRunArray.Build(
                in claims,
                static (in IReadOnlyList<ClaimedRunRecord> source, ref ClaimedRuns.ClaimedRunArray.Builder builder) =>
                {
                    foreach (ClaimedRunRecord run in source)
                    {
                        builder.AddItem(ClaimedRun.Build(
                            environment: run.Environment,
                            lease: LeaseGrant.Build(epoch: run.Lease.Epoch, expiresAt: run.Lease.ExpiresAt, token: run.Lease.Token),
                            runId: run.RunId.Value,
                            workflowId: run.WorkflowId));
                    }
                }));

    // The hosted versions become strings here and nowhere earlier: this is the leaf where they become the wait index's
    // query, and every backend's query takes strings.
    private static List<string> Realise(TimerClaimRequest.HostedVersionsEntityArray hostedVersions)
    {
        var hosted = new List<string>(hostedVersions.GetArrayLength());
        foreach (TimerClaimRequest.HostedVersionsEntityArray.HostedVersionsEntity version in hostedVersions.EnumerateArray())
        {
            hosted.Add((string)version);
        }

        return hosted;
    }

    private static List<string> Realise(MessageClaimRequest.HostedVersionsEntityArray hostedVersions)
    {
        var hosted = new List<string>(hostedVersions.GetArrayLength());
        foreach (MessageClaimRequest.HostedVersionsEntityArray.HostedVersionsEntity version in hostedVersions.EnumerateArray())
        {
            hosted.Add((string)version);
        }

        return hosted;
    }

    private static int? Limit(TimerClaimRequest.LimitEntity limit) => limit.IsNotUndefined() ? (int)limit : null;

    private static int? Limit(MessageClaimRequest.LimitEntity limit) => limit.IsNotUndefined() ? (int)limit : null;

    private static TimeSpan? Lease(TimerClaimRequest.LeaseSecondsEntity seconds) => seconds.IsNotUndefined() ? TimeSpan.FromSeconds((long)seconds) : null;

    private static TimeSpan? Lease(MessageClaimRequest.LeaseSecondsEntity seconds) => seconds.IsNotUndefined() ? TimeSpan.FromSeconds((long)seconds) : null;
}