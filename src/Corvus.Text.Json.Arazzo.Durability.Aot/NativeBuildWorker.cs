// <copyright file="NativeBuildWorker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The control-plane build worker that drives the asynchronous native-publish state machine one job at a time (ADR 0055):
/// it atomically claims the oldest queued <see cref="NativeBuildJob"/> (Queued -> Building), loads that version's package,
/// native-AOT compiles and signs its binary for the job's runtime target via <see cref="WorkflowAotBuildService"/>,
/// persists the attached binary back into the version (a metadata-only update that preserves the content hash), and
/// completes the job (Building -> Ready | Failed). It is host-agnostic — a poll loop (a hosted background service) calls
/// <see cref="DriveNextAsync"/> repeatedly — so the claim/complete concurrency and the build orchestration are testable
/// without a host, and any number of workers scale out safely because the claim is atomic.
/// </summary>
/// <remarks>
/// <para>The version's package flows through as <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> from load to build
/// to persist, never realised to a string; only the leaf identifiers the stores require as arguments (the job id, base
/// workflow id, runtime identifier, and etag) are realised, and the claimed job document is disposed before the long build
/// so its pooled buffer is not held across the compile.</para>
/// <para>A rebuild re-enqueues the same target (the id is derived from the target tuple), resetting the job to Queued; if
/// that happens while this worker is mid-build, the worker's completion no longer matches the job's etag and is abandoned
/// (<see cref="NativeBuildWorkerResult.WasSuperseded"/>), leaving the fresh Queued job for the next claim.</para>
/// </remarks>
public sealed class NativeBuildWorker
{
    // A build log or failure message is stored as the job's human-readable failureReason; bound it so a large compiler
    // log cannot bloat the job document. The tail is kept because a native-AOT compile reports its error at the end.
    private const int MaxFailureReasonLength = 4000;

    private readonly INativeBuildJobStore jobs;
    private readonly IWorkflowCatalogStore catalog;
    private readonly WorkflowAotBuildService buildService;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeBuildWorker"/> class.
    /// </summary>
    /// <param name="jobs">The build-job store the worker claims from and completes.</param>
    /// <param name="catalog">The catalog the worker loads a version's package from and persists the attached binary back into.</param>
    /// <param name="buildService">The build service that verifies, compiles, signs, and attaches the native binary for a target.</param>
    /// <param name="timeProvider">The time source driving the lease-renewal heartbeat; defaults to <see cref="TimeProvider.System"/>.</param>
    public NativeBuildWorker(INativeBuildJobStore jobs, IWorkflowCatalogStore catalog, WorkflowAotBuildService buildService, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(buildService);
        this.jobs = jobs;
        this.catalog = catalog;
        this.buildService = buildService;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Claims and processes the next queued build job, if any: atomically claims the oldest queued job, builds and
    /// attaches its native binary, persists the update, and completes the job. Returns <see cref="NativeBuildWorkerResult.Idle"/>
    /// when nothing is queued, so a poll loop can back off. A bad build input or a native-AOT compile failure is recorded
    /// as a <see cref="NativeBuildJobStatus.Failed"/> completion (never thrown), so one bad job does not stall the worker;
    /// a cancellation is propagated (the claimed job is left Building for a later reclaim, not marked failed).
    /// </summary>
    /// <param name="workerId">The identifier of this worker, stamped as the job's <c>claimedBy</c> for audit.</param>
    /// <param name="leaseTtl">How long the claiming worker's advisory lease is held before another worker may reclaim the job as an orphan (ADR 0056).</param>
    /// <param name="leaseRenewalInterval">How often the worker renews its lease while the build runs; must be comfortably below <paramref name="leaseTtl"/>.</param>
    /// <param name="cancellationToken">A cancellation token; a cancellation leaves any claimed job Building for reclaim.</param>
    /// <returns>The outcome of the cycle: idle, a completion (Ready or Failed), or a superseded completion.</returns>
    public async ValueTask<NativeBuildWorkerResult> DriveNextAsync(string workerId, TimeSpan leaseTtl, TimeSpan leaseRenewalInterval, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        string id;
        string baseWorkflowId;
        int versionNumber;
        string runtimeIdentifier;
        WorkflowEtag claimedEtag;

        // Claim the oldest claimable job (Queued, or an orphaned Building — ADR 0056) and read the leaf identifiers the
        // stores need, then release the pooled claim document before the long build so its buffer is not rented across it.
        using (ParsedJsonDocument<NativeBuildJob>? claimedDoc = await this.jobs.ClaimNextQueuedAsync(workerId, leaseTtl, cancellationToken).ConfigureAwait(false))
        {
            if (claimedDoc is not { } claimed)
            {
                return NativeBuildWorkerResult.Idle;
            }

            NativeBuildJob job = claimed.RootElement;
            id = job.IdValue;
            baseWorkflowId = job.BaseWorkflowIdValue;
            versionNumber = job.VersionNumberValue;
            runtimeIdentifier = job.RuntimeIdentifierValue;
            claimedEtag = job.EtagValue;
        }

        // Renew the lease on a heartbeat while the (minutes-long) build runs so no other worker reclaims this job as an
        // orphan (ADR 0056). Each renewal bumps the etag, so the heartbeat threads the current etag forward and the
        // completion uses the latest; the heartbeat is stopped before completing so that etag is stable. A renewal that
        // conflicts means another worker reclaimed an expired lease (this worker lost it): the heartbeat records that and the
        // cycle abandons its completion as superseded (the etag would reject it anyway, but this avoids a wasted round trip).
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
                        using ParsedJsonDocument<NativeBuildJob>? renewed = await this.jobs.RenewLeaseAsync(id, expected, leaseTtl, stopHeartbeat.Token).ConfigureAwait(false);
                        if (renewed is not { } r)
                        {
                            lock (etagGate)
                            {
                                leaseLost = true; // the job vanished (deleted mid-build)
                            }

                            return;
                        }

                        lock (etagGate)
                        {
                            currentEtag = r.RootElement.EtagValue;
                        }
                    }
                    catch (NativeBuildJobConflictException)
                    {
                        lock (etagGate)
                        {
                            leaseLost = true; // reclaimed by another worker
                        }

                        return;
                    }
                    catch (NativeBuildJobStateException)
                    {
                        lock (etagGate)
                        {
                            leaseLost = true; // no longer Building (reclaimed then re-completed)
                        }

                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The build finished (or a shutdown): stop renewing.
            }
        }

