// <copyright file="AuthenticationEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

/// <summary>
/// Tests for authentication provider integration through generated producer
/// and consumer code.
/// </summary>
[TestClass]
public class AuthenticationEndToEndTests
{
    private const string LightMeasurementChannel =
        "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";

    [TestMethod]
    public async Task Producer_WithAuthProvider_AuthenticatesBeforePublish()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        RecordingAuthProvider authProvider = new();

        TurnOnProducer producer = new(transport, ValidationMode.None, authProvider);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8);
        await producer.PublishTurnOnOffAsync(payload, "lamp-1");

        // Auth was called before publish
        Assert.AreEqual(1, authProvider.AuthenticateCallCount);
        Assert.IsTrue(authProvider.AuthenticatedBeforePublish);

        // Message still published
        Assert.AreEqual(1, transport.PublishedMessages.Count);
    }

    [TestMethod]
    public async Task Consumer_WithAuthProvider_AuthenticatesBeforeSubscribe()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        RecordingAuthProvider authProvider = new();
        CountingHandler handler = new();

        await using ReceiveLightMeasurementConsumer consumer = new(
            transport, handler, ValidationMode.None, errorPolicy: null, authProvider: authProvider);

        await consumer.StartAsync();

        // Auth was called during StartAsync (before subscribe)
        Assert.AreEqual(1, authProvider.AuthenticateCallCount);
    }

    [TestMethod]
    public async Task Producer_AuthProviderThrows_PropagatesException()
    {
        await using Testing.InMemoryMessageTransport transport = new();
        ThrowingAuthProvider authProvider = new();

        TurnOnProducer producer = new(transport, ValidationMode.None, authProvider);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8);

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await producer.PublishTurnOnOffAsync(payload, "lamp-1"));

        StringAssert.Contains(ex.Message, "Auth failed");

        // Message was NOT published
        Assert.AreEqual(0, transport.PublishedMessages.Count);
    }

    [TestMethod]
    public async Task Producer_WithNullAuthProvider_SkipsAuthentication()
    {
        await using Testing.InMemoryMessageTransport transport = new();

        // Explicitly pass null auth provider
        TurnOnProducer producer = new(transport, ValidationMode.None, authProvider: null);

        TurnOnOffPayload payload = TurnOnOffPayload.ParseValue("""{"command":"on","sentAt":"2024-01-01T00:00:00Z"}"""u8);
        await producer.PublishTurnOnOffAsync(payload, "lamp-1");

        // Published successfully without auth
        Assert.AreEqual(1, transport.PublishedMessages.Count);
    }

    private sealed class RecordingAuthProvider : IMessageAuthenticationProvider
    {
        public int AuthenticateCallCount { get; private set; }

        public bool AuthenticatedBeforePublish { get; private set; }

        public ValueTask AuthenticateAsync(MessageAuthenticationContext context, CancellationToken cancellationToken = default)
        {
            this.AuthenticateCallCount++;
            this.AuthenticatedBeforePublish = true;
            context.Credentials["token"] = "test-token";
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingAuthProvider : IMessageAuthenticationProvider
    {
        public ValueTask AuthenticateAsync(MessageAuthenticationContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Auth failed");
        }
    }

    private sealed class CountingHandler : IReceiveLightMeasurementHandler
    {
        public int CallCount { get; private set; }

        public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
        {
            this.CallCount++;
            return ValueTask.CompletedTask;
        }
    }
}