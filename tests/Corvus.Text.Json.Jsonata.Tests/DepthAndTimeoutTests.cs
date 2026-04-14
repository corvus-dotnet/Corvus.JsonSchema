// <copyright file="DepthAndTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class DepthAndTimeoutTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    [Fact]
    public void DeeplyRecursiveExpressionHitsDepthLimit()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(1000))";

        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.Evaluate(expression, doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, maxDepth: 50));
        Assert.Equal("U1001", ex.Code);
    }

    [Fact]
    public void NormalRecursionWithinDepthLimitSucceeds()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(10))";

        JsonElement result = Evaluator.Evaluate(expression, doc.RootElement, workspace);
        Assert.Equal(3628800.0, result.GetDouble());
    }

    [Fact]
    public void TimeoutStopsLongRunningExpression()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($loop := function($n) { $n > 0 ? $loop($n - 1) + 1 : 0 }; $loop(100000))";

        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.Evaluate(expression, doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 100));
        Assert.Equal("U1001", ex.Code);
    }

    [Fact]
    public void NormalExpressionCompletesWithinTimeout()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        JsonElement result = Evaluator.Evaluate("1 + 2", doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 5000);
        Assert.Equal(3.0, result.GetDouble());
    }

    [Fact]
    public void ZeroTimeoutMeansNoLimit()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        JsonElement result = Evaluator.Evaluate("$sum([1,2,3,4,5])", doc.RootElement, workspace, (IReadOnlyDictionary<string, JsonElement>?)null, timeLimitMs: 0);
        Assert.Equal(15.0, result.GetDouble());
    }

    [Fact]
    public void DefaultDepthAllowsModerateRecursion()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        string expression = "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(100))";

        // Should complete without throwing — default maxDepth is 500
        JsonElement result = Evaluator.Evaluate(expression, doc.RootElement, workspace);
        Assert.NotEqual(default, result);
    }
}
