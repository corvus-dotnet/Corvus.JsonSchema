// <copyright file="NatsChannelTransportFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Nats;

/// <summary>
/// The NATS <see cref="IChannelTransportFactory"/> (ADR 0051): builds a connected, JetStream-enabled
/// <see cref="NatsMessageTransport"/> from a channel credential's settings. The credential shapes map to the
/// CONNECT handshake: <c>bearer</c> presents its <c>value</c> secret as the connection token, and <c>basic</c>
/// presents the <c>username</c> config with its <c>password</c> secret; the other shapes are not yet supported
/// for NATS and fail closed.
/// </summary>
public sealed class NatsChannelTransportFactory : IChannelTransportFactory
{
    /// <inheritdoc/>
    public string Protocol => "nats";

    /// <inheritdoc/>
    public async ValueTask<IMessageTransport> CreateTransportAsync(ChannelTransportSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // JetStream is on by default: workflow channel steps are durable (a message published before its
        // consumer subscribes must not be lost), and the stream name derives per subject when unset.
        NatsTransportOptions options = new()
        {
            Url = settings.ServerUrl,
            Name = $"{settings.SourceName}-{settings.Environment}",
            UseJetStream = true,
            StorageType = StorageType.File,
        };

        switch (settings.AuthKind)
        {
            case "bearer":
                options.Token = RequireSecret(settings, "value");
                break;

            case "basic":
                options.Username = settings.Config.TryGetValue("username", out string? username) && username.Length > 0
                    ? username
                    : throw new InvalidOperationException($"The 'basic' channel credential for source '{settings.SourceName}' requires a 'username' config entry.");
                options.Password = RequireSecret(settings, "password");
                break;

            default:
                throw new NotSupportedException(
                    $"The NATS transport does not support the '{settings.AuthKind}' credential shape for source '{settings.SourceName}'; bind 'bearer' (a token presented at connect) or 'basic' (username/password).");
        }

        return await NatsMessageTransport.CreateAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private static string RequireSecret(ChannelTransportSettings settings, string role)
        => settings.Secrets.TryGetValue(role, out string? value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"The '{settings.AuthKind}' channel credential for source '{settings.SourceName}' requires a '{role}' secret reference.");
}