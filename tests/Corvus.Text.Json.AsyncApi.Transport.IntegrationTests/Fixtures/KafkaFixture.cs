// <copyright file="KafkaFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a Kafka container for integration tests using the official
/// Apache Kafka image in KRaft mode (no ZooKeeper dependency).
/// </summary>
/// <remarks>
/// <para>
/// The container uses three listeners:
/// </para>
/// <list type="bullet">
/// <item><description>EXTERNAL on port 9092 — advertised as <c>host:mappedPort</c> for host clients</description></item>
/// <item><description>INTERNAL on port 19092 — advertised as <c>localhost:19092</c> for in-container tools</description></item>
/// <item><description>CONTROLLER on port 9093 — for KRaft consensus</description></item>
/// </list>
/// <para>
/// Dual listeners are required because Kafka metadata tells clients to reconnect
/// to the advertised address. Without an internal listener, in-container tools
/// (health checks, admin commands) try to reach the host-mapped port and fail.
/// </para>
/// </remarks>
internal static class KafkaFixture
{
    private const int ExternalPort = 9092;
    private const int InternalPort = 19092;
    private const int ControllerPort = 9093;
    private const string StartupScript = "/testcontainers_start.sh";

    private static IContainer? s_container;

    /// <summary>
    /// Gets the bootstrap servers string for the running Kafka container.
    /// </summary>
    public static string BootstrapServers => s_container is not null
        ? $"{s_container.Hostname}:{s_container.GetMappedPublicPort(ExternalPort)}"
        : throw new InvalidOperationException("Kafka container not started.");

    /// <summary>
    /// Starts the Kafka container (KRaft mode, no ZooKeeper).
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        s_container = new ContainerBuilder("apache/kafka:3.8.1")
            .WithPortBinding(ExternalPort, true)
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "INTERNAL")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT,INTERNAL:PLAINTEXT")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", $"1@localhost:{ControllerPort}")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            .WithEnvironment("KAFKA_LISTENERS", $"EXTERNAL://0.0.0.0:{ExternalPort},INTERNAL://0.0.0.0:{InternalPort},CONTROLLER://0.0.0.0:{ControllerPort}")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand($"while [ ! -f {StartupScript} ]; do sleep 0.1; done; sh {StartupScript}")
            .WithStartupCallback(ConfigureAdvertisedListenersAsync)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Transitioning from RECOVERY to RUNNING.*"))
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

    private static async Task ConfigureAdvertisedListenersAsync(IContainer container, CancellationToken ct)
    {
        int mappedPort = container.GetMappedPublicPort(ExternalPort);
        string script = $"#!/bin/bash\nexport KAFKA_ADVERTISED_LISTENERS=\"EXTERNAL://{container.Hostname}:{mappedPort},INTERNAL://localhost:{InternalPort}\"\nexec /etc/kafka/docker/run\n";

        await container.CopyAsync(
            Encoding.UTF8.GetBytes(script),
            StartupScript,
            uid: 0,
            gid: 0,
            fileMode: UnixFileModes.OtherExecute | UnixFileModes.GroupExecute | UnixFileModes.UserExecute
                | UnixFileModes.OtherRead | UnixFileModes.GroupRead | UnixFileModes.UserRead
                | UnixFileModes.UserWrite,
            ct: ct).ConfigureAwait(false);
    }
}