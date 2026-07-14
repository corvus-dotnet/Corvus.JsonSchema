// <copyright file="GoogleMembershipExpansionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Google.Tests;

/// <summary>
/// Unit coverage (no network) of the Google adapter's membership-fetch parse (design §16.5.4):
/// <see cref="GooglePrincipalDirectory.ParseGroupNames"/> reads a <c>groups.list?userKey=</c> response to the group
/// <c>email</c>s the mapper keys on. The full fetch + enrich is proved by the stub conformance suite; this locks the parse
/// in the fast gate.
/// </summary>
[TestClass]
public sealed class GoogleMembershipExpansionTests
{
    [TestMethod]
    public void ParseGroupNames_reads_the_group_emails()
    {
        byte[] body = Encoding.UTF8.GetBytes(
            """{"kind":"admin#directory#groups","groups":[{"id":"g1","email":"payments","name":"payments"},{"id":"g2","email":"billing","name":"billing"}]}""");

        GooglePrincipalDirectory.ParseGroupNames(body).ShouldBe(["payments", "billing"]);
    }

    [TestMethod]
    public void ParseGroupNames_with_no_groups_member_is_empty()
        => GooglePrincipalDirectory.ParseGroupNames("""{"kind":"admin#directory#groups"}"""u8.ToArray()).ShouldBeEmpty();

    [TestMethod]
    public void ParseGroupNames_on_an_empty_groups_array_is_empty()
        => GooglePrincipalDirectory.ParseGroupNames("""{"groups":[]}"""u8.ToArray()).ShouldBeEmpty();
}
