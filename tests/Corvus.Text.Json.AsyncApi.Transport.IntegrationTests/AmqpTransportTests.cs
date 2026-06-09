// <copyright file="AmqpTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.Amqp;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AmqpMessageTransport"/> against a real RabbitMQ broker.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class AmqpTransportTests
{
    private static AmqpMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await AmqpFixture.StartAsync();
        s_transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-test",
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await AmqpFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        ReadOnlyMemory<byte> channel = "amqp.test.roundtrip"u8.ToArray();
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

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"order":"abc123","total":99.95}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        ReadOnlyMemory<byte> channel = "amqp.test.headers"u8.ToArray();
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

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":42}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-tenant":"acme","x-region":"eu-west-1"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleMessagesDeliveredInOrder()
    {
        ReadOnlyMemory<byte> channel = "amqp.test.ordering"u8.ToArray();
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

        await Task.Delay(300);

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
        ReadOnlyMemory<byte> channel = "amqp.test.unsub"u8.ToArray();
        int receiveCount = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(300);

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
    public async Task MultipleSubscribersOnDifferentRoutingKeys()
    {
        ReadOnlyMemory<byte> channel1 = "amqp.test.multi.a"u8.ToArray();
        ReadOnlyMemory<byte> channel2 = "amqp.test.multi.b"u8.ToArray();
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

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> docA = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"a"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> docB = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"b"}"""u8.ToArray());
        await s_transport.PublishAsync(channel1, docA.RootElement);
        await s_transport.PublishAsync(channel2, docB.RootElement);

        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(received, "Both routing keys did not receive messages.");
        Assert.AreEqual(1, countA);
        Assert.AreEqual(1, countB);

        await s_transport.UnsubscribeAsync(channel1);
        await s_transport.UnsubscribeAsync(channel2);
    }

    [TestMethod]
    public async Task HandlerExceptionInvokesErrorPolicy()
    {
        var actions = new List<MessageErrorKind>();
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.error",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-error",
            ErrorPolicy = new TrackingErrorPolicy(actions),
        });

        ReadOnlyMemory<byte> channel = "amqp.test.error"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Handler failure"));

        await Task.Delay(300);

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
        ReadOnlyMemory<byte> channel = "amqp.test.large"u8.ToArray();
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

        await Task.Delay(300);

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
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.dispose",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-dispose",
        });

        ReadOnlyMemory<byte> channel = "amqp.test.dispose"u8.ToArray();
        int receiveCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(300);
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
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.deser",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-deser",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.deser"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(500);

        RabbitMQ.Client.ConnectionFactory rawFactory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using RabbitMQ.Client.IConnection rawConn = await rawFactory.CreateConnectionAsync();
        using RabbitMQ.Client.IChannel rawChannel = await rawConn.CreateChannelAsync();
        await rawChannel.ExchangeDeclareAsync("corvus.test.deser", "topic", durable: false, autoDelete: false);
        await rawChannel.BasicPublishAsync(
            exchange: "corvus.test.deser",
            routingKey: "amqp.test.deser",
            mandatory: false,
            basicProperties: new RabbitMQ.Client.BasicProperties(),
            body: "THIS IS NOT VALID JSON!!!"u8.ToArray());

        await Task.Delay(500);

        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy was not invoked for deserialization error.");
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithDeadLetterAction()
    {
        // Arrange — policy returns DeadLetter for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.DeadLetter);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.deser-dl",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-deser-dl",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.deser-deadletter"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);
        byte[]? dlqPayload = null;

        // Set up a raw consumer on the dead-letter exchange (same pattern as existing DL test)
        RabbitMQ.Client.ConnectionFactory dlqFactory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using RabbitMQ.Client.IConnection dlqConn = await dlqFactory.CreateConnectionAsync();
        using RabbitMQ.Client.IChannel dlqCh = await dlqConn.CreateChannelAsync();

        await dlqCh.ExchangeDeclareAsync("corvus.dead-letter", "topic", durable: true, autoDelete: false);
        RabbitMQ.Client.QueueDeclareOk dlqQueue = await dlqCh.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true);
        await dlqCh.QueueBindAsync(dlqQueue.QueueName, "corvus.dead-letter", "amqp.test.deser-deadletter.dead-letter");

        RabbitMQ.Client.Events.AsyncEventingBasicConsumer dlqConsumer = new(dlqCh);
        dlqConsumer.ReceivedAsync += (sender, ea) =>
        {
            dlqPayload = ea.Body.ToArray();
            dlqReceived.Release();
            return Task.CompletedTask;
        };

        await dlqCh.BasicConsumeAsync(
            queue: dlqQueue.QueueName,
            autoAck: true,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: dlqConsumer,
            cancellationToken: default);

        await Task.Delay(300);

        // Subscribe with typed handler (will not be called due to deser failure)
        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(500);

        // Act — publish invalid JSON directly via raw AMQP
        ConnectionFactory factory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using IConnection rawConn = await factory.CreateConnectionAsync();
        using IChannel rawChannel = await rawConn.CreateChannelAsync();

        await rawChannel.BasicPublishAsync(
            exchange: "corvus.test.deser-dl",
            routingKey: "amqp.test.deser-deadletter",
            mandatory: false,
            basicProperties: new BasicProperties { ContentType = "application/json" },
            body: "THIS IS NOT JSON!!!"u8.ToArray());

        // Assert — dead-lettered message should arrive on DLQ
        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Deserialization error was not dead-lettered.");
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);
        Assert.IsNotNull(dlqPayload);

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithAbortAction()
    {
        // Arrange — policy returns Abort for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Abort);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.deser-abort",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-deser-abort",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.deser-abort"u8.ToArray();
        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish invalid JSON via raw AMQP
        ConnectionFactory factory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using IConnection rawConn = await factory.CreateConnectionAsync();
        using IChannel rawChannel = await rawConn.CreateChannelAsync();

        await rawChannel.BasicPublishAsync(
            exchange: "corvus.test.deser-abort",
            routingKey: "amqp.test.deser-abort",
            mandatory: false,
            basicProperties: new BasicProperties { ContentType = "application/json" },
            body: "NOT VALID JSON"u8.ToArray());

        await Task.Delay(1000);

        // Publish a valid message — should NOT be received (subscription aborted)
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"after-abort"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        // Assert
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);
        Assert.AreEqual(0, handlerCallCount, "Handler should never be called after deserialization abort.");

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterActionSendsToDeadLetterChannel()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.DeadLetter);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.dl",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-dl",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.dl"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);
        byte[]? dlqPayload = null;

        RabbitMQ.Client.ConnectionFactory dlqFactory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using RabbitMQ.Client.IConnection dlqConn = await dlqFactory.CreateConnectionAsync();
        using RabbitMQ.Client.IChannel dlqChannel = await dlqConn.CreateChannelAsync();

        await dlqChannel.ExchangeDeclareAsync("corvus.dead-letter", "topic", durable: true, autoDelete: false);
        RabbitMQ.Client.QueueDeclareOk dlqQueue = await dlqChannel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true);
        await dlqChannel.QueueBindAsync(dlqQueue.QueueName, "corvus.dead-letter", "amqp.test.dl.dead-letter");

        RabbitMQ.Client.Events.AsyncEventingBasicConsumer dlqConsumer = new(dlqChannel);
        dlqConsumer.ReceivedAsync += (sender, ea) =>
        {
            dlqPayload = ea.Body.ToArray();
            dlqReceived.Release();
            return Task.CompletedTask;
        };

        await dlqChannel.BasicConsumeAsync(
            queue: dlqQueue.QueueName,
            autoAck: true,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: dlqConsumer,
            cancellationToken: default);

        await Task.Delay(300);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Intentional failure"));

        await Task.Delay(300);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"order":"DL-001"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Dead-letter message was not received on DLQ.");
        Assert.IsNotNull(dlqPayload);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task AbortActionStopsSubscription()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Abort);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.abort",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-abort",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.abort"u8.ToArray();
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
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.mw",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-mw",
            HandlerMiddleware = async (operation, ct) =>
            {
                Interlocked.Increment(ref middlewareCallCount);
                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "amqp.test.mw"u8.ToArray();
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
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.mw.exhaust",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-mw-exhaust",
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

        ReadOnlyMemory<byte> channel = "amqp.test.mw.exhaust"u8.ToArray();

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
    public async Task RequestReplyTimeoutThrows()
    {
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.req",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-req",
        });

        ReadOnlyMemory<byte> requestChannel = "amqp.test.req"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "amqp.test.reply"u8.ToArray();
        byte[] correlationId = "timeout-corr-a01"u8.ToArray();

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
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.disposed",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-disposed",
        });

        await transport.DisposeAsync();

        ReadOnlyMemory<byte> channel = "amqp.test.disposed"u8.ToArray();
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

    [TestMethod]
    public async Task RequestReplyRoundtripWithResponder()
    {
        // Arrange — create a fresh transport for request/reply
        AmqpMessageTransport requesterTransport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.reqreply",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-reqreply",
        });

        // Set up a raw AMQP consumer that listens for requests and publishes replies
        ConnectionFactory factory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using IConnection responderConn = await factory.CreateConnectionAsync();
        using IChannel responderChannel = await responderConn.CreateChannelAsync();

        await responderChannel.ExchangeDeclareAsync(
            exchange: "corvus.test.reqreply",
            type: "topic",
            durable: false);

        string responderQueue = (await responderChannel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true)).QueueName;

        await responderChannel.QueueBindAsync(
            queue: responderQueue,
            exchange: "corvus.test.reqreply",
            routingKey: "amqp.test.reqreply.request");

        AsyncEventingBasicConsumer responder = new(responderChannel);
        responder.ReceivedAsync += async (_, args) =>
        {
            // Read correlation ID and reply-to from message properties
            string? corrId = args.BasicProperties?.CorrelationId;
            string? replyTo = args.BasicProperties?.ReplyTo;

            if (corrId is not null && replyTo is not null)
            {
                BasicProperties replyProps = new()
                {
                    ContentType = "application/json",
                    CorrelationId = corrId,
                };

                await responderChannel.BasicPublishAsync(
                    exchange: "corvus.test.reqreply",
                    routingKey: replyTo,
                    mandatory: false,
                    basicProperties: replyProps,
                    body: Encoding.UTF8.GetBytes("""{"result":"success","value":99}"""));
            }

            await responderChannel.BasicAckAsync(args.DeliveryTag, multiple: false);
        };

        await responderChannel.BasicConsumeAsync(
            queue: responderQueue,
            autoAck: false,
            consumer: responder);

        await Task.Delay(500);

        // Act — send a request through the transport
        ReadOnlyMemory<byte> requestChannel = "amqp.test.reqreply.request"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "amqp.test.reqreply.reply"u8.ToArray();
        byte[] correlationId = "amqp-roundtrip-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"action":"compute"}"""u8.ToArray());

        (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(99, replyPayload.GetProperty("value"u8).GetInt32());

        await requesterTransport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyResponderRoundTrip()
    {
        // Arrange — model two services on the same broker exchange: a responder service and a
        // separate requester service. The reply routes back via the request's ReplyTo/CorrelationId.
        static AmqpTransportOptions Options() => new()
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.responder",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-responder",
        };

        AmqpMessageTransport responder = await AmqpMessageTransport.CreateAsync(Options());
        AmqpMessageTransport requester = await AmqpMessageTransport.CreateAsync(Options());

        try
        {
            ReadOnlyMemory<byte> requestChannel = "amqp.test.responder.request"u8.ToArray();
            ReadOnlyMemory<byte> replyChannel = "amqp.test.responder.reply"u8.ToArray();
            byte[] correlationId = "amqp-responder-001"u8.ToArray();

            // The responder service: doubles the request's "value" field into the reply.
            await responder.SubscribeReplyAsync<JsonElement, JsonElement>(
                requestChannel,
                (request, headers, ct) =>
                {
                    int value = request.GetProperty("value"u8).GetInt32();

                    // The reply document must outlive the handler: the transport serializes the
                    // returned JsonElement after the handler completes, so it is not disposed here.
                    ParsedJsonDocument<JsonElement> replyDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes($$"""{"doubled":{{value * 2}}}"""));
                    return ValueTask.FromResult(replyDoc.RootElement);
                });

            await Task.Delay(500);

            // Act — the requester service sends a request.
            using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"value":21}"""u8.ToArray());
            (JsonElement replyPayload, _) = await requester.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId);

            // Assert — the responder doubled the value.
            Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
            Assert.AreEqual(42, replyPayload.GetProperty("doubled"u8).GetInt32());

            await responder.UnsubscribeAsync(requestChannel);
        }
        finally
        {
            await responder.DisposeAsync();
            await requester.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task RequestReplyRoundtripWithHeadersForwardsHeaders()
    {
        // Arrange — verify that headers are included in request messages
        AmqpMessageTransport requesterTransport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.reqreply-hdr",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-reqreply-hdr",
        });

        // Set up a raw AMQP consumer that checks for headers and replies
        ConnectionFactory factory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using IConnection responderConn = await factory.CreateConnectionAsync();
        using IChannel responderChannel = await responderConn.CreateChannelAsync();

        await responderChannel.ExchangeDeclareAsync(
            exchange: "corvus.test.reqreply-hdr",
            type: "topic",
            durable: false);

        string responderQueue = (await responderChannel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true)).QueueName;

        await responderChannel.QueueBindAsync(
            queue: responderQueue,
            exchange: "corvus.test.reqreply-hdr",
            routingKey: "amqp.test.reqreply-hdr.request");

        bool headersReceived = false;
        AsyncEventingBasicConsumer responder = new(responderChannel);
        responder.ReceivedAsync += async (_, args) =>
        {
            string? corrId = args.BasicProperties?.CorrelationId;
            string? replyTo = args.BasicProperties?.ReplyTo;

            // Check if headers were forwarded
            if (args.BasicProperties?.Headers?.ContainsKey("corvus-headers") == true)
            {
                headersReceived = true;
            }

            if (corrId is not null && replyTo is not null)
            {
                BasicProperties replyProps = new()
                {
                    ContentType = "application/json",
                    CorrelationId = corrId,
                };

                await responderChannel.BasicPublishAsync(
                    exchange: "corvus.test.reqreply-hdr",
                    routingKey: replyTo,
                    mandatory: false,
                    basicProperties: replyProps,
                    body: Encoding.UTF8.GetBytes("""{"ack":"ok"}"""));
            }

            await responderChannel.BasicAckAsync(args.DeliveryTag, multiple: false);
        };

        await responderChannel.BasicConsumeAsync(
            queue: responderQueue,
            autoAck: false,
            consumer: responder);

        await Task.Delay(500);

        // Act — send a request with headers
        ReadOnlyMemory<byte> requestChannel = "amqp.test.reqreply-hdr.request"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "amqp.test.reqreply-hdr.reply"u8.ToArray();
        byte[] correlationId = "amqp-hdr-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"action":"check-headers"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"authToken":"secret-xyz"}"""u8.ToArray());

        (JsonElement replyPayload, _) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId,
            headersDoc.RootElement);

        // Assert — reply received and headers were in the request
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.IsTrue(headersReceived, "Headers were not forwarded in the AMQP request message.");

        await requesterTransport.DisposeAsync();
    }

    [TestMethod]
    public async Task DoubleDisposeDoesNotThrow()
    {
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.double-dispose",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-dd",
        });

        await transport.DisposeAsync();
        await transport.DisposeAsync(); // Should be safe — no exception
    }

    [TestMethod]
    public async Task HandlerErrorWithSkipContinuesDelivery()
    {
        // Arrange — policy returns Skip for handler errors
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Skip);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.skip-cont",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-skip-cont",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.skip-continues"u8.ToArray();
        int handlerSuccessCount = 0;
        using var secondReceived = new SemaphoreSlim(0, 1);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                if (payload.GetProperty("fail"u8).ValueKind == JsonValueKind.True)
                {
                    throw new InvalidOperationException("Intentional handler failure");
                }

                Interlocked.Increment(ref handlerSuccessCount);
                secondReceived.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish a message that triggers handler error (will be skipped)
        using ParsedJsonDocument<JsonElement> badDoc = ParsedJsonDocument<JsonElement>.Parse("""{"fail":true}"""u8.ToArray());
        await transport.PublishAsync(channel, badDoc.RootElement);
        await Task.Delay(500);

        // Now publish a valid message — subscription should still be alive
        using ParsedJsonDocument<JsonElement> goodDoc = ParsedJsonDocument<JsonElement>.Parse("""{"fail":false}"""u8.ToArray());
        await transport.PublishAsync(channel, goodDoc.RootElement);

        // Assert
        bool received = await secondReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Second message was not received — subscription stopped after skip.");
        Assert.AreEqual(1, handlerSuccessCount);
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithSkipContinuesDelivery()
    {
        // Arrange — policy returns Skip for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);
        AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(new AmqpTransportOptions
        {
            ConnectionUri = AmqpFixture.ConnectionUri,
            ExchangeName = "corvus.test.deser-skip-cont",
            ExchangeType = "topic",
            ExchangeDurable = false,
            ConsumerTagPrefix = "corvus-dsc",
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "amqp.test.deser-skip-cont"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedKind = JsonValueKind.Undefined;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedKind = payload.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish invalid JSON via raw AMQP (triggers deser error → skip)
        RabbitMQ.Client.ConnectionFactory rawFactory = new() { Uri = new Uri(AmqpFixture.ConnectionUri) };
        using RabbitMQ.Client.IConnection rawConn = await rawFactory.CreateConnectionAsync();
        using RabbitMQ.Client.IChannel rawChannel = await rawConn.CreateChannelAsync();
        await rawChannel.ExchangeDeclareAsync("corvus.test.deser-skip-cont", "topic", durable: false, autoDelete: false);
        await rawChannel.BasicPublishAsync(
            exchange: "corvus.test.deser-skip-cont",
            routingKey: "amqp.test.deser-skip-cont",
            mandatory: false,
            basicProperties: new RabbitMQ.Client.BasicProperties(),
            body: "NOT JSON!!!"u8.ToArray());
        await Task.Delay(500);

        // Now publish a valid message — subscription should still be alive
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"skip"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Valid message was not received after deser error + skip.");
        Assert.AreEqual(JsonValueKind.Object, receivedKind);
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
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