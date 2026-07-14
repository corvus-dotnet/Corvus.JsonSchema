// <copyright file="ScimUserGroupsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Scim.Tests;

/// <summary>
/// Unit coverage (no network) of the SCIM adapter's inline membership scan (design §16.5.4): a SCIM user carries its
/// group memberships in the read-only <c>groups</c> multi-valued attribute, and
/// <see cref="ScimPrincipalDirectory.ParseUserGroups"/> pairs each resource's grantee value with its group NAMES (member
/// <c>display</c>, falling back to <c>value</c>) in one in-place pass. The full expansion is exercised by the stub
/// conformance suite; this locks the pairing in the fast gate.
/// </summary>
[TestClass]
public sealed class ScimUserGroupsTests
{
    private static readonly byte[] ListResponse = Encoding.UTF8.GetBytes(
        """
        {
          "schemas": ["urn:ietf:params:scim:api:messages:2.0:ListResponse"],
          "totalResults": 2,
          "Resources": [
            {
              "id": "id-alice", "userName": "alice", "displayName": "Alice Smith",
              "groups": [
                {"value": "id-payments", "display": "payments", "$ref": "../Groups/id-payments"},
                {"value": "id-billing", "display": "billing"}
              ]
            },
            {
              "id": "id-bob", "userName": "bob", "displayName": "Bob Brown"
            }
          ]
        }
        """);

    [TestMethod]
    public void Pairs_each_user_value_with_its_group_display_names()
    {
        Dictionary<string, List<string>> groups = ScimPrincipalDirectory.ParseUserGroups(ListResponse, "userName");

        groups["alice"].ShouldBe(["payments", "billing"]);
        groups.ContainsKey("bob").ShouldBeFalse(); // bob has no groups, so no entry
    }

    [TestMethod]
    public void Falls_back_to_the_member_value_when_no_display()
    {
        byte[] body = Encoding.UTF8.GetBytes(
            """{"Resources":[{"userName":"carol","groups":[{"value":"grp-eng"}]}]}""");

        ScimPrincipalDirectory.ParseUserGroups(body, "userName")["carol"].ShouldBe(["grp-eng"]);
    }

    [TestMethod]
    public void A_response_with_no_groups_yields_no_entries()
    {
        byte[] body = Encoding.UTF8.GetBytes(
            """{"Resources":[{"userName":"dave","displayName":"Dave"}]}""");

        ScimPrincipalDirectory.ParseUserGroups(body, "userName").ShouldBeEmpty();
    }
}
