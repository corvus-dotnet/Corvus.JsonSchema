// <copyright file="KeycloakPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak;

/// <summary>
/// A Keycloak <see cref="IPrincipalDirectory"/> (design §16.5.4): searches a realm's users / groups / roles over the
/// Keycloak <strong>Admin REST API</strong> and projects each record to its deployment-stamped <c>sys:</c> identity via
/// the supplied <see cref="IDirectoryIdentityMapper"/>. It authenticates with a client-credentials or password grant
/// (configured via <see cref="KeycloakAuthentication"/>) whose secret is a <c>SecretRef</c> resolved through the
/// deployment's <see cref="ISecretResolver"/> — never stored.
/// </summary>
/// <remarks>
/// The admin access token is fetched once and cached until just before expiry; the fetch is single-flight (concurrent
/// callers await one in-flight request). Responses are parsed with the Corvus <see cref="Utf8JsonReader"/> (no
/// <c>System.Text.Json</c>) straight into <see cref="DirectoryRecord"/>s. The <see cref="HttpClient"/> may be supplied by
/// the caller (who then owns its lifetime); when omitted, the directory owns a default client and disposes it.
/// </remarks>
public sealed class KeycloakPrincipalDirectory : IPrincipalDirectory, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromSeconds(60);

    private readonly KeycloakDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private volatile CachedToken? cachedToken;

    /// <summary>Initializes a new instance of the <see cref="KeycloakPrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret server + realm + auth + schema configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the auth credential's <c>SecretRef</c>(s).</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    /// <param name="httpClient">An HTTP client; when <see langword="null"/> the directory owns a default one.</param>
    /// <param name="timeProvider">The time source for token expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public KeycloakPrincipalDirectory(KeycloakDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper, HttpClient? httpClient = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(mapper);
        this.options = options;
        this.resolver = resolver;

        // Every resolved principal is funnelled through the projector, so the configured issuer is stamped onto each
        // identity (mapper-immutable) — the adapter cannot return a principal without its sys:iss.
        this.projector = new DirectoryPrincipalProjector(mapper, options.Issuer);
        this.ownsHttpClient = httpClient is null;
        this.httpClient = httpClient ?? new HttpClient();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!this.options.Kinds.TryGetValue(kind, out KeycloakResource resource))
        {
            return [];
        }

        int max = limit > 0 ? limit : 1;
        string token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, this.BuildSearchUri(resource, query, max));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new KeycloakDirectoryException($"the Keycloak Admin API returned {(int)response.StatusCode} ({response.StatusCode}) searching {resource}.");
        }

        // Allocation ledger (per search). The response byte[] is the one driver-forced GC alloc (HttpContent gives no
        // pooled-read that fits); ProjectResponse then reads it IN PLACE with the Corvus Utf8JsonReader — no JsonDocument
        // / STJ DOM, no second copy of the body. The only other GC-escaping allocations are the API contract's: the
        // returned List + each ResolvedPrincipal (its Value/Label strings + stamped SecurityTagSet). Per-row DirectoryRecord
        // + attribute Dictionary + transient GetString scalars are short-lived scratch the mapper contract forces.
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ProjectResponse(kind, resource, body, max, this.projector);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.tokenGate.Dispose();
        if (this.ownsHttpClient)
        {
            this.httpClient.Dispose();
        }
    }

    private Uri BuildSearchUri(KeycloakResource resource, string query, int max)
    {
        string realm = Uri.EscapeDataString(this.options.Realm);
        string resourcePath = resource switch
        {
            KeycloakResource.Users => "users",
            KeycloakResource.Groups => "groups",
            KeycloakResource.Roles => "roles",
            _ => throw new InvalidOperationException($"Unsupported Keycloak resource '{resource}'."),
        };

        // briefRepresentation=false on users so the attributes (the mapper's identity source) come back; an empty query
        // omits `search` (list all up to max). The user-supplied query is URL-encoded, so it can never alter the path.
        string search = query.Length == 0 ? string.Empty : $"&search={Uri.EscapeDataString(query)}";
        string brief = resource == KeycloakResource.Users ? "&briefRepresentation=false" : string.Empty;
        return new Uri(this.options.BaseUrl, $"/admin/realms/{realm}/{resourcePath}?max={max}{search}{brief}");
    }

    // Projects a Keycloak Admin list response (a JSON array) to resolved principals. A pure function of (bytes, projector)
    // — the reader borrows `body` in place (no DOM, no copy); a dropped record (mapper returns null) does not consume the
    // limit, so a kind whose backing resource carries unrecognised entries (e.g. Keycloak's built-in realm roles) still
    // yields the recognised ones up to `limit`. `internal` only so the allocation benchmark can drive it without a network.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, KeycloakResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return results;
        }

        while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            DirectoryRecord? record = resource == KeycloakResource.Users
                ? ReadUser(ref reader, kind)
                : ReadNamed(ref reader, kind);
            if (record is { } parsed && projector.Project(parsed) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results;
    }

    // Reads one user object (reader positioned at its StartObject) into a record keyed on the username, with the user's
    // own attributes plus id/email/firstName/lastName flattened in for the mapper. Leaves the reader at the EndObject.
    private static DirectoryRecord? ReadUser(ref Utf8JsonReader reader, GranteeKind kind)
    {
        string? username = null, firstName = null, lastName = null, email = null, id = null;
        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("username"u8))
            {
                reader.Read();
                username = reader.GetString();
            }
            else if (reader.ValueTextEquals("firstName"u8))
            {
                reader.Read();
                firstName = reader.GetString();
            }
            else if (reader.ValueTextEquals("lastName"u8))
            {
                reader.Read();
                lastName = reader.GetString();
            }
            else if (reader.ValueTextEquals("email"u8))
            {
                reader.Read();
                email = reader.GetString();
            }
            else if (reader.ValueTextEquals("id"u8))
            {
                reader.Read();
                id = reader.GetString();
            }
            else if (reader.ValueTextEquals("attributes"u8))
            {
                reader.Read();
                ReadAttributes(ref reader, attributes);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (username is null)
        {
            return null;
        }

        AddScalar(attributes, "id", id);
        AddScalar(attributes, "email", email);
        AddScalar(attributes, "firstName", firstName);
        AddScalar(attributes, "lastName", lastName);
        string? display = (firstName, lastName) switch
        {
            (not null, not null) => $"{firstName} {lastName}",
            (not null, null) => firstName,
            (null, not null) => lastName,
            _ => email,
        };

        return new DirectoryRecord(kind, username, display, attributes, []);
    }

    // Reads one group/role object (name + id) — both are name-keyed with no nested attributes. Leaves the reader at the EndObject.
    private static DirectoryRecord? ReadNamed(ref Utf8JsonReader reader, GranteeKind kind)
    {
        string? name = null, id = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("name"u8))
            {
                reader.Read();
                name = reader.GetString();
            }
            else if (reader.ValueTextEquals("id"u8))
            {
                reader.Read();
                id = reader.GetString();
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (name is null)
        {
            return null;
        }

        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        AddScalar(attributes, "id", id);
        return new DirectoryRecord(kind, name, name, attributes, []);
    }

    // Flattens a Keycloak user's `attributes` object ({ "tenant": ["acme"], … }) into the record's attribute map. The
    // reader is positioned at the attributes value; on a non-object (e.g. null) it skips and the map is unchanged.
    private static void ReadAttributes(ref Utf8JsonReader reader, Dictionary<string, IReadOnlyList<string>> attributes)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var values = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
                    {
                        values.Add(value);
                    }
                }

                attributes[key] = values;
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void AddScalar(Dictionary<string, IReadOnlyList<string>> attributes, string key, string? value)
    {
        if (value is not null)
        {
            attributes[key] = [value];
        }
    }

    // Parses { "access_token": "...", "expires_in": N, ... } with the Corvus reader (no System.Text.Json). A missing
    // expires_in falls back to the default lifetime.
    private static (string AccessToken, TimeSpan Lifetime) ParseTokenResponse(ReadOnlySpan<byte> body)
    {
        string? accessToken = null;
        TimeSpan lifetime = DefaultTokenLifetime;
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new KeycloakDirectoryException("the token response was not a JSON object.");
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
            throw new KeycloakDirectoryException("the token response did not contain an access_token.");
        }

        return (accessToken, lifetime);
    }

    // Allocation ledger (token). The warm path — a valid cached token — is a volatile read + IsStale compare, zero alloc.
    // A cold fetch (once per token lifetime, single-flight behind the gate so concurrent searches share one) allocates the
    // form + FormUrlEncodedContent + response byte[] (all transient/disposed), the revealed secret string (dropped after
    // the POST per the §13 reveal-then-drop contract, never logged/cached), and the access_token + CachedToken that escape
    // into the cache (amortised across every search until expiry). ParseTokenResponse reads the body in place — no DOM.
    private async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        CachedToken? token = this.cachedToken;
        if (token is not null && !this.IsStale(token))
        {
            return token.AccessToken;
        }

        await this.tokenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            token = this.cachedToken;
            if (token is not null && !this.IsStale(token))
            {
                return token.AccessToken;
            }

            CachedToken fresh = await this.FetchTokenAsync(cancellationToken).ConfigureAwait(false);
            this.cachedToken = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            this.tokenGate.Release();
        }
    }

    private bool IsStale(CachedToken token) => this.timeProvider.GetUtcNow() >= token.ExpiresAt - RefreshSkew;

    private async ValueTask<CachedToken> FetchTokenAsync(CancellationToken cancellationToken)
    {
        string tokenRealm = Uri.EscapeDataString(this.options.TokenRealm ?? this.options.Realm);
        var form = new List<KeyValuePair<string, string>>(5);
        switch (this.options.Authentication)
        {
            case KeycloakClientCredentials clientCredentials:
                form.Add(new("grant_type", "client_credentials"));
                form.Add(new("client_id", clientCredentials.ClientId));
                form.Add(new("client_secret", await clientCredentials.ClientSecret.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false)));
                break;

            case KeycloakPasswordGrant passwordGrant:
                form.Add(new("grant_type", "password"));
                form.Add(new("client_id", passwordGrant.ClientId));
                form.Add(new("username", passwordGrant.Username));
                form.Add(new("password", await passwordGrant.Password.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false)));
                if (passwordGrant.ClientSecret is { } clientSecret)
                {
                    form.Add(new("client_secret", await clientSecret.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false)));
                }

                break;

            default:
                throw new InvalidOperationException($"Unsupported Keycloak authentication '{this.options.Authentication.GetType().Name}'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(this.options.BaseUrl, $"/realms/{tokenRealm}/protocol/openid-connect/token"))
        {
            Content = new FormUrlEncodedContent(form),
        };

        DateTimeOffset requestedAt = this.timeProvider.GetUtcNow();
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new KeycloakDirectoryException($"the Keycloak token endpoint returned {(int)response.StatusCode} ({response.StatusCode}).");
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        (string accessToken, TimeSpan lifetime) = ParseTokenResponse(body);
        return new CachedToken(accessToken, requestedAt + lifetime);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}