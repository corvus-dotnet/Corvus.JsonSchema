// <copyright file="AzureServiceBusTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi.AzureServiceBus;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusMessageTransport"/>.
/// </summary>
/// <remarks>
/// These tests are currently disabled because Azure Service Bus does not have a local emulator
/// that can run in Docker/Testcontainers. To run these tests, you need:
/// 1. A real Azure Service Bus namespace (connection string or managed identity)
/// 2. Session-enabled reply queues (required for request-reply pattern)
/// 3. Update the connection string in the test methods
/// For CI, consider using Azure-hosted test infrastructure or skip these tests.
/// </remarks>
[TestClass]
public class AzureServiceBusTransportTests
{
    // TODO: Add Testcontainers support when Azure Service Bus emulator becomes available
    // See: https://github.com/Azure/azure-service-bus-emulator (not yet GA)

    /// <summary>
    /// Verifies that session-based request-reply uses the correlation ID as session ID.
    /// </summary>
    [TestMethod]
    [Ignore("Requires real Azure Service Bus namespace - no local emulator available")]
    public async Task SessionBasedRequestReply_UsesCorrelationIdAsSessionId()
    {
        // Arrange
        const string connectionString = "Endpoint=sb://your-namespace.servicebus.windows.net/;...";
        AzureServiceBusTransportOptions options = new()
        {
            ConnectionString = connectionString,
            QueueName = "test-queue",
        };

        await using AzureServiceBusMessageTransport transport = await AzureServiceBusMessageTransport.CreateAsync(options);

        // Act - Request with unique correlation ID
        using ParsedJsonDocument<JsonElement> requestDoc = ParsedJsonDocument<JsonElement>.Parse("""{"query":"status"}"""u8.ToArray());
        string correlationId = Guid.NewGuid().ToString();

        // This should set SessionId = correlationId on the request message
        // and use ServiceBusSessionReceiver.AcceptSessionAsync(correlationId) for the reply
        (JsonElement replyPayload, JsonElement replyHeaders) = await transport.RequestAsync<JsonElement, JsonElement>(
            "test-requests"u8.ToArray(),
            "test-replies"u8.ToArray(),
            requestDoc.RootElement,
            System.Text.Encoding.UTF8.GetBytes(correlationId),
            default);

        // Assert
        Assert.AreNotEqual(JsonValueKind.Undefined, replyPayload.ValueKind);
    }

    /// <summary>
    /// Verifies DoubleDisposeDoesNotThrow pattern.
    /// </summary>
    [TestMethod]
    [Ignore("Requires real Azure Service Bus namespace - no local emulator available")]
    public async Task DoubleDisposeDoesNotThrow()
    {
        AzureServiceBusTransportOptions options = new()
        {
            ConnectionString = "Endpoint=sb://your-namespace.servicebus.windows.net/;...",
            QueueName = "test-queue",
        };

        AzureServiceBusMessageTransport transport = await AzureServiceBusMessageTransport.CreateAsync(options);

        await transport.DisposeAsync();
        await transport.DisposeAsync();
    }
}