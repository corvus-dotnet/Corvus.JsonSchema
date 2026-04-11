// <copyright file="CodeGenEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// End-to-end edge case tests that compile CG expressions and execute them,
/// verifying the generated code produces correct results. These focus on code
/// paths that the conformance test suite may not exercise thoroughly.
/// </summary>
public class CodeGenEdgeCaseTests : IClassFixture<CodeGenConformanceFixture>
{
    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenEdgeCaseTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    [Theory]
    [Trait("category", "codegen-edge")]
    // Nested HOFs with same parameter name — tests variable stashing
    [InlineData("$map([1,2,3], function($v) { $map([10,20], function($v) { $v }) })", "[[10,20],[10,20],[10,20]]")]
    // $$ inside $map lambda — tests _usesRootRef fix
    [InlineData("$map([1], function($v) { $$ })", "[42]", "42")]
    // $$ inside $filter lambda — tests _usesRootRef fix
    [InlineData("$filter([1,2,3], function($v) { $v = $$ })", "3", "3")]
    // $$ inside $reduce lambda — tests _usesRootRef fix
    [InlineData("$reduce([1,2,3], function($prev, $curr) { $prev + $curr })", "6")]
    // Nested HOF with outer parameter capture
    [InlineData("$map([1,2], function($a) { $map([10,20], function($b) { $a + $b }) })", "[[11,21],[12,22]]")]
    // $map with index parameter
    [InlineData("$map([10,20,30], function($v, $i) { $i })", "[0,1,2]")]
    // Block with variable binding
    [InlineData("($x := 5; $x * 2)", "10")]
    // Multi-statement block
    [InlineData("($a := 3; $b := 4; $a + $b)", "7")]
    // Constant folding
    [InlineData("2 + 3 * 4", "14")]
    // String concatenation
    [InlineData("""  "hello" & " " & "world"  """, "\"hello world\"")]
    // Range operator
    [InlineData("[1..5]", "[1,2,3,4,5]")]
    // Truthiness: zero is falsy
    [InlineData("0 ? \"yes\" : \"no\"", "\"no\"")]
    // Truthiness: empty string is falsy
    [InlineData("\"\" ? \"yes\" : \"no\"", "\"no\"")]
    // Truthiness: non-empty string is truthy
    [InlineData("\"x\" ? \"yes\" : \"no\"", "\"yes\"")]
    // Logical short-circuit
    [InlineData("1 and 0", "false")]
    [InlineData("0 or 1", "true")]
    // In operator
    [InlineData("3 in [1,2,3,4]", "true")]
    [InlineData("5 in [1,2,3,4]", "false")]
    // Unary negation
    [InlineData("-5", "-5")]
    // Modulo
    [InlineData("10 % 3", "1")]
    public void EvaluateSelfContainedExpression(string expression, string expectedJson, string inputJson = "null")
    {
        this.CompileAndAssert(expression, inputJson, expectedJson);
    }

    [Theory]
    [Trait("category", "codegen-edge")]
    // $$ with data
    [InlineData("$$", """{"a":1}""", """{"a":1}""")]
    // Simple property navigation
    [InlineData("a.b", """{"a":{"b":42}}""", "42")]
    // Nested property with auto-map
    [InlineData("items.name", """{"items":[{"name":"x"},{"name":"y"}]}""", """["x","y"]""")]
    // Numeric index in path
    [InlineData("items[0]", """{"items":[10,20,30]}""", "10")]
    // Filter predicate in path
    [InlineData("items[val > 1].name", """{"items":[{"val":1,"name":"a"},{"val":2,"name":"b"},{"val":3,"name":"c"}]}""", """["b","c"]""")]
    // $$ reference inside filter predicate
    [InlineData("items[val > $$.min].name", """{"items":[{"val":1,"name":"a"},{"val":2,"name":"b"},{"val":3,"name":"c"}],"min":1}""", """["b","c"]""")]
    // $sum over property
    [InlineData("$sum(items.val)", """{"items":[{"val":1},{"val":2},{"val":3}]}""", "6")]
    // $count over property
    [InlineData("$count(items)", """{"items":[1,2,3]}""", "3")]
    // Sort with custom comparator
    [InlineData("$sort([3,1,2], function($a, $b) { $a > $b })", "null", """[1,2,3]""")]
    // Object keys/values
    [InlineData("$keys(x)", """{"x":{"a":1,"b":2}}""", """["a","b"]""")]
    // $exists
    [InlineData("$exists(a)", """{"a":1}""", "true")]
    [InlineData("$exists(b)", """{"a":1}""", "false")]
    // $type
    [InlineData("$type(a)", """{"a":"hello"}""", "\"string\"")]
    [InlineData("$type(a)", """{"a":42}""", "\"number\"")]
    public void EvaluateExpressionWithData(string expression, string inputJson, string expectedJson)
    {
        this.CompileAndAssert(expression, inputJson, expectedJson);
    }

