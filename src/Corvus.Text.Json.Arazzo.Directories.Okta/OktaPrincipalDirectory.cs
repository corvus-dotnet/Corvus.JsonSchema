// <copyright file="OktaPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Directories.Okta;

/// <summary>
/// An Okta <see cref="IPrincipalDirectory"/> (design §16.5.4): searches an org's users / groups / roles over the
/// <strong>Okta Management API</strong> with a <c>search</c> prefix expression and projects each record to its
/// deployment-stamped <c>sys:</c> identity via the supplied <see cref="IDirectoryIdentityMapper"/>. It authenticates with
/// an SSWS API token (configured via <see cref="OktaAuthentication"/>) whose value is a <c>SecretRef</c> resolved through
/// the deployment's <see cref="ISecretResolver"/> — never stored.
/// </summary>
/// <remarks>
/// Responses are parsed with the Corvus <see cref="Utf8JsonReader"/> (no <c>System.Text.Json</c>) straight into
/// <see cref="DirectoryRecord"/>s; an Okta user's <c>profile</c> attributes are flattened (<c>profile.login</c>,
/// <c>profile.firstName</c>, …) so a deployment mapper reads them by path. Okta has no server-side field projection, so
/// when the mapper declares the attributes it reads the adapter honours it on parse (only those — plus the value/label
/// attributes — are materialised). The token is the secret, so it is resolved per search and dropped, never cached. The
/// <see cref="HttpClient"/> may be supplied by the caller (who then owns its lifetime); when omitted, the directory owns a
/// default client and disposes it.
/// </remarks>
public sealed class OktaPrincipalDirectory : IPrincipalDirectory, IDisposable
{
    private readonly OktaDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;

