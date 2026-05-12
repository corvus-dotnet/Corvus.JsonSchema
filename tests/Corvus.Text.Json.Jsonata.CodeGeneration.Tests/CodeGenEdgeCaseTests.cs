// <copyright file="CodeGenEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// End-to-end edge case tests that compile CG expressions and execute them,
/// verifying the generated code produces correct results. These focus on code
/// paths that the conformance test suite may not exercise thoroughly.
/// </summary>
[TestClass]
public class CodeGenEdgeCaseTests
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

    [TestMethod]
    [TestCategory("codegen-edge")]
    // Nested HOFs with same parameter name — tests variable stashing
    [DataRow("$map([1,2,3], function($v) { $map([10,20], function($v) { $v }) })", "[[10,20],[10,20],[10,20]]")]
    // $$ inside $map lambda — tests _usesRootRef fix
    [DataRow("$map([1], function($v) { $$ })", "[42]", "42")]
    // $$ inside $filter lambda — tests _usesRootRef fix
    [DataRow("$filter([1,2,3], function($v) { $v = $$ })", "3", "3")]
    // $$ inside $reduce lambda — tests _usesRootRef fix
    [DataRow("$reduce([1,2,3], function($prev, $curr) { $prev + $curr })", "6")]
    // Nested HOF with outer parameter capture
    [DataRow("$map([1,2], function($a) { $map([10,20], function($b) { $a + $b }) })", "[[11,21],[12,22]]")]
    // $map with index parameter
    [DataRow("$map([10,20,30], function($v, $i) { $i })", "[0,1,2]")]
    // Block with variable binding
    [DataRow("($x := 5; $x * 2)", "10")]
    // Multi-statement block
    [DataRow("($a := 3; $b := 4; $a + $b)", "7")]
    // Constant folding
    [DataRow("2 + 3 * 4", "14")]
    // String concatenation
    [DataRow("""  "hello" & " " & "world"  """, "\"hello world\"")]
    // Range operator
    [DataRow("[1..5]", "[1,2,3,4,5]")]
    // Truthiness: zero is falsy
    [DataRow("0 ? \"yes\" : \"no\"", "\"no\"")]
    // Truthiness: empty string is falsy
    [DataRow("\"\" ? \"yes\" : \"no\"", "\"no\"")]
    // Truthiness: non-empty string is truthy
    [DataRow("\"x\" ? \"yes\" : \"no\"", "\"yes\"")]
    // Logical short-circuit
    [DataRow("1 and 0", "false")]
    [DataRow("0 or 1", "true")]
    // In operator
    [DataRow("3 in [1,2,3,4]", "true")]
    [DataRow("5 in [1,2,3,4]", "false")]
    // Unary negation
    [DataRow("-5", "-5")]
    // Modulo
    [DataRow("10 % 3", "1")]
    // Multi-level variable shadowing restoration
    [DataRow("($x := 1; ($x := 2; $x) + $x)", "3")]
    // Nested $reduce capturing outer lambda parameter
    [DataRow("$reduce([1,2,3], function($prev, $curr) { $prev + $curr * 2 })", "11")]
    // $single with exactly one match
    [DataRow("$single([1,2,3], function($v) { $v = 2 })", "2")]
    // Triple-nested HOF with different parameter names
    [DataRow("$map([1], function($a) { $map([2], function($b) { $a + $b }) })", "[[3]]")]
    // Lambda in conditional — then branch
    [DataRow("true ? $map([1,2], function($v) { $v * 2 }) : [0]", "[2,4]")]
    // Lambda in conditional — else branch
    [DataRow("false ? [0] : $map([1,2], function($v) { $v * 3 })", "[3,6]")]
    // Negative array index — last element
    [DataRow("[10,20,30][-1]", "30")]
    // Negative array index — second to last
    [DataRow("[10,20,30][-2]", "20")]
    // Negative index out of range — returns undefined (tested separately)
    // Object construction with literal keys
    [DataRow("""{"a": 1, "b": 2}""", """{"a":1,"b":2}""")]
    // Object construction with computed values
    [DataRow("""{"sum": 1 + 2, "prod": 3 * 4}""", """{"sum":3,"prod":12}""")]
    // $sort without custom comparator — natural order
    [DataRow("$sort([3,1,4,1,5])", "[1,1,3,4,5]")]
    // $reverse
    [DataRow("$reverse([1,2,3])", "[3,2,1]")]
    // $join
    [DataRow("""$join(["a","b","c"], "-")""", "\"a-b-c\"")]
    // $string on number
    [DataRow("$string(42)", "\"42\"")]
    // $number from string
    [DataRow("""$number("3.14")""", "3.14")]
    // $floor/$ceil/$round
    [DataRow("$floor(3.7)", "3")]
    [DataRow("$ceil(3.2)", "4")]
    [DataRow("$round(3.456, 2)", "3.46")]
    // $abs
    [DataRow("$abs(-5)", "5")]
    // $power/$sqrt
    [DataRow("$power(2, 10)", "1024")]
    [DataRow("$sqrt(144)", "12")]
    // $substring
    [DataRow("""$substring("hello world", 6)""", "\"world\"")]
    [DataRow("""$substring("hello world", 0, 5)""", "\"hello\"")]
    // $uppercase/$lowercase
    [DataRow("""$uppercase("hello")""", "\"HELLO\"")]
    [DataRow("""$lowercase("HELLO")""", "\"hello\"")]
    // $trim
    [DataRow("""$trim("  hello  ")""", "\"hello\"")]
    // $contains
    [DataRow("""$contains("hello world", "world")""", "true")]
    [DataRow("""$contains("hello world", "xyz")""", "false")]
    // $split
    [DataRow("""$split("a,b,c", ",")""", """["a","b","c"]""")]
    // $replace (string pattern, not regex)
    [DataRow("""$replace("hello", "l", "r")""", "\"herro\"")]
    // $boolean
    [DataRow("$boolean(0)", "false")]
    [DataRow("$boolean(1)", "true")]
    [DataRow("""$boolean("")""", "false")]
    [DataRow("""$boolean("x")""", "true")]
    // $not
    [DataRow("$not(true)", "false")]
    [DataRow("$not(false)", "true")]
    // $append
    [DataRow("$append([1,2], [3,4])", "[1,2,3,4]")]
    // $distinct
    [DataRow("$distinct([1,2,2,3,3,3])", "[1,2,3]")]
    // $count
    [DataRow("$count([1,2,3,4,5])", "5")]
    // $sum/$max/$min/$average
    [DataRow("$sum([1,2,3])", "6")]
    [DataRow("$max([1,2,3])", "3")]
    [DataRow("$min([1,2,3])", "1")]
    [DataRow("$average([2,4,6])", "4")]
    public void EvaluateSelfContainedExpression(string expression, string expectedJson, string inputJson = "null")
    {
        this.CompileAndAssert(expression, inputJson, expectedJson);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // $$ with data
    [DataRow("$$", """{"a":1}""", """{"a":1}""")]
    // Simple property navigation
    [DataRow("a.b", """{"a":{"b":42}}""", "42")]
    // Nested property with auto-map
    [DataRow("items.name", """{"items":[{"name":"x"},{"name":"y"}]}""", """["x","y"]""")]
    // Numeric index in path
    [DataRow("items[0]", """{"items":[10,20,30]}""", "10")]
    // Constant index with post-index navigation (benchmark: indexed-chain pattern)
    [DataRow("items.orders[0].products.name", """{"items":{"orders":[{"products":[{"name":"x"},{"name":"y"}]}]}}""", """["x","y"]""")]
    // Constant index 0 on singleton (autoboxing: non-array treated as single-element)
    [DataRow("data.value[0].name", """{"data":{"value":{"name":"hello"}}}""", "\"hello\"")]
    // Constant index out of range returns undefined — tested in undefined section
    // Deep constant index chain with multiple indices
    [DataRow("a.b[0].c[0].d", """{"a":{"b":[{"c":[{"d":42}]}]}}""", "42")]
    // Filter predicate in path
    [DataRow("items[val > 1].name", """{"items":[{"val":1,"name":"a"},{"val":2,"name":"b"},{"val":3,"name":"c"}]}""", """["b","c"]""")]
    // Filter predicate with post-predicate array flattening
    [DataRow("""items[type="a"].products.name""", """{"items":[{"type":"a","products":[{"name":"x"},{"name":"y"}]},{"type":"b","products":[{"name":"z"}]}]}""", """["x","y"]""")]
    // Equality predicate with nested array — multiple matches
    [DataRow("""items[type="a"].tags""", """{"items":[{"type":"a","tags":[1,2]},{"type":"a","tags":[3]},{"type":"b","tags":[4]}]}""", """[[1,2],[3]]""")]
    // Equality predicate with nested array then property — deep chain
    [DataRow("""orders[status="shipped"].lines.product.sku""", """{"orders":[{"status":"shipped","lines":[{"product":{"sku":"A1"}},{"product":{"sku":"A2"}}]},{"status":"pending","lines":[{"product":{"sku":"B1"}}]}]}""", """["A1","A2"]""")]
    // Post-predicate: object intermediate then terminal array — no flatten (EvalFromStep path)
    [DataRow("""items[type="a"].data.values""", """{"items":[{"type":"a","data":{"values":[10,20]}},{"type":"a","data":{"values":[30]}}]}""", """[[10,20],[30]]""")]
    // Post-predicate: array intermediate then terminal array — flatten (CollectAndContinue path)
    [DataRow("""items[type="a"].entries.value""", """{"items":[{"type":"a","entries":[{"value":10},{"value":20}]},{"type":"a","entries":[{"value":30}]}]}""", """[10,20,30]""")]
    // Post-predicate: single matching element returns unwrapped result
    [DataRow("""items[type="a"].name""", """{"items":[{"type":"a","name":"hello"},{"type":"b","name":"world"}]}""", "\"hello\"")]
    // Post-predicate: nested arrays at intermediate — recursive flatten
    [DataRow("""groups[active=true].members.tags""", """{"groups":[{"active":true,"members":[{"tags":["x","y"]},{"tags":["z"]}]},{"active":false,"members":[{"tags":["q"]}]}]}""", """["x","y","z"]""")]
    // Post-predicate: singleton source object (not array) with equality predicate match
    [DataRow("""item[type="a"].name""", """{"item":{"type":"a","name":"found"}}""", "\"found\"")]
    // Pre-predicate steps then post-predicate navigation (single match unwraps)
    [DataRow("""root.items[type="a"].products.name""", """{"root":{"items":[{"type":"a","products":[{"name":"x"}]},{"type":"b","products":[{"name":"y"}]}]}}""", "\"x\"")]
    // $$ reference inside filter predicate
    [DataRow("items[val > $$.min].name", """{"items":[{"val":1,"name":"a"},{"val":2,"name":"b"},{"val":3,"name":"c"}],"min":1}""", """["b","c"]""")]
    // $sum over property
    [DataRow("$sum(items.val)", """{"items":[{"val":1},{"val":2},{"val":3}]}""", "6")]
    // $count over property
    [DataRow("$count(items)", """{"items":[1,2,3]}""", "3")]
    // Sort with custom comparator
    [DataRow("$sort([3,1,2], function($a, $b) { $a > $b })", "null", """[1,2,3]""")]
    // Object keys/values
    [DataRow("$keys(x)", """{"x":{"a":1,"b":2}}""", """["a","b"]""")]
    // $exists
    [DataRow("$exists(a)", """{"a":1}""", "true")]
    [DataRow("$exists(b)", """{"a":1}""", "false")]
    // $type
    [DataRow("$type(a)", """{"a":"hello"}""", "\"string\"")]
    [DataRow("$type(a)", """{"a":42}""", "\"number\"")]
    // Negative index on data property
    [DataRow("items[-1]", """{"items":[10,20,30]}""", "30")]
    // Wildcard — enumerate all property values
    [DataRow("$sum(*)", """{"a":1,"b":2,"c":3}""", "6")]
    // Wildcard with property access
    [DataRow("*.name", """{"x":{"name":"a"},"y":{"name":"b"}}""", """["a","b"]""")]
    // Nested path with negative index
    [DataRow("items[-1].name", """{"items":[{"name":"first"},{"name":"last"}]}""", "\"last\"")]
    // $single on array returning undefined (multi-match) — tested in undefined section
    // $flatten
    [DataRow("$flatten([[1,2],[3,[4,5]]])", "null", "[1,2,3,4,5]")]
    // $merge
    [DataRow("""$merge([{"a":1},{"b":2}])""", "null", """{"a":1,"b":2}""")]
    // $lookup
    [DataRow("""$lookup({"a":1,"b":2}, "b")""", "null", "2")]
    // $values
    [DataRow("$values(x)", """{"x":{"a":1,"b":2}}""", "[1,2]")]
    // Autoboxing: scalar with index 0
    [DataRow("a[0]", """{"a":42}""", "42")]
    // KeepArray ([]) — single match wraps in array
    [DataRow("items.a[]", """{"items":[{"a":42}]}""", "[42]")]
    // KeepArray ([]) — multiple matches with array property values flatten
    [DataRow("items.a[]", """{"items":[{"a":[1,2]},{"a":[3]}]}""", "[1,2,3]")]
    // KeepArray ([]) — 3-level deep nesting flattens correctly
    [DataRow("items.a[]", """{"items":[[[{"a":1}]]]}""", "[1]")]
    // KeepArray ([]) — mixed types: only objects and nested arrays contribute
    [DataRow("items.x[]", """{"items":[1,"str",{"x":5},null,[{"x":6}]]}""", "[5,6]")]
    // KeepArray ([]) — object input wraps scalar property in array
    [DataRow("data.name[]", """{"data":{"name":"Alice"}}""", """["Alice"]""")]
    // $length on string
    [DataRow("""$length("hello")""", "null", "5")]
    // $substringBefore/$substringAfter
    [DataRow("""$substringBefore("hello world", " ")""", "null", "\"hello\"")]
    [DataRow("""$substringAfter("hello world", " ")""", "null", "\"world\"")]
    public void EvaluateExpressionWithData(string expression, string inputJson, string expectedJson)
    {
        this.CompileAndAssert(expression, inputJson, expectedJson);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // External binding in top-level expression
    [DataRow("$x + 1", "null", "11", """{"x":10}""")]
    // External binding inside $map lambda
    [DataRow("$map([1,2,3], function($v) { $v + $offset })", "null", "[11,12,13]", """{"offset":10}""")]
    // External binding inside $filter lambda
    [DataRow("$filter([1,2,3,4,5], function($v) { $v > $min })", "null", "[4,5]", """{"min":3}""")]
    // External binding inside $reduce lambda
    [DataRow("$reduce([1,2,3], function($prev, $curr) { $prev + $curr + $base })", "null", "206", """{"base":100}""")]
    // Both $$ and external binding
    [DataRow("$map([1,2], function($v) { $v + $offset })", """{"root":true}""", "[11,12]", """{"offset":10}""")]
    // Unused bindings — should still work
    [DataRow("1 + 2", "null", "3", """{"unused":99}""")]
    public void EvaluateExpressionWithBindings(string expression, string inputJson, string expectedJson, string bindingsJson)
    {
        this.CompileAndAssertWithBindings(expression, inputJson, expectedJson, bindingsJson);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // Undefined result from missing property
    [DataRow("a.b.c", """{"a":{"x":1}}""")]
    // Undefined from unresolved variable without bindings
    [DataRow("$unknown", "null")]
    // Undefined from filter that matches nothing
    [DataRow("items[val > 100].name", """{"items":[{"val":1,"name":"a"}]}""")]
    // Negative index out of range returns undefined
    [DataRow("[1,2,3][-10]", "null")]
    // KeepArray ([]) — no matching properties returns undefined
    [DataRow("items.nonexistent[]", """{"items":[{"a":1},{"b":2}]}""")]
    // KeepArray ([]) — empty array input returns undefined
    [DataRow("items.a[]", """{"items":[]}""")]
    // KeepArray ([]) — missing property on object input returns undefined
    [DataRow("data.missing[]", """{"data":{"name":"Alice"}}""")]
    // Post-predicate: singleton source object with equality predicate mismatch
    [DataRow("""item[type="b"].name""", """{"item":{"type":"a","name":"found"}}""")]
    // Constant index out of range in chain
    [DataRow("items.orders[5].name", """{"items":{"orders":[{"name":"x"}]}}""")]
    public void EvaluateExpressionReturnsUndefined(string expression, string inputJson)
    {
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);
        Assert.IsNotNull(compiled.Method);
        Assert.IsNull(compiled.Error);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // $single on empty array throws D3139
    [DataRow("$single([])", "null", "D3139")]
    // $single with multiple matches throws D3138
    [DataRow("$single([1,2,3], function($v) { $v > 0 })", "null", "D3138")]
    public void EvaluateExpressionThrowsJsonataException(string expression, string inputJson, string expectedErrorCode)
    {
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);
        Assert.IsNotNull(compiled.Method);
        Assert.IsNull(compiled.Error);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        var ex = Assert.ThrowsExactly<TargetInvocationException>(
            () => Invoke(compiled.Method, inputDoc.RootElement, workspace));
        var jex = Assert.IsInstanceOfType<JsonataException>(ex.InnerException);
        StringAssert.Contains(jex.Message, expectedErrorCode);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    public void Generate_BlockVariableCapturedInHofLambda_LambdaIsNotStatic()
    {
        // Block-scoped variable ($x) captured inside a HOF lambda must make the
        // lambda non-static; otherwise C# emits CS8820.
        CompiledExpression compiled = s_fixture!.GetOrCompile(
            "($x := 5; $map([1,2,3], function($v) { $v + $x }))");

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        // The expression must compile without error (no CS8820)
        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        // And must produce the correct result
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("[6,7,8]", result.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    public void Generate_BlockVariableInFilterPredicate_Compiles()
    {
        // Block-scoped variable ($x) referenced inside a filter predicate lambda
        // must not cause CS8820 if the filter stage uses {Static}.
        CompiledExpression compiled = s_fixture!.GetOrCompile(
            """($threshold := 2; items[$threshold < val].name)""");

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        // Must compile without error
        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        // And produce correct result
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"items":[{"val":1,"name":"a"},{"val":3,"name":"b"},{"val":5,"name":"c"}]}"""));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("""["b","c"]""", result.GetRawText());
    }

    private void CompileAndAssert(string expression, string inputJson, string expectedJson)
    {
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);

        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Inlined:    {compiled.IsInlined}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Code length: {compiled.GeneratedCode.Length}");
        }

        if (compiled.Error is not null)
        {
            Assert.Fail($"Compilation failed: {compiled.Error}");
        }

        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(expectedJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);

        string actualJson = result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();
        Console.WriteLine($"Expected: {expectedJson}");
        Console.WriteLine($"Actual:   {actualJson}");

        AssertJsonEqual(expectedDoc.RootElement, result);
    }

    private void CompileAndAssertWithBindings(string expression, string inputJson, string expectedJson, string bindingsJson)
    {
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);

        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Inlined:    {compiled.IsInlined}");

        if (compiled.Error is not null)
        {
            Assert.Fail($"Compilation failed: {compiled.Error}");
        }

        // Must have the 5-param bindings overload
        Assert.IsNotNull(compiled.BindingsMethod);

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
        Console.WriteLine($"Expected: {expectedJson}");
        Console.WriteLine($"Actual:   {actualJson}");

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
            Assert.AreEqual(expected.GetDouble(), actual.GetDouble(), 10);
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
            Assert.AreEqual(expectedLen, actualLen);
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

            Assert.AreEqual(expectedProps.Count, actualProps.Count);
            foreach (var kvp in expectedProps)
            {
                Assert.IsTrue(actualProps.ContainsKey(kvp.Key), $"Missing property: {kvp.Key}");
                AssertJsonEqual(kvp.Value, actualProps[kvp.Key]);
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.String)
        {
            Assert.AreEqual(expected.GetString(), actual.GetString());
            return;
        }

        Assert.AreEqual(expected.GetRawText(), actual.GetRawText());
    }

    // ═══════════════════════════════════════════════════════════
    // Coverage Round 2: targeted tests for uncovered code paths
    // in JsonataCodeGenerator.cs, CodeGenStringHelpers.cs
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("codegen-edge")]
    // Regex with case-insensitive flag (L542-544)
    [DataRow("""$match("Hello World", /hello/i).match""", "\"Hello\"")]
    // Regex with multiline flag (L545-547)
    [DataRow("$contains(\"line1\\nline2\", /^line2/m)", "true")]
    // Division operator (L2936+)
    [DataRow("10 / 4", "2.5")]
    // String with special characters that exercise EscapeCSharpStringLiteral (L37-46)
    [DataRow("\"hello\\tworld\"", "\"hello\\tworld\"")]
    [DataRow("\"line1\\nline2\"", "\"line1\\nline2\"")]
    // Object constructor with numeric values (L811-824)
    [DataRow("""{"x": 1, "y": 2, "z": 3}""", """{"x":1,"y":2,"z":3}""")]
    // Transform operator ~> (L2880+)
    [DataRow("$sum([1,2,3,4] ~> $map(function($v) { $v * $v }))", "30")]
    // $each for object iteration
    [DataRow("""$each({"a":1,"b":2}, function($v, $k) { $k & "=" & $string($v) })""", """["a=1","b=2"]""")]
    // $spread
    [DataRow("""$spread({"a":1,"b":2})""", """[{"a":1},{"b":2}]""")]
    // $sift — filter object properties
    [DataRow("""$sift({"a":1,"b":2,"c":3}, function($v) { $v > 1 })""", """{"b":2,"c":3}""")]
    // Lambda expression used as value
    [DataRow("$map([1,2,3], function($v) { $v * $v })", "[1,4,9]")]
    // $pad
    [DataRow("""$pad("hello", 10)""", "\"hello     \"")]
    [DataRow("""$pad("hello", -10)""", "\"     hello\"")]
    [DataRow("""$pad("hello", 10, "*")""", "\"hello*****\"")]
    // $eval (falls back to RT)
    [DataRow("""$eval("1+2")""", "3")]
    // Chained comparison operators
    [DataRow("1 < 2 and 2 < 3", "true")]
    [DataRow("1 > 2 or 2 > 3", "false")]
    // CodeGenStringHelpers: EscapeJsonStringContent \b \f paths (L85-86)
    // and EscapeCSharpStringLiteral \b \f paths (L39-40)
    [DataRow("\"hello\\bworld\"", "\"hello\\bworld\"")]
    [DataRow("\"hello\\fworld\"", "\"hello\\fworld\"")]
    // CodeGenStringHelpers: EscapeCSharpStringLiteral \u escape for control chars (L44-46)
    [DataRow("\"hello\\u0007world\"", "\"hello\\u0007world\"")]
    [DataRow("\"hello\\u000Bworld\"", "\"hello\\u000Bworld\"")]
    public void EvaluateR2SelfContainedExpression(string expression, string expectedJson)
    {
        this.CompileAndAssert(expression, "null", expectedJson);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // Group-by on path: data{key: value} (L793-806, L945-948)
    [DataRow(
        """Account.Order.Product{`Product Name`: Price}""",
        """{"Account":{"Order":{"Product":[{"Product Name":"Bowler Hat","Price":34.45},{"Product Name":"Trilby hat","Price":21.67}]}}}""",
        """{"Bowler Hat":34.45,"Trilby hat":21.67}""")]
    // Group-by with $sum aggregation (L803-806)
    [DataRow(
        """Account.Order.Product{`Product Name`: $sum(Price)}""",
        """{"Account":{"Order":[{"Product":[{"Product Name":"Hat","Price":10},{"Product Name":"Hat","Price":20}]},{"Product":[{"Product Name":"Shoes","Price":50}]}]}}""",
        """{"Hat":30,"Shoes":50}""")]
    // KeepSingletonArray on path after wildcard step (L826-836, L951-968)
    [DataRow("*.name[]", """{"a":{"name":"x"},"b":{"name":"y"}}""", """["x","y"]""")]
    // Descendant navigation (L844-870)
    [DataRow("**.price", """{"store":{"book":{"price":10},"pen":{"price":2}}}""", "[10,2]")]
    // Descendant + property fusion (L845-870)
    [DataRow("**.name", """{"a":{"b":{"name":"deep"}}}""", "\"deep\"")]
    // OR predicate extraction (L1147-1155)
    [DataRow(
        """items[type="a" or type="b"].name""",
        """{"items":[{"type":"a","name":"x"},{"type":"b","name":"y"},{"type":"c","name":"z"}]}""",
        """["x","y"]""")]
    // Conditional with undefined-test optimization
    [DataRow("a ? a : \"default\"", """{"a":"present"}""", "\"present\"")]
    [DataRow("a ? a : \"default\"", """{}""", "\"default\"")]
    // Multi-step path with fused chain + keepArray step (L663-668)
    [DataRow("items[0].details.tags[]",
        """{"items":[{"details":{"tags":["a","b"]}}]}""",
        """["a","b"]""")]
    // Constant index chain with multiple indices (L1412+)
    [DataRow("matrix[0][1]",
        """{"matrix":[[10,20,30],[40,50,60]]}""",
        "20")]
    // Pre-index step chain with post-index property (L1600+)
    [DataRow("data.rows[0].name",
        """{"data":{"rows":[{"name":"first"},{"name":"second"}]}}""",
        "\"first\"")]
    // Deep chain with filter and post-navigation
    [DataRow("""orders[status="active"].items.product.name""",
        """{"orders":[{"status":"active","items":[{"product":{"name":"Widget"}},{"product":{"name":"Gadget"}}]},{"status":"closed","items":[{"product":{"name":"Old"}}]}]}""",
        """["Widget","Gadget"]""")]
    // Wildcard enumerate all values then navigate (L837+)
    [DataRow("*.val",
        """{"a":{"val":1},"b":{"val":2},"c":{"val":3}}""",
        "[1,2,3]")]
    // NameNode with stages at step 0 (L774-786)
    [DataRow("items[0].name",
        """{"items":[{"name":"first"},{"name":"second"}]}""",
        "\"first\"")]
    // Sort with custom comparator on path (L2663+)
    [DataRow("$sort(items, function($a, $b) { $a.age > $b.age }).name",
        """{"items":[{"name":"Charlie","age":30},{"name":"Alice","age":25},{"name":"Bob","age":35}]}""",
        """["Alice","Charlie","Bob"]""")]
    public void EvaluateR2ExpressionWithData(string expression, string inputJson, string expectedJson)
    {
        this.CompileAndAssert(expression, inputJson, expectedJson);
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    // FilterNode standalone — falls back to RT (L594)
    [DataRow("[0]", "[10,20,30]")]
    // Complex annotations with focus — falls back to RT (L602-604)
    [DataRow("$sum(Account.Order.Product.(Price * Quantity))",
        """{"Account":{"Order":[{"Product":{"Price":10,"Quantity":2}},{"Product":{"Price":20,"Quantity":1}}]}}""")]
    public void FallbackExpressions_StillProduceCorrectResults(string expression, string inputJson)
    {
        // These expressions can't be inlined by CG; they fall back to RT.
        // We still verify that CG compilation succeeds and produces correct results.
        CompiledExpression compiled = s_fixture!.GetOrCompile(expression);
        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        // Run CG version
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        using JsonWorkspace cgWorkspace = JsonWorkspace.Create();
        JsonElement cgResult = Invoke(compiled.Method, inputDoc.RootElement, cgWorkspace);

        // Run RT version and compare
        using var inputDoc2 = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputJson));
        JsonElement rtResult = JsonataEvaluator.Default.Evaluate(expression, inputDoc2.RootElement);

        Assert.AreEqual(rtResult.GetRawText(), cgResult.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
    public void EvaluateExpressionWithCustomFunction_BlockFormWithBlankLines()
    {
        // L319-322: blank lines in custom function body
        var customFns = new[]
        {
            new CustomFunction(
                "add_ten",
                new[] { "x" },
                "double v = x.GetDouble();\n\nv = v + 10;\n\nreturn JsonataCodeGenHelpers.NumberFromDouble(v, workspace);",
                isExpression: false),
        };

        CompiledExpression compiled = s_fixture!.GetOrCompile("$add_ten(5)", customFns);

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("15", result.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
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

        CompiledExpression compiled = s_fixture!.GetOrCompile("$double_it(21)", customFns);

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("42", result.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
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

        CompiledExpression compiled = s_fixture!.GetOrCompile("$clamp(15, 0, 10)", customFns);

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("10", result.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
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

        CompiledExpression compiled = s_fixture!.GetOrCompile(
            "$map([1,2,3], function($v) { $triple($v) })", customFns);

        Console.WriteLine($"Error: {compiled.Error}");
        if (compiled.GeneratedCode is not null)
        {
            Console.WriteLine($"Generated:\n{compiled.GeneratedCode}");
        }

        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("null"));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("[3,6,9]", result.GetRawText());
    }

    [TestMethod]
    [TestCategory("codegen-edge")]
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

        CompiledExpression compiled = s_fixture!.GetOrCompile("$add_bonus(price)", customFns);

        Assert.IsNull(compiled.Error);
        Assert.IsNotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes("""{"price":50}"""));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = Invoke(compiled.Method, inputDoc.RootElement, workspace);
        Assert.AreEqual("60", result.GetRawText());
    }
}
