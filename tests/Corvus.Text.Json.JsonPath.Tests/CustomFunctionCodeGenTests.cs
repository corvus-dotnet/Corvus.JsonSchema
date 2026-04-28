// <copyright file="CustomFunctionCodeGenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json.JsonPath.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for the code generation pipeline with custom functions.
/// </summary>
public class CustomFunctionCodeGenTests
{
    // ── JpfnParser tests ────────────────────────────────────────────

    [Fact]
    public void JpfnParser_ParsesExpressionForm()
    {
        string content = """fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("ceil", fns[0].Name);
        Assert.True(fns[0].IsExpression);
        Assert.Equal(FunctionParamType.Value, fns[0].ReturnType);
        Assert.Single(fns[0].Parameters);
        Assert.Equal("x", fns[0].Parameters[0].Name);
        Assert.Equal(FunctionParamType.Value, fns[0].Parameters[0].Type);
        Assert.Contains("Math.Ceiling", fns[0].Body);
    }

    [Fact]
    public void JpfnParser_ParsesBlockForm()
    {
        string content = """
            fn is_isbn(value x) : logical
            {
                string s = x.GetString() ?? "";
                return s.Length == 10 || s.Length == 13;
            }
            """;

        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("is_isbn", fns[0].Name);
        Assert.False(fns[0].IsExpression);
        Assert.Equal(FunctionParamType.Logical, fns[0].ReturnType);
        Assert.Contains("s.Length == 10", fns[0].Body);
    }

    [Fact]
    public void JpfnParser_ParsesNodesParameter()
    {
        string content = """fn sum(nodes items) : value => items.Sum(x => x.GetDouble());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("sum", fns[0].Name);
        Assert.Single(fns[0].Parameters);
        Assert.Equal(FunctionParamType.Nodes, fns[0].Parameters[0].Type);
        Assert.Equal("items", fns[0].Parameters[0].Name);
    }

    [Fact]
    public void JpfnParser_ParsesMultipleFunctions()
    {
        string content = """
            fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());
            fn floor(value x) : value => (int)Math.Floor(x.GetDouble());
            """;

        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Equal(2, fns.Count);
        Assert.Equal("ceil", fns[0].Name);
        Assert.Equal("floor", fns[1].Name);
    }

    [Fact]
    public void JpfnParser_SkipsCommentsAndBlankLines()
    {
        string content = """
            // This is a comment

            fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());

            // Another comment
            fn floor(value x) : value => (int)Math.Floor(x.GetDouble());
            """;

        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Equal(2, fns.Count);
    }

    [Fact]
    public void JpfnParser_ParsesMultiArgFunction()
    {
        string content = """fn add(value a, value b) : value => JsonPathCodeGenHelpers.IntToElement(a.GetInt32() + b.GetInt32());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal(2, fns[0].Parameters.Length);
        Assert.Equal("a", fns[0].Parameters[0].Name);
        Assert.Equal("b", fns[0].Parameters[1].Name);
    }

    [Fact]
    public void JpfnParser_DefaultsParameterTypeToValue()
    {
        string content = """fn identity(x) : value => x;""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal(FunctionParamType.Value, fns[0].Parameters[0].Type);
        Assert.Equal("x", fns[0].Parameters[0].Name);
    }

    [Fact]
    public void JpfnParser_ThrowsOnMissingFnKeyword()
    {
        string content = """ceil(value x) : value => x;""";
        Assert.Throws<FormatException>(() => JpfnParser.Parse(content));
    }

    [Fact]
    public void JpfnParser_ThrowsOnMissingReturnType()
    {
        string content = """fn ceil(value x) => x;""";
        Assert.Throws<FormatException>(() => JpfnParser.Parse(content));
    }

    [Fact]
    public void JpfnParser_ThrowsOnUnknownParameterType()
    {
        string content = """fn ceil(blah x) : value => x;""";
        Assert.Throws<FormatException>(() => JpfnParser.Parse(content));
    }

