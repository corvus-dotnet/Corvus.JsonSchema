// <copyright file="ProviderBroker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Brokers the interactive OAuth/OIDC sign-in for every connected provider (ADR 0052): a
/// deployment registers providers as configuration (an OIDC issuer resolved by discovery, or
/// explicit authorize/token endpoints), and each principal connects per provider through the
/// brokered popup — authorize, callback, server-side code exchange. The user token is held
/// server-side per <c>(principal, provider)</c> and never reaches the browser; the client secret
/// is a REFERENCE resolved only at exchange time. GitHub folds in as provider #1 under this same
/// machinery (<see cref="GitHubBrokerOptions.ToProviderEntry"/>) — its repos/browse surface stays
/// GitHub-specific, its auth machinery is this one.
/// </summary>
/// <remarks>
/// <para><strong>Custody.</strong> Tokens are keyed by control-plane principal and provider name,
/// unreachable from any other principal's session (<see cref="IProviderTokenStore"/>; in-memory by
/// default, a deployment may substitute an encrypted-at-rest store). The OAuth <c>state</c> is
/// single-use, short-lived, and bound to both the principal and the provider: the callback is a
/// top-level browser navigation that carries no bearer token, so the state IS the authentication
/// of that request.</para>
/// <para><strong>Host coverage.</strong> A provider declares the <c>hosts</c> it covers (exact
/// names or <c>*.suffix</c> patterns); the fetch path attaches a principal's provider token only
/// to a URL whose host the provider covers, so a user token never rides to an arbitrary host.</para>
/// </remarks>
public sealed class ProviderBroker
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(60);

    private readonly HttpClient client;
    private readonly IReadOnlyList<ConnectedProviderOptions> providers;
    private readonly ISecretResolver secrets;
    private readonly IProviderTokenStore tokens;
    private readonly TimeProvider timeProvider;
    private readonly ILogger? logger;
    private readonly ConcurrentDictionary<string, PendingAuth> pending = new();
    private readonly ConcurrentDictionary<string, ProviderEndpoints> discovered = new();

    /// <summary>Initializes a new instance of the <see cref="ProviderBroker"/> class.</summary>
    /// <param name="client">The outbound HTTP client (a deployment configures its handler/egress policy).</param>
    /// <param name="providers">The deployment's provider registry (each entry is validated here).</param>
    /// <param name="secrets">Resolves each provider's client secret reference at exchange time (never held).</param>
    /// <param name="tokens">The per-(principal, provider) token store; <see langword="null"/> uses the in-memory (session-lifetime) store.</param>
    /// <param name="timeProvider">The clock (tests inject a fake).</param>
    /// <param name="logger">Names exchange/discovery refusals in the deployment's logs (never secret material).</param>
    public ProviderBroker(HttpClient client, IReadOnlyList<ConnectedProviderOptions> providers, ISecretResolver secrets, IProviderTokenStore? tokens = null, TimeProvider? timeProvider = null, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(secrets);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConnectedProviderOptions provider in providers)
        {
            provider.Validate();
            if (!names.Add(provider.Name!))
            {
                throw new ArgumentException($"The provider registry names '{provider.Name}' more than once.", nameof(providers));
            }
        }

        this.client = client;
        this.providers = providers;
        this.secrets = secrets;
        this.tokens = tokens ?? new InMemoryProviderTokenStore();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = logger;
    }

    /// <summary>The outcome kinds beginning an authorization can report.</summary>
    public enum BeginOutcome
    {
        /// <summary>The authorize URL composed and the state was minted.</summary>
        Success,

        /// <summary>No provider of that name is registered.</summary>
        UnknownProvider,

        /// <summary>The provider's OIDC discovery document could not be resolved.</summary>
        DiscoveryFailed,
    }

    /// <summary>The outcome kinds completing an authorization can report.</summary>
    public enum CompleteOutcome
    {
        /// <summary>The token exchanged and stored for the state's principal and provider.</summary>
        Success,

        /// <summary>The state is unknown, expired, already used, or bound to a different provider.</summary>
        InvalidState,

        /// <summary>The provider refused the code exchange, or could not be reached.</summary>
        ExchangeFailed,
    }

    /// <summary>Gets the registered providers (configuration entries; secret material is never present — the entry carries a reference).</summary>
    public IReadOnlyList<ConnectedProviderOptions> Providers => this.providers;

    /// <summary>Looks up a registered provider by name.</summary>
    /// <param name="name">The provider name.</param>
    /// <param name="provider">The registered entry, when present.</param>
    /// <returns><see langword="true"/> when the provider is registered.</returns>
    public bool TryGetProvider(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ConnectedProviderOptions? provider)
    {
        foreach (ConnectedProviderOptions candidate in this.providers)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                provider = candidate;
                return true;
            }
        }

        provider = null;
        return false;
    }

    /// <summary>
    /// Whether the named provider covers the host — the gate that keeps a principal's provider
    /// token from riding to a host outside the provider's declared coverage.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="host">The target URL's host.</param>
    /// <returns><see langword="true"/> when a hosts pattern (exact or <c>*.suffix</c>) matches.</returns>
    public bool CoversHost(string providerName, string host)
    {
        if (!this.TryGetProvider(providerName, out ConnectedProviderOptions? provider))
        {
            return false;
        }

        foreach (string pattern in provider.Hosts)
        {
            if (HostMatches(pattern, host))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mints a single-use state bound to the principal AND the provider, and composes the
    /// authorize URL the kit opens in a popup. The state authenticates the callback (a top-level
    /// navigation carries no bearer token), so it is unguessable (256-bit) and short-lived.
    /// </summary>
    /// <param name="providerName">The provider to sign in to.</param>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome and, on success, the authorize URL and the embedded state.</returns>
    public async ValueTask<(BeginOutcome Outcome, string? AuthorizeUrl, string? State)> BeginAuthAsync(string providerName, string principalKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principalKey);
        if (!this.TryGetProvider(providerName, out ConnectedProviderOptions? provider))
        {
            return (BeginOutcome.UnknownProvider, null, null);
        }

        ProviderEndpoints? endpoints = await this.ResolveEndpointsAsync(provider, cancellationToken).ConfigureAwait(false);
        if (endpoints is not { } resolved)
        {
            return (BeginOutcome.DiscoveryFailed, null, null);
        }

        this.PrunePending();
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        this.pending[state] = new PendingAuth(principalKey, provider.Name!, this.timeProvider.GetUtcNow() + StateLifetime);
        string scope = string.IsNullOrEmpty(provider.Scopes) ? string.Empty : $"&scope={Uri.EscapeDataString(provider.Scopes)}";
        string authorizeUrl = resolved.AuthorizeEndpoint +
            (resolved.AuthorizeEndpoint.Contains('?') ? '&' : '?') +
            $"client_id={Uri.EscapeDataString(provider.ClientId!)}" +
            $"&redirect_uri={Uri.EscapeDataString(provider.CallbackUrl!)}" +
            "&response_type=code" +
            scope +
            $"&state={Uri.EscapeDataString(state)}";
        return (BeginOutcome.Success, authorizeUrl, state);
    }

    /// <summary>
    /// Completes the flow: validates and consumes the state (single-use, provider-bound),
    /// exchanges the code server-side (resolving the provider's client secret only for the
    /// exchange), and stores the user token for the state's principal and provider.
    /// </summary>
    /// <param name="providerName">The provider named by the callback route.</param>
    /// <param name="state">The state from the callback query.</param>
    /// <param name="code">The authorization code from the callback query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async ValueTask<CompleteOutcome> CompleteAuthAsync(string providerName, string state, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(state)
            || !this.pending.TryRemove(state, out PendingAuth auth)
            || auth.ExpiresAt < this.timeProvider.GetUtcNow()
            || !string.Equals(auth.Provider, providerName, StringComparison.Ordinal)
            || !this.TryGetProvider(providerName, out ConnectedProviderOptions? provider))
        {
            return CompleteOutcome.InvalidState;
        }

        ProviderEndpoints? endpoints = await this.ResolveEndpointsAsync(provider, cancellationToken).ConfigureAwait(false);
        if (endpoints is not { } resolved)
        {
            return CompleteOutcome.ExchangeFailed;
        }

        ProviderToken? token = await this.ExchangeAsync(provider, resolved.TokenEndpoint, ("code", code), cancellationToken).ConfigureAwait(false);
        if (token is not { } t)
        {
            return CompleteOutcome.ExchangeFailed;
        }

        this.tokens.Set(auth.PrincipalKey, provider.Name!, t);
        return CompleteOutcome.Success;
    }

    /// <summary>Whether the principal currently holds a token for the provider (without touching the provider).</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="providerName">The provider name.</param>
    /// <returns><see langword="true"/> if a token is stored.</returns>
    public bool IsConnected(string principalKey, string providerName) => this.tokens.Get(principalKey, providerName) is not null;

    /// <summary>Drops the principal's token for the provider. Idempotent.</summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="providerName">The provider name.</param>
    public void Disconnect(string principalKey, string providerName) => this.tokens.Remove(principalKey, providerName);

    /// <summary>
    /// The valid access token for the principal and provider, refreshing an expiring one when a
    /// refresh token is held. <see langword="null"/> when the principal has no session (or the
    /// refresh fails — the session is dropped).
    /// </summary>
    /// <param name="principalKey">The calling principal's stable key.</param>
    /// <param name="providerName">The provider name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The access token, or <see langword="null"/>.</returns>
    public async ValueTask<string?> CurrentAccessTokenAsync(string principalKey, string providerName, CancellationToken cancellationToken)
    {
        if (!this.TryGetProvider(providerName, out ConnectedProviderOptions? provider))
        {
            return null;
        }

        ProviderToken? stored = this.tokens.Get(principalKey, providerName);
        if (stored is not { } current)
        {
            return null;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (current.ExpiresAt is not { } expires || expires - TokenRefreshSkew > now)
        {
            return current.AccessToken;
        }

        if (current.RefreshToken is null || (current.RefreshExpiresAt is { } refreshExpires && refreshExpires <= now))
        {
            this.tokens.Remove(principalKey, providerName);
            return null;
        }

        ProviderEndpoints? endpoints = await this.ResolveEndpointsAsync(provider, cancellationToken).ConfigureAwait(false);
        ProviderToken? refreshed = endpoints is { } resolved
            ? await this.ExchangeAsync(provider, resolved.TokenEndpoint, ("refresh_token", current.RefreshToken), cancellationToken).ConfigureAwait(false)
            : null;
        if (refreshed is not { } fresh)
        {
            this.tokens.Remove(principalKey, providerName);
            return null;
        }

        this.tokens.Set(principalKey, providerName, fresh);
        return fresh.AccessToken;
    }

    private static bool HostMatches(string pattern, string host)
        => pattern.StartsWith("*.", StringComparison.Ordinal)
            ? host.Length > pattern.Length - 1 && host.AsSpan().EndsWith(pattern.AsSpan(1), StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase);

    // The provider's authorize/token endpoints: explicit configuration, or resolved once from the
    // OIDC issuer's discovery document and cached for the broker's lifetime. Null when discovery
    // fails (the caller maps it to a typed outcome; the next attempt retries).
    private async ValueTask<ProviderEndpoints?> ResolveEndpointsAsync(ConnectedProviderOptions provider, CancellationToken cancellationToken)
    {
        if (provider.Issuer is null)
        {
            return new ProviderEndpoints(provider.AuthorizeEndpoint!, provider.TokenEndpoint!);
        }

        if (this.discovered.TryGetValue(provider.Name!, out ProviderEndpoints cached))
        {
            return cached;
        }

        string discoveryUrl = $"{provider.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
        using var request = new HttpRequestMessage(HttpMethod.Get, discoveryUrl);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "arazzo-control-plane");
        HttpResponseMessage response;
        try
        {
            response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            this.logger?.LogWarning(ex, "Provider '{Provider}' discovery could not reach {Endpoint}.", provider.Name, discoveryUrl);
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogWarning("Provider '{Provider}' discovery returned {StatusCode} from {Endpoint}.", provider.Name, (int)response.StatusCode, discoveryUrl);
                return null;
            }

            byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("authorization_endpoint"u8, out JsonElement authorize) || authorize.GetString() is not { Length: > 0 } authorizeEndpoint
                || !root.TryGetProperty("token_endpoint"u8, out JsonElement token) || token.GetString() is not { Length: > 0 } tokenEndpoint)
            {
                this.logger?.LogWarning("Provider '{Provider}' discovery document names no authorization_endpoint/token_endpoint.", provider.Name);
                return null;
            }

            var endpoints = new ProviderEndpoints(authorizeEndpoint, tokenEndpoint);
            this.discovered[provider.Name!] = endpoints;
            return endpoints;
        }
    }

    // One standard OAuth 2.0 exchange against the provider's token endpoint: an authorization code
    // (code) or a refresh grant (refresh_token). The provider's client secret resolves here and is
    // scrubbed after the call. GitHub's endpoint accepts this exact shape (Accept: application/json
    // selects its JSON response; the extra grant_type/redirect_uri are tolerated), so the folded
    // github entry needs no special case.
    private async ValueTask<ProviderToken?> ExchangeAsync(ConnectedProviderOptions provider, string tokenEndpoint, (string Name, string Value) grant, CancellationToken cancellationToken)
    {
        using SecretMaterial secret = await this.secrets.ResolveAsync(SecretRef.Parse(provider.ClientSecretRef!), cancellationToken).ConfigureAwait(false);
        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", provider.ClientId!),
            new("client_secret", secret.Reveal()),
            new(grant.Name, grant.Value),
        };
        if (grant.Name == "refresh_token")
        {
            form.Add(new("grant_type", "refresh_token"));
        }
        else
        {
            form.Add(new("grant_type", "authorization_code"));
            form.Add(new("redirect_uri", provider.CallbackUrl!));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "arazzo-control-plane");
        HttpResponseMessage? response;
        try
        {
            response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // A transport-level failure reaching the provider (DNS, proxy, TLS) is an exchange
            // FAILURE, not an unhandled 500: the caller maps a null to the typed problem, so the
            // operator sees an actionable message instead of a blank error.
            this.logger?.LogWarning(ex, "Provider '{Provider}' token exchange could not reach {Endpoint}.", provider.Name, tokenEndpoint);
            return null;
        }

        using (response)
        {
            byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogExchangeRefusal(this.logger, provider.Name!, (int)response.StatusCode, payload);
                return null;
            }

            using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("access_token"u8, out JsonElement accessToken) || accessToken.GetString() is not { Length: > 0 } access)
            {
                // GitHub reports a refused exchange as 200 with an error body; name the error
                // fields (codes and doc links — never secret material; a success body is never logged).
                LogExchangeRefusal(this.logger, provider.Name!, (int)response.StatusCode, payload);
                return null;
            }

            DateTimeOffset now = this.timeProvider.GetUtcNow();
            DateTimeOffset? expiresAt = root.TryGetProperty("expires_in"u8, out JsonElement expiresIn) && expiresIn.ValueKind == JsonValueKind.Number
                ? now + TimeSpan.FromSeconds(expiresIn.GetInt32())
                : null;
            string? refresh = root.TryGetProperty("refresh_token"u8, out JsonElement refreshToken) ? refreshToken.GetString() : null;

            // The refresh lifetime rides as refresh_expires_in (Keycloak/OIDC) or
            // refresh_token_expires_in (GitHub); read either.
            JsonElement refreshIn = default;
            bool hasRefreshIn = (root.TryGetProperty("refresh_expires_in"u8, out refreshIn) || root.TryGetProperty("refresh_token_expires_in"u8, out refreshIn))
                && refreshIn.ValueKind == JsonValueKind.Number;
            DateTimeOffset? refreshExpiresAt = hasRefreshIn ? now + TimeSpan.FromSeconds(refreshIn.GetInt32()) : null;
            return new ProviderToken(access, expiresAt, refresh, refreshExpiresAt);
        }
    }

    // A refused exchange names the provider's error code and description in the log — the
    // difference between "invalid_client" and an unreachable endpoint matters to the operator. The
    // payload here is a REFUSAL body (error codes and doc links); a success body is never logged.
    private static void LogExchangeRefusal(ILogger? logger, string provider, int statusCode, byte[] payload)
    {
        if (logger is null)
        {
            return;
        }

        string error = "(none)";
        string description = string.Empty;
        try
        {
            using var document = ParsedJsonDocument<JsonElement>.Parse(payload);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                error = root.TryGetProperty("error"u8, out JsonElement e) ? e.GetString() ?? "(none)" : "(none)";
                description = root.TryGetProperty("error_description"u8, out JsonElement ed) ? ed.GetString() ?? string.Empty : string.Empty;
            }
        }
        catch (JsonException)
        {
            // A non-JSON refusal body: the status code alone is the diagnostic.
        }

        logger.LogWarning("Provider '{Provider}' refused the token exchange ({StatusCode}): {Error} {Description}", provider, statusCode, error, description);
    }

    private void PrunePending()
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, PendingAuth> entry in this.pending)
        {
            if (entry.Value.ExpiresAt < now)
            {
                this.pending.TryRemove(entry.Key, out _);
            }
        }
    }

    private readonly record struct PendingAuth(string PrincipalKey, string Provider, DateTimeOffset ExpiresAt);

    private readonly record struct ProviderEndpoints(string AuthorizeEndpoint, string TokenEndpoint);
}