    [Theory]
    [Trait("category", "codegen-edge")]
    // External binding in top-level expression
    [InlineData("$x + 1", "null", "11", """{"x":10}""")]
    // External binding inside $map lambda
    [InlineData("$map([1,2,3], function($v) { $v + $offset })", "null", "[11,12,13]", """{"offset":10}""")]
    // External binding inside $filter lambda
    [InlineData("$filter([1,2,3,4,5], function($v) { $v > $min })", "null", "[4,5]", """{"min":3}""")]
    // External binding inside $reduce lambda
    [InlineData("$reduce([1,2,3], function($prev, $curr) { $prev + $curr + $base })", "null", "206", """{"base":100}""")]
    // Both $$ and external binding
    [InlineData("$map([1,2], function($v) { $v + $offset })", """{"root":true}""", "[11,12]", """{"offset":10}""")]
    // Unused bindings — should still work
    [InlineData("1 + 2", "null", "3", """{"unused":99}""")]
    public void EvaluateExpressionWithBindings(string expression, string inputJson, string expectedJson, string bindingsJson)
    {
        this.CompileAndAssertWithBindings(expression, inputJson, expectedJson, bindingsJson);
    }

    [Theory]
    [Trait("category", "codegen-edge")]
    // Undefined result from missing property
    [InlineData("a.b.c", """{"a":{"x":1}}""")]
    // Undefined from unresolved variable without bindings
    [InlineData("$unknown", "null")]
    // Undefined from filter that matches nothing
    [InlineData("items[val > 100].name", """{"items":[{"val":1,"name":"a"}]}""")]
    public void EvaluateExpressionReturnsUndefined(string expression, string inputJson)
    {
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.NotNull(compiled.Method);
        Assert.Null(compiled.Error);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }

    private void CompileAndAssert(string expression, string inputJson, string expectedJson)
    {
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Inlined:    {compiled.IsInlined}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Code length: {compiled.GeneratedCode.Length}");
        }

        if (compiled.Error is not null)
        {
            Assert.Fail($"Compilation failed: {compiled.Error}");
        }

        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(expectedJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);

        string actualJson = result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();
        this.output.WriteLine($"Expected: {expectedJson}");
        this.output.WriteLine($"Actual:   {actualJson}");

        AssertJsonEqual(expectedDoc.RootElement, result);
    }

    private void CompileAndAssertWithBindings(string expression, string inputJson, string expectedJson, string bindingsJson)
    {
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Inlined:    {compiled.IsInlined}");

        if (compiled.Error is not null)
        {
            Assert.Fail($"Compilation failed: {compiled.Error}");
        }

        // Must have the 5-param bindings overload
        Assert.NotNull(compiled.BindingsMethod);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(expectedJson));
        using var bindingsDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(bindingsJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        var bindings = new Dictionary<string, JsonElement>();
        foreach (var prop in bindingsDoc.RootElement.EnumerateObject())
        {
            bindings[prop.Name] = prop.Value;
        }

        JsonElement result = InvokeWithBindings(
            compiled.BindingsMethod, inputDoc.RootElement, workspace, bindings);

        string actualJson = result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();
        this.output.WriteLine($"Expected: {expectedJson}");
        this.output.WriteLine($"Actual:   {actualJson}");

        AssertJsonEqual(expectedDoc.RootElement, result);
    }

    private static JsonElement Invoke(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return result is JsonElement el ? el : default;
    }

    private static JsonElement InvokeWithBindings(
        MethodInfo method, JsonElement data, JsonWorkspace workspace,
        Dictionary<string, JsonElement> bindings)
    {
        object?[] args = [data, workspace, (IReadOnlyDictionary<string, JsonElement>)bindings, 500, 0];
        object? result = method.Invoke(null, args);
        return result is JsonElement el ? el : default;
    }

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
        {
            Assert.Equal(expected.GetDouble(), actual.GetDouble(), 10);
            return;
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            Assert.Fail($"Value kind mismatch: expected {expected.ValueKind}, actual {actual.ValueKind}");
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            int expectedLen = expected.GetArrayLength();
            int actualLen = actual.GetArrayLength();
            Assert.Equal(expectedLen, actualLen);
            for (int i = 0; i < expectedLen; i++)
            {
                AssertJsonEqual(expected[i], actual[i]);
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.Object)
        {
            var expectedProps = new Dictionary<string, JsonElement>();
            foreach (var prop in expected.EnumerateObject())
            {
                expectedProps[prop.Name] = prop.Value;
            }

            var actualProps = new Dictionary<string, JsonElement>();
            foreach (var prop in actual.EnumerateObject())
            {
                actualProps[prop.Name] = prop.Value;
            }

            Assert.Equal(expectedProps.Count, actualProps.Count);
            foreach (var kvp in expectedProps)
            {
                Assert.True(actualProps.ContainsKey(kvp.Key), $"Missing property: {kvp.Key}");
                AssertJsonEqual(kvp.Value, actualProps[kvp.Key]);
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.GetString(), actual.GetString());
            return;
        }

        Assert.Equal(expected.GetRawText(), actual.GetRawText());
    }
}
