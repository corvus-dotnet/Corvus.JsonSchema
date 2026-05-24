// <copyright file="CancellationAndConcurrencyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for cancellation token propagation, message ordering, concurrency,
/// and the <see cref="ReadOnlyMemoryByteComparer"/>.
/// </summary>
[TestClass]
public class CancellationAndConcurrencyTests
{
    private const string LightMeasurementChannel =
        "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";

    [TestMethod]
    public async Task RequestAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        JsonElement request = JsonElement.ParseValue("""{"lumens":100,"sentAt":"2024-01-01T00:00:00Z"}"""u8);

        using CancellationTokenSource cts = new();

        // Start request but don't complete it
        Task<(JsonElement Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<JsonElement, JsonElement>(
                "request/channel"u8.ToArray(),
                "reply/channel"u8.ToArray(),
                in request,
                "corr-cancel-test"u8.ToArray(),
                cancellationToken: cts.Token).AsTask();

        // Cancel before reply arrives
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => requestTask);
    }

    [TestMethod]
    public async Task Consumer_MultipleMessages_ProcessedInOrder()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        List<int> receivedOrder = [];

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) =>
            {
                // Extract lumens value to track ordering
                int lumens = (int)payload.Lumens;
                receivedOrder.Add(lumens);
                return ValueTask.CompletedTask;
            });

        // Deliver messages in sequence
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":1,"sentAt":"2024-01-01T00:00:00Z"}"""u8.ToArray());
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":2,"sentAt":"2024-01-02T00:00:00Z"}"""u8.ToArray());
        await transport.DeliverAsync<LightMeasuredPayload>(
            LightMeasurementChannel,
            """{"lumens":3,"sentAt":"2024-01-03T00:00:00Z"}"""u8.ToArray());

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, receivedOrder);
    }

    [TestMethod]
    public async Task Producer_MultiplePublishes_AllCapturedIndependently()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        TurnOnProducer producer = new(transport, ValidationMode.None);

        await producer.PublishTurnOnOffAsync(
            TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8),
            "lamp-1");
        await producer.PublishTurnOnOffAsync(
            TurnOnOffPayload.ParseValue("""{"command":"off","sentAt":"2024-01-02T00:00:00Z"}"""u8),
            "lamp-2");
        await producer.PublishTurnOnOffAsync(
            TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-03T00:00:00Z"}"""u8),
            "lamp-3");

        Assert.AreEqual(3, transport.PublishedMessages.Count);
        Assert.AreEqual("smartylighting.streetlights.1.0.action.lamp-1.turn.on", transport.PublishedMessages[0].Channel);
        Assert.AreEqual("smartylighting.streetlights.1.0.action.lamp-2.turn.on", transport.PublishedMessages[1].Channel);
        Assert.AreEqual("smartylighting.streetlights.1.0.action.lamp-3.turn.on", transport.PublishedMessages[2].Channel);

        // Verify payloads are independent
        string payload1 = Encoding.UTF8.GetString(transport.PublishedMessages[0].PayloadBytes);
        string payload2 = Encoding.UTF8.GetString(transport.PublishedMessages[1].PayloadBytes);
        StringAssert.Contains(payload1, "\"on\"");
        StringAssert.Contains(payload2, "\"off\"");
    }

    [TestMethod]
    public async Task ConcurrentDelivery_AllMessagesProcessed()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        int messageCount = 0;

        await transport.SubscribeAsync<LightMeasuredPayload>(
            LightMeasurementChannel.AsUtf8(),
            (payload, headers, ct) =>
            {
                Interlocked.Increment(ref messageCount);
                return ValueTask.CompletedTask;
            });

        // Deliver multiple messages concurrently
        Task[] deliveryTasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            int lumens = i * 10;
            deliveryTasks[i] = transport.DeliverAsync<LightMeasuredPayload>(
                LightMeasurementChannel,
                Encoding.UTF8.GetBytes($$$"""{"lumens":{{{lumens}}},"sentAt":"2024-01-01T00:00:00Z"}""")).AsTask();
        }

        await Task.WhenAll(deliveryTasks);
        Assert.AreEqual(10, messageCount);
    }

    [TestMethod]
    public void ReadOnlyMemoryByteComparer_EmptyMemory_EqualToEmpty()
    {
        ReadOnlyMemory<byte> a = ReadOnlyMemory<byte>.Empty;
        ReadOnlyMemory<byte> b = ReadOnlyMemory<byte>.Empty;

        Assert.IsTrue(ReadOnlyMemoryByteComparer.Instance.Equals(a, b));
        Assert.AreEqual(
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(a),
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(b));
    }

    [TestMethod]
    public void ReadOnlyMemoryByteComparer_SameContent_DifferentBacking_AreEqual()
    {
        byte[] array1 = "hello-world"u8.ToArray();
        byte[] array2 = "hello-world"u8.ToArray();

        ReadOnlyMemory<byte> a = array1;
        ReadOnlyMemory<byte> b = array2;

        Assert.IsFalse(ReferenceEquals(array1, array2));
        Assert.IsTrue(ReadOnlyMemoryByteComparer.Instance.Equals(a, b));
        Assert.AreEqual(
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(a),
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(b));
    }

    [TestMethod]
    public void ReadOnlyMemoryByteComparer_DifferentContent_NotEqual()
    {
        ReadOnlyMemory<byte> a = "abc"u8.ToArray();
        ReadOnlyMemory<byte> b = "xyz"u8.ToArray();

        Assert.IsFalse(ReadOnlyMemoryByteComparer.Instance.Equals(a, b));
    }

    [TestMethod]
    public void ReadOnlyMemoryByteComparer_SliceOfSameArray_MatchesIndependentCopy()
    {
        byte[] source = "prefix-correlation-id-suffix"u8.ToArray();
        ReadOnlyMemory<byte> slice = source.AsMemory(7, 14); // "correlation-id"
        ReadOnlyMemory<byte> copy = "correlation-id"u8.ToArray();

        Assert.IsTrue(ReadOnlyMemoryByteComparer.Instance.Equals(slice, copy));
        Assert.AreEqual(
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(slice),
            ReadOnlyMemoryByteComparer.Instance.GetHashCode(copy));
    }
}

/// <summary>
/// Extension method to convert a string to UTF-8 bytes for test readability.
/// </summary>
internal static class StringExtensions
{
    public static ReadOnlyMemory<byte> AsUtf8(this string value) => Encoding.UTF8.GetBytes(value);
}