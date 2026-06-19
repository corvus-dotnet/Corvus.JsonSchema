// <copyright file="KeycloakAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak;

/// <summary>
/// How <see cref="KeycloakPrincipalDirectory"/> obtains the admin access token it presents to the Keycloak Admin REST
/// API (design §16.5.4 / §13.4). The secret(s) are a <see cref="DirectoryCredential"/> (a <c>SecretRef</c> resolved
/// through the deployment's <c>ISecretResolver</c>), never stored.
/// </summary>
public abstract record KeycloakAuthentication;

/// <summary>
/// The OAuth 2.0 client-credentials grant — a confidential service-account client (granted the realm-management roles
/// that permit reading users / groups / roles, e.g. <c>view-users</c>). The recommended machine-to-machine option.
/// </summary>
/// <param name="ClientId">The service-account client id.</param>
/// <param name="ClientSecret">The client secret, as a <c>SecretRef</c>.</param>
public sealed record KeycloakClientCredentials(string ClientId, DirectoryCredential ClientSecret) : KeycloakAuthentication;

/// <summary>
/// The OAuth 2.0 password grant — an administrator user authenticating through a client (for example the built-in
/// <c>admin-cli</c> public client on the <c>master</c> realm, or a confidential client when a <paramref name="ClientSecret"/>
/// is supplied).
/// </summary>
/// <param name="ClientId">The client id (e.g. <c>admin-cli</c>).</param>
/// <param name="Username">The administrator username.</param>
/// <param name="Password">The administrator password, as a <c>SecretRef</c>.</param>
/// <param name="ClientSecret">The client secret when the client is confidential, as a <c>SecretRef</c>; <see langword="null"/> for a public client.</param>
public sealed record KeycloakPasswordGrant(string ClientId, string Username, DirectoryCredential Password, DirectoryCredential? ClientSecret = null) : KeycloakAuthentication;