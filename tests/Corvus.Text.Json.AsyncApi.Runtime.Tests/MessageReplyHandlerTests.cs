// <copyright file="MessageReplyHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.Internal;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for the <see cref="MessageReplyHandler{TRequest, TReply}"/> SPI type.
/// </summary>
[TestClass]
public class MessageReplyHandlerTests
{
    [TestMethod]
    public async Task InvokeReply_DefaultInstance_ThrowsInvalidOperation()
    {
        MessageReplyHandler<JsonElement, JsonElement> handler = default;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await handler.InvokeReply(default, ReadOnlyMemory<byte>.Empty, default, null, CancellationToken.None));
    }

    [TestMethod]
    public void WithoutDeliveryContext_NullHandler_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => MessageReplyHandler<JsonElement, JsonElement>.WithoutDeliveryContext(null!));
    }

    [TestMethod]
    public void WithDeliveryContext_NullHandler_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => MessageReplyHandler<JsonElement, JsonElement>.WithDeliveryContext(null!));
    }

    [TestMethod]
    public void UsesDeliveryContext_ReflectsStoredCallbackShape()
    {
        MessageReplyHandler<JsonElement, JsonElement> legacy = MessageReplyHandler<JsonElement, JsonElement>.WithoutDeliveryContext(
            (request, headers, ct) => ValueTask.FromResult(request));
        MessageReplyHandler<JsonElement, JsonElement> context = MessageReplyHandler<JsonElement, JsonElement>.WithDeliveryContext(
            (request, deliveryContext, ct) => ValueTask.FromResult(request));

        Assert.IsFalse(legacy.UsesDeliveryContext);
        Assert.IsTrue(context.UsesDeliveryContext);
    }

    [TestMethod]
    public async Task InvokeReply_LegacyHandler_PassesHeadersAndReturnsReply()
    {
        JsonValueKind seen = JsonValueKind.Undefined;
        MessageReplyHandler<JsonElement, JsonElement> handler = MessageReplyHandler<JsonElement, JsonElement>.WithoutDeliveryContext(
            (request, headers, ct) =>
            {
                seen = headers.ValueKind;
                return ValueTask.FromResult(request);
            });

        JsonElement request = JsonElement.ParseValue("""{"n":21}"""u8);
        JsonElement headers = JsonElement.ParseValue("""{"k":1}"""u8);
        JsonElement reply = await handler.InvokeReply(request, ReadOnlyMemory<byte>.Empty, headers, null, CancellationToken.None);

        Assert.AreEqual(JsonValueKind.Object, seen);
        Assert.AreEqual(21, reply.GetProperty("n"u8).GetInt32());
    }

    [TestMethod]
    public async Task InvokeReply_ContextHandler_PopulatesContextAndReturnsReply()
    {
        string? seenChannel = null;
        JsonValueKind seenHeaders = JsonValueKind.Undefined;
        object? seenNativeMessage = null;
        MessageReplyHandler<JsonElement, JsonElement> handler = MessageReplyHandler<JsonElement, JsonElement>.WithDeliveryContext(
            (request, deliveryContext, ct) =>
            {
                seenChannel = Encoding.UTF8.GetString(deliveryContext.ChannelUtf8.Span);
                seenHeaders = deliveryContext.Headers.ValueKind;
                seenNativeMessage = deliveryContext.NativeMessage;
                return ValueTask.FromResult(request);
            });

        JsonElement request = JsonElement.ParseValue("""{"n":21}"""u8);
        JsonElement headers = JsonElement.ParseValue("""{"k":1}"""u8);
        object nativeMessage = new();
        JsonElement reply = await handler.InvokeReply(request, "calc/requests"u8.ToArray(), headers, nativeMessage, CancellationToken.None);

        Assert.AreEqual("calc/requests", seenChannel);
        Assert.AreEqual(JsonValueKind.Object, seenHeaders);
        Assert.AreSame(nativeMessage, seenNativeMessage);
        Assert.AreEqual(21, reply.GetProperty("n"u8).GetInt32());
    }
}