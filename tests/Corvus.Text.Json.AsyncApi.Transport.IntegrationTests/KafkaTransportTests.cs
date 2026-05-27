// <copyright file="KafkaTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Confluent.Kafka;
using Corvus.Text.Json.AsyncApi.Kafka;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="KafkaMessageTransport"/> against a real Kafka broker.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("kafka")]
public class KafkaTransportTests
{
    private static KafkaMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await KafkaFixture.StartAsync();
        s_transport = new KafkaMessageTransport(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-test-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ConsumerConfig = new ConsumerConfig
            {
                // Fast metadata refresh for tests — each test creates topics
                // immediately before subscribing and Kafka must discover them quickly.
                TopicMetadataRefreshIntervalMs = 1000,
            },
        });

        // Allow Kafka broker to be fully ready (consumer group coordination
        // and topic metadata discovery requires extra time on loaded CI runners).
        // Increased to 10s for CI runner reliability under load.
        await Task.Delay(10000);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await KafkaFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        string topic = CreateTopicName("kafka-roundtrip");
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
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

        // Give consumer time for group coordinator handshake + partition assignment.
        // Increased to 5s for CI runner reliability under load.
        await Task.Delay(5000);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"event":"order.created","id":"k001"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, $"Message was not received within timeout. Bootstrap={KafkaFixture.BootstrapServers}, Topic={topic}");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(CreateTopicName("kafka-headers"));
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

        await Task.Delay(3000);

        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":1}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-request-id":"req-42"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleMessagesDelivered()
    {
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(CreateTopicName("kafka-multi"));
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

        await Task.Delay(3000);

        for (int i = 0; i < messageCount; i++)
        {
            byte[] json = Encoding.UTF8.GetBytes($$"""{"idx":{{i}}}""");
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
            await s_transport.PublishAsync(channel, doc.RootElement);
        }

        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(allArrived, $"Only received {receivedOrder.Count}/{messageCount} messages.");

        // Kafka guarantees ordering within a single partition
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, receivedOrder.ToArray());

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task UnsubscribeStopsDelivery()
    {
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(CreateTopicName("kafka-unsub"));
        int receiveCount = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(3000);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"n":1}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(2000);

        int countAfterFirst = receiveCount;
        await s_transport.UnsubscribeAsync(channel);
        await Task.Delay(500);

        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"n":2}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(2000);

        // After unsubscribe, no new messages should arrive
        Assert.AreEqual(countAfterFirst, receiveCount);
    }

    [TestMethod]
    public async Task HandlerExceptionInvokesErrorPolicy()
    {
        var actions = new List<MessageErrorKind>();
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-error-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-error-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = new TrackingErrorPolicy(actions),
        });

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Handler failure"));

        await Task.Delay(1000);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"trigger":"error"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(3000);

        Assert.IsTrue(actions.Count > 0, "Error policy was not invoked.");
        Assert.AreEqual(MessageErrorKind.Handler, actions[0]);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task LargePayloadTransmitsCorrectly()
    {
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(CreateTopicName("kafka-large"));
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

        await Task.Delay(3000);

        StringBuilder sb = new("[");
        for (int i = 0; i < 1000; i++)
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

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Large message was not received within timeout.");
        Assert.AreEqual(1000, receivedLength);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task DisposeStopsAllSubscriptions()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-dispose-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-dispose-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        int receiveCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(3000);
        await transport.DisposeAsync();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"dispose"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(2000);

        Assert.AreEqual(0, receiveCount);
    }

    [TestMethod]
    public async Task DeserializationErrorInvokesErrorPolicy()
    {
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-deser-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = policy,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        string topic = $"kafka-deser-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(2000);

        using IProducer<Null, byte[]> rawProducer = new ProducerBuilder<Null, byte[]>(
            new ProducerConfig { BootstrapServers = KafkaFixture.BootstrapServers }).Build();
        await rawProducer.ProduceAsync(topic, new Message<Null, byte[]>
        {
            Value = "this is not valid json!!!"u8.ToArray(),
        });

        await Task.Delay(3000);

        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy was not invoked for deserialization error.");
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterActionSendsToDeadLetterChannel()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.DeadLetter);
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-dl-{topicSuffix}";
        string dlqTopic = topic + ".dead-letter";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        await KafkaFixture.CreateTopicAsync(dlqTopic);

        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-dl-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = policy,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        using var dlqReceived = new SemaphoreSlim(0, 1);
        byte[]? dlqPayload = null;
        using IConsumer<Null, byte[]> dlqConsumer = new ConsumerBuilder<Null, byte[]>(
            new ConsumerConfig
            {
                BootstrapServers = KafkaFixture.BootstrapServers,
                GroupId = "corvus-dlq-reader-" + topicSuffix,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                TopicMetadataRefreshIntervalMs = 1000,
            }).Build();
        dlqConsumer.Subscribe(dlqTopic);
        _ = Task.Run(() =>
        {
            try
            {
                while (true)
                {
                    ConsumeResult<Null, byte[]>? result = dlqConsumer.Consume(TimeSpan.FromSeconds(15));
                    if (result?.Message?.Value is not null)
                    {
                        dlqPayload = result.Message.Value;
                        dlqReceived.Release();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Intentional failure"));

        await Task.Delay(2000);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"order":"DL-001"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(received, "Dead-letter message was not received on DLQ topic.");
        Assert.IsNotNull(dlqPayload);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task AbortActionStopsSubscription()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Abort);
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-abort-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-abort-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = policy,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                throw new InvalidOperationException("Trigger abort");
            });

        await Task.Delay(2000);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":1}"""u8.ToArray());
        await transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(3000);

        int countAfterAbort = handlerCallCount;
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":2}"""u8.ToArray());
        await transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(3000);

        Assert.AreEqual(1, policy.Invocations.Count, "Error policy should be invoked exactly once.");
        Assert.AreEqual(countAfterAbort, handlerCallCount, "Handler should not be called after abort.");

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareWrapsHandlerExecution()
    {
        int middlewareCallCount = 0;
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-mw-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-mw-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
            HandlerMiddleware = async (operation, ct) =>
            {
                Interlocked.Increment(ref middlewareCallCount);
                await operation(ct).ConfigureAwait(false);
            },
        });

        using var received = new SemaphoreSlim(0, 1);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(2000);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"mw-test"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(15));
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
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-mw-exhaust-{topicSuffix}";
        ReadOnlyMemory<byte> channel = await CreateChannelAsync(topic);
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-mw-exhaust-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = policy,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
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

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Always fails"));

        await Task.Delay(2000);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"exhaust-test"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(3000);

        Assert.AreEqual(2, retryCount, "Middleware should have retried 2 times.");
        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy should be invoked after middleware exhaustion.");
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyTimeoutThrows()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-req-timeout-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        string requestTopic = $"kafka-req-{topicSuffix}";
        string replyTopic = $"kafka-reply-{topicSuffix}";
        await KafkaFixture.CreateTopicAsync(requestTopic);
        await KafkaFixture.CreateTopicAsync(replyTopic);
        ReadOnlyMemory<byte> requestChannel = Encoding.UTF8.GetBytes(requestTopic);
        ReadOnlyMemory<byte> replyChannel = Encoding.UTF8.GetBytes(replyTopic);
        byte[] correlationId = "timeout-corr-k01"u8.ToArray();

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
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-disposed-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        await transport.DisposeAsync();

        ReadOnlyMemory<byte> channel = Encoding.UTF8.GetBytes($"kafka-disposed-{topicSuffix}");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":1}"""u8.ToArray());

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.PublishAsync(channel, doc.RootElement));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.SubscribeAsync<JsonElement>(channel, (p, h, ct) => ValueTask.CompletedTask));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.RequestAsync<JsonElement, JsonElement>(
                channel, channel, doc.RootElement, "corr"u8.ToArray()));
    }

    [TestMethod]
    public async Task HealthCheckReportsConnectedAndMessagingSystem()
    {
        // IsConnected and MessagingSystem are properties of IHealthCheckableTransport
        IHealthCheckableTransport healthCheck = s_transport;

        Assert.IsTrue(healthCheck.IsConnected);
        Assert.AreEqual("kafka", healthCheck.MessagingSystem);

        bool pingResult = await healthCheck.PingAsync();
        Assert.IsTrue(pingResult);
    }

    [TestMethod]
    public async Task PingReturnsFalseAfterDispose()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-ping-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        await transport.DisposeAsync();

        Assert.IsFalse(transport.IsConnected);
        bool pingResult = await ((IHealthCheckableTransport)transport).PingAsync();
        Assert.IsFalse(pingResult);
    }

    [TestMethod]
    public async Task DoubleDisposeIsIdempotent()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-double-dispose-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        await transport.DisposeAsync();
        await transport.DisposeAsync(); // Should not throw
    }

    [TestMethod]
    public async Task RequestReplyRoundtrip()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string requestTopic = $"kafka-reqrep-req-{topicSuffix}";
        string replyTopic = $"kafka-reqrep-rep-{topicSuffix}";
        await KafkaFixture.CreateTopicAsync(requestTopic);
        await KafkaFixture.CreateTopicAsync(replyTopic);

        ReadOnlyMemory<byte> requestChannel = Encoding.UTF8.GetBytes(requestTopic);
        ReadOnlyMemory<byte> replyChannel = Encoding.UTF8.GetBytes(replyTopic);

        // Set up a responder: subscribe to request topic, publish reply with correlation ID
        KafkaMessageTransport responderTransport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-responder-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        await responderTransport.SubscribeAsync<JsonElement>(
            requestChannel,
            async (payload, headers, ct) =>
            {
                // Echo back the request with a "reply" field added, preserving correlation
                // We need to read the correlation ID from the original message and attach it to the reply.
                // The transport attaches correlation-id header automatically on request, but
                // the responder needs a raw consumer to access it. For this test, we'll use
                // a dedicated raw consumer/producer to relay properly.
            });

        // The responder approach above won't work easily because the SubscribeAsync handler
        // doesn't expose raw headers with correlation IDs to the user handler. Instead, use
        // a raw Kafka consumer/producer to act as the responder.
        await responderTransport.UnsubscribeAsync(requestChannel);
        await responderTransport.DisposeAsync();

        // Use raw Confluent.Kafka consumer/producer as the responder
        using IConsumer<Null, byte[]> rawConsumer = new ConsumerBuilder<Null, byte[]>(
            new ConsumerConfig
            {
                BootstrapServers = KafkaFixture.BootstrapServers,
                GroupId = "corvus-raw-responder-" + topicSuffix,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                TopicMetadataRefreshIntervalMs = 1000,
            }).Build();
        rawConsumer.Subscribe(requestTopic);

        using IProducer<Null, byte[]> rawProducer = new ProducerBuilder<Null, byte[]>(
            new ProducerConfig { BootstrapServers = KafkaFixture.BootstrapServers }).Build();

        // Start a background task that consumes requests and produces replies
        using CancellationTokenSource responderCts = new();
        Task responderTask = Task.Run(async () =>
        {
            try
            {
                while (!responderCts.Token.IsCancellationRequested)
                {
                    ConsumeResult<Null, byte[]>? result = rawConsumer.Consume(TimeSpan.FromMilliseconds(200));
                    if (result?.IsPartitionEOF != false)
                    {
                        continue;
                    }

                    // Extract correlation ID
                    byte[]? corrId = null;
                    if (result.Message.Headers?.TryGetLastBytes("corvus-correlation-id", out byte[]? corrBytes) == true)
                    {
                        corrId = corrBytes;
                    }

                    // Send reply with same correlation ID
                    byte[] replyPayload = """{"answer":42}"""u8.ToArray();
                    Message<Null, byte[]> replyMsg = new()
                    {
                        Value = replyPayload,
                        Headers = [],
                    };

                    if (corrId is not null)
                    {
                        replyMsg.Headers.Add("corvus-correlation-id", corrId);
                    }

                    await rawProducer.ProduceAsync(replyTopic, replyMsg);
                    rawConsumer.Commit(result);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        // Give the responder time to start
        await Task.Delay(3000);

        // Create client transport for request-reply
        KafkaMessageTransport clientTransport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-reqrep-client-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"question":"meaning of life"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> requestHeaders = ParsedJsonDocument<JsonElement>.Parse("""{"x-trace-id":"trace-123"}"""u8.ToArray());
        byte[] correlationId = Guid.NewGuid().ToString("D").Substring(0, 36).Select(c => (byte)c).ToArray();

        using CancellationTokenSource requestCts = new(TimeSpan.FromSeconds(30));
        (JsonElement replyPayloadElement, JsonElement replyHeaders) = await clientTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId,
            requestHeaders.RootElement,
            requestCts.Token);

        Assert.AreEqual(JsonValueKind.Object, replyPayloadElement.ValueKind);
        Assert.AreEqual(42, replyPayloadElement.GetProperty("answer"u8).GetInt32());

        await responderCts.CancelAsync();
        try { await responderTask; }
        catch (OperationCanceledException) { }

        await clientTransport.DisposeAsync();
    }

    [TestMethod]
    public async Task ExplicitDeadLetterPublishesMessage()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string dlqTopic = $"kafka-explicit-dlq-{topicSuffix}";
        string originalTopic = $"kafka-explicit-orig-{topicSuffix}";
        await KafkaFixture.CreateTopicAsync(dlqTopic);
        await KafkaFixture.CreateTopicAsync(originalTopic);

        ReadOnlyMemory<byte> dlqChannel = Encoding.UTF8.GetBytes(dlqTopic);
        ReadOnlyMemory<byte> originalChannel = Encoding.UTF8.GetBytes(originalTopic);

        // Publish to dead-letter explicitly
        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"failed":"msg"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-trace":"abc"}"""u8.ToArray());

        await s_transport.DeadLetterAsync(
            dlqChannel,
            originalChannel,
            payloadDoc.RootElement,
            headersDoc.RootElement,
            new InvalidOperationException("Test dead-letter reason"));

        // Verify message arrives on the DLQ topic
        using IConsumer<Null, byte[]> dlqConsumer = new ConsumerBuilder<Null, byte[]>(
            new ConsumerConfig
            {
                BootstrapServers = KafkaFixture.BootstrapServers,
                GroupId = "corvus-explicit-dlq-reader-" + topicSuffix,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                TopicMetadataRefreshIntervalMs = 1000,
            }).Build();
        dlqConsumer.Subscribe(dlqTopic);

        ConsumeResult<Null, byte[]>? result = null;
        for (int i = 0; i < 50; i++)
        {
            result = dlqConsumer.Consume(TimeSpan.FromMilliseconds(200));
            if (result?.Message?.Value is not null)
            {
                break;
            }
        }

        Assert.IsNotNull(result?.Message?.Value, "Dead-letter message was not received.");

        // Verify headers contain error metadata
        Assert.IsNotNull(result.Message.Headers);
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-original-channel", out byte[]? origCh));
        Assert.AreEqual(originalTopic, Encoding.UTF8.GetString(origCh!));
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-error", out byte[]? errMsg));
        Assert.AreEqual("Test dead-letter reason", Encoding.UTF8.GetString(errMsg!));
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-error-type", out byte[]? errType));
        Assert.AreEqual("System.InvalidOperationException", Encoding.UTF8.GetString(errType!));

        // Verify the corvus-headers header is also present (since we passed headers)
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-headers", out _));
    }

    [TestMethod]
    public async Task DeserializationErrorWithDeadLetterActionSendsRawToDeadLetterChannel()
    {
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.DeadLetter);
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        string topic = $"kafka-deser-dl-{topicSuffix}";
        string dlqTopic = topic + ".dead-letter";
        await KafkaFixture.CreateTopicAsync(topic);
        await KafkaFixture.CreateTopicAsync(dlqTopic);
        ReadOnlyMemory<byte> channel = Encoding.UTF8.GetBytes(topic);

        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-deser-dl-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            ErrorPolicy = policy,
            ConsumerConfig = new ConsumerConfig { TopicMetadataRefreshIntervalMs = 1000 },
        });

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(2000);

        // Send invalid JSON that will fail deserialization
        byte[] invalidJson = "NOT VALID JSON {{{"u8.ToArray();
        using IProducer<Null, byte[]> rawProducer = new ProducerBuilder<Null, byte[]>(
            new ProducerConfig { BootstrapServers = KafkaFixture.BootstrapServers }).Build();
        await rawProducer.ProduceAsync(topic, new Message<Null, byte[]> { Value = invalidJson });

        // Check the DLQ receives the raw bytes
        using IConsumer<Null, byte[]> dlqConsumer = new ConsumerBuilder<Null, byte[]>(
            new ConsumerConfig
            {
                BootstrapServers = KafkaFixture.BootstrapServers,
                GroupId = "corvus-deser-dl-reader-" + topicSuffix,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                TopicMetadataRefreshIntervalMs = 1000,
            }).Build();
        dlqConsumer.Subscribe(dlqTopic);

        ConsumeResult<Null, byte[]>? result = null;
        for (int i = 0; i < 50; i++)
        {
            result = dlqConsumer.Consume(TimeSpan.FromMilliseconds(200));
            if (result?.Message?.Value is not null)
            {
                break;
            }
        }

        Assert.IsNotNull(result?.Message?.Value, "Dead-letter message was not received on DLQ.");

        // The raw bytes should be preserved
        CollectionAssert.AreEqual(invalidJson, result.Message.Value);

        // Should have error metadata headers
        Assert.IsNotNull(result.Message.Headers);
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-original-channel", out byte[]? origCh));
        Assert.AreEqual(topic, Encoding.UTF8.GetString(origCh!));
        Assert.IsTrue(result.Message.Headers.TryGetLastBytes("corvus-error-type", out _));

        Assert.IsTrue(policy.Invocations.Count > 0);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterAsyncAfterDisposeThrowsObjectDisposedException()
    {
        string topicSuffix = Guid.NewGuid().ToString("N")[..8];
        KafkaMessageTransport transport = new(new KafkaTransportOptions
        {
            BootstrapServers = KafkaFixture.BootstrapServers,
            GroupId = "corvus-dl-disposed-group-" + topicSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        await transport.DisposeAsync();

        ReadOnlyMemory<byte> dlqChannel = Encoding.UTF8.GetBytes($"kafka-dl-disposed-{topicSuffix}");
        ReadOnlyMemory<byte> origChannel = Encoding.UTF8.GetBytes("original");

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await transport.DeadLetterAsync(
                dlqChannel,
                origChannel,
                default,
                default,
                new Exception("test")));
    }

    private static string CreateTopicName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static async Task<ReadOnlyMemory<byte>> CreateChannelAsync(string topic)
    {
        await KafkaFixture.CreateTopicAsync(topic).ConfigureAwait(false);
        return Encoding.UTF8.GetBytes(topic);
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