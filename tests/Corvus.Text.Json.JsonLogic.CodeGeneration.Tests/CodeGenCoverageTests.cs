// <copyright file="CodeGenCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.JsonLogic;
using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Coverage tests that generate → compile → execute JsonLogic CG code and
/// compare results with the RT engine, targeting specific uncovered lines in
/// <see cref="JsonLogicCodeGenerator"/> and <see cref="JlopsParser"/>.
/// </summary>
public class CodeGenCoverageTests : IClassFixture<CodeGenConformanceFixture>
{
    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenCoverageTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    // ---- Modulo operator ----
    // Covers the % branch in arithmetic code generation

    [Theory]
    [InlineData("""{"%" : [5, 3]}""", "null", "2")]
    [InlineData("""{"%" : [10, 4]}""", "null", "2")]
    [InlineData("""{"%" : [7.5, 2.5]}""", "null", "0")]
    public void Modulo_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Subtraction operator ----
    // Ensures arithmetic minus path is covered

    [Theory]
    [InlineData("""{"-" : [10, 3]}""", "null", "7")]
    [InlineData("""{"-" : [5]}""", "null", "-5")]
    public void Subtract_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Division operator ----

    [Theory]
    [InlineData("""{"/" : [10, 3]}""", "null")]
    [InlineData("""{"/" : [7, 2]}""", "null")]
    public void Division_CG_MatchesRT(string rule, string data)
    {
        AssertCGMatchesRT(rule, data);
    }

    // ---- Nested arithmetic with var ----

    [Theory]
    [InlineData("""{"+" : [1, {"*" : [2, {"var" : "x"}]}, 3]}""", """{"x":5}""", "14")]
    [InlineData("""{"*" : [{"var":"a"}, {"+" : [{"var":"b"}, 1]}]}""", """{"a":3,"b":4}""", "15")]
    public void NestedArithmetic_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Reduce with scope-creating operator in body ----
    // Covers IsArithmeticOp empty-object fallback (lines 346-359)
    // and BodyUsesOnlyReduceVars scope-creating check (lines 400-402)

    [Theory]
    [InlineData("""{"reduce" : [[1,2,3], {"+" : [{"var":"current"}, {"var":"accumulator"}]}, 0]}""", "null", "6")]
    [InlineData("""{"reduce" : [[2,3,4], {"*" : [{"var":"current"}, {"var":"accumulator"}]}, 1]}""", "null", "24")]
    public void Reduce_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Map and filter ----

    [Theory]
    [InlineData("""{"map" : [{"var":"items"}, {"*" : [{"var":""}, 2]}]}""", """{"items":[1,2,3]}""", "[2,4,6]")]
    [InlineData("""{"filter" : [{"var":"items"}, {">" : [{"var":""}, 2]}]}""", """{"items":[1,2,3,4,5]}""", "[3,4,5]")]
    public void MapFilter_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Some / None / All ----

    [Theory]
    [InlineData("""{"some" : [[1,2,3], {">" : [{"var":""}, 2]}]}""", "null", "true")]
    [InlineData("""{"none" : [[1,2,3], {">" : [{"var":""}, 5]}]}""", "null", "true")]
    [InlineData("""{"all" : [[2,4,6], {"%" : [{"var":""}, 2]}]}""", "null")]
    public void SomeNoneAll_CG_MatchesRT(string rule, string data, string? expected = null)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Missing and missing_some ----

    [Theory]
    [InlineData("""{"missing" : ["a","b","c"]}""", """{"a":1,"c":3}""", "[\"b\"]")]
    [InlineData("""{"missing_some" : [1, ["a","b","c"]]}""", """{"a":1}""", "[]")]
    [InlineData("""{"missing_some" : [2, ["a","b","c"]]}""", """{"a":1}""", "[\"b\",\"c\"]")]
    public void MissingSome_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Substr ----

    [Theory]
    [InlineData("""{"substr" : ["jsonlogic", 4]}""", "null", "\"logic\"")]
    [InlineData("""{"substr" : ["jsonlogic", -5]}""", "null", "\"logic\"")]
    [InlineData("""{"substr" : ["jsonlogic", 0, 4]}""", "null", "\"json\"")]
    public void Substr_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Log ----

    [Theory]
    [InlineData("""{"log" : "hello"}""", "null", "\"hello\"")]
    public void Log_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- In (string and array) ----

