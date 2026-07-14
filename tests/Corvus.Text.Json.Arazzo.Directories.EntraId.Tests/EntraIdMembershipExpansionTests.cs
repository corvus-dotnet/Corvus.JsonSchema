// <copyright file="EntraIdMembershipExpansionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.EntraId.Tests;

/// <summary>
/// Unit coverage (no network) of the EntraId adapter's membership-fetch parse (design §16.5.4):
/// <see cref="EntraIdPrincipalDirectory.ParseGroupNames"/> reads a Graph <c>/users/{id}/memberOf</c> response to the GROUP
/// displayNames, filtering out directory-role and administrative-unit members by their <c>@odata.type</c> so only real
/// group memberships become <c>sys:group</c> tags. The full fetch + enrich is proved by the stub conformance suite; this
/// locks the type-filtered parse in the fast gate.
/// </summary>
[TestClass]
public sealed class EntraIdMembershipExpansionTests
{
    [TestMethod]
    public void ParseGroupNames_reads_only_group_typed_members()
    {
        // A memberOf mixing a group, a directory role, and an administrative unit — only the group contributes.
        byte[] body = Encoding.UTF8.GetBytes(
            """
            {"@odata.context":"…/$metadata","value":[
              {"@odata.type":"#microsoft.graph.group","id":"g1","displayName":"payments"},
              {"@odata.type":"#microsoft.graph.directoryRole","id":"r1","displayName":"Global Administrator"},
              {"@odata.type":"#microsoft.graph.group","id":"g2","displayName":"billing"},
              {"@odata.type":"#microsoft.graph.administrativeUnit","id":"a1","displayName":"West"}
            ]}
            """);

        EntraIdPrincipalDirectory.ParseGroupNames(body).ShouldBe(["payments", "billing"]);
    }

    [TestMethod]
    public void ParseGroupNames_skips_an_untyped_member()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"value":[{"id":"x","displayName":"mystery"}]}""");

        EntraIdPrincipalDirectory.ParseGroupNames(body).ShouldBeEmpty();
    }

    [TestMethod]
    public void ParseGroupNames_on_an_empty_value_array_is_empty()
        => EntraIdPrincipalDirectory.ParseGroupNames("""{"value":[]}"""u8.ToArray()).ShouldBeEmpty();
}
