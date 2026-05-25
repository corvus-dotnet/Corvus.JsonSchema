// <copyright file="NatsTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.Nats;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;
using NATS.Client.Core;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="NatsMessageTransport"/> against a real NATS server.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class NatsTransportTests
{
    private static NatsMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await NatsFixture.StartAsync();
        s_transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await NatsFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.roundtrip"u8.ToArray();
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

        // Allow subscription loop to start on the thread pool before publishing.
        await Task.Delay(500);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"sensor":"temp","value":22.5}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "[NATS] Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task SubscribeBeforePublishReceivesMessage()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.sub-first"u8.ToArray();
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

        // Small delay to ensure subscription is active
        await Task.Delay(100);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"hello"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task HeadersRoundtripCorrectly()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.headers"u8.ToArray();
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

        await Task.Delay(100);

        // Act
        using ParsedJsonDocument<JsonElement> payloadDoc = ParsedJsonDocument<JsonElement>.Parse("""{"data":1}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> headersDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x-trace-id":"abc123","x-priority":"high"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, payloadDoc.RootElement, headersDoc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task MultipleMessagesDeliveredInOrder()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.ordering"u8.ToArray();
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

        await Task.Delay(100);

        // Act
        for (int i = 0; i < messageCount; i++)
        {
            byte[] json = Encoding.UTF8.GetBytes($$"""{"idx":{{i}}}""");
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
            await s_transport.PublishAsync(channel, doc.RootElement);
        }

        // Assert
        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(allArrived, $"Only received {receivedOrder.Count}/{messageCount} messages.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, receivedOrder.ToArray());

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task UnsubscribeStopsDelivery()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.unsub"u8.ToArray();
        int receiveCount = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(100);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"n":1}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(200);

        // Act
        await s_transport.UnsubscribeAsync(channel);
        await Task.Delay(100);

        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"n":2}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(500);

        // Assert
        Assert.AreEqual(1, receiveCount);
    }

    [TestMethod]
    public async Task RequestReplyWithCorrelationId()
    {
        // Arrange — set up a responder on the request channel
        ReadOnlyMemory<byte> requestChannel = "test.request"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "test.reply"u8.ToArray();

        await s_transport.SubscribeAsync<JsonElement>(
            requestChannel,
            async (payload, headers, ct) =>
            {
                // Echo back a response — NATS reply is handled via native inbox
                // For this test we verify the request doesn't hang
                await Task.CompletedTask;
            });

        await Task.Delay(100);

        // Act & Assert — the native NATS request/reply should work
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"question":"ping"}"""u8.ToArray());
        byte[] correlationId = "corr-001"u8.ToArray();

        // NATS request/reply uses an inbox — the reply comes from the NATS server
        // if no handler responds within timeout, it throws OperationCanceledException
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));

        try
        {
            (JsonElement reply, JsonElement replyHeaders) = await s_transport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId,
                cancellationToken: cts.Token);

            // If we get here, a reply was received (the subscription handler or NATS handled it)
            // The important thing is that the correlation mechanism worked without throwing
        }
        catch (OperationCanceledException)
        {
            // Expected — no handler is configured to reply in this test.
            // The test validates that request/reply doesn't crash and respects timeout.
        }

        await s_transport.UnsubscribeAsync(requestChannel);
    }

    [TestMethod]
    public async Task MultipleSubscribersOnDifferentChannels()
    {
        // Arrange
        ReadOnlyMemory<byte> channel1 = "test.multi.a"u8.ToArray();
        ReadOnlyMemory<byte> channel2 = "test.multi.b"u8.ToArray();
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

        await Task.Delay(100);

        // Act
        using ParsedJsonDocument<JsonElement> docA = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"a"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> docB = ParsedJsonDocument<JsonElement>.Parse("""{"ch":"b"}"""u8.ToArray());
        await s_transport.PublishAsync(channel1, docA.RootElement);
        await s_transport.PublishAsync(channel2, docB.RootElement);

        // Assert
        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(received, "Both channels did not receive messages.");
        Assert.AreEqual(1, countA);
        Assert.AreEqual(1, countB);

        await s_transport.UnsubscribeAsync(channel1);
        await s_transport.UnsubscribeAsync(channel2);
    }

    [TestMethod]
    public async Task HandlerExceptionInvokesErrorPolicy()
    {
        // Arrange — create a separate transport with a tracking error policy
        var actions = new List<MessageErrorKind>();
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = new TrackingErrorPolicy(actions),
        });

        ReadOnlyMemory<byte> channel = "test.error-policy"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Handler failure"));

        await Task.Delay(100);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"trigger":"error"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(500);

        // Assert
        Assert.IsTrue(actions.Count > 0, "Error policy was not invoked.");
        Assert.AreEqual(MessageErrorKind.Handler, actions[0]);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task LargePayloadTransmitsCorrectly()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test.large"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        int receivedLength = 0;

        await s_transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                // Count the array elements to verify no truncation
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

        await Task.Delay(100);

        // Act — build a large array (~100KB)
        StringBuilder sb = new("[");
        for (int i = 0; i < 5000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($$"""{"index":{{i}},"data":"padding-value-to-increase-size-xxxxxxxxxxxxxxxxx"}""");
        }

        sb.Append(']');
        byte[] largePayload = Encoding.UTF8.GetBytes(sb.ToString());
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(largePayload);
        await s_transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(wasReceived, "Large message was not received within timeout.");
        Assert.AreEqual(5000, receivedLength);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task DisposeStopsAllSubscriptions()
    {
        // Arrange — use a dedicated transport
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
        });

        ReadOnlyMemory<byte> channel = "test.dispose"u8.ToArray();
        int receiveCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(100);

        // Act
        await transport.DisposeAsync();

        // Publish on main transport — should not be received by disposed one
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"dispose"}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(500);

        // Assert
        Assert.AreEqual(0, receiveCount);
    }

    /// <summary>
    /// Error policy that tracks which error kinds were reported.
    /// </summary>
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

    /// <summary>
    /// Error policy that returns a configurable action for each error kind.
    /// </summary>
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

    [TestMethod]
    public async Task DeserializationErrorInvokesErrorPolicy()
    {
        // Arrange — create a transport with a tracking error policy
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "test.deser-error"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(500);

        // Act — publish raw invalid JSON via a separate NATS connection
        NATS.Client.Core.NatsConnection rawConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await rawConn.ConnectAsync();
        await rawConn.PublishAsync("test.deser-error", "this is not valid json!!!"u8.ToArray());
        await Task.Delay(500);
        await rawConn.DisposeAsync();

        // Assert
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
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = policy,
            DeadLetterSuffix = ".deser-dlq",
        });

        ReadOnlyMemory<byte> channel = "test.deser-deadletter"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);

        // Subscribe to the DLQ channel
        NATS.Client.Core.NatsConnection rawConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await rawConn.ConnectAsync();
        _ = Task.Run(async () =>
        {
            await foreach (NATS.Client.Core.NatsMsg<byte[]> msg in rawConn.SubscribeAsync<byte[]>("test.deser-deadletter.deser-dlq"))
            {
                dlqReceived.Release();
                break;
            }
        });

        await Task.Delay(200);

        // Subscribe with typed handler (will never be called due to deser failure)
        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(500);

        // Act — publish invalid JSON via raw NATS
        await rawConn.PublishAsync("test.deser-deadletter", "NOT VALID JSON!!!"u8.ToArray());

        // Assert — dead-lettered message should arrive on DLQ
        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Deserialization error was not dead-lettered.");
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
        await rawConn.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithAbortAction()
    {
        // Arrange — policy returns Abort for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Abort);
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "test.deser-abort"u8.ToArray();
        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish invalid JSON via raw NATS (triggers deserialization error → abort)
        NATS.Client.Core.NatsConnection rawConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await rawConn.ConnectAsync();
        await rawConn.PublishAsync("test.deser-abort", "NOT VALID JSON"u8.ToArray());
        await Task.Delay(1000);

        // Now publish a valid message — should NOT be received (subscription aborted)
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"after-abort"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        // Assert
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);
        Assert.AreEqual(0, handlerCallCount, "Handler should never be called (first message failed deser, then subscription aborted).");

        await transport.DisposeAsync();
        await rawConn.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterActionSendsToDeadLetterChannel()
    {
        // Arrange — policy returns DeadLetter for handler errors
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.DeadLetter);
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = policy,
            DeadLetterSuffix = ".dlq",
        });

        ReadOnlyMemory<byte> channel = "test.deadletter"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);
        byte[]? dlqPayload = null;

        // Subscribe to the dead-letter channel with a raw NATS connection to see DLQ messages
        NATS.Client.Core.NatsConnection rawConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await rawConn.ConnectAsync();
        _ = Task.Run(async () =>
        {
            await foreach (NATS.Client.Core.NatsMsg<byte[]> msg in rawConn.SubscribeAsync<byte[]>("test.deadletter.dlq"))
            {
                dlqPayload = msg.Data;
                dlqReceived.Release();
                break;
            }
        });

        await Task.Delay(200);

        // Subscribe on main channel with a handler that throws
        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Intentional handler failure"));

        await Task.Delay(500);

        // Act — publish a valid message
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"order":"O-123"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        // Assert — message should arrive on DLQ
        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Dead-letter message was not received on DLQ channel.");
        Assert.IsNotNull(dlqPayload);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
        await rawConn.DisposeAsync();
    }

    [TestMethod]
    public async Task AbortActionStopsSubscription()
    {
        // Arrange — policy returns Abort for handler errors
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Abort);
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "test.abort"u8.ToArray();
        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                throw new InvalidOperationException("Trigger abort");
            });

        await Task.Delay(500);

        // Act — publish first message (triggers abort)
        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":1}"""u8.ToArray());
        await transport.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(1000);

        // Publish second message — should NOT be received (subscription aborted)
        int countAfterAbort = handlerCallCount;
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":2}"""u8.ToArray());
        await transport.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(1000);

        // Assert
        Assert.AreEqual(1, policy.Invocations.Count, "Error policy should be invoked exactly once.");
        Assert.AreEqual(countAfterAbort, handlerCallCount, "Handler should not be called after abort.");

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareWrapsHandlerExecution()
    {
        // Arrange — middleware that counts invocations
        int middlewareCallCount = 0;
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            HandlerMiddleware = async (operation, ct) =>
            {
                Interlocked.Increment(ref middlewareCallCount);
                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "test.middleware"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"test"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received through middleware.");
        Assert.AreEqual(1, middlewareCallCount, "Middleware should be called exactly once.");

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareExhaustionFallsThroughToErrorPolicy()
    {
        // Arrange — middleware retries 2 times then rethrows; policy catches
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Skip);
        int retryCount = 0;
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
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

                // Final attempt — let exception propagate
                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "test.middleware-exhaust"u8.ToArray();

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Always fails"));

        await Task.Delay(500);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"retry-test"}"""u8.ToArray());
        await transport.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        // Assert — middleware retried, then error policy caught the terminal failure
        Assert.AreEqual(2, retryCount, "Middleware should have retried 2 times.");
        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy should be invoked after middleware exhaustion.");
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await transport.UnsubscribeAsync(channel);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyTimeoutThrowsOperationCanceledException()
    {
        // Arrange — no responder, should timeout
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            RequestTimeout = TimeSpan.FromSeconds(2),
        });

        ReadOnlyMemory<byte> requestChannel = "test.request-timeout"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "test.reply-timeout"u8.ToArray();
        byte[] correlationId = "timeout-corr-001"u8.ToArray();

        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"q":"hello"}"""u8.ToArray());

        // Act & Assert — should timeout or get "no responders" error
        // NATS throws NatsNoRespondersException when no subscriber is available for request/reply
        try
        {
            await transport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId);

            Assert.Fail("Expected an exception for request with no responder.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or NATS.Client.Core.NatsNoRespondersException)
        {
            // Either timeout or no-responders is acceptable — both indicate the request failed
            // as expected when no handler is available.
        }

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task OperationsAfterDisposeThrowObjectDisposedException()
    {
        // Arrange
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
        });

        await transport.DisposeAsync();

        // Act & Assert
        ReadOnlyMemory<byte> channel = "test.disposed"u8.ToArray();
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
    public async Task SubjectWildcardSubscription()
    {
        // Arrange — NATS supports wildcard subjects (foo.* matches foo.bar, foo.baz)
        NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
        });

        ReadOnlyMemory<byte> wildcardChannel = "test.wild.*"u8.ToArray();
        List<string> receivedSubjects = [];
        using var allReceived = new SemaphoreSlim(0, 1);

        await transport.SubscribeAsync<JsonElement>(
            wildcardChannel,
            (payload, headers, ct) =>
            {
                string val = payload.GetProperty("src"u8).GetString()!;
                lock (receivedSubjects)
                {
                    receivedSubjects.Add(val);
                    if (receivedSubjects.Count == 2)
                    {
                        allReceived.Release();
                    }
                }

                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish to two subjects that match the wildcard
        // Publish via raw NATS to specific subjects (wildcard only applies to subscription)
        NATS.Client.Core.NatsConnection rawConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await rawConn.ConnectAsync();
        await rawConn.PublishAsync("test.wild.alpha", """{"src":"alpha"}"""u8.ToArray());
        await rawConn.PublishAsync("test.wild.beta", """{"src":"beta"}"""u8.ToArray());

        // Assert
        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(allArrived, "Not all wildcard messages were received.");
        CollectionAssert.Contains(receivedSubjects.ToArray(), "alpha");
        CollectionAssert.Contains(receivedSubjects.ToArray(), "beta");

        await transport.UnsubscribeAsync(wildcardChannel);
        await transport.DisposeAsync();
        await rawConn.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyRoundtripWithResponder()
    {
        // Arrange — transport for the requester
        NatsMessageTransport requesterTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
        {
            Url = NatsFixture.ConnectionString,
            RequestTimeout = TimeSpan.FromSeconds(10),
        });

        // Set up a raw NATS subscriber that replies to requests on "test.request-roundtrip"
        NATS.Client.Core.NatsConnection responderConn = new(new NATS.Client.Core.NatsOpts { Url = NatsFixture.ConnectionString });
        await responderConn.ConnectAsync();

        _ = Task.Run(async () =>
        {
            await foreach (NATS.Client.Core.NatsMsg<byte[]> msg in responderConn.SubscribeAsync<byte[]>("test.request-roundtrip"))
            {
                if (msg.ReplyTo is not null)
                {
                    // Echo back a response
                    await responderConn.PublishAsync(msg.ReplyTo, """{"answer":42}"""u8.ToArray());
                }
            }
        });

        await Task.Delay(500);

        // Act — send a request
        ReadOnlyMemory<byte> requestChannel = "test.request-roundtrip"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "test.reply-roundtrip"u8.ToArray();
        byte[] correlationId = "roundtrip-corr-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"question":"what is the meaning?"}"""u8.ToArray());

        (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(42, replyPayload.GetProperty("answer"u8).GetInt32());

        await requesterTransport.DisposeAsync();
        await responderConn.DisposeAsync();
    }
}