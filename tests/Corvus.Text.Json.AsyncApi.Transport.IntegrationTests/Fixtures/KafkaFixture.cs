// <copyright file="KafkaFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages a Kafka container for integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Testcontainers bootstrap address so Docker, Podman, and any
/// TESTCONTAINERS_HOST_OVERRIDE configuration all use the same host address that
/// Kafka advertises to clients.
/// </para>
/// </remarks>
internal static class KafkaFixture
{
    private static readonly TimeSpan TopicReadyTimeout = TimeSpan.FromSeconds(30);
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

            return s_container.GetBootstrapAddress();
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
    /// Creates a single-partition topic and waits until broker metadata reports it as ready.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <returns>A task that completes when the topic is ready.</returns>
    public static async Task CreateTopicAsync(string topic)
    {
        if (s_container is null)
        {
            throw new InvalidOperationException("Kafka container not started.");
        }

        using IAdminClient adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = BootstrapServers,
        }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
                [new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]).ConfigureAwait(false);
        }
        catch (CreateTopicsException ex) when (ex.Results.Count == 1 && ex.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
        {
            // A retry may observe a topic that was created by the previous attempt.
        }

        await WaitForTopicReadyAsync(adminClient, topic).ConfigureAwait(false);
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

    private static async Task WaitForTopicReadyAsync(IAdminClient adminClient, string topic)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TopicReadyTimeout;
        string lastError = "No metadata returned.";

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                Metadata metadata = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(5));
                TopicMetadata? topicMetadata = metadata.Topics.FirstOrDefault(t => string.Equals(t.Topic, topic, StringComparison.Ordinal));

                if (topicMetadata is not null)
                {
                    lastError = topicMetadata.Error.Reason;

                    if (!topicMetadata.Error.IsError &&
                        topicMetadata.Partitions.Count > 0 &&
                        topicMetadata.Partitions.All(p => !p.Error.IsError))
                    {
                        return;
                    }
                }
            }
            catch (KafkaException ex)
            {
                lastError = ex.Error.Reason;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka topic '{topic}' was not ready after {TopicReadyTimeout}. Last metadata error: {lastError}");
    }
}