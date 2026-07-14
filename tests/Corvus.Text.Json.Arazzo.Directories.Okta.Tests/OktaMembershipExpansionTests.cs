// <copyright file="OktaMembershipExpansionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Okta.Tests;

/// <summary>
/// Unit coverage (no network) of the Okta adapter's membership-expansion parse seams (design §16.5.4): the users projection
/// captures each user's id in step with the resolved principal (so the adapter can fetch that user's groups), and
/// <see cref="OktaPrincipalDirectory.ParseGroupNames"/> reads a <c>/users/{id}/groups</c> response to the group
/// <c>profile.name</c>s the mapper keys on. The full fetch + enrich is proved by the stub conformance suite; these lock the
/// pure parse pieces in the fast gate.
/// </summary>
[TestClass]
public sealed class OktaMembershipExpansionTests
{
    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"id-alice","status":"ACTIVE","profile":{"login":"alice","department":"acme"}},
          {"id":"id-bob","status":"ACTIVE","profile":{"login":"bob","department":"globex"}}
        ]
        """);

    private static readonly byte[] GroupsBody = Encoding.UTF8.GetBytes(
        """[{"id":"id-payments","profile":{"name":"payments"}},{"id":"id-billing","profile":{"name":"billing"}}]""");

    [TestMethod]
    public void Span_projection_captures_each_user_id_parallel_to_the_principal()
    {
        var ids = new List<string>();

        IReadOnlyList<ResolvedPrincipal> people = OktaPrincipalDirectory.ProjectResponseSpan(
            GranteeKind.Person, OktaResource.Users, UsersBody, 10, SpanProjector, ids);

        people.Select(p => p.Value).ShouldBe(["alice", "bob"]);
        ids.ShouldBe(["id-alice", "id-bob"]);
    }

    [TestMethod]
    public void String_projection_captures_each_user_id_parallel_to_the_principal()
    {
        var ids = new List<string>();

        IReadOnlyList<ResolvedPrincipal> people = OktaPrincipalDirectory.ProjectResponse(
            GranteeKind.Person, OktaResource.Users, UsersBody, 10, StringProjector, ids);

        people.Select(p => p.Value).ShouldBe(["alice", "bob"]);
        ids.ShouldBe(["id-alice", "id-bob"]);
    }

    [TestMethod]
    public void ParseGroupNames_reads_the_group_profile_names()
        => OktaPrincipalDirectory.ParseGroupNames(GroupsBody).ShouldBe(["payments", "billing"]);

    [TestMethod]
    public void ParseGroupNames_on_an_empty_array_is_empty()
        => OktaPrincipalDirectory.ParseGroupNames("[]"u8.ToArray()).ShouldBeEmpty();

    private static readonly DirectoryPrincipalProjector SpanProjector = new(
        DirectorySpanIdentityMapper.FromIdentity([], static (DirectoryRecordView record, ref IdentityBuilder identity) =>
        {
            identity.Add("sys:sub"u8, record.ValueUtf8);
            return true;
        }),
        "okta");

    private static readonly DirectoryPrincipalProjector StringProjector = new(
        DirectoryIdentityMapper.FromFunc(static record =>
            new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)]))),
        "okta");
}
