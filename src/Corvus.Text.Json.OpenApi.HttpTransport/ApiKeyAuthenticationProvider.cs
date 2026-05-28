// <copyright file="ApiKeyAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IHttpAuthenticationProvider"/> that injects an API key
/// into each request as a header, query parameter, or cookie.
/// </summary>
/// <remarks>
/// <para>
/// This corresponds to the OpenAPI <c>apiKey</c> security scheme type.
/// The <see cref="ApiKeyLocation"/> determines where the key is placed:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ApiKeyLocation.Header"/> — the key is
/// added as a request header with the specified parameter name.</description></item>
/// <item><description><see cref="ApiKeyLocation.Query"/> — the key is
/// appended as a query parameter to the request URI.</description></item>
/// <item><description><see cref="ApiKeyLocation.Cookie"/> — the key is
/// added as a <c>Cookie</c> header value.</description></item>
/// </list>
/// </remarks>
public sealed class ApiKeyAuthenticationProvider : IHttpAuthenticationProvider
{
    private readonly string apiKey;
    private readonly string parameterName;
    private readonly ApiKeyLocation location;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The API key value.</param>
    /// <param name="parameterName">The name of the header, query parameter,
    /// or cookie that carries the key (e.g. <c>X-API-Key</c>, <c>api_key</c>).</param>
    /// <param name="location">Where to place the API key in the request.</param>
    public ApiKeyAuthenticationProvider(
        string apiKey,
        string parameterName,
        ApiKeyLocation location)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(parameterName);
        this.apiKey = apiKey;
        this.parameterName = parameterName;
        this.location = location;
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        switch (this.location)
        {
            case ApiKeyLocation.Header:
                request.Headers.TryAddWithoutValidation(this.parameterName, this.apiKey);
                break;

            case ApiKeyLocation.Query:
                string originalUri = request.RequestUri!.OriginalString;
                char separator = originalUri.Contains('?') ? '&' : '?';
                request.RequestUri = new Uri(
                    $"{originalUri}{separator}{Uri.EscapeDataString(this.parameterName)}={Uri.EscapeDataString(this.apiKey)}",
                    UriKind.RelativeOrAbsolute);
                break;

            case ApiKeyLocation.Cookie:
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    $"{this.parameterName}={this.apiKey}");
                break;
        }

        return default;
    }
}