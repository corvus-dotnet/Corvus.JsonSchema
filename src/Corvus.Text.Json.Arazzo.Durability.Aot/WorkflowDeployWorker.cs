// <copyright file="WorkflowDeployWorker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The runner-side deploy worker that drives the asynchronous deploy state machine one deployment at a time (ADR 0059):
/// it atomically claims the oldest queued <see cref="WorkflowDeployment"/> (Queued -> Deploying), loads that version's
/// package, verifies the signed native binary's attestation and deploys it to the function platform via
/// <see cref="WorkflowDeployService"/>, and completes the deployment (Deploying -> Deployed | Failed), recording the
/// deployed function's invoke URL on success. It runs on the runner because the runner is the secure boundary that holds
/// the environment's cloud identity, and the control plane holds no user cloud secrets (ADR 0059); the shared deployment
/// store is the boundary the control-plane build worker enqueues into (deploy-on-build) and the control-plane dispatch
/// gate reads from. It is host-agnostic — a poll loop (a hosted background service) calls <see cref="DriveNextAsync"/>
/// repeatedly — so the claim/complete concurrency and the deploy orchestration are testable without a host, and any
/// number of workers scale out safely because the claim is atomic.
/// </summary>
/// <remarks>
/// <para>The version's package flows through as <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> from load to deploy,
/// never realised to a string; only the leaf identifiers the stores require as arguments (the deployment id, base workflow
/// id, environment, runtime identifier, and etag) are realised, and the claimed deployment document is disposed before the
/// deploy so its pooled buffer is not held across it.</para>
/// <para>A redeploy re-enqueues the same target (the id is derived from the target tuple), resetting the deployment to
/// Queued; if that happens while this worker is mid-deploy, the worker's completion no longer matches the deployment's
/// etag and is abandoned (<see cref="WorkflowDeployWorkerResult.WasSuperseded"/>), leaving the fresh Queued deployment for
/// the next claim.</para>
/// </remarks>
public sealed class WorkflowDeployWorker
{
    // A deploy log or failure message is stored as the deployment's human-readable failureReason; bound it so a large
    // platform log cannot bloat the deployment document. The tail is kept because a deploy reports its error at the end.
    private const int MaxFailureReasonLength = 4000;

