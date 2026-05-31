// <copyright file="AsyncApi26GeneratedEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi;
using Streetlights.Client.V26.Models;
using Streetlights26 = Streetlights.Client.V26;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// End-to-end tests that exercise AsyncAPI 2.6 generated producer and consumer code.
/// </summary>
[TestClass]
public class AsyncApi26GeneratedEndToEndTests
{
    private const string LightMeasurementChannel =
        "smartylighting/streetlights/1/0/action/{streetlightId}/lighting/measured";

    [TestMethod]
    public async Task Producer_PublishTurnOnOff_UsesSubscribeOperationChannel()
    {
        await using InMemoryMessageTransport transport = new();
        Streetlights26.TurnOnProducer producer = new(transport, ValidationMode.None);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on"}"""u8);

        await producer.PublishTurnOnOffAsync(payload, "lamp-26");

        Assert.AreEqual(1, transport.PublishedMessages.Count);

        InMemoryMessageTransport.PublishedMessage msg = transport.PublishedMessages[0];
        Assert.AreEqual(
            "smartylighting/streetlights/1/0/action/lamp-26/turn/on",
            msg.Channel);

        string payloadJson = Encoding.UTF8.GetString(msg.PayloadBytes);
        Assert.AreEqual("""{"command":"on"}""", payloadJson);
    }

    [TestMethod]
    public async Task Consumer_StartAsync_UsesPublishOperationChannel()
    {
        await using InMemoryMessageTransport transport = new();
        MockLightMeasurementHandler handler = new();
        await using Streetlights26.ReceiveLightMeasurementConsumer consumer = new(transport, handler, ValidationMode.None);

        await consumer.StartAsync();

        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":150}"""u8.ToArray());

        Assert.AreEqual(1, handler.ReceivedPayloads.Count);
    }

    private sealed class MockLightMeasurementHandler : Streetlights26.IReceiveLightMeasurementHandler
    {
        public List<LightMeasuredPayload> ReceivedPayloads { get; } = [];

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.ReceivedPayloads.Add(payload);
            return ValueTask.CompletedTask;
        }
    }
}