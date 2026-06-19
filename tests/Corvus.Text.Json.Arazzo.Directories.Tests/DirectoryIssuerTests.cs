// <copyright file="DirectoryIssuerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Tests;

/// <summary>
/// Unit tests for the issuer dimension (design §16.5.4): <see cref="DirectoryIssuer.Stamp"/> and the
/// <see cref="DirectoryPrincipalProjector"/> that makes every adapter stamp it. These cover the security-critical
/// "unforgeable / cross-provider-disjoint" guarantees the container conformance suites do not exercise (their mappers
/// never supply a competing <c>sys:iss</c>).
/// </summary>
[TestClass]
public sealed class DirectoryIssuerTests
{
    [TestMethod]
    public void Stamp_adds_the_issuer_tag_to_an_identity()
    {
        SecurityTagSet stamped = DirectoryIssuer.Stamp(
            SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme"), new SecurityTag("sys:sub", "alice")]),
            "corp-ldap");

        List<SecurityTag> tags = stamped.ToList();
        tags.Count.ShouldBe(3);
        tags.ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "corp-ldap"));
    }

    [TestMethod]
    public void Stamp_overrides_a_mapper_supplied_issuer_so_it_cannot_be_forged()
    {
        SecurityTagSet stamped = DirectoryIssuer.Stamp(
            SecurityTagSet.FromTags([new SecurityTag("sys:sub", "alice"), new SecurityTag(DirectoryIssuer.IssuerTagKey, "forged")]),
            "corp-ldap");

        List<SecurityTag> tags = stamped.ToList();
        tags.Count(t => t.Key == DirectoryIssuer.IssuerTagKey).ShouldBe(1);
        tags.ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "corp-ldap"));
        tags.ShouldNotContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "forged"));
    }

    [TestMethod]
    public void Stamp_on_an_empty_identity_yields_just_the_issuer()
        => DirectoryIssuer.Stamp(SecurityTagSet.Empty, "kc").ToList().ShouldBe([new SecurityTag(DirectoryIssuer.IssuerTagKey, "kc")]);

    [TestMethod]
    public void Stamp_requires_a_non_empty_issuer()
        => Should.Throw<ArgumentException>(() => DirectoryIssuer.Stamp(SecurityTagSet.Empty, string.Empty));

    [TestMethod]
    public void Two_issuers_never_produce_set_equal_identities_for_the_same_principal()
    {
        SecurityTagSet ldapAlice = DirectoryIssuer.Stamp(SecurityTagSet.FromTags([new SecurityTag("sys:sub", "alice")]), "corp-ldap");
        SecurityTagSet keycloakAlice = DirectoryIssuer.Stamp(SecurityTagSet.FromTags([new SecurityTag("sys:sub", "alice")]), "keycloak");

        // The two providers' "alice" identities differ only by sys:iss, so set-equality (the authorization comparison) is
        // false — a grant to one never admits the other.
        WorkflowIdentity.SameAdministrator(ldapAlice, keycloakAlice).ShouldBeFalse();
    }

    [TestMethod]
    public void Projector_maps_then_stamps_the_issuer()
    {
        var projector = new DirectoryPrincipalProjector(SubMapper(), "keycloak");

        ResolvedPrincipal? mapped = projector.Project(Record("alice"));

        mapped.ShouldNotBeNull();
        mapped!.Value.Identity.ToList().ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "keycloak"));
    }

    [TestMethod]
    public void Projector_drops_records_the_mapper_drops()
        => new DirectoryPrincipalProjector(SubMapper(), "keycloak").Project(Record("drop")).ShouldBeNull();

    // A mapper that drops records whose id is "drop" and otherwise resolves to a single sys:sub tag (no issuer — the
    // projector is responsible for that).
    private static IDirectoryIdentityMapper SubMapper()
        => DirectoryIdentityMapper.FromFunc(record => record.Id == "drop"
            ? null
            : new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)])));

    private static DirectoryRecord Record(string id)
        => new(GranteeKind.Person, id, id, new Dictionary<string, IReadOnlyList<string>>(), []);
}