    private readonly IWorkflowDeploymentStore deployments;
    private readonly IWorkflowCatalogStore catalog;
    private readonly WorkflowDeployService deployService;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDeployWorker"/> class.
    /// </summary>
    /// <param name="deployments">The deployment store the worker claims from and completes.</param>
    /// <param name="catalog">The catalog the worker loads a version's package (carrying the signed native binary) from.</param>
    /// <param name="deployService">The deploy service that verifies the native binary and deploys it to the function platform.</param>
    /// <param name="timeProvider">The time source driving the lease-renewal heartbeat; defaults to <see cref="TimeProvider.System"/>.</param>
    public WorkflowDeployWorker(IWorkflowDeploymentStore deployments, IWorkflowCatalogStore catalog, WorkflowDeployService deployService, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(deployService);
        this.deployments = deployments;
        this.catalog = catalog;
        this.deployService = deployService;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Claims and processes the next queued deployment, if any: atomically claims the oldest queued deployment, verifies
    /// and deploys its native binary, and completes the deployment. Returns <see cref="WorkflowDeployWorkerResult.Idle"/>
    /// when nothing is queued, so a poll loop can back off. A missing version or a deploy failure (a platform error or a
    /// bad, unverifiable binary) is recorded as a <see cref="WorkflowDeploymentStatus.Failed"/> completion (never thrown),
    /// so one bad deployment does not stall the worker; a cancellation is propagated (the claimed deployment is left
    /// Deploying for a later reclaim, not marked failed).
    /// </summary>
    /// <param name="workerId">The identifier of this worker, stamped as the deployment's <c>claimedBy</c> for audit.</param>
    /// <param name="leaseTtl">How long the claiming worker's advisory lease is held before another worker may reclaim the deployment as an orphan (ADR 0056).</param>
    /// <param name="leaseRenewalInterval">How often the worker renews its lease while the deploy runs; must be comfortably below <paramref name="leaseTtl"/>.</param>
    /// <param name="cancellationToken">A cancellation token; a cancellation leaves any claimed deployment Deploying for reclaim.</param>
    /// <returns>The outcome of the cycle: idle, a completion (Deployed or Failed), or a superseded completion.</returns>
    public async ValueTask<WorkflowDeployWorkerResult> DriveNextAsync(string workerId, TimeSpan leaseTtl, TimeSpan leaseRenewalInterval, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        string id;
        string baseWorkflowId;
        int versionNumber;
        string environment;
        string runtimeIdentifier;
        WorkflowEtag claimedEtag;

        // Claim the oldest claimable deployment (Queued, or an orphaned Deploying — ADR 0056) and read the leaf identifiers
        // the stores need, then release the pooled claim document before the deploy so its buffer is not rented across it.
        using (ParsedJsonDocument<WorkflowDeployment>? claimedDoc = await this.deployments.ClaimNextQueuedAsync(workerId, leaseTtl, cancellationToken).ConfigureAwait(false))
        {
            if (claimedDoc is not { } claimed)
            {
                return WorkflowDeployWorkerResult.Idle;
            }

            WorkflowDeployment deployment = claimed.RootElement;
            id = deployment.IdValue;
            baseWorkflowId = deployment.BaseWorkflowIdValue;
            versionNumber = deployment.VersionNumberValue;
            environment = deployment.EnvironmentValue;
            runtimeIdentifier = deployment.RuntimeIdentifierValue;
            claimedEtag = deployment.EtagValue;
        }

        // Renew the lease on a heartbeat while the deploy runs (a cloud create-plus-wait-active can take a while) so no
        // other worker reclaims this deployment as an orphan (ADR 0056). Each renewal bumps the etag, so the heartbeat
        // threads the current etag forward and the completion uses the latest; the heartbeat is stopped before completing
        // so that etag is stable. A renewal that conflicts means another worker reclaimed an expired lease (this worker lost
        // it): the heartbeat records that and the cycle abandons its completion as superseded (the etag would reject it
        // anyway, but this avoids a wasted round trip).
        Lock etagGate = new();
        WorkflowEtag currentEtag = claimedEtag;
        bool leaseLost = false;
        using var stopHeartbeat = new CancellationTokenSource();

        async Task RenewLeaseLoopAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(leaseRenewalInterval, this.timeProvider);
                while (await timer.WaitForNextTickAsync(stopHeartbeat.Token).ConfigureAwait(false))
                {
                    WorkflowEtag expected;
                    lock (etagGate)
                    {
                        expected = currentEtag;
                    }

                    try
                    {
                        using ParsedJsonDocument<WorkflowDeployment>? renewed = await this.deployments.RenewLeaseAsync(id, expected, leaseTtl, stopHeartbeat.Token).ConfigureAwait(false);
                        if (renewed is not { } r)
                        {
                            lock (etagGate)
                            {
                                leaseLost = true; // the deployment vanished (deleted mid-deploy)
                            }

                            return;
                        }

                        lock (etagGate)
                        {
                            currentEtag = r.RootElement.EtagValue;
                        }
                    }
                    catch (WorkflowDeploymentConflictException)
                    {
                        lock (etagGate)
                        {
                            leaseLost = true; // reclaimed by another worker
                        }

                        return;
                    }
                    catch (WorkflowDeploymentStateException)
                    {
                        lock (etagGate)
                        {
                            leaseLost = true; // no longer Deploying (reclaimed then re-completed)
                        }

                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The deploy finished (or a shutdown): stop renewing.
            }
        }

        Task heartbeat = RenewLeaseLoopAsync();
        WorkflowDeploymentCompletion completion;
        try
        {
            completion = await this.DetermineCompletionAsync(baseWorkflowId, versionNumber, environment, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Stop the heartbeat and let it drain before reading the final etag, so no renewal races the completion.
            await stopHeartbeat.CancelAsync().ConfigureAwait(false);
            await heartbeat.ConfigureAwait(false);
        }

        WorkflowEtag finalEtag;
        bool lost;
        lock (etagGate)
        {
            finalEtag = currentEtag;
            lost = leaseLost;
        }

        if (lost)
        {
            return WorkflowDeployWorkerResult.Superseded(id, runtimeIdentifier);
        }

        return await this.CompleteAsync(id, runtimeIdentifier, completion, finalEtag, cancellationToken).ConfigureAwait(false);
    }

    // Loads the package and deploys its verified native binary, returning the terminal completion to apply (Deployed with
    // the function URL, or Failed with a reason). A missing version, a bad, unverifiable binary, or a platform deploy
    // failure becomes a Failed completion (never thrown), so one bad deployment does not stall the worker; a shutdown
    // cancellation is propagated (the deployment is left Deploying for a later reclaim, not marked failed). This runs under
    // the lease heartbeat driven by the caller.
    private async ValueTask<WorkflowDeploymentCompletion> DetermineCompletionAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        // Load the version's package (carrying the signed native binary); its bytes flow straight into the deploy, never a string.
        ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (package is not { } packageBytes)
        {
            return new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: VersionGoneReason(baseWorkflowId, versionNumber));
        }

        WorkflowDeployOutcome outcome;
        try
        {
            outcome = await this.deployService.DeployAsync(packageBytes, baseWorkflowId, versionNumber, environment, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A shutdown/cancellation: leave the deployment Deploying for a later reclaim rather than recording a spurious failure.
            throw;
        }
        catch (Exception ex)
        {
            // A bad, unverifiable binary (a missing, unsigned, tampered, or swapped native binary: WorkflowAotBuildException)
            // or an unexpected deployer failure must terminate the deployment as Failed with the reason, so it is not orphaned
            // in the Deploying state. A well-formed platform rejection is already returned as a non-successful outcome below.
            return new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: Summarize(ex.Message));
        }

        return outcome.Succeeded
            ? new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, FunctionUrl: outcome.FunctionUrl)
            : new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Failed, FailureReason: Summarize(outcome.Log));
    }

    // The message for a version that no longer exists (deleted between enqueue and deploy, or mid-deploy).
    private static string VersionGoneReason(string baseWorkflowId, int versionNumber)
        => $"The version '{baseWorkflowId}' v{versionNumber} no longer exists.";

    // Bounds a deploy log or failure message to the deployment's failureReason field, keeping the tail (where a deploy
    // reports its error) and marking a truncation. The failure reason is an inherently human-readable string on a cold
    // path, not a hot seam, so realising it here is appropriate; the version's package itself never becomes a string.
    private static string Summarize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "The deploy failed without producing a diagnostic message.";
        }

        return message.Length <= MaxFailureReasonLength
            ? message
            : string.Concat("...(truncated)\n", message.AsSpan(message.Length - MaxFailureReasonLength));
    }

    private async ValueTask<WorkflowDeployWorkerResult> CompleteAsync(string id, string runtimeIdentifier, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        try
        {
            using ParsedJsonDocument<WorkflowDeployment>? completed = await this.deployments.CompleteAsync(id, completion, expectedEtag, cancellationToken).ConfigureAwait(false);
            return WorkflowDeployWorkerResult.Completed(id, runtimeIdentifier, completion.Status, completion.FunctionUrl, completion.FailureReason);
        }
        catch (WorkflowDeploymentConflictException)
        {
            // The deployment was re-enqueued (a redeploy reset it to Queued) while this deploy ran: the etag no longer
            // matches, so this completion is stale. Abandon it; the fresh Queued deployment is claimed on the next cycle.
            return WorkflowDeployWorkerResult.Superseded(id, runtimeIdentifier);
        }
        catch (WorkflowDeploymentStateException)
        {
            // The deployment is no longer Deploying (a re-enqueue then re-claim by another worker moved it on): the same
            // supersession, reached through a state check rather than an etag check.
            return WorkflowDeployWorkerResult.Superseded(id, runtimeIdentifier);
        }
    }
}

