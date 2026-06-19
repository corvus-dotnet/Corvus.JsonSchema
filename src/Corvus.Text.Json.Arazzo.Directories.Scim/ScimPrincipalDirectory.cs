// <copyright file="ScimPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Directories.Scim;

/// <summary>
/// A SCIM 2.0 <see cref="IPrincipalDirectory"/> (design §16.5.4): searches a service provider's resources (users / groups
/// / roles) with a SCIM filter over the <strong>SCIM 2.0 protocol</strong> (RFC 7644) and projects each record to its
/// deployment-stamped <c>sys:</c> identity via the supplied <see cref="IDirectoryIdentityMapper"/>. It authenticates with
/// a bearer token whose value is a <c>SecretRef</c> resolved through the deployment's <see cref="ISecretResolver"/> —
/// never stored.
/// </summary>
/// <remarks>
/// Responses are parsed with the Corvus <see cref="Utf8JsonReader"/> (no <c>System.Text.Json</c>) straight into
/// <see cref="DirectoryRecord"/>s; SCIM's structured resources (the complex <c>name</c>, multi-valued <c>emails</c>, and
/// extension schemas such as the enterprise extension) are flattened into the record's attribute map so a deployment
/// mapper reads them by name (<c>userName</c>, <c>name.givenName</c>, <c>emails</c>, <c>organization</c>). The bearer
/// token is long-lived secret material, so it is resolved at the point of each search and dropped immediately (it is
/// never cached). The <see cref="HttpClient"/> may be supplied by the caller (who then owns its lifetime); when omitted,
/// the directory owns a default client and disposes it.
/// </remarks>
public sealed class ScimPrincipalDirectory : IPrincipalDirectory, IDisposable
{
    private readonly ScimDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;

    /// <summary>Initializes a new instance of the <see cref="ScimPrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret endpoint + auth + resource configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the bearer token's <c>SecretRef</c>.</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    /// <param name="httpClient">An HTTP client; when <see langword="null"/> the directory owns a default one.</param>
    public ScimPrincipalDirectory(ScimDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper, HttpClient? httpClient = null)
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
        if (!this.options.Kinds.TryGetValue(kind, out ScimResourceType? resourceType))
        {
            return [];
        }

