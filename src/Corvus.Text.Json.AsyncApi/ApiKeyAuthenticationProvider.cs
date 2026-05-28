// <copyright file="ApiKeyAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider for API key security schemes.
/// </summary>
/// <remarks>
/// <para>
/// Populates the <see cref="MessageAuthenticationContext.Credentials"/> dictionary
/// with a <c>key</c> entry (and optionally <c>name</c> and <c>in</c> for HTTP API keys).
/// The transport reads this to attach the API key to outgoing messages or connections.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly string apiKey;
    private readonly string? name;
    private readonly string? location;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The API key value.</param>
    /// <param name="name">The header or query parameter name (for httpApiKey schemes).</param>
    /// <param name="location">The location: "header", "query", or "cookie" (for httpApiKey schemes).</param>
    public ApiKeyAuthenticationProvider(string apiKey, string? name = null, string? location = null)
    {
        this.apiKey = apiKey;
        this.name = name;
        this.location = location;
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        context.Credentials["key"] = this.apiKey;

        if (this.name is not null)
        {
            context.Credentials["name"] = this.name;
        }

        if (this.location is not null)
        {
            context.Credentials["in"] = this.location;
        }

        return ValueTask.CompletedTask;
    }
}