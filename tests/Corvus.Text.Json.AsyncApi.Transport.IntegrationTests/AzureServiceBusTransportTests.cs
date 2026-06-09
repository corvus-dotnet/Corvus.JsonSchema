// <copyright file="AzureServiceBusTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.AzureServiceBus;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusMessageTransport"/> against the Azure Service Bus emulator.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class AzureServiceBusTransportTests
{
    private static AzureServiceBusMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await AzureServiceBusFixture.StartAsync();
        s_transport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await AzureServiceBusFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test-queue"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedPayloadKind = JsonValueKind.Undefined;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedPayloadKind = payload.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        // Allow subscription loop to start
        await Task.Delay(500);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"sensor":"temp","value":22.5}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "[Azure Service Bus] Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task RequestReplyResponderRoundTrip()
    {
        // Arrange — a responder transport hosts the SubscribeReplyAsync handler on the request queue,
        // and a separate requester transport drives RequestAsync. The reply queue is session-enabled
        // so the requester's session receiver (keyed on the correlation ID) correlates the reply.
        AzureServiceBusMessageTransport responderTransport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });

        AzureServiceBusMessageTransport requesterTransport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });

        try
        {
            ReadOnlyMemory<byte> requestChannel = "test-queue"u8.ToArray();
            ReadOnlyMemory<byte> replyChannel = "test-reply-queue"u8.ToArray();

            // Register a responder that computes a reply from the request.
            await responderTransport.SubscribeReplyAsync<JsonElement, JsonElement>(
                requestChannel,
                (request, headers, ct) =>
                {
                    int value = request.GetProperty("value"u8).GetInt32();
                    using ParsedJsonDocument<JsonElement> replyDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes($$"""{"result":{{value * 2}}}"""));
                    return ValueTask.FromResult(replyDoc.RootElement);
                });

            // Allow the processor to start.
            await Task.Delay(500);

            // Act — send a request and await the correlated reply.
            byte[] correlationId = "asb-responder-roundtrip-001"u8.ToArray();
            using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"value":21}"""u8.ToArray());

            (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId);

            // Assert
            Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
            Assert.AreEqual(42, replyPayload.GetProperty("result"u8).GetInt32());

            await responderTransport.UnsubscribeAsync(requestChannel);
        }
        finally
        {
            await responderTransport.DisposeAsync();
            await requesterTransport.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ReceiveOneAndReplyRoundTrip()
    {
        // Arrange — mirrors RequestReplyResponderRoundTrip but drives the one-shot
        // ReceiveOneAndReplyAsync primitive instead of the persistent SubscribeReplyAsync.
        AzureServiceBusMessageTransport responderTransport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });

        AzureServiceBusMessageTransport requesterTransport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });

        try
        {
            ReadOnlyMemory<byte> requestChannel = "test-queue"u8.ToArray();
            ReadOnlyMemory<byte> replyChannel = "test-reply-queue"u8.ToArray();

            // Start the one-shot responder as a background task. It will handle exactly one
            // request, send the reply, and then complete — no explicit unsubscribe needed.
            System.Threading.Tasks.Task responderTask = responderTransport.ReceiveOneAndReplyAsync<JsonElement, JsonElement>(
                requestChannel,
                (request, headers) =>
                {
                    int value = request.GetProperty("value"u8).GetInt32();
                    using ParsedJsonDocument<JsonElement> replyDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes($$"""{"result":{{value * 2}}}"""));
                    return ValueTask.FromResult(replyDoc.RootElement);
                }).AsTask();

            // Allow the processor to start.
            await Task.Delay(500);

            // Act — send a request and await the correlated reply.
            byte[] correlationId = "asb-responder-roundtrip-001-once"u8.ToArray();
            using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"value":21}"""u8.ToArray());

            (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId);

            // Assert
            Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
            Assert.AreEqual(42, replyPayload.GetProperty("result"u8).GetInt32());

            // The one-shot responder unsubscribes itself after handling a single request;
            // await its completion rather than calling UnsubscribeAsync.
            await responderTask;
        }
        finally
        {
            await responderTransport.DisposeAsync();
            await requesterTransport.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task DoubleDisposeDoesNotThrow()
    {
        AzureServiceBusTransportOptions options = new()
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        };

        AzureServiceBusMessageTransport transport = await AzureServiceBusMessageTransport.CreateAsync(options);

        await transport.DisposeAsync();
        await transport.DisposeAsync();
    }
}