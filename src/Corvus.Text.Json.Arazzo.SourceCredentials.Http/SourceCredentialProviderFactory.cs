// <copyright file="SourceCredentialProviderFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Builds the <see cref="IHttpAuthenticationProvider"/> for a <see cref="SourceCredentialBinding"/> (design §13): it
/// resolves the binding's <see cref="SecretRef"/>(s) to live secret material via the runner-side
/// <see cref="ISecretResolver"/>, constructs the matching provider (api key / bearer / basic / OAuth2
/// client-credentials), and <strong>scrubs the secret material immediately</strong> once the provider holds the derived
/// artifact (the pre-built header, or — for OAuth2 — the credentials needed to mint tokens). This is the only place a
/// secret reference becomes a secret, and it is runner-side.
/// </summary>
/// <remarks>
/// The built provider is what the runner's <see cref="SourceCredentialCache"/> caches; the §13.4 warm path reuses it
/// with no further secret-store I/O. Config keys read from the binding (all non-secret): api key —
/// <c>parameterName</c>/<c>headerName</c> and <c>location</c>; basic — <c>username</c>; OAuth2 — <c>tokenUrl</c>,
/// <c>clientId</c>, <c>scope</c>/<c>scopes</c>, <c>clientAuthentication</c>.
/// </remarks>
public sealed class SourceCredentialProviderFactory
{
    private readonly ISecretResolver resolver;
    private readonly HttpClient? oauthTokenClient;
    private readonly TimeProvider timeProvider;
    private readonly bool allowInsecureOAuthTokenEndpoint;

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialProviderFactory"/> class.</summary>
    /// <param name="resolver">The runner-side secret resolver.</param>
    /// <param name="oauthTokenClient">An HTTP client for OAuth 2.0 token-endpoint calls; required only if an
    /// <see cref="SourceCredentialKind.OAuth2ClientCredentials"/> binding is built. The caller owns its lifetime.</param>
    /// <param name="timeProvider">The time source for token expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="allowInsecureOAuthTokenEndpoint">
    /// When <see langword="false"/> (the secure default, §17.5/F5) an OAuth2 client-credentials binding whose
    /// <c>tokenUrl</c> is not <c>https</c> is rejected — POSTing the client secret over cleartext would expose it on the
    /// wire. A deployment may set this to <see langword="true"/> to opt into an <c>http</c> token endpoint for local
    /// development against a non-TLS identity provider.
    /// </param>
    public SourceCredentialProviderFactory(ISecretResolver resolver, HttpClient? oauthTokenClient = null, TimeProvider? timeProvider = null, bool allowInsecureOAuthTokenEndpoint = false)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        this.resolver = resolver;
        this.oauthTokenClient = oauthTokenClient;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.allowInsecureOAuthTokenEndpoint = allowInsecureOAuthTokenEndpoint;
    }

    /// <summary>Builds the HTTP authentication provider for a binding.</summary>
    /// <param name="binding">The source credential binding (references + non-secret metadata).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The provider to apply to outbound requests for the binding's source.</returns>
    public async ValueTask<IHttpAuthenticationProvider> CreateAsync(SourceCredentialBinding binding, CancellationToken cancellationToken = default)
    {
        return binding.AuthKindValue switch
        {
            SourceCredentialKind.ApiKey => await this.CreateApiKeyAsync(binding, cancellationToken).ConfigureAwait(false),
            SourceCredentialKind.Bearer => await this.CreateBearerAsync(binding, cancellationToken).ConfigureAwait(false),
            SourceCredentialKind.Basic => await this.CreateBasicAsync(binding, cancellationToken).ConfigureAwait(false),
            SourceCredentialKind.OAuth2ClientCredentials => await this.CreateOAuth2Async(binding, cancellationToken).ConfigureAwait(false),

            // mTLS authenticates the connection at the TLS handshake (the client certificate is configured on the
            // source's HttpClient handler, §13.1) — there is nothing to apply per request, so the built provider is a
            // no-op. The certificate itself is resolved by ResolveClientCertificateAsync at client-construction time.
            SourceCredentialKind.Mtls => MtlsClientCertificateAuthenticationProvider.Instance,
            _ => throw new InvalidOperationException($"Unsupported source credential kind '{binding.AuthKindValue}'."),
        };
    }

    /// <summary>Resolves an <see cref="SourceCredentialKind.Mtls"/> binding's certificate references to a live client
    /// certificate for the TLS handshake (design §13.1). Because an mTLS certificate is connection-level — not
    /// per-request — the host applies the returned certificate to the source's <see cref="HttpClient"/> handler at
    /// construction time (see <see cref="SourceCredentialTransports.CreateSourceHttpClientAsync"/>), rather than the
    /// cache caching it as a per-request provider.</summary>
    /// <param name="binding">The mTLS source credential binding.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved client certificate (with its private key). The caller owns its lifetime.</returns>
    /// <remarks>The <c>certificate</c> secret is either a base64-encoded PKCS#12/PFX (optionally protected by a
    /// <c>passphrase</c> secret) or, when a separate <c>privateKey</c> secret is present, a PEM certificate paired with a
    /// PEM private key (optionally encrypted with the <c>passphrase</c>). A PEM-loaded certificate is re-exported to
    /// PKCS#12 so its private key is usable for TLS client authentication on every platform.</remarks>
    public async ValueTask<X509Certificate2> ResolveClientCertificateAsync(SourceCredentialBinding binding, CancellationToken cancellationToken = default)
    {
        using SecretMaterial certificate = await this.ResolveAsync(binding, "certificate", cancellationToken).ConfigureAwait(false);
        SecretMaterial? passphrase = null;
        if (binding.TryGetSecretRef("passphrase", out SecretRef passphraseRef))
        {
            passphrase = await this.resolver.ResolveAsync(passphraseRef, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (binding.TryGetSecretRef("privateKey", out SecretRef privateKeyRef))
            {
                using SecretMaterial privateKey = await this.resolver.ResolveAsync(privateKeyRef, cancellationToken).ConfigureAwait(false);
                using X509Certificate2 fromPem = passphrase is not null
                    ? X509Certificate2.CreateFromEncryptedPem(certificate.Reveal(), privateKey.Reveal(), passphrase.Reveal())
                    : X509Certificate2.CreateFromPem(certificate.Reveal(), privateKey.Reveal());

                // Re-export to PKCS#12 so the private key is persisted and usable for TLS client auth on every platform
                // (a PEM-loaded key is ephemeral on Windows and the handshake would fail to present it).
                return X509CertificateLoader.LoadPkcs12(fromPem.Export(X509ContentType.Pkcs12), null);
            }

            // A base64-encoded PKCS#12/PFX bundling the certificate and its private key, optionally passphrase-protected.
            byte[] pfx = Convert.FromBase64String(certificate.Reveal());
            return X509CertificateLoader.LoadPkcs12(pfx, passphrase?.Reveal());
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' (mtls) has an unreadable client certificate: {ex.Message}", ex);
        }
        finally
        {
            passphrase?.Dispose();
        }
    }

    private async ValueTask<IHttpAuthenticationProvider> CreateApiKeyAsync(SourceCredentialBinding binding, CancellationToken cancellationToken)
    {
        string parameterName = ConfigOrDefault(binding, "parameterName", binding.TryGetConfigValue("headerName", out string? header) ? header! : "X-API-Key");
        ApiKeyLocation location = ParseLocation(binding);
        using SecretMaterial material = await this.ResolveAsync(binding, "value", cancellationToken).ConfigureAwait(false);
        return new ApiKeyAuthenticationProvider(material.Reveal(), parameterName, location);
    }

    private async ValueTask<IHttpAuthenticationProvider> CreateBearerAsync(SourceCredentialBinding binding, CancellationToken cancellationToken)
    {
        using SecretMaterial material = await this.ResolveAsync(binding, "value", cancellationToken).ConfigureAwait(false);
        return new BearerTokenAuthenticationProvider(material.Reveal());
    }

    private async ValueTask<IHttpAuthenticationProvider> CreateBasicAsync(SourceCredentialBinding binding, CancellationToken cancellationToken)
    {
        string username = RequireConfig(binding, "username");
        using SecretMaterial material = await this.ResolveAsync(binding, "password", cancellationToken).ConfigureAwait(false);
        return new BasicAuthenticationProvider(username, material.Reveal());
    }

    private async ValueTask<IHttpAuthenticationProvider> CreateOAuth2Async(SourceCredentialBinding binding, CancellationToken cancellationToken)
    {
        if (this.oauthTokenClient is null)
        {
            throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' uses OAuth2 client credentials but no token-endpoint HttpClient was supplied to the factory.");
        }

        string tokenUrl = RequireConfig(binding, "tokenUrl");
        var tokenEndpoint = new Uri(tokenUrl, UriKind.Absolute);

        // §17.5/F5: refuse to POST the client secret over cleartext. https is mandatory unless the deployment has
        // explicitly opted into an insecure endpoint for local development.
        if (!this.allowInsecureOAuthTokenEndpoint && !string.Equals(tokenEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' has a non-https OAuth2 tokenUrl ('{tokenEndpoint.Scheme}://…'); the client secret would be sent in cleartext. Use an https token endpoint, or construct the factory with allowInsecureOAuthTokenEndpoint: true for local development.");
        }

        string clientId = RequireConfig(binding, "clientId");
        string? scope = binding.TryGetConfigValue("scope", out string? s) ? s : binding.TryGetConfigValue("scopes", out string? ss) ? ss : null;
        OAuth2ClientAuthentication clientAuth = binding.TryGetConfigValue("clientAuthentication", out string? ca) && string.Equals(ca, "basic", StringComparison.OrdinalIgnoreCase)
            ? OAuth2ClientAuthentication.BasicAuthenticationHeader
            : OAuth2ClientAuthentication.RequestBody;

        using SecretMaterial material = await this.ResolveAsync(binding, "clientSecret", cancellationToken).ConfigureAwait(false);
        var options = new OAuth2ClientCredentialsOptions
        {
            TokenEndpoint = tokenEndpoint,
            ClientId = clientId,
            ClientSecret = material.Reveal(),
            Scope = scope,
            ClientAuthentication = clientAuth,
        };

        return new OAuth2ClientCredentialsAuthenticationProvider(this.oauthTokenClient, options, this.timeProvider);
    }

    private async ValueTask<SecretMaterial> ResolveAsync(SourceCredentialBinding binding, string role, CancellationToken cancellationToken)
    {
        if (!binding.TryGetSecretRef(role, out SecretRef secretRef))
        {
            throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' ({binding.AuthKindValue}) is missing the required '{role}' secret reference.");
        }

        return await this.resolver.ResolveAsync(secretRef, cancellationToken).ConfigureAwait(false);
    }

    private static ApiKeyLocation ParseLocation(SourceCredentialBinding binding)
    {
        if (binding.TryGetConfigValue("location", out string? location))
        {
            // Case-fold without allocating a lowercased string (matching the OrdinalIgnoreCase idiom used above).
            if (string.Equals(location, "header", StringComparison.OrdinalIgnoreCase)) { return ApiKeyLocation.Header; }
            if (string.Equals(location, "query", StringComparison.OrdinalIgnoreCase)) { return ApiKeyLocation.Query; }
            if (string.Equals(location, "cookie", StringComparison.OrdinalIgnoreCase)) { return ApiKeyLocation.Cookie; }
            throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' has an unknown api-key location '{location}'.");
        }

        return ApiKeyLocation.Header;
    }

    private static string ConfigOrDefault(SourceCredentialBinding binding, string key, string fallback)
        => binding.TryGetConfigValue(key, out string? value) ? value! : fallback;

    private static string RequireConfig(SourceCredentialBinding binding, string key)
        => binding.TryGetConfigValue(key, out string? value) && !string.IsNullOrEmpty(value)
            ? value!
            : throw new InvalidOperationException($"Binding '{binding.SourceNameValue}' ({binding.AuthKindValue}) is missing the required '{key}' configuration value.");
}