// <copyright file="InstrumentedMessageTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Telemetry.Tests;

[TestClass]
public class InstrumentedMessageTransportTests
{
    private static readonly byte[] TestChannel = "test/channel"u8.ToArray();
    private static readonly byte[] TestPayloadJson = """{"temperature": 22.5}"""u8.ToArray();

    [TestMethod]
    public async Task PublishAsync_CreatesActivityWithCorrectTags()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        JsonElement noHeaders = default;
        await transport.PublishAsync(TestChannel, in payload, in noHeaders, default);

        Assert.AreEqual(1, activities.Count);
        Activity activity = activities[0];
        Assert.AreEqual("send test/channel", activity.OperationName);
        Assert.AreEqual(ActivityKind.Producer, activity.Kind);
        Assert.AreEqual("in-memory", activity.GetTagItem("messaging.system"));
        Assert.AreEqual("send", activity.GetTagItem("messaging.operation.type"));
        Assert.AreEqual("send", activity.GetTagItem("messaging.operation.name"));
        Assert.AreEqual("test/channel", activity.GetTagItem("messaging.destination.name"));
    }

    [TestMethod]
    public async Task PublishAsync_IncrementsMessagesSentCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "nats");

        JsonElement payload = JsonElement.ParseValue("""{"id": 1}"""u8.ToArray());
        JsonElement noHeaders = default;
        await transport.PublishAsync(TestChannel, in payload, in noHeaders, default);

        var sent = measurements.Where(m => m.Name == "messaging.client.sent.messages").ToList();
        Assert.AreEqual(1, sent.Count);
        Assert.AreEqual(1L, sent[0].Value);
        AssertTag(sent[0].Tags, "messaging.system", "nats");
        AssertTag(sent[0].Tags, "messaging.operation.name", "send");
        AssertTag(sent[0].Tags, "messaging.destination.name", "test/channel");
    }

    [TestMethod]
    public async Task PublishAsync_RecordsOperationDuration()
    {
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> histograms = [];
        using MeterListener meterListener = CreateHistogramListener(histograms);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "kafka");

        JsonElement payload = JsonElement.ParseValue("""{"x": true}"""u8.ToArray());
        JsonElement noHeaders = default;
        await transport.PublishAsync(TestChannel, in payload, in noHeaders, default);

        var durations = histograms.Where(m => m.Name == "messaging.client.operation.duration").ToList();
        Assert.AreEqual(1, durations.Count);
        Assert.IsTrue(durations[0].Value >= 0);
        AssertTag(durations[0].Tags, "messaging.operation.name", "send");
    }

    [TestMethod]
    public async Task PublishAsync_OnException_SetsActivityStatusError()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        FailingTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "failing");

        JsonElement payload = JsonElement.ParseValue("""{"v": 1}"""u8.ToArray());
        JsonElement noHeaders = default;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await transport.PublishAsync(TestChannel, in payload, in noHeaders, default));

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual(ActivityStatusCode.Error, activities[0].Status);
        Assert.AreEqual("System.InvalidOperationException", activities[0].GetTagItem("error.type"));
    }

    [TestMethod]
    public async Task SubscribeAsync_WrapsHandlerWithProcessActivity()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "mqtt");

        bool handlerCalled = false;
        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) =>
            {
                handlerCalled = true;
                return ValueTask.CompletedTask;
            },
            default);

        await inner.DeliverAsync<JsonElement>("test/channel", TestPayloadJson);

        Assert.IsTrue(handlerCalled);
        var processActivities = activities.Where(a => a.OperationName.StartsWith("process")).ToList();
        Assert.AreEqual(1, processActivities.Count);
        Assert.AreEqual(ActivityKind.Consumer, processActivities[0].Kind);
        Assert.AreEqual("mqtt", processActivities[0].GetTagItem("messaging.system"));
        Assert.AreEqual("process", processActivities[0].GetTagItem("messaging.operation.name"));
    }

    [TestMethod]
    public async Task SubscribeAsync_IncrementsMessagesConsumedCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "amqp");

        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) => ValueTask.CompletedTask,
            default);

        await inner.DeliverAsync<JsonElement>("test/channel", TestPayloadJson);

        var consumed = measurements.Where(m => m.Name == "messaging.client.consumed.messages").ToList();
        Assert.AreEqual(1, consumed.Count);
        AssertTag(consumed[0].Tags, "messaging.system", "amqp");
    }

    [TestMethod]
    public async Task DeadLetterAsync_IncrementsDeadLetterCounter()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using MeterListener meterListener = CreateMeterListener(measurements);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "kafka");

        byte[] dlqChannel = "test/dlq"u8.ToArray();
        JsonElement payload = JsonElement.ParseValue("""{"bad": true}"""u8.ToArray());
        JsonElement noHeaders = default;
        Exception exception = new InvalidOperationException("processing failed");

        await transport.DeadLetterAsync(dlqChannel, TestChannel, in payload, in noHeaders, exception, default);

        var deadLetters = measurements.Where(m => m.Name == "corvus.asyncapi.dead_letters").ToList();
        Assert.AreEqual(1, deadLetters.Count);
        AssertTag(deadLetters[0].Tags, "messaging.destination.name", "test/dlq");
        AssertTag(deadLetters[0].Tags, "corvus.asyncapi.original_channel", "test/channel");
    }

    [TestMethod]
    public async Task DeadLetterAsync_CreatesActivityWithOriginalChannel()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "nats");

        byte[] dlqChannel = "dead-letters/sensor"u8.ToArray();
        JsonElement payload = JsonElement.ParseValue("""{"x": 1}"""u8.ToArray());
        JsonElement noHeaders = default;
        Exception exception = new FormatException("bad data");

        await transport.DeadLetterAsync(dlqChannel, TestChannel, in payload, in noHeaders, exception, default);

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual("dead-letter dead-letters/sensor", activities[0].OperationName);
        Assert.AreEqual("test/channel", activities[0].GetTagItem("corvus.asyncapi.original_channel"));
        Assert.AreEqual("System.FormatException", activities[0].GetTagItem("error.type"));
    }

    [TestMethod]
    public async Task RequestAsync_CreatesActivityWithConversationId()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "nats");

        byte[] replyChannel = "reply/channel"u8.ToArray();
        byte[] correlationId = "corr-123"u8.ToArray();
        JsonElement request = JsonElement.ParseValue("""{"query": "status"}"""u8.ToArray());

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));
        JsonElement noHeaders = default;
        try
        {
            await transport.RequestAsync<JsonElement, JsonElement>(
                TestChannel, replyChannel, in request, correlationId, in noHeaders, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — no responder configured
        }

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual("request test/channel", activities[0].OperationName);
        Assert.AreEqual("corr-123", activities[0].GetTagItem("messaging.message.conversation_id"));
    }

    [TestMethod]
    public async Task NoListenerAttached_CompletesWithoutError()
    {
        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "nats");

        JsonElement payload = JsonElement.ParseValue("""{"no": "listener"}"""u8.ToArray());
        JsonElement noHeaders = default;
        await transport.PublishAsync(TestChannel, in payload, in noHeaders, default);
    }

    [TestMethod]
    public async Task UnsubscribeAsync_DelegatesToInner()
    {
        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "test");

        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) => ValueTask.CompletedTask,
            default);

        await transport.UnsubscribeAsync(TestChannel, default);

        bool called = false;
        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) =>
            {
                called = true;
                return ValueTask.CompletedTask;
            },
            default);

        await inner.DeliverAsync<JsonElement>("test/channel", TestPayloadJson);
        Assert.IsTrue(called);
    }

    [TestMethod]
    public async Task DisposeAsync_DelegatesToInner()
    {
        DisposableTrackingTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "test");

        Assert.IsFalse(inner.Disposed);
        await transport.DisposeAsync();
        Assert.IsTrue(inner.Disposed);
    }

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AsyncApiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
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

    private static MeterListener CreateHistogramListener(
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> histograms)
    {
        MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AsyncApiTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            histograms.Add((instrument.Name, value, tags.ToArray()));
        });
        listener.Start();
        return listener;
    }

    private static void AssertTag(KeyValuePair<string, object?>[] tags, string key, string expectedValue)
    {
        var tag = tags.FirstOrDefault(t => t.Key == key);
        Assert.IsNotNull(tag.Key, $"Tag '{key}' not found in measurement tags.");
        Assert.AreEqual(expectedValue, tag.Value?.ToString());
    }

    private sealed class FailingTransport : IMessageTransport
    {
        public ValueTask PublishAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            in TPayload payload,
            in JsonElement headers,
            CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            throw new InvalidOperationException("Simulated failure");
        }

        public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> requestChannelUtf8,
            ReadOnlyMemory<byte> replyChannelUtf8,
            in TRequest request,
            ReadOnlyMemory<byte> correlationIdUtf8,
            in JsonElement headers,
            CancellationToken cancellationToken)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
        {
            throw new InvalidOperationException("Simulated failure");
        }

        public ValueTask SubscribeAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask UnsubscribeAsync(
            ReadOnlyMemory<byte> channelUtf8,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DeadLetterAsync(
            ReadOnlyMemory<byte> deadLetterChannelUtf8,
            ReadOnlyMemory<byte> originalChannelUtf8,
            in JsonElement payload,
            in JsonElement headers,
            Exception exception,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposableTrackingTransport : IMessageTransport
    {
        public bool Disposed { get; private set; }

        public ValueTask PublishAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            in TPayload payload,
            in JsonElement headers,
            CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> requestChannelUtf8,
            ReadOnlyMemory<byte> replyChannelUtf8,
            in TRequest request,
            ReadOnlyMemory<byte> correlationIdUtf8,
            in JsonElement headers,
            CancellationToken cancellationToken)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
        {
            return ValueTask.FromResult<(TReply, JsonElement)>(default);
        }

        public ValueTask SubscribeAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask UnsubscribeAsync(
            ReadOnlyMemory<byte> channelUtf8,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DeadLetterAsync(
            ReadOnlyMemory<byte> deadLetterChannelUtf8,
            ReadOnlyMemory<byte> originalChannelUtf8,
            in JsonElement payload,
            in JsonElement headers,
            Exception exception,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            this.Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}