    [Theory]
    [InlineData("""{"in" : ["Spring", "Springfield"]}""", "null", "true")]
    [InlineData("""{"in" : ["xyz", "Springfield"]}""", "null", "false")]
    [InlineData("""{"in" : [1, [1,2,3]]}""", "null", "true")]
    [InlineData("""{"in" : [5, [1,2,3]]}""", "null", "false")]
    public void In_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Merge ----

    [Theory]
    [InlineData("""{"merge" : [[1,2], [3,4]]}""", "null", "[1,2,3,4]")]
    [InlineData("""{"merge" : [[1], [2], [3]]}""", "null", "[1,2,3]")]
    public void Merge_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Custom operators with JlopsParser ----
    // Covers JlopsParser block form (lines 114-146), blank lines (lines 127-129),
    // and JsonLogicCodeGenerator custom operator emission (lines 214-231)

    [Fact]
    public void CustomOperator_ExpressionForm_CG_MatchesRT()
    {
        string jlops = "op double(x) => x * 2;";
        var customOps = JlopsParser.Parse(jlops);

        string rule = """{"double" : {"var":"val"}}""";

        string code = JsonLogicCodeGenerator.Generate(rule, "CustomExprRule", "Test.Generated", customOps);

        this.output.WriteLine(code);

        // Verify the generated code contains the custom operator method
        Assert.Contains("CustomOp_double", code);
    }

    [Fact]
    public void CustomOperator_BlockForm_CG_Generates()
    {
        // Covers lines 214-231 (block body emission with blank lines and dedenting)
        string jlops = """
            op clamp(value, lo, hi)
            {
                if (value < lo)
                    return lo;

                if (value > hi)
                    return hi;

                return value;
            }
            """;

        var customOps = JlopsParser.Parse(jlops);

        string rule = """{"clamp" : [{"var":"x"}, 0, 100]}""";
        string code = JsonLogicCodeGenerator.Generate(rule, "ClampRule", "Test.Generated", customOps);

        this.output.WriteLine(code);

        // Verify block body was emitted
        Assert.Contains("clamp", code);
        Assert.Contains("return", code);
    }

    // ---- JlopsParser error paths ----
    // Covers parser edge cases (lines 75-76, 81-82, 102-103, 139-146)

    [Fact]
    public void JlopsParser_MissingOperatorName_Throws()
    {
        Assert.Throws<FormatException>(() =>
            JlopsParser.Parse("op (x) => x * 2;"));
    }

    [Fact]
    public void JlopsParser_MissingClosingParen_Throws()
    {
        Assert.Throws<FormatException>(() =>
            JlopsParser.Parse("op bad(x => x;"));
    }

    [Fact]
    public void JlopsParser_EmptyExpressionBody_Throws()
    {
        Assert.Throws<FormatException>(() =>
            JlopsParser.Parse("op empty(x) => ;"));
    }

    [Fact]
    public void JlopsParser_BlockFormOnNextLine_Parses()
    {
        // Covers lines 114-117 (brace on next line)
        string jlops = """
            op add(a, b)
            {
                return a + b;
            }
            """;

        var ops = JlopsParser.Parse(jlops);
        Assert.Single(ops);
        Assert.Equal("add", ops[0].Name);
    }

    [Fact]
    public void JlopsParser_BlankLineBeforeBrace_Parses()
    {
        // Covers lines 127-129 (blank lines between signature and brace)
        string jlops = "op add(a, b)\n\n\n{\n    return a + b;\n}\n";

        var ops = JlopsParser.Parse(jlops);
        Assert.Single(ops);
        Assert.Equal("add", ops[0].Name);
    }

    [Fact]
    public void JlopsParser_UnclosedBlock_Throws()
    {
        // Covers lines 139-146 (EOF inside block)
        Assert.Throws<FormatException>(() =>
            JlopsParser.Parse("op bad(x)\n{\n    return x;"));
    }

    [Fact]
    public void JlopsParser_MultipleOperators_Parses()
    {
        string jlops = """
            // Comment line
            op add(a, b) => a + b;

            op mul(a, b) => a * b;
            """;

        var ops = JlopsParser.Parse(jlops);
        Assert.Equal(2, ops.Count);
        Assert.Equal("add", ops[0].Name);
        Assert.Equal("mul", ops[1].Name);
    }

    // ---- Round 2: Comparison chain optimization ----
    // Covers TryEmitOptimizedComparisonChain lines 1989-2053