/// <summary>
/// One connected provider a deployment registers (ADR 0052): the OAuth/OIDC client registration
/// the platform holds there, and the hosts the provider's user identity covers. Adding an SSO'd
/// internal portal is one of these entries — configuration, not code.
/// </summary>
public sealed class ConnectedProviderOptions
{
    /// <summary>Gets the provider's stable name (the API's <c>{provider}</c> path segment).</summary>
    public string? Name { get; init; }

    /// <summary>Gets the human-readable name the pane shows; the <see cref="Name"/> when absent.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the OIDC issuer; the authorize/token endpoints resolve from its discovery
    /// document. Exactly one of the issuer or the explicit endpoint pair must be configured.</summary>
    public string? Issuer { get; init; }

    /// <summary>Gets the explicit authorize endpoint, for a provider without OIDC discovery.</summary>
    public string? AuthorizeEndpoint { get; init; }

    /// <summary>Gets the explicit token endpoint, for a provider without OIDC discovery.</summary>
    public string? TokenEndpoint { get; init; }

    /// <summary>Gets the OAuth client id the deployment registered with the provider.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the secret REFERENCE (e.g. <c>env://PORTAL_OAUTH_SECRET</c>) the deployment's
    /// resolver dereferences at exchange time; the secret itself is never configured or held.</summary>
    public string? ClientSecretRef { get; init; }

