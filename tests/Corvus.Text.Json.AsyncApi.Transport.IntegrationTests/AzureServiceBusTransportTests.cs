// <copyright file="AzureServiceBusTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.AzureServiceBus;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusMessageTransport"/> against the Azure Service Bus emulator.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class AzureServiceBusTransportTests
{
    private static AzureServiceBusMessageTransport s_transport = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await AzureServiceBusFixture.StartAsync();
        s_transport = await AzureServiceBusMessageTransport.CreateAsync(new AzureServiceBusTransportOptions
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        });
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_transport is not null)
        {
            await s_transport.DisposeAsync();
        }

        await AzureServiceBusFixture.StopAsync();
    }

    [TestMethod]
    public async Task PublishAndSubscribeRoundtrip()
    {
        // Arrange
        ReadOnlyMemory<byte> channel = "test-queue"u8.ToArray();
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

        // Allow subscription loop to start
        await Task.Delay(500);

        // Act
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"sensor":"temp","value":22.5}"""u8.ToArray());
        await s_transport.PublishAsync(channel, doc.RootElement);

        // Assert
        bool wasReceived = await received.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(wasReceived, "[Azure Service Bus] Message was not received within timeout.");
        Assert.AreEqual(JsonValueKind.Object, receivedPayloadKind);

        await s_transport.UnsubscribeAsync(channel);
    }

    [TestMethod]
    public async Task DoubleDisposeDoesNotThrow()
    {
        AzureServiceBusTransportOptions options = new()
        {
            ConnectionString = AzureServiceBusFixture.ConnectionString,
            QueueName = "test-queue",
        };

        AzureServiceBusMessageTransport transport = await AzureServiceBusMessageTransport.CreateAsync(options);

        await transport.DisposeAsync();
        await transport.DisposeAsync();
    }
}