    [Theory]
    [InlineData("""{"if":[{">":[{"var":"x"},100]},3,{">":[{"var":"x"},50]},2,{">":[{"var":"x"},10]},1,0]}""", """{"x":150}""", "3")]
    [InlineData("""{"if":[{">":[{"var":"x"},100]},3,{">":[{"var":"x"},50]},2,{">":[{"var":"x"},10]},1,0]}""", """{"x":75}""", "2")]
    [InlineData("""{"if":[{">":[{"var":"x"},100]},3,{">":[{"var":"x"},50]},2,{">":[{"var":"x"},10]},1,0]}""", """{"x":5}""", "0")]
    public void ComparisonChain_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- TryEmitPredicateAsBool via filter ----
    // Covers lines 2080-2148 (comparison as native bool) and 2624-2627

    [Theory]
    [InlineData("""{"filter":[{"var":"items"},{">":[{"var":""},5]}]}""", """{"items":[1,3,7,10,2]}""", "[7,10]")]
    [InlineData("""{"filter":[{"var":"items"},{"<=":[{"var":""},3]}]}""", """{"items":[1,3,7,10,2]}""", "[1,3,2]")]
    public void FilterWithComparison_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Negated predicate in filter ----
    // Covers TryEmitPredicateAsBool negation path lines 2151-2161

    [Theory]
    [InlineData("""{"filter":[{"var":"items"},{"!":[{">":[{"var":""},5]}]}]}""", """{"items":[1,3,7,10,2]}""", "[1,3,2]")]
    public void FilterWithNegatedComparison_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- All/some/none with comparison predicates ----
    // Covers lines 2992-3015

    [Theory]
    [InlineData("""{"all":[{"var":"items"},{">":[{"var":""},0]}]}""", """{"items":[1,2,3]}""", "true")]
    [InlineData("""{"all":[{"var":"items"},{">":[{"var":""},5]}]}""", """{"items":[1,2,3]}""", "false")]
    [InlineData("""{"some":[{"var":"items"},{">":[{"var":""},5]}]}""", """{"items":[1,3,7]}""", "true")]
    [InlineData("""{"some":[{"var":"items"},{">":[{"var":""},5]}]}""", """{"items":[1,2,3]}""", "false")]
    [InlineData("""{"none":[{"var":"items"},{">":[{"var":""},10]}]}""", """{"items":[1,3,7]}""", "true")]
    [InlineData("""{"none":[{"var":"items"},{">":[{"var":""},5]}]}""", """{"items":[1,3,7]}""", "false")]
    public void QuantifiersWithComparison_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Cat with zero args ----
    // Covers EmitCat empty branch lines 2252-2256

    [Theory]
    [InlineData("""{"cat":[]}""", "null", "\"\"")]
    public void CatEmpty_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Substr with variable start/length ----
    // Covers lines 2329-2346 (non-literal operands)

    [Theory]
    [InlineData("""{"substr":[{"var":"s"},{"var":"start"},{"var":"len"}]}""", """{"s":"hello world","start":0,"len":5}""", "\"hello\"")]
    [InlineData("""{"substr":[]}""", "null", "\"\"")]
    public void SubstrVariableArgs_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- In with insufficient args ----
    // Covers EmitIn lines 2361-2365

    [Theory]
    [InlineData("""{"in":[{"var":"x"}]}""", """{"x":"a"}""", "false")]
    public void InOneArg_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Merge with mixed operand types ----
    // Covers EmitMerge lines 2510-2560 (static array, dynamic, scalar branches)

    [Theory]
    [InlineData("""{"merge":[{"var":"a"},[1,2],{"var":"b"}]}""", """{"a":[10,20],"b":30}""")]
    [InlineData("""{"merge":[[1,2],[3,4]]}""", "null")]
    public void MergeMixed_CG_MatchesRT(string rule, string data)
    {
        AssertCGMatchesRT(rule, data);
    }

    // ---- Reduce with insufficient args ----
    // Covers EmitReduce lines 2653-2657

    [Theory]
    [InlineData("""{"reduce":[]}""", "null", "null")]
    [InlineData("""{"reduce":[{"var":"items"}]}""", """{"items":[1,2]}""", "null")]
    public void ReduceInsufficientArgs_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Fused map-reduce ----
    // Covers EmitFusedMapReduce lines 2820-2922

