// <copyright file="SequenceBindingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class SequenceBindingTests
{
    // ── SequenceFunction binding: numeric result ─────────────

    [Fact]
    public void SequenceFunctionReturnsNumericResult()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 5}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["triple"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() * 3, ws),
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$triple(x)", doc.RootElement, workspace, bindings);
        Assert.Equal(15.0, result.GetDouble());
    }

    // ── SequenceFunction binding: string result ──────────────

    [Fact]
    public void SequenceFunctionReturnsStringResult()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name": "world"}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["greet"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromString("Hello, " + args[0].AsElement().GetString()! + "!", ws),
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$greet(name)", doc.RootElement, workspace, bindings);
        Assert.Equal("Hello, world!", result.GetString());
    }

    // ── SequenceFunction binding: bool result ────────────────

    [Fact]
    public void SequenceFunctionReturnsBoolResult()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 10}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["isPositive"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromBool(args[0].AsDouble() > 0),
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$isPositive(val)", doc.RootElement, workspace, bindings);
        Assert.True(result.GetBoolean());
    }

    // ── Signature validation: valid call ─────────────────────

    [Fact]
    public void SignatureValidationPassesForCorrectType()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["inc"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() + 1, ws),
                parameterCount: 1,
                signature: "<n:n>"),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$inc(41)", doc.RootElement, workspace, bindings);
        Assert.Equal(42.0, result.GetDouble());
    }

    // ── Signature validation: type mismatch ──────────────────

    [Fact]
    public void SignatureValidationRejectsWrongType()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["inc"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() + 1, ws),
                parameterCount: 1,
                signature: "<n:n>"),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonataException ex = Assert.Throws<JsonataException>(() =>
            evaluator.Evaluate("""$inc("hello")""", doc.RootElement, workspace, bindings));
        Assert.Equal("T0410", ex.Code);
    }

    // ── Func<double,double> convenience overload ─────────────

    [Fact]
    public void FuncDoubleDoubleConvenienceOverload()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["cosine"] = JsonataBinding.FromFunction((double v) => Math.Cos(v)),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$cosine(0)", doc.RootElement, workspace, bindings);
        Assert.Equal(1.0, result.GetDouble());
    }

    // ── Func<double,double,double> convenience overload ──────

    [Fact]
    public void FuncDoubleDoubleDoubleConvenienceOverload()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["maxOf"] = JsonataBinding.FromFunction((double a, double b) => Math.Max(a, b)),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$maxOf(3, 7)", doc.RootElement, workspace, bindings);
        Assert.Equal(7.0, result.GetDouble());
    }

    // ── FromValue(double) ────────────────────────────────────

    [Fact]
    public void FromValueDoubleBinding()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["threshold"] = JsonataBinding.FromValue(42.0),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$threshold + 1", doc.RootElement, workspace, bindings);
        Assert.Equal(43.0, result.GetDouble());
    }

    // ── FromValue(string) ────────────────────────────────────

    [Fact]
    public void FromValueStringBinding()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["greeting"] = JsonataBinding.FromValue("hello"),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$greeting", doc.RootElement, workspace, bindings);
        Assert.Equal("hello", result.GetString());
    }

    // ── FromValue(bool) ──────────────────────────────────────

    [Fact]
    public void FromValueBoolBinding()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["flag"] = JsonataBinding.FromValue(true),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("""$flag ? "yes" : "no" """, doc.RootElement, workspace, bindings);
        Assert.Equal("yes", result.GetString());
    }

    // ── Implicit conversion from double ──────────────────────

    [Fact]
    public void ImplicitConversionFromDouble()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["val"] = 3.14,
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$val * 2", doc.RootElement, workspace, bindings);
        Assert.Equal(6.28, result.GetDouble(), 5);
    }

    // ── Mixed value and function bindings ────────────────────

    [Fact]
    public void MixedValueAndFunctionBindings()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["offset"] = JsonataBinding.FromValue(10.0),
            ["addOffset"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() + 10, ws),
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$addOffset($offset)", doc.RootElement, workspace, bindings);
        Assert.Equal(20.0, result.GetDouble());
    }

    // ── Arg count validation (too few) ───────────────────────

    [Fact]
    public void TooFewArgsThrowsT0410()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["add"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() + args[1].AsDouble(), ws),
                parameterCount: 2),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonataException ex = Assert.Throws<JsonataException>(() =>
            evaluator.Evaluate("$add(3)", doc.RootElement, workspace, bindings));
        Assert.Equal("T0410", ex.Code);
        Assert.Contains("expects 2", ex.Message);
        Assert.Contains("got 1", ex.Message);
    }

    // ── Extra args silently ignored ──────────────────────────

    [Fact]
    public void ExtraArgsSilentlyIgnoredInMapHof()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"items": [10, 20, 30]}"""u8.ToArray());

        // $map passes (value, index, array) but our function only declares 1 param.
        // Extra args should be silently sliced away per JSONata convention.
        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["negate"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(-args[0].AsDouble(), ws),
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = evaluator.Evaluate("$map(items, $negate)", doc.RootElement, workspace, bindings);
        Assert.Equal("[-10,-20,-30]", result.GetRawText());
    }
}
