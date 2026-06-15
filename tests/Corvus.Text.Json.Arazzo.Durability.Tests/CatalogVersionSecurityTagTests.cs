// <copyright file="CatalogVersionSecurityTagTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the catalog-version security-tag data model (§14.2 slice 1): KVP labels round-trip through the
/// CTJ <see cref="CatalogVersion"/> document, distinct from the free-form user tags, and are absent-tolerant
/// for versions catalogued before security tags existed.
/// </summary>
[TestClass]
public sealed class CatalogVersionSecurityTagTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Security_tags_round_trip_through_the_catalog_version_document()
    {
        SecurityTag[] security = [new("tenant", "acme"), new("team", "payments")];
        CatalogVersion version = CatalogVersion.Create(
            "orders", 1, "orders-v1", "Orders", null, CatalogStatus.Active,
            tags: TagSet.FromTags(["nightly"]),
            owner: new CatalogOwner("Team", "team@example.com"),
            sources: SourceSet.FromSources([new CatalogSourceRef("petstore", "openapi")]),
            hash: "abc", createdBy: "ops", createdAt: CreatedAt,
            securityTags: SecurityTagSet.FromTags(security));

        CatalogVersion roundTripped = CatalogVersion.FromJson(version.ToJsonBytes());

        roundTripped.SecurityTagsValue.ToList().ShouldBe(security);
        roundTripped.TagsValue.ToList().ShouldBe(["nightly"]);
    }

    [TestMethod]
    public void A_version_with_no_security_tags_reports_an_empty_list()
    {
        CatalogVersion version = CatalogVersion.Create(
            "orders", 1, "orders-v1", "Orders", null, CatalogStatus.Active,
            tags: TagSet.FromTags(["nightly"]),
            owner: new CatalogOwner("Team", "team@example.com"),
            sources: SourceSet.FromSources([new CatalogSourceRef("petstore", "openapi")]),
            hash: "abc", createdBy: "ops", createdAt: CreatedAt);

        CatalogVersion.FromJson(version.ToJsonBytes()).SecurityTagsValue.ToList().ShouldBeEmpty();
    }
}