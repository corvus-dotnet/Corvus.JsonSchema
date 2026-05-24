// <copyright file="MqttFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages an Eclipse Mosquitto container for MQTT integration tests.
/// </summary>
internal static class MqttFixture
{
    private const int MqttPort = 1883;
    private static IContainer? s_container;

    /// <summary>
    /// Gets the host for the running Mosquitto container.
    /// </summary>
    public static string Host => s_container?.Hostname
        ?? throw new InvalidOperationException("MQTT container not started.");

    /// <summary>
    /// Gets the mapped port for the running Mosquitto container.
    /// </summary>
    public static int Port => s_container?.GetMappedPublicPort(MqttPort)
        ?? throw new InvalidOperationException("MQTT container not started.");

    /// <summary>
    /// Starts the Mosquitto container with a permissive listener configuration.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        // Mosquitto 2.x requires explicit config for anonymous access
        byte[] config = "listener 1883\nallow_anonymous true\n"u8.ToArray();

        s_container = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2")
            .WithPortBinding(MqttPort, true)
            .WithResourceMapping(config, "/mosquitto/config/mosquitto.conf")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(MqttPort))
            .Build();

        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the Mosquitto container.
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