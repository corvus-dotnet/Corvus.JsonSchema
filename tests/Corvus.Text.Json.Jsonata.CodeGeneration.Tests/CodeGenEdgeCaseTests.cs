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
    // Multi-level variable shadowing restoration
    [InlineData("($x := 1; ($x := 2; $x) + $x)", "3")]
    // Nested $reduce capturing outer lambda parameter
    [InlineData("$reduce([1,2,3], function($prev, $curr) { $prev + $curr * 2 })", "11")]
    // $single with exactly one match
    [InlineData("$single([1,2,3], function($v) { $v = 2 })", "2")]
    // Triple-nested HOF with different parameter names
    [InlineData("$map([1], function($a) { $map([2], function($b) { $a + $b }) })", "[[3]]")]
    // Lambda in conditional — then branch
    [InlineData("true ? $map([1,2], function($v) { $v * 2 }) : [0]", "[2,4]")]
    // Lambda in conditional — else branch
    [InlineData("false ? [0] : $map([1,2], function($v) { $v * 3 })", "[3,6]")]
    // Negative array index — last element
    [InlineData("[10,20,30][-1]", "30")]
    // Negative array index — second to last
    [InlineData("[10,20,30][-2]", "20")]
    // Negative index out of range — returns undefined (tested separately)
    // Object construction with literal keys
    [InlineData("""{"a": 1, "b": 2}""", """{"a":1,"b":2}""")]
    // Object construction with computed values
    [InlineData("""{"sum": 1 + 2, "prod": 3 * 4}""", """{"sum":3,"prod":12}""")]
    // $sort without custom comparator — natural order
    [InlineData("$sort([3,1,4,1,5])", "[1,1,3,4,5]")]
    // $reverse
    [InlineData("$reverse([1,2,3])", "[3,2,1]")]
    // $join
    [InlineData("""$join(["a","b","c"], "-")""", "\"a-b-c\"")]
    // $string on number
    [InlineData("$string(42)", "\"42\"")]
    // $number from string
    [InlineData("""$number("3.14")""", "3.14")]
    // $floor/$ceil/$round
    [InlineData("$floor(3.7)", "3")]
    [InlineData("$ceil(3.2)", "4")]
    [InlineData("$round(3.456, 2)", "3.46")]
    // $abs
    [InlineData("$abs(-5)", "5")]
    // $power/$sqrt
    [InlineData("$power(2, 10)", "1024")]
    [InlineData("$sqrt(144)", "12")]
    // $substring
    [InlineData("""$substring("hello world", 6)""", "\"world\"")]
    [InlineData("""$substring("hello world", 0, 5)""", "\"hello\"")]
    // $uppercase/$lowercase
    [InlineData("""$uppercase("hello")""", "\"HELLO\"")]
    [InlineData("""$lowercase("HELLO")""", "\"hello\"")]
    // $trim
    [InlineData("""$trim("  hello  ")""", "\"hello\"")]
    // $contains
    [InlineData("""$contains("hello world", "world")""", "true")]
    [InlineData("""$contains("hello world", "xyz")""", "false")]
    // $split
    [InlineData("""$split("a,b,c", ",")""", """["a","b","c"]""")]
    // $replace (string pattern, not regex)
    [InlineData("""$replace("hello", "l", "r")""", "\"herro\"")]
    // $boolean
    [InlineData("$boolean(0)", "false")]
    [InlineData("$boolean(1)", "true")]
    [InlineData("""$boolean("")""", "false")]
    [InlineData("""$boolean("x")""", "true")]
    // $not
    [InlineData("$not(true)", "false")]
    [InlineData("$not(false)", "true")]
    // $append
    [InlineData("$append([1,2], [3,4])", "[1,2,3,4]")]
    // $distinct
    [InlineData("$distinct([1,2,2,3,3,3])", "[1,2,3]")]
    // $count
    [InlineData("$count([1,2,3,4,5])", "5")]
    // $sum/$max/$min/$average
    [InlineData("$sum([1,2,3])", "6")]
    [InlineData("$max([1,2,3])", "3")]
    [InlineData("$min([1,2,3])", "1")]
    [InlineData("$average([2,4,6])", "4")]
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
    // Negative index on data property
    [InlineData("items[-1]", """{"items":[10,20,30]}""", "30")]
    // Wildcard — enumerate all property values
    [InlineData("$sum(*)", """{"a":1,"b":2,"c":3}""", "6")]
    // Wildcard with property access
    [InlineData("*.name", """{"x":{"name":"a"},"y":{"name":"b"}}""", """["a","b"]""")]
    // Nested path with negative index
    [InlineData("items[-1].name", """{"items":[{"name":"first"},{"name":"last"}]}""", "\"last\"")]
    // $single on array returning undefined (multi-match) — tested in undefined section
    // $flatten
    [InlineData("$flatten([[1,2],[3,[4,5]]])", "null", "[1,2,3,4,5]")]
    // $merge
    [InlineData("""$merge([{"a":1},{"b":2}])""", "null", """{"a":1,"b":2}""")]
    // $lookup
    [InlineData("""$lookup({"a":1,"b":2}, "b")""", "null", "2")]
    // $values
    [InlineData("$values(x)", """{"x":{"a":1,"b":2}}""", "[1,2]")]
    // Autoboxing: scalar with index 0
    [InlineData("a[0]", """{"a":42}""", "42")]
    // $length on string
    [InlineData("""$length("hello")""", "null", "5")]
    // $substringBefore/$substringAfter
    [InlineData("""$substringBefore("hello world", " ")""", "null", "\"hello\"")]
    [InlineData("""$substringAfter("hello world", " ")""", "null", "\"world\"")]
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
    // Negative index out of range returns undefined
    [InlineData("[1,2,3][-10]", "null")]
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

    [Theory]
    [Trait("category", "codegen-edge")]
    // $single on empty array throws D3139
    [InlineData("$single([])", "null", "D3139")]
    // $single with multiple matches throws D3138
    [InlineData("$single([1,2,3], function($v) { $v > 0 })", "null", "D3138")]
    public void EvaluateExpressionThrowsJsonataException(string expression, string inputJson, string expectedErrorCode)
    {
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.NotNull(compiled.Method);
        Assert.Null(compiled.Error);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        var ex = Assert.Throws<TargetInvocationException>(
            () => Invoke(compiled.Method, inputDoc.RootElement, workspace));
        var jex = Assert.IsType<JsonataException>(ex.InnerException);
        Assert.Contains(expectedErrorCode, jex.Message);
    }

    [Fact]
    [Trait("category", "codegen-edge")]
    public void Generate_BlockVariableCapturedInHofLambda_LambdaIsNotStatic()
    {
        // Block-scoped variable ($x) captured inside a HOF lambda must make the
        // lambda non-static; otherwise C# emits CS8820.
        CompiledExpression compiled = this.fixture.GetOrCompile(
            "($x := 5; $map([1,2,3], function($v) { $v + $x }))");

        this.output.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        // The expression must compile without error (no CS8820)
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        // And must produce the correct result
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("[6,7,8]", result.GetRawText());
    }

    [Fact]
    [Trait("category", "codegen-edge")]
    public void Generate_BlockVariableInFilterPredicate_Compiles()
    {
        // Block-scoped variable ($x) referenced inside a filter predicate lambda
        // must not cause CS8820 if the filter stage uses {Static}.
        CompiledExpression compiled = this.fixture.GetOrCompile(
            """($threshold := 2; items[$threshold < val].name)""");

        this.output.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        // Must compile without error
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        // And produce correct result
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"items":[{"val":1,"name":"a"},{"val":3,"name":"b"},{"val":5,"name":"c"}]}"""));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("""["b","c"]""", result.GetRawText());
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

    [Fact]
    [Trait("category", "codegen-edge")]
    public void EvaluateExpressionWithCustomFunction_ExpressionForm()
    {
        var customFns = new[]
        {
            new CustomFunction(
                "double_it",
                new[] { "x" },
                "JsonataCodeGenHelpers.NumberFromDouble(x.GetDouble() * 2, workspace)",
                isExpression: true),
        };

        CompiledExpression compiled = this.fixture.GetOrCompile("$double_it(21)", customFns);

        this.output.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("42", result.GetRawText());
    }

    [Fact]
    [Trait("category", "codegen-edge")]
    public void EvaluateExpressionWithCustomFunction_BlockForm()
    {
        var customFns = new[]
        {
            new CustomFunction(
                "clamp",
                new[] { "val", "lo", "hi" },
                "double v = val.GetDouble();\ndouble low = lo.GetDouble();\ndouble high = hi.GetDouble();\ndouble result = Math.Max(low, Math.Min(high, v));\nreturn JsonataCodeGenHelpers.NumberFromDouble(result, workspace);",
                isExpression: false),
        };

        CompiledExpression compiled = this.fixture.GetOrCompile("$clamp(15, 0, 10)", customFns);

        this.output.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("10", result.GetRawText());
    }

    [Fact]
    [Trait("category", "codegen-edge")]
    public void EvaluateExpressionWithCustomFunction_UsedInMap()
    {
        var customFns = new[]
        {
            new CustomFunction(
                "triple",
                new[] { "x" },
                "JsonataCodeGenHelpers.NumberFromDouble(x.GetDouble() * 3, workspace)",
                isExpression: true),
        };

        CompiledExpression compiled = this.fixture.GetOrCompile(
            "$map([1,2,3], function($v) { $triple($v) })", customFns);

        this.output.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("[3,6,9]", result.GetRawText());
    }

    [Fact]
    [Trait("category", "codegen-edge")]
    public void EvaluateExpressionWithCustomFunction_WithDataInput()
    {
        var customFns = new[]
        {
            new CustomFunction(
                "add_bonus",
                new[] { "price" },
                "JsonataCodeGenHelpers.NumberFromDouble(price.GetDouble() + 10, workspace)",
                isExpression: true),
        };

        CompiledExpression compiled = this.fixture.GetOrCompile("$add_bonus(price)", customFns);

        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"price":50}"""));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal("60", result.GetRawText());
    }
}
