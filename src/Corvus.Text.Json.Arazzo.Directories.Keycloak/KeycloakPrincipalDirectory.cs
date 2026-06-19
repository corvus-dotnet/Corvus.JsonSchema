// <copyright file="KeycloakPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
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
        return this.projector.SupportsSpanProjection
            ? ProjectResponseSpan(kind, resource, body, max, this.projector)
            : ProjectResponse(kind, resource, body, max, this.projector);
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

    // The bytes-to-bytes path (used when the mapper is a span mapper). Keycloak is the most bespoke shape: there is no
    // configurable value/label attribute — a user is keyed on its top-level `username` with a display computed from
    // `firstName`/`lastName`, a group/role on its top-level `name`; and a user's custom attributes are ARRAYS under an
    // `attributes` object. So this captures the value (+ firstName/lastName for users) from the top level and the mapper's
    // declared custom attributes from the `attributes` object's first array element — all as unescaped UTF-8 into a pooled
    // scratch, with no attribute string.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponseSpan(GranteeKind kind, KeycloakResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);
        bool isUser = resource == KeycloakResource.Users;

        // Wanted UTF-8 names: the value (username / name), then firstName + lastName for a user's computed display, then
        // the mapper's declared custom attributes (flat names, found in the `attributes` object).
        string[] required = [.. projector.RequiredAttributes];
        int wantedCount = (isUser ? 3 : 1) + required.Length;
        byte[][] wanted = new byte[wantedCount][];
        int next = 0;
        int valueWanted = next;
        wanted[next++] = isUser ? "username"u8.ToArray() : "name"u8.ToArray();
        int firstNameWanted = -1;
        int lastNameWanted = -1;
        if (isUser)
        {
            firstNameWanted = next;
            wanted[next++] = "firstName"u8.ToArray();
            lastNameWanted = next;
            wanted[next++] = "lastName"u8.ToArray();
        }

        foreach (string attribute in required)
        {
            wanted[next++] = Encoding.UTF8.GetBytes(attribute);
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return results;
        }

        byte[] scratch = ArrayPool<byte>.Shared.Rent(body.Length);
        Span<DirectoryAttributeSlice> slices = stackalloc DirectoryAttributeSlice[wantedCount];

        // A reusable scratch buffer for an assembled "first last" display label (rented lazily, grown if a name pair
        // exceeds it, reused across rows since TryProjectIdentity copies the label span before the next row writes it).
        byte[]? labelBuffer = null;
        try
        {
            while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                int captured = 0;
                int position = 0;
                int valueSlice = -1;
                int firstSlice = -1;
                int lastSlice = -1;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    int which = MatchWanted(ref reader, wanted);
                    reader.Read();
                    if (which >= 0 && reader.TokenType == JsonTokenType.String)
                    {
                        CaptureScalar(ref reader, wanted[which], scratch, ref position, slices, ref captured);
                        int index = captured - 1;
                        if (which == valueWanted)
                        {
                            valueSlice = index;
                        }
                        else if (which == firstNameWanted)
                        {
                            firstSlice = index;
                        }
                        else if (which == lastNameWanted)
                        {
                            lastSlice = index;
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        // The `attributes` object: each member is an array of strings; capture the first of each wanted one.
                        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                        {
                            int member = MatchWanted(ref reader, wanted);
                            reader.Read();
                            if (member >= 0 && reader.TokenType == JsonTokenType.StartArray)
                            {
                                CaptureFirstArrayString(ref reader, wanted[member], scratch, ref position, slices, ref captured);
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (valueSlice < 0)
                {
                    continue;
                }

                DirectoryAttributeSlice value = slices[valueSlice];
                ReadOnlySpan<byte> valueSpan = scratch.AsSpan(value.ValueOffset, value.ValueLength);

                // The display label, bytes-to-bytes: a single name attribute is its own scratch span; a "first last"
                // display is assembled into a reusable pooled buffer (no managed string); otherwise it falls back to the
                // value span. The label span is copied into the principal by TryProjectIdentity before the next row reuses
                // the buffer.
                ReadOnlySpan<byte> labelSpan = isUser
                    ? DisplaySpan(scratch, slices, firstSlice, lastSlice, valueSpan, ref labelBuffer)
                    : valueSpan;

                var view = new DirectoryRecordView(kind, valueSpan, scratch, slices[..captured]);
                if (projector.TryProjectIdentity(kind, valueSpan, labelSpan, hasLabel: true, view) is { } principal)
                {
                    results.Add(principal);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
            if (labelBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(labelBuffer);
            }
        }

        return results;
    }

    private static int MatchWanted(scoped ref Utf8JsonReader reader, byte[][] wanted)
    {
        for (int i = 0; i < wanted.Length; i++)
        {
            if (reader.ValueTextEquals(wanted[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static void CaptureScalar(scoped ref Utf8JsonReader reader, byte[] key, byte[] scratch, ref int position, scoped Span<DirectoryAttributeSlice> slices, ref int captured)
    {
        int keyOffset = position;
        key.CopyTo(scratch.AsSpan(position));
        position += key.Length;
        int valueOffset = position;
        int valueLength = reader.CopyString(scratch.AsSpan(position));
        position += valueLength;
        slices[captured++] = new DirectoryAttributeSlice(keyOffset, key.Length, valueOffset, valueLength);
    }

    // Captures the first string element of a Keycloak attribute array (reader at its StartArray), skipping the rest; leaves
    // the reader at the matching EndArray.
    private static void CaptureFirstArrayString(scoped ref Utf8JsonReader reader, byte[] key, byte[] scratch, ref int position, scoped Span<DirectoryAttributeSlice> slices, ref int captured)
    {
        bool taken = false;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (!taken && reader.TokenType == JsonTokenType.String)
            {
                CaptureScalar(ref reader, key, scratch, ref position, slices, ref captured);
                taken = true;
            }
            else
            {
                reader.Skip();
            }
        }
    }

    // The user display computed from the captured firstName/lastName spans as unescaped UTF-8 ("first last", or whichever
    // is present), or the value span when neither name is present — no managed string. A "first last" pair is assembled
    // into the reusable pooled label buffer (grown on demand); a single name is its own scratch span (zero copy).
    private static ReadOnlySpan<byte> DisplaySpan(byte[] scratch, scoped Span<DirectoryAttributeSlice> slices, int firstSlice, int lastSlice, ReadOnlySpan<byte> value, ref byte[]? labelBuffer)
    {
        if (firstSlice < 0 && lastSlice < 0)
        {
            return value;
        }

        if (firstSlice >= 0 && lastSlice >= 0)
        {
            DirectoryAttributeSlice f = slices[firstSlice];
            DirectoryAttributeSlice l = slices[lastSlice];
            int needed = f.ValueLength + 1 + l.ValueLength;
            if (labelBuffer is null || labelBuffer.Length < needed)
            {
                if (labelBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(labelBuffer);
                }

                labelBuffer = ArrayPool<byte>.Shared.Rent(needed);
            }

            scratch.AsSpan(f.ValueOffset, f.ValueLength).CopyTo(labelBuffer);
            labelBuffer[f.ValueLength] = (byte)' ';
            scratch.AsSpan(l.ValueOffset, l.ValueLength).CopyTo(labelBuffer.AsSpan(f.ValueLength + 1));
            return labelBuffer.AsSpan(0, needed);
        }

        DirectoryAttributeSlice present = firstSlice >= 0 ? slices[firstSlice] : slices[lastSlice];
        return scratch.AsSpan(present.ValueOffset, present.ValueLength);
    }

    // Projects a Keycloak Admin list response (a JSON array) to resolved principals. A pure function of (bytes, projector)
    // — the reader borrows `body` in place (no DOM, no copy); a dropped record (mapper returns null) does not consume the
    // limit, so a kind whose backing resource carries unrecognised entries (e.g. Keycloak's built-in realm roles) still
    // yields the recognised ones up to `limit`. `internal` only so the allocation benchmark can drive it without a network.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, KeycloakResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);

        // Attribute-projection seam (§16.5.4), parse-side. Keycloak's Admin API has no server-side field projection (the
        // user representation is all-or-nothing under briefRepresentation=false), so when the mapper declares what it reads
        // the adapter honours it on parse: only the declared custom attributes are flattened (the unbounded source — a user
        // may carry many HR attributes the mapper never reads). The fixed user fields (username/firstName/lastName/email/id)
        // are always surfaced — they are bounded and the value/label need them. No declaration => flatten every attribute.
        HashSet<string>? customAttributeFilter = projector.RequiredAttributes.Count > 0
            ? new HashSet<string>(projector.RequiredAttributes, StringComparer.OrdinalIgnoreCase)
            : null;

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return results;
        }

        while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            DirectoryRecord? record = resource == KeycloakResource.Users
                ? ReadUser(ref reader, kind, customAttributeFilter)
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
    private static DirectoryRecord? ReadUser(ref Utf8JsonReader reader, GranteeKind kind, HashSet<string>? customAttributeFilter)
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
                ReadAttributes(ref reader, attributes, customAttributeFilter);
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
    // reader is positioned at the attributes value; on a non-object (e.g. null) it skips and the map is unchanged. When
    // `filter` is non-null (the mapper declared what it reads) only those custom attributes are materialised; a key the
    // mapper never reads is skipped without allocating its value list.
    private static void ReadAttributes(ref Utf8JsonReader reader, Dictionary<string, IReadOnlyList<string>> attributes, HashSet<string>? filter)
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
            if (filter?.Contains(key) == false)
            {
                reader.Skip();
                continue;
            }

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