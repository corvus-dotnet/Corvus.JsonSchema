// <copyright file="ProcessingLoopHeartbeat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Tracks the liveness of transport processing loops by recording heartbeat ticks.
/// </summary>
/// <remarks>
/// <para>
/// Each transport's subscribe/consume loop calls <see cref="Tick(string, string)"/> on
/// every iteration to signal that the loop is alive and processing. The heartbeat
/// monitor tracks these ticks and exposes:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IsAlive(string)"/> — whether a specific subscription's
/// loop has ticked within the staleness threshold.</description></item>
/// <item><description><see cref="GetSubscriptionStatuses"/> — all tracked subscriptions
/// with their liveness state.</description></item>
/// <item><description>A <c>corvus.asyncapi.processing_loop_alive</c> counter emitted on
/// each tick, useful for rate-based alerting (rate drops to zero = loop died).</description></item>
/// <item><description>A <c>corvus.asyncapi.processing_loop_started</c> /
/// <c>corvus.asyncapi.processing_loop_stopped</c> counter pair for lifecycle tracking.</description></item>
/// </list>
/// <para>
/// Staleness detection: if a loop has not ticked for longer than
/// <see cref="StalenessThreshold"/>, it is considered dead. This catches loops that
/// exit silently (unhandled exception, unexpected cancellation, GC of the Task).
/// </para>
/// <para>
/// Usage in transports:
/// <code>
/// // At the top of each loop iteration:
/// this.heartbeat.Tick(channel, "nats");
///
/// // On subscribe start:
/// this.heartbeat.Start(channel, "nats");
///
/// // On normal unsubscribe:
/// this.heartbeat.Stop(channel, "nats");
/// </code>
/// </para>
/// </remarks>
public sealed class ProcessingLoopHeartbeat
{
    private readonly ConcurrentDictionary<string, LoopState> loops = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the duration after which a loop is considered dead if no tick has been received.
    /// </summary>
    /// <remarks>
    /// The default is 60 seconds. Set higher for low-throughput channels where messages
    /// may arrive infrequently.
    /// </remarks>
    public TimeSpan StalenessThreshold { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Records that the processing loop for the specified channel has started.
    /// </summary>
    /// <param name="channel">The channel/topic/subject being subscribed to.</param>
    /// <param name="messagingSystem">The transport type (e.g., <c>"nats"</c>, <c>"kafka"</c>).</param>
    public void Start(string channel, string messagingSystem)
    {
        long now = Stopwatch.GetTimestamp();
        this.loops[channel] = new LoopState(messagingSystem, now, Running: true);

        AsyncApiTelemetry.Heartbeats.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.loop_event", "started" },
            });
    }

    /// <summary>
    /// Records that the processing loop for the specified channel has stopped normally
    /// (e.g., due to an explicit unsubscribe).
    /// </summary>
    /// <param name="channel">The channel/topic/subject being unsubscribed from.</param>
    /// <param name="messagingSystem">The transport type.</param>
    public void Stop(string channel, string messagingSystem)
    {
        if (this.loops.TryGetValue(channel, out LoopState state))
        {
            this.loops[channel] = state with { Running = false };
        }

        AsyncApiTelemetry.Heartbeats.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.loop_event", "stopped" },
            });
    }

    /// <summary>
    /// Records a heartbeat tick from the processing loop, indicating it is alive.
    /// </summary>
    /// <param name="channel">The channel/topic/subject being processed.</param>
    /// <param name="messagingSystem">The transport type.</param>
    /// <remarks>
    /// Call this at the top of each loop iteration. It is zero-cost when no
    /// <see cref="MeterListener"/> is attached — the only overhead is updating
    /// the <see cref="Stopwatch"/> timestamp.
    /// </remarks>
    public void Tick(string channel, string messagingSystem)
    {
        long now = Stopwatch.GetTimestamp();
        this.loops.AddOrUpdate(
            channel,
            static (_, args) => new LoopState(args.MessagingSystem, args.Now, Running: true),
            static (_, existing, args) => existing with { LastTickTimestamp = args.Now },
            (MessagingSystem: messagingSystem, Now: now));

        AsyncApiTelemetry.Heartbeats.Add(
            1,
            new TagList
            {
                { "messaging.system", messagingSystem },
                { "messaging.destination.name", channel },
                { "corvus.asyncapi.loop_event", "tick" },
            });
    }

    /// <summary>
    /// Gets a value indicating whether the processing loop for the specified channel
    /// is considered alive (has ticked within <see cref="StalenessThreshold"/>).
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><see langword="true"/> if the loop is alive; <see langword="false"/> if
    /// it has not ticked recently or is not tracked.</returns>
    public bool IsAlive(string channel)
    {
        if (!this.loops.TryGetValue(channel, out LoopState state))
        {
            return false;
        }

        if (!state.Running)
        {
            return false;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(state.LastTickTimestamp);
        return elapsed <= this.StalenessThreshold;
    }

    /// <summary>
    /// Gets the status of all tracked subscriptions.
    /// </summary>
    /// <returns>A snapshot of all subscription loop states.</returns>
    public IReadOnlyList<SubscriptionLoopStatus> GetSubscriptionStatuses()
    {
        List<SubscriptionLoopStatus> results = new(this.loops.Count);

        foreach (KeyValuePair<string, LoopState> entry in this.loops)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(entry.Value.LastTickTimestamp);
            bool alive = entry.Value.Running && elapsed <= this.StalenessThreshold;

            results.Add(new SubscriptionLoopStatus(
                entry.Key,
                entry.Value.MessagingSystem,
                alive,
                entry.Value.Running,
                elapsed));
        }

        return results;
    }

    /// <summary>
    /// Removes a subscription from tracking (e.g., after it has been stopped and cleaned up).
    /// </summary>
    /// <param name="channel">The channel to remove from tracking.</param>
    public void Remove(string channel)
    {
        this.loops.TryRemove(channel, out _);
    }

    private record struct LoopState(string MessagingSystem, long LastTickTimestamp, bool Running);
}

/// <summary>
/// Represents the current status of a subscription's processing loop.
/// </summary>
/// <param name="Channel">The channel/topic/subject.</param>
/// <param name="MessagingSystem">The transport type.</param>
/// <param name="IsAlive">Whether the loop has ticked within the staleness threshold.</param>
/// <param name="IsRunning">Whether the loop has been marked as running (not explicitly stopped).</param>
/// <param name="TimeSinceLastTick">Time elapsed since the last tick.</param>
public readonly record struct SubscriptionLoopStatus(
    string Channel,
    string MessagingSystem,
    bool IsAlive,
    bool IsRunning,
    TimeSpan TimeSinceLastTick);