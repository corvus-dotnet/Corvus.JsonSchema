// <copyright file="DirectoryFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories.Conformance;

/// <summary>
/// The fixed set of principals every <see cref="IPrincipalDirectory"/> conformance run expects to be resolvable. This is
/// the <em>expected output</em> (kind / value / label / <c>sys:</c> identity); each backend's test provisions its
/// directory with entries that, under that provider's schema, yield exactly these via the adapter's configured
/// <see cref="IDirectoryIdentityMapper"/>. People alice + albert share a tenant (so a value-prefix search returns both);
/// bob is a second tenant; two teams and two roles exercise the other kinds.
/// </summary>
public static class DirectoryFixture
{
    /// <summary>A person in tenant <c>acme</c>; shares the <c>al</c> value prefix with <see cref="Albert"/>.</summary>
    public static readonly ExpectedPrincipal Alice = new(GranteeKind.Person, "alice", "Alice Smith", Identity(("sys:tenant", "acme"), ("sys:sub", "alice")));

    /// <summary>A second person in tenant <c>acme</c> sharing the <c>al</c> value prefix.</summary>
    public static readonly ExpectedPrincipal Albert = new(GranteeKind.Person, "albert", "Albert Jones", Identity(("sys:tenant", "acme"), ("sys:sub", "albert")));

    /// <summary>A person in a different tenant (<c>globex</c>).</summary>
    public static readonly ExpectedPrincipal Bob = new(GranteeKind.Person, "bob", "Bob Brown", Identity(("sys:tenant", "globex"), ("sys:sub", "bob")));

    /// <summary>A team.</summary>
    public static readonly ExpectedPrincipal Payments = new(GranteeKind.Team, "payments", "payments", Identity(("sys:team", "payments")));

    /// <summary>A second team.</summary>
    public static readonly ExpectedPrincipal Billing = new(GranteeKind.Team, "billing", "billing", Identity(("sys:team", "billing")));

    /// <summary>A role.</summary>
    public static readonly ExpectedPrincipal WorkflowAdmin = new(GranteeKind.Role, "workflow-admin", "workflow-admin", Identity(("sys:role", "workflow-admin")));

    /// <summary>A second role.</summary>
    public static readonly ExpectedPrincipal Viewer = new(GranteeKind.Role, "viewer", "viewer", Identity(("sys:role", "viewer")));

    /// <summary>Every fixture principal.</summary>
    public static readonly IReadOnlyList<ExpectedPrincipal> All = [Alice, Albert, Bob, Payments, Billing, WorkflowAdmin, Viewer];

    /// <summary>
    /// The issuer every conformance adapter is configured with. Each expected identity carries it as <c>sys:iss</c>
    /// (auto-appended by <see cref="Identity"/>); a concrete adapter test sets its options' <c>Issuer</c> to this, so the
    /// suite proves the adapter stamps the configured issuer onto every resolved principal (a missing or wrong stamp
    /// fails set-equality).
    /// </summary>
    public const string Issuer = "conformance";

    /// <summary>Builds a <c>sys:</c> identity tag set from (key, value) pairs, with the conformance <c>sys:iss</c> appended (the adapter is expected to stamp it).</summary>
    /// <param name="tags">The tag pairs (excluding the issuer, which is appended automatically).</param>
    /// <returns>The tag set.</returns>
    public static SecurityTagSet Identity(params (string Key, string Value)[] tags)
        => SecurityTagSet.FromTags([.. tags.Select(t => new SecurityTag(t.Key, t.Value)), new SecurityTag(DirectoryIssuer.IssuerTagKey, Issuer)]);
}

/// <summary>One principal the conformance suite expects an adapter to resolve.</summary>
/// <param name="Kind">The grantee kind.</param>
/// <param name="Value">The grantee value (the prefix-searched key).</param>
/// <param name="Label">The expected display label.</param>
/// <param name="Identity">The expected exact <c>sys:</c> identity.</param>
public sealed record ExpectedPrincipal(GranteeKind Kind, string Value, string Label, SecurityTagSet Identity);