    /// <summary>Gets the space-separated scopes the sign-in requests (e.g. <c>openid profile</c>).</summary>
    public string? Scopes { get; init; }

    /// <summary>Gets the deployment's externally visible callback URL for this provider — it must
    /// exactly match a redirect URI in the provider-side client registration.</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>Gets the hosts this provider's user identity covers: exact names or
    /// <c>*.suffix</c> patterns. The fetch path attaches the user's token only to a covered host.</summary>
    public IReadOnlyList<string> Hosts { get; init; } = [];

    /// <summary>Throws when a required field is missing or the endpoint configuration is ambiguous.</summary>
    /// <exception cref="ArgumentException">The entry is not a valid provider registration.</exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(this.Name) || string.IsNullOrEmpty(this.ClientId) || string.IsNullOrEmpty(this.ClientSecretRef) || string.IsNullOrEmpty(this.CallbackUrl))
        {
            throw new ArgumentException($"A connected provider requires Name, ClientId, ClientSecretRef, and CallbackUrl (provider '{this.Name}').");
        }

        bool hasIssuer = !string.IsNullOrEmpty(this.Issuer);
        bool hasEndpoints = !string.IsNullOrEmpty(this.AuthorizeEndpoint) && !string.IsNullOrEmpty(this.TokenEndpoint);
        if (hasIssuer == hasEndpoints)
        {
            throw new ArgumentException($"A connected provider requires exactly one of Issuer (OIDC discovery) or AuthorizeEndpoint + TokenEndpoint (provider '{this.Name}').");
        }

