// <copyright file="KeycloakMembershipExpansionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak.Tests;

/// <summary>
/// Unit coverage (no container) of the Keycloak adapter's membership-expansion parse seams (design §16.5.4): the users
/// projection now captures each user's uuid in step with the resolved principal (so the adapter can fetch that user's
/// groups), and <see cref="KeycloakPrincipalDirectory.ParseGroupNames"/> reads a group-membership response to the bare group
/// names the mapper keys on. The full fetch + enrich is proved end-to-end by the container conformance suite and the live
/// sample; these lock the pure parse pieces in the fast gate.
/// </summary>
[TestClass]
public sealed class KeycloakMembershipExpansionTests
{
    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"uuid-alice","username":"alice","enabled":true,"firstName":"Alice","lastName":"Smith"},
          {"id":"uuid-bob","username":"bob","enabled":true,"firstName":"Bob","lastName":"Brown"}
        ]
        """);

    // A live-shaped group-membership response ([{id,name,path,subGroups}]) — the exact shape /users/{id}/groups returns.
    private static readonly byte[] GroupsBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"g1","name":"arazzo-admins","path":"/arazzo-admins","subGroups":[]},
          {"id":"g2","name":"payments","path":"/payments","subGroups":[]}
        ]
        """);

    [TestMethod]
    public void Span_projection_captures_each_user_uuid_parallel_to_the_principal()
    {
        var ids = new List<string>();

        IReadOnlyList<ResolvedPrincipal> people = KeycloakPrincipalDirectory.ProjectResponseSpan(
            GranteeKind.Person, KeycloakResource.Users, UsersBody, 10, SpanProjector, ids);

        people.Select(p => p.Value).ShouldBe(["alice", "bob"]);
        ids.ShouldBe(["uuid-alice", "uuid-bob"]);
    }

    [TestMethod]
    public void String_projection_captures_each_user_uuid_parallel_to_the_principal()
    {
        var ids = new List<string>();

        IReadOnlyList<ResolvedPrincipal> people = KeycloakPrincipalDirectory.ProjectResponse(
            GranteeKind.Person, KeycloakResource.Users, UsersBody, 10, StringProjector, ids);

        people.Select(p => p.Value).ShouldBe(["alice", "bob"]);
        ids.ShouldBe(["uuid-alice", "uuid-bob"]);
    }

    [TestMethod]
    public void Span_projection_without_capture_is_unchanged()
        => KeycloakPrincipalDirectory.ProjectResponseSpan(GranteeKind.Person, KeycloakResource.Users, UsersBody, 10, SpanProjector)
            .Select(p => p.Value).ShouldBe(["alice", "bob"]);

    [TestMethod]
    public void ParseGroupNames_reads_the_bare_group_names()
        => KeycloakPrincipalDirectory.ParseGroupNames(GroupsBody).ShouldBe(["arazzo-admins", "payments"]);

    [TestMethod]
    public void ParseGroupNames_on_an_empty_array_is_empty()
        => KeycloakPrincipalDirectory.ParseGroupNames("[]"u8.ToArray()).ShouldBeEmpty();

    [TestMethod]
    public void ParseGroupNames_on_a_non_array_is_empty()
        => KeycloakPrincipalDirectory.ParseGroupNames("{}"u8.ToArray()).ShouldBeEmpty();

    // The bytes-to-bytes mapper (the demo shape): Person → sys:sub from the username.
    private static readonly DirectoryPrincipalProjector SpanProjector = new(
        DirectorySpanIdentityMapper.FromIdentity([], static (DirectoryRecordView record, ref IdentityBuilder identity) =>
        {
            identity.Add("sys:sub"u8, record.ValueUtf8);
            return true;
        }),
        "arazzo-keycloak");

    private static readonly DirectoryPrincipalProjector StringProjector = new(
        DirectoryIdentityMapper.FromFunc(static record =>
            new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)]))),
        "arazzo-keycloak");
}
