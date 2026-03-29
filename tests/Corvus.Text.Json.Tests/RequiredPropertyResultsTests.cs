// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that the results collector correctly reports missing required properties.
/// </summary>
public class RequiredPropertyResultsTests
{
    [Fact]
    public void MissingRequiredProperty_AppearsInResults_Verbose()
    {
        // ClosedObjectNoPatterns has required: ["name"]. An empty object is missing "name".
        using var doc = ParsedJsonDocument<ClosedObjectNoPatterns>.Parse("{}");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);

        bool isValid = doc.RootElement.EvaluateSchema(collector);

        Assert.False(isValid, "An empty object should not be valid when 'name' is required");

        // Collect all results
        List<(bool IsMatch, string Message)> results = [];
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            results.Add((result.IsMatch, result.GetMessageText()));
        }

        // There must be at least one result about the missing required property
        Assert.Contains(
            results,
            r => !r.IsMatch && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingRequiredProperty_AppearsInResults_Detailed()
    {
        // Same test at Detailed level — the required failure should still be reported
        using var doc = ParsedJsonDocument<ClosedObjectNoPatterns>.Parse("{}");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        bool isValid = doc.RootElement.EvaluateSchema(collector);

        Assert.False(isValid, "An empty object should not be valid when 'name' is required");

        List<(bool IsMatch, string Message)> results = [];
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            results.Add((result.IsMatch, result.GetMessageText()));
        }

        Assert.Contains(
            results,
            r => !r.IsMatch && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingRequiredProperty_AppearsInResults_Basic()
    {
        // At Basic level, messages may be empty but failure results should still be present
        using var doc = ParsedJsonDocument<ClosedObjectNoPatterns>.Parse("{}");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        bool isValid = doc.RootElement.EvaluateSchema(collector);

        Assert.False(isValid, "An empty object should not be valid when 'name' is required");

        bool hasFailure = false;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (!result.IsMatch)
            {
                hasFailure = true;
                break;
            }
        }

        Assert.True(hasFailure, "Expected at least one failure result at Basic level for a missing required property");
    }

    [Fact]
    public void ValidDocument_StillReturnsTrue_WithCollector()
    {
        // Ensure the fix doesn't break valid document evaluation
        using var doc = ParsedJsonDocument<ClosedObjectNoPatterns>.Parse("""{"name": "Alice"}""");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        bool isValid = doc.RootElement.EvaluateSchema(collector);

        Assert.True(isValid, "A document with the required 'name' property should be valid");
    }
}