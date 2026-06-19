// <copyright file="GoogleDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Google;

/// <summary>
/// A Google Admin SDK Directory API resource backing a grantee kind (design §16.5.4) — the collection to search, how its
/// list response is shaped, the field the prefix query targets, and the fields that carry the resolved value and display
/// label. People are the <c>users</c> collection (a <c>users</c> array, queried on <c>email</c>), teams the <c>groups</c>
/// collection (a <c>groups</c> array), and roles the <c>roles</c> collection (an <c>items</c> array).
/// </summary>
/// <param name="Path">The collection path relative to the Directory API base URL (e.g. <c>users</c>, <c>groups</c>).</param>
/// <param name="ResultsProperty">The response array property holding the resources (e.g. <c>users</c>, <c>groups</c>, <c>items</c>).</param>
/// <param name="QueryField">The field the value-prefix search filters on in the Directory <c>query</c> syntax (e.g. <c>email</c>, <c>name</c>).</param>
/// <param name="ValueField">The response field whose value becomes the resolved grantee value (e.g. <c>primaryEmail</c> for users).</param>
/// <param name="DisplayField">The field used for the display label (e.g. <c>name.fullName</c>); when <see langword="null"/> the adapter falls back to the value.</param>
public sealed record GoogleResource(string Path, string ResultsProperty, string QueryField, string ValueField, string? DisplayField = null)
{
    /// <summary>Gets the <c>users</c> resource — a <c>users</c> array, queried on <c>email</c>, valued by <c>primaryEmail</c>, displayed by <c>name.fullName</c>.</summary>
    public static GoogleResource Users { get; } = new("users", "users", "email", "primaryEmail", "name.fullName");

    /// <summary>Gets the <c>groups</c> resource — a <c>groups</c> array, queried on and valued by <c>email</c>, displayed by <c>name</c>.</summary>
    public static GoogleResource Groups { get; } = new("groups", "groups", "email", "email", "name");
}

/// <summary>
/// The non-secret configuration for <see cref="GooglePrincipalDirectory"/> (design §16.5.4) — the Directory API endpoint,
/// the customer to search, how the token is obtained, and which collection backs each grantee kind. No secret lives here:
/// the <see cref="GoogleAuthentication"/>'s credential is always a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>.
/// </summary>
public sealed class GoogleDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets how the access token is obtained (a service-account JWT) and its <c>SecretRef</c> credential.</summary>
    public required GoogleAuthentication Authentication { get; init; }

    /// <summary>Gets the per-grantee-kind collection mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, GoogleResource> Kinds { get; init; }

    /// <summary>Gets the customer id whose users / groups / roles are searched; defaults to <c>my_customer</c> (the account of the impersonated subject).</summary>
    public string Customer { get; init; } = "my_customer";

    /// <summary>Gets the Directory API base URL; defaults to <c>https://admin.googleapis.com/admin/directory/v1</c>.</summary>
    public Uri DirectoryBaseUrl { get; init; } = new("https://admin.googleapis.com/admin/directory/v1");

    /// <summary>Gets the OAuth 2.0 token endpoint that exchanges the signed JWT for an access token; defaults to <c>https://oauth2.googleapis.com/token</c>.</summary>
    public Uri TokenEndpoint { get; init; } = new("https://oauth2.googleapis.com/token");

    /// <summary>Gets the OAuth 2.0 scope requested for the token; defaults to the read-only Directory scope.</summary>
    public string Scope { get; init; } = "https://www.googleapis.com/auth/admin.directory.user.readonly https://www.googleapis.com/auth/admin.directory.group.readonly";
}