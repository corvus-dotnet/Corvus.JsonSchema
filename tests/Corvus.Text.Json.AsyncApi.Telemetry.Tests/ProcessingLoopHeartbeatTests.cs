// <copyright file="ProcessingLoopHeartbeatTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.AsyncApi.Telemetry.Tests;

[TestClass]
public class ProcessingLoopHeartbeatTests
{
    [TestMethod]
    public void Start_TracksChannel()
    {
        ProcessingLoopHeartbeat heartbeat = new();

        heartbeat.Start("orders/created", "nats", new object());

        Assert.IsTrue(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void IsAlive_ReturnsFalseForUnknownChannel()
    {
        ProcessingLoopHeartbeat heartbeat = new();

        Assert.IsFalse(heartbeat.IsAlive("unknown/channel"));
    }

    [TestMethod]
    public void Stop_MarksChannelAsNotAlive()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object owner = new();

        heartbeat.Start("orders/created", "kafka", owner);
        heartbeat.Stop("orders/created", "kafka", owner);

        Assert.IsFalse(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Stop_ByNonOwner_DoesNotStopLiveSubscription()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object winner = new();
        object loser = new();

        heartbeat.Start("orders/created", "kafka", winner);

        // A losing claim racer (or a loop unwinding after a resubscribe) stops with its own
        // identity; the winner's live subscription must not be marked stopped.
        heartbeat.Stop("orders/created", "kafka", loser);

        Assert.IsTrue(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Stop_ByPreviousOwner_AfterRestart_DoesNotStopNewSubscription()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object first = new();
        object second = new();

        heartbeat.Start("orders/created", "kafka", first);
        heartbeat.Start("orders/created", "kafka", second);

        // The old subscription's deferred teardown lands after the resubscribe.
        heartbeat.Stop("orders/created", "kafka", first);

        Assert.IsTrue(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Tick_KeepsChannelAlive()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object owner = new();

        heartbeat.Start("sensors/temperature", "mqtt", owner);
        heartbeat.Tick("sensors/temperature", "mqtt", owner);

        Assert.IsTrue(heartbeat.IsAlive("sensors/temperature"));
    }

    [TestMethod]
    public void Tick_WithoutStart_CreatesEntry()
    {
        ProcessingLoopHeartbeat heartbeat = new();

        heartbeat.Tick("sensors/temperature", "mqtt", new object());

        Assert.IsTrue(heartbeat.IsAlive("sensors/temperature"));
    }

    [TestMethod]
    public void Tick_ByNonOwner_DoesNotRefreshEntry()
    {
        ProcessingLoopHeartbeat heartbeat = new()
        {
            StalenessThreshold = TimeSpan.FromMilliseconds(50),
        };
        object owner = new();
        object stale = new();

        heartbeat.Start("orders/created", "amqp", owner);
        Thread.Sleep(100);

        // A draining handler from a torn-down subscription must not fake-freshen the entry.
        heartbeat.Tick("orders/created", "amqp", stale);

        Assert.IsFalse(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Tick_ByNonOwner_DoesNotResurrectStoppedEntry()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object owner = new();
        object stale = new();

        heartbeat.Start("orders/created", "amqp", owner);
        heartbeat.Stop("orders/created", "amqp", owner);
        heartbeat.Tick("orders/created", "amqp", stale);

        Assert.IsFalse(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void IsAlive_ReturnsFalseWhenStale()
    {
        ProcessingLoopHeartbeat heartbeat = new()
        {
            StalenessThreshold = TimeSpan.FromMilliseconds(50),
        };

        heartbeat.Start("orders/created", "amqp", new object());

        // Wait beyond the staleness threshold
        Thread.Sleep(100);

        Assert.IsFalse(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Tick_ResetsStaleTimer()
    {
        ProcessingLoopHeartbeat heartbeat = new()
        {
            StalenessThreshold = TimeSpan.FromMilliseconds(100),
        };
        object owner = new();

        heartbeat.Start("orders/created", "nats", owner);

        // Wait partway into the staleness window
        Thread.Sleep(60);

        // Tick resets the timer
        heartbeat.Tick("orders/created", "nats", owner);

        // Still alive because tick reset the timer
        Assert.IsTrue(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void Remove_RemovesChannelFromTracking()
    {
        ProcessingLoopHeartbeat heartbeat = new();

        heartbeat.Start("orders/created", "kafka", new object());
        heartbeat.Remove("orders/created");

        Assert.IsFalse(heartbeat.IsAlive("orders/created"));
    }

    [TestMethod]
    public void GetSubscriptionStatuses_ReturnsAllTrackedChannels()
    {
        ProcessingLoopHeartbeat heartbeat = new();
        object ownerB = new();

        heartbeat.Start("channel-a", "nats", new object());
        heartbeat.Start("channel-b", "kafka", ownerB);
        heartbeat.Stop("channel-b", "kafka", ownerB);

        IReadOnlyList<SubscriptionLoopStatus> statuses = heartbeat.GetSubscriptionStatuses();

        Assert.AreEqual(2, statuses.Count);

        SubscriptionLoopStatus statusA = statuses.Single(s => s.Channel == "channel-a");
        Assert.AreEqual("nats", statusA.MessagingSystem);
        Assert.IsTrue(statusA.IsAlive);
        Assert.IsTrue(statusA.IsRunning);

        SubscriptionLoopStatus statusB = statuses.Single(s => s.Channel == "channel-b");
        Assert.AreEqual("kafka", statusB.MessagingSystem);
        Assert.IsFalse(statusB.IsAlive);
        Assert.IsFalse(statusB.IsRunning);
    }

    [TestMethod]
    public void GetSubscriptionStatuses_ReportsStaleAsNotAlive()
    {
        ProcessingLoopHeartbeat heartbeat = new()
        {
            StalenessThreshold = TimeSpan.FromMilliseconds(50),
        };

        heartbeat.Start("stale-channel", "websocket", new object());
        Thread.Sleep(100);

        IReadOnlyList<SubscriptionLoopStatus> statuses = heartbeat.GetSubscriptionStatuses();
        SubscriptionLoopStatus status = statuses.Single();

        Assert.AreEqual("stale-channel", status.Channel);
        Assert.IsTrue(status.IsRunning);
        Assert.IsFalse(status.IsAlive);
        Assert.IsTrue(status.TimeSinceLastTick > TimeSpan.FromMilliseconds(50));
    }

    [TestMethod]
    public void Start_EmitsHeartbeatCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        ProcessingLoopHeartbeat heartbeat = new();
        heartbeat.Start("test/channel", "nats", new object());

        (string Name, long Value, KeyValuePair<string, object?>[] Tags) measurement =
            measurements.Single(m => m.Name == "corvus.asyncapi.heartbeats");

        Assert.AreEqual(1L, measurement.Value);
        Assert.AreEqual("nats", measurement.Tags.Single(t => t.Key == "messaging.system").Value);
        Assert.AreEqual("test/channel", measurement.Tags.Single(t => t.Key == "messaging.destination.name").Value);
        Assert.AreEqual("started", measurement.Tags.Single(t => t.Key == "corvus.asyncapi.loop_event").Value);
    }

    [TestMethod]
    public void Stop_EmitsHeartbeatCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        ProcessingLoopHeartbeat heartbeat = new();
        object owner = new();
        heartbeat.Start("test/channel", "kafka", owner);
        heartbeat.Stop("test/channel", "kafka", owner);

        (string Name, long Value, KeyValuePair<string, object?>[] Tags) stopped =
            measurements.Single(m => m.Name == "corvus.asyncapi.heartbeats" &&
                m.Tags.Any(t => t.Key == "corvus.asyncapi.loop_event" && (string?)t.Value == "stopped"));

        Assert.AreEqual(1L, stopped.Value);
        Assert.AreEqual("kafka", stopped.Tags.Single(t => t.Key == "messaging.system").Value);
    }

    [TestMethod]
    public void Tick_EmitsHeartbeatCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        ProcessingLoopHeartbeat heartbeat = new();
        heartbeat.Tick("test/channel", "mqtt", new object());

        (string Name, long Value, KeyValuePair<string, object?>[] Tags) tick =
            measurements.Single(m => m.Name == "corvus.asyncapi.heartbeats" &&
                m.Tags.Any(t => t.Key == "corvus.asyncapi.loop_event" && (string?)t.Value == "tick"));

        Assert.AreEqual(1L, tick.Value);
        Assert.AreEqual("mqtt", tick.Tags.Single(t => t.Key == "messaging.system").Value);
        Assert.AreEqual("test/channel", tick.Tags.Single(t => t.Key == "messaging.destination.name").Value);
    }

    [TestMethod]
    public void MultipleChannels_IndependentlyTracked()
    {
        ProcessingLoopHeartbeat heartbeat = new()
        {
            StalenessThreshold = TimeSpan.FromMilliseconds(50),
        };
        object ownerA = new();

        heartbeat.Start("channel-a", "nats", ownerA);
        heartbeat.Start("channel-b", "nats", new object());

        Thread.Sleep(100);

        // Only tick channel-a, so channel-b becomes stale
        heartbeat.Tick("channel-a", "nats", ownerA);

        Assert.IsTrue(heartbeat.IsAlive("channel-a"));
        Assert.IsFalse(heartbeat.IsAlive("channel-b"));
    }

    private static MeterListener CreateMeterListener(
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AsyncApiTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            measurements.Add((instrument.Name, value, tags.ToArray()));
        });
        listener.Start();
        return listener;
    }
}