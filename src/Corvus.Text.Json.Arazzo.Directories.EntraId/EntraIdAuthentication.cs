// <copyright file="EntraIdAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.EntraId;

/// <summary>
/// How <see cref="EntraIdPrincipalDirectory"/> obtains the access token it presents to Microsoft Graph (design §16.5.4 /
/// §13.4). The secret is a <see cref="DirectoryCredential"/> (a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>), never stored.
/// </summary>
public abstract record EntraIdAuthentication;

/// <summary>
/// The OAuth 2.0 client-credentials grant — a confidential app registration with a client secret, granted the
/// application permissions it needs to read users / groups / directory roles (e.g. <c>User.Read.All</c>,
/// <c>GroupMember.Read.All</c>, <c>Directory.Read.All</c>). The recommended machine-to-machine option.
/// </summary>
/// <param name="ClientId">The app registration's application (client) id.</param>
/// <param name="ClientSecret">The client secret, as a <c>SecretRef</c>.</param>
public sealed record EntraIdClientCredentials(string ClientId, DirectoryCredential ClientSecret) : EntraIdAuthentication;