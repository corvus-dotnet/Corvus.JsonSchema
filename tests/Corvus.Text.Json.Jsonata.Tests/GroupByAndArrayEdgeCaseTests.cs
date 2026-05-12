// <copyright file="GroupByAndArrayEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for <c>$group</c> (implicit grouping via <c>x{key:val}</c> syntax),
/// array wrapping, keep-array semantics, and CG helper arithmetic/string paths.
/// </summary>
[TestClass]
public class GroupByAndArrayEdgeCaseTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ─── Grouping / $group ───────────────────────────────────────────

    [TestMethod]
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
        Assert.IsNotNull(result);
        // Should produce an object with "Widget A" and "Widget B" keys
        StringAssert.Contains(result, "Widget A");
        StringAssert.Contains(result, "Widget B");
    }

    [TestMethod]
    public void GroupBy_NonObjectElementsInSequence_SkipsNonObjects()
    {
        // When the sequence contains a mix of objects and non-objects,
        // the groupby should skip non-object elements gracefully.
        string? result = Evaluator.EvaluateToString(
            """items{name: value}""",
            """{"items": [{"name": "a", "value": 1}, 42, {"name": "b", "value": 2}]}""");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "a");
        StringAssert.Contains(result, "b");
    }

    [TestMethod]
    public void GroupBy_EmptyInput_ReturnsEmptyObject()
    {
        string? result = Evaluator.EvaluateToString(
            """missing{key: value}""",
            """{"other": 1}""");
        // Reference returns {} for group-by when navigation yields undefined
        Assert.AreEqual("{}", result);
    }

    // ─── Array wrapping / keep-array ─────────────────────────────────

    [TestMethod]
    public void SingleElementArrayAccess_ReturnsSingleElement()
    {
        string? result = Evaluator.EvaluateToString(
            """items[0]""",
            """{"items": [42]}""");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void FlattenProjection_SingleItem_ReturnsArray()
    {
        string? result = Evaluator.EvaluateToString(
            """items[].name""",
            """{"items": [{"name": "a"}]}""");
        Assert.AreEqual("[\"a\"]", result);
    }

    [TestMethod]
    public void AppendToUndefined_ReturnsValue()
    {
        string? result = Evaluator.EvaluateToString(
            """$append(missing, 1)""",
            """{}""");
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void AppendUndefined_ReturnsOriginal()
    {
        string? result = Evaluator.EvaluateToString(
            """$append([1,2], missing)""",
            """{}""");
        Assert.AreEqual("[1,2]", result);
    }

    // ─── CG arithmetic paths (exercised via evaluator) ───────────────

    [TestMethod]
    [DataRow("2 + 3", "5")]
    [DataRow("10 - 3", "7")]
    [DataRow("4 * 5", "20")]
    [DataRow("15 / 3", "5")]
    [DataRow("17 % 5", "2")]
    public void Arithmetic_BasicOperations(string expression, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Arithmetic_UnaryNegation()
    {
        string? result = Evaluator.EvaluateToString("-5", "{}");
        Assert.AreEqual("-5", result);
    }

    [TestMethod]
    [DataRow("1 = 1", "true")]
    [DataRow("1 != 2", "true")]
    [DataRow("1 < 2", "true")]
    [DataRow("2 > 1", "true")]
    [DataRow("1 <= 1", "true")]
    [DataRow("1 >= 1", "true")]
    public void Comparison_BasicOperations(string expression, string expected)
    {
        string? result = Evaluator.EvaluateToString(expression, "{}");
        Assert.AreEqual(expected, result);
    }

    // ─── CG string operations ────────────────────────────────────────

    [TestMethod]
    public void StringConcatenation_Ampersand()
    {
        string? result = Evaluator.EvaluateToString("\"hello\" & \" \" & \"world\"", "{}");
        Assert.AreEqual("\"hello world\"", result);
    }

    [TestMethod]
    public void Pad_Right()
    {
        // Positive width = right-pad
        string? result = Evaluator.EvaluateToString("""$pad("42", 5, "0")""", "{}");
        Assert.AreEqual("\"42000\"", result);
    }

    [TestMethod]
    public void Pad_Left()
    {
        // Negative width = left-pad
        string? result = Evaluator.EvaluateToString("""$pad("42", -5, "0")""", "{}");
        Assert.AreEqual("\"00042\"", result);
    }

    [TestMethod]
    public void FormatBase_Hex()
    {
        string? result = Evaluator.EvaluateToString("""$formatBase(255, 16)""", "{}");
        Assert.AreEqual("\"ff\"", result);
    }

    [TestMethod]
    public void FormatBase_Binary()
    {
        string? result = Evaluator.EvaluateToString("""$formatBase(10, 2)""", "{}");
        Assert.AreEqual("\"1010\"", result);
    }

    // ─── Spread / property chain navigation ──────────────────────────

    [TestMethod]
    public void DeepNavigation_MultiLevel()
    {
        string? result = Evaluator.EvaluateToString(
            "a.b.c",
            """{"a":{"b":{"c":42}}}""");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void DeepNavigation_MissingIntermediate_ReturnsUndefined()
    {
        string? result = Evaluator.EvaluateToString(
            "a.b.c",
            """{"a":{"x":1}}""");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void WildcardDescendant()
    {
        string? result = Evaluator.EvaluateToString(
            "**.price",
            """{"order":{"items":[{"price":10},{"price":20}]}}""");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "10");
        StringAssert.Contains(result, "20");
    }
}
