// <copyright file="CustomFunctionCodeGenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json.JsonPath.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for the code generation pipeline with custom functions.
/// </summary>
[TestClass]
public class CustomFunctionCodeGenTests
{
    // ── JpfnParser tests ────────────────────────────────────────────

    [TestMethod]
    public void JpfnParser_ParsesExpressionForm()
    {
        string content = """fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual("ceil", fns[0].Name);
        Assert.IsTrue(fns[0].IsExpression);
        Assert.AreEqual(FunctionParamType.Value, fns[0].ReturnType);
        Assert.AreEqual(1, (fns[0].Parameters).Count());
        Assert.AreEqual("x", fns[0].Parameters[0].Name);
        Assert.AreEqual(FunctionParamType.Value, fns[0].Parameters[0].Type);
        StringAssert.Contains(fns[0].Body, "Math.Ceiling");
    }

    [TestMethod]
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

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual("is_isbn", fns[0].Name);
        Assert.IsFalse(fns[0].IsExpression);
        Assert.AreEqual(FunctionParamType.Logical, fns[0].ReturnType);
        StringAssert.Contains(fns[0].Body, "s.Length == 10");
    }

    [TestMethod]
    public void JpfnParser_ParsesNodesParameter()
    {
        string content = """fn sum(nodes items) : value => items.Sum(x => x.GetDouble());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual("sum", fns[0].Name);
        Assert.AreEqual(1, (fns[0].Parameters).Count());
        Assert.AreEqual(FunctionParamType.Nodes, fns[0].Parameters[0].Type);
        Assert.AreEqual("items", fns[0].Parameters[0].Name);
    }

    [TestMethod]
    public void JpfnParser_ParsesMultipleFunctions()
    {
        string content = """
            fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());
            fn floor(value x) : value => (int)Math.Floor(x.GetDouble());
            """;

        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(2, fns.Count);
        Assert.AreEqual("ceil", fns[0].Name);
        Assert.AreEqual("floor", fns[1].Name);
    }

    [TestMethod]
    public void JpfnParser_SkipsCommentsAndBlankLines()
    {
        string content = """
            // This is a comment

            fn ceil(value x) : value => (int)Math.Ceiling(x.GetDouble());

            // Another comment
            fn floor(value x) : value => (int)Math.Floor(x.GetDouble());
            """;

        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(2, fns.Count);
    }

    [TestMethod]
    public void JpfnParser_ParsesMultiArgFunction()
    {
        string content = """fn add(value a, value b) : value => JsonPathCodeGenHelpers.IntToElement(a.GetInt32() + b.GetInt32());""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual(2, fns[0].Parameters.Length);
        Assert.AreEqual("a", fns[0].Parameters[0].Name);
        Assert.AreEqual("b", fns[0].Parameters[1].Name);
    }

    [TestMethod]
    public void JpfnParser_DefaultsParameterTypeToValue()
    {
        string content = """fn identity(x) : value => x;""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual(FunctionParamType.Value, fns[0].Parameters[0].Type);
        Assert.AreEqual("x", fns[0].Parameters[0].Name);
    }

    [TestMethod]
    public void JpfnParser_ThrowsOnMissingFnKeyword()
    {
        string content = """ceil(value x) : value => x;""";
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse(content));
    }

    [TestMethod]
    public void JpfnParser_ThrowsOnMissingReturnType()
    {
        string content = """fn ceil(value x) => x;""";
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse(content));
    }

    [TestMethod]
    public void JpfnParser_ThrowsOnUnknownParameterType()
    {
        string content = """fn ceil(blah x) : value => x;""";
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse(content));
    }

    [TestMethod]
    public void JpfnParser_ParsesZeroArgFunction()
    {
        string content = """fn zero() : value => JsonPathCodeGenHelpers.IntToElement(0);""";
        IReadOnlyList<CustomFunction> fns = JpfnParser.Parse(content);

        Assert.AreEqual(1, (fns).Count());
        Assert.AreEqual(0, (fns[0].Parameters).Count());
    }

    // ── Code generator integration tests ────────────────────────────

    [TestMethod]
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
        StringAssert.Contains(code, "CustomFn_ceil");
        StringAssert.Contains(code, "Math.Ceiling");
        StringAssert.Contains(code, "private static JsonElement CustomFn_ceil(JsonElement x, JsonWorkspace workspace)");
    }

    [TestMethod]
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

        StringAssert.Contains(code, "CustomFn_is_even");
        StringAssert.Contains(code, "private static bool CustomFn_is_even(JsonElement x, JsonWorkspace workspace)");
    }

    [TestMethod]
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
        StringAssert.Contains(code, "JsonPathResult");
        StringAssert.Contains(code, "CreatePooled");
        StringAssert.Contains(code, "CustomFn_sum");
        StringAssert.Contains(code, "ReadOnlySpan<JsonElement> items");
    }

    [TestMethod]
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

        StringAssert.Contains(code, "CustomFn_clamp");
        StringAssert.Contains(code, "double v = x.GetDouble()");
    }

    [TestMethod]
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

        Assert.AreEqual(1, count);
    }
}