// <copyright file="OktaAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Okta;

/// <summary>
/// How <see cref="OktaPrincipalDirectory"/> authenticates to the Okta Management API (design §16.5.4 / §13.4). The
/// credential is a <see cref="DirectoryCredential"/> (a <c>SecretRef</c> resolved through the deployment's
/// <c>ISecretResolver</c>), never stored.
/// </summary>
public abstract record OktaAuthentication;

/// <summary>
/// An Okta SSWS API token presented as <c>Authorization: SSWS &lt;token&gt;</c> — the org-scoped token a service account
/// holds. The token is long-lived secret material, so it is resolved from its <c>SecretRef</c> at the point of each
/// search and dropped immediately (the §13 boundary); it is never cached in the adapter.
/// </summary>
/// <param name="Token">The SSWS API token, as a <c>SecretRef</c>.</param>
public sealed record OktaApiToken(DirectoryCredential Token) : OktaAuthentication;