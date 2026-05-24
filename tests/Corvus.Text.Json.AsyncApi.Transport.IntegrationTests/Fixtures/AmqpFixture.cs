// <copyright file="AmqpFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Testcontainers.RabbitMq;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a RabbitMQ container for AMQP integration tests.
/// </summary>
internal static class AmqpFixture
{
    private static RabbitMqContainer? s_container;

    /// <summary>
    /// Gets the connection URI for the running RabbitMQ container.
    /// </summary>
    public static string ConnectionUri => s_container?.GetConnectionString()
        ?? throw new InvalidOperationException("RabbitMQ container not started.");

    /// <summary>
    /// Starts the RabbitMQ container.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        s_container = new RabbitMqBuilder().Build();
        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the RabbitMQ container.
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