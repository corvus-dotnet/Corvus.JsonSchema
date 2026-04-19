// <copyright file="GroupByAndArrayEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for <c>$group</c> (implicit grouping via <c>x{key:val}</c> syntax),
/// array wrapping, keep-array semantics, and CG helper arithmetic/string paths.
/// </summary>
public class GroupByAndArrayEdgeCaseTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ─── Grouping / $group ───────────────────────────────────────────

    [Fact]
    public void GroupBy_GroupsItemsByCategory()
    {
        // JSONata grouping: array{key: values} groups array elements by key
        string? result = Evaluator.EvaluateToString(
            """Account.Order.Product{`Product Name`: Price}""",
            """
            {
              "Account": {
                "Order": [
                  {"Product": {"Product Name": "Widget A", "Price": 10}},
                  {"Product": {"Product Name": "Widget B", "Price": 20}},
                  {"Product": {"Product Name": "Widget A", "Price": 30}}
                ]
              }
            }
            """);
        Assert.NotNull(result);
        // Should produce an object with "Widget A" and "Widget B" keys
        Assert.Contains("Widget A", result);
        Assert.Contains("Widget B", result);
    }

    [Fact]
    public void GroupBy_NonObjectElementsInSequence_SkipsNonObjects()
    {
        // When the sequence contains a mix of objects and non-objects,
        // the groupby should skip non-object elements gracefully.
        string? result = Evaluator.EvaluateToString(
            """items{name: value}""",
            """{"items": [{"name": "a", "value": 1}, 42, {"name": "b", "value": 2}]}""");
        Assert.NotNull(result);
        Assert.Contains("a", result);
        Assert.Contains("b", result);
    }

    [Fact]
    public void GroupBy_EmptyInput_ReturnsUndefined()
    {
        string? result = Evaluator.EvaluateToString(
            """missing{key: value}""",
            """{"other": 1}""");
        Assert.Null(result);
    }

    // ─── Array wrapping / keep-array ─────────────────────────────────

    [Fact]
    public void SingleElementArrayAccess_ReturnsSingleElement()
    {
        string? result = Evaluator.EvaluateToString(
            """items[0]""",
            """{"items": [42]}""");
        Assert.Equal("42", result);
    }

    [Fact]
    public void FlattenProjection_SingleItem_ReturnsArray()
    {
        string? result = Evaluator.EvaluateToString(
            """items[].name""",
            """{"items": [{"name": "a"}]}""");
        Assert.Equal("[\"a\"]", result);
    }

    [Fact]
    public void AppendToUndefined_ReturnsValue()
    {
        string? result = Evaluator.EvaluateToString(
            """$append(missing, 1)""",
            """{}""");
        Assert.Equal("1", result);
    }

    [Fact]
    public void AppendUndefined_ReturnsOriginal()
    {
        string? result = Evaluator.EvaluateToString(
            """$append([1,2], missing)""",
            """{}""");
        Assert.Equal("[1,2]", result);
    }

    // ─── CG arithmetic paths (exercised via evaluator) ───────────────

    [Theory]
    [InlineData("2 + 3", "5")]
    [InlineData("10 - 3", "7")]
    [InlineData("4 * 5", "20")]
    [InlineData("15 / 3", "5")]
    [InlineData("17 % 5", "2")]
    public void Arithmetic_BasicOperations(string expression, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, "{}");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Arithmetic_UnaryNegation()
    {
        string? result = Evaluator.EvaluateToString("-5", "{}");
        Assert.Equal("-5", result);
    }

    [Theory]
    [InlineData("1 = 1", "true")]
    [InlineData("1 != 2", "true")]
    [InlineData("1 < 2", "true")]
    [InlineData("2 > 1", "true")]
    [InlineData("1 <= 1", "true")]
    [InlineData("1 >= 1", "true")]
    public void Comparison_BasicOperations(string expression, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, "{}");
        Assert.Equal(expected, result);
    }

    // ─── CG string operations ────────────────────────────────────────

    [Fact]
    public void StringConcatenation_Ampersand()
    {
        string? result = Evaluator.EvaluateToString("\"hello\" & \" \" & \"world\"", "{}");
        Assert.Equal("\"hello world\"", result);
    }

    [Fact]
    public void Pad_Right()
    {
        // Positive width = right-pad
        string? result = Evaluator.EvaluateToString("""$pad("42", 5, "0")""", "{}");
        Assert.Equal("\"42000\"", result);
    }

    [Fact]
    public void Pad_Left()
    {
        // Negative width = left-pad
        string? result = Evaluator.EvaluateToString("""$pad("42", -5, "0")""", "{}");
        Assert.Equal("\"00042\"", result);
    }

    [Fact]
    public void FormatBase_Hex()
    {
        string? result = Evaluator.EvaluateToString("""$formatBase(255, 16)""", "{}");
        Assert.Equal("\"ff\"", result);
    }

    [Fact]
    public void FormatBase_Binary()
    {
        string? result = Evaluator.EvaluateToString("""$formatBase(10, 2)""", "{}");
        Assert.Equal("\"1010\"", result);
    }

    // ─── Spread / property chain navigation ──────────────────────────

    [Fact]
    public void DeepNavigation_MultiLevel()
    {
        string? result = Evaluator.EvaluateToString(
            "a.b.c",
            """{"a":{"b":{"c":42}}}""");
        Assert.Equal("42", result);
    }

    [Fact]
    public void DeepNavigation_MissingIntermediate_ReturnsUndefined()
    {
        string? result = Evaluator.EvaluateToString(
            "a.b.c",
            """{"a":{"x":1}}""");
        Assert.Null(result);
    }

    [Fact]
    public void WildcardDescendant()
    {
        string? result = Evaluator.EvaluateToString(
            "**.price",
            """{"order":{"items":[{"price":10},{"price":20}]}}""");
        Assert.NotNull(result);
        Assert.Contains("10", result);
        Assert.Contains("20", result);
    }
}
