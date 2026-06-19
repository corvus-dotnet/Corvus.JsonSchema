// <copyright file="ScimDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Scim;

/// <summary>
/// A SCIM 2.0 resource type backing a grantee kind (design §16.5.4) — the resource endpoint to search and the attributes
/// that carry its searchable value and display label. SCIM is intentionally extensible, so the path and attributes are
/// configured rather than hard-coded: people are the core <c>Users</c> resource (searchable on <c>userName</c>), teams
/// the core <c>Groups</c> resource (searchable on <c>displayName</c>), and roles whatever resource the provider exposes.
/// </summary>
/// <param name="Path">The resource path relative to the service-provider base URL (e.g. <c>Users</c>, <c>Groups</c>, <c>Roles</c>).</param>
/// <param name="FilterAttribute">The attribute the value-prefix search filters on and which becomes the resolved grantee value (e.g. <c>userName</c> for users, <c>displayName</c> for groups).</param>
/// <param name="DisplayAttribute">The attribute used for the human-friendly display label; when <see langword="null"/> the adapter falls back to <c>displayName</c>, then <c>name.formatted</c>, then the value.</param>
public sealed record ScimResourceType(string Path, string FilterAttribute, string? DisplayAttribute = null)
{
    /// <summary>Gets the core <c>Users</c> resource type (searchable on <c>userName</c>, displayed by <c>displayName</c>).</summary>
    public static ScimResourceType Users { get; } = new("Users", "userName", "displayName");

    /// <summary>Gets the core <c>Groups</c> resource type (searchable on and displayed by <c>displayName</c>).</summary>
    public static ScimResourceType Groups { get; } = new("Groups", "displayName", "displayName");
}

/// <summary>
/// The non-secret configuration for <see cref="ScimPrincipalDirectory"/> (design §16.5.4) — the SCIM 2.0 service-provider
/// endpoint, how the bearer token is obtained, and which resource type backs each grantee kind. No secret lives here: the
/// <see cref="ScimAuthentication"/>'s credential is always a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>.
/// </summary>
public sealed class ScimDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the SCIM 2.0 service-provider base URL — the root the resource paths hang off (e.g. <c>https://example.com/scim/v2</c>).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Gets how the SCIM request is authenticated (a bearer token whose value is a <c>SecretRef</c>).</summary>
    public required ScimAuthentication Authentication { get; init; }

    /// <summary>Gets the per-grantee-kind SCIM resource mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, ScimResourceType> Kinds { get; init; }
}