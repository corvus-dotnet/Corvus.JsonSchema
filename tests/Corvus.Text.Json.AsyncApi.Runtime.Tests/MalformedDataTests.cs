// <copyright file="MalformedDataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for malformed and invalid data handling through the transport's
/// deserialization error path and schema validation.
/// </summary>
[TestClass]
public class MalformedDataTests
{
    private const string LightMeasurementChannel =
        "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";

    [TestMethod]
    public async Task DeliverRaw_MalformedJson_PolicyDeadLetter_SendsToDeadLetter()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) => ValueTask.CompletedTask);

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        // Deliver malformed JSON
        MessageErrorAction? action = await transport.DeliverRawAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            "this is not json at all"u8.ToArray(),
            dlPolicy);

        Assert.AreEqual(MessageErrorAction.DeadLetter, action);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
        Assert.AreEqual("dead-letter." + LightMeasurementChannel, transport.DeadLetteredMessages[0].DeadLetterChannel);
        Assert.AreEqual(LightMeasurementChannel, transport.DeadLetteredMessages[0].OriginalChannel);
    }

    [TestMethod]
    public async Task DeliverRaw_MalformedJson_PolicySkip_ReturnsSkip()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) => ValueTask.CompletedTask);

        DefaultMessageErrorPolicy skipPolicy = new(
            MessageErrorAction.Skip,
            MessageErrorAction.Skip,
            MessageErrorAction.Abort);

        MessageErrorAction? action = await transport.DeliverRawAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            "{{{invalid"u8.ToArray(),
            skipPolicy);

        Assert.AreEqual(MessageErrorAction.Skip, action);
        Assert.AreEqual(0, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task DeliverRaw_EmptyBytes_DeserializationError()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) => ValueTask.CompletedTask);

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        MessageErrorAction? action = await transport.DeliverRawAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            ReadOnlyMemory<byte>.Empty,
            dlPolicy);

        Assert.AreEqual(MessageErrorAction.DeadLetter, action);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task DeliverRaw_ValidJson_NoError_ReturnsNull()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        int handlerCalled = 0;

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) =>
            {
                handlerCalled++;
                return ValueTask.CompletedTask;
            });

        DefaultMessageErrorPolicy policy = new();

        MessageErrorAction? action = await transport.DeliverRawAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray(),
            policy);

        Assert.IsNull(action);
        Assert.AreEqual(1, handlerCalled);
    }

    [TestMethod]
    public async Task DeliverRaw_NoSubscription_Throws()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        DefaultMessageErrorPolicy policy = new();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => transport.DeliverRawAsync<LightMeasuredPayload>(
                "nonexistent/channel",
                """{}"""u8.ToArray(),
                policy).AsTask());
    }

    [TestMethod]
    public async Task Consumer_SchemaValidation_WrongType_DeadLetters()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Basic, dlPolicy);
        await consumer.StartAsync();

        // Valid JSON but lumens is a string — type mismatch fails validation
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":"not-integer","sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());

        Assert.AreEqual(0, handler.CallCount);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task Consumer_Detailed_Validation_IncludesSpecificPropertyPath()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        CountingHandler handler = new();

        DefaultMessageErrorPolicy dlPolicy = new(
            MessageErrorAction.DeadLetter,
            MessageErrorAction.DeadLetter,
            MessageErrorAction.Abort);

        await using ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.Detailed, dlPolicy);
        await consumer.StartAsync();

        // lumens value exceeds no constraint (minimum:0) but type is wrong
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":"invalid-type","sentAt":"bad-date"}"""u8.ToArray());

        Assert.AreEqual(0, handler.CallCount);
        Assert.AreEqual(1, transport.DeadLetteredMessages.Count);

        // The detailed exception message should mention the schema path
        string message = transport.DeadLetteredMessages[0].Exception.Message;
        Assert.IsTrue(message.Length > 80, $"Expected detailed message with paths, got ({message.Length} chars): {message}");
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
}