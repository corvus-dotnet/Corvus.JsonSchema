// <copyright file="DirectoryMembershipExpansionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Tests;

/// <summary>
/// Unit tests for <see cref="DirectoryPrincipalProjector.EnrichWithMemberships"/> — the membership-expansion foundation
/// (design §16.5.4). A person resolved from a directory must carry its <strong>full membership-expanded identity</strong>
/// (a <c>sys:group</c> / <c>sys:role</c> tag per membership) so the effective-access lookup surfaces the grants the person
/// inherits through its groups / roles. These cover both projection paths — the string mapper (LDAP-style) and the
/// bytes-to-bytes span mapper (the Keycloak / HTTP-JSON deployments use it, and its string <c>Map</c> throws, so the
/// enrichment MUST take the span path for it).
/// </summary>
[TestClass]
public sealed class DirectoryMembershipExpansionTests
{
    [TestMethod]
    public void Enrich_unions_a_group_membership_as_sys_group_on_the_string_path()
    {
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, ["arazzo-admins"], []);

        List<SecurityTag> tags = enriched.Identity.ToList();
        tags.ShouldContain(new SecurityTag("sys:sub", "alice"));
        tags.ShouldContain(new SecurityTag("sys:group", "arazzo-admins"));
        tags.ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "keycloak"));
    }

    [TestMethod]
    public void Enrich_unions_a_group_membership_as_sys_group_on_the_span_path()
    {
        // The demo / Keycloak shape: a span mapper whose string Map throws, so enrichment must project the synthetic team
        // through the bytes-to-bytes path.
        var projector = new DirectoryPrincipalProjector(KindSpanMapper(), "keycloak");
        ResolvedPrincipal alice = projector.TryProjectIdentity(GranteeKind.Person, "alice"u8, "alice"u8, hasLabel: true, new DirectoryRecordView(GranteeKind.Person, "alice"u8, default, default))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, ["arazzo-admins"], []);

        List<SecurityTag> tags = enriched.Identity.ToList();
        tags.ShouldContain(new SecurityTag("sys:sub", "alice"));
        tags.ShouldContain(new SecurityTag("sys:group", "arazzo-admins"));
        tags.ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, "keycloak"));
    }

    [TestMethod]
    public void Enrich_unions_a_role_membership_as_sys_role()
    {
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, [], ["approver"]);

        enriched.Identity.ToList().ShouldContain(new SecurityTag("sys:role", "approver"));
    }

    [TestMethod]
    public void The_bare_group_identity_is_a_subset_of_the_enriched_person_identity()
    {
        // The property the effective-access engine relies on: a person who belongs to arazzo-admins carries an identity that
        // CONTAINS the bare arazzo-admins team identity, so every grant the team matches (by membership) the person matches.
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;
        SecurityTagSet teamIdentity = projector.Project(new DirectoryRecord(GranteeKind.Team, "arazzo-admins", "arazzo-admins", Empty, []))!.Value.Identity;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, ["arazzo-admins"], []);

        teamIdentity.IsSubsetOf(enriched.Identity).ShouldBeTrue();
    }

    [TestMethod]
    public void Enrich_does_not_duplicate_the_shared_issuer()
    {
        // Both the person and the synthetic team are stamped the SAME sys:iss by the projector, so the union must carry it once.
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, ["arazzo-admins", "payments"], []);

        enriched.Identity.ToList().Count(t => t.Key == DirectoryIssuer.IssuerTagKey).ShouldBe(1);
    }

    [TestMethod]
    public void Enrich_unions_every_group_membership()
    {
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        List<SecurityTag> tags = projector.EnrichWithMemberships(alice, ["arazzo-admins", "payments", "sre"], []).Identity.ToList();

        tags.ShouldContain(new SecurityTag("sys:group", "arazzo-admins"));
        tags.ShouldContain(new SecurityTag("sys:group", "payments"));
        tags.ShouldContain(new SecurityTag("sys:group", "sre"));
    }

    [TestMethod]
    public void Enrich_with_no_memberships_returns_the_person_unchanged()
    {
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, [], []);

        enriched.Identity.SetEquals(alice.Identity).ShouldBeTrue();
    }

    [TestMethod]
    public void Enrich_skips_a_membership_the_mapper_drops()
    {
        // A membership name the mapper does not recognise (drops) contributes no tag — it is silently excluded, never an
        // inert tag on the identity.
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;

        ResolvedPrincipal enriched = projector.EnrichWithMemberships(alice, ["drop"], []);

        enriched.Identity.SetEquals(alice.Identity).ShouldBeTrue();
    }

    [TestMethod]
    public void Enrich_is_idempotent_for_a_membership_already_expanded()
    {
        // Re-expanding a person who already carries a group's tags adds nothing new (the union deduplicates), so the identity
        // is unchanged — enrichment is safe to apply more than once.
        var projector = new DirectoryPrincipalProjector(KindMapper(), "keycloak");
        ResolvedPrincipal alice = projector.Project(Person("alice"))!.Value;
        ResolvedPrincipal once = projector.EnrichWithMemberships(alice, ["arazzo-admins"], []);

        ResolvedPrincipal twice = projector.EnrichWithMemberships(once, ["arazzo-admins"], []);

        twice.Identity.SetEquals(once.Identity).ShouldBeTrue();
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Empty = new Dictionary<string, IReadOnlyList<string>>();

    // A string mapper that resolves each kind to its dimension (Person→sys:sub, Team→sys:group, Role→sys:role) and drops the
    // reserved id "drop"; the projector appends the issuer.
    private static IDirectoryIdentityMapper KindMapper()
        => DirectoryIdentityMapper.FromFunc(record => record.Id == "drop"
            ? null
            : record.Kind switch
            {
                GranteeKind.Person => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)])),
                GranteeKind.Team => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:group", record.Id)])),
                GranteeKind.Role => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:role", record.Id)])),
                _ => (ResolvedPrincipal?)null,
            });

    // The span (bytes-to-bytes) counterpart, matching the demo mapper's shape — Person→sys:sub, Team→sys:group,
    // Role→sys:role, dropping "drop". Its string Map throws, so enrichment exercises the span path.
    private static IDirectoryIdentityMapper KindSpanMapper()
        => DirectorySpanIdentityMapper.FromIdentity([], static (DirectoryRecordView record, ref IdentityBuilder identity) =>
        {
            if (record.ValueUtf8.SequenceEqual("drop"u8))
            {
                return false;
            }

            switch (record.Kind)
            {
                case GranteeKind.Person:
                    identity.Add("sys:sub"u8, record.ValueUtf8);
                    return true;
                case GranteeKind.Team:
                    identity.Add("sys:group"u8, record.ValueUtf8);
                    return true;
                case GranteeKind.Role:
                    identity.Add("sys:role"u8, record.ValueUtf8);
                    return true;
                default:
                    return false;
            }
        });

    private static DirectoryRecord Person(string id) => new(GranteeKind.Person, id, id, Empty, []);
}