/// <summary>
/// The outcome of a single <see cref="WorkflowDeployWorker.DriveNextAsync"/> cycle: whether a deployment was claimed, and if
/// so how it resolved — a terminal completion (<see cref="WorkflowDeploymentStatus.Deployed"/> or
/// <see cref="WorkflowDeploymentStatus.Failed"/>) or a superseded completion abandoned because the deployment was
/// re-enqueued mid-deploy. A poll loop uses <see cref="Claimed"/> to decide whether to keep draining or back off, and the
/// rest to log the result.
/// </summary>
public readonly record struct WorkflowDeployWorkerResult
{
    private WorkflowDeployWorkerResult(bool claimed, string? deploymentId, string? runtimeIdentifier, WorkflowDeploymentStatus? outcome, string? functionUrl, string? failureReason, bool wasSuperseded)
    {
        this.Claimed = claimed;
        this.DeploymentId = deploymentId;
        this.RuntimeIdentifier = runtimeIdentifier;
        this.Outcome = outcome;
        this.FunctionUrl = functionUrl;
        this.FailureReason = failureReason;
        this.WasSuperseded = wasSuperseded;
    }

    /// <summary>Gets a value indicating whether a deployment was claimed this cycle. When <see langword="false"/>, nothing was queued and the loop can back off.</summary>
    public bool Claimed { get; }

    /// <summary>Gets the claimed deployment's id, or <see langword="null"/> when idle.</summary>
    public string? DeploymentId { get; }

    /// <summary>Gets the claimed deployment's runtime target, or <see langword="null"/> when idle.</summary>
    public string? RuntimeIdentifier { get; }

    /// <summary>Gets the terminal status the deployment was completed with (<see cref="WorkflowDeploymentStatus.Deployed"/>
    /// or <see cref="WorkflowDeploymentStatus.Failed"/>), or <see langword="null"/> when idle or superseded.</summary>
    public WorkflowDeploymentStatus? Outcome { get; }

    /// <summary>Gets the deployed function's invoke URL when <see cref="Outcome"/> is <see cref="WorkflowDeploymentStatus.Deployed"/>; otherwise <see langword="null"/>.</summary>
    public string? FunctionUrl { get; }

    /// <summary>Gets the failure reason when <see cref="Outcome"/> is <see cref="WorkflowDeploymentStatus.Failed"/>; otherwise <see langword="null"/>.</summary>
    public string? FailureReason { get; }

    /// <summary>Gets a value indicating whether the completion was abandoned because the deployment was re-enqueued (a redeploy) while this cycle was deploying.</summary>
    public bool WasSuperseded { get; }

    /// <summary>Gets the idle result: no deployment was queued.</summary>
    public static WorkflowDeployWorkerResult Idle => default;

    /// <summary>Creates a completed result carrying the terminal status the deployment was completed with.</summary>
    /// <param name="deploymentId">The completed deployment's id.</param>
    /// <param name="runtimeIdentifier">The deployment's runtime target.</param>
    /// <param name="outcome">The terminal status (Deployed or Failed).</param>
    /// <param name="functionUrl">The deployed function's invoke URL for a Deployed outcome; otherwise <see langword="null"/>.</param>
    /// <param name="failureReason">The failure reason for a Failed outcome; otherwise <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public static WorkflowDeployWorkerResult Completed(string deploymentId, string runtimeIdentifier, WorkflowDeploymentStatus outcome, string? functionUrl, string? failureReason)
        => new(true, deploymentId, runtimeIdentifier, outcome, functionUrl, failureReason, false);

    /// <summary>Creates a superseded result: a deployment was claimed and deployed, but its completion was abandoned because the deployment was re-enqueued mid-deploy.</summary>
    /// <param name="deploymentId">The claimed deployment's id.</param>
    /// <param name="runtimeIdentifier">The deployment's runtime target.</param>
    /// <returns>The result.</returns>
    public static WorkflowDeployWorkerResult Superseded(string deploymentId, string runtimeIdentifier)
        => new(true, deploymentId, runtimeIdentifier, null, null, null, true);
}