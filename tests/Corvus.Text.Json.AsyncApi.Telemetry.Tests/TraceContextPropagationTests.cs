// <copyright file="TraceContextPropagationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Telemetry.Tests;

[TestClass]
public class TraceContextPropagationTests
{
    private static readonly byte[] TestChannel = "events/temperature"u8.ToArray();
    private static readonly byte[] TestPayloadJson = """{"value":22.5}"""u8.ToArray();

    [TestMethod]
    public async Task PublishAsync_InjectsTraceparentIntoHeaders()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        await transport.PublishAsync(TestChannel, in payload, default, default);

        Assert.AreEqual(1, inner.PublishedMessages.Count);
        byte[] headerBytes = inner.PublishedMessages[0].HeaderBytes;

        // Headers should contain traceparent
        Assert.IsTrue(headerBytes.Length > 0, "Expected headers with traceparent to be injected");

        JsonElement headers = JsonElement.ParseValue(headerBytes);
        Assert.AreEqual(JsonValueKind.Object, headers.ValueKind);
        Assert.IsTrue(headers.TryGetProperty("traceparent"u8, out JsonElement traceparentProp));
        Assert.AreEqual(JsonValueKind.String, traceparentProp.ValueKind);

        string? traceparent = traceparentProp.GetString();
        Assert.IsNotNull(traceparent);

        // W3C traceparent format: 00-<traceId>-<spanId>-<flags>
        Assert.IsTrue(traceparent.StartsWith("00-"), $"traceparent should start with version '00-': {traceparent}");
    }

    [TestMethod]
    public async Task PublishAsync_PreservesExistingHeaders()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        JsonElement existingHeaders = JsonElement.ParseValue("""{"correlationId":"abc-123","source":"sensor-1"}""");
        await transport.PublishAsync(TestChannel, in payload, in existingHeaders, default);

        Assert.AreEqual(1, inner.PublishedMessages.Count);
        byte[] headerBytes = inner.PublishedMessages[0].HeaderBytes;

        JsonElement headers = JsonElement.ParseValue(headerBytes);

        // Original headers preserved
        Assert.IsTrue(headers.TryGetProperty("correlationId"u8, out JsonElement corrId));
        Assert.AreEqual("abc-123", corrId.GetString());
        Assert.IsTrue(headers.TryGetProperty("source"u8, out JsonElement source));
        Assert.AreEqual("sensor-1", source.GetString());

        // traceparent injected
        Assert.IsTrue(headers.TryGetProperty("traceparent"u8, out JsonElement tp));
        Assert.AreEqual(JsonValueKind.String, tp.ValueKind);
    }

    [TestMethod]
    public async Task PublishAsync_NoListener_DoesNotInjectHeaders()
    {
        // No ActivityListener registered — Activity.Current will be null
        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        await transport.PublishAsync(TestChannel, in payload, default, default);

        Assert.AreEqual(1, inner.PublishedMessages.Count);
        byte[] headerBytes = inner.PublishedMessages[0].HeaderBytes;

        // No trace context injected when no listener is active
        Assert.AreEqual(0, headerBytes.Length);
    }

    [TestMethod]
    public async Task SubscribeHandler_ExtractsTraceparentAsParent()
    {
        // Set up a known trace context in headers
        const string expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
        const string expectedParentSpanId = "b7ad6b7169203331";
        string traceparent = $"00-{expectedTraceId}-{expectedParentSpanId}-01";

        byte[] headersJson = Encoding.UTF8.GetBytes(
            $$"""{"traceparent":"{{traceparent}}"}""");

        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        TaskCompletionSource handlerCalled = new();

        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) =>
            {
                handlerCalled.SetResult();
                return ValueTask.CompletedTask;
            },
            default);

        // Deliver message with traceparent in headers
        await inner.DeliverAsync<JsonElement>("events/temperature", TestPayloadJson, headersJson);
        await handlerCalled.Task;

        // The consumer activity should have the injected trace as parent
        Activity? processActivity = activities.FirstOrDefault(a => a.OperationName == "process events/temperature");
        Assert.IsNotNull(processActivity, "Expected a 'process' activity");
        Assert.AreEqual(expectedTraceId, processActivity.TraceId.ToString());
        Assert.AreEqual(expectedParentSpanId, processActivity.ParentSpanId.ToString());
    }

    [TestMethod]
    public async Task SubscribeHandler_NoTraceparent_CreatesIndependentSpan()
    {
        byte[] headersJson = """{"source":"sensor-1"}"""u8.ToArray();

        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        TaskCompletionSource handlerCalled = new();

        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) =>
            {
                handlerCalled.SetResult();
                return ValueTask.CompletedTask;
            },
            default);

        await inner.DeliverAsync<JsonElement>("events/temperature", TestPayloadJson, headersJson);
        await handlerCalled.Task;

        Activity? processActivity = activities.FirstOrDefault(a => a.OperationName == "process events/temperature");
        Assert.IsNotNull(processActivity, "Expected a 'process' activity");

        // ParentSpanId should be default (no parent extracted from headers)
        Assert.AreEqual(default(ActivitySpanId), processActivity.ParentSpanId);
    }

    [TestMethod]
    public async Task PublishAndSubscribe_EndToEnd_PropagatesTraceContext()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        // Subscribe first
        TaskCompletionSource handlerCalled = new();
        await transport.SubscribeAsync<JsonElement>(
            TestChannel,
            (payload, headers, ct) =>
            {
                handlerCalled.SetResult();
                return ValueTask.CompletedTask;
            },
            default);

        // Publish — this will inject traceparent and auto-deliver to subscriber
        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        await transport.PublishAsync(TestChannel, in payload, default, default);

        // Wait for the handler to be called via auto-delivery
        await handlerCalled.Task;

        Activity? sendActivity = activities.FirstOrDefault(a => a.OperationName == "send events/temperature");
        Activity? processActivity = activities.FirstOrDefault(a => a.OperationName == "process events/temperature");

        Assert.IsNotNull(sendActivity, "Expected a 'send' activity");
        Assert.IsNotNull(processActivity, "Expected a 'process' activity");

        // The consumer span should be a child of the producer span's trace
        Assert.AreEqual(sendActivity.TraceId, processActivity.TraceId,
            "Consumer should share the same TraceId as producer");
        Assert.AreEqual(sendActivity.SpanId, processActivity.ParentSpanId,
            "Consumer's parent should be the producer's span");
    }

    [TestMethod]
    public async Task PublishAsync_InjectsTracestate_WhenPresent()
    {
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        // Start a parent activity with tracestate set
        using Activity? parent = new Activity("test-parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        parent!.TraceStateString = "congo=t61rcWkgMzE";

        await using InMemoryMessageTransport inner = new();
        InstrumentedMessageTransport transport = new(inner, "in-memory");

        JsonElement payload = JsonElement.ParseValue(TestPayloadJson);
        await transport.PublishAsync(TestChannel, in payload, default, default);

        byte[] headerBytes = inner.PublishedMessages[0].HeaderBytes;
        JsonElement headers = JsonElement.ParseValue(headerBytes);

        Assert.IsTrue(headers.TryGetProperty("tracestate"u8, out JsonElement tracestateProp));
        Assert.AreEqual("congo=t61rcWkgMzE", tracestateProp.GetString());

        parent.Stop();
    }

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AsyncApiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}