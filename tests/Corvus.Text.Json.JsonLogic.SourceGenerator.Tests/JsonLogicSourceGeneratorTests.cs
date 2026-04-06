// <copyright file="JsonLogicSourceGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Integration tests for the JsonLogic source generator.
/// Each test exercises a source-generated evaluator by providing data and asserting on the result.
/// </summary>
public class JsonLogicSourceGeneratorTests
{
    [Fact]
    public void AddRule_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":3,"b":4}""");
        JsonElement result = AddRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);

        // 3 + 4 = 7
        Assert.Equal("7", result.GetRawText());
    }

    [Fact]
    public void AddRule_WithDecimalValues()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1.5,"b":2.5}""");
        JsonElement result = AddRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);

        // 1.5 + 2.5 = 4
        Assert.Equal("4", result.GetRawText());
    }

    [Fact]
    public void IfRule_WhenTrue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"age":25}""");
        JsonElement result = IfRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("adult", result.GetString());
    }

    [Fact]
    public void IfRule_WhenFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"age":12}""");
        JsonElement result = IfRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("minor", result.GetString());
    }

    [Fact]
    public void CatRule_ConcatenatesStrings()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"World"}""");
        JsonElement result = CatRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("Hello, World!", result.GetString());
    }

    [Fact]
    public void FilterRule_FiltersPositiveNumbers()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"items":[-1,0,1,2,-3,4]}""");
        JsonElement result = FilterRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal("[1,2,4]", result.GetRawText());
    }

    [Fact]
    public void MissingRule_ReturnsAllWhenNonePresent()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{}""");
        JsonElement result = MissingRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);

        // All three keys are missing
        Assert.Equal("""["a","b","c"]""", result.GetRawText());
    }

    [Fact]
    public void MissingRule_ReturnsOnlyMissing()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"c":3}""");
        JsonElement result = MissingRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);

        // Only "b" is missing
        Assert.Equal("""["b"]""", result.GetRawText());
    }

    [Fact]
    public void MissingRule_ReturnsEmptyWhenAllPresent()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2,"c":3}""");
        JsonElement result = MissingRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);

        // None are missing
        Assert.Equal("[]", result.GetRawText());
    }

    // ─── Custom Operator Tests ──────────────────────────────────

    [Fact]
    public void CustomOpRule_DoublesAndAdds()
    {
        // Rule: {"+":[{"double_it":[{"var":"x"}]}, {"var":"y"}]}
        // double_it(5) + 3 = 10 + 3 = 13
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":5,"y":3}""");
        JsonElement result = CustomOpRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal("13", result.GetRawText());
    }

    [Fact]
    public void CustomOpRule_WithZero()
    {
        // double_it(0) + 7 = 0 + 7 = 7
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":0,"y":7}""");
        JsonElement result = CustomOpRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal("7", result.GetRawText());
    }

    [Fact]
    public void CustomOpRule_WithDecimals()
    {
        // double_it(1.5) + 2.5 = 3 + 2.5 = 5.5
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"x":1.5,"y":2.5}""");
        JsonElement result = CustomOpRule.Evaluate(doc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);

        // BigNumber arithmetic may produce scientific notation
        Assert.True(
            result.GetRawText() == "5.5" || result.GetRawText() == "55E-1",
            $"Expected 5.5 or 55E-1 but got {result.GetRawText()}");
    }
}