    /// <summary>Initializes a new instance of the <see cref="OktaPrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret endpoint + auth + resource configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the API token's <c>SecretRef</c>.</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    /// <param name="httpClient">An HTTP client; when <see langword="null"/> the directory owns a default one.</param>
    public OktaPrincipalDirectory(OktaDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper, HttpClient? httpClient = null)
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
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!this.options.Kinds.TryGetValue(kind, out OktaResource? resource))
        {
            return [];
        }

        int pageLimit = limit > 0 ? limit : 1;
        string token = await this.ResolveTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(this.options.BaseUrl, resource, query, pageLimit));
        request.Headers.Authorization = new AuthenticationHeaderValue("SSWS", token);
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new OktaDirectoryException($"the Management API returned {(int)response.StatusCode} ({response.StatusCode}) searching {resource.Path}.");
        }

        // Allocation ledger (per search). The response byte[] is the one driver-forced GC alloc (HttpContent gives no
        // pooled-read that fits); ProjectResponse then reads it IN PLACE with the Corvus Utf8JsonReader — no JsonDocument
        // / STJ DOM, no second copy. The only other GC-escaping allocations are the API contract's: the returned List +
        // each ResolvedPrincipal (its Value/Label strings + stamped SecurityTagSet). Okta has no wire projection, so when
        // the mapper declares its RequiredAttributes the parse keeps only those (plus value/label) — skipping the value
        // list + dictionary entry for every profile attribute the mapper never reads.
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return this.projector.SupportsSpanProjection
            ? ProjectResponseSpan(kind, resource, body, pageLimit, this.projector)
            : ProjectResponse(kind, resource, body, pageLimit, this.projector);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.ownsHttpClient)
        {
            this.httpClient.Dispose();
        }
    }

    private static Uri BuildSearchUri(Uri baseUrl, OktaResource resource, string query, int limit)
    {
        // Okta resources hang off the org's /api/v1 path, so append rather than root-replace. The value-prefix search is
        // an Okta `search` expression (`<attr> sw "q"`); an empty query omits it (list the resource). The user query is
        // quote-/backslash-escaped inside the expression literal and the whole expression is URL-encoded, so it can never
        // break out of it.
        string root = baseUrl.AbsoluteUri.TrimEnd('/');
        string path = resource.Path.Trim('/');
        string url = $"{root}/api/v1/{path}?limit={limit}";
        if (query.Length > 0)
        {
            string search = $"{resource.FilterAttribute} sw \"{EscapeSearchLiteral(query)}\"";
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        return new Uri(url, UriKind.Absolute);
    }

    private static string EscapeSearchLiteral(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    // The bytes-to-bytes path (used when the mapper is a span mapper): capture the wanted attributes — value, label, and the
    // mapper's declared attributes — as unescaped UTF-8 into a pooled scratch, then project span-wise with no attribute
    // string. Okta nests its identity attributes under `profile`, so a wanted attribute is matched (and captured) by the
    // LEAF of its name — a top-level scalar by its name (a role's `label`) and one level into an object by its member name
    // (so `profile.login` / `profile.department` are captured under `login` / `department`, which the span mapper reads).
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponseSpan(GranteeKind kind, OktaResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);

        string[] required = [.. projector.RequiredAttributes];
        int wantedCount = 1 + (resource.DisplayAttribute is null ? 0 : 1) + required.Length;
        byte[][] wanted = new byte[wantedCount][];
        int next = 0;
        int valueWanted = next;
        wanted[next++] = Encoding.UTF8.GetBytes(Leaf(resource.FilterAttribute));
        int displayWanted = -1;
        if (resource.DisplayAttribute is { } displayAttribute)
        {
            displayWanted = next;
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(displayAttribute));
        }

        foreach (string attribute in required)
        {
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(attribute));
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read())
        {
            return results;
        }

        if (resource.ResultsProperty is { } property)
        {
            if (reader.TokenType != JsonTokenType.StartObject || !SeekProperty(ref reader, property))
            {
                return results;
            }
        }
        else if (reader.TokenType != JsonTokenType.StartArray)
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

    // The leaf of an Okta attribute path — the part after the last `.` (the `profile` member name), the key the span mapper
    // reads. A bare name (a role's top-level `label`) is its own leaf.
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

    // Projects an Okta list response to resolved principals. A pure function of (bytes, resource, projector) — the reader
    // borrows `body` in place (no DOM, no copy). Users / groups are a bare top-level array; a resource with a
    // ResultsProperty (e.g. custom roles) wraps its array in that property. A dropped record (mapper returns null, or one
    // missing its value attribute) does not consume the limit. `internal` only so the allocation benchmark can drive it.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, OktaResource resource, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);

        // Parse-side projection (the §16.5.4 seam): Okta has no server-side $select, so when the mapper declares what it
        // reads only those keys (plus the value/label paths the adapter needs) are materialised; an undeclared mapper
        // surfaces every attribute (the safe, general default).
        HashSet<string>? keep = null;
        if (projector.RequiredAttributes.Count > 0)
        {
            keep = new HashSet<string>(projector.RequiredAttributes, StringComparer.OrdinalIgnoreCase) { resource.FilterAttribute };
            if (resource.DisplayAttribute is { } display)
            {
                keep.Add(display);
            }
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read())
        {
            return results;
        }

        if (resource.ResultsProperty is { } property)
        {
            if (reader.TokenType != JsonTokenType.StartObject || !SeekProperty(ref reader, property))
            {
                return results;
            }
        }
        else if (reader.TokenType != JsonTokenType.StartArray)
        {
            return results;
        }

        while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            Dictionary<string, IReadOnlyList<string>> attributes = ReadEntity(ref reader, keep);
            if (Project(kind, resource, attributes, projector) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results;
    }

    // Advances a reader positioned at an object's StartObject to the start of the named array property's array (consuming
    // its StartArray); returns false if the property is absent or not an array. Other members are skipped.
    private static bool SeekProperty(ref Utf8JsonReader reader, string property)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = string.Equals(reader.GetString(), property, StringComparison.Ordinal);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.StartArray;
            }

            reader.Skip();
        }

        return false;
    }

    private static ResolvedPrincipal? Project(GranteeKind kind, OktaResource resource, Dictionary<string, IReadOnlyList<string>> attributes, DirectoryPrincipalProjector projector)
    {
        string? value = First(attributes, resource.FilterAttribute);
        if (value is null)
        {
            return null;
        }

        string? display = (resource.DisplayAttribute is { } configured ? First(attributes, configured) : null) ?? value;
        return projector.Project(new DirectoryRecord(kind, value, display, attributes, []));
    }

    private static string? First(Dictionary<string, IReadOnlyList<string>> attributes, string key)
        => attributes.TryGetValue(key, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null;

    // Flattens one Okta entity object (reader at its StartObject) into a string attribute map the mapper reads by path: a
    // nested object (e.g. `profile`) flattens dotted (`profile.login`), a scalar array becomes the list of its values.
    // Numbers / nulls are not surfaced (a sys: tag is always a string); booleans surface as "true"/"false". When `keep` is
    // non-null only those keys are materialised. Leaves the reader at the matching EndObject.
    private static Dictionary<string, IReadOnlyList<string>> ReadEntity(ref Utf8JsonReader reader, HashSet<string>? keep)
    {
        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string name = reader.GetString()!;
            reader.Read();
            FlattenValue(ref reader, name, keep, attributes);
        }

        return attributes;
    }

    private static void FlattenValue(ref Utf8JsonReader reader, string key, HashSet<string>? keep, Dictionary<string, IReadOnlyList<string>> attributes)
    {
        switch (reader.TokenType)
        {
            // The keep-check precedes GetString so a filtered scalar never materialises its value string (only the key,
            // needed to test the filter, is allocated) — the parse-side cost of an attribute the mapper never reads.
            case JsonTokenType.String:
                if (keep?.Contains(key) != false)
                {
                    attributes[key] = [reader.GetString()!];
                }

                break;
            case JsonTokenType.True:
                if (keep?.Contains(key) != false)
                {
                    attributes[key] = ["true"];
                }

                break;
            case JsonTokenType.False:
                if (keep?.Contains(key) != false)
                {
                    attributes[key] = ["false"];
                }

                break;
            case JsonTokenType.StartObject:
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string sub = reader.GetString()!;
                    reader.Read();
                    FlattenValue(ref reader, $"{key}.{sub}", keep, attributes);
                }

                break;
            case JsonTokenType.StartArray:
                FlattenArray(ref reader, key, keep, attributes);
                break;
            default:
                // Number / Null — nothing to surface; the scalar token is already consumed.
                break;
        }
    }

    private static void FlattenArray(ref Utf8JsonReader reader, string key, HashSet<string>? keep, Dictionary<string, IReadOnlyList<string>> attributes)
    {
        // A key the mapper never reads is skipped without allocating its value list.
        if (keep?.Contains(key) == false)
        {
            reader.Skip();
            return;
        }

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
    }

    private async ValueTask<string> ResolveTokenAsync(CancellationToken cancellationToken)
    {
        // The SSWS token IS the secret (not a derived access token), so per the §13 boundary it is resolved from its
        // SecretRef at the point of use and dropped immediately — never cached. Any caching policy belongs to the
        // deployment's ISecretResolver.
        return this.options.Authentication switch
        {
            OktaApiToken apiToken => await apiToken.Token.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Okta authentication '{this.options.Authentication.GetType().Name}'."),
        };
    }
}