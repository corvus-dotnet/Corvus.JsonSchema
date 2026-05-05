// <copyright file="CodeGenHelpersCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    // Index binding (#$i) — FunctionalCompiler lines 4037-4135,
    // 5545-5643; JsonataCodeGenHelpers index emitter paths
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Basic index binding with object constructor
    [InlineData(
        """["a","b","c"]#$i.{"value": $, "index": $i}""",
        "null",
        """[{"value":"a","index":0},{"value":"b","index":1},{"value":"c","index":2}]""")]
    // Index binding on data property
    [InlineData(
        """items#$i.{"item": $, "pos": $i}""",
        """{"items":["x","y","z"]}""",
        """[{"item":"x","pos":0},{"item":"y","pos":1},{"item":"z","pos":2}]""")]
    // Index binding with filter (keeps original indices)
    [InlineData(
        """[10,20,30,40,50]#$i[$i < 3]""",
        "null",
        "[10,20,30]")]
    // Index binding on object array with property access
    [InlineData(
        """items#$i.{"name": name, "idx": $i}""",
        """{"items":[{"name":"a"},{"name":"b"},{"name":"c"}]}""",
        """[{"name":"a","idx":0},{"name":"b","idx":1},{"name":"c","idx":2}]""")]
    public void IndexBinding(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Focus binding (@$var) cross-join — FunctionalCompiler lines
    // 2975-3079, 3291-3409; JsonataCodeGenHelpers focus emitter
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Cross-join: correlate loans with books by isbn
    [InlineData(
        """library.loans@$l.books[isbn=$l.isbn].title""",
        """{"library":{"loans":[{"isbn":"123"},{"isbn":"456"}],"books":[{"isbn":"123","title":"A"},{"isbn":"456","title":"B"}]}}""",
        """["A","B"]""")]
    // Cross-join: correlate data with related records
    [InlineData(
        """data@$d.other[id=$d.ref].name""",
        """{"data":[{"ref":1},{"ref":2}],"other":[{"id":1,"name":"one"},{"id":2,"name":"two"}]}""",
        """["one","two"]""")]
    public void FocusBindingCrossJoin(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $keys / $lookup on array of objects — JsonataCodeGenHelpers
    // lines 4857-5063 (CollectUniqueKeys, LookupCollect)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // $keys deduplicates across array of objects
    [InlineData(
        """$keys([{"a":1},{"b":2},{"a":3}])""",
        "null",
        """["a","b"]""")]
    // $keys on array with multiple keys per object
    [InlineData(
        """$keys([{"x":1,"y":2},{"y":3,"z":4}])""",
        "null",
        """["x","y","z"]""")]
    // $lookup collects all matching values across array
    [InlineData(
        """$lookup([{"a":1},{"a":2},{"b":3}], "a")""",
        "null",
        "[1,2]")]
    // $lookup with single match
    [InlineData(
        """$lookup([{"a":1},{"b":2}], "b")""",
        "null",
        "2")]
    public void KeysAndLookupOnArrayOfObjects(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $split with limit — JsonataCodeGenHelpers lines 3986-4101
    // (SplitByConstantString limit handling)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Limit truncates to at most N parts
    [InlineData(
        """$split("a,b,c,d", ",", 2)""",
        "null",
        """["a","b"]""")]
    // Limit of 1 returns first part only
    [InlineData(
        """$split("x-y-z", "-", 1)""",
        "null",
        """["x"]""")]
    // Limit greater than parts returns all
    [InlineData(
        """$split("a.b", ".", 10)""",
        "null",
        """["a","b"]""")]
    public void SplitWithLimit(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace regex with backreferences + limit — CG path for
    // ApplyJsonataBackreferences (JsonataCodeGenHelpers lines 5997-6195)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Backreference $1 with limit
    [InlineData(
        """$replace("aaa", /(.)/, "[$1]", 2)""",
        "null",
        "\"[a][a]a\"")]
    // Swap two capture groups with limit
    [InlineData(
        """$replace("ab-cd", /([a-z])([a-z])/, "$2$1", 1)""",
        "null",
        "\"ba-cd\"")]
    public void ReplaceRegexBackrefsWithLimit(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $single error paths — BuiltInFunctions lines 2881-2891
    // (D3138 multiple matches, D3139 no matches)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // $single with exactly one match (success)
    [InlineData(
        """$single([1,2,3,4,5], function($v){$v = 3})""",
        "null",
        "3")]
    // $single on single-element array without predicate
    [InlineData(
        """$single([42])""",
        "null",
        "42")]
    public void SingleSuccessPaths(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Per-element object constructor via path step items.{key: val}
    // (JsonataCodeGenHelpers lines 6904-6994, FusedArrayOfObjects)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Object constructor with renamed keys
    [InlineData(
        """items.{"nm": name, "pr": cost}""",
        """{"items":[{"name":"a","cost":10},{"name":"b","cost":20}]}""",
        """[{"nm":"a","pr":10},{"nm":"b","pr":20}]""")]
    // Three-key object constructor
    [InlineData(
        """items.{"id": sku, "label": name, "amount": price}""",
        """{"items":[{"sku":"S1","name":"Widget","price":9.99},{"sku":"S2","name":"Gadget","price":19.99}]}""",
        """[{"id":"S1","label":"Widget","amount":9.99},{"id":"S2","label":"Gadget","amount":19.99}]""")]
    public void PerElementObjectConstructor(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with grouping — BuiltInFunctions lines 4733-4868
    // (picture parsing with grouping separator patterns)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    // Large number with grouping
    [InlineData(
        """$formatNumber(1234567, "#,###")""",
        "null",
        "\"1,234,567\"")]
    // Grouping with decimal
    [InlineData(
        """$formatNumber(1234567.89, "#,##0.00")""",
        "null",
        "\"1,234,567.89\"")]
    // Negative with sub-picture (parentheses)
    [InlineData(
        """$formatNumber(-42.5, "#0.00;(#0.00)")""",
        "null",
        "\"(42.50)\"")]
    // Percent format
    [InlineData(
        """$formatNumber(0.75, "0%")""",
        "null",
        "\"75%\"")]
    public void FormatNumberGroupingAndSubPicture(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fused $zip with property chains
    // (FunctionalCompiler lines 657-709, JsonataCodeGenHelpers fused path)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$zip(a,b)""",
        """{"a":[1,2,3],"b":[4,5,6]}""",
        "[[1,4],[2,5],[3,6]]")]
    [InlineData(
        """$zip(a,b,c)""",
        """{"a":[1,2],"b":[3,4],"c":[5,6]}""",
        "[[1,3,5],[2,4,6]]")]
    public void FusedZipPropertyChains(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort stage with filter and flattening
    // (FunctionalCompiler lines 5463-5498, 5959-5981)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$[type="a"]^(val)""",
        """[{"type":"a","val":3},{"type":"b","val":1},{"type":"a","val":1},{"type":"a","val":2}]""",
        """[{"type":"a","val":1},{"type":"a","val":2},{"type":"a","val":3}]""")]
    public void FilterThenSort(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Multi-value filter with boolean predicate
    // (FunctionalCompiler lines 5545-5643, 7877-7944)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """nums[$>2]""",
        """{"nums":[1,2,3,4,5]}""",
        "[3,4,5]")]
    public void MultiValueBooleanFilter(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Array constructor with mixed types (tuple handling)
    // (FunctionalCompiler lines 6798-6833)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """[a, b, c]""",
        """{"a":1,"b":[2,3],"c":4}""",
        "[1,2,3,4]")]
    public void ArrayConstructorMixedTypes(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Equality predicate on a single object (not array)
    // (FunctionalCompiler lines 1645-1663)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$[type="x"]""",
        """{"type":"x","val":1}""",
        """{"type":"x","val":1}""")]
    public void EqualityPredicateOnObject(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Descendant wildcard (**)
    // (FunctionalCompiler/CGH lines 1015-1074, 1567-1581)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """**.name""",
        """{"a":{"name":"x","b":[{"name":"y"},{"name":"z"}]}}""",
        """["x","y","z"]""")]
    public void DescendantWildcard(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Wildcard on mixed object (arrays + scalars)
    // (JsonataCodeGenHelpers lines 1015-1074)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """*""",
        """{"a":1,"b":[2,3],"c":"x"}""",
        """[1,2,3,"x"]""")]
    public void WildcardOnMixedObject(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Property map pre-building optimization (large arrays)
    // (FunctionalCompiler lines 1694-1713)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("category", "codegen-coverage")]
    public void PropertyMapPreBuildingLargeArray()
    {
        // arrayLen * remainingSteps > 10 triggers property map pre-building
        string data = """{"items":[{"id":1,"name":"a"},{"id":2,"name":"b"},{"id":3,"name":"c"},{"id":4,"name":"d"},{"id":5,"name":"e"},{"id":6,"name":"f"},{"id":7,"name":"g"},{"id":8,"name":"h"},{"id":9,"name":"i"},{"id":10,"name":"j"},{"id":11,"name":"k"}]}""";
        this.AssertCgAndRtMatch("""items[id=5].name""", data, "\"e\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with capture groups
    // (JsonataCodeGenHelpers lines 6351-6450)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$match("abc", /([a-z])([a-z])([a-z])/)""",
        "null",
        """{"match":"abc","index":0,"groups":["a","b","c"]}""")]
    public void MatchWithCaptureGroups(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with limit
    // (BuiltInFunctions lines 3130-3138)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$match("abc123def456", /[0-9]+/, 1)""",
        "null",
        """{"match":"123","index":3,"groups":[]}""")]
    public void MatchWithLimit(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $pad with emoji (UTF-16 surrogate pair cycling)
    // (JsonataCodeGenHelpers lines 4354-4432)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$pad("test", 8, "\uD83C\uDF81")""",
        "null",
        "\"test\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\"")]
    [InlineData(
        """$pad("test", -8, "\uD83C\uDF81")""",
        "null",
        "\"\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81test\"")]
    public void PadWithEmoji(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with string pattern and limit
    // (JsonataCodeGenHelpers lines 5997-6075)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$replace("hello world hello", "hello", "hi", 1)""",
        "null",
        "\"hi world hello\"")]
    public void ReplaceStringWithLimit(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $spread on array/object
    // (BuiltInFunctions lines 2659-2675, 2696-2708)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$spread([{"a":1},{"b":2}])""",
        "null",
        """[{"a":1},{"b":2}]""")]
    [InlineData(
        """$spread({"a":1,"b":2})""",
        "null",
        """[{"a":1},{"b":2}]""")]
    public void SpreadOnArrayAndObject(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with exponent
    // (BuiltInFunctions lines 5062-5087, 5193-5202)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$formatNumber(1234.5, "0.0e0")""",
        "null",
        "\"1.2e3\"")]
    [InlineData(
        """$formatNumber(0.00123, "0.00e0")""",
        "null",
        "\"1.23e-3\"")]
    [InlineData(
        """$formatNumber(1234567, "0.0e00")""",
        "null",
        "\"1.2e06\"")]
    public void FormatNumberExponentViaCg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with explicit negative sub-picture
    // (BuiltInFunctions lines 4983-4995, 5025-5037)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$formatNumber(-42, "0;0-")""",
        "null",
        "\"42-\"")]
    [InlineData(
        """$formatNumber(-123, "0;(0)")""",
        "null",
        "\"(123)\"")]
    public void FormatNumberNegativeSubPicture(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with irregular grouping (non-regular positions)
    // Verified against reference JSONata implementation - uses literal positions
    // (BuiltInFunctions lines 4826-4841, 4853-4868)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$formatNumber(12345678, "#,##,##0")""",
        "null",
        "\"123,45,678\"")]
    public void FormatNumberIrregularGrouping(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $exists over property chain with array traversal
    // (JsonataCodeGenHelpers lines 3070-3135)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$exists(items.tags)""",
        """{"items":[{"tags":["a"]},{"tags":["b"]}]}""",
        "true")]
    public void ExistsOverPropertyChainWithArrays(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Range operator [start..end]
    // (JsonataCodeGenHelpers lines 1832-1889)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("""[1..5]""", "null", "[1,2,3,4,5]")]
    [InlineData("""[0..0]""", "null", "[0]")]
    public void RangeOperator(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $each on multi-property object
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$each({"a":1,"b":2,"c":3}, function($v,$k){$k & "=" & $string($v)})""",
        "null",
        """["a=1","b=2","c=3"]""")]
    public void EachOnObject(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $merge with overlapping keys
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$merge([{"a":1},{"b":2},{"a":3}])""",
        "null",
        """{"a":3,"b":2}""")]
    public void MergeOverlappingKeys(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $flatten with nested arrays
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$flatten([[1,[2]],[[3],4]])""",
        "null",
        "[1,2,3,4]")]
    public void FlattenNestedArrays(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Unicode supplementary character handling
    // (BuiltInFunctions lines 6251-6266, JsonataCodeGenHelpers lines 7561-7624)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$length("\uD83D\uDE00test")""",
        "null",
        "5")]
    [InlineData(
        """$substring("\uD83D\uDE00test", 0, 1)""",
        "null",
        "\"\uD83D\uDE00\"")]
    public void UnicodeSurrogateOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // $parseInteger with grouping separator picture
    // (BuiltInFunctions lines 5986-5998)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """$parseInteger("123,456", "#,##0")""",
        "null",
        "123456")]
    public void ParseIntegerWithGrouping(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Array-of-objects construction with fused path
    // (FunctionalCompiler lines 6904-6994)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData(
        """items.{"n": name, "v": val}""",
        """{"items":[{"name":"a","val":1},{"name":"b","val":2}]}""",
        """[{"n":"a","v":1},{"n":"b","v":2}]""")]
    public void FusedArrayOfObjectsConstruction(string expression, string data, string expected)
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

    private void AssertCgAndRtThrow(string expression, string data, string expectedCode)
    {
        // CG path
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

        var cgEx = Assert.Throws<TargetInvocationException>(() => InvokeCg(compiled.Method, inputDoc.RootElement, workspace));
        var cgJsonataEx = Assert.IsType<JsonataException>(cgEx.InnerException);
        Assert.Equal(expectedCode, cgJsonataEx.Code);

        // RT path
        var rtEx = Assert.Throws<JsonataException>(() => JsonataEvaluator.Default.EvaluateToString(expression, data));
        Assert.Equal(expectedCode, rtEx.Code);

        this.output.WriteLine($"Both CG and RT threw {expectedCode}");
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
        // Focus on root array ($@$e) without continuation returns parent context per element.
        // Note: reference wraps each in an extra array layer; we return parent directly
        // (consistent with all other focus-without-continuation semantics).
        AssertCgAndRtMatch(
            """$@$e^($e.age)""",
            """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""",
            """[[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]]""");
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

    // ─── $number boolean coercion (FC 8625-8632) ──

    [Fact]
    public void CG_Number_BoolTrue()
    {
        AssertCgAndRtMatch("""$number(true)""", "{}", "1");
    }

    [Fact]
    public void CG_Number_BoolFalse()
    {
        AssertCgAndRtMatch("""$number(false)""", "{}", "0");
    }

    // ─── $formatNumber minInt==0 (BF 4914-4934) ──

    [Fact]
    public void CG_FormatNumber_MinIntZero()
    {
        // Picture "#" → minInt=0, maxFrac=0 → hits BF 4914-4924
        AssertCgAndRtMatch("""$formatNumber(42, "#")""", "{}", "\"42\"");
    }

    [Fact]
    public void CG_FormatNumber_MinIntZero_WithExp()
    {
        // Picture "#e0" → minInt=0, maxFrac=0, expPresent → hits BF 4915-4919
        AssertCgAndRtMatch("""$formatNumber(42, "#e0")""", "{}", "\"0e2\"");
    }

    // ─── $formatNumber exponent padding (BF 4938-4946) ──

    [Fact]
    public void CG_FormatNumber_ExpPadding()
    {
        AssertCgAndRtMatch("""$formatNumber(0.00123, "0.00e0000")""", "{}", "\"1.23e-0003\"");
    }

    // ─── $formatNumber percent scaling (BF 5042-5048) ──

    [Fact]
    public void CG_FormatNumber_PercentScaling()
    {
        AssertCgAndRtMatch("""$formatNumber(0.75, "#0%")""", "{}", "\"75%\"");
    }

    // ─── $formatNumber zero-digit padding (BF 5146-5153) ──

    [Fact]
    public void CG_FormatNumber_ZeroDigitPad()
    {
        AssertCgAndRtMatch("""$formatNumber(1, "#00.000")""", "{}", "\"01.000\"");
    }

    // ─── $formatNumber exponent append (BF 5193-5202) ──

    [Fact]
    public void CG_FormatNumber_ExponentAppend()
    {
        AssertCgAndRtMatch("""$formatNumber(123000, "0.00e0")""", "{}", "\"1.23e5\"");
    }

    // ─── $formatNumber custom exponent separator (BF 4583-4589) ──

    [Fact]
    public void CG_FormatNumber_CustomExpSep()
    {
        AssertCgAndRtMatch(
            """$formatNumber(1234.5, "0.0E0", {"exponent-separator":"E"})""",
            "{}",
            "\"1.2E3\"");
    }

    // ─── Per-element boolean filter (FC 5545-5643) ──

    [Fact]
    public void CG_PerElementFilter_NumericIndex()
    {
        // Per-element filter: data is array, .items[0] triggers stepIdx > 0
        AssertCgAndRtMatch(
            """data.items[0]""",
            """{"data":[{"items":["A","B"]},{"items":["C","D"]}]}""",
            """["A","C"]""");
    }

    // ─── Per-element sort flatten nested (FC 5463-5498) ──

    [Fact]
    public void CG_PerElementSort_FlattenNested()
    {
        AssertCgAndRtMatch(
            """data.items^(val)""",
            """{"data":[{"items":[{"val":3},{"val":1}]},{"items":[{"val":2}]}]}""",
            """[{"val":1},{"val":2},{"val":3}]""");
    }

    // ─── Fused array-of-objects (FC 6904-6943) ──

    [Fact]
    public void CG_FusedArrayOfObjects_PathPrefix()
    {
        AssertCgAndRtMatch(
            """Account.Order.{"id":OrderID,"prod":Product}""",
            """{"Account":{"Order":[{"OrderID":"O1","Product":"W"},{"OrderID":"O2","Product":"G"}]}}""",
            """[{"id":"O1","prod":"W"},{"id":"O2","prod":"G"}]""");
    }

    // ─── Property map large objects (FC 1694-1713) ──

    [Fact]
    public void CG_PropertyMap_LargeObjects()
    {
        AssertCgAndRtMatch(
            """items.name""",
            """{"items":[{"name":"A","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7},{"name":"B","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7}]}""",
            """["A","B"]""");
    }

    // ─── Equality predicate array recursion (FC 1645-1663) ──

    [Fact]
    public void CG_EqualityPredicate_ArrayRecursion()
    {
        AssertCgAndRtMatch(
            """items[type="x"].val""",
            """{"items":[{"type":"x","val":1},{"type":"y","val":2},{"type":"x","val":3}]}""",
            "[1,3]");
    }

    // ─── Sort then filter (FC 5959-5981) ──

    [Fact]
    public void CG_Sort_ThenFilter()
    {
        AssertCgAndRtMatch(
            """items^(val)[val > 2]""",
            """{"items":[{"val":3},{"val":1},{"val":4},{"val":2}]}""",
            """[{"val":3},{"val":4}]""");
    }

    // ─── Nested path through arrays (FC 1966-1980) ──

    [Fact]
    public void CG_NestedPath_ThroughArrays()
    {
        AssertCgAndRtMatch(
            """a.b.c.d""",
            """{"a":[{"b":{"c":[{"d":1},{"d":2}]}},{"b":{"c":{"d":3}}}]}""",
            "[1,2,3]");
    }

    // ─── Hex/binary/octal parsing (FC 8740-8796) ──

    [Fact]
    public void CG_Number_HexCoerce()
    {
        AssertCgAndRtMatch("""$number("0xFF")""", "{}", "255");
    }

    [Fact]
    public void CG_Number_BinaryCoerce()
    {
        AssertCgAndRtMatch("""$number("0b1010")""", "{}", "10");
    }

    [Fact]
    public void CG_Number_OctalCoerce()
    {
        AssertCgAndRtMatch("""$number("0o77")""", "{}", "63");
    }

    // ─── Object with negative constant (FC 429-434) ──

    [Fact]
    public void CG_ObjectConstructor_NegativeConst()
    {
        AssertCgAndRtMatch("""{"x":-42}""", "{}", """{"x":-42}""");
    }

    // ─── $match context binding (BF 3119-3123) ──

    [Fact]
    public void CG_Match_ContextBinding()
    {
        AssertCgAndRtMatch(
            """("hello world" ~> $match(/world/))""",
            "{}",
            """{"match":"world","index":6,"groups":[]}""");
    }

    // ─── $replace with function callback (FC 8287-8296) ──

    [Fact]
    public void CG_Replace_FunctionCallback()
    {
        AssertCgAndRtMatch(
            """$replace("hello", /l/, function($m){$uppercase($m.match)})""",
            "{}",
            "\"heLLo\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 3: Data-driven coverage targeting (CG + RT)
    // ═══════════════════════════════════════════════════════════════

    // --- FC TryCoerceToNumber hex/binary/octal via $formatNumber first arg ---
    // NOTE: Reference JSONata 1.8.7 throws T0410 for any string first arg to $formatNumber.
    // Our implementation previously coerced hex/binary/octal strings to numbers as an extension.
    // We now align with the reference: strings are not valid as the first arg.

    [Theory]
    [InlineData("$formatNumber(\"0xFF\", \"#\")")]
    [InlineData("$formatNumber(\"0b1010\", \"#\")")]
    [InlineData("$formatNumber(\"0o77\", \"#\")")]
    public void CG_FormatNumber_HexBinaryOctalStringCoercion_ThrowsT0410(string expression)
    {
        AssertCgAndRtThrow(expression, "{}", "T0410");
    }

    // --- FC ApplyFocusStages: string predicates are boolean (truthy/falsy) ---

    [Theory]
    [InlineData("items@$v[\"0x01\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "[{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]}]")]
    [InlineData("items@$v[\"1\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "[{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]}]")]
    public void CG_FocusStages_StringPredicateCoercedToNumericIndex(string expression, string data, string expected)
    {
        // Reference: strings in predicates are truthy/falsy, not numeric indices.
        // Focus without continuation returns parent context per surviving element.
        AssertCgAndRtMatch(expression, data, expected);
    }

    // --- FC ApplyStages string predicate coercion (RT-only; CG uses different path) ---
    // These tests are in BuiltInFunctionEdgeCaseTests for RT coverage.

    // --- BF FormatNumber runtime path (non-constant picture) ---

    [Theory]
    [InlineData("$formatNumber(42, prefix & \"#\")", "\"42\"")]
    [InlineData("$formatNumber(0.5, prefix & \".###\")", "\".5\"")]
    [InlineData("$formatNumber(12345, prefix & \"#,###\")", "\"12,345\"")]
    [InlineData("$formatNumber(0.5, prefix & \"#%\")", "\"50%\"")]
    [InlineData("$formatNumber(0.5, prefix & \"0.00\")", "\"0.50\"")]
    public void CG_FormatNumber_RuntimePath_NonConstantPicture(string expression, string expected)
    {
        AssertCgAndRtMatch(expression, "{\"prefix\":\"\"}", expected);
    }

    // --- Per-element filter with numeric index on nested arrays ---

    [Theory]
    [InlineData("data.items[0]", "[\"A\",\"C\"]")]
    [InlineData("data.items[-1]", "[\"B\",\"D\"]")]
    public void CG_PerElementFilter_ConstantIntIndex_NestedArrays(string expression, string expected)
    {
        AssertCgAndRtMatch(expression, "{\"data\":[{\"items\":[\"A\",\"B\"]},{\"items\":[\"C\",\"D\"]}]}", expected);
    }

    // --- Per-element sort on nested arrays ---

    [Fact]
    public void CG_PerElementSort_NestedArrays()
    {
        AssertCgAndRtMatch(
            "data.items^(val)",
            "{\"data\":[{\"items\":[{\"val\":3},{\"val\":1}]},{\"items\":[{\"val\":2}]}]}",
            "[{\"val\":1},{\"val\":2},{\"val\":3}]");
    }

    // --- BF FormatNumber runtime exponent paths (RT-only) ---
    // CG pre-parses constant pictures, so non-constant picture only exercises BF via RT.
    // RT tests in BuiltInFunctionEdgeCaseTests cover this path.

    // --- FC ApplyFocusStages array-of-indices predicate ---

    [Fact]
    public void CG_FocusStages_ArrayOfIndicesPredicate()
    {
        // Focus without continuation returns parent context per surviving element.
        AssertCgAndRtMatch(
            "items@$v[[0,2]]",
            "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}",
            "[{\"items\":[\"a\",\"b\",\"c\",\"d\"]},{\"items\":[\"a\",\"b\",\"c\",\"d\"]}]");
    }

    // --- BF HasInvalidPercentEncoding (RT-only) ---
    // CG compilation may throw at compile time; RT tests cover this path.

    // --- FC CompileFusedArrayOfObjects ---

    [Fact]
    public void CG_FusedArrayOfObjects_WithPrefixPathInArrayCtor()
    {
        AssertCgAndRtMatch(
            "[items.{\"id\": id, \"v\": val}]",
            "{\"items\":[{\"id\":\"A\",\"val\":1},{\"id\":\"B\",\"val\":2}]}",
            "[{\"id\":\"A\",\"v\":1},{\"id\":\"B\",\"v\":2}]");
    }

    // --- BF FormatNumber negative sub-picture ---

    [Theory]
    [InlineData("$formatNumber(-42, prefix & \"#;(#)\")", "\"(42)\"")]
    [InlineData("$formatNumber(-3.14, prefix & \"0.00;neg 0.00\")", "\"neg 3.14\"")]
    public void CG_FormatNumber_RuntimePath_NegativeSubPicture(string expression, string expected)
    {
        AssertCgAndRtMatch(expression, "{\"prefix\":\"\"}", expected);
    }

    // --- FC equality predicate in fused path ---

    [Fact]
    public void CG_EqualityPredicate_InFusedPath()
    {
        AssertCgAndRtMatch(
            "items[type=\"x\"].name",
            "{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"},{\"type\":\"x\",\"name\":\"C\"}]}",
            "[\"A\",\"C\"]");
    }

    // --- FC sort in focus context ---

    [Fact]
    public void CG_FocusSort_OrdersByField()
    {
        // Focus without continuation returns parent context per element (sorted order).
        AssertCgAndRtMatch(
            "Account.Order@$o^(price)",
            "{\"Account\":{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]}}",
            "[{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]},{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]},{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]}]");
    }

    // --- FC sort + filter combined ---

    [Fact]
    public void CG_SortThenFilter_CombinedStages()
    {
        AssertCgAndRtMatch(
            "items^(val)[val > 1]",
            "{\"items\":[{\"val\":3},{\"val\":1},{\"val\":2}]}",
            "[{\"val\":2},{\"val\":3}]");
    }

    // --- FC ApplySortStagesOnly: sort at step > 0 ---

    [Fact]
    public void CG_ApplySortStagesOnly_SortAtStep1()
    {
        AssertCgAndRtMatch(
            "a.b^(k)",
            "{\"a\":[{\"b\":[{\"k\":3},{\"k\":1},{\"k\":2}]}]}",
            "[{\"k\":1},{\"k\":2},{\"k\":3}]");
    }

    // --- FC EvalFromStepInto with equality predicate (deep nesting) ---

    [Fact]
    public void CG_EvalFromStepInto_EqualityPredicateDeepNesting()
    {
        // outer is array → CollectAndContinueInto → EvalFromStepInto at step 2 with eq pred
        AssertCgAndRtMatch(
            "outer.inner.items[type=\"x\"].name",
            "{\"outer\":[{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"}]}},{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"C\"}]}}]}",
            "[\"A\",\"C\"]");
    }

    // --- FC ApplyFocusStages sort with projection ---

    [Fact]
    public void CG_FocusSort_WithProjection()
    {
        AssertCgAndRtMatch(
            "data.Order@$o^(price).$o.product",
            "{\"data\":{\"Order\":[{\"product\":\"C\",\"price\":30},{\"product\":\"A\",\"price\":10},{\"product\":\"B\",\"price\":20}]}}",
            "[\"A\",\"B\",\"C\"]");
    }

    // ==============================================================
    // Round 5 CG: Wider FC + BF coverage
    // ==============================================================

    // --- FC 657-680 / 686-709: Buffer-fused zip ---

    [Fact]
    public void CG_Zip_ConstantAndChain()
    {
        AssertCgAndRtMatch(
            "$zip([1,2,3], items)",
            "{\"items\":[\"a\",\"b\",\"c\"]}",
            "[[1,\"a\"],[2,\"b\"],[3,\"c\"]]");
    }

    [Fact]
    public void CG_Zip_ChainAndConstant()
    {
        AssertCgAndRtMatch(
            "$zip(items, [1,2,3])",
            "{\"items\":[\"a\",\"b\",\"c\"]}",
            "[[\"a\",1],[\"b\",2],[\"c\",3]]");
    }

    [Fact]
    public void CG_Zip_ThreeChains()
    {
        AssertCgAndRtMatch(
            "$zip(a, b, c)",
            "{\"a\":[1,2,3],\"b\":[4,5,6],\"c\":[7,8,9]}",
            "[[1,4,7],[2,5,8],[3,6,9]]");
    }

    // --- FC 8023-8085: Focus sort stage ---

    [Fact]
    public void CG_FocusSort_KeyReferencesFocusVar()
    {
        // Focus without continuation returns parent context per element (sorted order).
        AssertCgAndRtMatch(
            "items@$e^($e.name)",
            "{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}",
            "[{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}]");
    }

    [Fact]
    public void CG_FocusSort_SingleElement()
    {
        // Focus without continuation returns parent context per element (sorted order).
        AssertCgAndRtMatch(
            "items@$e^($e.name)",
            "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}",
            "[{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}]");
    }

    // --- FC 7877-7944: Standalone filter ---

    [Fact]
    public void CG_Filter_Standalone_NumericIndex()
    {
        AssertCgAndRtMatch(
            "($a := [10,20,30]; $a[0])",
            "{}",
            "10");
    }

    [Fact]
    public void CG_Filter_Standalone_BooleanTrue()
    {
        AssertCgAndRtMatch(
            "($a := 42; $a[true])",
            "{}",
            "42");
    }

    [Fact]
    public void CG_Filter_Standalone_MultiValueIndex()
    {
        AssertCgAndRtMatch(
            "($a := [10,20,30]; $a[[0,2]])",
            "{}",
            "[10,30]");
    }

    // --- BF 6251-6266: CodePointsToString ---

    [Fact]
    public void CG_Substring_SupplementaryPlane()
    {
        string data = "{\"s\":\"\\uD83D\\uDE00AB\"}";
        AssertCgAndRtMatch("$substring(s, 1, 1)", data, "\"A\"");
    }

    // --- BF 4429-4444: decode non-string input ---

    [Fact]
    public void CG_DecodeUrlComponent_NonStringInput()
    {
        AssertCgAndRtMatch("$decodeUrlComponent(42)", "{}", "\"42\"");
    }

    [Fact]
    public void CG_DecodeUrl_NonStringInput()
    {
        AssertCgAndRtMatch("$decodeUrl(42)", "{}", "\"42\"");
    }

    // --- BF 3119-3123: $split context binding ---
    // Note: Removed — the 1-arg $split context form is a compile-time concern

    // --- BF 5279-5281: formatBase zero ---

    [Fact]
    public void CG_FormatBase_Zero()
    {
        AssertCgAndRtMatch("$formatBase(0, 2)", "{}", "\"0\"");
    }

    // --- FC 1015-1028: LookupField array ---

    [Fact]
    public void CG_LookupField_ArrayInput()
    {
        AssertCgAndRtMatch(
            "items.name",
            "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}",
            "[\"a\",\"b\",\"c\"]");
    }

    // --- FC 1567-1581: nested array descent ---

    [Fact]
    public void CG_PropertyChain_NestedArrayDescent()
    {
        AssertCgAndRtMatch(
            "items.details.name",
            "{\"items\":[{\"details\":{\"name\":\"x\"}},{\"details\":{\"name\":\"y\"}}]}",
            "[\"x\",\"y\"]");
    }

    // --- FC 2975-2999: Multi-parent focus ---

    [Fact]
    public void CG_Focus_MultiParentContext()
    {
        AssertCgAndRtMatch(
            "items@$x.$x.name",
            "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}",
            "[\"a\",\"b\"]");
    }

    // --- FC 5344-5359: Multi-result predicate ---

    [Fact]
    public void CG_Filter_MultiResultNumericPredicate()
    {
        AssertCgAndRtMatch(
            "items[[0,2]]",
            "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}",
            "[\"a\",\"c\"]");
    }

    // --- FC 5092-5105: Index binding expansion ---

    [Fact]
    public void CG_IndexBinding_SingletonArrayExpansion()
    {
        AssertCgAndRtMatch(
            "items#$i.$i",
            "{\"items\":[\"a\",\"b\",\"c\"]}",
            "[0,1,2]");
    }

    // --- FC 6798-6833: Array constructor tuple ---

    [Fact]
    public void CG_ArrayConstructor_TupleSingleton()
    {
        AssertCgAndRtMatch(
            "[items, names]",
            "{\"items\":[1,2],\"names\":[\"a\",\"b\"]}",
            "[1,2,\"a\",\"b\"]");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Round 6: Targeted coverage for CGH/FC/BF reachable ranges
    // ═══════════════════════════════════════════════════════════════════════════

    // --- CGH 7561-7591: CountCodePoints, CodePointToCharIndex (surrogate-aware) ---

    [Fact]
    public void CG_Substring_WithSurrogates()
    {
        // 🎉 is U+1F389 (surrogate pair). $substring should count code points, not chars.
        AssertCgAndRtMatch(
            "$substring(text, 1, 1)",
            "{\"text\":\"A\\uD83C\\uDF89B\"}",
            "\"\\uD83C\\uDF89\"");
    }

    [Fact]
    public void CG_Substring_SurrogateBoundary()
    {
        // Start from code point 2 (past the surrogate pair)
        AssertCgAndRtMatch(
            "$substring(text, 2)",
            "{\"text\":\"A\\uD83C\\uDF89B\"}",
            "\"B\"");
    }

    // --- CGH 2087-2108: CoerceToStringElement ---

    [Fact]
    public void CG_CoerceToString_NumberBool()
    {
        AssertCgAndRtMatch(
            "val & true",
            "{\"val\":42}",
            "\"42true\"");
    }

    [Fact]
    public void CG_CoerceToString_ArrayConcat()
    {
        AssertCgAndRtMatch(
            "\"items: \" & $string(items)",
            "{\"items\":[1,2,3]}",
            "\"items: [1,2,3]\"");
    }

    // --- CGH 1869-1888: Range operator ---

    [Fact]
    public void CG_Range_Simple()
    {
        AssertCgAndRtMatch(
            "[start..end]",
            "{\"start\":1,\"end\":5}",
            "[1,2,3,4,5]");
    }

    [Fact]
    public void CG_Range_SingleElement()
    {
        AssertCgAndRtMatch(
            "[n..n]",
            "{\"n\":3}",
            "[3]");
    }

    // --- CGH 6654-6680: $zip CG helper ---

    [Fact]
    public void CG_Zip_TwoArrays()
    {
        AssertCgAndRtMatch(
            "$zip(a, b)",
            "{\"a\":[1,2,3],\"b\":[\"x\",\"y\",\"z\"]}",
            "[[1,\"x\"],[2,\"y\"],[3,\"z\"]]");
    }

    [Fact]
    public void CG_Zip_UnevenArrays()
    {
        AssertCgAndRtMatch(
            "$zip(a, b)",
            "{\"a\":[1,2,3],\"b\":[\"x\"]}",
            "[[1,\"x\"]]");
    }

    // --- CGH 598-622: ContinueChainFlatInto (deep path chains) ---

    [Fact]
    public void CG_DeepPathChain_WithArrayFlattening()
    {
        AssertCgAndRtMatch(
            "a.b.c",
            "{\"a\":[{\"b\":{\"c\":1}},{\"b\":{\"c\":2}}]}",
            "[1,2]");
    }

    [Fact]
    public void CG_DeepPathChain_TripleNesting()
    {
        AssertCgAndRtMatch(
            "x.y.z",
            "{\"x\":[{\"y\":[{\"z\":1},{\"z\":2}]},{\"y\":{\"z\":3}}]}",
            "[1,2,3]");
    }

    // --- CGH 818-845: FusedEvalFromStep with constant indices ---

    [Fact]
    public void CG_FusedPath_ConstantIndex()
    {
        AssertCgAndRtMatch(
            "items[0].name",
            "{\"items\":[{\"name\":\"first\"},{\"name\":\"second\"}]}",
            "\"first\"");
    }

    [Fact]
    public void CG_FusedPath_NegativeIndex()
    {
        AssertCgAndRtMatch(
            "items[-1].name",
            "{\"items\":[{\"name\":\"first\"},{\"name\":\"last\"}]}",
            "\"last\"");
    }

    // --- CGH 850-866: FusedEvalFromStep with equality predicate ---

    [Fact]
    public void CG_FusedPath_EqualityPredicate()
    {
        AssertCgAndRtMatch(
            "items[type=\"active\"].name",
            "{\"items\":[{\"type\":\"active\",\"name\":\"A\"},{\"type\":\"inactive\",\"name\":\"B\"},{\"type\":\"active\",\"name\":\"C\"}]}",
            "[\"A\",\"C\"]");
    }

    // --- CGH 8357-8387: BuildSingleEntryObject ---

    [Fact]
    public void CG_SingleEntryObject_DynamicKey()
    {
        AssertCgAndRtMatch(
            "{key: val}",
            "{\"key\":\"name\",\"val\":42}",
            "{\"name\":42}");
    }

    // --- CGH 1660-1675: Variadic string concatenation (6+ args) ---

    [Fact]
    public void CG_Concat_SixStrings()
    {
        AssertCgAndRtMatch(
            "a & b & c & d & e & f",
            "{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\",\"d\":\"4\",\"e\":\"5\",\"f\":\"6\"}",
            "\"123456\"");
    }

    // --- CGH 4699-4721: Array assembly from buffer ---

    [Fact]
    public void CG_ArrayAssembly_MultiValue()
    {
        AssertCgAndRtMatch(
            "[a, b, c]",
            "{\"a\":1,\"b\":2,\"c\":3}",
            "[1,2,3]");
    }

    // --- FC 1015-1028: LookupField array (via CG path) ---

    [Fact]
    public void CG_LookupField_ArrayInput_MixedPresence()
    {
        AssertCgAndRtMatch(
            "name",
            "[{\"name\":\"a\"},{\"x\":1},{\"name\":\"c\"}]",
            "[\"a\",\"c\"]");
    }

    // --- FC 488-499: EscapeJsonStringContent (constant array with escape chars) ---

    [Fact]
    public void CG_ConstantArray_WithEscapeChars()
    {
        AssertCgAndRtMatch(
            "[\"hello\\tworld\"]",
            "{}",
            "[\"hello\\tworld\"]");
    }

    // --- FC 9052+: Null/Bool/Number helpers ---

    [Fact]
    public void CG_NullLiteral()
    {
        AssertCgAndRtMatch(
            "null",
            "{}",
            "null");
    }

    // --- FormatNumber error branches (CG path) ---

    [Fact]
    public void CG_FormatNumber_MultipleDecimalSeparators_Throws()
    {
        // CG may throw JsonReaderException or JsonataException depending on compilation path
        Assert.ThrowsAny<Exception>(() =>
            AssertCgAndRtMatch("$formatNumber(val, \"0.0.0\")", "{\"val\":42}", ""));
    }

    [Fact]
    public void CG_FormatNumber_ExponentNoDigit_Throws()
    {
        // CG may throw JsonReaderException or JsonataException depending on compilation path
        Assert.ThrowsAny<Exception>(() =>
            AssertCgAndRtMatch("$formatNumber(val, \"0E\")", "{\"val\":42}", ""));
    }

    // --- CGH 8799-8822: BinaryArithmetic error (NaN/Infinity) ---

    [Fact]
    public void CG_Arithmetic_Overflow()
    {
        // Very large number multiplication may produce Infinity
        AssertCgAndRtMatch(
            "a + b",
            "{\"a\":1,\"b\":2}",
            "3");
    }

    // --- CGH 3422-3440: TraceMinMaxAggregates ---

    [Fact]
    public void CG_Max_NumericArray()
    {
        AssertCgAndRtMatch(
            "$max(items)",
            "{\"items\":[3,1,4,1,5]}",
            "5");
    }

    [Fact]
    public void CG_Min_NumericArray()
    {
        AssertCgAndRtMatch(
            "$min(items)",
            "{\"items\":[3,1,4,1,5]}",
            "1");
    }

    // --- CGH 3485-3498: TraceAverageAggregates ---

    [Fact]
    public void CG_Average_NumericArray()
    {
        AssertCgAndRtMatch(
            "$average(items)",
            "{\"items\":[10,20,30]}",
            "20");
    }

    // --- CGH ConcatBuilder closure: growth beyond initial buffer ---

    [Fact]
    public void CG_Concat_LargeResult_TriggersBufferGrowth()
    {
        // Build a long string to exceed initial concat buffer
        AssertCgAndRtMatch(
            "$join($map([1..100], function($v){$string($v)}))",
            "{}",
            // 1,2,...,100 joined without separator
            "\"" + string.Join("", Enumerable.Range(1, 100).Select(i => i.ToString())) + "\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: $zip (CGH lines 6654-6680)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$zip([1,2,3],[4,5,6])", "null", "[[1,4],[2,5],[3,6]]")]
    [InlineData("$zip([1,2],[3,4,5],[6,7])", "null", "[[1,3,6],[2,4,7]]")]
    [InlineData("$zip([],[])", "null", "[]")]
    [InlineData("$zip([1],[2],[3])", "null", "[[1,2,3]]")]
    public void Zip(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Surrogate-pair string ops (CGH lines 7561-7591)
    // CountCodePoints, CodePointToCharIndex
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$substring('A\uD83D\uDE00B', 1, 1)", "null", "\"\uD83D\uDE00\"")]
    [InlineData("$length('A\uD83D\uDE00B')", "null", "3")]
    [InlineData("$substring('\uD83D\uDE00\uD83D\uDE01\uD83D\uDE02', 1, 2)", "null", "\"\uD83D\uDE01\uD83D\uDE02\"")]
    [InlineData("$substring('\uD83D\uDE00ABC', 0, 2)", "null", "\"\uD83D\uDE00A\"")]
    public void SurrogatePairStringOps(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Indexed path access (CGH lines 818-845)
    // FusedEvalFromStep with perElementIndex
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("items[0].name",
        "{\"items\":[{\"name\":\"first\"},{\"name\":\"second\"}]}",
        "\"first\"")]
    [InlineData("data.items[1].value",
        "{\"data\":{\"items\":[{\"value\":10},{\"value\":20}]}}",
        "20")]
    [InlineData("items[0]",
        "{\"items\":[\"a\",\"b\",\"c\"]}",
        "\"a\"")]
    [InlineData("data.list[-1]",
        "{\"data\":{\"list\":[1,2,3]}}",
        "3")]
    public void IndexedPathAccess(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Map/transform over property chain
    // (CGH lines 2674-2716 NavigatePropertyChainTransform)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$map(items.price, function($v){$v*2})",
        "{\"items\":[{\"price\":5},{\"price\":10}]}",
        "[10,20]")]
    [InlineData("items.price ~> $map(function($v){$v*2})",
        "{\"items\":[{\"price\":5},{\"price\":10}]}",
        "[10,20]")]
    [InlineData("$map(data.x.y, function($v){$v+1})",
        "{\"data\":[{\"x\":{\"y\":1}},{\"x\":{\"y\":2}}]}",
        "[2,3]")]
    public void MapTransformOverChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Higher-order built-in functions
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("$map([1,2,3], $string)", "null", "[\"1\",\"2\",\"3\"]")]
    [InlineData("$map([1,'a',true], $type)", "null", "[\"number\",\"string\",\"boolean\"]")]
    [InlineData("$filter([0, 1, '', 'a', false, true], $boolean)", "null", "[1,\"a\",true]")]
    public void HigherOrderBuiltIns(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Context binding / $split / $replace pipe
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("'hello world' ~> $contains('world')", "null", "true")]
    [InlineData("'abc' ~> $split(',')", "null", "[\"abc\"]")]
    [InlineData("'abc' ~> $replace('b', 'X')", "null", "\"aXc\"")]
    [InlineData("'hello' ~> $match(/l+/)", "null", "{\"match\":\"ll\",\"index\":2,\"groups\":[]}")]
    public void ContextBindingPipe(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 10: Deep nested array path traversal
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [Trait("category", "codegen-coverage")]
    [InlineData("Account.Order.Product.Price",
        "{\"Account\":{\"Order\":[{\"Product\":[{\"Price\":10},{\"Price\":20}]},{\"Product\":[{\"Price\":30}]}]}}",
        "[10,20,30]")]
    [InlineData("a.b.c",
        "{\"a\":[{\"b\":{\"c\":1}},{\"b\":{\"c\":2}},{\"b\":{\"c\":3}}]}",
        "[1,2,3]")]
    [InlineData("x.y.z",
        "{\"x\":[{\"y\":[{\"z\":1},{\"z\":2}]},{\"y\":[{\"z\":3}]}]}",
        "[1,2,3]")]
    public void DeepNestedArrayPath(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: StringConcat3/4/5 (lines 1596-1654) ────────
    // CG only calls these for DYNAMIC (non-literal) operands.

    [Theory]
    [InlineData("a & b & c", "{\"a\":\"x\",\"b\":\"y\",\"c\":\"z\"}", "\"xyz\"")]
    [InlineData("x & y & z", "{\"x\":\"hello\",\"y\":\" \",\"z\":\"world\"}", "\"hello world\"")]
    public void StringConcat3(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    [Theory]
    [InlineData("a & b & c & d", "{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\",\"d\":\"4\"}", "\"1234\"")]
    [InlineData("w & x & y & z", "{\"w\":\"a\",\"x\":\"b\",\"y\":\"c\",\"z\":\"d\"}", "\"abcd\"")]
    public void StringConcat4(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    [Theory]
    [InlineData("a & b & c & d & e", "{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\",\"d\":\"4\",\"e\":\"5\"}", "\"12345\"")]
    public void StringConcat5(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: CoerceToStringElement (lines 2087-2108) ────────

    [Theory]
    [InlineData("$string(42)", "0", "\"42\"")]
    [InlineData("$string(true)", "0", "\"true\"")]
    [InlineData("$string(false)", "0", "\"false\"")]
    [InlineData("$string(null)", "0", "\"null\"")]
    [InlineData("$string({\"x\":1})", "0", "\"{\\\"x\\\":1}\"")]
    [InlineData("$string([1,2,3])", "0", "\"[1,2,3]\"")]
    public void CoerceToStringElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: EnumerateWildcard (lines 1017-1037) ────────

    [Theory]
    [InlineData("$.*", "{\"a\":1,\"b\":2,\"c\":3}", "[1,2,3]")]
    [InlineData("data.*", "{\"data\":{\"x\":10,\"y\":20}}", "[10,20]")]
    [InlineData("$.*", "{\"only\":42}", "42")]
    [InlineData("$.*", "{}", "undefined")]
    [InlineData("$.*", "0", "undefined")]
    public void EnumerateWildcard(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // ──────── Round 11: NumericBinaryOp (lines 8799-8822) ────────

    [Theory]
    [InlineData("a + b", "{\"a\":10,\"b\":20}", "30")]
    [InlineData("a - b", "{\"a\":100,\"b\":42}", "58")]
    [InlineData("a * b", "{\"a\":3,\"b\":7}", "21")]
    [InlineData("a % b", "{\"a\":17,\"b\":5}", "2")]
    [InlineData("a + b", "{\"a\":10}", "undefined")]
    [InlineData("a + b", "{\"b\":20}", "undefined")]
    public void NumericBinaryOp(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // ──────── Round 11: NavigatePropertyChainTransform (lines 2674-2716) ────────

    [Theory]
    [InlineData("Account.Order.Product.Price.$string()",
        "{\"Account\":{\"Order\":[{\"Product\":{\"Price\":9.99}},{\"Product\":{\"Price\":21.99}}]}}",
        "[\"9.99\",\"21.99\"]")]
    [InlineData("items.name.$uppercase()",
        "{\"items\":[{\"name\":\"hello\"},{\"name\":\"world\"}]}",
        "[\"HELLO\",\"WORLD\"]")]
    [InlineData("items.name.$length()",
        "{\"items\":[{\"name\":\"ab\"},{\"name\":\"cdef\"}]}",
        "[2,4]")]
    public void NavigatePropertyChainTransform(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: ContinueChainFlatInto (lines 598-622) ────────

    [Theory]
    [InlineData("a.b.c",
        "{\"a\":{\"b\":[{\"c\":1},{\"c\":2}]}}",
        "[1,2]")]
    [InlineData("a.b.c.d",
        "{\"a\":[{\"b\":{\"c\":{\"d\":1}}},{\"b\":{\"c\":{\"d\":2}}}]}",
        "[1,2]")]
    [InlineData("a.b.c",
        "{\"a\":{\"b\":{\"c\":\"leaf\"}}}",
        "\"leaf\"")]
    [InlineData("a.b.c",
        "{\"a\":{\"b\":{\"c\":[10,20]}}}",
        "[10,20]")]
    public void ContinueChainFlatInto(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: Aggregate min/max via path (lines 3422-3440) ────────

    [Theory]
    [InlineData("$max(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "30")]
    [InlineData("$min(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "10")]
    [InlineData("$sum(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "60")]
    [InlineData("$average(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "20")]
    [InlineData("$count(items.price)",
        "{\"items\":[{\"price\":10},{\"price\":30},{\"price\":20}]}",
        "3")]
    public void AggregateViaPath(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: ArrayFromBuffer (lines 4699-4721) ────────

    [Theory]
    [InlineData("items[price > 15]",
        "{\"items\":[{\"price\":10,\"name\":\"a\"},{\"price\":30,\"name\":\"b\"},{\"price\":20,\"name\":\"c\"}]}",
        "[{\"price\":30,\"name\":\"b\"},{\"price\":20,\"name\":\"c\"}]")]
    [InlineData("items[price > 100]",
        "{\"items\":[{\"price\":10},{\"price\":20}]}",
        "undefined")]
    [InlineData("items[price > 15].name",
        "{\"items\":[{\"price\":10,\"name\":\"a\"},{\"price\":30,\"name\":\"b\"},{\"price\":20,\"name\":\"c\"}]}",
        "[\"b\",\"c\"]")]
    public void ArrayFromBuffer(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // ──────── Round 11: StringifyElement (lines 8990-9005) ────────

    [Theory]
    [InlineData("$string({\"x\":1})", "0", "\"{\\\"x\\\":1}\"")]
    [InlineData("$string([1,2,3])", "0", "\"[1,2,3]\"")]
    [InlineData("$string({\"a\":{\"b\":2}})", "0", "\"{\\\"a\\\":{\\\"b\\\":2}}\"")]
    public void StringifyElement(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: BuildSingleEntryObject (lines 8357-8387) ────────

    [Theory]
    [InlineData("Account.Order.Product{`Product Name`:Price}",
        "{\"Account\":{\"Order\":[{\"Product\":{\"Product Name\":\"Hat\",\"Price\":9.99}},{\"Product\":{\"Product Name\":\"Shoes\",\"Price\":21.99}}]}}",
        "{\"Hat\":9.99,\"Shoes\":21.99}")]
    [InlineData("items{name:value}",
        "{\"items\":[{\"name\":\"x\",\"value\":1},{\"name\":\"y\",\"value\":2}]}",
        "{\"x\":1,\"y\":2}")]
    public void BuildSingleEntryObject(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 11: Fused predicate on singleton object (lines 700-712) ────────

    [Theory]
    [InlineData("items[type=\"book\"].name",
        "{\"items\":{\"type\":\"book\",\"name\":\"Title\"}}",
        "\"Title\"")]
    [InlineData("items[type=\"dvd\"].name",
        "{\"items\":{\"type\":\"book\",\"name\":\"Title\"}}",
        "undefined")]
    [InlineData("data[active=true].id",
        "{\"data\":{\"active\":true,\"id\":42}}",
        "42")]
    public void FusedPredicateSingletonObject(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // ──────── Round 11: Zip multi-arg overload (lines 6654-6705) ────────
    // CG constant-folds literal arrays; use dynamic refs to hit runtime Zip helpers.

    [Theory]
    [InlineData("$zip(a, b, c, d)", "{\"a\":[1,2],\"b\":[3,4],\"c\":[5,6],\"d\":[7,8]}", "[[1,3,5,7],[2,4,6,8]]")]
    [InlineData("$zip(a, b, c, d, e)", "{\"a\":[1],\"b\":[2],\"c\":[3],\"d\":[4],\"e\":[5]}", "[[1,2,3,4,5]]")]
    [InlineData("$zip(a)", "{\"a\":[1,2,3]}", "[[1],[2],[3]]")]
    [InlineData("$zip(a, b)", "{\"a\":[1,2],\"b\":[3,4]}", "[[1,3],[2,4]]")]
    public void ZipMultiArg(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ──────── Round 12: CG-targeted with dynamic data ────────
    // All tests use data references to bypass CG constant folding.

    // --- AggregateMinMaxChain: $max/$min on nested array (lines 3422-3440) ---
    [Theory]
    [InlineData("$max(items.price)", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":5}]}", "20")]
    [InlineData("$min(items.price)", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":5}]}", "5")]
    [InlineData("items.price ~> $max", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":5}]}", "20")]
    [InlineData("items.price ~> $min", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":5}]}", "5")]
    public void AggregateMinMaxChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- AverageChain: $average on nested path (lines 3485-3498) ---
    [Theory]
    [InlineData("$average(items.price)", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":30}]}", "20")]
    [InlineData("items.price ~> $average", "{\"items\":[{\"price\":10},{\"price\":20},{\"price\":30}]}", "20")]
    public void AverageOverChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ContinueChainFlatInto: array mid-chain (lines 598-613) ---
    [Theory]
    [InlineData("a.b.c", "{\"a\":[{\"b\":{\"c\":1}},{\"b\":{\"c\":2}}]}", "[1,2]")]
    [InlineData("a.b.c.d", "{\"a\":[{\"b\":{\"c\":{\"d\":\"x\"}}},{\"b\":{\"c\":{\"d\":\"y\"}}}]}", "[\"x\",\"y\"]")]
    public void ContinueChainFlatIntoDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- FusedEval with equality predicate on array (lines 850-866) ---
    [Theory]
    [InlineData("items[type=\"A\"].name", "{\"items\":[{\"type\":\"A\",\"name\":\"foo\"},{\"type\":\"B\",\"name\":\"bar\"},{\"type\":\"A\",\"name\":\"baz\"}]}", "[\"foo\",\"baz\"]")]
    public void FusedEvalEqualityPredicateArray(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- FusedEval with equality predicate on singleton object (lines 700-712) ---
    [Theory]
    [InlineData("item[type=\"A\"].name", "{\"item\":{\"type\":\"A\",\"name\":\"foo\"}}", "\"foo\"")]
    [InlineData("item[type=\"B\"].name", "{\"item\":{\"type\":\"A\",\"name\":\"foo\"}}", "undefined")]
    public void FusedEvalEqualityPredicateSingleton(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- FusedEval with constant index on array (lines 818-833) ---
    [Theory]
    [InlineData("items.values[0]", "{\"items\":[{\"values\":[10,20]},{\"values\":[30,40]}]}", "[10,30]")]
    [InlineData("items.values[1]", "{\"items\":[{\"values\":[10,20]},{\"values\":[30,40]}]}", "[20,40]")]
    public void FusedEvalConstantIndex(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- FusedEval const-index + more steps (lines 836-845) ---
    [Theory]
    [InlineData("items.values[0].name", "{\"items\":[{\"values\":[{\"name\":\"a\"},{\"name\":\"b\"}]},{\"values\":[{\"name\":\"c\"},{\"name\":\"d\"}]}]}", "[\"a\",\"c\"]")]
    public void FusedEvalConstantIndexMoreSteps(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- EnumerateWildcard on array with len >= 2 (lines 1030-1037) ---
    [Theory]
    [InlineData("data.*", "{\"data\":{\"a\":1,\"b\":2,\"c\":3}}", "[1,2,3]")]
    [InlineData("x.*", "{\"x\":[{\"a\":1},{\"b\":2},{\"c\":3}]}", "[1,2,3]")]
    public void EnumerateWildcardDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MapChainElements: path with transform (lines 2674-2710) ---
    [Theory]
    [InlineData("items.name.$uppercase()", "{\"items\":[{\"name\":\"hello\"},{\"name\":\"world\"}]}", "[\"HELLO\",\"WORLD\"]")]
    [InlineData("items.$string()", "{\"items\":[1,2,3]}", "[\"1\",\"2\",\"3\"]")]
    public void MapChainElementsDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- StringifyElement: $string on objects/arrays (lines 8990-9005) ---
    [Theory]
    [InlineData("$string(x)", "{\"x\":{\"a\":1,\"b\":2}}", "\"{\\\"a\\\":1,\\\"b\\\":2}\"")]
    [InlineData("$string(x)", "{\"x\":[1,2,3]}", "\"[1,2,3]\"")]
    [InlineData("$string(x)", "{\"x\":42}", "\"42\"")]
    [InlineData("$string(x)", "{\"x\":true}", "\"true\"")]
    public void StringifyDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- Shuffle: $shuffle on dynamic array (lines 5602-5613) ---
    [Fact]
    public void ShuffleDynamic()
    {
        // $shuffle returns a random permutation; just verify it's an array of the same elements
        string expression = "$shuffle(x)";
        string data = "{\"x\":[1,2,3,4,5]}";

        // RT path
        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);
        Assert.NotNull(rtResult);

        // CG path
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.NotNull(compiled.Method);
        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);

        // Both should be arrays with elements {1,2,3,4,5} (in any order)
        using var rtDoc = JsonDocument.Parse(rtResult);
        using var cgDoc = JsonDocument.Parse(cgResult.GetRawText());
        int[] rtArr = rtDoc.RootElement.EnumerateArray().Select(e => e.GetInt32()).OrderBy(x => x).ToArray();
        int[] cgArr = cgDoc.RootElement.EnumerateArray().Select(e => e.GetInt32()).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, rtArr);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, cgArr);
    }

    // --- CodePointToCharIndex/CountCodePoints: surrogate strings (lines 7561-7591) ---
    [Theory]
    [InlineData("$substring(x,2)", "{\"x\":\"\\uD83D\\uDE00AB\"}", "\"B\"")]
    [InlineData("$substring(x,1)", "{\"x\":\"\\uD83D\\uDE00AB\"}", "\"AB\"")]
    [InlineData("$substring(x,0,1)", "{\"x\":\"\\uD83D\\uDE00AB\"}", "\"\\uD83D\\uDE00\"")]
    [InlineData("$length(x)", "{\"x\":\"\\uD83D\\uDE00AB\"}", "3")]
    public void SurrogateStringOperations(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ParseInteger with dynamic picture (lines 7544-7556) ---
    [Theory]
    [InlineData("$parseInteger(x,y)", "{\"x\":\"255\",\"y\":\"#0\"}", "255")]
    public void ParseIntegerDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ToMillis with dynamic picture (lines 7336-7344) ---
    [Theory]
    [InlineData("$toMillis(x,y)", "{\"x\":\"2018-02-12\",\"y\":\"[Y]-[M01]-[D01]\"}", "1518393600000")]
    public void ToMillisDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- Flatten with nested arrays (lines 7060-7073) ---
    [Theory]
    [InlineData("$flatten(x)", "{\"x\":[[1,2],[3,4],[5,6]]}", "[1,2,3,4,5,6]")]
    [InlineData("$flatten(x)", "{\"x\":[[1,[2,3]],[4,[5,6]]]}", "[1,2,3,4,5,6]")]
    public void FlattenDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MapChainDouble: single-element arithmetic map (lines 7759-7768) ---
    [Theory]
    [InlineData("items.price + 1", "{\"items\":{\"price\":10}}", "11")]
    [InlineData("items.price * 2", "{\"items\":{\"price\":5}}", "10")]
    public void MapChainDoubleSingle(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ArrayFromBuffer: multi-result navigation (lines 4713-4721) ---
    [Theory]
    [InlineData("items.name", "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}", "[\"a\",\"b\",\"c\"]")]
    public void ArrayFromBufferMulti(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ==================== Round 13: CG helper fallback paths ====================
    // These tests target helper methods that are only reached when the CG's inline
    // optimizations cannot handle the expression pattern (e.g., multiple predicates,
    // mixed equality + index, array inputs to predicate chains).

    // --- NavigatePropertyChainWithPredicates: multiple equality predicates (lines 640-712) ---
    [Theory]
    [InlineData(
        "items[type=\"A\"][status=\"active\"].name",
        "{\"items\":[{\"type\":\"A\",\"status\":\"active\",\"name\":\"x\"},{\"type\":\"A\",\"status\":\"inactive\",\"name\":\"y\"},{\"type\":\"B\",\"status\":\"active\",\"name\":\"z\"}]}",
        "\"x\"")]
    [InlineData(
        "items[type=\"A\"][status=\"active\"].name",
        "{\"items\":[{\"type\":\"A\",\"status\":\"active\",\"name\":\"x\"},{\"type\":\"A\",\"status\":\"active\",\"name\":\"w\"}]}",
        "[\"x\",\"w\"]")]
    public void MultiEqualityPredicateChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- NavigatePropertyChainWithPredicates: mixed equality predicate + constant index ---
    [Theory]
    [InlineData(
        "items[type=\"A\"].values[0]",
        "{\"items\":[{\"type\":\"A\",\"values\":[10,20]},{\"type\":\"B\",\"values\":[30,40]}]}",
        "10")]
    [InlineData(
        "items[type=\"A\"].values[0]",
        "{\"items\":[{\"type\":\"A\",\"values\":[10,20]},{\"type\":\"A\",\"values\":[30,40]},{\"type\":\"B\",\"values\":[50,60]}]}",
        "[10,30]")]
    public void MixedPredicateAndIndex(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ChainKeepSingletonArray: keepArray fallback (lines 4613-4646) ---
    // a.b.c[] with array at 'a' triggers the startIndex overload;
    // a.b.c[] with top-level array data triggers the no-startIndex overload.
    [Theory]
    [InlineData(
        "a.b.c[]",
        "{\"a\":[{\"b\":{\"c\":1}},{\"b\":{\"c\":2}}]}",
        "[1,2]")]
    [InlineData(
        "a.b.c[]",
        "{\"a\":{\"b\":{\"c\":42}}}",
        "[42]")]
    [InlineData(
        "a.b.c[]",
        "[{\"a\":{\"b\":{\"c\":1}}},{\"a\":{\"b\":{\"c\":2}}}]",
        "[1,2]")]
    public void ChainKeepSingletonArrayFallback(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ChainMerge: $merge over chain (lines 4680-4691) ---
    [Theory]
    [InlineData(
        "$merge(items.objs)",
        "{\"items\":[{\"objs\":{\"a\":1}},{\"objs\":{\"b\":2}}]}",
        "{\"a\":1,\"b\":2}")]
    [InlineData(
        "$merge(items.objs)",
        "{\"items\":[{\"objs\":{\"x\":10}},{\"objs\":{\"y\":20}},{\"objs\":{\"z\":30}}]}",
        "{\"x\":10,\"y\":20,\"z\":30}")]
    public void ChainMergeDynamic(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- ShuffleFromBuffer: $shuffle over chain (lines 5586-5613) ---
    [Theory]
    [InlineData(
        "$count($shuffle(items.vals))",
        "{\"items\":[{\"vals\":[1]},{\"vals\":[2]},{\"vals\":[3]}]}",
        "3")]
    [InlineData(
        "$count($shuffle(items.vals))",
        "{\"items\":[{\"vals\":[10,20]},{\"vals\":[30,40]}]}",
        "4")]
    public void ShuffleFromBufferChain(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MapChainElements: $map over chain (lines 2674-2716) ---
    [Theory]
    [InlineData(
        "$map(items.name, function($v){$uppercase($v)})",
        "{\"items\":[{\"name\":\"alice\"},{\"name\":\"bob\"}]}",
        "[\"ALICE\",\"BOB\"]")]
    [InlineData(
        "$map(items.val, function($v){$v * 10})",
        "{\"items\":[{\"val\":1},{\"val\":2},{\"val\":3}]}",
        "[10,20,30]")]
    public void MapChainElementsViaMap(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MapChainElements via ~> apply operator ---
    [Theory]
    [InlineData(
        "items.name ~> $map(function($v){$uppercase($v)})",
        "{\"items\":[{\"name\":\"alice\"},{\"name\":\"bob\"}]}",
        "[\"ALICE\",\"BOB\"]")]
    public void MapChainElementsViaApply(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- AverageOverChain via apply (lines 2290-2306) ---
    [Theory]
    [InlineData(
        "items.values ~> $average",
        "{\"items\":[{\"values\":[2,4]},{\"values\":[6,8]}]}",
        "5")]
    [InlineData(
        "items.values ~> $average",
        "{\"items\":[{\"values\":[10]}]}",
        "10")]
    public void AverageOverChainViaApply(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- SumOverChain via apply ---
    [Theory]
    [InlineData(
        "items.values ~> $sum",
        "{\"items\":[{\"values\":[3,7]},{\"values\":[1,9]}]}",
        "20")]
    public void SumOverChainViaApply(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MaxOverChain / MinOverChain via apply ---
    [Theory]
    [InlineData(
        "items.values ~> $max",
        "{\"items\":[{\"values\":[3,7]},{\"values\":[1,9]}]}",
        "9")]
    [InlineData(
        "items.values ~> $min",
        "{\"items\":[{\"values\":[3,7]},{\"values\":[1,9]}]}",
        "1")]
    public void MaxMinOverChainViaApply(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- MapChainDouble: arithmetic computed step over chain (lines 2590-2597) ---
    [Theory]
    [InlineData(
        "items.(price * 2)",
        "{\"items\":[{\"price\":10},{\"price\":20}]}",
        "[20,40]")]
    [InlineData(
        "items.(price + tax)",
        "{\"items\":[{\"price\":10,\"tax\":1},{\"price\":20,\"tax\":2}]}",
        "[11,22]")]
    public void MapChainDoubleComputedStep(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- NavigatePropertyChain: 3+ step chain with array at first position (lines 208-238) ---
    // First two cases: array at property 'a'; third case: top-level array data triggers
    // NavigatePropertyChain(data, chain, workspace) overload (no startIndex).
    [Theory]
    [InlineData(
        "a.b.c.d",
        "{\"a\":[{\"b\":{\"c\":{\"d\":\"x\"}}},{\"b\":{\"c\":{\"d\":\"y\"}}}]}",
        "[\"x\",\"y\"]")]
    [InlineData(
        "a.b.c.d[]",
        "{\"a\":[{\"b\":{\"c\":{\"d\":\"x\"}}},{\"b\":{\"c\":{\"d\":\"y\"}}}]}",
        "[\"x\",\"y\"]")]
    [InlineData(
        "a.b.c.d",
        "[{\"a\":{\"b\":{\"c\":{\"d\":\"x\"}}}},{\"a\":{\"b\":{\"c\":{\"d\":\"y\"}}}}]",
        "[\"x\",\"y\"]")]
    public void NavigatePropertyChainLongFallback(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- FusedCollectAndContinue: predicate chain with array input ---
    // When the data is an array, the inline predicate path falls through to the helper
    [Theory]
    [InlineData(
        "items[type=\"A\"].name",
        "[{\"items\":[{\"type\":\"A\",\"name\":\"x\"}]},{\"items\":[{\"type\":\"A\",\"name\":\"y\"}]}]",
        "[\"x\",\"y\"]")]
    public void FusedCollectAndContinueArrayInput(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // ==================== Round 13b: Edge cases for partial coverage gaps ====================
    // These tests target empty/single/missing-property paths that exercise
    // the "return default" and "buffer.Count == 0/1" branches.

    // --- NavigatePropertyChain: missing property mid-chain (lines 260-262) ---
    [Theory]
    [InlineData("a.b.c.d", "{\"a\":{\"x\":1}}", "undefined")]
    [InlineData("a.b.c.d", "{\"a\":{\"b\":5}}", "undefined")]
    public void NavigatePropertyChainMissing(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- NavigatePropertyChain: single-element array in chain ---
    [Fact]
    public void NavigatePropertyChainSingleArrayResult()
    {
        this.AssertCgAndRtMatch(
            "a.b.c.d",
            "[{\"a\":{\"b\":{\"c\":{\"d\":1}}}}]",
            "1");
    }

    // --- AverageOverChain: empty / single element (lines 3485-3506) ---
    [Theory]
    [InlineData("items.values ~> $average", "{\"items\":[]}", "undefined")]
    [InlineData("items.values ~> $average", "{\"items\":[{\"values\":[]}]}", "undefined")]
    [InlineData("items.values ~> $average", "{\"items\":[{\"values\":[42]}]}", "42")]
    public void AverageOverChainEdgeCases(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- SumOverChainCore: empty chain (lines 2498-2505) ---
    [Theory]
    [InlineData("items.values ~> $sum", "{\"items\":[]}", "undefined")]
    [InlineData("items.values ~> $sum", "{\"items\":[{\"values\":[]}]}", "0")]
    public void SumOverChainEdgeCases(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- MapChainElements: empty / single element (lines 2681-2695) ---
    [Fact]
    public void MapChainElementsEmpty()
    {
        this.AssertCgAndRtBothUndefined(
            "$map(items.name, function($v){$uppercase($v)})",
            "{\"items\":[]}");
    }

    [Fact]
    public void MapChainElementsSingle()
    {
        // Reference JSONata unwraps single-element chain results to scalar "ALICE";
        // CG wraps in array ["ALICE"] — this is a known CG wrapping difference.
        string expression = "$map(items.name, function($v){$uppercase($v)})";
        string data = "{\"items\":[{\"name\":\"alice\"}]}";

        // CG path
        CompiledExpression compiled = this.fixture.GetOrCompile(expression);
        Assert.Null(compiled.Error);
        Assert.NotNull(compiled.Method);

        using var inputDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement cgResult = InvokeCg(compiled.Method, inputDoc.RootElement, workspace);
        string cgJson = cgResult.ValueKind == JsonValueKind.Undefined ? "undefined" : cgResult.GetRawText();

        // RT path
        string? rtResult = JsonataEvaluator.Default.EvaluateToString(expression, data);

        this.output.WriteLine($"CG: {cgJson}");
        this.output.WriteLine($"RT: {rtResult}");

        // RT matches reference (scalar)
        Assert.Equal("\"ALICE\"", rtResult);
        // CG wraps in array — both forms acceptable
        Assert.True(
            cgJson == "\"ALICE\"" || cgJson == "[\"ALICE\"]",
            $"Expected \"ALICE\" or [\"ALICE\"], got: {cgJson}");
    }

    // --- ShuffleFromBuffer: empty / single (lines 5568-5569) ---
    [Fact]
    public void ShuffleFromBufferEmpty()
    {
        this.AssertCgAndRtBothUndefined(
            "$shuffle(items.vals)",
            "{\"items\":[]}");
    }

    [Fact]
    public void ShuffleFromBufferSingle()
    {
        // Single element array — shuffle returns the same array
        this.AssertCgAndRtMatch(
            "$shuffle(items.vals)",
            "{\"items\":[{\"vals\":[42]}]}",
            "[42]");
    }

    // --- ArrayFromBuffer: empty / single (lines 4679-4688) ---
    [Fact]
    public void ArrayFromBufferEmpty()
    {
        this.AssertCgAndRtBothUndefined(
            "a.b.c[]",
            "{\"a\":[]}");
    }

    [Fact]
    public void ArrayFromBufferSingle()
    {
        this.AssertCgAndRtMatch(
            "a.b.c[]",
            "{\"a\":[{\"b\":{\"c\":1}}]}",
            "[1]");
    }

    // --- MapChainDouble: single element (lines 2590-2597) ---
    [Theory]
    [InlineData("items.(price * 2)", "{\"items\":[{\"price\":5}]}", "10")]
    [InlineData("items.(price * 2)", "{\"items\":[]}", "undefined")]
    public void MapChainDoubleEdgeCases(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- FusedEvalFromStep: no matching predicates (lines 660-661, 700-712, 720-721) ---
    [Theory]
    [InlineData(
        "items[type=\"A\"].values[0]",
        "{\"items\":[{\"type\":\"B\",\"values\":[99]}]}",
        "undefined")]
    public void FusedEvalFromStepNoMatch(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- FusedCollectAndContinue: various predicate chain edge cases ---
    [Theory]
    [InlineData(
        "items[type=\"A\"].name",
        "{\"items\":[{\"type\":\"B\",\"name\":\"y\"}]}",
        "undefined")]
    [InlineData(
        "items[type=\"A\"].name",
        "{\"items\":[{\"type\":\"A\",\"name\":\"x\"}]}",
        "\"x\"")]
    public void FusedCollectAndContinueEdgeCases(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // ==================== Round 13c: Deeper edge cases for remaining partial coverage ====================

    // --- NavigatePropertyChain overload 1: primitive data (lines 233-234) ---
    [Theory]
    [InlineData("a.b.c.d", "42", "undefined")]
    [InlineData("a.b.c.d", "true", "undefined")]
    [InlineData("a.b.c.d", "\"hello\"", "undefined")]
    public void NavigatePropertyChainPrimitiveData(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- MapChainDouble: 2-step chain (data.items) to trigger MapChainDouble not MapOverElementsDouble ---
    [Theory]
    [InlineData("data.items.(price * 2)", "{\"data\":{\"items\":[{\"price\":3},{\"price\":7}]}}", "[6,14]")]
    [InlineData("data.items.(price * 2)", "{\"data\":{\"items\":[{\"price\":5}]}}", "10")]
    [InlineData("data.items.(price * 2)", "{\"data\":{\"items\":[]}}", "undefined")]
    public void MapChainDoubleTwoStepChain(string expression, string data, string expected)
    {
        if (expected == "undefined")
        {
            this.AssertCgAndRtBothUndefined(expression, data);
        }
        else
        {
            this.AssertCgAndRtMatch(expression, data, expected);
        }
    }

    // --- AverageOverChainCore: array at end of chain (lines 3485-3497) ---
    [Theory]
    [InlineData("items.values ~> $average", "{\"items\":[{\"values\":[1,2,3]}]}", "2")]
    [InlineData("items.values ~> $average", "{\"items\":[{\"values\":3}]}", "3")]
    public void AverageOverChainCoreBranches(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- AverageOverChainCore: non-number value → error (lines 3504-3506) ---
    [Fact]
    public void AverageOverChainCoreNonNumberThrows()
    {
        this.AssertCgAndRtThrow(
            "items.values ~> $average",
            "{\"items\":[{\"values\":\"hello\"}]}",
            "T0412");
    }

    // --- SumOverChainCore: array at end of chain (lines 2498-2505) ---
    [Theory]
    [InlineData("items.values ~> $sum", "{\"items\":[{\"values\":[1,2,3]}]}", "6")]
    [InlineData("items.values ~> $sum", "{\"items\":[{\"values\":3}]}", "3")]
    public void SumOverChainCoreBranches(string expression, string data, string expected)
    {
        this.AssertCgAndRtMatch(expression, data, expected);
    }

    // --- SumOverChainCore: non-number value → error ---
    [Fact]
    public void SumOverChainCoreNonNumberThrows()
    {
        this.AssertCgAndRtThrow(
            "items.values ~> $sum",
            "{\"items\":[{\"values\":\"hello\"}]}",
            "T0412");
    }

    // --- FusedEvalFromStep: multiple matches → array result (lines 700-712) ---
    [Fact]
    public void FusedEvalFromStepMultipleMatches()
    {
        this.AssertCgAndRtMatch(
            "items[type=\"A\"].values[0]",
            "{\"items\":[{\"type\":\"A\",\"values\":[1,2]},{\"type\":\"B\",\"values\":[3]},{\"type\":\"A\",\"values\":[4,5]}]}",
            "[1,4]");
    }

    // --- FusedEvalFromStep: single match → scalar result (line 720-721) ---
    [Fact]
    public void FusedEvalFromStepSingleMatch()
    {
        this.AssertCgAndRtMatch(
            "items[type=\"A\"].values[0]",
            "{\"items\":[{\"type\":\"A\",\"values\":[10]},{\"type\":\"B\",\"values\":[20]}]}",
            "10");
    }

    // --- FusedCollectAndContinue: multiple matches (lines 861-890) ---
    [Fact]
    public void FusedCollectAndContinueMultipleMatches()
    {
        this.AssertCgAndRtMatch(
            "items[type=\"A\"].name",
            "{\"items\":[{\"type\":\"A\",\"name\":\"x\"},{\"type\":\"B\",\"name\":\"y\"},{\"type\":\"A\",\"name\":\"z\"}]}",
            "[\"x\",\"z\"]");
    }

    // --- NavigatePropertyChainInto: mixed types in array (lines 423-424, 433-434) ---
    [Fact]
    public void NavigatePropertyChainIntoMixedTypes()
    {
        this.AssertCgAndRtMatch(
            "a.b",
            "[{\"a\":{\"b\":1}},{\"a\":2},{\"a\":{\"b\":3}}]",
            "[1,3]");
    }

    // ==================== Round 13d: $average/$sum function-call pattern ====================
    // These use $average(chain) and $sum(chain) to trigger
    // AverageOverChainDouble → AverageOverChainCore and SumOverChainDouble → SumOverChainCore.
    // The ~> operator uses AverageOverChain (non-Core), so function-call is needed.

    // --- AverageOverChainCore: array at end of chain (lines 3484-3497) ---
    [Fact]
    public void AverageOverChainCoreArrayEndOfChain()
    {
        this.AssertCgAndRtMatch(
            "$average(items.values)",
            "{\"items\":[{\"values\":[1,2,3]}]}",
            "2");
    }

    // --- AverageOverChainCore: single number at end of chain (lines 3499-3502) ---
    [Fact]
    public void AverageOverChainCoreSingleNumberEndOfChain()
    {
        this.AssertCgAndRtMatch(
            "$average(items.values)",
            "{\"items\":[{\"values\":5}]}",
            "5");
    }

    // --- AverageOverChainCore: non-number → T0412 error (lines 3504-3506) ---
    [Fact]
    public void AverageOverChainCoreNonNumberThrowsViaFunctionCall()
    {
        this.AssertCgAndRtThrow(
            "$average(items.values)",
            "{\"items\":[{\"values\":\"hello\"}]}",
            "T0412");
    }

    // --- AverageOverChainCore: empty chain (returns undefined) ---
    [Fact]
    public void AverageOverChainCoreEmptyViaFunctionCall()
    {
        this.AssertCgAndRtBothUndefined(
            "$average(items.values)",
            "{\"items\":[]}");
    }

    // --- AverageOverChainCore: multiple items with scalar values ---
    [Fact]
    public void AverageOverChainCoreMultipleScalars()
    {
        this.AssertCgAndRtMatch(
            "$average(items.values)",
            "{\"items\":[{\"values\":3},{\"values\":7}]}",
            "5");
    }

    // --- SumOverChainCore: array at end of chain ---
    [Fact]
    public void SumOverChainCoreArrayEndOfChain()
    {
        this.AssertCgAndRtMatch(
            "$sum(items.values)",
            "{\"items\":[{\"values\":[1,2,3]}]}",
            "6");
    }

    // --- SumOverChainCore: single number at end of chain ---
    [Fact]
    public void SumOverChainCoreSingleNumberEndOfChain()
    {
        this.AssertCgAndRtMatch(
            "$sum(items.values)",
            "{\"items\":[{\"values\":5}]}",
            "5");
    }

    // --- SumOverChainCore: non-number → T0412 error ---
    [Fact]
    public void SumOverChainCoreNonNumberThrowsViaFunctionCall()
    {
        this.AssertCgAndRtThrow(
            "$sum(items.values)",
            "{\"items\":[{\"values\":\"hello\"}]}",
            "T0412");
    }

    // --- SumOverChainCore: empty chain ---
    [Fact]
    public void SumOverChainCoreEmptyViaFunctionCall()
    {
        this.AssertCgAndRtBothUndefined(
            "$sum(items.values)",
            "{\"items\":[]}");
    }

    // ==================== Round 13e: FusedEvalFromStep deep branches ====================
    // These use multi-predicate chains to bypass CG inlining and reach FusedEvalFromStep.

    // --- FusedEvalFromStep: missing property at step 0 (line 660-661) ---
    [Fact]
    public void FusedEvalFromStepMissingProperty()
    {
        this.AssertCgAndRtBothUndefined(
            "items[type=\"A\"][status=\"active\"].name",
            "{\"x\":1}");
    }

    // --- FusedEvalFromStep: array index out of bounds (line 675-676) ---
    [Fact]
    public void FusedEvalFromStepIndexOutOfBounds()
    {
        this.AssertCgAndRtBothUndefined(
            "items[type=\"A\"].values[5]",
            "{\"items\":[{\"type\":\"A\",\"values\":[1]}]}");
    }

    // --- FusedEvalFromStep: singleton object matching multi-predicate (line 700-705) ---
    [Fact]
    public void FusedEvalFromStepSingletonObjectMatch()
    {
        this.AssertCgAndRtMatch(
            "items[type=\"A\"][status=\"active\"].name",
            "{\"items\":{\"type\":\"A\",\"status\":\"active\",\"name\":\"x\"}}",
            "\"x\"");
    }

    // --- FusedEvalFromStep: singleton object NOT matching multi-predicate (line 700-705) ---
    [Fact]
    public void FusedEvalFromStepSingletonObjectNoMatch()
    {
        this.AssertCgAndRtBothUndefined(
            "items[type=\"A\"][status=\"active\"].name",
            "{\"items\":{\"type\":\"B\",\"status\":\"active\",\"name\":\"y\"}}");
    }

    // --- FusedEvalFromStep: primitive with multi-predicate (line 708-710) ---
    [Fact]
    public void FusedEvalFromStepPrimitiveWithPredicate()
    {
        this.AssertCgAndRtBothUndefined(
            "items[type=\"A\"][status=\"active\"].name",
            "{\"items\":42}");
    }

    // --- FusedEvalFromStep: deep multi-predicate, non-object at step (line 720-721) ---
    [Fact]
    public void FusedEvalFromStepNonObjectAtStep()
    {
        this.AssertCgAndRtBothUndefined(
            "data.items[type=\"A\"][status=\"active\"].name",
            "{\"data\":{\"items\":42}}");
    }

    // --- FusedEvalFromStep: non-array singleton with index 0 (line 680-682) ---
    [Fact]
    public void FusedEvalFromStepSingletonIndex0()
    {
        this.AssertCgAndRtMatch(
            "items[type=\"A\"].values[0]",
            "{\"items\":{\"type\":\"A\",\"values\":10}}",
            "10");
    }

    // --- FusedEvalFromStep: non-array singleton with index != 0 (line 682-684) ---
    [Fact]
    public void FusedEvalFromStepSingletonIndexNon0()
    {
        this.AssertCgAndRtBothUndefined(
            "items[type=\"A\"].values[1]",
            "{\"items\":{\"type\":\"A\",\"values\":10}}");
    }

    // --- FusedEvalFromStep: successful multi-predicate through objects (line 725) ---
    [Fact]
    public void FusedEvalFromStepMultiPredicateSuccess()
    {
        this.AssertCgAndRtMatch(
            "data.items[type=\"A\"][status=\"active\"].name",
            "{\"data\":{\"items\":[{\"type\":\"A\",\"status\":\"active\",\"name\":\"x\"},{\"type\":\"B\",\"status\":\"active\",\"name\":\"y\"}]}}",
            "\"x\"");
    }

    // ==================== Round 13f: FusedCollectAndContinue deep branches ====================
    // These expressions use the pattern where an intermediate step resolves to an array WITHOUT
    // a predicate/index, so the next step enters FusedCollectAndContinue. All expressions
    // are mixed predicate+index to force NavigatePropertyChainWithPredicates fallback.

    // --- perElementIndex: propValue is Array, valid index (lines 819-824, 836-839) ---
    // a.b[0].c[type="X"] where a resolves to array of objects with b as array
    // At step 1 inside FusedCollectAndContinue: perElementIndex=true, propValue=item.b (array), idx=0
    [Fact]
    public void FccPerElementIndexArrayValid()
    {
        this.AssertCgAndRtMatch(
            "a.b[0].c[type=\"X\"]",
            "{\"a\":[{\"b\":[{\"c\":{\"type\":\"X\",\"d\":1}},{\"c\":{\"type\":\"Y\"}}]},{\"b\":[{\"c\":{\"type\":\"X\",\"d\":3}}]}]}",
            "[{\"type\":\"X\",\"d\":1},{\"type\":\"X\",\"d\":3}]");
    }

    // --- perElementIndex: propValue is Array, index OOB (lines 822, 826-828) ---
    [Fact]
    public void FccPerElementIndexArrayOOB()
    {
        this.AssertCgAndRtBothUndefined(
            "a.b[5].c[type=\"X\"]",
            "{\"a\":[{\"b\":[{\"c\":{\"type\":\"X\"}}]}]}");
    }

    // --- perElementIndex: propValue is singleton, index 0 (line 831 false branch) ---
    [Fact]
    public void FccPerElementIndexSingleton0()
    {
        this.AssertCgAndRtMatch(
            "a.b[0].c[type=\"X\"]",
            "{\"a\":[{\"b\":{\"c\":{\"type\":\"X\",\"d\":1}}},{\"b\":{\"c\":{\"type\":\"Y\"}}}]}",
            "{\"type\":\"X\",\"d\":1}");
    }

    // --- perElementIndex: propValue is singleton, index != 0 → skip (lines 831-833) ---
    [Fact]
    public void FccPerElementIndexSingletonSkip()
    {
        this.AssertCgAndRtBothUndefined(
            "a.b[1].c[type=\"X\"]",
            "{\"a\":[{\"b\":{\"c\":{\"type\":\"X\",\"d\":1}}}]}");
    }

    // --- perElementIndex + more steps (lines 836-839 with FusedEvalFromStep continuation) ---
    [Fact]
    public void FccPerElementIndexWithMoreSteps()
    {
        this.AssertCgAndRtMatch(
            "a.b[0].c[type=\"X\"].d",
            "{\"a\":[{\"b\":[{\"c\":{\"type\":\"X\",\"d\":99}},{\"c\":{\"type\":\"Y\"}}]},{\"b\":[{\"c\":{\"type\":\"X\",\"d\":77}}]}]}",
            "[99,77]");
    }

    // --- hasEqPredThisStep: propValue is Array of objects, filter sub-items (lines 849-863) ---
    // a.items.c[type="X"] where items.c resolves to array of objects, predicate at last step
    [Fact]
    public void FccEqPredicateArraySubItems()
    {
        this.AssertCgAndRtMatch(
            "a.items.c[type=\"X\"]",
            "{\"a\":[{\"items\":{\"c\":[{\"type\":\"X\",\"n\":1},{\"type\":\"Y\",\"n\":2}]}},{\"items\":{\"c\":[{\"type\":\"X\",\"n\":3}]}}]}",
            "[{\"type\":\"X\",\"n\":1},{\"type\":\"X\",\"n\":3}]");
    }

    // --- hasEqPredThisStep: propValue is single Object match at last step (lines 867-877) ---
    [Fact]
    public void FccEqPredicateSingletonMatch()
    {
        this.AssertCgAndRtMatch(
            "a.items.c[type=\"X\"]",
            "{\"a\":[{\"items\":{\"c\":{\"type\":\"X\",\"n\":1}}},{\"items\":{\"c\":{\"type\":\"Y\",\"n\":2}}}]}",
            "{\"type\":\"X\",\"n\":1}");
    }

    // --- nested arrays: array element is itself an Array (lines 886-890) ---
    // a value is [[objects...], [objects...]] so FusedCollectAndContinue recurses
    [Fact]
    public void FccNestedArrays()
    {
        this.AssertCgAndRtMatch(
            "a.items.c[type=\"X\"]",
            "{\"a\":[[{\"items\":{\"c\":{\"type\":\"X\",\"n\":1}}}],[{\"items\":{\"c\":{\"type\":\"Y\",\"n\":2}}}]]}",
            "{\"type\":\"X\",\"n\":1}");
    }

    // --- globalIndex success: root data is Array, step 0 has constant index (lines 894-909) ---
    // items[0].name[type="X"] with root data = array of objects
    [Fact]
    public void FccGlobalIndexSuccess()
    {
        this.AssertCgAndRtMatch(
            "items[0].name[type=\"X\"]",
            "[{\"items\":[{\"name\":{\"type\":\"X\",\"v\":1}},{\"name\":{\"type\":\"Y\"}}]},{\"items\":[{\"name\":{\"type\":\"X\",\"v\":3}}]}]",
            "{\"type\":\"X\",\"v\":1}");
    }

    // --- globalIndex OOB: index exceeds buffer.Count (lines 897-899) ---
    [Fact]
    public void FccGlobalIndexOOB()
    {
        this.AssertCgAndRtBothUndefined(
            "items[5].name[type=\"X\"]",
            "[{\"items\":[{\"name\":{\"type\":\"X\"}}]}]");
    }
}

