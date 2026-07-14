// <copyright file="EntraIdPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Directories.EntraId;

/// <summary>
/// A Microsoft Entra ID <see cref="IPrincipalDirectory"/> (design §16.5.4): searches a tenant's users / groups /
/// directory roles over the <strong>Microsoft Graph API</strong> with a <c>startsWith</c> prefix filter and projects each
/// record to its deployment-stamped <c>sys:</c> identity via the supplied <see cref="IDirectoryIdentityMapper"/>. It
/// authenticates with an OAuth 2.0 client-credentials grant (configured via <see cref="EntraIdAuthentication"/>) whose
/// client secret is a <c>SecretRef</c> resolved through the deployment's <see cref="ISecretResolver"/> — never stored.
/// </summary>
/// <remarks>
/// The access token is fetched once and cached until just before expiry; the fetch is single-flight (concurrent callers
/// await one in-flight request). Responses are parsed with the Corvus <see cref="Utf8JsonReader"/> (no
/// <c>System.Text.Json</c>) straight into <see cref="DirectoryRecord"/>s. When the mapper declares the attributes it reads
/// the adapter requests only those via Graph <c>$select</c>. The <see cref="HttpClient"/> may be supplied by the caller
/// (who then owns its lifetime); when omitted, the directory owns a default client and disposes it.
/// </remarks>
public sealed class EntraIdPrincipalDirectory : IPrincipalDirectory, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromSeconds(60);

    // The membership cache is pruned of expired entries only once it grows past this many distinct users, bounding its size
    // for a large directory without paying an O(n) sweep on the common (small, warm) path.
    private const int MembershipCachePruneThreshold = 1024;

    private readonly EntraIdDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private readonly ConcurrentDictionary<string, CachedGroups> membershipCache = new(StringComparer.Ordinal);
    private volatile CachedToken? cachedToken;

    /// <summary>Initializes a new instance of the <see cref="EntraIdPrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret endpoint + tenant + auth + schema configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the auth credential's <c>SecretRef</c>.</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    /// <param name="httpClient">An HTTP client; when <see langword="null"/> the directory owns a default one.</param>
    /// <param name="timeProvider">The time source for token expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public EntraIdPrincipalDirectory(EntraIdDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper, HttpClient? httpClient = null, TimeProvider? timeProvider = null)
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
        if (!this.options.Kinds.TryGetValue(kind, out EntraIdResource? resource))
        {
            return [];
        }

        int top = limit > 0 ? limit : 1;
        string token = await this.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(this.options.GraphBaseUrl, resource, query, top, this.projector.RequiredAttributes));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new EntraIdDirectoryException($"Graph returned {(int)response.StatusCode} ({response.StatusCode}) searching {resource.Path}.");
        }

        // Allocation ledger (per search). The response byte[] is the one driver-forced GC alloc (HttpContent gives no
        // pooled-read that fits); ProjectResponse then reads it IN PLACE with the Corvus Utf8JsonReader — no JsonDocument
        // / STJ DOM, no second copy. The only other GC-escaping allocations are the API contract's: the returned List +
        // each ResolvedPrincipal (its Value/Label strings + stamped SecurityTagSet). When the mapper declares its
        // RequiredAttributes the $select makes Graph return only those, so both the response and the flatten shrink.
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // A person resolves to its FULL membership-expanded identity (design §16.5.4): capture each user's object id during
        // the projection (Graph returns id on every entity), then fetch its group memberships (cached per user) and union a
        // sys:group per group through the SAME mapper. Teams / roles ARE the memberships, so they are not themselves expanded.
        if (kind == GranteeKind.Person)
        {
            var ids = new List<string>(top);
            IReadOnlyList<ResolvedPrincipal> people = this.projector.SupportsSpanProjection
                ? ProjectResponseSpan(kind, resource, body, top, this.projector, ids)
                : ProjectResponse(kind, resource, body, top, this.projector, ids);
            return await this.ExpandMembershipsAsync(people, ids, token, cancellationToken).ConfigureAwait(false);
        }

        return this.projector.SupportsSpanProjection
            ? ProjectResponseSpan(kind, resource, body, top, this.projector)
            : ProjectResponse(kind, resource, body, top, this.projector);
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

    private static Uri BuildSearchUri(Uri graphBaseUrl, EntraIdResource resource, string query, int top, IReadOnlyCollection<string> requiredAttributes)
    {
        // The Graph collection hangs off the API base path (e.g. https://graph.microsoft.com/v1.0 + users), so append
        // rather than root-replace. The value-prefix search is `$filter=startsWith(attr,'q')`; an empty query omits the
        // filter (list the collection). The user query is single-quote-escaped inside the OData literal and the whole
        // $filter is URL-encoded, so it can never break out of the expression.
        string root = graphBaseUrl.AbsoluteUri.TrimEnd('/');
        string path = resource.Path.Trim('/');
        string url = $"{root}/{path}?$top={top}";
        if (query.Length > 0)
        {
            string filter = $"startsWith({resource.FilterAttribute},'{EscapeODataLiteral(query)}')";
            url += $"&$filter={Uri.EscapeDataString(filter)}";
        }

        // Attribute projection (the §16.5.4 seam): when the mapper declares what it reads, ask Graph (via $select) for only
        // those plus the value/label attributes the adapter needs, so the entity comes back smaller. When the mapper
        // declares nothing the parameter is omitted and Graph returns its default property set (the safe, general default).
        if (requiredAttributes.Count > 0)
        {
            string select = string.Join(",", ProjectionTokens(resource, requiredAttributes).Select(Uri.EscapeDataString));
            url += $"&$select={select}";
        }

        return new Uri(url, UriKind.Absolute);
    }

    private static string EscapeODataLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    // The Graph properties to request: the value attribute and (if any) the display attribute the adapter needs, unioned
    // with the mapper's declared attributes — order-preserved and de-duplicated for a stable request.
    private static IEnumerable<string> ProjectionTokens(EntraIdResource resource, IReadOnlyCollection<string> requiredAttributes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(requiredAttributes.Count + 2);
        Append(resource.FilterAttribute);
        if (resource.DisplayAttribute is { } display)
        {
            Append(display);
        }

        foreach (string attribute in requiredAttributes)
        {
            Append(attribute);
        }

        return ordered;

        void Append(string token)
        {
            if (seen.Add(token))
            {
                ordered.Add(token);
            }
        }
    }

    // Projects a Graph collection response ({ "value": [ ... ] }) to resolved principals. A pure function of (bytes,
    // resource, projector) — the reader borrows `body` in place (no DOM, no copy). A dropped record (mapper returns null,
    // or one missing its value attribute) does not consume the limit. `internal` only so the allocation benchmark can
    // drive it without a network.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, EntraIdResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector, List<string>? capturedIds = null)
    {
        var results = new List<ResolvedPrincipal>(limit);
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return results;
        }

        // Forward-scan for the OData "value" array; other envelope members (@odata.context, @odata.nextLink, @odata.count)
        // are skipped in whatever order they arrive.
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isValue = reader.ValueTextEquals("value"u8);
            reader.Read();
            if (isValue && reader.TokenType == JsonTokenType.StartArray)
            {
                while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                {
                    Dictionary<string, IReadOnlyList<string>> attributes = ReadEntity(ref reader);
                    if (Project(kind, resource, attributes, projector) is { } principal)
                    {
                        results.Add(principal);
                        capturedIds?.Add(First(attributes, "id") ?? string.Empty);
                    }
                }

                return results;
            }

            reader.Skip();
        }

        return results;
    }

    // The bytes-to-bytes path (used when the mapper is a span mapper): capture only the wanted attributes — the value, the
    // label, and the mapper's declared attributes — as unescaped UTF-8 into a pooled scratch (reused per entity), build a
    // stack-only DirectoryRecordView, and let the projector write the identity straight into a pooled buffer. No attribute
    // string is materialized; only the per-principal value/label (which ResolvedPrincipal requires) and the one identity
    // byte[] escape. The Graph entity is flat, so a wanted property is matched by name directly.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponseSpan(GranteeKind kind, EntraIdResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector, List<string>? capturedIds = null)
    {
        var results = new List<ResolvedPrincipal>(limit);

        // When the caller will expand a person's memberships it also needs the user's object id (the id /users/{id}/memberOf
        // keys on), captured in step with the principal so the two lists stay parallel.
        bool captureId = capturedIds is not null;

        // The wanted attribute names as UTF-8 (value first, then label, then the mapper's declared attributes, then the id
        // when captured), built once.
        string[] required = [.. projector.RequiredAttributes];
        int wantedCount = 1 + (resource.DisplayAttribute is null ? 0 : 1) + required.Length + (captureId ? 1 : 0);
        byte[][] wanted = new byte[wantedCount][];
        int next = 0;
        int valueWanted = next;
        wanted[next++] = Encoding.UTF8.GetBytes(resource.FilterAttribute);
        int displayWanted = -1;
        if (resource.DisplayAttribute is { } displayAttribute)
        {
            displayWanted = next;
            wanted[next++] = Encoding.UTF8.GetBytes(displayAttribute);
        }

        foreach (string attribute in required)
        {
            wanted[next++] = Encoding.UTF8.GetBytes(attribute);
        }

        int idWanted = -1;
        if (captureId)
        {
            idWanted = next;
            wanted[next++] = "id"u8.ToArray();
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return results;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isValue = reader.ValueTextEquals("value"u8);
            reader.Read();
            if (!isValue || reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                continue;
            }

            // The scratch (sized to the body, since unescaped bytes never exceed their escaped source) is reused per
            // entity — each view is consumed by TryProjectIdentity before the next entity overwrites it.
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
                    int idSlice = -1;
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        int which = -1;
                        for (int i = 0; i < wantedCount; i++)
                        {
                            if (reader.ValueTextEquals(wanted[i]))
                            {
                                which = i;
                                break;
                            }
                        }

                        reader.Read();
                        if (which < 0 || reader.TokenType != JsonTokenType.String)
                        {
                            reader.Skip();
                            continue;
                        }

                        int keyOffset = position;
                        wanted[which].CopyTo(scratch.AsSpan(position));
                        position += wanted[which].Length;
                        int valueOffset = position;
                        int valueLength = reader.CopyString(scratch.AsSpan(position));
                        position += valueLength;
                        if (which == valueWanted)
                        {
                            valueSlice = captured;
                        }
                        else if (which == displayWanted)
                        {
                            displaySlice = captured;
                        }
                        else if (which == idWanted)
                        {
                            idSlice = captured;
                        }

                        slices[captured++] = new DirectoryAttributeSlice(keyOffset, wanted[which].Length, valueOffset, valueLength);
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
                        capturedIds?.Add(idSlice >= 0 ? Encoding.UTF8.GetString(scratch, slices[idSlice].ValueOffset, slices[idSlice].ValueLength) : string.Empty);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scratch);
            }

            return results;
        }

        return results;
    }

    // Expands a page of resolved people to their full membership identity (design §16.5.4): fetch each person's group names
    // (cached per id, all concurrent under the one token so a page of N costs one round-trip of latency, not N), then fold a
    // sys:group per group into the identity through the SAME mapper. A person whose id is missing is unexpanded.
    private async ValueTask<IReadOnlyList<ResolvedPrincipal>> ExpandMembershipsAsync(IReadOnlyList<ResolvedPrincipal> people, List<string> ids, string token, CancellationToken cancellationToken)
    {
        if (people.Count == 0)
        {
            return people;
        }

        var fetches = new Task<IReadOnlyList<string>>[people.Count];
        for (int i = 0; i < people.Count; i++)
        {
            fetches[i] = this.GetGroupNamesAsync(ids[i], token, cancellationToken);
        }

        await Task.WhenAll(fetches).ConfigureAwait(false);

        var expanded = new ResolvedPrincipal[people.Count];
        for (int i = 0; i < people.Count; i++)
        {
            expanded[i] = this.projector.EnrichWithMemberships(people[i], fetches[i].Result, null);
        }

        return expanded;
    }

    // A person's group names, served from the per-user TTL cache when warm or fetched once and cached otherwise. Staleness is
    // safe: the directory identity feeds the picker / read view only, never a live request (see MembershipCacheTtl).
    private async Task<IReadOnlyList<string>> GetGroupNamesAsync(string id, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return [];
        }

        if (this.membershipCache.TryGetValue(id, out CachedGroups cached) && this.timeProvider.GetUtcNow() < cached.ExpiresAt)
        {
            return cached.Names;
        }

        IReadOnlyList<string> names = await this.FetchGroupNamesAsync(id, token, cancellationToken).ConfigureAwait(false);

        if (this.options.MembershipCacheTtl > TimeSpan.Zero)
        {
            this.membershipCache[id] = new CachedGroups(names, this.timeProvider.GetUtcNow() + this.options.MembershipCacheTtl);
            this.PruneExpiredMemberships();
        }

        return names;
    }

    // GET {GraphBaseUrl}/users/{id}/memberOf → { value: [{ @odata.type, displayName }, …] } (groups, directory roles, admin
    // units). Only group entries (@odata.type == #microsoft.graph.group) contribute a sys:group; their identity value is the
    // displayName. Throws on a non-success status so a misconfigured app permission surfaces.
    private async Task<IReadOnlyList<string>> FetchGroupNamesAsync(string id, string token, CancellationToken cancellationToken)
    {
        string root = this.options.GraphBaseUrl.AbsoluteUri.TrimEnd('/');
        var uri = new Uri($"{root}/users/{Uri.EscapeDataString(id)}/memberOf?$select=id,displayName", UriKind.Absolute);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new EntraIdDirectoryException($"Graph returned {(int)response.StatusCode} ({response.StatusCode}) fetching group memberships for a user.");
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return ParseGroupNames(body);
    }

    // Reads a Graph memberOf response ({ value: [{ @odata.type, displayName }, …] }) to the list of GROUP displayNames in
    // place — a directoryRole / administrativeUnit member is skipped (its @odata.type is not a group), so only real group
    // memberships become sys:group tags. `internal` only so the parse can be unit-tested.
    internal static IReadOnlyList<string> ParseGroupNames(byte[] body)
    {
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return [];
        }

        List<string>? names = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isValue = reader.ValueTextEquals("value"u8);
            reader.Read();
            if (!isValue || reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                continue;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                string? displayName = null;
                bool isGroup = false;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("@odata.type"u8))
                    {
                        reader.Read();
                        isGroup = reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("#microsoft.graph.group"u8);
                    }
                    else if (reader.ValueTextEquals("displayName"u8))
                    {
                        reader.Read();
                        displayName = reader.GetString();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                if (isGroup && !string.IsNullOrEmpty(displayName))
                {
                    (names ??= []).Add(displayName);
                }
            }

            return names ?? (IReadOnlyList<string>)[];
        }

        return names ?? (IReadOnlyList<string>)[];
    }

    // Bounds the membership cache: once it exceeds the prune threshold, drop the entries that have already expired (the warm
    // path never pays this — the check short-circuits on Count). TTL drains the rest.
    private void PruneExpiredMemberships()
    {
        if (this.membershipCache.Count <= MembershipCachePruneThreshold)
        {
            return;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, CachedGroups> entry in this.membershipCache)
        {
            if (now >= entry.Value.ExpiresAt)
            {
                this.membershipCache.TryRemove(entry.Key, out _);
            }
        }
    }

    private static ResolvedPrincipal? Project(GranteeKind kind, EntraIdResource resource, Dictionary<string, IReadOnlyList<string>> attributes, DirectoryPrincipalProjector projector)
    {
        string? value = First(attributes, resource.FilterAttribute);
        if (value is null)
        {
            return null;
        }

        string? display = (resource.DisplayAttribute is { } configured ? First(attributes, configured) : null)
            ?? First(attributes, "displayName")
            ?? value;

        return projector.Project(new DirectoryRecord(kind, value, display, attributes, []));
    }

    private static string? First(Dictionary<string, IReadOnlyList<string>> attributes, string key)
        => attributes.TryGetValue(key, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null;

    // Flattens one Graph entity object (reader at its StartObject) into a string attribute map the mapper reads by name.
    // Graph entities are mostly flat scalars; a complex property (e.g. onPremisesExtensionAttributes) flattens dotted, and
    // a scalar array (e.g. businessPhones) becomes the list of its values. Numbers and nulls are not surfaced (a sys: tag
    // is always a string); booleans surface as "true"/"false". Leaves the reader at the matching EndObject.
    private static Dictionary<string, IReadOnlyList<string>> ReadEntity(ref Utf8JsonReader reader)
    {
        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();

            // OData annotations (@odata.* on the entity) are protocol metadata, never identity — skip them.
            if (name.StartsWith('@'))
            {
                reader.Skip();
                continue;
            }

            FlattenValue(ref reader, name, attributes);
        }

        return attributes;
    }

    private static void FlattenValue(ref Utf8JsonReader reader, string key, Dictionary<string, IReadOnlyList<string>> attributes)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                Add(attributes, key, reader.GetString());
                break;
            case JsonTokenType.True:
                Add(attributes, key, "true");
                break;
            case JsonTokenType.False:
                Add(attributes, key, "false");
                break;
            case JsonTokenType.StartObject:
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string sub = reader.GetString()!;
                    reader.Read();
                    FlattenValue(ref reader, $"{key}.{sub}", attributes);
                }

                break;
            case JsonTokenType.StartArray:
                var values = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } scalar)
                    {
                        values.Add(scalar);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (values.Count > 0)
                {
                    attributes[key] = values;
                }

                break;
            default:
                // Number / Null — nothing to surface; the scalar token is already consumed.
                break;
        }
    }

    private static void Add(Dictionary<string, IReadOnlyList<string>> attributes, string key, string? value)
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
            throw new EntraIdDirectoryException("the token response was not a JSON object.");
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
            throw new EntraIdDirectoryException("the token response did not contain an access_token.");
        }

        return (accessToken, lifetime);
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
        if (this.options.Authentication is not EntraIdClientCredentials clientCredentials)
        {
            throw new InvalidOperationException($"Unsupported Entra ID authentication '{this.options.Authentication.GetType().Name}'.");
        }

        var form = new List<KeyValuePair<string, string>>(4)
        {
            new("grant_type", "client_credentials"),
            new("client_id", clientCredentials.ClientId),
            new("scope", this.options.Scope),
            new("client_secret", await clientCredentials.ClientSecret.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false)),
        };

        string tenant = Uri.EscapeDataString(this.options.TenantId);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(this.options.LoginBaseUrl, $"/{tenant}/oauth2/v2.0/token"))
        {
            Content = new FormUrlEncodedContent(form),
        };

        DateTimeOffset requestedAt = this.timeProvider.GetUtcNow();
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new EntraIdDirectoryException($"the identity platform token endpoint returned {(int)response.StatusCode} ({response.StatusCode}).");
        }

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        (string accessToken, TimeSpan lifetime) = ParseTokenResponse(body);
        return new CachedToken(accessToken, requestedAt + lifetime);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    // A user's cached group names + when the entry goes stale. A struct so a cache hit is a value read with no per-entry heap
    // allocation (the names list is shared, not copied).
    private readonly record struct CachedGroups(IReadOnlyList<string> Names, DateTimeOffset ExpiresAt);
}