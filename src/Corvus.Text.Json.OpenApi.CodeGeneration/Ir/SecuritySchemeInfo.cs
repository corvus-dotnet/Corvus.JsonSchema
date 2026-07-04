// <copyright file="SecuritySchemeInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI security scheme.
/// </summary>
/// <param name="SchemeName">The scheme name.</param>
/// <param name="SchemeType">The scheme type.</param>
/// <param name="IsDeprecated">Whether the scheme is deprecated.</param>
/// <param name="Description">The optional description.</param>
/// <param name="ApiKeyName">The optional API key name.</param>
/// <param name="ApiKeyIn">The optional API key location.</param>
/// <param name="HttpScheme">The optional HTTP scheme.</param>
/// <param name="BearerFormat">The optional bearer format.</param>
/// <param name="OpenIdConnectUrl">The optional OpenID Connect URL.</param>
/// <param name="Oauth2MetadataUrl">The optional OAuth2 metadata URL.</param>
/// <param name="DeviceAuthorizationUrl">The optional device authorization URL.</param>
/// <param name="TokenUrl">The optional token URL.</param>
/// <param name="AuthorizationUrl">The optional authorization URL.</param>
/// <param name="AvailableScopes">The optional available scopes.</param>
public readonly record struct SecuritySchemeInfo(
    string SchemeName,
    string SchemeType,
    bool IsDeprecated,
    string? Description,
    string? ApiKeyName,
    string? ApiKeyIn,
    string? HttpScheme,
    string? BearerFormat,
    string? OpenIdConnectUrl,
    string? Oauth2MetadataUrl,
    string? DeviceAuthorizationUrl,
    string? TokenUrl = null,
    string? AuthorizationUrl = null,
    string[]? AvailableScopes = null);