// <copyright file="MqttTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.Mqtt;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;
using MQTTnet;
using MQTTnet.Client;

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
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await MqttFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/roundtrip"u8.ToArray();
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

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"sensor":"humidity","value":65.2}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        ReadOnlyMemory<byte> channel = "mqtt/test/headers"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedHeadersKind = JsonValueKind.Undefined;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedHeadersKind = headers.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":1}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-source":"test","x-version":"2"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind);

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

        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(30));
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

    [TestMethod]
    public async Task DeserializationErrorInvokesErrorPolicy()
    {
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-deser-" + Guid.NewGuid().ToString("N")[..8],
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/deser-error"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(300);

        MqttFactory factory = new();
        using IMqttClient rawClient = factory.CreateMqttClient();
        MqttClientOptions rawOpts = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttFixture.Host, MqttFixture.Port)
            .WithClientId("corvus-raw-" + Guid.NewGuid().ToString("N")[..8])
            .Build();
        await rawClient.ConnectAsync(rawOpts);
        await rawClient.PublishBinaryAsync("mqtt/test/deser-error", "NOT VALID JSON!!!"u8.ToArray());
        await rawClient.DisconnectAsync();

        await Task.Delay(500);

        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy was not invoked for deserialization error.");
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterActionSendsToDeadLetterChannel()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.DeadLetter);
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-dl-" + Guid.NewGuid().ToString("N")[..8],
            ErrorPolicy = policy,
            DeadLetterSuffix = "/dlq",
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/deadletter"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);
        byte[]? dlqPayload = null;

        MqttFactory factory = new();
        using IMqttClient dlqClient = factory.CreateMqttClient();
        MqttClientOptions dlqOpts = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttFixture.Host, MqttFixture.Port)
            .WithClientId("corvus-dlq-reader-" + Guid.NewGuid().ToString("N")[..8])
            .Build();
        await dlqClient.ConnectAsync(dlqOpts);
        dlqClient.ApplicationMessageReceivedAsync += args =>
        {
            dlqPayload = args.ApplicationMessage.PayloadSegment.ToArray();
            dlqReceived.Release();
            return Task.CompletedTask;
        };
        await dlqClient.SubscribeAsync("mqtt/test/deadletter/dlq");
        await Task.Delay(200);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Intentional failure"));

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"item":"DL-001"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Dead-letter message was not received on DLQ topic.");
        Assert.IsNotNull(dlqPayload);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
        await dlqClient.DisconnectAsync();
    }

    [TestMethod]
    public async Task AbortActionStopsSubscription()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Abort);
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-abort-" + Guid.NewGuid().ToString("N")[..8],
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/abort"u8.ToArray();
        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                throw new InvalidOperationException("Trigger abort");
            });

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":1}"""u8.ToArray());
        await transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(500);

        int countAfterAbort = handlerCallCount;
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":2}"""u8.ToArray());
        await transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(500);

        Assert.AreEqual(1, policy.Invocations.Count, "Error policy should be invoked exactly once.");
        Assert.AreEqual(countAfterAbort, handlerCallCount, "Handler should not be called after abort.");

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareWrapsHandlerExecution()
    {
        int middlewareCallCount = 0;
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-mw-" + Guid.NewGuid().ToString("N")[..8],
            HandlerMiddleware = async (operation, ct) =>
            {
                Interlocked.Increment(ref middlewareCallCount);
                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/middleware"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"mw-test"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received through middleware.");
        Assert.AreEqual(1, middlewareCallCount, "Middleware should be called exactly once.");

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareExhaustionFallsThroughToErrorPolicy()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Skip);
        int retryCount = 0;
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-mw-exhaust-" + Guid.NewGuid().ToString("N")[..8],
            ErrorPolicy = policy,
            HandlerMiddleware = async (operation, ct) =>
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        await operation(ct).ConfigureAwait(false);
                        return;
                    }
                    catch (InvalidOperationException) when (i < 2)
                    {
                        Interlocked.Increment(ref retryCount);
                    }
                }

                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "mqtt/test/mw-exhaust"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Always fails"));

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"exhaust"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        Assert.AreEqual(2, retryCount, "Middleware should have retried 2 times.");
        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy should be invoked after middleware exhaustion.");
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyRoundtripWithResponder()
    {
        // Arrange — create a requester transport
        MqttMessageTransport requesterTransport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-requester-" + Guid.NewGuid().ToString("N")[..8],
        });

        // Set up a raw MQTT client as the responder
        MqttClientOptions responderOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttFixture.Host, MqttFixture.Port)
            .WithClientId("corvus-responder-" + Guid.NewGuid().ToString("N")[..8])
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .Build();

        IMqttClient responderClient = new MqttFactory().CreateMqttClient();
        await responderClient.ConnectAsync(responderOptions);

        // Subscribe the responder to the request topic
        await responderClient.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("mqtt/test/reqreply/request")
                .Build());

        responderClient.ApplicationMessageReceivedAsync += async args =>
        {
            byte[]? corrData = args.ApplicationMessage.CorrelationData;
            if (corrData is { Length: > 0 })
            {
                // Echo back a reply on the reply topic with the same correlation data
                MqttApplicationMessage reply = new MqttApplicationMessageBuilder()
                    .WithTopic("mqtt/test/reqreply/reply")
                    .WithPayload("""{"answer":"from-responder","value":77}"""u8.ToArray())
                    .WithCorrelationData(corrData)
                    .Build();

                await responderClient.PublishAsync(reply);
            }
        };

        await Task.Delay(500);

        // Act — send a request through the transport
        ReadOnlyMemory<byte> requestChannel = "mqtt/test/reqreply/request"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "mqtt/test/reqreply/reply"u8.ToArray();
        byte[] correlationId = "mqtt-roundtrip-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"question":"what?"}"""u8.ToArray());

        (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(77, replyPayload.GetProperty("value"u8).GetInt32());

        await requesterTransport.DisposeAsync();
        await responderClient.DisconnectAsync();
        responderClient.Dispose();
    }

    [TestMethod]
    public async Task RequestReplyTimeoutThrows()
    {
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-req-timeout-" + Guid.NewGuid().ToString("N")[..8],
        });

        ReadOnlyMemory<byte> requestChannel = "mqtt/test/req-timeout"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "mqtt/test/reply-timeout"u8.ToArray();
        byte[] correlationId = "timeout-corr-m01"u8.ToArray();

        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"q":"hello"}"""u8.ToArray());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await transport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId,
                cancellationToken: cts.Token));

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task OperationsAfterDisposeThrowObjectDisposedException()
    {
        MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(new MqttTransportOptions
        {
            Host = MqttFixture.Host,
            Port = MqttFixture.Port,
            ClientId = "corvus-disposed-" + Guid.NewGuid().ToString("N")[..8],
        });

        await transport.DisposeAsync();

        ReadOnlyMemory<byte> channel = "mqtt/test/disposed"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}"""u8.ToArray());

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.PublishAsync(channel, doc.RootElement));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.SubscribeAsync<JsonElement>(channel, (p, h, ct) => ValueTask.CompletedTask));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.RequestAsync<JsonElement, JsonElement>(
                channel,
                channel,
                doc.RootElement,
                "corr"u8.ToArray()));
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

    private sealed class ConfigurableErrorPolicy(
        MessageErrorAction deserializationAction = MessageErrorAction.Skip,
        MessageErrorAction handlerAction = MessageErrorAction.Skip,
        MessageErrorAction transportAction = MessageErrorAction.Skip) : IMessageErrorPolicy
    {
        public List<(MessageErrorKind Kind, Exception Exception)> Invocations { get; } = [];

        public ValueTask<MessageErrorAction> HandleErrorAsync(
            Exception exception,
            MessageErrorContext context,
            CancellationToken cancellationToken)
        {
            this.Invocations.Add((context.ErrorKind, exception));
            MessageErrorAction action = context.ErrorKind switch
            {
                MessageErrorKind.Deserialization => deserializationAction,
                MessageErrorKind.Handler => handlerAction,
                MessageErrorKind.Transport => transportAction,
                _ => MessageErrorAction.Skip,
            };

            return ValueTask.FromResult(action);
        }
    }
}