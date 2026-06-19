// <copyright file="LdapBindMethod.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Ldap;

/// <summary>
/// How <see cref="LdapPrincipalDirectory"/> authenticates its read-only service-account bind (design §16.5.4). The auth
/// mechanism is the deployment's choice; the secret(s) it needs are always a <see cref="DirectoryCredential"/>
/// (a <c>SecretRef</c>) resolved through the deployment's <c>ISecretResolver</c> — never stored in config. All three
/// mechanisms are pure-managed and behave identically on Windows, Linux, and Alpine.
/// </summary>
public abstract record LdapBindMethod;

/// <summary>A simple bind (distinguished name + password). Use with <see cref="LdapTransportSecurity.Ldaps"/> or
/// <see cref="LdapTransportSecurity.StartTls"/> so the password is never sent in clear text.</summary>
/// <param name="BindDn">The service account's distinguished name.</param>
/// <param name="Password">The service account's password (a <c>SecretRef</c>).</param>
public sealed record LdapSimpleBind(string BindDn, DirectoryCredential Password) : LdapBindMethod;

/// <summary>A SASL DIGEST-MD5 bind — challenge/response, so the password is never sent in clear text even without TLS.</summary>
/// <param name="Username">The SASL authentication id (e.g. a <c>uid</c>, not a full DN).</param>
/// <param name="Password">The password (a <c>SecretRef</c>).</param>
/// <param name="Realm">The SASL realm, or <see langword="null"/> for the server's default.</param>
public sealed record LdapDigestMd5Bind(string Username, DirectoryCredential Password, string? Realm = null) : LdapBindMethod;

/// <summary>
/// A SASL EXTERNAL bind authenticated by a client certificate (mutual TLS) — password-less. The certificate (and its
/// optional passphrase) is a <see cref="DirectoryCredential"/> resolving to a PFX/PKCS#12 blob; it is presented on the
/// TLS handshake and the directory derives the authorization identity from it. Requires a TLS transport
/// (<see cref="LdapTransportSecurity.Ldaps"/> or <see cref="LdapTransportSecurity.StartTls"/>).
/// </summary>
/// <param name="Certificate">The client certificate + private key as a PFX/PKCS#12 blob (a <c>SecretRef</c>).</param>
/// <param name="CertificatePassword">The PFX passphrase (a <c>SecretRef</c>), or <see langword="null"/> if the PFX is unencrypted.</param>
/// <param name="AuthorizationId">An optional explicit SASL authorization id; <see langword="null"/> lets the directory derive it from the certificate.</param>
public sealed record LdapClientCertificateBind(DirectoryCredential Certificate, DirectoryCredential? CertificatePassword = null, string? AuthorizationId = null) : LdapBindMethod;

/// <summary>The transport security for an LDAP connection.</summary>
public enum LdapTransportSecurity
{
    /// <summary>Plain LDAP (port 389) — no transport encryption. Only for a trusted network / local development.</summary>
    None,

    /// <summary>LDAPS — TLS from connect (port 636).</summary>
    Ldaps,

    /// <summary>StartTLS — connect plain (port 389) then upgrade the connection to TLS before binding.</summary>
    StartTls,
}