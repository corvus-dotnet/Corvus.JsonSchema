// <copyright file="GeneratedEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Streetlights.Client;
using Streetlights.Client.Models;

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
        "smartylighting.streetlights.1.0.action.1.lighting.measured";

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

        await consumer.StartAsync("1");

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

        await consumer.StartAsync("1");

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
        DefaultMessageErrorPolicy abortPolicy = new(MessageErrorAction.Abort, MessageErrorAction.Abort, MessageErrorAction.Abort);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, abortPolicy);

        await consumer.StartAsync("1");

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

        await consumer.StartAsync("1");

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

        await consumer.StartAsync("1");
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

        await consumer.StartAsync("1");
        await consumer.DisposeAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray()).AsTask());
    }

    [TestMethod]
    public async Task Consumer_RefusedStart_DisposeDoesNotUnsubscribeWinner()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler winnerHandler = new();
        await using ReceiveLightMeasurementConsumer winner = new(transport, winnerHandler, ValidationMode.None);
        await winner.StartAsync("1");

        // The second consumer is refused the channel; disposing it must not tear down the
        // winner's live subscription.
        MockLightMeasurementHandler loserHandler = new();
        ReceiveLightMeasurementConsumer loser = new(transport, loserHandler, ValidationMode.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => loser.StartAsync("1").AsTask());
        await loser.DisposeAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, winnerHandler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_DisposeWithoutStart_CompletesQuietly()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();

        await using (ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None))
        {
            // Never started: leaving the await-using must not throw.
        }
    }

    [TestMethod]
    public async Task Consumer_SecondStart_ThrowsConsumerAlreadyStarted()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);
        await consumer.StartAsync("1");

        // A second start would silently orphan the first subscription if the consumer just
        // overwrote its record; it must refuse instead.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => consumer.StartAsync("2").AsTask());

        // The original subscription is untouched.
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_RefusedRestart_PreservesDeadLetterAddress()
    {
        await using InMemoryMessageTransport transport = new();
        ThrowingLightMeasurementHandler handler = new();
        DefaultMessageErrorPolicy deadLetterPolicy = new(
            MessageErrorAction.DeadLetter, MessageErrorAction.DeadLetter, MessageErrorAction.DeadLetter);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None, deadLetterPolicy);
        await consumer.StartAsync("1");

        // The refused restart must fail before touching the running subscription's retained
        // dead-letter address, or every later dead-letter silently misroutes.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => consumer.StartAsync("2").AsTask());

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        InMemoryMessageTransport.DeadLetteredMessage deadLettered = transport.DeadLetteredMessages.Single();
        StringAssert.Contains(deadLettered.DeadLetterChannel, ".action.1.lighting.measured");

        await consumer.StopAsync();
    }

    [TestMethod]
    public async Task Consumer_StopThenDispose_DoesNotUnsubscribeNextOwner()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler firstHandler = new();
        ReceiveLightMeasurementConsumer first = new(transport, firstHandler, ValidationMode.None);
        await first.StartAsync("1");
        await first.StopAsync();

        MockLightMeasurementHandler secondHandler = new();
        await using ReceiveLightMeasurementConsumer second = new(transport, secondHandler, ValidationMode.None);
        await second.StartAsync("1");

        // The stopped consumer owns nothing; disposing it must not unsubscribe the channel's
        // new owner.
        await first.DisposeAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, secondHandler.ReceivedPayloads.Count);
    }

    [TestMethod]
    public async Task Consumer_StopDuringStart_ReleasesTheSubscriptionTheStartLands()
    {
        PausableTransport transport = new();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.SubscribeGate = gate;
        MockLightMeasurementHandler handler = new();
        ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        // The first start claims the gate and parks inside the transport subscribe.
        Task firstStart = consumer.StartAsync("1").AsTask();

        // The stop takes the first start's claim; its unsubscribe finds nothing to remove.
        await consumer.StopAsync();

        // A legitimate restart on another channel claims and lands.
        transport.SubscribeGate = null;
        await consumer.StartAsync("2");

        // The first start's subscribe now lands. It must recognize that ITS claim is gone —
        // not be fooled by the successor's — release the channel-1 subscription it just
        // created, and report the stop, rather than returning success for an orphan.
        gate.SetResult();
        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => firstStart);
        StringAssert.Contains(ex.Message, "stopped");
        Assert.IsFalse(transport.IsSubscribed(LightMeasurementChannel), "The superseded start must release the subscription it landed.");

        // The restart's subscription is untouched and still stoppable.
        await consumer.StopAsync();
        Assert.IsFalse(transport.IsSubscribed("smartylighting.streetlights.1.0.action.2.lighting.measured"));
    }

    [TestMethod]
    public async Task Consumer_StopAfterSubscribeLands_ExactlyOnePartyUnsubscribes()
    {
        PausableTransport transport = new();
        TaskCompletionSource postGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.PostSubscribeGate = postGate;
        MockLightMeasurementHandler handler = new();
        ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        // The first start's subscription LANDS at the transport, then the start parks before
        // its completion logic runs.
        Task firstStart = consumer.StartAsync("1").AsTask();

        // The stop takes the claim mid-start: per the handshake it declines the transport
        // removal (the superseded start owns that cleanup), so the channel is still held.
        await consumer.StopAsync();

        // A restart while the superseded start is still unwinding is therefore honestly
        // refused — the alternative (the stop unsubscribing eagerly, the restart landing,
        // and the superseded start then destroying the restart's live subscription) is the
        // silent-message-loss defect this pins. The gate is disarmed first so the refusal
        // (or, on defective code, the wrongly-successful restart) completes promptly.
        transport.PostSubscribeGate = null;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => consumer.StartAsync("1").AsTask());

        // The superseded start resumes, releases the subscription it landed, and reports the stop.
        postGate.SetResult();
        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => firstStart);
        StringAssert.Contains(ex.Message, "stopped");
        Assert.IsFalse(transport.IsSubscribed(LightMeasurementChannel));

        // Now the restart succeeds, is genuinely live, and stops cleanly.
        transport.PostSubscribeGate = null;
        await consumer.StartAsync("1");
        Assert.IsTrue(transport.IsSubscribed(LightMeasurementChannel));
        await consumer.StopAsync();
        Assert.IsFalse(transport.IsSubscribed(LightMeasurementChannel));
    }

    [TestMethod]
    public async Task Consumer_FailedStartAfterRestart_DoesNotReleaseTheNewClaim()
    {
        PausableTransport transport = new();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.SubscribeGate = gate;
        MockLightMeasurementHandler handler = new();
        ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        // First start parks inside the transport subscribe; a stop takes its claim.
        Task firstStart = consumer.StartAsync("1").AsTask();
        await consumer.StopAsync();

        // A restart on the SAME channel claims and lands.
        transport.SubscribeGate = null;
        await consumer.StartAsync("1");

        // The first start's subscribe resumes and is refused (the restart holds the channel).
        // Its failure path must not release the restart's claim.
        gate.SetResult();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => firstStart);

        // The consumer still owns its subscription: stop succeeds and removes it.
        await consumer.StopAsync();
        Assert.IsFalse(transport.IsSubscribed(LightMeasurementChannel));
    }

    private sealed class PausableTransport : IMessageTransport
    {
        private readonly object syncRoot = new();
        private readonly Dictionary<string, Delegate> subscriptions = [];

        // Read once at the top of each subscribe, so a parked subscribe keeps its own gate
        // while the test re-arms or clears the property for later calls.
        public TaskCompletionSource? SubscribeGate { get; set; }

        // Awaited AFTER the subscription has landed, so a test can hold a start between the
        // transport recording the subscription and the start's own completion logic running.
        public TaskCompletionSource? PostSubscribeGate { get; set; }

        public bool IsSubscribed(string channel)
        {
            lock (this.syncRoot)
            {
                return this.subscriptions.ContainsKey(channel);
            }
        }

        public ValueTask PublishAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            in TPayload payload,
            in JsonElement headers = default,
            CancellationToken cancellationToken = default)
            where TPayload : struct, Corvus.Text.Json.Internal.IJsonElement<TPayload>
            => ValueTask.CompletedTask;

        public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
            ReadOnlyMemory<byte> requestChannelUtf8,
            ReadOnlyMemory<byte> replyChannelUtf8,
            TRequest request,
            ReadOnlyMemory<byte> correlationIdUtf8,
            JsonWorkspace workspace,
            JsonElement headers = default,
            CancellationToken cancellationToken = default)
            where TRequest : struct, Corvus.Text.Json.Internal.IJsonElement<TRequest>
            where TReply : struct, Corvus.Text.Json.Internal.IJsonElement<TReply>
            => throw new NotSupportedException();

        public async ValueTask SubscribeAsync<TPayload>(
            ReadOnlyMemory<byte> channelUtf8,
            Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken = default)
            where TPayload : struct, Corvus.Text.Json.Internal.IJsonElement<TPayload>
        {
            string channel = Encoding.UTF8.GetString(channelUtf8.Span);
            if (this.SubscribeGate is { } gate)
            {
                await gate.Task.ConfigureAwait(false);
            }

            lock (this.syncRoot)
            {
                if (!this.subscriptions.TryAdd(channel, handler))
                {
                    throw new InvalidOperationException(
                        $"Channel '{channel}' already has a subscription. Unsubscribe before subscribing again.");
                }
            }

            if (this.PostSubscribeGate is { } postGate)
            {
                await postGate.Task.ConfigureAwait(false);
            }
        }

        public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
        {
            string channel = Encoding.UTF8.GetString(channelUtf8.Span);
            lock (this.syncRoot)
            {
                this.subscriptions.Remove(channel);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DeadLetterAsync(
            ReadOnlyMemory<byte> deadLetterChannelUtf8,
            ReadOnlyMemory<byte> originalChannelUtf8,
            in JsonElement payload,
            in JsonElement headers,
            Exception exception,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingLightMeasurementHandler : IReceiveLightMeasurementHandler
    {
        public ValueTask HandleLightMeasuredAsync(
            LightMeasuredPayload payload,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Handler failure to drive the dead-letter path.");
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

        await consumer.StartAsync("1");

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
        DefaultMessageErrorPolicy deadLetterPolicy = new(MessageErrorAction.DeadLetter, MessageErrorAction.DeadLetter, MessageErrorAction.Abort);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, deadLetterPolicy);

        await consumer.StartAsync("1");

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-1,"sentAt":"2024-03-01T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(0, handler.ReceivedPayloads.Count);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
        Assert.AreEqual("dead-letter." + LightMeasurementChannel, transport.DeadLetteredMessages[0].DeadLetterChannel);
        Assert.AreEqual(LightMeasurementChannel, transport.DeadLetteredMessages[0].OriginalChannel);
    }

    [TestMethod]
    public async Task Consumer_SkipPolicy_HandlerFailure_SkipsMessage()
    {
        await using InMemoryMessageTransport transport = new();
        int callCount = 0;
        ThrowingHandler throwingHandler = new(() =>
        {
            callCount++;
            throw new InvalidOperationException("Permanent failure");
        });

        // Skip on handler errors — no retry (retry is middleware's job)
        DefaultMessageErrorPolicy skipPolicy = new(MessageErrorAction.Skip, MessageErrorAction.Skip, MessageErrorAction.Abort);
        await using ReceiveLightMeasurementConsumer consumer = new(transport, throwingHandler, ValidationMode.None, skipPolicy);

        await consumer.StartAsync("1");

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        // Handler called once, error skipped (no retry loop)
        Assert.AreEqual(1, callCount);
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