// <copyright file="EntraIdDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.EntraId;

/// <summary>
/// A Microsoft Graph resource type backing a grantee kind (design §16.5.4) — the Graph entity collection to search and
/// the attributes that carry its searchable value and display label. People are the <c>users</c> collection (searchable
/// on <c>mailNickname</c> or <c>userPrincipalName</c>), teams the <c>groups</c> collection (searchable on
/// <c>displayName</c>), and roles the <c>directoryRoles</c> collection (or a custom collection the deployment exposes).
/// </summary>
/// <param name="Path">The Graph entity collection, relative to the API base URL (e.g. <c>users</c>, <c>groups</c>, <c>directoryRoles</c>).</param>
/// <param name="FilterAttribute">The attribute the value-prefix search filters on (a Graph <c>startsWith</c>-filterable property) and which becomes the resolved grantee value (e.g. <c>mailNickname</c> for users, <c>displayName</c> for groups).</param>
/// <param name="DisplayAttribute">The attribute used for the human-friendly display label; when <see langword="null"/> the adapter falls back to <c>displayName</c>, then the value.</param>
public sealed record EntraIdResource(string Path, string FilterAttribute, string? DisplayAttribute = null)
{
    /// <summary>Gets the <c>users</c> resource searched on <c>mailNickname</c> (the short alias) and displayed by <c>displayName</c>.</summary>
    public static EntraIdResource Users { get; } = new("users", "mailNickname", "displayName");

    /// <summary>Gets the <c>groups</c> resource searched on and displayed by <c>displayName</c>.</summary>
    public static EntraIdResource Groups { get; } = new("groups", "displayName", "displayName");
}

/// <summary>
/// The non-secret configuration for <see cref="EntraIdPrincipalDirectory"/> (design §16.5.4) — the Graph endpoint, the
/// tenant and authority used to obtain the token, how the token is obtained, and which Graph collection backs each
/// grantee kind. No secret lives here: the <see cref="EntraIdAuthentication"/>'s credential is always a <c>SecretRef</c>
/// resolved through the deployment's <c>ISecretResolver</c>.
/// </summary>
public sealed class EntraIdDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the directory (tenant) id whose users / groups / roles are searched and whose token authority is addressed.</summary>
    public required string TenantId { get; init; }

    /// <summary>Gets how the Graph access token is obtained (a client-credentials grant) and its <c>SecretRef</c> credential.</summary>
    public required EntraIdAuthentication Authentication { get; init; }

    /// <summary>Gets the per-grantee-kind Graph collection mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, EntraIdResource> Kinds { get; init; }

    /// <summary>Gets the Microsoft Graph API base URL; defaults to the public cloud <c>https://graph.microsoft.com/v1.0</c> (set a sovereign-cloud endpoint where required).</summary>
    public Uri GraphBaseUrl { get; init; } = new("https://graph.microsoft.com/v1.0");

    /// <summary>Gets the Microsoft identity platform authority base URL; defaults to the public cloud <c>https://login.microsoftonline.com</c> (set a sovereign-cloud authority where required).</summary>
    public Uri LoginBaseUrl { get; init; } = new("https://login.microsoftonline.com");

    /// <summary>Gets the OAuth 2.0 scope requested for the token; defaults to <c>https://graph.microsoft.com/.default</c> (the app's configured application permissions).</summary>
    public string Scope { get; init; } = "https://graph.microsoft.com/.default";
}