    [Theory]
    [InlineData("""{"reduce":[{"map":[{"var":"items"},{"*":[{"var":""},2]}]},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""", """{"items":[1,2,3]}""", "12")]
    public void FusedMapReduce_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Reduce with scope-creating body (non-optimizable) ----
    // Covers BodyUsesOnlyReduceVars returns false (line 401), slow reduce path (lines 2925-2950)

    [Theory]
    [InlineData("""{"reduce":[{"var":"items"},{"if":[{">":[{"var":"current"},{"var":"accumulator"}]},{"var":"current"},{"var":"accumulator"}]},0]}""", """{"items":[3,1,5,2,4]}""", "5")]
    public void ReduceWithConditionalBody_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Min/max with var operands (non-literal, non-deferred) ----
    // Covers min/max lines 1413-1419 (element-backed operands)

    [Theory]
    [InlineData("""{"max":[{"var":"a"},{"var":"b"},{"var":"c"}]}""", """{"a":10,"b":30,"c":20}""", "30")]
    [InlineData("""{"min":[{"var":"a"},{"var":"b"},{"var":"c"}]}""", """{"a":10,"b":30,"c":20}""", "10")]
    public void MinMaxVarOperands_CG_MatchesRT(string rule, string data, string expected)
    {
        AssertCGMatchesRT(rule, data, expected);
    }

    // ---- Filter with < 2 operands ----
    // Covers EmitFilter lines 2600-2603

    [Theory]
    [InlineData("""{"filter":[{"var":"items"}]}""", """{"items":[1,2,3]}""")]
    public void FilterOneOperand_CG_MatchesRT(string rule, string data)
    {
        AssertCGMatchesRT(rule, data);
    }

    // ---- Map with < 2 operands ----
    // Covers EmitMap lines 2557-2560

    [Theory]
    [InlineData("""{"map":[{"var":"items"}]}""", """{"items":[1,2,3]}""")]
    public void MapOneOperand_CG_MatchesRT(string rule, string data)
    {
        AssertCGMatchesRT(rule, data);
    }

    /// <summary>
    /// Generates CG code for the rule, compiles it, executes it, then compares with RT.
    /// </summary>
    private void AssertCGMatchesRT(string rule, string data, string? expectedJson = null)
    {
        // RT evaluation
        string? rtResult = JsonLogicEvaluator.Default.EvaluateToString(rule, data);
        string rtJson = rtResult ?? "null";

        this.output.WriteLine($"Rule:     {rule}");
        this.output.WriteLine($"Data:     {data}");
        this.output.WriteLine($"RT:       {rtJson}");

        // If expected is provided, verify RT matches expected first
        if (expectedJson is not null)
        {
            AssertJsonEqual(expectedJson, rtJson);
        }

        // CG evaluation
        CompiledRule compiled = this.fixture.GetOrCompile(rule);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"CG code length: {compiled.GeneratedCode.Length}");
        }

        if (compiled.Method is null)
        {
            Assert.Fail($"CG compilation failed: {compiled.Error}");
            return;
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);
        using ParsedJsonDocument<JsonElement>? dataDoc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        object?[] args = [dataDoc.RootElement, workspace];
        object? ret = compiled.Method.Invoke(null, args);
        JsonElement cgResult = ret is JsonElement el ? el : default;

        string cgJson = cgResult.IsNullOrUndefined() ? "null" : cgResult.GetRawText();
        this.output.WriteLine($"CG:       {cgJson}");

        // CG must match RT
        AssertJsonEqual(rtJson, cgJson);
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        string normExpected = NormalizeJson(expected);
        string normActual = NormalizeJson(actual);
        Assert.Equal(normExpected, normActual);
    }

    private static string NormalizeJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false }))
        {
            NormalizeElement(doc.RootElement, writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void NormalizeElement(System.Text.Json.JsonElement element, System.Text.Json.Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Number:
                double d = element.GetDouble();
                if (d == Math.Truncate(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                {
                    writer.WriteNumberValue((long)d);
                }
                else
                {
                    writer.WriteNumberValue(d);
                }

                break;

            case System.Text.Json.JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
                {
                    NormalizeElement(item, writer);
                }

                writer.WriteEndArray();
                break;

            case System.Text.Json.JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (System.Text.Json.JsonProperty prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    NormalizeElement(prop.Value, writer);
                }

                writer.WriteEndObject();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
