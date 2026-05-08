// <copyright file="DepthAndTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

[TestClass]
public class DepthAndTimeoutTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    [TestMethod]
    public void DeeplyRecursiveExpressionHitsDepthLimit()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(1000))";

        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.Evaluate(expression, doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, maxDepth: 50));
        Assert.AreEqual("U1001", ex.Code);
    }

    [TestMethod]
    public void NormalRecursionWithinDepthLimitSucceeds()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(10))";

        JsonElement result = Evaluator.Evaluate(expression, doc.RootElement, workspace);
        Assert.AreEqual(3628800.0, result.GetDouble());
    }

    [TestMethod]
    public void TimeoutStopsLongRunningExpression()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($loop := function($n) { $n > 0 ? $loop($n - 1) + 1 : 0 }; $loop(100000))";

        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.Evaluate(expression, doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 100));
        Assert.AreEqual("U1001", ex.Code);
    }

    [TestMethod]
    public void NormalExpressionCompletesWithinTimeout()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        JsonElement result = Evaluator.Evaluate("1 + 2", doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 5000);
        Assert.AreEqual(3.0, result.GetDouble());
    }

    [TestMethod]
    public void ZeroTimeoutMeansNoLimit()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        JsonElement result = Evaluator.Evaluate("$sum([1,2,3,4,5])", doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 0);
        Assert.AreEqual(15.0, result.GetDouble());
    }

    [TestMethod]
    public void DefaultDepthAllowsModerateRecursion()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(100))";

        // Should complete without throwing — default maxDepth is 500
        JsonElement result = Evaluator.Evaluate(expression, doc.RootElement, workspace);
        Assert.AreNotEqual(default, result);
    }
}
