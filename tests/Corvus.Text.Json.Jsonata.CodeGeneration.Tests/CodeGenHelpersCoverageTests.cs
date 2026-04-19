// <copyright file="CodeGenHelpersCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// Data-driven coverage tests targeting uncovered branches in
/// <see cref="JsonataCodeGenHelpers"/>, identified from merged Cobertura data.
/// Every test runs the same expression through BOTH the code generator (CG)
/// and the runtime compiler (RT), asserting identical results.
/// </summary>
public class CodeGenHelpersCoverageTests : IClassFixture<CodeGenConformanceFixture>
{
    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenHelpersCoverageTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    // ═══════════════════════════════════════════════════════════════
    // Aggregate over property chains (lines 3370-3534)
    // MaxOverChainDouble, MinOverChainDouble, AverageOverChainDouble
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$max(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "30")]
    [InlineData("$min(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "10")]
    [InlineData("$average(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":30}]}",
        "20")]
    [InlineData("$sum(items.price)",
        "{\"items\":[{\"price\":5},{\"price\":15}]}",
        "20")]
    [InlineData("$max(data.nested.values)",
        "{\"data\":[{\"nested\":{\"values\":3}},{\"nested\":{\"values\":7}},{\"nested\":{\"values\":1}}]}",
        "7")]
    [InlineData("$min(data.nested.values)",
        "{\"data\":[{\"nested\":{\"values\":3}},{\"nested\":{\"values\":7}},{\"nested\":{\"values\":1}}]}",
        "1")]
    public void AggregateOverChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $each and $sift (lines 9236-9501)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$each({\"a\":1,\"b\":2}, function($v, $k) { $k & \"=\" & $string($v) })",
        "null",
        "[\"a=1\",\"b=2\"]")]
    [InlineData(
        "$sift({\"a\":1,\"b\":2,\"c\":3}, function($v) { $v > 1 })",
        "null",
        "{\"b\":2,\"c\":3}")]
    [InlineData(
        "$sift({\"x\":10,\"y\":0,\"z\":5}, function($v, $k) { $v > 0 })",
        "null",
        "{\"x\":10,\"z\":5}")]
    [InlineData(
        "$each({\"name\":\"Alice\",\"age\":30}, function($v) { $v })",
        "null",
        "[\"Alice\",30]")]
    public void EachAndSift(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Nested array flattening / property chain navigation
    // (lines 205-365, 598-622, 8763-8909)
    // NavigatePropertyChain, CollectChainFlat, ContinueChainFlat
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.details.value",
        "{\"items\":[{\"details\":{\"value\":1}},{\"details\":{\"value\":2}}]}",
        "[1,2]")]
    [InlineData("data.items.tags",
        "{\"data\":[{\"items\":[{\"tags\":\"a\"},{\"tags\":\"b\"}]},{\"items\":[{\"tags\":\"c\"}]}]}",
        "[\"a\",\"b\",\"c\"]")]
    [InlineData("a.b.c",
        "{\"a\":[{\"b\":{\"c\":1}},{\"b\":{\"c\":2}},{\"b\":{\"c\":3}}]}",
        "[1,2,3]")]
    [InlineData("orders.items.name",
        "{\"orders\":[{\"items\":[{\"name\":\"x\"},{\"name\":\"y\"}]},{\"items\":[{\"name\":\"z\"}]}]}",
        "[\"x\",\"y\",\"z\"]")]
    [InlineData("a.b",
        "{\"a\":{\"b\":42}}",
        "42")]
    [InlineData("a.b",
        "{\"a\":[{\"b\":1},{\"b\":2},{\"b\":3}]}",
        "[1,2,3]")]
    public void NavigatePropertyChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $zip operations (lines 6665-6702, 6773-6885)
    // Zip3Arrays, ZipNAry, ZipElementAndBuffer, ZipBufferAndElement
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$zip([1,2,3],[4,5,6],[7,8,9])", "null", "[[1,4,7],[2,5,8],[3,6,9]]")]
    [InlineData("$zip([1,2],[3,4],[5,6],[7,8])", "null", "[[1,3,5,7],[2,4,6,8]]")]
    [InlineData("$zip([1,2,3],[4,5])", "null", "[[1,4],[2,5]]")]
    [InlineData("$zip(items.a, items.b)",
        "{\"items\":[{\"a\":1,\"b\":10},{\"a\":2,\"b\":20}]}",
        "[[1,10],[2,20]]")]
    public void ZipOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // URL encoding/decoding (lines 5782-5864)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$encodeUrlComponent(\"hello world\")", "null")]
    [InlineData("$decodeUrlComponent(\"hello%20world\")", "null")]
    [InlineData("$encodeUrl(\"https://example.com/path\")", "null")]
    [InlineData("$decodeUrl(\"https%3A%2F%2Fexample.com\")", "null")]
    public void UrlEncoding(string expression, string data)
    {
        this.AssertCgAndRtMatchNoExpected(expression, data);
    }

    // ═══════════════════════════════════════════════════════════════
    // Map/filter over chains (lines 2685-2755)
    // MapChainElements, FilterChainElements
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$map(items.price, function($v) { $v * 2 })",
        "{\"items\":[{\"price\":5},{\"price\":10}]}",
        "[10,20]")]
    [InlineData("$filter(items.price, function($v) { $v > 7 })",
        "{\"items\":[{\"price\":5},{\"price\":10},{\"price\":3}]}",
        "10")]
    [InlineData("$map(items, function($v) { $v.name })",
        "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}",
        "[\"a\",\"b\"]")]
    public void MapFilterOverChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // String concatenation multi-operand (lines 1599-1667)
    // StringConcat3, StringConcat4, StringConcat5, StringConcatMany
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("\"a\" & \"b\" & \"c\"", "null", "\"abc\"")]
    [InlineData("\"a\" & \"b\" & \"c\" & \"d\"", "null", "\"abcd\"")]
    [InlineData("\"a\" & \"b\" & \"c\" & \"d\" & \"e\"", "null", "\"abcde\"")]
    [InlineData("\"x=\" & $string(val) & \"!\"",
        "{\"val\":42}",
        "\"x=42!\"")]
    [InlineData("name & \" is \" & $string(age) & \" years old\"",
        "{\"name\":\"Alice\",\"age\":30}",
        "\"Alice is 30 years old\"")]
    public void StringConcatMultiOperand(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fused equality predicates (lines 768-890)
    // NavigatePropertyChainWithPredicates
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items[status=\"active\"].name",
        "{\"items\":[{\"status\":\"active\",\"name\":\"a\"},{\"status\":\"inactive\",\"name\":\"b\"},{\"status\":\"active\",\"name\":\"c\"}]}",
        "[\"a\",\"c\"]")]
    [InlineData("items[type=\"x\"][0]",
        "{\"items\":[{\"type\":\"y\",\"v\":1},{\"type\":\"x\",\"v\":2},{\"type\":\"x\",\"v\":3}]}",
        "{\"type\":\"x\",\"v\":2}")]
    [InlineData("data[category=\"A\"].value",
        "{\"data\":[{\"category\":\"A\",\"value\":10},{\"category\":\"B\",\"value\":20},{\"category\":\"A\",\"value\":30}]}",
        "[10,30]")]
    public void FusedEqualityPredicates(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy operations (lines 8344-8467)
    // ═══════════════════════════════════════════════════════════════

    // GroupBy via property chain annotation syntax
    // Removed: needs specific JSONata groupBy syntax research

    // ═══════════════════════════════════════════════════════════════
    // Merge/spread/distinct (lines 5162-5256, 5468-5610)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$merge([{\"a\":1},{\"b\":2},{\"a\":3}])", "null", "{\"a\":3,\"b\":2}")]
    [InlineData("$merge([{\"x\":1},{\"y\":2}])", "null", "{\"x\":1,\"y\":2}")]
    [InlineData("$distinct([1,2,2,3,3,3,1])", "null", "[1,2,3]")]
    [InlineData("$spread({\"a\":1,\"b\":2})", "null", "[{\"a\":1},{\"b\":2}]")]
    public void MergeSpreadDistinct(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Reduce operations (lines 8103-8247)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$reduce([1,2,3,4], function($prev, $curr) { $prev + $curr })", "null", "10")]
    [InlineData("$reduce([1,2,3], function($prev, $curr) { $prev + $curr }, 100)", "null", "106")]
    [InlineData("$reduce([\"a\",\"b\",\"c\"], function($prev, $curr) { $prev & $curr }, \"start:\")",
        "null", "\"start:abc\"")]
    public void ReduceOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort with custom comparator (lines 7653-7680)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$sort([3,1,4,1,5,9])", "null", "[1,1,3,4,5,9]")]
    [InlineData("$sort(items, function($a, $b) { $a.name > $b.name })",
        "{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}",
        "[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]")]
    [InlineData("$sort(items, function($a, $b) { $a.v < $b.v })",
        "{\"items\":[{\"v\":3},{\"v\":1},{\"v\":2}]}",
        "[{\"v\":3},{\"v\":2},{\"v\":1}]")]
    public void SortOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Date/time functions (lines 7287-7342)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$toMillis(\"2024-01-15T12:00:00.000Z\")", "null", "1705320000000")]
    [InlineData("$fromMillis(1705320000000, \"[Y]-[M01]-[D01]\")", "null", "\"2024-01-15\"")]
    [InlineData("$fromMillis(0)", "null", "\"1970-01-01T00:00:00.000Z\"")]
    public void DateTimeFunctions(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Range generation (lines 1877-1881)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("[1..5]", "null", "[1,2,3,4,5]")]
    [InlineData("[0..0]", "null", "[0]")]
    [InlineData("[3..7]", "null", "[3,4,5,6,7]")]
    public void RangeGeneration(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Flatten (lines 7060-7082)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$flatten([[1,2],[3,[4,5]]])", "null")]
    [InlineData("$flatten([1,[2,[3,[4]]]])", "null")]
    [InlineData("$flatten(items.tags)",
        "{\"items\":[{\"tags\":[\"a\",\"b\"]},{\"tags\":[\"c\"]}]}")]
    public void FlattenOperations(string expression, string data)
    {
        this.AssertCgAndRtMatchNoExpected(expression, data);
    }

    // ═══════════════════════════════════════════════════════════════
    // Regex match (lines 6370-6409)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$match(\"hello world\", /wo/)", "null")]
    [InlineData("$match(\"abc123\", /([a-z]+)(\\d+)/)", "null")]
    [InlineData("$contains(\"test123\", /\\d+/)", "null", "true")]
    [InlineData("$replace(\"hello\", /l/, \"r\")", "null", "\"herro\"")]
    public void RegexOperations(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchNoExpected(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard and descendant (lines 601-622)
    // EnumerateWildcard, EnumerateDescendant
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("*", "{\"a\":1,\"b\":2,\"c\":3}", "[1,2,3]")]
    [InlineData("**.v", "{\"a\":{\"v\":1},\"b\":{\"c\":{\"v\":2}}}")]
    [InlineData("items.*",
        "{\"items\":{\"x\":1,\"y\":2,\"z\":3}}",
        "[1,2,3]")]
    public void WildcardAndDescendant(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchNoExpected(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Encoding: base64 (lines 5782+ adjacent)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$base64encode(\"hello\")", "null")]
    [InlineData("$base64decode(\"aGVsbG8=\")", "null", "\"hello\"")]
    public void Base64Operations(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchNoExpected(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Count/exists over chains (lines 2287-2306)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$count(items.name)",
        "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}",
        "3")]
    [InlineData("$exists(items.name)",
        "{\"items\":[{\"name\":\"a\"}]}",
        "true")]
    [InlineData("$exists(items.missing)",
        "{\"items\":[{\"name\":\"a\"}]}",
        "false")]
    public void CountExistsOverChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $string on complex objects (lines 9236-9260)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$string({\"a\":1,\"b\":[2,3]})", "null")]
    [InlineData("$string([1,2,3])", "null")]
    [InlineData("$string(null)", "null", "\"null\"")]
    [InlineData("$string(true)", "null", "\"true\"")]
    public void StringifyComplex(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchNoExpected(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // $split with empty separator (lines 4026-4044)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$split(\"abc\", \"\")", "null", "[\"a\",\"b\",\"c\"]")]
    [InlineData("$split(\"hello\", \"l\")", "null", "[\"he\",\"\",\"o\"]")]
    [InlineData("$split(\"a.b.c\", \".\")", "null", "[\"a\",\"b\",\"c\"]")]
    public void SplitOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Arithmetic via CG
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("a + b", "{\"a\":10,\"b\":20}", "30")]
    [InlineData("a - b", "{\"a\":20,\"b\":7}", "13")]
    [InlineData("a * b", "{\"a\":3,\"b\":4}", "12")]
    [InlineData("a / b", "{\"a\":10,\"b\":4}", "2.5")]
    [InlineData("a % b", "{\"a\":10,\"b\":3}", "1")]
    [InlineData("-val", "{\"val\":42}", "-42")]
    public void ArithmeticWithData(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatBase / $formatNumber (lines 7287+)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatBase(255, 16)", "null", "\"ff\"")]
    [InlineData("$formatBase(10, 2)", "null", "\"1010\"")]
    [InlineData("$formatBase(255, 8)", "null", "\"377\"")]
    public void FormatBase(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $lookup / $keys / $values (lines 4713-4766)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$keys({\"a\":1,\"b\":2,\"c\":3})", "null", "[\"a\",\"b\",\"c\"]")]
    [InlineData("$values({\"a\":1,\"b\":2,\"c\":3})", "null", "[1,2,3]")]
    [InlineData("$lookup({\"a\":1,\"b\":2}, \"b\")", "null", "2")]
    public void KeysValuesLookup(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void LookupMissing_ReturnsUndefined()
    {
        this.AssertCgAndRtBothUndefined("$lookup({\"a\":1,\"b\":2}, \"missing\")", "null");
    }

    // ═══════════════════════════════════════════════════════════════
    // $pad (string padding)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$pad(\"hello\", 10)", "null", "\"hello     \"")]
    [InlineData("$pad(\"hello\", -10)", "null", "\"     hello\"")]
    [InlineData("$pad(\"hello\", 10, \"#\")", "null", "\"hello#####\"")]
    public void PadOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $shuffle (lines 5162-5185)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void Shuffle_ProducesSameLengthArray()
    {
        string expression = "$shuffle([1,2,3,4,5])";
        string data = "null";

        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Array, cgResult.ValueKind);
        Assert.Equal(5, cgResult.GetArrayLength());

        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        Assert.NotNull(rtResult);
    }

    // ═══════════════════════════════════════════════════════════════
    // Comparison operators via CG (lines 1030-1035)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("a < b", "{\"a\":1,\"b\":2}", "true")]
    [InlineData("a > b", "{\"a\":3,\"b\":2}", "true")]
    [InlineData("a <= b", "{\"a\":2,\"b\":2}", "true")]
    [InlineData("a >= b", "{\"a\":1,\"b\":2}", "false")]
    [InlineData("a = b", "{\"a\":1,\"b\":1}", "true")]
    [InlineData("a != b", "{\"a\":1,\"b\":2}", "true")]
    [InlineData("a in [\"x\",\"y\",\"z\"]", "{\"a\":\"y\"}", "true")]
    public void ComparisonOperators(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $append / $reverse (lines 5252-5256)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$append([1,2], [3,4])", "null", "[1,2,3,4]")]
    [InlineData("$append(1, [2,3])", "null", "[1,2,3]")]
    [InlineData("$append([1,2], 3)", "null", "[1,2,3]")]
    [InlineData("$reverse([1,2,3])", "null", "[3,2,1]")]
    [InlineData("$reverse(items)", "{\"items\":[10,20,30]}", "[30,20,10]")]
    public void AppendReverse(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $type / $length (lines 7780-7830)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$type(42)", "null", "\"number\"")]
    [InlineData("$type(\"hello\")", "null", "\"string\"")]
    [InlineData("$type(true)", "null", "\"boolean\"")]
    [InlineData("$type(null)", "null", "\"null\"")]
    [InlineData("$type({\"a\":1})", "null", "\"object\"")]
    [InlineData("$type([1,2])", "null", "\"array\"")]
    [InlineData("$length(\"hello\")", "null", "5")]
    [InlineData("$count([1,2,3])", "null", "3")]
    public void TypeAndLength(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Chain with keep-array and wildcard (lines 601-622)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.name[]",
        "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}",
        "[\"a\",\"b\"]")]
    [InlineData("data.*[]",
        "{\"data\":{\"x\":[1,2],\"y\":[3,4]}}")]
    public void ChainWithKeepArray(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchNoExpected(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Substring operations
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$substringBefore(\"hello world\", \" \")", "null", "\"hello\"")]
    [InlineData("$substringAfter(\"hello world\", \" \")", "null", "\"world\"")]
    [InlineData("$substring(\"hello world\", 3, 5)", "null", "\"lo wo\"")]
    [InlineData("$substring(\"hello\", -3)", "null", "\"llo\"")]
    public void SubstringOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $join with separator (lines 2498-2505)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$join(items.name, \", \")",
        "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}",
        "\"a, b, c\"")]
    [InlineData("$join([\"x\",\"y\",\"z\"])", "null", "\"xyz\"")]
    public void JoinOverChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $single with predicate (lines 5243-5256)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$single([1,2,3], function($v) { $v = 2 })", "null", "2")]
    public void SingleWithPredicate(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Conditional / ternary
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("active ? name : \"none\"",
        "{\"active\":true,\"name\":\"Alice\"}",
        "\"Alice\"")]
    [InlineData("active ? name : \"none\"",
        "{\"active\":false,\"name\":\"Alice\"}",
        "\"none\"")]
    [InlineData("$count(items) > 0 ? items[0] : null",
        "{\"items\":[42]}",
        "42")]
    public void ConditionalExpressions(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $sum/$count/$max/$min/$average over chains (lines 2294-2505)
    // SumOverChain, SumOverChainCore, AggregateBuffer
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$sum(data.nested.v)",
        "{\"data\":[{\"nested\":{\"v\":1}},{\"nested\":{\"v\":2}},{\"nested\":{\"v\":3}}]}",
        "6")]
    [InlineData("$count(data.items)",
        "{\"data\":[{\"items\":\"a\"},{\"items\":\"b\"},{\"items\":\"c\"},{\"items\":\"d\"}]}",
        "4")]
    [InlineData("$average(scores.val)",
        "{\"scores\":[{\"val\":10},{\"val\":20},{\"val\":30}]}",
        "20")]
    public void AggregateOverDeepChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $distinct over chains (lines 5468-5472)
    // ChainDistinct
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$distinct(items.cat)",
        "{\"items\":[{\"cat\":\"A\"},{\"cat\":\"B\"},{\"cat\":\"A\"},{\"cat\":\"C\"}]}",
        "[\"A\",\"B\",\"C\"]")]
    public void DistinctOverChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $map with index parameter via CG
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$map([10,20,30], function($v, $i) { $i })", "null", "[0,1,2]")]
    [InlineData("$map([10,20,30], function($v, $i) { $v + $i })", "null", "[10,21,32]")]
    public void MapWithIndex(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Object construction via CG
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("{\"sum\": a + b}", "{\"a\":3,\"b\":4}", "{\"sum\":7}")]
    [InlineData("{\"x\": $count(items)}", "{\"items\":[1,2,3]}", "{\"x\":3}")]
    public void ObjectConstruction(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper methods
    // ═══════════════════════════════════════════════════════════════

    private void AssertCgAndRtMatch(string expression, string data, string expectedJson)
    {
        // === CG path ===
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Inlined:    {compiled.IsInlined}");

        if (compiled.Error is not null)
        {
            Assert.Fail($"CG compilation failed: {compiled.Error}");
        }

        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(expectedJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        string cgJson = cgResult.ValueKind == JsonValueKind.Undefined ? "undefined" : cgResult.GetRawText();

        // === RT path ===
        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        string rtJson = rtResult ?? "undefined";

        this.output.WriteLine($"Expected: {expectedJson}");
        this.output.WriteLine($"CG:       {cgJson}");
        this.output.WriteLine($"RT:       {rtJson}");

        // Assert CG matches expected
        AssertJsonEqual(expectedDoc.RootElement, cgResult);

        // Assert RT matches expected
        if (rtJson != "undefined")
        {
            using var rtDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(rtJson));
            AssertJsonEqual(expectedDoc.RootElement, rtDoc.RootElement);
        }
        else
        {
            Assert.Equal("undefined", expectedJson);
        }
    }

    private void AssertCgAndRtBothUndefined(string expression, string data)
    {
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Undefined, cgResult.ValueKind);

        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        Assert.Null(rtResult);
    }

    private void AssertCgAndRtMatchNoExpected(string expression, string data)
    {
        // === CG path ===
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Inlined:    {compiled.IsInlined}");

        if (compiled.Error is not null)
        {
            Assert.Fail($"CG compilation failed: {compiled.Error}");
        }

        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        string cgJson = cgResult.ValueKind == JsonValueKind.Undefined ? "undefined" : cgResult.GetRawText();

        // === RT path ===
        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        string rtJson = rtResult ?? "undefined";

        this.output.WriteLine($"CG: {cgJson}");
        this.output.WriteLine($"RT: {rtJson}");

        // Assert CG and RT produce the same output
        if (cgJson == "undefined" && rtJson == "undefined")
        {
            return;
        }

        if (cgJson == "undefined" || rtJson == "undefined")
        {
            Assert.Fail($"Mismatch: CG={cgJson}, RT={rtJson}");
        }

        using var cgDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(cgJson));
        using var rtDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(rtJson));
        AssertJsonEqual(rtDoc.RootElement, cgDoc.RootElement);
    }

    private static JsonElement InvokeCg(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
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
            Assert.Fail($"Value kind mismatch: expected {expected.ValueKind} ({expected.GetRawText()}), actual {actual.ValueKind} ({(actual.ValueKind == JsonValueKind.Undefined ? "undefined" : actual.GetRawText())})");
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
