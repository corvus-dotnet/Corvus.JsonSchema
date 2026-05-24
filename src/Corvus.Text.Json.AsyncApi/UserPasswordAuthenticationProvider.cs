// <copyright file="UserPasswordAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider for username/password (SASL PLAIN) security schemes.
/// </summary>
/// <remarks>
/// <para>
/// Populates the <see cref="MessageAuthenticationContext.Credentials"/> dictionary
/// with <c>username</c> and <c>password</c> entries. The transport reads these
/// to configure its connection (e.g., Kafka SASL, AMQP PLAIN, MQTT credentials).
/// </para>
/// </remarks>
public sealed class UserPasswordAuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly string username;
    private readonly string password;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPasswordAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public UserPasswordAuthenticationProvider(string username, string password)
    {
        this.username = username;
        this.password = password;
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        context.Credentials["username"] = this.username;
        context.Credentials["password"] = this.password;
        return ValueTask.CompletedTask;
    }
}