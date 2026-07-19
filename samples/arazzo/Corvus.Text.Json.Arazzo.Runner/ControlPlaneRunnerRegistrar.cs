// <copyright file="ControlPlaneRunnerRegistrar.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Runner;

/// <summary>
/// Registers this runner with the control plane through its authenticated HTTP API (design §5.5/§16.4). The runner
/// authenticates as its machine principal — a Keycloak client-credentials client (<c>arazzo-runner</c>) — and POSTs its
/// self-description to <c>POST /environments/{environment}/runners</c>. The control plane derives the trusted principal from
/// the presented token and binds the runner's authorization to it, rather than the runner self-asserting a Pending row
/// straight into the shared store. The control plane stamps the environment, the reach tags (from the environment's
/// managementTags), and the last-seen instant server-side, so the runner never supplies its own reach.
/// </summary>
/// <remarks>
/// This is the §16.4 hardening of registration only. Heartbeat, run claiming, and dispatch remain store-direct (the
/// store-as-queue residual §5.5): what changes is that the runner's identity is now proven by an authenticated token, not
/// self-asserted. A client-credentials access token is short-lived, so a token is acquired per registration attempt.
/// </remarks>
public sealed class ControlPlaneRunnerRegistrar
{
    private readonly HttpClient http;
    private readonly Uri registerEndpoint;
    private readonly Uri tokenEndpoint;
    private readonly string clientId;
    private readonly string clientSecret;

    /// <summary>Initializes a new instance of the <see cref="ControlPlaneRunnerRegistrar"/> class.</summary>
    /// <param name="http">The HTTP client used for both the token request and the registration POST.</param>
    /// <param name="controlPlaneBaseUrl">The control plane's base URL (the Aspire-injected <c>controlplane</c> endpoint).</param>
    /// <param name="environment">The single deployment environment this runner serves (the registration path segment).</param>
    /// <param name="tokenEndpoint">The Keycloak token endpoint for the arazzo realm.</param>
    /// <param name="clientId">The runner's machine-principal client id (its <c>azp</c> — the bound principal).</param>
    /// <param name="clientSecret">The client-credentials secret.</param>
    public ControlPlaneRunnerRegistrar(HttpClient http, string controlPlaneBaseUrl, string environment, string tokenEndpoint, string clientId, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(controlPlaneBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        this.http = http;
        // The control-plane API is mounted under /arazzo/v1 (the same prefix the CLI and designer call).
        this.registerEndpoint = new Uri(new Uri(controlPlaneBaseUrl.EndsWith('/') ? controlPlaneBaseUrl : controlPlaneBaseUrl + "/"), $"arazzo/v1/environments/{Uri.EscapeDataString(environment)}/runners");
        this.tokenEndpoint = new Uri(tokenEndpoint);
        this.clientId = clientId;
        this.clientSecret = clientSecret;
    }

    /// <summary>Acquires a client-credentials token and registers the runner, returning the authorization status the control
    /// plane reports (for example <c>Pending</c> or, if an administrator has already authorized it, <c>Authorized</c>).</summary>
    /// <param name="registrationBody">The <c>RunnerRegistrationRequest</c> JSON (the runner's self-description).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The authorization status the control plane reports.</returns>
    /// <exception cref="InvalidOperationException">The registration was refused (for example a 409 principal conflict, or a 404 for an unknown environment).</exception>
    public async Task<string> RegisterAsync(ReadOnlyMemory<byte> registrationBody, CancellationToken cancellationToken)
    {
        string token = await this.AcquireTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, this.registerEndpoint)
        {
            Content = new ReadOnlyMemoryContent(registrationBody),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Runner registration was refused ({(int)response.StatusCode} {response.StatusCode}): {body}");
        }

        using Stj.JsonDocument view = Stj.JsonDocument.Parse(body);
        return view.RootElement.TryGetProperty("status", out Stj.JsonElement status) ? status.GetString() ?? "Pending" : "Pending";
    }

    private async Task<string> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, this.tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = this.clientId,
                ["client_secret"] = this.clientSecret,
            }),
        };

        using HttpResponseMessage response = await this.http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Acquiring the runner's client-credentials token failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
        }

        using Stj.JsonDocument token = Stj.JsonDocument.Parse(body);
        return token.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The token response carried no access_token.");
    }

    /// <summary>Builds the Keycloak token endpoint URL for a realm from the server base URL.</summary>
    /// <param name="keycloakBaseUrl">The Keycloak base URL.</param>
    /// <param name="realm">The realm name.</param>
    /// <returns>The realm's OpenID Connect token endpoint URL.</returns>
    public static string TokenEndpointFor(string keycloakBaseUrl, string realm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        string trimmed = keycloakBaseUrl.TrimEnd('/');
        return $"{trimmed}/realms/{Uri.EscapeDataString(realm)}/protocol/openid-connect/token";
    }
}
