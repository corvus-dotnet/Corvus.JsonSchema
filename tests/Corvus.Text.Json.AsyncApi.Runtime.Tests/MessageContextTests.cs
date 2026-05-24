// <copyright file="MessageContextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for <see cref="MessageContext"/> and the default interface method overloads on
/// <see cref="IMessageTransport"/> that accept a <see cref="MessageContext"/>.
/// </summary>
[TestClass]
public class MessageContextTests
{
    [TestMethod]
    public void HasBindings_ReturnsFalse_WhenEmpty()
    {
        MessageContext ctx = default;
        Assert.IsFalse(ctx.HasBindings);
    }

    [TestMethod]
    public void HasBindings_ReturnsTrue_WhenChannelBindingsPresent()
    {
        MessageContext ctx = new()
        {
            ChannelBindingsJson = Encoding.UTF8.GetBytes("""{"kafka":{}}"""),
        };

        Assert.IsTrue(ctx.HasBindings);
    }

    [TestMethod]
    public void HasBindings_ReturnsTrue_WhenOperationBindingsPresent()
    {
        MessageContext ctx = new()
        {
            OperationBindingsJson = Encoding.UTF8.GetBytes("""{"kafka":{}}"""),
        };

        Assert.IsTrue(ctx.HasBindings);
    }

    [TestMethod]
    public void HasBindings_ReturnsTrue_WhenMessageBindingsPresent()
    {
        MessageContext ctx = new()
        {
            MessageBindingsJson = Encoding.UTF8.GetBytes("""{"kafka":{}}"""),
        };

        Assert.IsTrue(ctx.HasBindings);
    }

    [TestMethod]
    public async Task PublishAsync_WithContext_DelegatesToSimpleOverload()
    {
        await using InMemoryMessageTransport transport = new();

        JsonElement payload = JsonElement.ParseValue("""{"data":1}"""u8);
        MessageContext context = new()
        {
            ContentType = "application/json",
            ChannelBindingsJson = Encoding.UTF8.GetBytes("""{"kafka":{"topic":"test"}}"""),
        };

        // Use the default interface method overload that accepts MessageContext
        await ((IMessageTransport)transport).PublishAsync("ch"u8.ToArray(), in payload, in context);

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        Assert.AreEqual("ch", transport.PublishedMessages[0].Channel);
    }

    [TestMethod]
    public async Task SubscribeAsync_WithContext_DelegatesToSimpleOverload()
    {
        await using InMemoryMessageTransport transport = new();

        bool handlerCalled = false;
        MessageContext context = new()
        {
            ContentType = "application/json",
        };

        // Use the default interface method overload that accepts MessageContext
        await ((IMessageTransport)transport).SubscribeAsync<JsonElement>(
            "ch"u8.ToArray(),
            (_, _, _) => { handlerCalled = true; return ValueTask.CompletedTask; },
            in context);

        ReadOnlyMemory<byte> msg = Encoding.UTF8.GetBytes("""{"v":42}""");
        await transport.DeliverAsync<JsonElement>("ch", msg);

        Assert.IsTrue(handlerCalled);
    }

    [TestMethod]
    public async Task RequestAsync_WithContext_DelegatesToSimpleOverload()
    {
        await using InMemoryMessageTransport transport = new();

        JsonElement request = JsonElement.ParseValue("""{"q":1}"""u8);
        MessageContext context = new()
        {
            OperationBindingsJson = Encoding.UTF8.GetBytes("""{"kafka":{}}"""),
        };

        // Use the default interface method overload that accepts MessageContext
        Task<(JsonElement Payload, JsonElement Headers)> requestTask =
            ((IMessageTransport)transport).RequestAsync<JsonElement, JsonElement>(
                "req"u8.ToArray(), "rep"u8.ToArray(), in request, "c-1"u8.ToArray(), in context).AsTask();

        byte[] replyBytes = Encoding.UTF8.GetBytes("""{"a":2}""");
        transport.CompleteRequest("c-1", replyBytes);

        (JsonElement reply, _) = await requestTask;
        Assert.AreEqual(JsonValueKind.Object, reply.ValueKind);
    }
}