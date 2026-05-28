// <copyright file="IMessageAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Provides authentication credentials for message transport connections.
/// </summary>
/// <remarks>
/// <para>
/// Implementations supply credentials to the <see cref="IMessageTransport"/>
/// before messages are published or subscriptions are established — for example,
/// providing username/password for SASL authentication, an API key, a bearer
/// token, or a client certificate.
/// </para>
/// <para>
/// The provider is called by the transport during connection establishment
/// or when credentials need to be refreshed. The <see cref="MessageAuthenticationContext"/>
/// contains information about the security scheme requirements.
/// </para>
/// </remarks>
public interface IMessageAuthenticationProvider
{
    /// <summary>
    /// Applies authentication credentials to the transport connection context.
    /// </summary>
    /// <param name="context">The authentication context describing the required
    /// security scheme and providing a mechanism to supply credentials.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when credentials have been supplied.</returns>
    ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default);
}