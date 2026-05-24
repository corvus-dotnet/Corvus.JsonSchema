// <copyright file="NatsFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Testcontainers.Nats;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a NATS container for integration tests.
/// </summary>
internal static class NatsFixture
{
    private static NatsContainer? s_container;

    /// <summary>
    /// Gets the connection string for the running NATS container.
    /// </summary>
    public static string ConnectionString => s_container?.GetConnectionString()
        ?? throw new InvalidOperationException("NATS container not started.");

    /// <summary>
    /// Starts the NATS container.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        s_container = new NatsBuilder().Build();
        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the NATS container.
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