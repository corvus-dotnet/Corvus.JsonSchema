// <copyright file="ScimAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Scim;

/// <summary>
/// How <see cref="ScimPrincipalDirectory"/> authenticates to the SCIM 2.0 service provider (design §16.5.4 / §13.4).
/// The credential is a <see cref="DirectoryCredential"/> (a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>), never stored.
/// </summary>
public abstract record ScimAuthentication;

/// <summary>
/// A bearer token presented as <c>Authorization: Bearer &lt;token&gt;</c> — the common SCIM provisioning credential
/// (Microsoft Entra outbound SCIM, Okta, OneLogin, SailPoint, …). The token itself is long-lived secret material, so it
/// is resolved from its <c>SecretRef</c> at the point of each search and dropped immediately (the §13 boundary); it is
/// never cached in the adapter.
/// </summary>
/// <param name="Token">The bearer token, as a <c>SecretRef</c>.</param>
public sealed record ScimBearerToken(DirectoryCredential Token) : ScimAuthentication;