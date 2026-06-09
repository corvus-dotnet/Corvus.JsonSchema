// <copyright file="InMemoryMessageTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for the <see cref="Testing.InMemoryMessageTransport"/> from the Testing package.
/// </summary>
[TestClass]
public class InMemoryMessageTransportTests
{
    [TestMethod]
    public async Task PublishAsync_CapturesPayloadAndHeaders()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement payload = JsonElement.ParseValue("""{"temp":22.5}"""u8);
        JsonElement headers = JsonElement.ParseValue("""{"source":"sensor-1"}"""u8);

        await transport.PublishAsync("sensors/temperature"u8.ToArray(), in payload, in headers);

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        PublishedMessage msg = transport.PublishedMessages[0];
        Assert.AreEqual("sensors/temperature", msg.Channel);
        Assert.IsTrue(msg.PayloadBytes.Length > 0);
        Assert.IsTrue(msg.HeaderBytes.Length > 0);
    }

    [TestMethod]
    public async Task PublishAsync_WithoutHeaders_CapturesEmptyHeaders()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement payload = JsonElement.ParseValue("""{"value":42}"""u8);
        await transport.PublishAsync("events/data"u8.ToArray(), in payload);

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        PublishedMessage msg = transport.PublishedMessages[0];
        Assert.AreEqual("events/data", msg.Channel);
        Assert.AreEqual(0, msg.HeaderBytes.Length);
    }

    [TestMethod]
    public async Task SubscribeAsync_ReceivesDeliveredMessages()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonValueKind? receivedKind = null;
        await transport.SubscribeAsync<JsonElement>(
            "test/channel"u8.ToArray(),
            (payload, headers, ct) =>
            {
                receivedKind = payload.ValueKind;
                return ValueTask.CompletedTask;
            });

        ReadOnlyMemory<byte> payloadJson = Encoding.UTF8.GetBytes("""{"hello":"world"}""");
        await transport.DeliverAsync<JsonElement>("test/channel", payloadJson);

        Assert.IsNotNull(receivedKind);
        Assert.AreEqual(JsonValueKind.Object, receivedKind.Value);
    }

    [TestMethod]
    public async Task UnsubscribeAsync_StopsReceivingMessages()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        int receivedCount = 0;
        await transport.SubscribeAsync<JsonElement>(
            "test/channel"u8.ToArray(),
            (payload, headers, ct) =>
            {
                receivedCount++;
                return ValueTask.CompletedTask;
            });

        ReadOnlyMemory<byte> payloadJson = Encoding.UTF8.GetBytes("""{"seq":1}""");
        await transport.DeliverAsync<JsonElement>("test/channel", payloadJson);
        Assert.AreEqual(1, receivedCount);

        await transport.UnsubscribeAsync("test/channel"u8.ToArray());

        // After unsubscribe, delivery should throw
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => { transport.DeliverAsync<JsonElement>("test/channel", payloadJson).AsTask().GetAwaiter().GetResult(); });
        StringAssert.Contains(ex.Message, "test/channel");
    }

    [TestMethod]
    public async Task RequestAsync_CompletesWhenReplyDelivered()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement request = JsonElement.ParseValue("""{"question":"ping"}"""u8);

        // Start the request in background
        Task<(JsonElement Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<JsonElement, JsonElement>(
                "request/channel"u8.ToArray(),
                "reply/channel"u8.ToArray(),
                request,
                "corr-123"u8.ToArray()).AsTask();

        // Deliver a reply as byte[]
        byte[] replyBytes = Encoding.UTF8.GetBytes("""{"answer":"pong"}""");
        transport.CompleteRequest("corr-123", replyBytes);

        (JsonElement replyPayload, JsonElement _) = await requestTask;
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
    }

    [TestMethod]
    public async Task SubscribeReplyAsync_RespondsToRequestInProcess()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        // A responder echoes the request's value back, doubled, as the reply.
        await transport.SubscribeReplyAsync<JsonElement, JsonElement>(
            "rpc/double"u8.ToArray(),
            (request, _, _) =>
            {
                int n = request.GetProperty("n"u8).GetInt32();
                JsonElement reply = JsonElement.ParseValue(Encoding.UTF8.GetBytes($$"""{"result":{{n * 2}}}"""));
                return ValueTask.FromResult(reply);
            });

        JsonElement request = JsonElement.ParseValue("""{"n":21}"""u8);
        (JsonElement reply, JsonElement _) = await transport.RequestAsync<JsonElement, JsonElement>(
            "rpc/double"u8.ToArray(),
            "rpc/double/replies"u8.ToArray(),
            request,
            "corr-rr"u8.ToArray());

        Assert.AreEqual(42, reply.GetProperty("result"u8).GetInt32());

        // The request was still recorded as a published message.
        Assert.AreEqual(1, transport.PublishedMessages.Count);
        Assert.AreEqual("rpc/double", transport.PublishedMessages[0].Channel);
    }

    [TestMethod]
    public async Task ReceiveOneAndReplyAsync_RepliesToOneRequestThenUnsubscribes()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        // The one-shot responder wrapper handles exactly one request, replies, and unsubscribes.
        ValueTask responder = transport.ReceiveOneAndReplyAsync<JsonElement, JsonElement>(
            "rpc/once"u8.ToArray(),
            (request, _) =>
            {
                int n = request.GetProperty("n"u8).GetInt32();
                JsonElement reply = JsonElement.ParseValue(Encoding.UTF8.GetBytes($$"""{"result":{{n * 2}}}"""));
                return new ValueTask<JsonElement>(reply);
            });

        JsonElement request = JsonElement.ParseValue("""{"n":21}"""u8);
        (JsonElement reply, JsonElement _) = await transport.RequestAsync<JsonElement, JsonElement>(
            "rpc/once"u8.ToArray(),
            "rpc/once/replies"u8.ToArray(),
            request,
            "corr-once"u8.ToArray());

        await responder;
        Assert.AreEqual(42, reply.GetProperty("result"u8).GetInt32());

        // After the one-shot responder unsubscribed, a further request parks for CompleteRequest (it is no
        // longer routed to the now-removed responder).
        JsonElement second = JsonElement.ParseValue("""{"n":5}"""u8);
        Task<(JsonElement Payload, JsonElement Headers)> parked =
            transport.RequestAsync<JsonElement, JsonElement>(
                "rpc/once"u8.ToArray(),
                "rpc/once/replies"u8.ToArray(),
                second,
                "corr-once-2"u8.ToArray()).AsTask();
        Assert.IsFalse(parked.IsCompleted);
        transport.CompleteRequest("corr-once-2", Encoding.UTF8.GetBytes("""{"result":99}"""));
        (JsonElement parkedReply, JsonElement _) = await parked;
        Assert.AreEqual(99, parkedReply.GetProperty("result"u8).GetInt32());
    }

    [TestMethod]
    public async Task ReceiveOneAndReplyAsync_RethrowsHandlerFailure()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        ValueTask responder = transport.ReceiveOneAndReplyAsync<JsonElement, JsonElement>(
            "rpc/boom"u8.ToArray(),
            (_, _) => throw new InvalidOperationException("handler failed"));

        // Drive a request at the responder; the failing handler produces no usable reply (its failure is
        // captured), so the transport's own reply handling may surface an error too — that is not what this
        // test asserts, so it is tolerated.
        JsonElement request = JsonElement.ParseValue("""{"n":1}"""u8);
        try
        {
            _ = await transport.RequestAsync<JsonElement, JsonElement>(
                "rpc/boom"u8.ToArray(),
                "rpc/boom/replies"u8.ToArray(),
                request,
                "corr-boom"u8.ToArray());
        }
        catch (InvalidOperationException)
        {
            // The default reply produced after a handler failure is not serializable; ignore.
        }

        // The captured handler failure is re-thrown to the awaiting responder.
        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await responder);
        Assert.AreEqual("handler failed", ex.Message);
    }

    [TestMethod]
    public async Task RequestAsync_WithoutResponder_StillParksForCompleteRequest()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement request = JsonElement.ParseValue("""{"n":1}"""u8);
        Task<(JsonElement Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<JsonElement, JsonElement>(
                "rpc/none"u8.ToArray(),
                "rpc/none/replies"u8.ToArray(),
                request,
                "corr-none"u8.ToArray()).AsTask();

        transport.CompleteRequest("corr-none", Encoding.UTF8.GetBytes("""{"result":7}"""));

        (JsonElement reply, JsonElement _) = await requestTask;
        Assert.AreEqual(7, reply.GetProperty("result"u8).GetInt32());
    }

    [TestMethod]
    public async Task Reset_ClearsAllState()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement payload = JsonElement.ParseValue("""{"x":1}"""u8);
        await transport.PublishAsync("ch"u8.ToArray(), in payload);
        await transport.SubscribeAsync<JsonElement>("ch"u8.ToArray(), (_, _, _) => ValueTask.CompletedTask);

        Assert.AreEqual(1, transport.PublishedMessages.Count);

        transport.Reset();

        Assert.AreEqual(0, transport.PublishedMessages.Count);
        Assert.AreEqual(0, transport.DeadLetteredMessages.Count);
    }

    [TestMethod]
    public async Task MultipleChannels_RoutesIndependently()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        List<string> channelsReceived = [];

        await transport.SubscribeAsync<JsonElement>(
            "channel/a"u8.ToArray(),
            (_, _, _) => { channelsReceived.Add("a"); return ValueTask.CompletedTask; });

        await transport.SubscribeAsync<JsonElement>(
            "channel/b"u8.ToArray(),
            (_, _, _) => { channelsReceived.Add("b"); return ValueTask.CompletedTask; });

        ReadOnlyMemory<byte> msg = Encoding.UTF8.GetBytes("""{}""");
        await transport.DeliverAsync<JsonElement>("channel/b", msg);
        await transport.DeliverAsync<JsonElement>("channel/a", msg);
        await transport.DeliverAsync<JsonElement>("channel/b", msg);

        Assert.AreEqual(3, channelsReceived.Count);
        Assert.AreEqual("b", channelsReceived[0]);
        Assert.AreEqual("a", channelsReceived[1]);
        Assert.AreEqual("b", channelsReceived[2]);
    }

    [TestMethod]
    public async Task DeliverAsync_WithHeaders_PassesHeadersToHandler()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonValueKind? receivedHeadersKind = null;
        await transport.SubscribeAsync<JsonElement>(
            "test/channel"u8.ToArray(),
            (payload, headers, ct) =>
            {
                receivedHeadersKind = headers.ValueKind;
                return ValueTask.CompletedTask;
            });

        ReadOnlyMemory<byte> payloadJson = Encoding.UTF8.GetBytes("""{"value":1}""");
        ReadOnlyMemory<byte> headersJson = Encoding.UTF8.GetBytes("""{"traceId":"abc"}""");
        await transport.DeliverAsync<JsonElement>("test/channel", payloadJson, headersJson);

        Assert.IsNotNull(receivedHeadersKind);
        Assert.AreEqual(JsonValueKind.Object, receivedHeadersKind.Value);
    }

    [TestMethod]
    public async Task CompleteRequest_WithHeaders_ReturnsHeaders()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement request = JsonElement.ParseValue("""{"req":true}"""u8);

        Task<(JsonElement Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<JsonElement, JsonElement>(
                "req/ch"u8.ToArray(),
                "rep/ch"u8.ToArray(),
                request,
                "corr-headers"u8.ToArray()).AsTask();

        byte[] replyBytes = Encoding.UTF8.GetBytes("""{"ok":true}""");
        byte[] headerBytes = Encoding.UTF8.GetBytes("""{"status":"200"}""");
        transport.CompleteRequest("corr-headers", replyBytes, headerBytes);

        (JsonElement replyPayload, JsonElement replyHeaders) = await requestTask;
        Assert.AreEqual(JsonValueKind.Object, replyPayload.ValueKind);
        Assert.AreEqual(JsonValueKind.Object, replyHeaders.ValueKind);
    }

    [TestMethod]
    public async Task CompleteRequest_UnknownCorrelationId_Throws()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => transport.CompleteRequest("unknown-id", Encoding.UTF8.GetBytes("""{}""")));
    }

    [TestMethod]
    public async Task DeliverAsync_NoSubscription_Throws()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        ReadOnlyMemory<byte> payloadJson = Encoding.UTF8.GetBytes("""{}""");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => { transport.DeliverAsync<JsonElement>("no/subscription", payloadJson).AsTask().GetAwaiter().GetResult(); });
        StringAssert.Contains(ex.Message, "no/subscription");
    }
}