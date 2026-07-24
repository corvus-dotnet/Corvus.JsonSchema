// <copyright file="IChannelTransportFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// The connection settings a protocol's <see cref="IChannelTransportFactory"/> builds an
/// <see cref="IMessageTransport"/> from (ADR 0051): the environment's broker endpoint, the credential shape, the
/// resolved secret values by role, and any non-secret configuration. Protocol-neutral by design — each factory
/// interprets the auth kind and config in its protocol's terms (a <c>bearer</c> secret is a NATS token or an
/// Azure Service Bus SAS; a <c>basic</c> pair is a NATS user/password or Kafka SASL/PLAIN).
/// </summary>
/// <param name="SourceName">The channel source's <c>sourceDescriptions</c> name (for connection naming/diagnostics).</param>
/// <param name="Environment">The deployment environment the connection serves (for connection naming/diagnostics).</param>
/// <param name="ServerUrl">The broker endpoint from the binding's <c>serverUrl</c> config — protocol-interpreted
/// (a Kafka value may be a bootstrap list).</param>
/// <param name="AuthKind">The credential shape's wire token (<c>bearer</c>, <c>basic</c>,
/// <c>oauth2ClientCredentials</c>, <c>mtls</c>) — how the secrets become an authenticator.</param>
/// <param name="Secrets">The resolved secret values by role name (e.g. <c>value</c>, <c>password</c>). Revealed
/// once at connection time and held only by the built transport's connection, mirroring the OAuth
/// client-credential posture; never logged.</param>
/// <param name="Config">The binding's non-secret configuration entries (e.g. <c>username</c>, a SASL
/// <c>mechanism</c>), excluding secrets by construction.</param>
public sealed record ChannelTransportSettings(
    string SourceName,
    string Environment,
    string ServerUrl,
    string AuthKind,
    IReadOnlyDictionary<string, string> Secrets,
    IReadOnlyDictionary<string, string> Config);

/// <summary>
/// Builds a connected <see cref="IMessageTransport"/> for one broker protocol (ADR 0051) — the channel analogue
/// of the HTTP authentication providers. The protocol is declared by the channel source's AsyncAPI document
/// (<c>servers[].protocol</c>) and baked into the workflow's descriptor; a host registers one factory per
/// protocol it serves (each in its own package with its own broker SDK), and the runner's channel-transport
/// cache dispatches to the factory matching the source's protocol. An unregistered protocol or an unsupported
/// (protocol, auth-kind) combination fails closed.
/// </summary>
public interface IChannelTransportFactory
{
    /// <summary>Gets the AsyncAPI protocol identifier this factory serves (e.g. <c>nats</c>, <c>kafka</c>).</summary>
    string Protocol { get; }

    /// <summary>Builds and connects a transport from the settings.</summary>
    /// <param name="settings">The connection settings resolved from the source's channel credential.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The connected transport; the caller owns its lifetime.</returns>
    /// <exception cref="NotSupportedException">The auth kind is not supported for this protocol.</exception>
    ValueTask<IMessageTransport> CreateTransportAsync(ChannelTransportSettings settings, CancellationToken cancellationToken);
}