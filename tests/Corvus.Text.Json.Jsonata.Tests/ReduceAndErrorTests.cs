// <copyright file="ReduceAndErrorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Additional tests for <c>$reduce</c> patterns and error-code paths not already
/// covered by <see cref="ErrorCodeTests"/> or <see cref="SequenceBuilderTests"/>.
/// </summary>
public class ReduceAndErrorTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ─── $reduce ─────────────────────────────────────────────────────

    [Fact]
    public void Reduce_NoInit_SumsElements()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([1,2,3], function($prev,$curr){$prev+$curr})""", "{}");
        Assert.Equal("6", result);
    }

    [Fact]
    public void Reduce_WithInit_AddsToInit()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([1,2,3], function($prev,$curr){$prev+$curr}, 10)""", "{}");
        Assert.Equal("16", result);
    }

    [Fact]
    public void Reduce_SingleElement_WithInit_AppliesFunction()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce([42], function($prev,$curr){$prev+$curr}, 10)""", "{}");
        Assert.Equal("52", result);
    }

    [Fact]
    public void Reduce_StringConcatenation()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce(["a","b","c"], function($prev,$curr){$prev & $curr})""", "{}");
        Assert.Equal("\"abc\"", result);
    }

    [Fact]
    public void Reduce_WithDataBinding()
    {
        string? result = Evaluator.EvaluateToString(
            """$reduce(items, function($prev,$curr){$prev+$curr})""",
            """{"items":[10,20,30]}""");
        Assert.Equal("60", result);
    }

    // ─── Error codes / exceptions ────────────────────────────────────

    [Fact]
    public void Error_CustomMessage_ThrowsJsonataException()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$error("custom error")""", "{}"));
        Assert.NotNull(ex.Code);
    }

    [Fact]
    public void Number_NonNumericString_ThrowsD3030()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$number("not-a-number")""", "{}"));
        Assert.Equal("D3030", ex.Code);
    }

    [Fact]
    public void Assert_False_ThrowsJsonataException()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$assert(false, "msg")""", "{}"));
        Assert.NotNull(ex.Code);
    }

    [Fact]
    public void Each_OnNonObject_ReturnsUndefined()
    {
        string? result = Evaluator.EvaluateToString("""$each(42, function($v,$k){$k})""", "{}");
        Assert.Null(result);
    }
}