        int max = limit > 0 ? limit : 1;
        string token = await this.ResolveBearerTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(this.options.BaseUrl, resourceType, query, max, this.projector.RequiredAttributes));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/scim+json"));
        using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ScimDirectoryException($"the service provider returned {(int)response.StatusCode} ({response.StatusCode}) searching {resourceType.Path}.");
        }

        // Allocation ledger (per search). The response byte[] is the one driver-forced GC alloc (HttpContent gives no
        // pooled-read that fits); ProjectResponse then reads it IN PLACE with the Corvus Utf8JsonReader — no JsonDocument
        // / STJ DOM, no second copy of the body. The only other GC-escaping allocations are the API contract's: the
        // returned List + each ResolvedPrincipal (its Value/Label strings + stamped SecurityTagSet). Per-row scratch (the
        // flattened attribute Dictionary + transient GetString scalars) is what the mapper contract forces — and when the
        // mapper declares its RequiredAttributes the provider returns only those, so both the response byte[] and the
        // flatten shrink (the attribute-projection seam moves the saving onto the wire; see BuildSearchUri).
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return this.projector.SupportsSpanProjection
            ? ProjectResponseSpan(kind, resourceType, body, max, this.projector)
            : ProjectResponse(kind, resourceType, body, max, this.projector);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.ownsHttpClient)
        {
            this.httpClient.Dispose();
        }
    }

    private static Uri BuildSearchUri(Uri baseUrl, ScimResourceType resourceType, string query, int max, IReadOnlyCollection<string> requiredAttributes)
    {
        // SCIM resources hang off the provider's base path (e.g. https://host/scim/v2 + Users), so we append rather than
        // root-replace (the new Uri(base, "/Users") gotcha). The value-prefix search is the SCIM `sw` (starts-with)
        // operator; an empty query omits the filter (list the resource). The user query is quote-/backslash-escaped inside
        // the filter literal and the whole filter is URL-encoded, so it can never break out of the expression.
        string root = baseUrl.AbsoluteUri.TrimEnd('/');
        string path = resourceType.Path.Trim('/');
        string url = $"{root}/{path}?count={max}";
        if (query.Length > 0)
        {
            string filter = $"{resourceType.FilterAttribute} sw \"{EscapeFilterLiteral(query)}\"";
            url += $"&filter={Uri.EscapeDataString(filter)}";
        }

        // Attribute projection (the §16.5.4 seam): when the mapper declares what it reads, ask the provider (SCIM
        // `attributes`, RFC 7644 §3.4.2.5, standard attribute notation) to return only those plus the value/label
        // attributes the adapter itself needs — so the resource comes back smaller and there is less to flatten. When the
        // mapper declares nothing the parameter is omitted and the full resource is returned (the safe, general default).
        if (requiredAttributes.Count > 0)
        {
            string attributes = string.Join(",", ProjectionTokens(resourceType, requiredAttributes).Select(Uri.EscapeDataString));
            url += $"&attributes={attributes}";
        }

        return new Uri(url, UriKind.Absolute);
    }

    // The provider tokens to request: the value attribute and (if any) the display attribute the adapter needs to form the
    // grantee, unioned with the mapper's declared attributes — order-preserved and de-duplicated for a stable request.
    private static IEnumerable<string> ProjectionTokens(ScimResourceType resourceType, IReadOnlyCollection<string> requiredAttributes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(requiredAttributes.Count + 2);
        Append(resourceType.FilterAttribute);
        if (resourceType.DisplayAttribute is { } display)
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

    private static string EscapeFilterLiteral(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    // The bytes-to-bytes path (used when the mapper is a span mapper): capture the wanted attributes — the value, the
    // label, and the mapper's declared attributes — as unescaped UTF-8 into a pooled scratch (reused per resource), then
    // project span-wise with no attribute string. SCIM nests, so a wanted attribute is matched (and captured) by the LEAF
    // of its name: a top-level scalar by its name, and one level into a complex/extension object by its member name (so the
    // enterprise extension's `…:User:organization` is captured under `organization`, which the span mapper reads).
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponseSpan(GranteeKind kind, ScimResourceType resourceType, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);

        // The wanted attribute leaves as UTF-8 (value first, then label, then the mapper's declared attributes), built once.
        string[] required = [.. projector.RequiredAttributes];
        int wantedCount = 1 + (resourceType.DisplayAttribute is null ? 0 : 1) + required.Length;
        byte[][] wanted = new byte[wantedCount][];
        int next = 0;
        int valueWanted = next;
        wanted[next++] = Encoding.UTF8.GetBytes(Leaf(resourceType.FilterAttribute));
        int displayWanted = -1;
        if (resourceType.DisplayAttribute is { } displayAttribute)
        {
            displayWanted = next;
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(displayAttribute));
        }

        foreach (string attribute in required)
        {
            wanted[next++] = Encoding.UTF8.GetBytes(Leaf(attribute));
        }

        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return results;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isResources = reader.ValueTextEquals("Resources"u8);
            reader.Read();
            if (!isResources || reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                continue;
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
                        if (reader.ValueTextEquals("schemas"u8) || reader.ValueTextEquals("meta"u8))
                        {
                            reader.Read();
                            reader.Skip();
                            continue;
                        }

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
                    string valueText = Encoding.UTF8.GetString(valueSpan);
                    string label = displaySlice >= 0
                        ? Encoding.UTF8.GetString(scratch.AsSpan(slices[displaySlice].ValueOffset, slices[displaySlice].ValueLength))
                        : valueText;

                    var view = new DirectoryRecordView(kind, valueSpan, scratch, slices[..captured]);
                    if (projector.TryProjectIdentity(kind, valueText, label, view) is { } principal)
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

        return results;
    }

    // The leaf of a SCIM attribute path — the part after the last schema-URN ':' or sub-attribute '.', the key under which
    // the adapter flattens (and the span mapper reads) it. A bare name is its own leaf.
    private static string Leaf(string name)
    {
        int cut = Math.Max(name.LastIndexOf(':'), name.LastIndexOf('.'));
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

    // Projects a SCIM ListResponse ({ "Resources": [ ... ] }) to resolved principals. A pure function of (bytes, resource,
    // projector) — the reader borrows `body` in place (no DOM, no copy). A dropped record (mapper returns null, or a
    // resource missing its value attribute) does not consume the limit. `internal` only so the allocation benchmark can
    // drive it without a network.
    internal static IReadOnlyList<ResolvedPrincipal> ProjectResponse(GranteeKind kind, ScimResourceType resourceType, byte[] body, int limit, DirectoryPrincipalProjector projector)
    {
        var results = new List<ResolvedPrincipal>(limit);
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return results;
        }

        // SCIM list responses are forward-scanned for the (case-sensitive) "Resources" array; other envelope members
        // (schemas, totalResults, itemsPerPage, startIndex) are skipped in whatever order they arrive.
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isResources = reader.ValueTextEquals("Resources"u8);
            reader.Read();
            if (isResources && reader.TokenType == JsonTokenType.StartArray)
            {
                while (results.Count < limit && reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                {
                    Dictionary<string, IReadOnlyList<string>> attributes = ReadResource(ref reader);
                    if (Project(kind, resourceType, attributes, projector) is { } principal)
                    {
                        results.Add(principal);
                    }
                }

                return results;
            }

            reader.Skip();
        }

        return results;
    }

    private static ResolvedPrincipal? Project(GranteeKind kind, ScimResourceType resourceType, Dictionary<string, IReadOnlyList<string>> attributes, DirectoryPrincipalProjector projector)
    {
        string? value = First(attributes, resourceType.FilterAttribute);
        if (value is null)
        {
            return null;
        }

        string? display = (resourceType.DisplayAttribute is { } configured ? First(attributes, configured) : null)
            ?? First(attributes, "displayName")
            ?? First(attributes, "name.formatted")
            ?? value;

        return projector.Project(new DirectoryRecord(kind, value, display, attributes, []));
    }

    private static string? First(Dictionary<string, IReadOnlyList<string>> attributes, string key)
        => attributes.TryGetValue(key, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null;

    // Flattens one SCIM resource object (reader at its StartObject) into a string attribute map the mapper reads by name.
    // Scalars are keyed by their attribute name; a complex attribute (e.g. `name`) flattens dotted (`name.givenName`); an
    // extension schema (a URN-keyed object) flattens its members by leaf name (the enterprise extension's `organization`
    // becomes `organization`); a multi-valued attribute (e.g. `emails`) becomes the list of its entries' `value`s,
    // primary first. Leaves the reader at the matching EndObject.
    private static Dictionary<string, IReadOnlyList<string>> ReadResource(ref Utf8JsonReader reader)
    {
        var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            // Skip SCIM's structural common attributes (RFC 7643 §3.1): `schemas` is a list of long URN strings and `meta`
            // is resource bookkeeping (resourceType, timestamps, location, version) — neither is ever part of a principal's
            // identity, and flattening them is the bulk of the parse's wasted allocation. Every other member — core,
            // extension, or custom — is surfaced, since the deployment mapper may key on any of them.
            if (reader.ValueTextEquals("schemas"u8) || reader.ValueTextEquals("meta"u8))
            {
                reader.Read();
                reader.Skip();
                continue;
            }

            string name = reader.GetString()!;
            reader.Read();
            FlattenValue(ref reader, name, name.IndexOf(':', StringComparison.Ordinal) >= 0, attributes);
        }

        return attributes;
    }

    // Flattens the value the reader is positioned on under `key`. `isExtension` flattens a nested object's members by leaf
    // name rather than dotted (so an extension schema's scalars surface unqualified). Numbers and nulls are not surfaced
    // (a sys: identity tag is always a string); booleans surface as "true"/"false".
    private static void FlattenValue(ref Utf8JsonReader reader, string key, bool isExtension, Dictionary<string, IReadOnlyList<string>> attributes)
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
                    FlattenValue(ref reader, isExtension ? sub : $"{key}.{sub}", false, attributes);
                }

                break;
            case JsonTokenType.StartArray:
                FlattenArray(ref reader, key, attributes);
                break;
            default:
                // Number / Null — nothing to surface; the scalar token is already consumed.
                break;
        }
    }

    // Flattens a multi-valued attribute (reader at its StartArray) to the list of member `value`s (primary first), or the
    // list of scalar entries. Leaves the reader at the matching EndArray.
    private static void FlattenArray(ref Utf8JsonReader reader, string key, Dictionary<string, IReadOnlyList<string>> attributes)
    {
        var values = new List<string>();
        string? primary = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                (string? memberValue, bool isPrimary) = ReadMultiValuedMember(ref reader);
                if (memberValue is not null)
                {
                    values.Add(memberValue);
                    if (isPrimary)
                    {
                        primary = memberValue;
                    }
                }
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                if (reader.GetString() is { } scalar)
                {
                    values.Add(scalar);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        if (values.Count == 0)
        {
            return;
        }

        if (primary is not null && !string.Equals(values[0], primary, StringComparison.Ordinal))
        {
            values.Remove(primary);
            values.Insert(0, primary);
        }

        attributes[key] = values;
    }

    // Reads one multi-valued member object (reader at its StartObject), returning its `value` sub-attribute and whether it
    // is flagged primary. Leaves the reader at the matching EndObject.
    private static (string? Value, bool Primary) ReadMultiValuedMember(ref Utf8JsonReader reader)
    {
        string? value = null;
        bool primary = false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("value"u8))
            {
                reader.Read();
                value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }
            else if (reader.ValueTextEquals("primary"u8))
            {
                reader.Read();
                primary = reader.TokenType == JsonTokenType.True;
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        return (value, primary);
    }

    private static void Add(Dictionary<string, IReadOnlyList<string>> attributes, string key, string? value)
    {
        if (value is not null)
        {
            attributes[key] = [value];
        }
    }

    private async ValueTask<string> ResolveBearerTokenAsync(CancellationToken cancellationToken)
    {
        // The bearer token IS the secret (not a derived, short-lived access token), so per the §13 boundary it is resolved
        // from its SecretRef at the point of use and dropped immediately — never cached in the adapter. Any caching policy
        // belongs to the deployment's ISecretResolver.
        return this.options.Authentication switch
        {
            ScimBearerToken bearer => await bearer.Token.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported SCIM authentication '{this.options.Authentication.GetType().Name}'."),
        };
    }
}