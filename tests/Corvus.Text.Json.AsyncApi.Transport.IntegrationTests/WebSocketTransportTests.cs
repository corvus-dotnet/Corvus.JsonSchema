// <copyright file="WebSocketTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;
using Corvus.Text.Json.AsyncApi.WebSocket;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="WebSocketMessageTransport"/> against an in-process relay.
/// </summary>
[TestClass]
[TestCategory("integration")]
public class WebSocketTransportTests
{
    private static WebSocketMessageTransport s_publisher = null!;
    private static WebSocketMessageTransport s_subscriber = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await WebSocketFixture.StartAsync();

        // Two separate connections — one publishes, one subscribes
        // (The relay doesn't echo back to the sender)
        s_publisher = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        s_subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await s_publisher.DisposeAsync();
        await s_subscriber.DisposeAsync();
        await WebSocketFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        ReadOnlyMemory<byte> channel = "ws/test/roundtrip"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedPayloadKind = JsonValueKind.Undefined;

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedPayloadKind = payload.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"event":"ws-test","value":42}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_subscriber.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        ReadOnlyMemory<byte> channel = "ws/test/headers"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedHeadersKind = JsonValueKind.Undefined;

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedHeadersKind = headers.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"x"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-session":"sess-01"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind);

        await s_subscriber.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleMessagesDeliveredInOrder()
    {
        ReadOnlyMemory<byte> channel = "ws/test/ordering"u8.ToArray();
        List<int> receivedOrder = [];
        using var allReceived = new SemaphoreSlim(0, 1);
        const int messageCount = 5;

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                int idx = payload.GetProperty("idx"u8).GetInt32();
                receivedOrder.Add(idx);
                if (receivedOrder.Count == messageCount)
                {
                    allReceived.Release();
                }

                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        for (int i = 0; i < messageCount; i++)
        {
            byte[] json = Encoding.UTF8.GetBytes($$"""{"idx":{{i}}}""");
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
            await s_publisher.PublishAsync(channel, doc.RootElement);
            await Task.Delay(50); // Small gap to ensure ordering over WebSocket
        }

        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(allArrived, $"Only received {receivedOrder.Count}/{messageCount} messages.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, receivedOrder.ToArray());

        await s_subscriber.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleChannelsRouteCorrectly()
    {
        ReadOnlyMemory<byte> channel1 = "ws/test/multi/a"u8.ToArray();
        ReadOnlyMemory<byte> channel2 = "ws/test/multi/b"u8.ToArray();
        int countA = 0;
        int countB = 0;
        using var bothReceived = new SemaphoreSlim(0, 1);

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel1,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref countA);
                if (countA >= 1 && countB >= 1)
                {
                    bothReceived.Release();
                }

                return ValueTask.CompletedTask;
            });

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel2,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref countB);
                if (countA >= 1 && countB >= 1)
                {
                    bothReceived.Release();
                }

                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> docA = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"a"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> docB = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"b"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel1, docA.RootElement);
        await s_publisher.PublishAsync(channel2, docB.RootElement);

        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Both channels did not receive messages.");
        Assert.AreEqual(1, countA);
        Assert.AreEqual(1, countB);

        await s_subscriber.UnsubscribeAsync(channel1);
        await s_subscriber.UnsubscribeAsync(channel2);
    }

    [TestMethod]
    public async Task HandlerExceptionInvokesErrorPolicy()
    {
        var actions = new List<MessageErrorKind>();
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = new TrackingErrorPolicy(actions),
        });

        ReadOnlyMemory<byte> channel = "ws/test/error"u8.ToArray();

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Handler failure"));

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"trigger":"error"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);
        await Task.Delay(500);

        Assert.IsTrue(actions.Count > 0, "Error policy was not invoked.");
        Assert.AreEqual(MessageErrorKind.Handler, actions[0]);

        await subscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeClosesConnection()
    {
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        ReadOnlyMemory<byte> channel = "ws/test/dispose"u8.ToArray();
        int receiveCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);
        await transport.DisposeAsync();
        await Task.Delay(200);

        // Publishing after dispose — the disposed transport should not receive
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"dispose"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);
        await Task.Delay(500);

        Assert.AreEqual(0, receiveCount);
    }

    private sealed class TrackingErrorPolicy(List<MessageErrorKind> actions) : IMessageErrorPolicy
    {
        public ValueTask<MessageErrorAction> HandleErrorAsync(
            Exception exception,
            MessageErrorContext context,
            CancellationToken cancellationToken)
        {
            actions.Add(context.ErrorKind);
            return ValueTask.FromResult(MessageErrorAction.Skip);
        }
    }
}