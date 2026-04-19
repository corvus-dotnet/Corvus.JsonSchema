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
    [InlineData("$encodeUrlComponent(\"hello world\")", "null", "\"hello%20world\"")]
    [InlineData("$decodeUrlComponent(\"hello%20world\")", "null", "\"hello world\"")]
    [InlineData("$encodeUrl(\"https://example.com/path\")", "null", "\"https://example.com/path\"")]
    [InlineData("$decodeUrl(\"https%3A%2F%2Fexample.com\")", "null", "\"https://example.com\"")]
    public void UrlEncoding(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
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
    [InlineData("$flatten([[1,2],[3,[4,5]]])", "null", "[1,2,3,4,5]")]
    [InlineData("$flatten([1,[2,[3,[4]]]])", "null", "[1,2,3,4]")]
    [InlineData("$flatten(items.tags)",
        "{\"items\":[{\"tags\":[\"a\",\"b\"]},{\"tags\":[\"c\"]}]}",
        "[\"a\",\"b\",\"c\"]")]
    public void FlattenOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Regex match (lines 6370-6409)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$match(\"hello world\", /wo/)", "null",
        "{\"match\":\"wo\",\"index\":6,\"groups\":[]}")]
    [InlineData("$match(\"abc123\", /([a-z]+)(\\d+)/)", "null",
        "{\"match\":\"abc123\",\"index\":0,\"groups\":[\"abc\",\"123\"]}")]
    [InlineData("$contains(\"test123\", /\\d+/)", "null", "true")]
    [InlineData("$replace(\"hello\", /l/, \"r\")", "null", "\"herro\"")]
    public void RegexOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard and descendant (lines 601-622)
    // EnumerateWildcard, EnumerateDescendant
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("*", "{\"a\":1,\"b\":2,\"c\":3}", "[1,2,3]")]
    [InlineData("**.v", "{\"a\":{\"v\":1},\"b\":{\"c\":{\"v\":2}}}", "[1,2]")]
    [InlineData("items.*",
        "{\"items\":{\"x\":1,\"y\":2,\"z\":3}}",
        "[1,2,3]")]
    public void WildcardAndDescendant(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Encoding: base64 (lines 5782+ adjacent)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$base64encode(\"hello\")", "null", "\"aGVsbG8=\"")]
    [InlineData("$base64decode(\"aGVsbG8=\")", "null", "\"hello\"")]
    public void Base64Operations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
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
    [InlineData("$string({\"a\":1,\"b\":[2,3]})", "null", "\"{\\\"a\\\":1,\\\"b\\\":[2,3]}\"")]
    [InlineData("$string([1,2,3])", "null", "\"[1,2,3]\"")]
    [InlineData("$string(null)", "null", "\"null\"")]
    [InlineData("$string(true)", "null", "\"true\"")]
    public void StringifyComplex(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
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
        "{\"data\":{\"x\":[1,2],\"y\":[3,4]}}",
        "[1,2,3,4]")]
    public void ChainWithKeepArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
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
    // NavigatePropertyChain FALLBACK — array at intermediate level
    // (lines 2312-2360: else branches in nested TryGetProperty)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Root is array → i==0 fallback (line 2328)
    [InlineData("a.b.c",
        "[{\"a\":{\"b\":{\"c\":1}}},{\"a\":{\"b\":{\"c\":2}}}]",
        "[1,2]")]
    // 4-step chain where 2nd step yields array → middle fallback (line 2356)
    [InlineData("a.b.c.d",
        "{\"a\":[{\"b\":{\"c\":{\"d\":10}}},{\"b\":{\"c\":{\"d\":20}}}]}",
        "[10,20]")]
    // 5-step chain with array at step 2
    [InlineData("a.b.c.d.e",
        "{\"a\":{\"b\":[{\"c\":{\"d\":{\"e\":99}}}]}}",
        "99")]
    public void NavigatePropertyChainFallback(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // SimpleGroupByPerElement — .{keyName: valueName} as path step
    // (line 2499: single-pair NameNode key/value)
    // Note: items{...} = annotation (SimpleGroupByAnnotation)
    //       items.{...} = path step (SimpleGroupByPerElement)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.{category: value}",
        "{\"items\":[{\"category\":\"A\",\"value\":1},{\"category\":\"B\",\"value\":2},{\"category\":\"A\",\"value\":3}]}",
        "[{\"A\":1},{\"B\":2},{\"A\":3}]")]
    [InlineData("items.{type: name}",
        "{\"items\":[{\"type\":\"x\",\"name\":\"foo\"},{\"type\":\"y\",\"name\":\"bar\"},{\"type\":\"x\",\"name\":\"baz\"}]}",
        "[{\"x\":\"foo\"},{\"y\":\"bar\"},{\"x\":\"baz\"}]")]
    public void SimpleGroupByPerElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // FusedChainGroupByPerElement — chain + {keyName: valueName}
    // (line 2762: 2+ NameNode chain + single-pair NameNode groupby)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("data.items{category: value}",
        "{\"data\":{\"items\":[{\"category\":\"A\",\"value\":1},{\"category\":\"B\",\"value\":2},{\"category\":\"A\",\"value\":3}]}}",
        "{\"A\":[1,3],\"B\":2}")]
    [InlineData("store.products{brand: price}",
        "{\"store\":{\"products\":[{\"brand\":\"X\",\"price\":10},{\"brand\":\"Y\",\"price\":20},{\"brand\":\"X\",\"price\":30}]}}",
        "{\"X\":[10,30],\"Y\":20}")]
    public void FusedChainGroupByPerElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // ZipFromBuffers — $zip where ALL args are property chains
    // (line 6835: 2-arg, line 6839: 3-arg)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // 2-arg: both are 2-step chains
    [InlineData("$zip(data.prices, data.quantities)",
        "{\"data\":{\"prices\":[10,20,30],\"quantities\":[2,3,4]}}",
        "[[10,2],[20,3],[30,4]]")]
    // 3-arg: all three are chains
    [InlineData("$zip(data.a, data.b, data.c)",
        "{\"data\":{\"a\":[1,2],\"b\":[3,4],\"c\":[5,6]}}",
        "[[1,3,5],[2,4,6]]")]
    public void ZipFromBuffers(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // ZipBufferAndElement / ZipElementAndBuffer — mixed chain+literal
    // (line 6847: chain first, line 6851: literal first)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // chain + literal → ZipBufferAndElement
    [InlineData("$zip(data.items, [10,20,30])",
        "{\"data\":{\"items\":[1,2,3]}}",
        "[[1,10],[2,20],[3,30]]")]
    // literal + chain → ZipElementAndBuffer
    [InlineData("$zip([10,20,30], data.items)",
        "{\"data\":{\"items\":[1,2,3]}}",
        "[[10,1],[20,2],[30,3]]")]
    // chain + computed expression → ZipBufferAndElement
    [InlineData("$zip(data.values, $reverse([7,8,9]))",
        "{\"data\":{\"values\":[1,2,3]}}",
        "[[1,9],[2,8],[3,7]]")]
    public void ZipMixedBufferAndElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // MapElementsDouble — $map with arithmetic lambda body (non-chain)
    // (line 5640: MapElementsDouble specialization)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$map([1,2,3,4,5], function($v){$v * 2})",
        "null",
        "[2,4,6,8,10]")]
    [InlineData("$map([10,20,30], function($v){$v + 5})",
        "null",
        "[15,25,35]")]
    [InlineData("$map([100,200], function($v){$v / 4})",
        "null",
        "[25,50]")]
    [InlineData("$map(items, function($v){$v - 1})",
        "{\"items\":[5,10,15]}",
        "[4,9,14]")]
    public void MapElementsDouble(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // MapChainDouble — chain + arithmetic computed step
    // (line 2741: data.items.($ * 2))
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("data.items.($ * 2)",
        "{\"data\":{\"items\":[1,2,3,4]}}",
        "[2,4,6,8]")]
    [InlineData("data.values.($ + 10)",
        "{\"data\":{\"values\":[5,15,25]}}",
        "[15,25,35]")]
    [InlineData("data.prices.($ / 100)",
        "{\"data\":{\"prices\":[150,250,350]}}",
        "[1.5,2.5,3.5]")]
    public void MapChainDouble(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // SumOverChainDoubleRaw — $sum(chain.(arithmetic))
    // (line 4725: prefix is simple chain + last step arithmetic)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$sum(data.items.(price * quantity))",
        "{\"data\":{\"items\":[{\"price\":10,\"quantity\":2},{\"price\":20,\"quantity\":3}]}}",
        "80")]
    [InlineData("$sum(data.values.($ + 1))",
        "{\"data\":{\"values\":[1,2,3]}}",
        "9")]
    public void SumOverChainDoubleRaw(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // SumOverElementsDoubleRaw — $sum(filtered.(arithmetic))
    // (line 4740: prefix is NOT simple chain → fallback path)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$sum(items[type=\"A\"].(price * qty))",
        "{\"items\":[{\"type\":\"A\",\"price\":10,\"qty\":2},{\"type\":\"B\",\"price\":5,\"qty\":1},{\"type\":\"A\",\"price\":7,\"qty\":3}]}",
        "41")]
    [InlineData("$sum(items[active=true].(value + bonus))",
        "{\"items\":[{\"active\":true,\"value\":10,\"bonus\":5},{\"active\":false,\"value\":100,\"bonus\":50},{\"active\":true,\"value\":20,\"bonus\":3}]}",
        "38")]
    public void SumOverElementsDoubleRaw(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // EachProperty — $each with 2-param and 3-param lambdas on data
    // (line 6305: 3-param, line 6309: 2-param)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // 2-param on data property
    [InlineData("$each(obj, function($v, $k){ $k & \":\" & $string($v) })",
        "{\"obj\":{\"x\":1,\"y\":2,\"z\":3}}",
        "[\"x:1\",\"y:2\",\"z:3\"]")]
    // 1-param $each — just values
    [InlineData("$each(obj, function($v){ $v * 2 })",
        "{\"obj\":{\"a\":1,\"b\":2,\"c\":3}}",
        "[2,4,6]")]
    public void EachPropertyOnData(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // FusedChainBuildArray — [chain.chain.chain.{"key": expr, ...}]
    // (line 4254: array wrapping chain + StringNode-keyed object ctor)
    // Requires: ArrayConstructor(1 expr) → PathNode(3+ steps)
    //   where last step is ObjectConstructor with all StringNode keys
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("[data.items.profiles.{\"fullName\": name, \"years\": age}]",
        "{\"data\":{\"items\":[{\"profiles\":[{\"name\":\"Alice\",\"age\":30},{\"name\":\"Bob\",\"age\":25}]}]}}",
        "[{\"fullName\":\"Alice\",\"years\":30},{\"fullName\":\"Bob\",\"years\":25}]")]
    [InlineData("[store.dept.employees.{\"id\": empId, \"title\": role}]",
        "{\"store\":{\"dept\":[{\"employees\":[{\"empId\":1,\"role\":\"dev\"},{\"empId\":2,\"role\":\"qa\"}]}]}}",
        "[{\"id\":1,\"title\":\"dev\"},{\"id\":2,\"title\":\"qa\"}]")]
    public void FusedChainBuildArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // AverageOverChain via ~> pipe (line 2895)
    // Also: SumOverChain, CountOverChain, MaxOverChain, MinOverChain
    // These use EmitApply → TryGetFusedChainAggregateHelper
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("data.scores ~> $average",
        "{\"data\":{\"scores\":[10,20,30]}}",
        "20")]
    [InlineData("data.values ~> $sum",
        "{\"data\":{\"values\":[1,2,3,4]}}",
        "10")]
    [InlineData("data.items ~> $count",
        "{\"data\":{\"items\":[\"a\",\"b\",\"c\"]}}",
        "3")]
    [InlineData("data.nums ~> $max",
        "{\"data\":{\"nums\":[3,7,1,9,2]}}",
        "9")]
    [InlineData("data.nums ~> $min",
        "{\"data\":{\"nums\":[3,7,1,9,2]}}",
        "1")]
    public void AggregateOverChainViaPipe(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort — chain-fused with comparator (line 6069)
    // $sort(chain, comparator) where first arg is 2+ step chain
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Chain-fused sort with comparator
    [InlineData("$sort(data.items, function($a, $b) { $a > $b })",
        "{\"data\":{\"items\":[3,1,4,1,5]}}",
        "[1,1,3,4,5]")]
    // Chain-fused sort by property
    [InlineData("$sort(data.records, function($a, $b) { $a.score > $b.score })",
        "{\"data\":{\"records\":[{\"score\":30},{\"score\":10},{\"score\":20}]}}",
        "[{\"score\":10},{\"score\":20},{\"score\":30}]")]
    // Chain-fused default sort (no comparator)
    [InlineData("$sort(data.values)",
        "{\"data\":{\"values\":[5,2,8,1,9]}}",
        "[1,2,5,8,9]")]
    public void SortChainFused(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // AverageOverChainDouble — $average(chain.(arithmetic))
    // Uses TryEmitAggregationChainFusion with "AverageOverChainDouble"
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$average(data.items.(price * quantity))",
        "{\"data\":{\"items\":[{\"price\":10,\"quantity\":2},{\"price\":20,\"quantity\":3}]}}",
        "40")]
    public void AverageOverChainDouble(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Concat with auto-map — chain property in string concat
    // (lines 4595-4622: ConcatBuilder with AppendAutoMap)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Single element from chain in concat
    [InlineData("\"Prefix: \" & data.name",
        "{\"data\":{\"name\":\"Alice\"}}",
        "\"Prefix: Alice\"")]
    [InlineData("data.label & \" suffix\"",
        "{\"data\":{\"label\":\"hello\"}}",
        "\"hello suffix\"")]
    // Multi-step chain concat
    [InlineData("\"v=\" & $string(data.info.value)",
        "{\"data\":{\"info\":{\"value\":42}}}",
        "\"v=42\"")]
    public void ConcatWithChainProperty(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // SortBy with keys (line 2707: SortByKeys)
    // Path step annotation with ^(>key) or ^(<key) syntax
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items^(price)",
        "{\"items\":[{\"name\":\"c\",\"price\":30},{\"name\":\"a\",\"price\":10},{\"name\":\"b\",\"price\":20}]}",
        "[{\"name\":\"a\",\"price\":10},{\"name\":\"b\",\"price\":20},{\"name\":\"c\",\"price\":30}]")]
    [InlineData("items^(>price)",
        "{\"items\":[{\"name\":\"a\",\"price\":10},{\"name\":\"b\",\"price\":20},{\"name\":\"c\",\"price\":30}]}",
        "[{\"name\":\"c\",\"price\":30},{\"name\":\"b\",\"price\":20},{\"name\":\"a\",\"price\":10}]")]
    public void SortByKeys(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // String concatenation with 6+ operands (StringConcatMany)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("\"a\" & \"b\" & \"c\" & \"d\" & \"e\" & \"f\"", "null", "\"abcdef\"")]
    [InlineData("\"1\" & \"2\" & \"3\" & \"4\" & \"5\" & \"6\" & \"7\"", "null", "\"1234567\"")]
    public void StringConcatMany(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // String concat with auto-mapped chain (BeginConcatRented + CoerceToStringElement)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("\"prefix:\" & name",
        "{\"name\":\"hello\"}",
        "\"prefix:hello\"")]
    [InlineData("\"val=\" & $string(num)",
        "{\"num\":42}",
        "\"val=42\"")]
    public void StringConcatWithChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $each with 2-parameter and 3-parameter functions
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$each({\"a\":1,\"b\":2}, function($v,$k){$k & \"=\" & $string($v)})",
        "null",
        "[\"a=1\",\"b=2\"]")]
    public void EachPropertyThreeParam(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Chain operations: ChainDistinct, ChainKeepSingletonArray, ChainMerge
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.category",
        "{\"items\":[{\"category\":\"a\"},{\"category\":\"b\"},{\"category\":\"a\"},{\"category\":\"c\"}]}",
        "[\"a\",\"b\",\"a\",\"c\"]")]
    public void ChainPropertyOverArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber via CG path (CreateFormatNumberPicture)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatNumber(num, \"#,##0.00\")",
        "{\"num\":12345.6}",
        "\"12,345.60\"")]
    [InlineData("$formatNumber(val, \"000\")",
        "{\"val\":7}",
        "\"007\"")]
    public void FormatNumberViaCg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $now and $millis (nullary builtins)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void NowReturnsTimestamp()
    {
        // $now() and $millis() are time-sensitive — can't compare CG vs RT
        CompiledExpression compiled = this.fixture.GetOrCompile("$now()");
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse("null"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal(JsonValueKind.String, cgResult.ValueKind);
    }

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void MillisReturnsNumber()
    {
        CompiledExpression compiled = this.fixture.GetOrCompile("$millis()");
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse("null"u8.ToArray());
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        Assert.Equal(JsonValueKind.Number, cgResult.ValueKind);
        Assert.True(cgResult.GetDouble() > 0);
    }

    // ═══════════════════════════════════════════════════════════════
    // $exists over chain (ExistsOverChain)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$exists(data.name)", "{\"data\":{\"name\":\"hello\"}}", "true")]
    [InlineData("$exists(data.missing)", "{\"data\":{\"name\":\"hello\"}}", "false")]
    public void ExistsOverChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Comparison operators (CompareNumberGTE/LTE)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("a >= b", "{\"a\":5,\"b\":3}", "true")]
    [InlineData("a >= b", "{\"a\":3,\"b\":3}", "true")]
    [InlineData("a >= b", "{\"a\":2,\"b\":3}", "false")]
    [InlineData("a <= b", "{\"a\":2,\"b\":3}", "true")]
    [InlineData("a <= b", "{\"a\":3,\"b\":3}", "true")]
    [InlineData("a <= b", "{\"a\":5,\"b\":3}", "false")]
    public void CompareNumberGteLte(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Arithmetic operators (BinaryArithmetic)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("a + b", "{\"a\":10,\"b\":3}", "13")]
    [InlineData("a - b", "{\"a\":10,\"b\":3}", "7")]
    [InlineData("a * b", "{\"a\":10,\"b\":3}", "30")]
    [InlineData("a / b", "{\"a\":10,\"b\":2}", "5")]
    [InlineData("a % b", "{\"a\":10,\"b\":3}", "1")]
    public void BinaryArithmetic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $shuffle via CG (ShuffleFromBuffer)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void ShuffleFromBufferViaCg()
    {
        // $shuffle randomizes order, but $count is deterministic
        this.AssertCgAndRtMatch("$count($shuffle(items))", "{\"items\":[1,2,3,4,5]}", "5");
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with regex backreference via CG
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$replace(text, /(\\w+)\\s(\\w+)/, \"$2 $1\")",
        "{\"text\":\"John Smith\"}",
        "\"Smith John\"")]
    public void ReplaceBackreferenceViaCg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $parseInteger with XPath picture via CG
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$parseInteger(val, \"#0\")", "{\"val\":\"42\"}", "42")]
    [InlineData("$parseInteger(val, \"#,##0\")", "{\"val\":\"1,234\"}", "1234")]
    public void ParseIntegerViaCg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard and descendant enumeration (EnumerateWildcard)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("data.*",
        "{\"data\":{\"a\":1,\"b\":2,\"c\":3}}",
        "[1,2,3]")]
    public void WildcardEnumeration(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void DescendantEnumeration()
    {
        this.AssertCgAndRtMatch("data.**", "{\"data\":{\"a\":{\"x\":1},\"b\":2}}", "[{\"a\":{\"x\":1},\"b\":2},{\"x\":1},1,2]");
    }

    // ═══════════════════════════════════════════════════════════════
    // FusedCollectAndContinue — chain with predicate filter
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items[price > 20].name",
        "{\"items\":[{\"name\":\"a\",\"price\":10},{\"name\":\"b\",\"price\":30},{\"name\":\"c\",\"price\":25}]}",
        "[\"b\",\"c\"]")]
    [InlineData("items[type=\"fruit\"].name",
        "{\"items\":[{\"name\":\"apple\",\"type\":\"fruit\"},{\"name\":\"carrot\",\"type\":\"veg\"},{\"name\":\"banana\",\"type\":\"fruit\"}]}",
        "[\"apple\",\"banana\"]")]
    public void FusedCollectAndContinue(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // FusedEvalFromStep — chain with computed step
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.(name & \":\" & $string(price))",
        "{\"items\":[{\"name\":\"a\",\"price\":10},{\"name\":\"b\",\"price\":20}]}",
        "[\"a:10\",\"b:20\"]")]
    public void FusedEvalFromStep(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy annotation (SimpleGroupByAnnotation)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items{category: name}",
        "{\"items\":[{\"name\":\"a\",\"category\":\"x\"},{\"name\":\"b\",\"category\":\"y\"},{\"name\":\"c\",\"category\":\"x\"}]}",
        "{\"x\":[\"a\",\"c\"],\"y\":\"b\"}")]
    public void SimpleGroupByAnnotation(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy per-element via path step (SimpleGroupByPerElement + FusedChainGroupByPerElement)
    // Uses dot before { to make it a path step rather than annotation
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items.{category: name}",
        "{\"items\":[{\"name\":\"a\",\"category\":\"x\"},{\"name\":\"b\",\"category\":\"y\"},{\"name\":\"c\",\"category\":\"x\"}]}",
        "[{\"x\":\"a\"},{\"y\":\"b\"},{\"x\":\"c\"}]")]
    [InlineData("data.items.{category: name}",
        "{\"data\":{\"items\":[{\"name\":\"a\",\"category\":\"x\"},{\"name\":\"b\",\"category\":\"y\"}]}}",
        "[{\"x\":\"a\"},{\"y\":\"b\"}]")]
    public void GroupByPerElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $zip variadic (4+ args) — exercises Zip(JsonElement[], workspace)
    // Lines 6654-6705 in JsonataCodeGenHelpers.cs (52 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$zip([1,2],[3,4],[5,6],[7,8],[9,10])", "null",
        "[[1,3,5,7,9],[2,4,6,8,10]]")]
    [InlineData("$zip([\"a\"],[\"b\"],[\"c\"],[\"d\"])", "null",
        "[[\"a\",\"b\",\"c\",\"d\"]]")]
    public void ZipVariadic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $zip single arg — exercises Zip(JsonElement, workspace)
    // Lines 6529-6556 in JsonataCodeGenHelpers.cs (28 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$zip([1,2,3])", "null", "[[1],[2],[3]]")]
    [InlineData("$zip([\"a\",\"b\"])", "null", "[[\"a\"],[\"b\"]]")]
    public void ZipSingleArg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $split with empty separator — exercises Split empty separator path
    // Lines 4024-4046 in JsonataCodeGenHelpers.cs (23 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$split(\"hello\", \"\")", "null",
        "[\"h\",\"e\",\"l\",\"l\",\"o\"]")]
    [InlineData("$split(\"hello\", \"\", 3)", "null",
        "[\"h\",\"e\",\"l\"]")]
    public void SplitEmptySeparator(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with grouping separators
    // Lines 4811-4853 in BuiltInFunctions.cs (ComputeRegularGrouping, 32 uncovered)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatNumber(1234567, \"#,##0\")", "null", "\"1,234,567\"")]
    [InlineData("$formatNumber(1234567.89, \"#,##0.00\")", "null", "\"1,234,567.89\"")]
    public void FormatNumberGrouping(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with exponent pattern
    // Lines 4899-4919, 5047-5072 in BuiltInFunctions.cs (~47 uncovered lines)
    // Note: reference jsonata 1.8.7 hangs on exponent patterns
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatNumber(12345, \"0.00e0\")", "null")]
    [InlineData("$formatNumber(0.00123, \"0.00e0\")", "null")]
    [InlineData("$formatNumber(-42.5, \"0.0e0\")", "null")]
    [InlineData("$formatNumber(999, \"0.0e0\")", "null")]
    public void FormatNumberExponent(string expression, string data)
    {
        // Reference jsonata 1.8.7 hangs on exponent patterns — verify CG == RT
        this.AssertCgAndRtMatchCorvusExtension(expression, data);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatInteger with word/roman — exercises XPathDateTimeFormatter
    // Lines 1655-1673 (Unicode digit + grouping), 533-540 (ordinal modifier)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatInteger(42, \"w\")", "null", "\"forty-two\"")]
    [InlineData("$formatInteger(42, \"W\")", "null", "\"FORTY-TWO\"")]
    [InlineData("$formatInteger(42, \"Ww\")", "null", "\"Forty-Two\"")]
    [InlineData("$formatInteger(5, \"I\")", "null", "\"V\"")]
    [InlineData("$formatInteger(1999, \"I\")", "null", "\"MCMXCIX\"")]
    [InlineData("$formatInteger(100, \"w\")", "null", "\"one hundred\"")]
    [InlineData("$formatInteger(0, \"w\")", "null", "\"zero\"")]
    [InlineData("$formatInteger(1234, \"#,##0\")", "null")]
    public void FormatIntegerViaCg(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchCorvusExtension(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // $parseInteger — exercises XPathDateTimeFormatter parsing paths
    // Lines 2712-2737 (grouping skip), 2849-2860 (plus sign),
    // 2444-2452 (scale words)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$parseInteger(\"42\", \"0\")", "null", "42")]
    [InlineData("$parseInteger(\"1,234\", \"#,##0\")", "null", "1234")]
    [InlineData("$parseInteger(\"forty-two\", \"w\")", "null", "42")]
    [InlineData("$parseInteger(\"XLII\", \"I\")", "null", "42")]
    [InlineData("$parseInteger(\"one hundred\", \"w\")", "null", "100")]
    [InlineData("$parseInteger(\"one thousand\", \"w\")", "null", "1000")]
    [InlineData("$parseInteger(\"five thousand two hundred\", \"w\")", "null", "5200")]
    public void ParseIntegerCoverage(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $fromMillis/$toMillis with picture strings — exercises XPathDateTimeFormatter
    // Lines 572-598 (TryParseInteger), 1104-1114 (timezone 2-3 char),
    // 3015-3026 (literal bracket), 3540-3553 (skip word)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$fromMillis(1704067200000, \"[Y0001]-[M01]-[D01]\")", "null", "\"2024-01-01\"")]
    [InlineData("$fromMillis(1704067200000, \"[H01]:[m01]:[s01]\")", "null", "\"00:00:00\"")]
    [InlineData("$toMillis(\"2024-01-01\", \"[Y]-[M01]-[D01]\")", "null", "1704067200000")]
    // Day-of-week name, ordinal day, month name — exercises FormatComponent branches
    [InlineData("$fromMillis(1704067200000, \"[FNn], [D1o] [MNn] [Y]\")", "null")]
    // AM/PM marker — exercises [P] component
    [InlineData("$fromMillis(1704067200000, \"[Y]-[M]-[D] [P]\")", "null")]
    // Timezone with colon — exercises timezone formatting
    [InlineData("$fromMillis(1704067200000, \"[Y]-[M]-[D]T[H]:[m]:[s][Z01:01]\")", "null")]
    public void FromToMillisPictures(string expression, string data, string? expected = null)
    {
        if (expected != null)
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
        else
        {
            this.AssertCgAndRtMatchCorvusExtension(expression, data);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard on arrays — exercises EnumerateWildcard array branch
    // Lines 1017-1037 in JsonataCodeGenHelpers.cs (21 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("[{\"a\":1},{\"b\":2}].*", "null", "[1,2]")]
    public void WildcardOnArrays(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // CoerceToStringElement — exercises non-string type coercion path
    // Lines 2087-2108 in JsonataCodeGenHelpers.cs (22 uncovered lines)
    // The CG path calls this when $string() is applied to non-strings
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$string(42)", "null", "\"42\"")]
    [InlineData("$string(true)", "null", "\"true\"")]
    [InlineData("$string(null)", "null", "\"null\"")]
    [InlineData("$string({\"a\":1})", "null", "\"{\\\"a\\\":1}\"")]
    [InlineData("$string([1,2])", "null", "\"[1,2]\"")]
    public void CoerceToString(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Deep chain navigation through nested arrays
    // Lines 2033-2109 in FunctionalCompiler.cs (77 uncovered lines)
    // Exercises EvalChainOverArrayIntoStatic — recursive array descent
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("store.departments.employees.name",
        "{\"store\":{\"departments\":[{\"employees\":[{\"name\":\"Alice\"},{\"name\":\"Bob\"}]},{\"employees\":[{\"name\":\"Carol\"}]}]}}",
        "[\"Alice\",\"Bob\",\"Carol\"]")]
    [InlineData("org.teams.members.skills",
        "{\"org\":{\"teams\":[{\"members\":[{\"skills\":[\"js\",\"py\"]},{\"skills\":[\"go\"]}]},{\"members\":[{\"skills\":[\"rust\"]}]}]}}",
        "[\"js\",\"py\",\"go\",\"rust\"]")]
    [InlineData("Account.Order.Product.Price",
        "{\"Account\":{\"Order\":[{\"Product\":[{\"Price\":10},{\"Price\":20}]},{\"Product\":[{\"Price\":30}]}]}}",
        "[10,20,30]")]
    public void DeepChainThroughNestedArrays(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $encodeUrlComponent with special chars — exercises encodeUrl string path
    // Lines 4315-4343 in BuiltInFunctions.cs (29 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$encodeUrlComponent(\"a=1&b=2\")", "null", "\"a%3D1%26b%3D2\"")]
    [InlineData("$encodeUrlComponent(\"hello world!\")", "null", "\"hello%20world%21\"")]
    public void EncodeUrlSpecialChars(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy annotation with duplicate keys
    // Lines 5016-5082 in FunctionalCompiler.cs (67 uncovered lines)
    // Exercises ApplySimpleNamePairGroupBy — singleton vs array merging
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "items{category: value}",
        "{\"items\":[{\"category\":\"fruit\",\"value\":\"apple\"},{\"category\":\"veg\",\"value\":\"carrot\"},{\"category\":\"fruit\",\"value\":\"banana\"}]}",
        "{\"fruit\":[\"apple\",\"banana\"],\"veg\":\"carrot\"}")]
    public void GroupByAnnotationDuplicateKeys(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Equality predicate filter + continue chain
    // Lines 1558-1640 in FunctionalCompiler.cs (83 uncovered lines)
    // Lines 765-791 in JsonataCodeGenHelpers.cs (27 uncovered lines)
    // Exercises CollectAndContinue / FusedFilterAndContinue
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "orders[status=\"shipped\"].items.name",
        "{\"orders\":[{\"status\":\"shipped\",\"items\":[{\"name\":\"Widget\"},{\"name\":\"Gadget\"}]},{\"status\":\"pending\",\"items\":[{\"name\":\"Thing\"}]}]}",
        "[\"Widget\",\"Gadget\"]")]
    [InlineData(
        "data[status=\"approved\"].value",
        "{\"data\":[{\"status\":\"approved\",\"value\":10},{\"status\":\"pending\",\"value\":20},{\"status\":\"approved\",\"value\":30}]}",
        "[10,30]")]
    public void EqualityPredicateFilterChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Index in property chain path
    // Lines 818-845 in JsonataCodeGenHelpers.cs (28 uncovered lines)
    // Exercises FusedEvalFromStep with constant index + array value
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "data[0].tags",
        "{\"data\":[{\"tags\":[\"a\",\"b\"]},{\"tags\":[\"c\"]}]}",
        "[\"a\",\"b\"]")]
    [InlineData(
        "data[-1].tags",
        "{\"data\":[{\"tags\":[\"a\",\"b\"]},{\"tags\":[\"c\"]}]}",
        "[\"c\"]")]
    public void IndexInPropertyChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort with comparator function
    // Lines 8091-8153 in FunctionalCompiler.cs (63 uncovered lines)
    // Exercises CompileFocusSortStage — sort with focus variable
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$sort(items, function($l,$r){$l.priority > $r.priority})",
        "{\"items\":[{\"name\":\"c\",\"priority\":3},{\"name\":\"a\",\"priority\":1},{\"name\":\"b\",\"priority\":2}]}",
        "[{\"name\":\"a\",\"priority\":1},{\"name\":\"b\",\"priority\":2},{\"name\":\"c\",\"priority\":3}]")]
    public void SortWithComparator(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $filter with 2-arg callback (value + index)
    // Lines 2234-2256 in BuiltInFunctions.cs (23 uncovered lines)
    // Exercises CompileFilterFunc — multi-valued sequence flattening with arrays
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$filter([1,2,3,4,5], function($v){$v > 3})",
        "null",
        "[4,5]")]
    public void FilterWithCallback(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $spread multi-element (object → array of single-key objects)
    // Lines 2644-2660 in BuiltInFunctions.cs (17 uncovered lines)
    // Exercises CompileSpread multi-element array path
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$spread({\"a\":1,\"b\":2})",
        "null",
        "[{\"a\":1},{\"b\":2}]")]
    public void SpreadObjectToArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $length / $substring with Unicode surrogate pairs (emoji)
    // Lines 7561-7591 in JsonataCodeGenHelpers.cs (31 uncovered lines)
    // Exercises CountCodePoints / CodePointToCharIndex
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$length(\"abc\\uD83D\\uDE00def\")", "null", "7")]
    [InlineData("$substring(\"abc\\uD83D\\uDE00def\", 4, 3)", "null", "\"def\"")]
    public void UnicodeSurrogateHandling(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $encodeUrl with string (non-UTF8 path)
    // Lines 4315-4343 in BuiltInFunctions.cs (29 uncovered lines)
    // Exercises the string coercion fallback in CompileEncodeUrl
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$encodeUrl(\"hello world/path\")", "null", "\"hello%20world/path\"")]
    public void EncodeUrlStringPath(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatInteger with ordinal modifier
    // Lines 572-598 in XPathDateTimeFormatter.cs (27 uncovered lines)
    // Exercises TryParseInteger ordinal format picture
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$formatInteger(1, \"1;o\")", "null", "\"1st\"")]
    [InlineData("$formatInteger(2, \"1;o\")", "null", "\"2nd\"")]
    [InlineData("$formatInteger(3, \"1;o\")", "null", "\"3rd\"")]
    [InlineData("$formatInteger(11, \"1;o\")", "null", "\"11th\"")]
    [InlineData("$formatInteger(12, \"1;o\")", "null", "\"12th\"")]
    [InlineData("$formatInteger(13, \"1;o\")", "null", "\"13th\"")]
    [InlineData("$formatInteger(21, \"1;o\")", "null", "\"21st\"")]
    [InlineData("$formatInteger(101, \"1;o\")", "null", "\"101st\"")]
    public void FormatIntegerOrdinal(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $parseInteger with sign prefix
    // Lines 2849-2860 in XPathDateTimeFormatter.cs (12 uncovered lines)
    // Exercises plus/minus sign parsing in ASCII integer
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$parseInteger(\"+42\", \"0\")", "null", "42")]
    [InlineData("$parseInteger(\"-99\", \"0\")", "null", "-99")]
    public void ParseIntegerWithSign(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $parseInteger with grouping separators
    // Lines 2712-2725 in XPathDateTimeFormatter.cs (14 uncovered lines)
    // Exercises single-byte grouping separator handling
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$parseInteger(\"1,234,567\", \"#,##0\")", "null", "1234567")]
    public void ParseIntegerGrouping(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $each with 3-parameter callback ($value, $key, $object)
    // Lines 9185-9215 in JsonataCodeGenHelpers.cs (31 uncovered lines)
    // NOTE: RT has a bug where 3-param $each returns [] — CG is correct
    // per reference jsonata. Test CG against expected, RT separately.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void EachThreeParamCallback()
    {
        // Reference jsonata returns ["object","object"]
        // CG returns ["object","object"] — correct
        // RT returns [] — RT bug (3-param $each not supported)
        string expression = "$each({\"x\":1,\"y\":2}, function($v,$k,$o){$type($o)})";
        string data = "null";
        string expectedJson = "[\"object\",\"object\"]";

        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using var expectedDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(expectedJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        string cgJson = cgResult.ValueKind == JsonValueKind.Undefined ? "undefined" : cgResult.GetRawText();
        this.output.WriteLine($"CG: {cgJson}");
        AssertJsonEqual(expectedDoc.RootElement, cgResult);
    }

    // ═══════════════════════════════════════════════════════════════
    // $map over chain property — exercises MapChainElements
    // Lines 2674-2716 in JsonataCodeGenHelpers.cs (43 uncovered lines)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$map(items, function($v){$v.nested.value})",
        "{\"items\":[{\"nested\":{\"value\":10}},{\"nested\":{\"value\":20}}]}",
        "[10,20]")]
    public void MapOverChainProperty(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Chain flattening through nested arrays
    // Lines 598-622 in JsonataCodeGenHelpers.cs (25 uncovered lines)
    // Exercises ContinueChainFlatInto — when chain encounters arrays mid-path
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "data.items.name",
        "{\"data\":[{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"c\"}]}]}",
        "[\"a\",\"b\",\"c\"]")]
    [InlineData(
        "data.items.values",
        "{\"data\":{\"items\":[{\"values\":[1,2]},{\"values\":[3,4]}]}}",
        "[1,2,3,4]")]
    public void ChainFlattenThroughArrays(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Deep multi-level chain through nested arrays
    // Targets ContinueChainFlatInto (lines 598-622) — array in middle of chain
    // Also targets CollectChainFlatInto (lines 560-591)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "orders.items.tags.name",
        "{\"orders\":{\"items\":[{\"tags\":[{\"name\":\"foo\"},{\"name\":\"bar\"}]},{\"tags\":[{\"name\":\"baz\"}]}]}}",
        "[\"foo\",\"bar\",\"baz\"]")]
    [InlineData(
        "data.nested.items.name",
        "{\"data\":{\"nested\":[{\"items\":[{\"name\":\"a\"}]},{\"items\":[{\"name\":\"b\"},{\"name\":\"c\"}]}]}}",
        "[\"a\",\"b\",\"c\"]")]
    [InlineData(
        "company.departments.teams.members.name",
        "{\"company\":{\"departments\":[{\"teams\":[{\"members\":[{\"name\":\"Alice\"}]},{\"members\":[{\"name\":\"Bob\"}]}]},{\"teams\":[{\"members\":[{\"name\":\"Carol\"}]}]}]}}",
        "[\"Alice\",\"Bob\",\"Carol\"]")]
    public void DeepMultiLevelChainFlatten(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Equality predicate filter in fused chain (CG path)
    // Targets FusedFilterAndContinue (lines 765-791)
    // Data must be array of objects with string predicate then continuation
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "data[type=\"order\"].items.name",
        "{\"data\":[{\"type\":\"order\",\"items\":[{\"name\":\"x\"},{\"name\":\"y\"}]},{\"type\":\"invoice\",\"items\":[{\"name\":\"z\"}]}]}",
        "[\"x\",\"y\"]")]
    public void FusedFilterContinueChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Index in middle of chain (per-element constant index)
    // Targets FusedEvalFromStep with constantIndices (lines 818-845)
    // The index must be on a non-first step of the chain
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "data.items[0].name",
        "{\"data\":{\"items\":[{\"name\":\"first\"},{\"name\":\"second\"}]}}",
        "\"first\"")]
    public void IndexInMiddleOfChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard on data reference (array of objects)
    // Targets EnumerateWildcard when source is data-bound array
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "data.*",
        "{\"data\":[{\"a\":1},{\"b\":2},{\"c\":3}]}",
        "[1,2,3]")]
    public void WildcardOnDataArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $spread on array of multi-key objects
    // Targets CompileSpread multi-element array (BI lines 2644-2660)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "$spread([{\"a\":1,\"b\":2},{\"c\":3}])",
        "null",
        "[{\"a\":1},{\"b\":2},{\"c\":3}]")]
    public void SpreadArrayOfMultiKeyObjects(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy annotation with actual duplicate keys
    // Targets ApplySimpleNamePairGroupBy duplicate branch (FC lines 5046-5068)
    // Key requirement: multiple items MUST share the same key value
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        "items{cat: val}",
        "{\"items\":[{\"cat\":\"A\",\"val\":1},{\"cat\":\"A\",\"val\":2},{\"cat\":\"B\",\"val\":3}]}",
        "{\"A\":[1,2],\"B\":3}")]
    public void GroupByDuplicateKeys(string expression, string data, string expected)
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

    /// <summary>
    /// For Corvus extensions (not in reference JSONata) or cases where
    /// the reference implementation differs by design: asserts CG and RT
    /// produce identical non-undefined results.
    /// </summary>
    private void AssertCgAndRtMatchCorvusExtension(string expression, string data)
    {
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

        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        string rtJson = rtResult ?? "undefined";

        this.output.WriteLine($"CG: {cgJson}");
        this.output.WriteLine($"RT: {rtJson}");

        Assert.NotEqual("undefined", cgJson);
        Assert.NotEqual("undefined", rtJson);

        using var cgDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(cgJson));
        using var rtDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(rtJson));
        AssertJsonEqual(rtDoc.RootElement, cgDoc.RootElement);
    }

    /// <summary>
    /// For Corvus extensions with known expected values (e.g., <c>??</c> operator
    /// whose behavior matches <c>$exists(lhs) ? lhs : rhs</c> verified against reference).
    /// </summary>
    private void AssertCgAndRtMatchCorvusExtension(string expression, string data, string expectedJson)
    {
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

    // ═══════════════════════════════════════════════════════════════════
    // $zip — multiple overloads (JsonataCodeGenHelpers lines 6528-6888)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Zip_SingleArg()
    {
        AssertCgAndRtMatch(
            """$zip([1,2,3])""",
            "null",
            """[[1],[2],[3]]""");
    }

    [Fact]
    public void Zip_TwoLiteralArrays()
    {
        AssertCgAndRtMatch(
            """$zip([1,2,3],[4,5,6])""",
            "null",
            """[[1,4],[2,5],[3,6]]""");
    }

    [Fact]
    public void Zip_ThreeArrays()
    {
        AssertCgAndRtMatch(
            """$zip([1,2],[3,4],[5,6])""",
            "null",
            """[[1,3,5],[2,4,6]]""");
    }

    [Fact]
    public void Zip_FourArrays_Variadic()
    {
        AssertCgAndRtMatch(
            """$zip([1,2],[3,4],[5,6],[7,8])""",
            "null",
            """[[1,3,5,7],[2,4,6,8]]""");
    }

    [Fact]
    public void Zip_MismatchedLengths()
    {
        // Shorter arrays truncate to shortest
        AssertCgAndRtMatch(
            """$zip([1,2,3],[4,5])""",
            "null",
            """[[1,4],[2,5]]""");
    }

    [Fact]
    public void Zip_TwoChains_FromBuffers()
    {
        AssertCgAndRtMatch(
            """$zip(data.prices, data.quantities)""",
            """{"data":{"prices":[10,20,30],"quantities":[2,3,4]}}""",
            """[[10,2],[20,3],[30,4]]""");
    }

    [Fact]
    public void Zip_ThreeChains_FromBuffers()
    {
        AssertCgAndRtMatch(
            """$zip(data.a, data.b, data.c)""",
            """{"data":{"a":[1,2],"b":[3,4],"c":[5,6]}}""",
            """[[1,3,5],[2,4,6]]""");
    }

    [Fact]
    public void Zip_ChainAndLiteral_Mixed()
    {
        // Chain first, literal second → ZipBufferAndElement
        AssertCgAndRtMatch(
            """$zip(data.items, [10,20,30])""",
            """{"data":{"items":[1,2,3]}}""",
            """[[1,10],[2,20],[3,30]]""");
    }

    [Fact]
    public void Zip_LiteralAndChain_Mixed()
    {
        // Literal first, chain second → ZipElementAndBuffer
        AssertCgAndRtMatch(
            """$zip([10,20,30], data.items)""",
            """{"data":{"items":[1,2,3]}}""",
            """[[10,1],[20,2],[30,3]]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ChainDistinct (JsonataCodeGenHelpers lines 4567-4601)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ChainDistinct_DuplicateStrings()
    {
        AssertCgAndRtMatch(
            """$distinct(items.category)""",
            """{"items":[{"category":"A"},{"category":"B"},{"category":"A"},{"category":"C"}]}""",
            """["A","B","C"]""");
    }

    [Fact]
    public void ChainDistinct_DuplicateNumbers()
    {
        AssertCgAndRtMatch(
            """$distinct(orders.id)""",
            """{"orders":[{"id":1},{"id":2},{"id":1},{"id":3}]}""",
            """[1,2,3]""");
    }

    [Fact]
    public void ChainDistinct_DeepChain()
    {
        AssertCgAndRtMatch(
            """$distinct(data.nested.value)""",
            """{"data":[{"nested":{"value":"X"}},{"nested":{"value":"Y"}},{"nested":{"value":"X"}}]}""",
            """["X","Y"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ChainKeepSingletonArray (JsonataCodeGenHelpers lines 4612-4646)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ChainKeepSingletonArray_MultipleItems()
    {
        AssertCgAndRtMatch(
            """items.prices[]""",
            """{"items":[{"prices":[1,2]},{"prices":[3]}]}""",
            """[1,2,3]""");
    }

    [Fact]
    public void ChainKeepSingletonArray_SingleValues()
    {
        AssertCgAndRtMatch(
            """items.value[]""",
            """{"items":[{"value":10},{"value":20}]}""",
            """[10,20]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ChainMerge (JsonataCodeGenHelpers lines 4657-4691)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ChainMerge_OverlappingKeys()
    {
        AssertCgAndRtMatch(
            """$merge(items.tags)""",
            """{"items":[{"tags":{"a":1,"b":2}},{"tags":{"b":3,"c":4}}]}""",
            """{"a":1,"b":3,"c":4}""");
    }

    [Fact]
    public void ChainMerge_NoOverlap()
    {
        AssertCgAndRtMatch(
            """$merge(data.config)""",
            """{"data":[{"config":{"debug":true}},{"config":{"verbose":true}}]}""",
            """{"debug":true,"verbose":true}""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // FusedChainGroupByPerElement (JsonataCodeGenHelpers lines 304-365)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FusedChainGroupByPerElement_Basic()
    {
        AssertCgAndRtMatch(
            """data.items.{category: value}""",
            """{"data":{"items":[{"category":"A","value":1},{"category":"B","value":2},{"category":"A","value":3}]}}""",
            """[{"A":1},{"B":2},{"A":3}]""");
    }

    [Fact]
    public void FusedChainGroupByPerElement_AllUnique()
    {
        AssertCgAndRtMatch(
            """data.items.{name: score}""",
            """{"data":{"items":[{"name":"x","score":10},{"name":"y","score":20}]}}""",
            """[{"x":10},{"y":20}]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatNumber via CG (exponent, percent, per-mille, grouping)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatNumber_Percent()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.1234, "#0.##%")""",
            "null",
            "\"12.34%\"");
    }

    [Fact]
    public void CG_FormatNumber_PerMille()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.1234, "#0.#‰")""",
            "null",
            "\"123.4‰\"");
    }

    [Fact]
    public void CG_FormatNumber_NegativeSubpicture()
    {
        AssertCgAndRtMatch(
            """$formatNumber(-1234.56, "#,##0.00;(#,##0.00)")""",
            "null",
            "\"(1,234.56)\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $fromMillis/$toMillis via CG with custom pictures
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FromMillis_CustomPicture()
    {
        AssertCgAndRtMatch(
            """$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]")""",
            "null",
            "\"2021-01-01\"");
    }

    [Fact]
    public void CG_FromMillis_WithTimezone()
    {
        AssertCgAndRtMatch(
            """$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01]", "+05:30")""",
            "null",
            "\"2021-01-01T05:30:00\"");
    }

    [Fact]
    public void CG_ToMillis_CustomPicture()
    {
        AssertCgAndRtMatch(
            """$toMillis("2021-01-01", "[Y0001]-[M01]-[D01]")""",
            "null",
            "1609459200000");
    }

    [Fact]
    public void CG_FromMillis_MonthName()
    {
        AssertCgAndRtMatch(
            """$fromMillis(1609459200000, "[MNn]")""",
            "null",
            "\"January\"");
    }

    [Fact]
    public void CG_FromMillis_DayOfWeek()
    {
        AssertCgAndRtMatch(
            """$fromMillis(1609459200000, "[FNn]")""",
            "null",
            "\"Friday\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatInteger via CG (word, ordinal, roman)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatInteger_Words()
    {
        AssertCgAndRtMatch(
            """$formatInteger(42, "w")""",
            "null",
            "\"forty-two\"");
    }

    [Fact]
    public void CG_FormatInteger_Ordinal()
    {
        AssertCgAndRtMatch(
            """$formatInteger(1, "w;o")""",
            "null",
            "\"first\"");
    }

    [Fact]
    public void CG_FormatInteger_RomanUpper()
    {
        AssertCgAndRtMatch(
            """$formatInteger(42, "I")""",
            "null",
            "\"XLII\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Focus sort via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FocusSort_ByVariable()
    {
        AssertCgAndRtMatch(
            """$@$e^($e.age)""",
            """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""",
            """[{"name":"A","age":10},{"name":"B","age":20},{"name":"C","age":30}]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $match via CG with capture groups
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Match_WithGroups()
    {
        AssertCgAndRtMatch(
            """$match("2026-04-19", /(\d{4})-(\d{2})-(\d{2})/)""",
            "null",
            """{"match":"2026-04-19","index":0,"groups":["2026","04","19"]}""");
    }

    [Fact]
    public void CG_Match_NoMatch()
    {
        AssertCgAndRtBothUndefined(
            """$match("hello", /xyz/)""",
            "null");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Coalesce operator via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Coalesce_MissingFallback()
    {
        AssertCgAndRtMatch(
            "missing ?? \"default\"",
            """{"existing":"value"}""",
            "\"default\"");
    }

    [Fact]
    public void CG_Coalesce_ExistingValue()
    {
        AssertCgAndRtMatch(
            "existing ?? \"default\"",
            """{"existing":"value"}""",
            "\"value\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $spread via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Spread_ArrayOfObjects()
    {
        AssertCgAndRtMatch(
            """$spread([{"a":1},{"b":2}])""",
            "null",
            """[{"a":1},{"b":2}]""");
    }

    [Fact]
    public void CG_Spread_SingleObjectMultipleKeys()
    {
        AssertCgAndRtMatch(
            """$spread({"a":1,"b":2})""",
            "null",
            """[{"a":1},{"b":2}]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $replace with function via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Replace_WithFunction()
    {
        AssertCgAndRtMatch(
            """$replace("hello world", /\w+/, function($m) { $uppercase($m.match) }, 1)""",
            "null",
            "\"HELLO world\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $string coercion of small exponent numbers
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_String_SmallExponent()
    {
        AssertCgAndRtMatch(
            """$string(1e-7)""",
            "null",
            "\"1e-7\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Deep path chain over nested arrays via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_DeepPathChain_NestedArrays()
    {
        AssertCgAndRtMatch(
            "data.items.tag",
            """{"data":[{"items":[{"tag":"a"},{"tag":"b"}]},{"items":[{"tag":"c"}]}]}""",
            """["a","b","c"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Equality predicate filtering via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_EqualityPredicate_FilterArray()
    {
        AssertCgAndRtMatch(
            """users[name="Alice"].email""",
            """{"users":[{"name":"Alice","email":"a@test.com"},{"name":"Bob","email":"b@test.com"}]}""",
            "\"a@test.com\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $number hex/binary/octal via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Number_Hex()
    {
        AssertCgAndRtMatch(
            """$number("0xFF")""",
            "null",
            "255");
    }

    [Fact]
    public void CG_Number_Binary()
    {
        AssertCgAndRtMatch(
            """$number("0b101")""",
            "null",
            "5");
    }

    [Fact]
    public void CG_Number_Octal()
    {
        AssertCgAndRtMatch(
            """$number("0o777")""",
            "null",
            "511");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 598-622: ContinueChainFlatInto
    // Property chain through nested arrays at multiple levels
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_DeepChain_ThreeLevelArray()
    {
        AssertCgAndRtMatch(
            "a.b.c.d",
            """{"a":[{"b":[{"c":[{"d":"x"},{"d":"y"}]},{"c":{"d":"z"}}]},{"b":{"c":{"d":"w"}}}]}""",
            """["x","y","z","w"]""");
    }

    [Fact]
    public void CG_NestedArrayChain_AccountOrder()
    {
        AssertCgAndRtMatch(
            "Account.Order.Product.Description.Colour",
            """{"Account":{"Order":[{"Product":[{"Description":{"Colour":"red"}},{"Description":{"Colour":"blue"}}]},{"Product":[{"Description":{"Colour":"green"}}]}]}}""",
            """["red","blue","green"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 765-791: FusedFilterAndContinue
    // Equality predicate filter then property continuation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FusedFilter_EqualityPredicate()
    {
        AssertCgAndRtMatch(
            """data[type="active"].name""",
            """{"data":[{"type":"active","name":"a"},{"type":"inactive","name":"b"},{"type":"active","name":"c"}]}""",
            """["a","c"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 818-845: FusedEvalFromStep with per-element index
    // Property chain with constant index at a step
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FusedEval_IndexInChain()
    {
        AssertCgAndRtMatch(
            "data.items[0].name",
            """{"data":{"items":[{"name":"first"},{"name":"second"}]}}""",
            "\"first\"");
    }

    [Fact]
    public void CG_FusedEval_LastIndex()
    {
        AssertCgAndRtMatch(
            "data.items[-1].name",
            """{"data":{"items":[{"name":"first"},{"name":"second"},{"name":"last"}]}}""",
            "\"last\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 693-712: FusedEvalFromStep equality predicate
    // Equality predicate in middle of chain
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FusedEval_EqualityInChain()
    {
        AssertCgAndRtMatch(
            """data.tags[value="important"].label""",
            """{"data":{"tags":[{"value":"important","label":"X"},{"value":"minor","label":"Y"},{"value":"important","label":"Z"}]}}""",
            """["X","Z"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 850-866: FusedEvalFromStep nested array filter
    // Equality predicate on nested array property
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FusedEval_NestedArrayFilter()
    {
        AssertCgAndRtMatch(
            """orders[status="shipped"].items.name""",
            """{"orders":[{"status":"shipped","items":[{"name":"Widget"},{"name":"Gadget"}]},{"status":"pending","items":[{"name":"Thing"}]}]}""",
            """["Widget","Gadget"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CGH lines 7561-7591: CountCodePoints
    // String functions on surrogate-pair strings via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Length_WithSurrogates()
    {
        AssertCgAndRtMatch(
            """$length("Hello\ud83d\ude0aWorld")""",
            "null",
            "11");
    }

    [Fact]
    public void CG_Substring_SurrogatePair()
    {
        AssertCgAndRtMatch(
            """$substring("Hello\ud83d\ude0aWorld", 5, 1)""",
            "null",
            "\"\ud83d\ude0a\"");
    }

    [Fact]
    public void CG_Substring_AfterSurrogate()
    {
        AssertCgAndRtMatch(
            """$substring("Hello\ud83d\ude0aWorld", 6)""",
            "null",
            "\"World\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // BIF lines 5047-5072 via CG: FormatNumber exponent
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatNumber_Exponent()
    {
        AssertCgAndRtMatch(
            """$formatNumber(12345, "0.00e0")""",
            "null",
            "\"1.23e4\"");
    }

    [Fact]
    public void CG_FormatNumber_SmallExponent()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.00123, "0.00e0")""",
            "null",
            "\"1.23e-3\"");
    }

    [Fact]
    public void CG_FormatNumber_ExpZero()
    {
        AssertCgAndRtMatch(
            """$formatNumber(1, "0.00e0")""",
            "null",
            "\"1.00e0\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // BIF lines 4811-4853 via CG: FormatNumber grouping
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatNumber_Grouping()
    {
        AssertCgAndRtMatch(
            """$formatNumber(1234567, "#,##0")""",
            "null",
            "\"1,234,567\"");
    }

    [Fact]
    public void CG_FormatNumber_GroupingWithDecimals()
    {
        AssertCgAndRtMatch(
            """$formatNumber(1234567.89, "#,##0.00")""",
            "null",
            "\"1,234,567.89\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // BIF lines 4315-4343 via CG: $encodeUrl non-string (Corvus extension)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_EncodeUrl_NumberInput()
    {
        // Corvus extension: coerces non-string to string before URL encoding
        AssertCgAndRtMatchCorvusExtension(
            "$encodeUrl(42)",
            "null");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Filter with array of numeric indices via CG
    // FC lines 5641-5739
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Filter_ArrayOfIndices()
    {
        AssertCgAndRtMatch(
            "data[[0,2,4]]",
            """{"data":["a","b","c","d","e"]}""",
            """["a","c","e"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // GroupBy with duplicate keys via CG
    // FC lines 5016-5082: ApplySimpleNamePairGroupBy
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_GroupBy_DuplicateKeys()
    {
        AssertCgAndRtMatch(
            "items{cat: val}",
            """{"items":[{"cat":"A","val":10},{"cat":"B","val":20},{"cat":"A","val":30}]}""",
            """{"A":[10,30],"B":20}""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Array constructor with multi-valued items via CG
    // FC lines 6866-6901: Tuple array constructor
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_ArrayConstructor_MultiValuedItems()
    {
        AssertCgAndRtMatch(
            """[data.items.name, "extra"]""",
            """{"data":{"items":[{"name":"x"},{"name":"y"}]}}""",
            """["x","y","extra"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $each with 3 parameters via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Each_ThreeParam()
    {
        AssertCgAndRtMatch(
            """$each({"a":1,"b":2}, function($v,$k,$o){$k & "=" & $string($v)})""",
            "null",
            """["a=1","b=2"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $spread with multi-key objects via CG
    // BIF lines 2644-2660: CompileSpread property counting
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Spread_MultiKeyObject()
    {
        AssertCgAndRtMatch(
            """$spread({"a":1,"b":2,"c":3})""",
            "null",
            """[{"a":1},{"b":2},{"c":3}]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sort then continue chain via CG
    // FC lines 3155-3180 and 5559-5594
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_Sort_ThenPropertyAccess()
    {
        AssertCgAndRtMatch(
            "items^(price).name",
            """{"items":[{"price":30,"name":"c"},{"price":10,"name":"a"},{"price":20,"name":"b"}]}""",
            """["a","b","c"]""");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatNumber percent via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatNumber_PercentPicture()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.45, "##0%")""",
            "null",
            "\"45%\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatInteger via CG
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CG_FormatInteger_Words_Large()
    {
        AssertCgAndRtMatch(
            """$formatInteger(1001, "w")""",
            "null",
            "\"one thousand and one\"");
    }

    [Fact]
    public void CG_FormatInteger_Grouping()
    {
        AssertCgAndRtMatch(
            """$formatInteger(1234567, "#,##0")""",
            "null",
            "\"1,234,567\"");
    }

    // ─── JsonataCodeGenHelpers: Zip variadic (lines 6654-6705) ────────
    // $zip with 4+ args triggers the variadic overload

    [Fact]
    public void CG_Zip_FourArgs()
    {
        AssertCgAndRtMatch(
            """$zip([1,2],[3,4],[5,6],[7,8])""",
            "null",
            "[[1,3,5,7],[2,4,6,8]]");
    }

    [Fact]
    public void CG_Zip_FiveArgs()
    {
        AssertCgAndRtMatch(
            """$zip([1,2],[3,4],[5,6],[7,8],[9,10])""",
            "null",
            "[[1,3,5,7,9],[2,4,6,8,10]]");
    }

    // ─── JsonataCodeGenHelpers: Zip single arg (lines 6529-6556) ─────

    [Fact]
    public void CG_Zip_SingleArg()
    {
        AssertCgAndRtMatch(
            """$zip([1,2,3])""",
            "null",
            "[[1],[2],[3]]");
    }

    // ─── JsonataCodeGenHelpers: ContinueChainFlatInto (lines 598-622) ──

    [Fact]
    public void CG_Chain_NestedArraysAtIntermediate()
    {
        AssertCgAndRtMatch(
            "data.items.name",
            """{"data":[{"items":[{"name":"a"},{"name":"b"}]},{"items":[{"name":"c"}]}]}""",
            """["a","b","c"]""");
    }

    [Fact]
    public void CG_Chain_ThreeLevelArray()
    {
        AssertCgAndRtMatch(
            "Data.matrix.values",
            """{"Data":{"matrix":[{"values":[10,20,30]},{"values":[40,50]}]}}""",
            "[10,20,30,40,50]");
    }

    // ─── JsonataCodeGenHelpers: FusedFilterAndContinue (lines 765-791) ──

    [Fact]
    public void CG_FusedFilter_EqualityWithContinuation()
    {
        AssertCgAndRtMatch(
            """data[type="order"].items.name""",
            """{"data":[{"type":"order","items":[{"name":"x"},{"name":"y"}]},{"type":"invoice","items":[{"name":"z"}]}]}""",
            """["x","y"]""");
    }

    // ─── JsonataCodeGenHelpers: FusedCollectAndContinue index (lines 818-845) ──

    [Fact]
    public void CG_FusedIndex_WithContinuation()
    {
        AssertCgAndRtMatch(
            "Data.records[0].items[1].value",
            """{"Data":{"records":[{"items":[{"value":"a"},{"value":"b"},{"value":"c"}]},{"items":[{"value":"x"},{"value":"y"}]}]}}""",
            "\"b\"");
    }

    [Fact]
    public void CG_FusedIndex_OutOfRange()
    {
        AssertCgAndRtBothUndefined(
            "data.items[5].name",
            """{"data":{"items":[{"name":"A"},{"name":"B"}]}}""");
    }

    // ─── JsonataCodeGenHelpers: EnumerateWildcard array (lines 1017-1037) ──

    [Fact]
    public void CG_Wildcard_OnObject()
    {
        AssertCgAndRtMatch(
            """{"a":1,"b":2,"c":3}.*""",
            "null",
            "[1,2,3]");
    }

    [Fact]
    public void CG_Wildcard_ChainedOnObject()
    {
        AssertCgAndRtMatch(
            "data.*.name",
            """{"data":{"a":{"name":"Alice"},"b":{"name":"Bob"},"c":{"name":"Charlie"}}}""",
            """["Alice","Bob","Charlie"]""");
    }

    // ─── JsonataCodeGenHelpers: CountCodePoints (lines 7561-7591) ──

    [Fact]
    public void CG_Length_Emoji()
    {
        // Emoji is a surrogate pair, counts as 1 code point
        AssertCgAndRtMatch(
            """$length("abc\uD83D\uDE00def")""",
            "null",
            "7");
    }

    [Fact]
    public void CG_Substring_SurrogatePairIndexing()
    {
        // $substring at code-point index 4, length 3 = "def" (skipping emoji)
        AssertCgAndRtMatch(
            """$substring("abc\uD83D\uDE00def", 4, 3)""",
            "null",
            "\"def\"");
    }

    // ─── JsonataCodeGenHelpers: CoerceToStringElement (lines 2087-2108) ──

    [Fact]
    public void CG_String_CoerceNumber()
    {
        AssertCgAndRtMatch(
            """$string(42)""",
            "null",
            "\"42\"");
    }

    [Fact]
    public void CG_String_CoerceBoolean()
    {
        AssertCgAndRtMatch(
            """$string(true)""",
            "null",
            "\"true\"");
    }

    // ─── JsonataCodeGenHelpers: BinaryArithmetic (lines 8799-8822) ──

    [Fact]
    public void CG_Arithmetic_Add()
    {
        AssertCgAndRtMatch(
            "a + b",
            """{"a":10,"b":3}""",
            "13");
    }

    [Fact]
    public void CG_Arithmetic_Multiply()
    {
        AssertCgAndRtMatch(
            "a * b",
            """{"a":7,"b":6}""",
            "42");
    }

    // ─── JsonataCodeGenHelpers: MapChainElements (lines 2674-2716) ──

    [Fact]
    public void CG_Map_WithIndex()
    {
        AssertCgAndRtMatch(
            """$map([10,20,30], function($v,$i){$v * $i})""",
            "null",
            "[0,20,60]");
    }

    // ─── CollectAndContinue: equality predicate singleton (FC 1619-1638 via CGH) ──

    [Fact]
    public void CG_EqualityPredicate_SingletonObject()
    {
        AssertCgAndRtMatch(
            """account.orders[type="premium"].items.name""",
            """{"account":{"orders":{"type":"premium","items":[{"name":"Widget"},{"name":"Gadget"}]}}}""",
            """["Widget","Gadget"]""");
    }

    // ─── GroupBy annotation (FC 5016-5082 via CGH) ──

    [Fact]
    public void CG_GroupBy_Annotation_DuplicateKeys()
    {
        AssertCgAndRtMatch(
            "Account.Order.Product{Name: Price}",
            """{"Account":{"Order":[{"Product":{"Name":"A","Price":10}},{"Product":{"Name":"B","Price":20}},{"Product":{"Name":"A","Price":30}}]}}""",
            """{"A":[10,30],"B":20}""");
    }

    // ─── Sort then chain access (FC 3155-3180 via CGH) ──

    [Fact]
    public void CG_Sort_ThenChainAccess()
    {
        AssertCgAndRtMatch(
            "Account.Order^(Product.Price).Product.Name",
            """{"Account":{"Order":[{"Product":{"Price":30,"Name":"C"}},{"Product":{"Price":10,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}}]}}""",
            """["A","B","C"]""");
    }

    // ─── Transform operator ──

    [Fact]
    public void CG_Transform_AddProperty()
    {
        AssertCgAndRtMatch(
            """$ ~> |Account.Order|{"Discount":10}|""",
            """{"Account":{"Order":[{"Product":"A"},{"Product":"B"}]}}""",
            """{"Account":{"Order":[{"Product":"A","Discount":10},{"Product":"B","Discount":10}]}}""");
    }

    // ─── $sift (BuiltInFunctions) ──

    [Fact]
    public void CG_Sift_FilterProperties()
    {
        AssertCgAndRtMatch(
            """$sift({"a":1,"b":2,"c":3}, function($v){$v > 1})""",
            "null",
            """{"b":2,"c":3}""");
    }

    // ─── $reduce with initial value ──

    [Fact]
    public void CG_Reduce_WithInit()
    {
        AssertCgAndRtMatch(
            """$reduce([1,2,3,4], function($prev,$curr){$prev + $curr}, 10)""",
            "null",
            "20");
    }

    // ─── $sort ──

    [Fact]
    public void CG_Sort_Default()
    {
        AssertCgAndRtMatch(
            """$sort(["banana","apple","cherry"])""",
            "null",
            """["apple","banana","cherry"]""");
    }

    // ─── $formatNumber exponent (BIF 5062-5087) ──

    [Fact]
    public void CG_FormatNumber_Scientific()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.000123, "0.00e0")""",
            "null",
            "\"1.23e-4\"");
    }

    // ─── $formatBase ──

    [Theory]
    [InlineData(255, 16, "ff")]
    [InlineData(255, 2, "11111111")]
    [InlineData(255, 8, "377")]
    public void CG_FormatBase_Various(int value, int radix, string expected)
    {
        AssertCgAndRtMatch(
            $"$formatBase({value}, {radix})",
            "null",
            $"\"{expected}\"");
    }

    // ─── Descendant operator ──

    [Fact]
    public void CG_Descendant_Search()
    {
        AssertCgAndRtMatch(
            "**.name",
            """{"data":{"a":{"name":"Alice"},"b":{"inner":{"name":"Bob"}}}}""",
            """["Alice","Bob"]""");
    }

    // ─── $formatInteger Unicode digits (XPath 1298-1312, 1619-1637) ──

    [Theory]
    [InlineData(2025, "\u0661", "\u0662\u0660\u0662\u0665")]
    [InlineData(99, "\u0967", "\u096F\u096F")]
    public void CG_FormatInteger_UnicodeDigits(int value, string presentation, string expected)
    {
        AssertCgAndRtMatch(
            $"$formatInteger({value}, \"{presentation}\")",
            "null",
            $"\"{expected}\"");
    }

    // ─── Closure / higher-order functions ──

    [Fact]
    public void CG_Lambda_Closure()
    {
        AssertCgAndRtMatch(
            """( $add := function($x){function($y){$x + $y}}; $add(3)(4) )""",
            "null",
            "7");
    }

    // ─── $string on array (verifies bug #2 fix via CG path) ──

    [Fact]
    public void CG_String_Array()
    {
        AssertCgAndRtMatch(
            """$string([1,2,3])""",
            "null",
            "\"[1,2,3]\"");
    }

    // ─── $filter on multi-valued path ──

    [Fact]
    public void CG_Filter_MultiValuedPath()
    {
        AssertCgAndRtMatch(
            """$filter(Account.Order.Product, function($v){$v.Price > 30})""",
            """{"Account":{"Order":[{"Product":{"Price":35,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}},{"Product":{"Price":50,"Name":"C"}}]}}""",
            """[{"Price":35,"Name":"A"},{"Price":50,"Name":"C"}]""");
    }

    // ─── $count/$sum/$average/$min/$max on multi-value paths ──

    [Fact]
    public void CG_Count_MultiValued()
    {
        AssertCgAndRtMatch(
            "$count(data.items.name)",
            """{"data":{"items":[{"name":"A"},{"name":"B"},{"name":"C"}]}}""",
            "3");
    }

    [Fact]
    public void CG_Sum_MultiValued()
    {
        AssertCgAndRtMatch(
            "$sum(data.items.price)",
            """{"data":{"items":[{"price":10},{"price":20},{"price":30}]}}""",
            "60");
    }

    [Fact]
    public void CG_Average_MultiValued()
    {
        AssertCgAndRtMatch(
            "$average(data.items.price)",
            """{"data":{"items":[{"price":10},{"price":20},{"price":30}]}}""",
            "20");
    }

    // ─── Coalesce (??) — Corvus extension ──

    [Fact]
    public void CG_Coalesce_SimpleChain_Found()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.value ?? "default" """,
            """{"data":{"value":42}}""",
            "42");
    }

    [Fact]
    public void CG_Coalesce_SimpleChain_Fallback()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.value ?? "default" """,
            """{"data":{}}""",
            "\"default\"");
    }

    [Fact]
    public void CG_Coalesce_DeepChain_Found()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.x.y ?? "fallback" """,
            """{"data":{"x":{"y":"found"}}}""",
            "\"found\"");
    }

    [Fact]
    public void CG_Coalesce_DeepChain_Fallback()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.x.y ?? "fallback" """,
            """{"data":{"x":{}}}""",
            "\"fallback\"");
    }

    [Fact]
    public void CG_Coalesce_ArrayMidChain()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.items.name ?? []""",
            """{"data":{"items":[{"name":"A"},{"name":"B"},{"name":"C"}]}}""",
            """["A","B","C"]""");
    }

    [Fact]
    public void CG_Coalesce_ArrayMidChain_Fallback()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.items.name ?? []""",
            """{"data":{}}""",
            "[]");
    }

    [Fact]
    public void CG_Coalesce_NonObject_Fallback()
    {
        AssertCgAndRtMatchCorvusExtension(
            """data.value.nested ?? 0""",
            """{"data":{"value":42}}""",
            "0");
    }

    // ─── $formatNumber patterns ──

    [Fact]
    public void CG_FormatNumber_ScientificLarge()
    {
        AssertCgAndRtMatch(
            """$formatNumber(12345, "0.00e0")""",
            "{}",
            "\"1.23e4\"");
    }

    [Fact]
    public void CG_FormatNumber_ScientificSmall()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.05, "0.00e0")""",
            "{}",
            "\"5.00e-2\"");
    }

    [Fact]
    public void CG_FormatNumber_PerMille_Small()
    {
        AssertCgAndRtMatch(
            """$formatNumber(0.025, "##0\u2030")""",
            "{}",
            "\"25\u2030\"");
    }

    [Fact]
    public void CG_FormatNumber_GroupingSeparator_Large()
    {
        AssertCgAndRtMatch(
            """$formatNumber(1234567, "#,##0")""",
            "{}",
            "\"1,234,567\"");
    }

    [Fact]
    public void CG_FormatNumber_NegativeSubPicture()
    {
        AssertCgAndRtMatch(
            """$formatNumber(-42, "000;(000)")""",
            "{}",
            "\"(042)\"");
    }

    // ─── $formatInteger / $parseInteger word output ──

    [Fact]
    public void CG_FormatInteger_WordLower()
    {
        AssertCgAndRtMatch(
            """$formatInteger(42, "w")""",
            "{}",
            "\"forty-two\"");
    }

    [Fact]
    public void CG_ParseInteger_Word()
    {
        AssertCgAndRtMatch(
            """$parseInteger("forty-two", "w")""",
            "{}",
            "42");
    }
}
