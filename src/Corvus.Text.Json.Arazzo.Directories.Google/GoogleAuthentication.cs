// <copyright file="GoogleAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Google;

/// <summary>
/// How <see cref="GooglePrincipalDirectory"/> obtains the access token it presents to the Google Admin SDK Directory API
/// (design §16.5.4 / §13.4). The secret is a <see cref="DirectoryCredential"/> (a <c>SecretRef</c> resolved through the
/// deployment's <c>ISecretResolver</c>), never stored.
/// </summary>
public abstract record GoogleAuthentication;

/// <summary>
/// A Google service account with <strong>domain-wide delegation</strong> — the adapter signs a JWT with the service
/// account's private key and exchanges it for an access token impersonating <paramref name="Subject"/> (an admin
/// authorized for the requested Directory scopes). The recommended server-to-server option.
/// </summary>
/// <param name="ClientEmail">The service account's client email (the JWT issuer).</param>
/// <param name="PrivateKey">The service account's PEM private key, as a <c>SecretRef</c> (the <c>private_key</c> field of its JSON key).</param>
/// <param name="Subject">The Workspace admin email to impersonate (domain-wide delegation), authorized for the Directory scopes.</param>
public sealed record GoogleServiceAccount(string ClientEmail, DirectoryCredential PrivateKey, string Subject) : GoogleAuthentication;