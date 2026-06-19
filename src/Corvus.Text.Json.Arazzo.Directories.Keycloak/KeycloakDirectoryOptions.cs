// <copyright file="KeycloakDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak;

/// <summary>The Keycloak Admin resource a grantee kind is searched against.</summary>
public enum KeycloakResource
{
    /// <summary>Realm users (<c>/admin/realms/{realm}/users</c>).</summary>
    Users,

    /// <summary>Realm groups (<c>/admin/realms/{realm}/groups</c>).</summary>
    Groups,

    /// <summary>Realm roles (<c>/admin/realms/{realm}/roles</c>).</summary>
    Roles,
}

/// <summary>
/// The non-secret configuration for <see cref="KeycloakPrincipalDirectory"/> (design §16.5.4) — the Keycloak server, the
/// realm to search, how the admin token is obtained, and which Admin resource backs each grantee kind. No secret lives
/// here: the <see cref="KeycloakAuthentication"/>'s credential is always a <c>SecretRef</c> resolved through the
/// deployment's <c>ISecretResolver</c>.
/// </summary>
public sealed class KeycloakDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the Keycloak server base URL (e.g. <c>https://keycloak.example.org</c>), without a trailing path.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Gets the realm whose users / groups / roles are searched.</summary>
    public required string Realm { get; init; }

    /// <summary>Gets the realm whose token endpoint authenticates the admin client; defaults to <see cref="Realm"/> when
    /// <see langword="null"/> (set <c>master</c> when using the built-in <c>admin-cli</c> client).</summary>
    public string? TokenRealm { get; init; }

    /// <summary>Gets how the admin access token is obtained (client-credentials or password grant) and its <c>SecretRef</c> credential(s).</summary>
    public required KeycloakAuthentication Authentication { get; init; }

    /// <summary>Gets the per-grantee-kind Admin resource mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, KeycloakResource> Kinds { get; init; }
}