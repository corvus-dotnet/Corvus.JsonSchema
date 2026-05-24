// <copyright file="KafkaFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Testcontainers.Kafka;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a Kafka container for integration tests.
/// </summary>
internal static class KafkaFixture
{
    private static KafkaContainer? s_container;

    /// <summary>
    /// Gets the bootstrap servers string for the running Kafka container.
    /// </summary>
    public static string BootstrapServers => s_container?.GetBootstrapAddress()
        ?? throw new InvalidOperationException("Kafka container not started.");

    /// <summary>
    /// Starts the Kafka container (KRaft mode, no ZooKeeper).
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        s_container = new KafkaBuilder()
            .Build();
        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the Kafka container.
    /// </summary>
    /// <returns>A task that completes when the container is disposed.</returns>
    public static async Task StopAsync()
    {
        if (s_container is not null)
        {
            await s_container.DisposeAsync().ConfigureAwait(false);
            s_container = null;
        }
    }
}