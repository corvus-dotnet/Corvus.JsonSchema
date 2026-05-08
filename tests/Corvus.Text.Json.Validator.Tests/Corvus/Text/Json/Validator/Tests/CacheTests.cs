// <copyright file="CacheTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for the caching behaviour of <see cref="JsonSchema"/>.
/// </summary>
[TestClass]
public class CacheTests
{
    [TestMethod]
    public void FromText_SameSchema_ReturnsCachedInstance()
    {
        string schemaJson =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/cache-same",
              "type": "string"
            }
            """;

        var first = JsonSchema.FromText(schemaJson);
        var second = JsonSchema.FromText(schemaJson);

        // Both should validate identically (cached pipeline)
        Assert.IsTrue(first.Validate("\"hello\""));
        Assert.IsTrue(second.Validate("\"hello\""));
    }

    [TestMethod]
    public void FromText_RefreshCache_RecompilesSchema()
    {
        string schemaJson =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/cache-refresh",
              "type": "string"
            }
            """;

        var first = JsonSchema.FromText(schemaJson);
        Assert.IsTrue(first.Validate("\"hello\""));

        // Refresh the cache — this should not throw and should produce a working schema
        var refreshed = JsonSchema.FromText(schemaJson, refreshCache: true);
        Assert.IsTrue(refreshed.Validate("\"hello\""));
    }

    [TestMethod]
    public void FromText_DifferentAlwaysAssertFormat_UsesSeparateCacheEntries()
    {
        string schemaJson =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/cache-format-key",
              "type": "string",
              "format": "email"
            }
            """;

        JsonSchema.Options assertFormat = new(alwaysAssertFormat: true);
        JsonSchema.Options annotateFormat = new(alwaysAssertFormat: false);

        var asserting = JsonSchema.FromText(schemaJson, options: assertFormat);
        var annotating = JsonSchema.FromText(schemaJson, options: annotateFormat);

        // "not-an-email" should fail when format is asserted, pass when annotated
        Assert.IsFalse(asserting.Validate("\"not-an-email\""));
        Assert.IsTrue(annotating.Validate("\"not-an-email\""));
    }

    [TestMethod]
    public void FromFile_SameFile_ReturnsCachedInstance()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "simple-string.json");

        var first = JsonSchema.FromFile(schemaPath);
        var second = JsonSchema.FromFile(schemaPath);

        Assert.IsTrue(first.Validate("\"hello\""));
        Assert.IsTrue(second.Validate("\"hello\""));
    }

    [TestMethod]
    public void FromFile_RefreshCache_Recompiles()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "simple-integer.json");

        var first = JsonSchema.FromFile(schemaPath);
        Assert.IsTrue(first.Validate("50"));

        var refreshed = JsonSchema.FromFile(schemaPath, refreshCache: true);
        Assert.IsTrue(refreshed.Validate("50"));
    }
}