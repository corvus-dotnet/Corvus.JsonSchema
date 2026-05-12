// <copyright file="ReduceAndErrorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Additional tests for <c>$reduce</c> patterns and error-code paths not already
/// covered by <see cref="ErrorCodeTests"/> or <see cref="SequenceBuilderTests"/>.
/// </summary>
[TestClass]
public class ReduceAndErrorTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ─── $reduce ─────────────────────────────────────────────────────

    [TestMethod]
    public void Reduce_NoInit_SumsElements()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([1,2,3], function($prev,$curr){$prev+$curr})""", "{}");
        Assert.AreEqual("6", result);
    }

    [TestMethod]
    public void Reduce_WithInit_AddsToInit()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([1,2,3], function($prev,$curr){$prev+$curr}, 10)""", "{}");
        Assert.AreEqual("16", result);
    }

    [TestMethod]
    public void Reduce_SingleElement_WithInit_AppliesFunction()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([42], function($prev,$curr){$prev+$curr}, 10)""", "{}");
        Assert.AreEqual("52", result);
    }

    [TestMethod]
    public void Reduce_StringConcatenation()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce(["a","b","c"], function($prev,$curr){$prev & $curr})""", "{}");
        Assert.AreEqual("\"abc\"", result);
    }

    [TestMethod]
    public void Reduce_WithDataBinding()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce(items, function($prev,$curr){$prev+$curr})""",
            """{"items":[10,20,30]}""");
        Assert.AreEqual("60", result);
    }

    // ─── Error codes / exceptions ────────────────────────────────────

    [TestMethod]
    public void Error_CustomMessage_ThrowsJsonataException()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$error("custom error")""", "{}"));
        Assert.IsNotNull(ex.Code);
    }

    [TestMethod]
    public void Number_NonNumericString_ThrowsD3030()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$number("not-a-number")""", "{}"));
        Assert.AreEqual("D3030", ex.Code);
    }

    [TestMethod]
    public void Assert_False_ThrowsJsonataException()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$assert(false, "msg")""", "{}"));
        Assert.IsNotNull(ex.Code);
    }

    [TestMethod]
    public void Each_OnNonObject_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$each(42, function($v,$k){$k})""", "{}"));
        Assert.AreEqual("T0410", ex.Code);
    }
}