        Task heartbeat = RenewLeaseLoopAsync();
        NativeBuildJobCompletion completion;
        try
        {
            completion = await this.DetermineCompletionAsync(id, baseWorkflowId, versionNumber, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
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
            return NativeBuildWorkerResult.Superseded(id, runtimeIdentifier);
        }

        return await this.CompleteAsync(id, runtimeIdentifier, completion, finalEtag, cancellationToken).ConfigureAwait(false);
    }

    // Loads the package, builds and attaches the native binary, and persists it, returning the terminal completion to apply
    // (Ready, or Failed with a reason). A bad input or a native-AOT compile failure becomes a Failed completion (never
    // thrown), so one bad job does not stall the worker; a shutdown cancellation is propagated (the job is left Building for
    // a later reclaim, not marked failed). This runs under the lease heartbeat driven by the caller.
    private async ValueTask<NativeBuildJobCompletion> DetermineCompletionAsync(string id, string baseWorkflowId, int versionNumber, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        // Load the version's package (carrying the signed executor); its bytes flow straight into the build, never a string.
        ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (package is not { } packageBytes)
        {
            return new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, VersionGoneReason(baseWorkflowId, versionNumber));
        }

        WorkflowAotBuildOutcome outcome;
        try
        {
            outcome = await this.buildService.BuildAndAttachAsync(packageBytes, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A shutdown/cancellation: leave the job Building for a later reclaim rather than recording a spurious failure.
            throw;
        }
        catch (Exception ex)
        {
            // Bad input (an unsigned, tampered, or untrusted executor: WorkflowAotBuildException) or an unexpected builder
            // failure must terminate the job as Failed with the reason, so it is not orphaned in the Building state.
            return new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, Summarize(ex.Message));
        }

