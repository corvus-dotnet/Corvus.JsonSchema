// <copyright file="MqttTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.Mqtt;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="MqttMessageTransport"/> against a real Mosquitto broker.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class MqttTransportTests
{
    private static MqttMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await MqttFixture.StartAsync();
        s_transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-test-" + Guid.NewGuid().ToString("N")[..8],
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await s_transport.DisposeAsync();
        await MqttFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/roundtrip"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonElement receivedPayload = default;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedPayload = payload;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"sensor":"humidity","value":65.2}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayload.ValueKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/headers"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonElement receivedHeaders = default;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedHeaders = headers;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":1}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-source":"test","x-version":"2"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeaders.ValueKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleMessagesDeliveredInOrder()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/ordering"u8.ToArray();
        List<int> receivedOrder = [];
        using var allReceived = new SemaphoreSlim(0, 1);
        const int messageCount = 5;

        await s_transport.SubscribeAsync<JsonElement>(
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
            await s_transport.PublishAsync(channel, doc.RootElement);
        }

        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(allArrived, $"Only received {receivedOrder.Count}/{messageCount} messages.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, receivedOrder.ToArray());

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task UnsubscribeStopsDelivery()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/unsub"u8.ToArray();
        int receiveCount = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"n":1}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(300);

        await s_transport.UnsubscribeAsync(channel);
        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"n":2}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(500);

        Assert.AreEqual(1, receiveCount);
    }

    [TestMethod]
    public async Task MultipleSubscribersOnDifferentTopics()
    {
        ReadOnlyMemory<byte> channel1 = "mqtt/test/multi/a"u8.ToArray();
        ReadOnlyMemory<byte> channel2 = "mqtt/test/multi/b"u8.ToArray();
        int countA = 0;
        int countB = 0;
        using var bothReceived = new SemaphoreSlim(0, 1);

        await s_transport.SubscribeAsync<JsonElement>(
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

        await s_transport.SubscribeAsync<JsonElement>(
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
        await s_transport.PublishAsync(channel1, docA.RootElement);
        await s_transport.PublishAsync(channel2, docB.RootElement);

        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Both topics did not receive messages.");
        Assert.AreEqual(1, countA);
        Assert.AreEqual(1, countB);

        await s_transport.UnsubscribeAsync(channel1);
        await s_transport.UnsubscribeAsync(channel2);
    }

    [TestMethod]
    public async Task HandlerExceptionInvokesErrorPolicy()
    {
        var actions = new List<MessageErrorKind>();
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-error-" + Guid.NewGuid().ToString("N")[..8],
            ErrorPolicy = new TrackingErrorPolicy(actions),
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/error"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Handler failure"));

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"trigger":"error"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(500);

        Assert.IsTrue(actions.Count > 0, "Error policy was not invoked.");
        Assert.AreEqual(MessageErrorKind.Handler, actions[0]);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task LargePayloadTransmitsCorrectly()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/large"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        int receivedLength = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                int count = 0;
                foreach (JsonElement item in payload.EnumerateArray())
                {
                    _ = item;
                    count++;
                }

                receivedLength = count;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        StringBuilder sb = new("[");
        for (int i = 0; i < 2000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($$"""{"index":{{i}},"data":"padding-xxxxxxxxxxxxxxxxx"}""");
        }

        sb.Append(']');
        byte[] largePayload = Encoding.UTF8.GetBytes(sb.ToString());
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(largePayload);
        await s_transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(wasReceived, "Large message was not received within timeout.");
        Assert.AreEqual(2000, receivedLength);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task DisposeStopsAllSubscriptions()
    {
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-dispose-" + Guid.NewGuid().ToString("N")[..8],
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/dispose"u8.ToArray();
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

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"dispose"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);
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