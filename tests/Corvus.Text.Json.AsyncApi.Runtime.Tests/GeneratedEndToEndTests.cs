// <copyright file="GeneratedEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Streetlights.Client;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// End-to-end tests that exercise the generated producer and consumer code through
/// the <see cref="InMemoryMessageTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that:
/// <list type="bullet">
/// <item>Producers serialize payloads correctly via the transport.</item>
/// <item>Channel address templates are populated with parameter values.</item>
/// <item>Consumers invoke handlers with deserialized typed payloads.</item>
/// <item>Schema validation is applied at both producer and consumer boundaries.</item>
/// <item>Validation mode <c>None</c> skips validation entirely.</item>
/// <item>Validation mode <c>Detailed</c> produces detailed error messages.</item>
/// </list>
/// </para>
/// </remarks>
[TestClass]
public class GeneratedEndToEndTests
{
    private const string LightMeasurementChannel =
        "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_SerializesPayloadToChannel()
    {
        await using InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.None);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8);

        await producer.PublishTurnOnOffAsync(payload, "lamp-42");

        Assert.AreEqual(1, transport.PublishedMessages.Count);

        InMemoryMessageTransport.PublishedMessage msg = transport.PublishedMessages[0];
        Assert.AreEqual(
            "smartylighting.streetlights.1.0.action.lamp-42.turn.on",
            msg.Channel);

        string payloadJson = Encoding.UTF8.GetString(msg.PayloadBytes);
        Assert.AreEqual("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}""", payloadJson);
    }

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_DifferentStreetlightId_UsesCorrectChannel()
    {
        await using InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.None);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"off","sentAt":"2024-06-15T12:00:00Z"}"""u8);

        await producer.PublishTurnOnOffAsync(payload, "streetlight-99");

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        Assert.AreEqual(
            "smartylighting.streetlights.1.0.action.streetlight-99.turn.on",
            transport.PublishedMessages[0].Channel);
    }

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_BasicValidation_ValidPayload_Succeeds()
    {
        await using InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.Basic);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8);

        await producer.PublishTurnOnOffAsync(payload, "lamp-1");

        Assert.AreEqual(1, transport.PublishedMessages.Count);
    }

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_BasicValidation_InvalidPayload_Throws()
    {
        await using InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.Basic);

        // Invalid: "command" must be "on" or "off" per the enum constraint
        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"invalid","sentAt":"2024-01-01T00:00:00Z"}"""u8);

        ArgumentException ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await producer.PublishTurnOnOffAsync(payload, "lamp-1"));

        StringAssert.Contains(ex.Message, "payload");
    }

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_DetailedValidation_InvalidPayload_IncludesDetails()
    {
        await using InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.Detailed);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"invalid","sentAt":"not-a-date"}"""u8);

        ArgumentException ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await producer.PublishTurnOnOffAsync(payload, "lamp-1"));

        StringAssert.Contains(ex.Message, "payload");

        // Detailed mode includes schema evaluation details
        Assert.IsTrue(ex.Message.Length > 50, "Detailed validation should produce a longer message.");
    }

    [TestMethod]
    public async Task Consumer_StartAsync_SubscribesToChannel()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();

        // Deliver a message to verify the subscription is active
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_HandlerReceivesDeserializedPayload()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":250,"sentAt":"2024-07-04T18:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
        LightMeasuredPayload received = handler.ReceivedPayloads[0];

        // Verify the payload is deserialized correctly
        Assert.AreEqual(JsonValueKind.Object, received.ValueKind);
    }

    [TestMethod]
    public async Task Consumer_BasicValidation_InvalidPayload_WithAbortPolicy_Stops()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        DefaultMessageErrorPolicy abortPolicy = new(0, MessageErrorAction.Abort);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, abortPolicy);

        await consumer.StartAsync();

        // lumens has minimum:0, so -1 is invalid; policy says abort immediately (0 retries)
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-1,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(0, handler.ReceivedPayloads.Count);

        // After abort, delivering another message should throw (unsubscribed)
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray()).AsTask());
    }

    [TestMethod]
    public async Task Consumer_ValidationNone_InvalidPayload_StillDelivers()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();

        // Invalid payload (lumens < 0) but validation is disabled
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-1,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_StopAsync_UnsubscribesFromChannel()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();
        await consumer.StopAsync();

        // After stopping, delivering should throw because there's no subscription
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray()).AsTask());
    }

    [TestMethod]
    public async Task Consumer_DisposeAsync_UnsubscribesFromChannel()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();
        await consumer.DisposeAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray()).AsTask());
    }

    private sealed class MockLightMeasurementHandler : IReceiveLightMeasurementHandler
    {
        public List<LightMeasuredPayload> ReceivedPayloads { get; } = [];

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.ReceivedPayloads.Add(payload);
            return ValueTask.CompletedTask;
        }
    }

    [TestMethod]
    public async Task Consumer_DefaultPolicy_SkipsAfterRetries()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();

        // Default policy: 3 retries then skip
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic);

        await consumer.StartAsync();

        // Invalid payload triggers validation failure; default policy retries 3 times, then skips
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-1,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        // Handler should not have been called (validation failed each attempt)
        Assert.AreEqual(0, handler.ReceivedPayloads.Count);

        // Consumer is still alive — can deliver valid messages
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_DeadLetterPolicy_SendsToDeadLetterChannel()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        DefaultMessageErrorPolicy deadLetterPolicy = new(0, MessageErrorAction.DeadLetter);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, deadLetterPolicy);

        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-1,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(0, handler.ReceivedPayloads.Count);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
        Assert.AreEqual("dead-letter." + LightMeasurementChannel, transport.DeadLetteredMessages[0].DeadLetterChannel);
        Assert.AreEqual(LightMeasurementChannel, transport.DeadLetteredMessages[0].OriginalChannel);
    }

    [TestMethod]
    public async Task Consumer_RetryPolicy_RetriesBeforeExhausted()
    {
        await using InMemoryMessageTransport transport = new();
        int callCount = 0;
        ThrowingHandler throwingHandler = new(() =>
        {
            callCount++;
            if (callCount <= 2)
            {
                throw new InvalidOperationException("Transient failure");
            }
        });

        // 3 retries — handler succeeds on 3rd call
        DefaultMessageErrorPolicy retryPolicy = new(3, MessageErrorAction.Skip);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, throwingHandler, ValidationMode.None, retryPolicy);

        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        // Handler was called 3 times (2 failures + 1 success)
        Assert.AreEqual(3, callCount);
    }

    private sealed class ThrowingHandler : IReceiveLightMeasurementHandler
    {
        private readonly Action onHandle;

        public ThrowingHandler(Action onHandle) => this.onHandle = onHandle;

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.onHandle();
            return ValueTask.CompletedTask;
        }
    }
}