    [Fact]
    public void JpfnParser_ParsesZeroArgFunction()
    {
        string content = """fn zero() : value => JsonPathCodeGenHelpers.IntToElement(0);""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Empty(fns[0].Parameters);
    }

    // ── Code generator integration tests ────────────────────────────

    [Fact]
    public void CodeGenerator_WithCustomFunction_CompilesToValidCode()
    {
        CustomFunction[] fns = [
            new CustomFunction(
                "ceil",
                [new FunctionParameter(FunctionParamType.Value, "x")],
                FunctionParamType.Value,
                "JsonPathCodeGenHelpers.IntToElement((int)Math.Ceiling(x.GetDouble()), workspace)",
                isExpression: true),
        ];

        string code = JsonPathCodeGenerator.Generate(
            "$[?ceil(@.price) == 10]",
            "CeilQuery",
            "TestNamespace",
            customFunctions: fns);

        // The generated code should contain our custom function helper
        Assert.Contains("CustomFn_ceil", code);
        Assert.Contains("Math.Ceiling", code);
        Assert.Contains("private static JsonElement CustomFn_ceil(JsonElement x, JsonWorkspace workspace)", code);
    }

    [Fact]
    public void CodeGenerator_WithLogicalCustomFunction_CompilesToValidCode()
    {
        CustomFunction[] fns = [
            new CustomFunction(
                "is_even",
                [new FunctionParameter(FunctionParamType.Value, "x")],
                FunctionParamType.Logical,
                "x.GetInt32() % 2 == 0",
                isExpression: true),
        ];

        string code = JsonPathCodeGenerator.Generate(
            "$[?is_even(@.value)]",
            "IsEvenQuery",
            "TestNamespace",
            customFunctions: fns);

        Assert.Contains("CustomFn_is_even", code);
        Assert.Contains("private static bool CustomFn_is_even(JsonElement x, JsonWorkspace workspace)", code);
    }

    [Fact]
    public void CodeGenerator_WithNodesParam_EmitsPooledResult()
    {
        CustomFunction[] fns = [
            new CustomFunction(
                "sum",
                [new FunctionParameter(FunctionParamType.Nodes, "items")],
                FunctionParamType.Value,
                "SumImpl(items)",
                isExpression: true),
        ];

        string code = JsonPathCodeGenerator.Generate(
            "$[?sum(@.prices.*) > 100]",
            "SumQuery",
            "TestNamespace",
            customFunctions: fns);

        // Should emit a pooled JsonPathResult for the nodes arg
        Assert.Contains("JsonPathResult", code);
        Assert.Contains("CreatePooled", code);
        Assert.Contains("CustomFn_sum", code);
        Assert.Contains("ReadOnlySpan<JsonElement> items", code);
    }

    [Fact]
    public void CodeGenerator_WithBlockCustomFunction_EmitsBlock()
    {
        CustomFunction[] fns = [
            new CustomFunction(
                "clamp",
                [new FunctionParameter(FunctionParamType.Value, "x")],
                FunctionParamType.Value,
                "    double v = x.GetDouble();\n    if (v < 0) return JsonPathCodeGenHelpers.IntToElement(0, workspace);\n    return x;",
                isExpression: false),
        ];

        string code = JsonPathCodeGenerator.Generate(
            "$[?clamp(@.val) > 0]",
            "ClampQuery",
            "TestNamespace",
            customFunctions: fns);

        Assert.Contains("CustomFn_clamp", code);
        Assert.Contains("double v = x.GetDouble()", code);
    }

    [Fact]
    public void CodeGenerator_HelperEmittedOnlyOnce_ForRepeatedUse()
    {
        CustomFunction[] fns = [
            new CustomFunction(
                "double_it",
                [new FunctionParameter(FunctionParamType.Value, "x")],
                FunctionParamType.Value,
                "JsonPathCodeGenHelpers.IntToElement(x.GetInt32() * 2, workspace)",
                isExpression: true),
        ];

        // Use double_it in both sides of a comparison
        string code = JsonPathCodeGenerator.Generate(
            "$[?double_it(@.a) == double_it(@.b)]",
            "DoubleQuery",
            "TestNamespace",
            customFunctions: fns);

        // The helper should only appear once
        int count = 0;
        int idx = 0;
        while ((idx = code.IndexOf("private static JsonElement CustomFn_double_it", idx)) >= 0)
        {
            count++;
            idx++;
        }

        Assert.Equal(1, count);
    }
}