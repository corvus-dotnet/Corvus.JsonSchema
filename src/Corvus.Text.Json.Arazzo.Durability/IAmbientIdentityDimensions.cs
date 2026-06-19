// <copyright file="IAmbientIdentityDimensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Resolves the ambient <c>sys:</c> identity dimensions for the current request context (design §16.5.5) — <c>sys:</c>
/// tags such as <c>sys:tenant</c> whose value comes from the request itself (a vanity host, a route prefix, a validated
/// API-gateway header) rather than the IdP token. It is the <strong>single source of truth</strong> consulted by
/// <em>both</em> moments an identity is stamped — when a grantee is resolved (authoring) and when the caller is stamped
/// (runtime) — so the two can never drift: a grant authored within a tenant context set-equals exactly the caller in
/// that context (membership is exact set-equality on the whole stamped identity, §16.5.4).
/// </summary>
/// <remarks>
/// <para>
/// This generalises the <c>DirectoryIssuer</c> / <c>sys:iss</c> pattern: a deployment-controlled, mapper-immutable
/// dimension funnelled through one component and stamped correct-by-construction. <see cref="GovernedKeys"/> are the
/// dimensions the provider owns; a stamping path strips any same-key tag an upstream mapper produced before appending
/// the provider's value, so a mapper can neither omit nor forge an ambient dimension.
/// </para>
/// <para>
/// <strong>Fail closed.</strong> An unresolvable context (an unknown host, a missing or invalid gateway header) must
/// yield <see cref="AmbientDimensionSet.Empty"/> — no ambient dimension, therefore no cross-tenant access — never a
/// blank or wildcard value that would cross tenants. Because the dimension is an isolation primitive, the provider is
/// also the <strong>trust boundary</strong>: it must resolve only from an authoritative, validated source (an
/// allow-list), never a raw, spoofable <c>Host</c> header or an un-validated client-supplied value. None of <c>sys:</c>'s
/// correct-by-construction guarantees hold if the dimension's source is spoofable.
/// </para>
/// </remarks>
public interface IAmbientIdentityDimensions
{
    /// <summary>
    /// Gets the <c>sys:</c> dimension keys this provider governs (e.g. <c>sys:tenant</c>). A stamping path strips a tag
    /// carrying one of these keys from an upstream mapper's output before the provider's own value is appended, so the
    /// dimension stays mapper-immutable <em>even when this context resolves no value</em> (a smuggled tenant is stripped
    /// rather than honoured — fail-closed).
    /// </summary>
    IReadOnlyCollection<string> GovernedKeys { get; }

    /// <summary>
    /// Resolves the ambient dimensions for the current request context, or <see cref="AmbientDimensionSet.Empty"/> when
    /// none resolves (fail-closed).
    /// </summary>
    /// <returns>The resolved ambient dimensions — a reusable, allocation-free instance (the provider returns a cached set, never building one per call).</returns>
    AmbientDimensionSet Resolve();
}