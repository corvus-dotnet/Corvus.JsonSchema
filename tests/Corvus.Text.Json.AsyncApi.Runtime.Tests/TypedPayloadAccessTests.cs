// <copyright file="TypedPayloadAccessTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Streetlights.Client;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests that verify typed property access on payloads published, delivered, and
/// returned via request/reply through the transport layer. These exercise the
/// generated numeric type conversions (explicit operators) to ensure fractional
/// values, integer values, and string-format values all round-trip correctly.
/// </summary>
[TestClass]
public class TypedPayloadAccessTests
{
    private const string Channel = "streetlights/1/0/event/lighting/measured";

    [TestMethod]
    public async Task Publish_TypedPayload_SerializesAllProperties()
    {
        await using InMemoryMessageTransport transport = new();

        LightMeasuredPayload payload = LightMeasuredPayload.ParseValue(
            """{"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}"""u8);

        await transport.PublishAsync(Channel.AsUtf8(), in payload);

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        string json = Encoding.UTF8.GetString(transport.PublishedMessages[0].PayloadBytes);
        Assert.AreEqual("""{"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""", json);
    }

    [TestMethod]
    public async Task Publish_WithHeaders_SerializesBothPayloadAndHeaders()
    {
        await using InMemoryMessageTransport transport = new();

        LightMeasuredPayload payload = LightMeasuredPayload.ParseValue(
            """{"lumens":100,"sentAt":"2026-01-01T00:00:00Z"}"""u8);
        JsonElement headers = JsonElement.ParseValue(
            """{"x-correlation-id":"abc-123","x-source":"sensor-42"}"""u8);

        await transport.PublishAsync(Channel.AsUtf8(), in payload, in headers);

        Assert.AreEqual(1, transport.PublishedMessages.Count);
        InMemoryMessageTransport.PublishedMessage msg = transport.PublishedMessages[0];

        string payloadJson = Encoding.UTF8.GetString(msg.PayloadBytes);
        string headersJson = Encoding.UTF8.GetString(msg.HeaderBytes);

        Assert.AreEqual("""{"lumens":100,"sentAt":"2026-01-01T00:00:00Z"}""", payloadJson);
        Assert.AreEqual("""{"x-correlation-id":"abc-123","x-source":"sensor-42"}""", headersJson);
    }

    [TestMethod]
    public async Task Subscribe_FractionalLumens_AccessibleAsDouble()
    {
        await using InMemoryMessageTransport transport = new();

        double? receivedLumens = null;
        await transport.SubscribeAsync<LightMeasuredPayload>(
            Channel.AsUtf8(),
            (payload, headers, ct) =>
            {
                receivedLumens = (double)payload.Lumens;
                return ValueTask.CompletedTask;
            });

        await transport.DeliverAsync<LightMeasuredPayload>(
            Channel,
            """{"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}"""u8.ToArray());

        Assert.IsNotNull(receivedLumens);
        Assert.AreEqual(512.7, receivedLumens.Value, 0.001);
    }

    [TestMethod]
    public async Task Subscribe_IntegerLumens_AccessibleAsBothLongAndDouble()
    {
        await using InMemoryMessageTransport transport = new();

        long? asLong = null;
        double? asDouble = null;
        await transport.SubscribeAsync<LightMeasuredPayload>(
            Channel.AsUtf8(),
            (payload, headers, ct) =>
            {
                asLong = (long)payload.Lumens;
                asDouble = (double)payload.Lumens;
                return ValueTask.CompletedTask;
            });

        await transport.DeliverAsync<LightMeasuredPayload>(
            Channel,
            """{"lumens":100,"sentAt":"2026-05-25T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual(100L, asLong);
        Assert.AreEqual(100.0, asDouble);
    }

    [TestMethod]
    public async Task Subscribe_FractionalLumens_CastToLongThrowsFormatException()
    {
        await using InMemoryMessageTransport transport = new();

        FormatException? caught = null;
        await transport.SubscribeAsync<LightMeasuredPayload>(
            Channel.AsUtf8(),
            (payload, headers, ct) =>
            {
                try
                {
                    _ = (long)payload.Lumens;
                }
                catch (FormatException ex)
                {
                    caught = ex;
                }

                return ValueTask.CompletedTask;
            });

        await transport.DeliverAsync<LightMeasuredPayload>(
            Channel,
            """{"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}"""u8.ToArray());

        Assert.IsNotNull(caught, "Casting fractional lumens to long should throw FormatException.");
    }

    [TestMethod]
    public async Task Subscribe_SentAt_AccessibleAsString()
    {
        await using InMemoryMessageTransport transport = new();

        string? receivedSentAt = null;
        await transport.SubscribeAsync<LightMeasuredPayload>(
            Channel.AsUtf8(),
            (payload, headers, ct) =>
            {
                receivedSentAt = (string)payload.SentAt;
                return ValueTask.CompletedTask;
            });

        await transport.DeliverAsync<LightMeasuredPayload>(
            Channel,
            """{"lumens":100,"sentAt":"2026-05-25T10:30:00Z"}"""u8.ToArray());

        Assert.AreEqual("2026-05-25T10:30:00Z", receivedSentAt);
    }

    [TestMethod]
    public async Task RequestReply_TypedAccess_OnReplyPayload()
    {
        await using InMemoryMessageTransport transport = new();

        LightMeasuredPayload request = LightMeasuredPayload.ParseValue(
            """{"lumens":42,"sentAt":"2026-01-01T00:00:00Z"}"""u8);

        Task<(LightMeasuredPayload Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<LightMeasuredPayload, LightMeasuredPayload>(
                "request/ch"u8.ToArray(),
                "reply/ch"u8.ToArray(),
                request,
                "corr-typed-access"u8.ToArray()).AsTask();

        transport.CompleteRequest(
            "corr-typed-access",
            """{"lumens":999.9,"sentAt":"2026-12-31T23:59:59Z"}"""u8.ToArray());

        (LightMeasuredPayload reply, _) = await requestTask;

        Assert.AreEqual(999.9, (double)reply.Lumens, 0.001);
        Assert.AreEqual("2026-12-31T23:59:59Z", (string)reply.SentAt);
    }

    [TestMethod]
    public async Task RequestReply_WithHeaders_ReturnsTypedReplyAndHeaders()
    {
        await using InMemoryMessageTransport transport = new();

        LightMeasuredPayload request = LightMeasuredPayload.ParseValue(
            """{"lumens":50,"sentAt":"2026-06-01T00:00:00Z"}"""u8);

        Task<(LightMeasuredPayload Payload, JsonElement Headers)> requestTask =
            transport.RequestAsync<LightMeasuredPayload, LightMeasuredPayload>(
                "req/ch"u8.ToArray(),
                "rep/ch"u8.ToArray(),
                request,
                "corr-with-headers"u8.ToArray()).AsTask();

        transport.CompleteRequest(
            "corr-with-headers",
            """{"lumens":75.5,"sentAt":"2026-06-01T12:00:00Z"}"""u8.ToArray(),
            """{"status":"ok"}"""u8.ToArray());

        (LightMeasuredPayload reply, JsonElement headers) = await requestTask;

        Assert.AreEqual(75.5, (double)reply.Lumens, 0.001);
        Assert.AreEqual(JsonValueKind.Object, headers.ValueKind);
    }
}