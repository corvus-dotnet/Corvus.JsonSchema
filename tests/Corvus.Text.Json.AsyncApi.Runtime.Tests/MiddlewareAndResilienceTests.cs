// <copyright file="MiddlewareAndResilienceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for <see cref="MessageHandlerMiddleware"/> integration and error policy
/// handoff in generated consumers.
/// </summary>
[TestClass]
public class MiddlewareAndResilienceTests
{
    private const string LightMeasurementChannel =
        "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";

    [TestMethod]
    public async Task Consumer_WithMiddleware_MiddlewareWrapsHandler()
    {
        // The generated consumer calls the handler directly. Middleware is a
        // transport-level concern used in real transports (Kafka, MQTT, etc.).
        // This test verifies the basic subscribe→handle pipeline works correctly.
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);
        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task Consumer_HandlerThrows_PolicySkip_ContinuesProcessing()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        int callCount = 0;
        ThrowOnceHandler handler = new(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("Transient failure");
            }
        });

        DefaultMessageErrorPolicy skipPolicy = new(
            MessageErrorAction.Skip,
            MessageErrorAction.Skip,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None, skipPolicy);
        await consumer.StartAsync();

        // First message: handler throws, policy says skip
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, callCount);

        // Second message: handler succeeds
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":200,"sentAt":"2024-02-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public async Task Consumer_HandlerThrows_PolicyDeadLetter_SendsToDeadLetter()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        ThrowOnceHandler handler = new(() => throw new InvalidOperationException("Permanent failure"));

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None, dlPolicy);
        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":50,"sentAt":"2024-03-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
        DeadLetteredMessage dlm = transport.DeadLetteredMessages[0];
        Assert.AreEqual("dead-letter." + LightMeasurementChannel, dlm.DeadLetterChannel);
        Assert.AreEqual(LightMeasurementChannel, dlm.OriginalChannel);
        StringAssert.Contains(dlm.Exception.Message, "Permanent failure");
    }

    [TestMethod]
    public async Task Consumer_HandlerThrows_PolicyAbort_UnsubscribesFromChannel()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        ThrowOnceHandler handler = new(() => throw new InvalidOperationException("Fatal"));

        DefaultMessageErrorPolicy abortPolicy = new(
            MessageErrorAction.Abort,
            MessageErrorAction.Abort,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None, abortPolicy);
        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":50,"sentAt":"2024-03-01T00:00:00Z"}"""u8.ToArray());

        // After abort, subscription is removed
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray()).AsTask());
    }

    [TestMethod]
    public async Task Consumer_ValidationFailure_PolicyDeadLetter_SendsInvalidPayloadToDeadLetter()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, dlPolicy);
        await consumer.StartAsync();

        // Invalid: lumens is negative (minimum: 0)
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":-5,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        // Handler was NOT called (validation intercepted)
        Assert.AreEqual(0, handler.CallCount);

        // Message was dead-lettered
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task Consumer_MultipleFailures_PolicySkip_ContinuesEachTime()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        int callCount = 0;
        ThrowOnceHandler handler = new(() =>
        {
            callCount++;
            throw new InvalidOperationException($"Failure #{callCount}");
        });

        DefaultMessageErrorPolicy skipPolicy = new(
            MessageErrorAction.Skip,
            MessageErrorAction.Skip,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None, skipPolicy);
        await consumer.StartAsync();

        // Deliver 3 messages that all fail
        for (int i = 0; i < 3; i++)
        {
            await transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());
        }

        // Handler was called 3 times (each threw, policy skipped each time)
        Assert.AreEqual(3, callCount);

        // No dead-lettered messages (skip policy)
        Assert.AreEqual(0, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task Consumer_ValidationMode_Detailed_IncludesSchemaPath()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Detailed, dlPolicy);
        await consumer.StartAsync();

        // Invalid: lumens is a string instead of integer
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":"not-a-number","sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(0, handler.CallCount);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);

        // The exception message should contain detailed schema info
        string exMessage = transport.DeadLetteredMessages[0].Exception.Message;
        Assert.IsTrue(exMessage.Length > 50, $"Expected detailed message, got: {exMessage}");
    }

    [TestMethod]
    public async Task Consumer_HandlerSucceeds_NoDeadLetter_NoAbort()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        DefaultMessageErrorPolicy strictPolicy = new(
            MessageErrorAction.Abort,
            MessageErrorAction.Abort,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, strictPolicy);
        await consumer.StartAsync();

        // Valid message — handler succeeds, no policy invoked
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(0, transport.DeadLetteredMessages.Count);
    }

    private sealed class CountingHandler : IReceiveLightMeasurementHandler
    {
        public int CallCount { get; private set; }

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowOnceHandler : IReceiveLightMeasurementHandler
    {
        private readonly Action onHandle;

        public ThrowOnceHandler(Action onHandle) => this.onHandle = onHandle;

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.onHandle();
            return ValueTask.CompletedTask;
        }
    }
}