        if (!outcome.Succeeded)
        {
            // A clean native-AOT compile failure (an AOT-cleanliness gap): record the build log as the failure reason.
            return new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, Summarize(outcome.Log));
        }

        // Persist the attached binary back into the version (metadata-only: the content hash is unchanged, ADR 0055).
        bool updated;
        try
        {
            updated = await this.catalog.UpdatePackageAsync(baseWorkflowId, versionNumber, outcome.Package, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // The update would change immutable content — a logic error (the build attaches only metadata); record it as
            // Failed rather than looping, since a retry would fail identically.
            return new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, Summarize(ex.Message));
        }

        return updated
            ? new NativeBuildJobCompletion(NativeBuildJobStatus.Ready)
            : new NativeBuildJobCompletion(NativeBuildJobStatus.Failed, VersionGoneReason(baseWorkflowId, versionNumber));
    }

    // The message for a version that no longer exists (deleted between enqueue and build, or mid-build).
    private static string VersionGoneReason(string baseWorkflowId, int versionNumber)
        => $"The version '{baseWorkflowId}' v{versionNumber} no longer exists.";

    // Bounds a build log or failure message to the job's failureReason field, keeping the tail (where a compile reports its
    // error) and marking a truncation. The failure reason is an inherently human-readable string on a cold path, not a hot
    // seam, so realising it here is appropriate; the version's package itself never becomes a string.
    private static string Summarize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "The native build failed without producing a diagnostic message.";
        }

        return message.Length <= MaxFailureReasonLength
            ? message
            : string.Concat("...(truncated)\n", message.AsSpan(message.Length - MaxFailureReasonLength));
    }

    private async ValueTask<NativeBuildWorkerResult> CompleteAsync(string id, string runtimeIdentifier, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        try
        {
            using ParsedJsonDocument<NativeBuildJob>? completed = await this.jobs.CompleteAsync(id, completion, expectedEtag, cancellationToken).ConfigureAwait(false);
            return NativeBuildWorkerResult.Completed(id, runtimeIdentifier, completion.Status, completion.FailureReason);
        }
        catch (NativeBuildJobConflictException)
        {
            // The job was re-enqueued (a rebuild reset it to Queued) while this build ran: the etag no longer matches, so
            // this completion is stale. Abandon it; the fresh Queued job is claimed on the next cycle.
            return NativeBuildWorkerResult.Superseded(id, runtimeIdentifier);
        }
        catch (NativeBuildJobStateException)
        {
            // The job is no longer Building (a re-enqueue then re-claim by another worker moved it on): the same
            // supersession, reached through a state check rather than an etag check.
            return NativeBuildWorkerResult.Superseded(id, runtimeIdentifier);
        }
    }
}

/// <summary>
/// The outcome of a single <see cref="NativeBuildWorker.DriveNextAsync"/> cycle: whether a job was claimed, and if so how it
/// resolved — a terminal completion (<see cref="NativeBuildJobStatus.Ready"/> or <see cref="NativeBuildJobStatus.Failed"/>)
/// or a superseded completion abandoned because the job was re-enqueued mid-build. A poll loop uses <see cref="Claimed"/> to
/// decide whether to keep draining or back off, and the rest to log the result.
/// </summary>
public readonly record struct NativeBuildWorkerResult
{
    private NativeBuildWorkerResult(bool claimed, string? jobId, string? runtimeIdentifier, NativeBuildJobStatus? outcome, string? failureReason, bool wasSuperseded)
    {
        this.Claimed = claimed;
        this.JobId = jobId;
        this.RuntimeIdentifier = runtimeIdentifier;
        this.Outcome = outcome;
        this.FailureReason = failureReason;
        this.WasSuperseded = wasSuperseded;
    }

    /// <summary>Gets a value indicating whether a job was claimed this cycle. When <see langword="false"/>, nothing was queued and the loop can back off.</summary>
    public bool Claimed { get; }

    /// <summary>Gets the claimed job's id, or <see langword="null"/> when idle.</summary>
    public string? JobId { get; }

    /// <summary>Gets the claimed job's runtime target, or <see langword="null"/> when idle.</summary>
    public string? RuntimeIdentifier { get; }

    /// <summary>Gets the terminal status the job was completed with (<see cref="NativeBuildJobStatus.Ready"/> or
    /// <see cref="NativeBuildJobStatus.Failed"/>), or <see langword="null"/> when idle or superseded.</summary>
    public NativeBuildJobStatus? Outcome { get; }

    /// <summary>Gets the failure reason when <see cref="Outcome"/> is <see cref="NativeBuildJobStatus.Failed"/>; otherwise <see langword="null"/>.</summary>
    public string? FailureReason { get; }

    /// <summary>Gets a value indicating whether the completion was abandoned because the job was re-enqueued (a rebuild) while this cycle was building.</summary>
    public bool WasSuperseded { get; }

    /// <summary>Gets the idle result: no job was queued.</summary>
    public static NativeBuildWorkerResult Idle => default;

    /// <summary>Creates a completed result carrying the terminal status the job was completed with.</summary>
    /// <param name="jobId">The completed job's id.</param>
    /// <param name="runtimeIdentifier">The job's runtime target.</param>
    /// <param name="outcome">The terminal status (Ready or Failed).</param>
    /// <param name="failureReason">The failure reason for a Failed outcome; otherwise <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public static NativeBuildWorkerResult Completed(string jobId, string runtimeIdentifier, NativeBuildJobStatus outcome, string? failureReason)
        => new(true, jobId, runtimeIdentifier, outcome, failureReason, false);

    /// <summary>Creates a superseded result: a job was claimed and built, but its completion was abandoned because the job was re-enqueued mid-build.</summary>
    /// <param name="jobId">The claimed job's id.</param>
    /// <param name="runtimeIdentifier">The job's runtime target.</param>
    /// <returns>The result.</returns>
    public static NativeBuildWorkerResult Superseded(string jobId, string runtimeIdentifier)
        => new(true, jobId, runtimeIdentifier, null, null, true);
}