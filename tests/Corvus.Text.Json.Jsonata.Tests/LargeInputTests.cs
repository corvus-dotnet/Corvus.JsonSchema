// <copyright file="LargeInputTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests JSONata evaluation with large JSON inputs to verify
/// no OOM errors and reasonable performance.
/// </summary>
public class LargeInputTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    [Fact]
    public void SumLargeArray()
    {
        // Build a 100K-element array
        var sb = new StringBuilder();
        sb.Append("""{"items":[""");
        for (int i = 0; i < 100_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append("]}");

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Evaluator.Evaluate(
            "$sum(items)",
            doc.RootElement,
            workspace);

        // Sum of 0..99999 = (99999 * 100000) / 2 = 4999950000
        Assert.Equal(4_999_950_000.0, result.GetDouble());
    }

    [Fact]
    public void FilterLargeArray()
    {
        var sb = new StringBuilder();
        sb.Append("""{"items":[""");
        for (int i = 0; i < 10_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append("]}");

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Evaluator.Evaluate(
            "$count($filter(items, function($v) { $v >= 5000 }))",
            doc.RootElement,
            workspace);

        Assert.Equal(5000.0, result.GetDouble());
    }

    [Fact]
    public void MapLargeArray()
    {
        var sb = new StringBuilder();
        sb.Append("""{"items":[""");
        for (int i = 0; i < 10_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append("]}");

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Evaluator.Evaluate(
            "$count($map(items, function($v) { $v * 2 }))",
            doc.RootElement,
            workspace);

        Assert.Equal(10_000.0, result.GetDouble());
    }

    [Fact]
    public void DeeplyNestedObjectNavigation()
    {
        // Build a 50-level nested object: {"a":{"a":{"a":...{"a":42}...}}}
        var sb = new StringBuilder();
        const int depth = 50;
        for (int i = 0; i < depth; i++)
        {
            sb.Append("""{"a":""");
        }

        sb.Append("42");
        sb.Append(new string('}', depth));

        // Build the path expression: a.a.a...a (50 levels)
        string path = string.Join(".", Enumerable.Repeat("a", depth));

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Evaluator.Evaluate(
            path,
            doc.RootElement,
            workspace);

        Assert.Equal(42.0, result.GetDouble());
    }

    [Fact]
    public void LargeObjectPropertyAccess()
    {
        // Build an object with 1000 properties
        var sb = new StringBuilder();
        sb.Append('{');
        for (int i = 0; i < 1000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"prop{i}\":{i}");
        }

        sb.Append('}');

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Access the last property
        JsonElement result = Evaluator.Evaluate(
            "prop999",
            doc.RootElement,
            workspace);

        Assert.Equal(999.0, result.GetDouble());
    }

    [Fact]
    public void LargeArrayOfObjects()
    {
        // Build 10K objects with multiple properties
        var sb = new StringBuilder();
        sb.Append("""{"employees":[""");
        for (int i = 0; i < 10_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($$$"""{"id":{{{i}}},"name":"emp{{{i}}}","salary":{{{i * 100}}}}""");
        }

        sb.Append("]}");

        byte[] json = Encoding.UTF8.GetBytes(sb.ToString());
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Sum all salaries: 0*100 + 1*100 + ... + 9999*100 = 100 * (9999*10000/2) = 4999500000
        JsonElement result = Evaluator.Evaluate(
            "$sum(employees.salary)",
            doc.RootElement,
            workspace);

        Assert.Equal(4_999_500_000.0, result.GetDouble());
    }
}
