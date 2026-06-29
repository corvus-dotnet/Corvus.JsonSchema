// <copyright file="HttpClientApiTransportFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IApiTransportFactory"/> that produces <see cref="HttpClientTransport"/> instances over a
/// shared, host-owned <see cref="HttpClient"/> (whose <see cref="HttpClient.BaseAddress"/> is the source's
/// base URL). Each created transport leaves the client open (<c>disposeClient: false</c>), so a caller that
/// disposes the per-use transport does not tear down the shared client.
/// </summary>
public sealed class HttpClientApiTransportFactory : IApiTransportFactory
{
    private readonly HttpClient httpClient;
    private readonly IHttpAuthenticationProvider? authenticationProvider;
    private readonly Func<CancellationToken, ValueTask<Uri?>>? baseUrlOverride;

    /// <summary>Initializes a new instance of the <see cref="HttpClientApiTransportFactory"/> class.</summary>
    /// <param name="httpClient">The shared client to send through; its <see cref="HttpClient.BaseAddress"/> is the source's base URL. The host owns its lifetime.</param>
    /// <param name="authenticationProvider">An optional authentication provider applied to each request; <see langword="null"/> for unauthenticated sources.</param>
    /// <param name="baseUrlOverride">An optional per-environment base URL override resolver (design §8); when it yields a non-null URI, relative requests resolve against it instead of the client's base address.</param>
    public HttpClientApiTransportFactory(HttpClient httpClient, IHttpAuthenticationProvider? authenticationProvider = null, Func<CancellationToken, ValueTask<Uri?>>? baseUrlOverride = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
        this.authenticationProvider = authenticationProvider;
        this.baseUrlOverride = baseUrlOverride;
    }

    /// <inheritdoc/>
    public IApiTransport CreateTransport()
        => new HttpClientTransport(this.httpClient, this.authenticationProvider, disposeClient: false, baseUrlOverride: this.baseUrlOverride);
}