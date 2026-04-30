// <copyright file="AbsoluteRootInHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Tests that code-generated helper methods for non-singular filter queries
/// correctly resolve absolute root references (<c>$</c>) instead of
/// referencing the main method's <c>data</c> parameter.
/// </summary>
public class AbsoluteRootInHelperTests : IClassFixture<CodeGenConformanceFixture>
{
    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public AbsoluteRootInHelperTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    /// <summary>
    /// A non-singular query (<c>$..items[?@ == $.x]</c>) triggers a helper method.
    /// The filter inside the helper references the root via <c>$.x</c>.
    /// The generated helper method must use <c>root</c>, not <c>data</c>.
    /// </summary>
    [Fact]
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
        Assert.Equal(2, rtResult.Count);

        // CG: generate, compile, and execute
        CompiledJsonPathExpression compiled = this.fixture.GetOrCompile(expression);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine("Generated code:");
            this.output.WriteLine(compiled.GeneratedCode);
        }

        Assert.True(
            compiled.Method is not null,
            $"CG compilation failed (expected success): {compiled.Error}");

        JsonElement cgData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeEvaluate(compiled.Method, cgData, workspace);

        string resultJson = cgResult.IsUndefined() ? "[]" : cgResult.GetRawText();
        this.output.WriteLine($"CG result: {resultJson}");

        // The root object has 2 property values, and the filter condition is true for both
        Assert.False(cgResult.IsUndefined(), "CG result should not be undefined");
        Assert.Equal(2, cgResult.GetArrayLength());
    }

    /// <summary>
    /// Singular absolute reference (<c>$.maxPrice</c>) inside a filter within a
    /// non-singular query (<c>$..book[?@.price &lt; $.maxPrice]</c>) used via <c>value()</c>.
    /// </summary>
    [Fact]
    public void NonSingularQueryWithAbsoluteSingularReference()
    {
        const string expression = """$[?count($..book[?@.price < $.maxPrice]) > 0]""";
        const string json = """{"maxPrice":10,"book":[{"price":5},{"price":15}]}""";

        // RT — $[?...] on an object iterates property values; both match
        JsonElement rtData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult rtResult = JsonPathEvaluator.Default.QueryNodes(expression, rtData);
        Assert.Equal(2, rtResult.Count);

        // CG
        CompiledJsonPathExpression compiled = this.fixture.GetOrCompile(expression);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine("Generated code:");
            this.output.WriteLine(compiled.GeneratedCode);
        }

        Assert.True(
            compiled.Method is not null,
            $"CG compilation failed (expected success): {compiled.Error}");

        JsonElement cgData = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeEvaluate(compiled.Method, cgData, workspace);

        string resultJson = cgResult.IsUndefined() ? "[]" : cgResult.GetRawText();
        this.output.WriteLine($"CG result: {resultJson}");

        Assert.False(cgResult.IsUndefined(), "CG result should not be undefined");
        Assert.Equal(2, cgResult.GetArrayLength());
    }

    private static JsonElement InvokeEvaluate(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return (JsonElement)result!;
    }
}
