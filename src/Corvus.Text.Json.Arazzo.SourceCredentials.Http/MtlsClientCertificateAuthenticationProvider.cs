// <copyright file="MtlsClientCertificateAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// The per-request authentication for an mTLS source (design §13.1): a no-op. An mTLS client certificate authenticates
/// the <em>connection</em> at the TLS handshake — it is configured on the source's <see cref="HttpClient"/> handler
/// (see <see cref="SourceCredentialTransports"/>), not applied to each request — so there is nothing to add per request.
/// The <see cref="SourceCredentialCache"/> caches this as the built provider for a <c>mtls</c> binding, keeping the warm
/// path allocation-free like every other kind.
/// </summary>
internal sealed class MtlsClientCertificateAuthenticationProvider : IHttpAuthenticationProvider
{
    /// <summary>The shared instance (the provider is stateless).</summary>
    public static readonly MtlsClientCertificateAuthenticationProvider Instance = new();

    private MtlsClientCertificateAuthenticationProvider()
    {
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}