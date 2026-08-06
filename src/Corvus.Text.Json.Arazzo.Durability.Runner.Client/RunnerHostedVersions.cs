// <copyright file="RunnerHostedVersions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// The versioned workflow ids this runner can execute, resolved over the runner API and reused for a bounded window
/// (ADR 0065).
/// </summary>
/// <remarks>
/// <para>
/// This is the runner's single answer to "what have I baked", and every path that claims work asks it: dispatch, the
/// due-timer sweep, and message delivery. Each computing its own would let them disagree, and the disagreement would be
/// invisible — a listener resuming a run for a version dispatch would not have touched faults for want of an executor,
/// which reads as a workflow failure rather than a wiring one.
/// </para>
/// <para>
/// It is cached because message delivery is a hot path. Resolving per message would put a round trip on every delivery
/// and, since the runner API meters catalog reads, would spend a quota on each one. It is refreshed rather than held
/// because a newly published version must be picked up, and a revoked binding must drop away, without a restart.
/// </para>
/// <para>
/// The set is what the control plane resolves from this runner's environment bindings, so it is already reach-scoped.
/// The runner adds only what the control plane cannot know: the reserved scheduler id, which is not a catalogued
/// version, and only on a runner wired to resume one.
/// </para>
/// </remarks>
public sealed class RunnerHostedVersions
{
    private static readonly string[] None = [];

    private readonly ArazzoRunnerClient client;
    private readonly bool servesSchedules;
    private readonly TimeSpan refreshWindow;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string[] cached = None;
    private DateTimeOffset expiresAt = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of the <see cref="RunnerHostedVersions"/> class.</summary>
    /// <param name="client">The runner's client for the runner API.</param>
    /// <param name="servesSchedules">Whether this runner has the scheduler wired into its resumer. A schedule run claimed
    /// by a runner that cannot resume it would simply fault, so the reserved id is added only when it can.</param>
    /// <param name="refreshWindow">How long a resolution is reused. Defaults to two seconds, matching the dispatch
    /// loop's poll interval: that is the tightest existing consumer, so nothing becomes staler than it was today, and
    /// the delivery path still stops resolving per message.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public RunnerHostedVersions(ArazzoRunnerClient client, bool servesSchedules, TimeSpan? refreshWindow = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        this.client = client;
        this.servesSchedules = servesSchedules;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.refreshWindow = refreshWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromSeconds(2);
    }

    /// <summary>The versioned workflow ids this runner can execute.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids. Empty is normal and not an error: a runner bound to nothing, or one whose authorization is
    /// pending or revoked, can execute nothing and should claim nothing.</returns>
    public async ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken)
    {
        if (this.timeProvider.GetUtcNow() < this.expiresAt)
        {
            return this.cached;
        }

        // One resolution at a time. Without this a burst of messages arriving together on an expired window would each
        // start their own listing, which is the load the cache exists to prevent, at exactly the moment it matters.
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            if (now < this.expiresAt)
            {
                return this.cached;
            }

            IReadOnlyList<RunnerHostedVersion> hosted = await this.client.ListHostedVersionsAsync(cancellationToken).ConfigureAwait(false);

            var ids = new string[hosted.Count + (this.servesSchedules ? 1 : 0)];
            for (int i = 0; i < hosted.Count; ++i)
            {
                ids[i] = hosted[i].ToWorkflowId();
            }

            if (this.servesSchedules)
            {
                // A durable schedule is a run of the built-in scheduler workflow rather than a catalogued version, so
                // its reserved id is never in the listing.
                ids[^1] = ScheduleHostedWorkflow.ScheduleWorkflowId;
            }

            this.cached = ids;
            this.expiresAt = now + this.refreshWindow;
            return ids;
        }
        finally
        {
            this.gate.Release();
        }
    }
}