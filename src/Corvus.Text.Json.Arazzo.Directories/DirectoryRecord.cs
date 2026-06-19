// <copyright file="DirectoryRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// A raw principal record fetched from an external directory, <em>before</em> a deployment maps it to a <c>sys:</c>
/// identity (design §16.5.4). A protocol adapter (LDAP, Keycloak, Graph, …) knows how to <em>fetch</em> these — the id,
/// display name, the directory's attributes, and group memberships — but <strong>not</strong> how the deployment stamps
/// those into <c>sys:</c> tags; that projection is the deployment's <see cref="IDirectoryIdentityMapper"/>.
/// </summary>
/// <param name="Kind">The grantee kind the adapter queried for (person / team / role).</param>
/// <param name="Id">The directory's stable identifier for this principal (e.g. an LDAP <c>entryUUID</c>/<c>objectGUID</c>, a Graph object id, a Keycloak user id) — typically the searchable grantee value.</param>
/// <param name="DisplayName">The principal's human-friendly display name, or <see langword="null"/>.</param>
/// <param name="Attributes">The principal's raw directory attributes, each possibly multi-valued (e.g. <c>department</c>, <c>mail</c>, <c>employeeType</c>); the mapper reads from these to derive <c>sys:</c> tags.</param>
/// <param name="Groups">The principal's group / role memberships as the directory names them (DNs, ids, or names) — a common source of team/role <c>sys:</c> tags.</param>
public sealed record DirectoryRecord(
    GranteeKind Kind,
    string Id,
    string? DisplayName,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes,
    IReadOnlyList<string> Groups)
{
    /// <summary>Gets the first value of an attribute, or <see langword="null"/> if it is absent or empty.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The first value, or <see langword="null"/>.</returns>
    public string? Attribute(string name)
        => this.Attributes.TryGetValue(name, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null;
}