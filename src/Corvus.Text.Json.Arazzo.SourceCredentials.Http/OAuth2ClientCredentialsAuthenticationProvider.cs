// <copyright file="OAuth2ClientCredentialsAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// An <see cref="IHttpAuthenticationProvider"/> implementing the OAuth 2.0 client-credentials flow (design §13.4): it
/// fetches an access token from the token endpoint, caches it until just before expiry, and applies it as
/// <c>Authorization: Bearer</c>. The fetch is <strong>single-flight</strong> (concurrent callers await one in-flight
/// request — no thundering herd) and <strong>proactive</strong> (a token within <see cref="OAuth2ClientCredentialsOptions.RefreshSkew"/>
/// of expiry is refreshed before it is used), so the warm hot path never stalls on, nor uses an expired, token.
/// </summary>
/// <remarks>
/// The client secret is retained in memory (so the token can be refreshed) but never persisted or logged; the cached
/// access token is the derived artifact applied on the warm path. One provider instance serves one
/// (sourceName, environment); the runner's <see cref="SourceCredentialCache"/> bounds its lifetime.
/// </remarks>
public sealed class OAuth2ClientCredentialsAuthenticationProvider : IHttpAuthenticationProvider, IDisposable
{
    private readonly HttpClient tokenClient;
    private readonly OAuth2ClientCredentialsOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private volatile CachedToken? cached;

    /// <summary>Initializes a new instance of the <see cref="OAuth2ClientCredentialsAuthenticationProvider"/> class.</summary>
    /// <param name="tokenClient">The HTTP client used to call the token endpoint. The caller owns its lifetime.</param>
    /// <param name="options">The flow configuration (token endpoint, resolved credentials, refresh tuning).</param>
    /// <param name="timeProvider">The time source for expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public OAuth2ClientCredentialsAuthenticationProvider(HttpClient tokenClient, OAuth2ClientCredentialsOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(tokenClient);
        ArgumentNullException.ThrowIfNull(options);
        this.tokenClient = tokenClient;
        this.options = options;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = await this.GetTokenHeaderAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => this.refreshGate.Dispose();

    private async ValueTask<AuthenticationHeaderValue> GetTokenHeaderAsync(CancellationToken cancellationToken)
    {
        // Fast path: a token that is not within the refresh window — no I/O, no locking.
        CachedToken? token = this.cached;
        if (token is not null && !this.IsStale(token))
        {
            return token.Header;
        }

        // Single-flight: one caller fetches; the rest await the gate and then see the freshly-cached token.
        await this.refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            token = this.cached;
            if (token is not null && !this.IsStale(token))
            {
                return token.Header;
            }

            CachedToken fresh = await this.FetchTokenAsync(cancellationToken).ConfigureAwait(false);
            this.cached = fresh;
            return fresh.Header;
        }
        finally
        {
            this.refreshGate.Release();
        }
    }

    private bool IsStale(CachedToken token) => this.timeProvider.GetUtcNow() >= token.ExpiresAt - this.options.RefreshSkew;

    private async ValueTask<CachedToken> FetchTokenAsync(CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, this.options.TokenEndpoint);
        var form = new List<KeyValuePair<string, string>>(4) { new("grant_type", "client_credentials") };
        if (this.options.Scope is { } scope)
        {
            form.Add(new("scope", scope));
        }

        if (this.options.ClientAuthentication == OAuth2ClientAuthentication.BasicAuthenticationHeader)
        {
            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{this.options.ClientId}:{this.options.ClientSecret}"));
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
        else
        {
            form.Add(new("client_id", this.options.ClientId));
            form.Add(new("client_secret", this.options.ClientSecret));
        }

        requestMessage.Content = new FormUrlEncodedContent(form);

        DateTimeOffset requestedAt = this.timeProvider.GetUtcNow();
        using HttpResponseMessage response = await this.tokenClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new OAuth2TokenException($"the token endpoint returned {(int)response.StatusCode} ({response.StatusCode}).");
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        (string accessToken, TimeSpan lifetime) = ParseTokenResponse(body, this.options.DefaultTokenLifetime);
        return new CachedToken(new AuthenticationHeaderValue("Bearer", accessToken), requestedAt + lifetime);
    }

    // Parses { "access_token": "...", "expires_in": N, ... } with the Corvus reader (no System.Text.Json). Unknown
    // members are skipped; a missing expires_in falls back to the configured default lifetime.
    private static (string AccessToken, TimeSpan Lifetime) ParseTokenResponse(ReadOnlySpan<byte> body, TimeSpan defaultLifetime)
    {
        string? accessToken = null;
        TimeSpan lifetime = defaultLifetime;

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new OAuth2TokenException("the token response was not a JSON object.");
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("access_token"u8))
            {
                reader.Read();
                accessToken = reader.GetString();
            }
            else if (reader.ValueTextEquals("expires_in"u8))
            {
                reader.Read();
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out long seconds) && seconds > 0)
                {
                    lifetime = TimeSpan.FromSeconds(seconds);
                }
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new OAuth2TokenException("the token response did not contain an access_token.");
        }

        return (accessToken, lifetime);
    }

    private sealed record CachedToken(AuthenticationHeaderValue Header, DateTimeOffset ExpiresAt);
}