// <copyright file="OktaDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Okta;

/// <summary>
/// An Okta Management API resource backing a grantee kind (design §16.5.4) — the resource collection to search, the
/// attribute that carries its searchable value and display label, and how its list response is shaped. Okta users and
/// groups expose their attributes under a <c>profile</c> object (so the value/display attributes are paths such as
/// <c>profile.login</c> / <c>profile.name</c>) and are returned as a bare JSON array; some resources (e.g. custom roles)
/// wrap their results in a named array.
/// </summary>
/// <param name="Path">The resource path relative to the API base URL (e.g. <c>users</c>, <c>groups</c>, <c>iam/roles</c>).</param>
/// <param name="FilterAttribute">The attribute the value-prefix search filters on (an Okta <c>search</c> path such as <c>profile.login</c>) and which becomes the resolved grantee value.</param>
/// <param name="DisplayAttribute">The attribute used for the human-friendly display label; when <see langword="null"/> the adapter falls back to the value.</param>
/// <param name="ResultsProperty">The response array property to read; <see langword="null"/> (the default) for a bare top-level array (users / groups), or the wrapping property name for resources that nest their results (e.g. <c>roles</c>).</param>
public sealed record OktaResource(string Path, string FilterAttribute, string? DisplayAttribute = null, string? ResultsProperty = null)
{
    /// <summary>Gets the <c>users</c> resource searched on <c>profile.login</c> and displayed by <c>profile.displayName</c> (a bare array).</summary>
    public static OktaResource Users { get; } = new("users", "profile.login", "profile.displayName");

    /// <summary>Gets the <c>groups</c> resource searched on and displayed by <c>profile.name</c> (a bare array).</summary>
    public static OktaResource Groups { get; } = new("groups", "profile.name", "profile.name");
}

/// <summary>
/// The non-secret configuration for <see cref="OktaPrincipalDirectory"/> (design §16.5.4) — the Okta org endpoint, how the
/// request is authenticated, and which resource backs each grantee kind. No secret lives here: the
/// <see cref="OktaAuthentication"/>'s credential is always a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>.
/// </summary>
public sealed class OktaDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the Okta org base URL (e.g. <c>https://example.okta.com</c>), without a trailing path; the API hangs off <c>/api/v1</c>.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Gets how the request is authenticated (an SSWS API token whose value is a <c>SecretRef</c>).</summary>
    public required OktaAuthentication Authentication { get; init; }

    /// <summary>Gets the per-grantee-kind resource mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, OktaResource> Kinds { get; init; }
}