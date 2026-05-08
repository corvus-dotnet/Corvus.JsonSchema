// <copyright file="AbsoluteRootInHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Tests that code-generated helper methods for non-singular filter queries
/// correctly resolve absolute root references (<c>$</c>) instead of
/// referencing the main method's <c>data</c> parameter.
/// </summary>
[TestClass]
public class AbsoluteRootInHelperTests
{
    private static CodeGenConformanceFixture? s_fixture;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        s_fixture = new CodeGenConformanceFixture();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    /// <summary>
    /// A non-singular query (<c>$..items[?@ == $.x]</c>) triggers a helper method.
    /// The filter inside the helper references the root via <c>$.x</c>.
    /// The generated helper method must use <c>root</c>, not <c>data</c>.
    /// </summary>
    [TestMethod]
    public void NonSingularQueryWithAbsoluteFilterReference()
    {
        // $.x is an absolute singular query inside a filter inside a descendant query.
        // The descendant query $..items[?@ == $.x] is non-singular → generates a helper method.
        // Inside the helper, $.x must resolve via the "root" parameter.
        const string expression = """$[?count($..items[?@ == $.x]) > 0]""";
        const string json = """{"x": 1, "items": [1, 2, 3]}""";

        // RT should work correctly — $[?...] on an object iterates all property values
        JsonElement rtData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult rtResult = JsonPathEvaluator.Default.QueryNodes(expression, rtData);
        Assert.AreEqual(2, rtResult.Count);

        // CG: generate, compile, and execute
        CompiledJsonPathExpression compiled = s_fixture!.GetOrCompile(expression);

        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine("Generated code:");
            Console.WriteLine(compiled.GeneratedCode);
        }

        Assert.IsTrue(
            compiled.Method is not null,
            $"CG compilation failed (expected success): {compiled.Error}");

        JsonElement cgData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeEvaluate(compiled.Method, cgData, workspace);

        string resultJson = cgResult.IsUndefined() ? "[]" : cgResult.GetRawText();
        Console.WriteLine($"CG result: {resultJson}");

        // The root object has 2 property values, and the filter condition is true for both
        Assert.IsFalse(cgResult.IsUndefined(), "CG result should not be undefined");
        Assert.AreEqual(2, cgResult.GetArrayLength());
    }

    /// <summary>
    /// Singular absolute reference (<c>$.maxPrice</c>) inside a filter within a
    /// non-singular query (<c>$..book[?@.price &lt; $.maxPrice]</c>) used via <c>value()</c>.
    /// </summary>
    [TestMethod]
    public void NonSingularQueryWithAbsoluteSingularReference()
    {
        const string expression = """$[?count($..book[?@.price < $.maxPrice]) > 0]""";
        const string json = """{"maxPrice":10,"book":[{"price":5},{"price":15}]}""";

        // RT — $[?...] on an object iterates property values; both match
        JsonElement rtData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult rtResult = JsonPathEvaluator.Default.QueryNodes(expression, rtData);
        Assert.AreEqual(2, rtResult.Count);

        // CG
        CompiledJsonPathExpression compiled = s_fixture!.GetOrCompile(expression);

        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine("Generated code:");
            Console.WriteLine(compiled.GeneratedCode);
        }

        Assert.IsTrue(
            compiled.Method is not null,
            $"CG compilation failed (expected success): {compiled.Error}");

        JsonElement cgData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeEvaluate(compiled.Method, cgData, workspace);

        string resultJson = cgResult.IsUndefined() ? "[]" : cgResult.GetRawText();
        Console.WriteLine($"CG result: {resultJson}");

        Assert.IsFalse(cgResult.IsUndefined(), "CG result should not be undefined");
        Assert.AreEqual(2, cgResult.GetArrayLength());
    }

    private static JsonElement InvokeEvaluate(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return (JsonElement)result!;
    }
}
