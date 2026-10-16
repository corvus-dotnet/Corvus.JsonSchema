// <copyright file="SecuritySchemeInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A security scheme from the document's components.
/// </summary>
/// <param name="SchemeName">The scheme's key in <c>components.securitySchemes</c>.</param>
/// <param name="SchemeType">The scheme <c>type</c> (<c>apiKey</c>, <c>http</c>, <c>oauth2</c>, <c>openIdConnect</c>, <c>mutualTLS</c>).</param>
/// <param name="IsDeprecated">Whether the scheme is marked <c>deprecated</c>.</param>
/// <param name="Description">The scheme's <c>description</c>, or <see langword="null"/>.</param>
/// <param name="ApiKeyName">For <c>apiKey</c>, the header, query or cookie name; otherwise <see langword="null"/>.</param>
/// <param name="ApiKeyIn">For <c>apiKey</c>, where the key is sent (<c>header</c>, <c>query</c> or <c>cookie</c>); otherwise <see langword="null"/>.</param>
/// <param name="HttpScheme">For <c>http</c>, the authorization scheme (<c>basic</c>, <c>bearer</c>, ...); otherwise <see langword="null"/>.</param>
/// <param name="BearerFormat">For <c>http</c> bearer, the <c>bearerFormat</c> hint; otherwise <see langword="null"/>.</param>
/// <param name="OpenIdConnectUrl">For <c>openIdConnect</c>, the discovery URL; otherwise <see langword="null"/>.</param>
/// <param name="Oauth2MetadataUrl">For <c>oauth2</c>, the authorization server metadata URL (OpenAPI 3.2); otherwise <see langword="null"/>.</param>
/// <param name="DeviceAuthorizationUrl">For <c>oauth2</c>, the device authorization flow URL (OpenAPI 3.2); otherwise <see langword="null"/>.</param>
/// <param name="TokenUrl">For <c>oauth2</c>, the token URL of the first flow that has one; otherwise <see langword="null"/>.</param>
/// <param name="AuthorizationUrl">For <c>oauth2</c>, the authorization URL of the first flow that has one; otherwise <see langword="null"/>.</param>
/// <param name="AvailableScopes">For <c>oauth2</c>, the union of the scopes its flows declare; otherwise <see langword="null"/>.</param>
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