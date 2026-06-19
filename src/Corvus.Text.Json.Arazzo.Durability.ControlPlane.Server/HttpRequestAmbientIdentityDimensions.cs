// <copyright file="HttpRequestAmbientIdentityDimensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// An <see cref="IAmbientIdentityDimensions"/> that derives the current request's ambient <c>sys:</c> dimensions from the
/// request context (design §16.5.5) — read through <see cref="IHttpContextAccessor"/> — by matching a context value (the
/// vanity host, a route value, a gateway-inserted header) against an <strong>authoritative allow-list</strong>. Only a
/// configured value resolves; anything else yields <see cref="AmbientDimensionSet.Empty"/> (fail-closed). The allow-list
/// <em>is</em> the validation: the provider never trusts a raw context value at face value, so the trust decision lives
/// in this one auditable component.
/// </summary>
/// <remarks>
/// <para>
/// The provider is registered as a singleton wrapping the singleton <see cref="IHttpContextAccessor"/>;
/// <see cref="Resolve"/> reads the <em>current</em> request's context on each call, so it is request-correct without being
/// request-scoped (no captive dependency). Each allow-list entry is pre-built into a reusable
/// <see cref="AmbientDimensionSet"/> at construction, so <see cref="Resolve"/> is a dictionary lookup returning a cached
/// instance — it allocates nothing of its own.
/// </para>
/// <para>
/// <strong>Trust boundary.</strong> A context-derived dimension is only as trustworthy as the path that sets it. A
/// gateway header (<see cref="ByHeader"/>) is safe <em>only</em> if the application cannot be reached bypassing the
/// gateway, or strips any client-supplied copy and trusts only the gateway-inserted value. A vanity host
/// (<see cref="ByHost"/>) is matched against the configured allow-list, never the raw <c>Host</c> taken at face value.
/// </para>
/// </remarks>
public sealed class HttpRequestAmbientIdentityDimensions : IAmbientIdentityDimensions
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly Func<HttpContext, string?> contextKeySelector;
    private readonly IReadOnlyDictionary<string, AmbientDimensionSet> allowList;
    private readonly string[] governedKeys;

    /// <summary>Initializes a new instance of the <see cref="HttpRequestAmbientIdentityDimensions"/> class.</summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's context.</param>
    /// <param name="contextKeySelector">Extracts the raw context value to match against the allow-list (e.g. the host or a header), or <see langword="null"/> when the request carries none.</param>
    /// <param name="allowList">The authoritative mapping from a validated context value to the ambient <c>sys:</c> tags it confers. Every entry should declare the same dimension keys, which become the provider's <see cref="GovernedKeys"/>.</param>
    /// <param name="keyComparer">The comparer for the context value (defaults to <see cref="StringComparer.Ordinal"/>; the host factory uses a case-insensitive comparer).</param>
    public HttpRequestAmbientIdentityDimensions(
        IHttpContextAccessor httpContextAccessor,
        Func<HttpContext, string?> contextKeySelector,
        IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> allowList,
        IEqualityComparer<string>? keyComparer = null)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(contextKeySelector);
        ArgumentNullException.ThrowIfNull(allowList);

        this.httpContextAccessor = httpContextAccessor;
        this.contextKeySelector = contextKeySelector;

        var map = new Dictionary<string, AmbientDimensionSet>(allowList.Count, keyComparer ?? StringComparer.Ordinal);
        var governed = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<SecurityTag>> entry in allowList)
        {
            map[entry.Key] = AmbientDimensionSet.Create(entry.Value);
            foreach (SecurityTag tag in entry.Value)
            {
                governed.Add(tag.Key);
            }
        }

        this.allowList = map;
        this.governedKeys = [.. governed];
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GovernedKeys => this.governedKeys;

    /// <summary>
    /// Builds a provider that maps the request's <strong>vanity host</strong> to its ambient dimensions (e.g.
    /// <c>acme.host.example</c> → <c>sys:tenant=acme</c>). Host matching is case-insensitive.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's host.</param>
    /// <param name="hostAllowList">The authoritative host → ambient-tags mapping.</param>
    /// <returns>The provider.</returns>
    public static HttpRequestAmbientIdentityDimensions ByHost(
        IHttpContextAccessor httpContextAccessor,
        IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> hostAllowList)
        => new(httpContextAccessor, static context => context.Request.Host.Host, hostAllowList, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds a provider that maps a <strong>gateway-inserted header</strong> to its ambient dimensions (e.g.
    /// <c>X-Tenant: acme</c> → <c>sys:tenant=acme</c>). Safe only behind a gateway that strips any client-supplied copy of
    /// the header (see the type remarks).
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's headers.</param>
    /// <param name="headerName">The gateway-inserted header carrying the validated context value.</param>
    /// <param name="headerAllowList">The authoritative header-value → ambient-tags mapping.</param>
    /// <returns>The provider.</returns>
    public static HttpRequestAmbientIdentityDimensions ByHeader(
        IHttpContextAccessor httpContextAccessor,
        string headerName,
        IReadOnlyDictionary<string, IReadOnlyList<SecurityTag>> headerAllowList)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        return new(
            httpContextAccessor,
            context => context.Request.Headers.TryGetValue(headerName, out Microsoft.Extensions.Primitives.StringValues value) ? value.ToString() : null,
            headerAllowList);
    }

    /// <inheritdoc/>
    public AmbientDimensionSet Resolve()
    {
        HttpContext? context = this.httpContextAccessor.HttpContext;
        if (context is null)
        {
            return AmbientDimensionSet.Empty;
        }

        string? key = this.contextKeySelector(context);
        if (string.IsNullOrEmpty(key))
        {
            return AmbientDimensionSet.Empty;
        }

        return this.allowList.TryGetValue(key, out AmbientDimensionSet? set) ? set : AmbientDimensionSet.Empty;
    }
}