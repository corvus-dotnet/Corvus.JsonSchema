// <copyright file="AzureServiceBusTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.Messaging.ServiceBus;

namespace Corvus.Text.Json.AsyncApi.AzureServiceBus;

/// <summary>
/// Configuration options for the Azure Service Bus message transport.
/// </summary>
public sealed class AzureServiceBusTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the Service Bus namespace connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified namespace (e.g., "myns.servicebus.windows.net").
    /// Used with managed identity or TokenCredential.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Gets or sets the Azure token credential for authentication.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the queue name (required when UseTopic = false).
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Gets or sets the topic name (required when UseTopic = true).
    /// </summary>
    public string? TopicName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use topics (true) or queues (false).
    /// Topics enable pub/sub with multiple subscriptions per channel.
    /// </summary>
    public bool UseTopic { get; set; }

    /// <summary>
    /// Gets or sets the subscription name when using topics.
    /// Multiple consumers with the same subscription name share messages (competing consumers).
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Gets or sets the suffix appended to channel names to form dead-letter channel addresses.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = ".dead";

    /// <summary>
    /// Gets or sets the request timeout for request-reply operations.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the default receive mode.
    /// </summary>
    public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;

    /// <summary>
    /// Gets or sets the maximum auto-lock renewal duration.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the prefetch count.
    /// </summary>
    public int PrefetchCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to use sessions.
    /// </summary>
    public bool EnableSessions { get; set; }

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }

    /// <inheritdoc/>
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}