        if (this.Hosts.Count == 0)
        {
            throw new ArgumentException($"A connected provider requires at least one hosts pattern — with no coverage its connection could never authenticate a fetch (provider '{this.Name}').");
        }
    }
}

/// <summary>One principal's brokered user token for one provider.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="ExpiresAt">When the access token expires (absent: non-expiring).</param>
/// <param name="RefreshToken">The refresh token, when the provider issues expiring tokens.</param>
/// <param name="RefreshExpiresAt">When the refresh token expires.</param>
public readonly record struct ProviderToken(string AccessToken, DateTimeOffset? ExpiresAt, string? RefreshToken, DateTimeOffset? RefreshExpiresAt);

/// <summary>
/// Per-(principal, provider) custody for brokered user tokens (ADR 0052): one principal's token is
/// unreachable from any other principal's session. The default is in-memory (session-lifetime); a
/// deployment may substitute an encrypted-at-rest store (KMS ref).
/// </summary>
public interface IProviderTokenStore
{
    /// <summary>Gets the principal's token for the provider, if any.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    /// <param name="provider">The provider name.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    ProviderToken? Get(string principalKey, string provider);

    /// <summary>Stores (replaces) the principal's token for the provider.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="token">The token.</param>
    void Set(string principalKey, string provider, ProviderToken token);

    /// <summary>Removes the principal's token for the provider. Idempotent.</summary>
    /// <param name="principalKey">The principal's stable key.</param>
    /// <param name="provider">The provider name.</param>
    void Remove(string principalKey, string provider);
}

/// <summary>The in-memory (session-lifetime) <see cref="IProviderTokenStore"/>.</summary>
public sealed class InMemoryProviderTokenStore : IProviderTokenStore
{
    private readonly ConcurrentDictionary<(string PrincipalKey, string Provider), ProviderToken> tokens = new();

    /// <inheritdoc/>
    public ProviderToken? Get(string principalKey, string provider) => this.tokens.TryGetValue((principalKey, provider), out ProviderToken token) ? token : null;

    /// <inheritdoc/>
    public void Set(string principalKey, string provider, ProviderToken token) => this.tokens[(principalKey, provider)] = token;

    /// <inheritdoc/>
    public void Remove(string principalKey, string provider) => this.tokens.TryRemove((principalKey, provider), out _);
}