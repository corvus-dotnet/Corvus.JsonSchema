// <copyright file="GooglePrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Directories.Google;

/// <summary>
/// A Google Workspace <see cref="IPrincipalDirectory"/> (design §16.5.4): searches a customer's users / groups / roles
/// over the <strong>Google Admin SDK Directory API</strong> and projects each record to its deployment-stamped <c>sys:</c>
/// identity via the supplied <see cref="IDirectoryIdentityMapper"/>. It authenticates with a service account using
/// domain-wide delegation (configured via <see cref="GoogleAuthentication"/>): it signs a JWT with the service account's
/// private key — a <c>SecretRef</c> resolved through the deployment's <see cref="ISecretResolver"/>, never stored — and
/// exchanges it for an access token.
/// </summary>
/// <remarks>
/// The access token is fetched once and cached until just before expiry; the fetch is single-flight. Responses are parsed
/// with the Corvus <see cref="Utf8JsonReader"/> (no <c>System.Text.Json</c>). When the supplied mapper is a span mapper the
/// identity is built bytes-to-bytes (<see cref="ProjectResponseSpan"/>); otherwise the string path applies. The
/// <see cref="HttpClient"/> may be supplied by the caller (who then owns its lifetime); when omitted, the directory owns a
/// default client and disposes it.
/// </remarks>
public sealed class GooglePrincipalDirectory : IPrincipalDirectory, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromSeconds(60);

    private readonly GoogleDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private volatile CachedToken? cachedToken;

    /// <summary>Initializes a new instance of the <see cref="GooglePrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret endpoint + customer + auth + schema configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the service-account private key's <c>SecretRef</c>.</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    /// <param name="httpClient">An HTTP client; when <see langword="null"/> the directory owns a default one.</param>
    /// <param name="timeProvider">The time source for token expiry / JWT timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public GooglePrincipalDirectory(GoogleDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper, HttpClient? httpClient = null, TimeProvider? timeProvider = null)
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
        if (!this.options.Kinds.TryGetValue(kind, out GoogleResource? resource))
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
            throw new GoogleDirectoryException($"the Directory API returned {(int)response.StatusCode} ({response.StatusCode}) searching {resource.Path}.");
        }

        // Allocation ledger (per search). The response byte[] is the one driver-forced GC alloc; the parse reads it IN
        // PLACE with the Corvus reader — no JsonDocument / STJ DOM, no second copy. With a span mapper the identity is built
        // bytes-to-bytes (only the per-principal value/label strings + the one identity byte[] escape); otherwise the string
        // path applies. The token path is cold (once per lifetime, single-flight) and the warm path is a volatile read.
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

    // The leaf of a Google attribute path (after the last '.') — the key the adapter flattens (and the span mapper reads).
    private static string Leaf(string name)
    {
        int cut = name.LastIndexOf('.');
        return cut < 0 ? name : name[(cut + 1)..];
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

    // Advances a reader positioned at an object's StartObject to the start of the named array property's array (consuming
    // its StartArray); returns false if the property is absent or not an array. Other members are skipped.
    private static bool SeekResults(ref Utf8JsonReader reader, byte[] property)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(property);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.StartArray;
            }

            reader.Skip();
        }

        return false;
    }

    // The bytes-to-bytes path (used when the mapper is a span mapper): capture the wanted attributes — value, label, and the
    // mapper's declared attributes — by the LEAF of each name (a top-level scalar by its name, e.g. `primaryEmail` /
    // `orgUnitPath`, and one level into an object by its member name, e.g. `name.fullName` → `fullName`) as unescaped UTF-8
    // into a pooled scratch, then project span-wise with no attribute string.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponseSpan(GranteeKind kind, GoogleResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);

        string[] required = [.. projector.RequiredAttributes];
        int wantedCount = 1 + (resource.DisplayField is null ? 0 : 1) + required.Length;
        byte[][] wanted = new byte[wantedCount][];
        int next = 0;
        int valueWanted = next;
        wanted[next++] = Encoding.UTF8.GetBytes(Leaf(resource.ValueField));
        int displayWanted = -1;
        if (resource.DisplayField is { } displayField)
        {
            displayWanted = next;
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(displayField));
        }

        foreach (string attribute in required)
        {
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(attribute));
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject || !SeekResults(ref reader, Encoding.UTF8.GetBytes(resource.ResultsProperty)))
        {
            return results;
        }

        byte[] scratch = ArrayPool<byte>.Shared.Rent(body.Length);
        Span<DirectoryAttributeSlice> slices = stackalloc DirectoryAttributeSlice[wantedCount];
        try
        {
            while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                int captured = 0;
                int position = 0;
                int valueSlice = -1;
                int displaySlice = -1;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    int which = MatchWanted(ref reader, wanted);
                    reader.Read();
                    if (which >= 0 && reader.TokenType == JsonTokenType.String)
                    {
                        CaptureScalar(ref reader, wanted[which], scratch, ref position, slices, ref captured);
                        if (which == valueWanted)
                        {
                            valueSlice = captured - 1;
                        }
                        else if (which == displayWanted)
                        {
                            displaySlice = captured - 1;
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                        {
                            int member = MatchWanted(ref reader, wanted);
                            reader.Read();
                            if (member >= 0 && reader.TokenType == JsonTokenType.String)
                            {
                                CaptureScalar(ref reader, wanted[member], scratch, ref position, slices, ref captured);
                                if (member == valueWanted)
                                {
                                    valueSlice = captured - 1;
                                }
                                else if (member == displayWanted)
                                {
                                    displaySlice = captured - 1;
                                }
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

                // The display label as unescaped UTF-8: the display attribute's own scratch span, or the value span when
                // none — no managed string. TryProjectIdentity copies it into the principal before the next row reuses scratch.
                ReadOnlySpan<byte> labelSpan = displaySlice >= 0
                    ? scratch.AsSpan(slices[displaySlice].ValueOffset, slices[displaySlice].ValueLength)
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
        }

        return results;
    }

    // The string path (used when the mapper is a string mapper): flatten each entity (scalars by name, a nested object
    // dotted) into a record's attribute map and apply the string mapper.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, GoogleResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject || !SeekResults(ref reader, Encoding.UTF8.GetBytes(resource.ResultsProperty)))
        {
            return results;
        }

        while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            Dictionary<string, IReadOnlyList<string>> attributes = ReadEntity(ref reader);
            if (Project(kind, resource, attributes, projector) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results;
    }

    private static ResolvedPrincipal? Project(GranteeKind kind, GoogleResource resource, Dictionary<string, IReadOnlyList<string>> attributes, DirectoryPrincipalProjector projector)
    {
        string? value = First(attributes, resource.ValueField) ?? First(attributes, Leaf(resource.ValueField));
        if (value is null)
        {
            return null;
        }

        string? display = (resource.DisplayField is { } configured ? First(attributes, configured) ?? First(attributes, Leaf(configured)) : null) ?? value;
        return projector.Project(new DirectoryRecord(kind, value, display, attributes, []));
    }

    private static string? First(Dictionary<string, IReadOnlyList<string>> attributes, string key)
        => attributes.TryGetValue(key, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null;

    private static Dictionary<string, IReadOnlyList<string>> ReadEntity(ref Utf8JsonReader reader)
    {
        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                attributes[name] = [reader.GetString()!];
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string sub = reader.GetString()!;
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        attributes[$"{name}.{sub}"] = [reader.GetString()!];
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

        return attributes;
    }

    private static (string AccessToken, TimeSpan Lifetime) ParseTokenResponse(ReadOnlySpan<byte> body)
    {
        string? accessToken = null;
        TimeSpan lifetime = DefaultTokenLifetime;
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new GoogleDirectoryException("the token response was not a JSON object.");
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
            throw new GoogleDirectoryException("the token response did not contain an access_token.");
        }

        return (accessToken, lifetime);
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    private Uri BuildSearchUri(GoogleResource resource, string query, int max)
    {
        // The collection hangs off the Directory API base path, so append rather than root-replace. The value-prefix search
        // is the Directory `query` syntax (`<field>:<prefix>*`); an empty query omits it (list the collection). The user
        // query is URL-encoded inside the parameter, so it can never alter the request.
        string root = this.options.DirectoryBaseUrl.AbsoluteUri.TrimEnd('/');
        string path = resource.Path.Trim('/');
        string url = $"{root}/{path}?customer={Uri.EscapeDataString(this.options.Customer)}&maxResults={max}";
        if (query.Length > 0)
        {
            url += $"&query={Uri.EscapeDataString($"{resource.QueryField}:{query}*")}";
        }

        return new Uri(url, UriKind.Absolute);
    }

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
        if (this.options.Authentication is not GoogleServiceAccount account)
        {
            throw new InvalidOperationException($"Unsupported Google authentication '{this.options.Authentication.GetType().Name}'.");
        }

        DateTimeOffset requestedAt = this.timeProvider.GetUtcNow();
        string assertion = await this.BuildAssertionAsync(account, requestedAt.ToUnixTimeSeconds(), cancellationToken).ConfigureAwait(false);
        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new("assertion", assertion),
        ]);

        using var request = new HttpRequestMessage(HttpMethod.Post, this.options.TokenEndpoint) { Content = content };
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleDirectoryException($"the token endpoint returned {(int)response.StatusCode} ({response.StatusCode}).");
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        (string accessToken, TimeSpan lifetime) = ParseTokenResponse(body);
        return new CachedToken(accessToken, requestedAt + lifetime);
    }

    // Builds and signs the RS256 service-account assertion (a JWT) for the OAuth 2.0 jwt-bearer grant. The private key is
    // resolved from its SecretRef, imported, used to sign, and dropped — never stored (the §13 boundary).
    private async ValueTask<string> BuildAssertionAsync(GoogleServiceAccount account, long issuedAt, CancellationToken cancellationToken)
    {
        long expiresAt = issuedAt + 3600;
        string claimsJson = BuildClaims(account.ClientEmail, this.options.Scope, this.options.TokenEndpoint.AbsoluteUri, account.Subject, issuedAt, expiresAt);
        string signingInput = $"{Base64Url("""{"alg":"RS256","typ":"JWT"}"""u8)}.{Base64Url(Encoding.UTF8.GetBytes(claimsJson))}";

        string pem = await account.PrivateKey.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false);
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (ArgumentException e)
        {
            throw new GoogleDirectoryException("the service-account private key could not be imported.", e);
        }

        byte[] signature = rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string BuildClaims(string issuer, string scope, string audience, string subject, long issuedAt, long expiresAt)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("iss"u8, issuer);
            writer.WriteString("scope"u8, scope);
            writer.WriteString("aud"u8, audience);
            writer.WriteString("sub"u8, subject);
            writer.WriteNumber("iat"u8, issuedAt);
            writer.WriteNumber("exp"u8, expiresAt);
            writer.WriteEndObject();
            writer.Flush();
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}