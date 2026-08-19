// <copyright file="MqttTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using MQTTnet.Protocol;

namespace Corvus.Text.Json.AsyncApi.Mqtt;

/// <summary>
/// Configuration options for the <see cref="MqttMessageTransport"/>.
/// </summary>
public sealed class MqttTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the MQTT broker host (e.g., "localhost").
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the MQTT broker port.
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Gets or sets the client ID. If empty, a random ID is generated.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MQTT username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the MQTT password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the default QoS level for published messages.
    /// </summary>
    public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; } = MqttQualityOfServiceLevel.AtLeastOnce;

    /// <summary>
    /// Gets or sets a value indicating whether to retain published messages.
    /// </summary>
    public bool Retain { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use a clean session.
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// Gets or sets the keep-alive interval in seconds.
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the dead-letter topic suffix.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = "/dead-letter";

    /// <summary>
    /// Gets or sets the user property key under which requests duplicate the correlation ID,
    /// or <see langword="null"/> (the default) to send it only as MQTT 5
    /// <c>CorrelationData</c>, the native slot this transport reads. Set a key only for
    /// foreign consumers that cannot read <c>CorrelationData</c>; the duplicate costs a
    /// string and a user property per request.
    /// </summary>
    public string? CorrelationIdPropertyKey { get; set; }

    /// <summary>
    /// Gets or sets the headers user property key.
    /// </summary>
    public string HeadersPropertyKey { get; set; } = "corvus-headers";

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }

    /// <inheritdoc/>
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}