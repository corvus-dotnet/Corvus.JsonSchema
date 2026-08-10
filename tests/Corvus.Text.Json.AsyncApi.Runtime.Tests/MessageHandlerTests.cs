// <copyright file="MessageHandlerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.Internal;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for the <see cref="MessageHandler{TPayload}"/> SPI type.
/// </summary>
[TestClass]
public class MessageHandlerTests
{
    [TestMethod]
    public async Task Invoke_DefaultInstance_ThrowsInvalidOperation()
    {
        MessageHandler<JsonElement> handler = default;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await handler.Invoke(default, ReadOnlyMemory<byte>.Empty, default, null, CancellationToken.None));
    }

    [TestMethod]
    public void WithoutDeliveryContext_NullHandler_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => MessageHandler<JsonElement>.WithoutDeliveryContext(null!));
    }

    [TestMethod]
    public void WithDeliveryContext_NullHandler_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => MessageHandler<JsonElement>.WithDeliveryContext(null!));
    }

    [TestMethod]
    public async Task Invoke_LegacyHandler_PassesHeaders()
    {
        JsonValueKind seen = JsonValueKind.Undefined;
        MessageHandler<JsonElement> handler = MessageHandler<JsonElement>.WithoutDeliveryContext(
            (payload, headers, ct) =>
            {
                seen = headers.ValueKind;
                return ValueTask.CompletedTask;
            });

        JsonElement headers = JsonElement.ParseValue("""{"k":1}"""u8);
        await handler.Invoke(default, ReadOnlyMemory<byte>.Empty, headers, null, CancellationToken.None);

        Assert.AreEqual(JsonValueKind.Object, seen);
    }
}