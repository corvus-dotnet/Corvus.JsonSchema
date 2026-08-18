// <copyright file="ProcessingLoopHeartbeat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Tracks the liveness of transport processing loops by recording heartbeat ticks.
/// </summary>
/// <remarks>
/// <para>
/// Each transport's subscribe/consume loop calls <see cref="Tick(string, string, object)"/> on
/// every iteration to signal that the loop is alive and processing. The heartbeat
/// monitor tracks these ticks and exposes:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IsAlive(string)"/> — whether a specific subscription's
/// loop has ticked within the staleness threshold.</description></item>
/// <item><description><see cref="GetSubscriptionStatuses"/> — all tracked subscriptions
/// with their liveness state.</description></item>
/// <item><description>A <c>corvus.asyncapi.heartbeats</c> counter emitted on every start,
/// stop and tick, distinguished by the <c>corvus.asyncapi.loop_event</c> tag — the tick
/// rate dropping to zero for a channel means its loop died.</description></item>
/// </list>
/// <para>
/// Staleness detection: if a loop has not ticked for longer than
/// <see cref="StalenessThreshold"/>, it is considered dead. This catches loops that
/// exit silently (unhandled exception, unexpected cancellation, GC of the Task).
/// </para>
/// <para>
/// Every call carries an <c>owner</c> object identifying the subscription it belongs to.
/// A channel can be resubscribed while the previous subscription is still unwinding (its
/// loop draining, its broker teardown deferred), so a bare channel key is not enough:
/// without the owner, the old subscription's <see cref="Stop(string, string, object)"/>
/// would clobber the new subscription's live state, and its final ticks would fake-freshen
/// it. <see cref="Start(string, string, object)"/> records the owner; <see cref="Stop(string, string, object)"/>
/// and <see cref="Tick(string, string, object)"/> only act when the entry still belongs to
/// that owner.
/// </para>
/// <para>
/// Usage in transports:
/// <code>
/// // At the top of each loop iteration:
/// this.heartbeat.Tick(channel, "nats", subscriptionMarker);
///
/// // After the subscription claim succeeds:
/// this.heartbeat.Start(channel, "nats", subscriptionMarker);
///
/// // When the subscription ends (unsubscribe, loop exit):
/// this.heartbeat.Stop(channel, "nats", subscriptionMarker);
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
    /// <param name="owner">The identity of the subscription this loop belongs to, compared by
    /// reference: pass the same object instance to <see cref="Start"/>, <see cref="Tick"/>, and
    /// <see cref="Stop"/> for one subscription (a dedicated marker object, as the transports use).
    /// A boxed value type is a fresh box per call and every call is ignored as a stale owner; an
    /// instance shared across subscriptions defeats the ownership check.</param>
    /// <remarks>
    /// Overwrites unconditionally: a transport only starts the heartbeat after its
    /// subscription claim succeeds, and a channel has a single claimed owner at a time, so
    /// an overwrite is always a legitimate ownership transfer to a new subscription.
    /// </remarks>
    public void Start(string channel, string messagingSystem, object owner)
    {
        long now = Stopwatch.GetTimestamp();
        this.loops[channel] = new LoopState(messagingSystem, owner, now);

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
    /// <param name="owner">The identity of the subscription being stopped, compared by reference
    /// with the instance passed to <see cref="Start"/>.</param>
    /// <remarks>
    /// Only lands if the entry still belongs to <paramref name="owner"/>: a losing claim
    /// racer or a loop unwinding after a resubscribe must not mark the new owner's live
    /// subscription as stopped. The stop transitions the owning state exactly once via an
    /// interlocked exchange, so a concurrent tick cannot swallow it, and a concurrent
    /// <see cref="Start(string, string, object)"/> by a new owner replaces the entry, leaving
    /// this stop to land harmlessly on the superseded state.
    /// </remarks>
    public void Stop(string channel, string messagingSystem, object owner)
    {
        // A stop that does not land (a losing racer, an unwinding predecessor, an entry already
        // stopped) emits no event, so the started/stopped pairing tracks real transitions.
        if (!this.loops.TryGetValue(channel, out LoopState? state)
            || !ReferenceEquals(state.Owner, owner)
            || !state.TryStop())
        {
            return;
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
    /// <param name="owner">The identity of the subscription this tick belongs to, compared by
    /// reference with the instance passed to <see cref="Start"/>.</param>
    /// <remarks>
    /// <para>
    /// Call this at the top of each loop iteration. It is allocation-free on the steady
    /// state — a dictionary read, a reference comparison, and a volatile timestamp store —
    /// and the counter add is a no-op when no <see cref="MeterListener"/> is attached.
    /// </para>
    /// <para>
    /// Only refreshes an entry that still belongs to <paramref name="owner"/>: a draining
    /// handler from a torn-down subscription must not fake-freshen its replacement's
    /// liveness (refreshing a state object that <see cref="Start(string, string, object)"/>
    /// has already replaced is harmless — it is no longer the one liveness reads). Loops
    /// start ticking before their claim is recorded, so the first tick may create the
    /// entry; a losing racer's entry converges to the winner because Start overwrites and
    /// mismatched ticks are ignored.
    /// </para>
    /// </remarks>
    public void Tick(string channel, string messagingSystem, object owner)
    {
        long now = Stopwatch.GetTimestamp();
        if (!this.loops.TryGetValue(channel, out LoopState? state))
        {
            state = this.loops.GetOrAdd(channel, new LoopState(messagingSystem, owner, now));
        }

        // A tick that was ignored (another subscription owns the entry) emits no event, so
        // the tick rate reflects only the owning subscription's loop. Owner identity is the
        // test — a timestamp comparison would false-positive when two ticks share a
        // Stopwatch quantum.
        if (!ReferenceEquals(state.Owner, owner))
        {
            return;
        }

        state.Tick(now);

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
        if (!this.loops.TryGetValue(channel, out LoopState? state))
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

    // Owner is compared by reference, which makes the owner gate a true identity check on
    // the owning subscription. This is a class rather than a record struct so a tick can
    // refresh the timestamp in place with a volatile store: a struct value forced
    // ConcurrentDictionary to allocate a replacement node on every tick, once per delivered
    // message on every transport with a heartbeat configured.
    private sealed class LoopState
    {
        private long lastTickTimestamp;
        private int running;

        public LoopState(string messagingSystem, object owner, long timestamp)
        {
            this.MessagingSystem = messagingSystem;
            this.Owner = owner;
            this.lastTickTimestamp = timestamp;
            this.running = 1;
        }

        public string MessagingSystem { get; }

        public object Owner { get; }

        public long LastTickTimestamp => Volatile.Read(ref this.lastTickTimestamp);

        public bool Running => Volatile.Read(ref this.running) == 1;

        public void Tick(long timestamp) => Volatile.Write(ref this.lastTickTimestamp, timestamp);

        public bool TryStop() => Interlocked.Exchange(ref this.running, 0) == 1;
    }
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