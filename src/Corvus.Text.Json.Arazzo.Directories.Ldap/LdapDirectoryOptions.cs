// <copyright file="LdapDirectoryOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Security;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Ldap;

/// <summary>
/// The non-secret connection + schema configuration for <see cref="LdapPrincipalDirectory"/> (design §16.5.4) — the
/// directory's host, transport security, the chosen <see cref="LdapBindMethod"/>, and how each grantee kind maps onto
/// the directory's schema (object class, the searched value attribute, and the attributes to fetch for the identity
/// mapper). No secret lives here: the bind method's password / certificate is always a <c>SecretRef</c> resolved
/// through the deployment's <c>ISecretResolver</c>.
/// </summary>
public sealed class LdapDirectoryOptions
{
    /// <summary>
    /// Gets the issuer id stamped onto every resolved principal's identity (the reserved <c>sys:iss</c> tag), making this
    /// directory's identities disjoint from every other provider's. Must be a stable, deployment-unique id; the
    /// deployment's runtime identity stamper must emit the same <c>sys:iss</c> for resolved grants to match (see
    /// <see cref="DirectoryIssuer"/>).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the LDAP server host name.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the LDAP server port (default 389; set 636 for <see cref="LdapTransportSecurity.Ldaps"/>).</summary>
    public int Port { get; init; } = 389;

    /// <summary>Gets the transport security (default <see cref="LdapTransportSecurity.StartTls"/>).</summary>
    public LdapTransportSecurity Security { get; init; } = LdapTransportSecurity.StartTls;

    /// <summary>Gets the bind method (simple / DIGEST-MD5 / client-certificate) and its <c>SecretRef</c> credential(s).</summary>
    public required LdapBindMethod Bind { get; init; }

    /// <summary>
    /// Gets an optional server-certificate validation callback for TLS connections; when <see langword="null"/> the OS
    /// trust store is used (the secure default). Supply one only to trust a private CA (or, for development, to accept a
    /// self-signed certificate).
    /// </summary>
    public RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; init; }

    /// <summary>Gets the per-grantee-kind schema mapping; a kind absent from the map is not searchable.</summary>
    public required IReadOnlyDictionary<GranteeKind, LdapKindOptions> Kinds { get; init; }
}

/// <summary>How one grantee kind (person / team / role) maps onto the directory's LDAP schema.</summary>
public sealed class LdapKindOptions
{
    /// <summary>Gets the search base DN for this kind (e.g. <c>ou=people,dc=example,dc=org</c>).</summary>
    public required string BaseDn { get; init; }

    /// <summary>Gets the object-class filter selecting entries of this kind (e.g. <c>inetOrgPerson</c>, <c>groupOfNames</c>).</summary>
    public required string ObjectClass { get; init; }

    /// <summary>Gets the attribute that is both the grantee value and the prefix-searched key (e.g. <c>uid</c>, <c>cn</c>).</summary>
    public required string ValueAttribute { get; init; }

    /// <summary>Gets the attribute holding the human-friendly display name (e.g. <c>cn</c>, <c>displayName</c>), or <see langword="null"/>.</summary>
    public string? DisplayAttribute { get; init; }

    /// <summary>Gets the additional attributes to fetch and expose to the identity mapper (e.g. <c>department</c>, <c>mail</c>).</summary>
    public IReadOnlyList<string> FetchAttributes { get; init; } = [];

    /// <summary>Gets the attribute holding group/role memberships to expose as <see cref="DirectoryRecord.Groups"/> (e.g. <c>memberOf</c>), or <see langword="null"/>.</summary>
    public string? GroupAttribute { get; init; }
}