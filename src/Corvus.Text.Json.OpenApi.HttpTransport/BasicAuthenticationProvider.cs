// <copyright file="BasicAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IHttpAuthenticationProvider"/> that sets the
/// <c>Authorization: Basic {credentials}</c> header on each request.
/// </summary>
/// <remarks>
/// The credentials are Base64-encoded from <c>username:password</c>
/// at construction time and reused for every request.
/// </remarks>
public sealed class BasicAuthenticationProvider : IHttpAuthenticationProvider
{
    private readonly AuthenticationHeaderValue headerValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public BasicAuthenticationProvider(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        string encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));
        this.headerValue = new AuthenticationHeaderValue("Basic", encoded);
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = this.headerValue;
        return default;
    }
}