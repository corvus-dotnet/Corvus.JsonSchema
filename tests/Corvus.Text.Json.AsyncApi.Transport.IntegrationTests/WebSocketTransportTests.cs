// <copyright file="WebSocketTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.WebSockets;
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

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
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

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
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
    public async Task ConcurrentPublishesDoNotThrowAndDeliverAllMessages()
    {
        ReadOnlyMemory<byte> channel = "ws/test/concurrent-publish"u8.ToArray();
        const int messageCount = 40;
        int receivedCount = 0;
        using var allReceived = new SemaphoreSlim(0, 1);

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                if (Interlocked.Increment(ref receivedCount) == messageCount)
                {
                    allReceived.Release();
                }

                return ValueTask.CompletedTask;
            });

        await Task.Delay(300);

        Task[] publishTasks = new Task[messageCount];
        for (int i = 0; i < messageCount; i++)
        {
            int messageIndex = i;
            publishTasks[i] = Task.Run(async () =>
            {
                byte[] json = Encoding.UTF8.GetBytes($$"""{"idx":{{messageIndex}}}""");
                using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
                await s_publisher.PublishAsync(channel, doc.RootElement);
            });
        }

        await Task.WhenAll(publishTasks);

        bool allArrived = await allReceived.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsTrue(allArrived, $"Only received {receivedCount}/{messageCount} concurrent messages.");
        Assert.AreEqual(messageCount, receivedCount);

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

        bool received = await bothReceived.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(received, "Both channels did not receive messages.");
        Assert.AreEqual(1, countA);
        Assert.AreEqual(1, countB);

        await s_subscriber.UnsubscribeAsync(channel1);
        await s_subscriber.UnsubscribeAsync(channel2);
    }

    [TestMethod]
    public async Task UnsubscribeStopsDelivery()
    {
        ReadOnlyMemory<byte> channel = "ws/test/unsubscribe"u8.ToArray();
        int receiveCount = 0;

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref receiveCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        // Publish first — should be received
        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"before"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(500);
        Assert.AreEqual(1, receiveCount);

        // Unsubscribe
        await s_subscriber.UnsubscribeAsync(channel);
        await Task.Delay(200);

        // Publish again — should NOT be received
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"after"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(500);
        Assert.AreEqual(1, receiveCount, "Messages should not be delivered after unsubscribe.");
    }

    [TestMethod]
    public async Task LargePayloadTransmitsCorrectly()
    {
        ReadOnlyMemory<byte> channel = "ws/test/large"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        int receivedLength = 0;

        await s_subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                // The payload is an object with a "data" field containing a large string
                receivedLength = payload.GetProperty("data"u8).GetString()!.Length;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        // Build a large payload (~2KB string)
        string largeStr = new('x', 2000);
        byte[] json = Encoding.UTF8.GetBytes($$"""{"data":"{{largeStr}}"}""");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        await s_publisher.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Large payload was not received.");
        Assert.AreEqual(2000, receivedLength);

        await s_subscriber.UnsubscribeAsync(channel);
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

    [TestMethod]
    public async Task DeserializationErrorInvokesErrorPolicy()
    {
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "ws/test/deser-error"u8.ToArray();

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(200);

        using ClientWebSocket rawWs = new();
        await rawWs.ConnectAsync(new Uri(WebSocketFixture.ServerUri), CancellationToken.None);

        byte[] envelopeBytes = Encoding.UTF8.GetBytes("""{"channel":"ws/test/deser-error","payload":{"ok":true},"correlationId":123}""");
        await rawWs.SendAsync(envelopeBytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(500);
        await rawWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy was not invoked for deserialization error.");
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await subscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithDeadLetterAction()
    {
        // Arrange — policy returns DeadLetter for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.DeadLetter);
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
            DeadLetterSuffix = "/deser-dlq",
        });

        using var dlqReceived = new SemaphoreSlim(0, 1);

        // Use a SEPARATE transport for DLQ subscription (relay won't forward back to sender)
        WebSocketMessageTransport dlqSubscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        await dlqSubscriber.SubscribeAsync<JsonElement>(
            "ws/test/deser-deadletter/deser-dlq"u8.ToArray(),
            (payload, headers, ct) =>
            {
                dlqReceived.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        // Subscribe to the source channel
        await transport.SubscribeAsync<JsonElement>(
            "ws/test/deser-deadletter"u8.ToArray(),
            (payload, headers, ct) => ValueTask.CompletedTask);

        await Task.Delay(300);

        // Act — send malformed envelope via raw WebSocket (correlationId is wrong type → parse failure)
        using ClientWebSocket rawWs = new();
        await rawWs.ConnectAsync(new Uri(WebSocketFixture.ServerUri), CancellationToken.None);

        // Send an envelope with invalid correlationId type (number instead of string → deserialization error)
        byte[] badEnvelope = Encoding.UTF8.GetBytes("""{"channel":"ws/test/deser-deadletter","payload":{"ok":true},"correlationId":999}""");
        await rawWs.SendAsync(badEnvelope, WebSocketMessageType.Text, true, CancellationToken.None);

        // Assert — dead-lettered message should arrive on DLQ
        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Deserialization error was not dead-lettered.");
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await rawWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await dlqSubscriber.DisposeAsync();
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithAbortAction()
    {
        // Arrange — policy returns Abort for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Abort);
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "ws/test/deser-abort"u8.ToArray();
        int handlerCallCount = 0;

        await transport.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                return ValueTask.CompletedTask;
            });

        await Task.Delay(300);

        // Act — send malformed envelope (correlationId as number → causes deserialization error)
        using ClientWebSocket rawWs = new();
        await rawWs.ConnectAsync(new Uri(WebSocketFixture.ServerUri), CancellationToken.None);

        byte[] badEnvelope = Encoding.UTF8.GetBytes("""{"channel":"ws/test/deser-abort","payload":{"ok":true},"correlationId":999}""");
        await rawWs.SendAsync(badEnvelope, WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(1000);

        // Publish a valid message via s_publisher — should NOT be received (subscription aborted)
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"msg":"after-abort"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        // Assert
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);
        Assert.AreEqual(0, handlerCallCount, "Handler should never be called after deserialization abort.");

        await rawWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task DeadLetterActionSendsToDeadLetterChannel()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.DeadLetter);
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
            DeadLetterSuffix = "/dlq",
        });

        ReadOnlyMemory<byte> channel = "ws/test/deadletter"u8.ToArray();
        ReadOnlyMemory<byte> dlqChannel = "ws/test/deadletter/dlq"u8.ToArray();
        using var dlqReceived = new SemaphoreSlim(0, 1);
        JsonValueKind dlqKind = JsonValueKind.Undefined;

        WebSocketMessageTransport dlqSubscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        await dlqSubscriber.SubscribeAsync<JsonElement>(
            dlqChannel,
            (payload, headers, ct) =>
            {
                dlqKind = payload.ValueKind;
                dlqReceived.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Intentional failure"));

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"item":"DL-001"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);

        bool received = await dlqReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Dead-letter message was not received on DLQ channel.");
        Assert.AreNotEqual(JsonValueKind.Undefined, dlqKind);

        await subscriber.DisposeAsync();
        await dlqSubscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task AbortActionStopsSubscription()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Abort);
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "ws/test/abort"u8.ToArray();
        int handlerCallCount = 0;

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                throw new InvalidOperationException("Trigger abort");
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":1}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc1.RootElement);
        await Task.Delay(500);

        int countAfterAbort = handlerCallCount;
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"msg":2}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc2.RootElement);
        await Task.Delay(500);

        Assert.AreEqual(1, policy.Invocations.Count, "Error policy should be invoked exactly once.");
        Assert.AreEqual(countAfterAbort, handlerCallCount, "Handler should not be called after abort.");

        await subscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareWrapsHandlerExecution()
    {
        int middlewareCallCount = 0;
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            HandlerMiddleware = async (operation, ct) =>
            {
                Interlocked.Increment(ref middlewareCallCount);
                await operation(ct).ConfigureAwait(false);
            },
        });

        ReadOnlyMemory<byte> channel = "ws/test/middleware"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"mw-test"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);

        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Message was not received through middleware.");
        Assert.AreEqual(1, middlewareCallCount, "Middleware should be called exactly once.");

        await subscriber.UnsubscribeAsync(channel);
        await subscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task MiddlewareExhaustionFallsThroughToErrorPolicy()
    {
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Skip);
        int retryCount = 0;
        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
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

        ReadOnlyMemory<byte> channel = "ws/test/mw-exhaust"u8.ToArray();

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) => throw new InvalidOperationException("Always fails"));

        await Task.Delay(200);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"data":"exhaust"}"""u8.ToArray());
        await s_publisher.PublishAsync(channel, doc.RootElement);
        await Task.Delay(1000);

        Assert.AreEqual(2, retryCount, "Middleware should have retried 2 times.");
        Assert.IsTrue(policy.Invocations.Count > 0, "Error policy should be invoked after middleware exhaustion.");
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await subscriber.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyRoundtripWithResponder()
    {
        // Arrange — requester transport subscribes to the reply channel so the relay forwards replies to it
        WebSocketMessageTransport requesterTransport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        // Subscribe the requester to the reply channel (dummy handler — correlationId match takes priority)
        ReadOnlyMemory<byte> replyChannel = "ws/test/reqreply/reply"u8.ToArray();
        await requesterTransport.SubscribeAsync<JsonElement>(
            replyChannel,
            (_, _, _) => ValueTask.CompletedTask);

        // Set up a raw WebSocket client as responder
        using ClientWebSocket responderWs = new();
        await responderWs.ConnectAsync(new Uri(WebSocketFixture.ServerUri), CancellationToken.None);

        // Subscribe the responder to the request channel via the relay
        byte[] subscribeEnvelope = Encoding.UTF8.GetBytes("""{"channel":"ws/test/reqreply/request","type":"subscribe"}""");
        await responderWs.SendAsync(
            new ArraySegment<byte>(subscribeEnvelope),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        // Start a receive loop on the responder that echoes replies
        using CancellationTokenSource responderCts = new();
        _ = Task.Run(async () =>
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!responderCts.Token.IsCancellationRequested)
                {
                    using MemoryStream ms = new();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await responderWs.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            responderCts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    // Parse the envelope to extract correlationId
                    byte[] msgBytes = ms.ToArray();
                    using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(msgBytes);
                    System.Text.Json.JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("correlationId", out System.Text.Json.JsonElement corrProp))
                    {
                        string? corrId = corrProp.GetString();
                        if (corrId is not null)
                        {
                            // Build a reply envelope with same correlationId on the reply channel
                            string replyEnvelope = @"{""channel"":""ws/test/reqreply/reply"",""correlationId"":""" + corrId + @""",""payload"":{""result"":""ws-reply"",""code"":55}}";
                            byte[] replyBytes = Encoding.UTF8.GetBytes(replyEnvelope);
                            await responderWs.SendAsync(
                                new ArraySegment<byte>(replyBytes),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                CancellationToken.None);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.Delay(500);

        // Act — send a request through the transport
        ReadOnlyMemory<byte> requestChannel = "ws/test/reqreply/request"u8.ToArray();
        byte[] correlationId = "ws-roundtrip-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"action":"query"}"""u8.ToArray());

        (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(55, replyPayload.GetProperty("code"u8).GetInt32());

        await responderCts.CancelAsync();
        await requesterTransport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyResponderRoundTrip()
    {
        // Arrange — a responder transport subscribes to the request channel via SubscribeReplyAsync,
        // and a requester transport subscribes to the reply channel so the relay forwards replies to it.
        WebSocketMessageTransport responderTransport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        WebSocketMessageTransport requesterTransport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        ReadOnlyMemory<byte> requestChannel = "ws/test/reqreply-responder/request"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "ws/test/reqreply-responder/reply"u8.ToArray();

        // The requester must be subscribed to the reply channel so the relay forwards the reply to it
        // (correlationId match takes priority over the dummy handler).
        await requesterTransport.SubscribeAsync<JsonElement>(
            replyChannel,
            (_, _, _) => ValueTask.CompletedTask);

        // Register the responder: it reads the request's "value", computes value + 1, and replies.
        await responderTransport.SubscribeReplyAsync<JsonElement, JsonElement>(
            requestChannel,
            (request, headers, ct) =>
            {
                int input = request.GetProperty("value"u8).GetInt32();
                byte[] replyJson = Encoding.UTF8.GetBytes($$"""{"result":{{input + 1}}}""");
                ParsedJsonDocument<JsonElement> replyDoc = ParsedJsonDocument<JsonElement>.Parse(replyJson);
                return ValueTask.FromResult(replyDoc.RootElement);
            });

        await Task.Delay(500);

        // Act — send a request through the requester transport.
        byte[] correlationId = "ws-responder-001"u8.ToArray();
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"value":41}"""u8.ToArray());

        (JsonElement replyPayload, JsonElement replyHeaders) = await requesterTransport.RequestAsync<JsonElement, JsonElement>(
            requestChannel,
            replyChannel,
            requestDoc.RootElement,
            correlationId);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(42, replyPayload.GetProperty("result"u8).GetInt32());

        await responderTransport.DisposeAsync();
        await requesterTransport.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestReplyTimeoutThrows()
    {
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        ReadOnlyMemory<byte> requestChannel = "ws/test/req-timeout"u8.ToArray();
        ReadOnlyMemory<byte> replyChannel = "ws/test/reply-timeout"u8.ToArray();
        byte[] correlationId = "timeout-corr-ws01"u8.ToArray();

        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"q":"hello"}"""u8.ToArray());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await transport.RequestAsync<JsonElement, JsonElement>(
                requestChannel,
                replyChannel,
                requestDoc.RootElement,
                correlationId,
                default,
                cts.Token));

        await transport.DisposeAsync();
    }

    [TestMethod]
    public async Task OperationsAfterDisposeThrowObjectDisposedException()
    {
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        await transport.DisposeAsync();

        ReadOnlyMemory<byte> channel = "ws/test/disposed"u8.ToArray();
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
    public async Task DoubleDisposeDoesNotThrow()
    {
        WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        await transport.DisposeAsync();
        await transport.DisposeAsync(); // Should be safe — no exception
    }

    [TestMethod]
    public async Task HandlerErrorWithSkipContinuesDelivery()
    {
        // Arrange — policy returns Skip for handler errors
        ConfigurableErrorPolicy policy = new(handlerAction: MessageErrorAction.Skip);

        WebSocketMessageTransport publisher = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "ws/test/skip-continues"u8.ToArray();
        int handlerSuccessCount = 0;
        using var secondReceived = new SemaphoreSlim(0, 1);

        await subscriber.SubscribeAsync<JsonElement>(
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
        await publisher.PublishAsync(channel, badDoc.RootElement);
        await Task.Delay(500);

        // Now publish a valid message — subscription should still be alive
        using ParsedJsonDocument<JsonElement> goodDoc = ParsedJsonDocument<JsonElement>.Parse("""{"fail":false}"""u8.ToArray());
        await publisher.PublishAsync(channel, goodDoc.RootElement);

        // Assert
        bool received = await secondReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(received, "Second message was not received — subscription stopped after skip.");
        Assert.AreEqual(1, handlerSuccessCount);
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Handler, policy.Invocations[0].Kind);

        await subscriber.UnsubscribeAsync(channel);
        await subscriber.DisposeAsync();
        await publisher.DisposeAsync();
    }

    [TestMethod]
    public async Task DeserializationErrorWithSkipContinuesDelivery()
    {
        // Arrange — policy returns Skip for deserialization errors
        ConfigurableErrorPolicy policy = new(deserializationAction: MessageErrorAction.Skip);

        WebSocketMessageTransport publisher = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
        });

        WebSocketMessageTransport subscriber = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = WebSocketFixture.ServerUri,
            ErrorPolicy = policy,
        });

        ReadOnlyMemory<byte> channel = "ws/test/deser-skip-continues"u8.ToArray();
        using var received = new SemaphoreSlim(0, 1);
        JsonValueKind receivedKind = JsonValueKind.Undefined;

        await subscriber.SubscribeAsync<JsonElement>(
            channel,
            (payload, headers, ct) =>
            {
                receivedKind = payload.ValueKind;
                received.Release();
                return ValueTask.CompletedTask;
            });

        await Task.Delay(500);

        // Act — publish a valid envelope that triggers deserialization error in the subscriber.
        // The WebSocket transport's DispatchEnvelopeAsync calls GetString() on correlationId;
        // sending a numeric correlationId causes it to throw (same pattern as existing deser error tests).
        using ClientWebSocket rawWs = new();
        await rawWs.ConnectAsync(new Uri(WebSocketFixture.ServerUri), CancellationToken.None);
        byte[] badEnvelope = """{"channel":"ws/test/deser-skip-continues","correlationId":999,"payload":{"data":1}}"""u8.ToArray();
        await rawWs.SendAsync(new ArraySegment<byte>(badEnvelope), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
        await Task.Delay(500);

        // Now publish a valid message — subscription should still be alive
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"after":"skip"}"""u8.ToArray());
        await publisher.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wasReceived, "Valid message was not received after deser error + skip.");
        Assert.AreEqual(JsonValueKind.Object, receivedKind);
        Assert.AreEqual(1, policy.Invocations.Count);
        Assert.AreEqual(MessageErrorKind.Deserialization, policy.Invocations[0].Kind);

        await subscriber.UnsubscribeAsync(channel);
        await subscriber.DisposeAsync();
        await publisher.DisposeAsync();
        rawWs.Dispose();
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