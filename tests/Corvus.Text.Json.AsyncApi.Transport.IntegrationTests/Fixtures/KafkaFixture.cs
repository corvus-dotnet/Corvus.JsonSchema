// <copyright file="KafkaFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Testcontainers.Kafka;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a Kafka container for integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Uses the default KafkaBuilder configuration which works on Docker in CI.
/// On Podman, the container.Hostname advertised by default may not be resolvable
/// from the host, but the mapped port via localhost should work.
/// </para>
/// </remarks>
internal static class KafkaFixture
{
    private static KafkaContainer? s_container;

    /// <summary>
    /// Gets the bootstrap servers string for the running Kafka container.
    /// </summary>
    public static string BootstrapServers
    {
        get
        {
            if (s_container is null)
            {
                throw new InvalidOperationException("Kafka container not started.");
            }

            // For Podman compatibility: use localhost instead of container.Hostname
            // which isn't resolvable from Windows host under Podman.
            // Docker in CI works with either approach.
            int mappedPort = s_container.GetMappedPublicPort(9092);
            return $"localhost:{mappedPort}";
        }
    }

    /// <summary>
    /// Starts the Kafka container.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        s_container = new KafkaBuilder("confluentinc/cp-kafka:7.8.0")
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