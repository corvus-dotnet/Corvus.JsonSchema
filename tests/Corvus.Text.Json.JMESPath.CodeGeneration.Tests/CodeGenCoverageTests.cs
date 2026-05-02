// <copyright file="CodeGenCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.JMESPath;
using Corvus.Text.Json.JMESPath.CodeGeneration;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JMESPath.CodeGeneration.Tests;

/// <summary>
/// Coverage tests that generate → compile → execute JMESPath CG code and
/// compare results with the RT engine, targeting specific uncovered lines in
/// <see cref="JMESPathCodeGenerator"/>.
/// </summary>
public class CodeGenCoverageTests : IClassFixture<CodeGenConformanceFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenCoverageTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    // ---- Flatten projections ----
    // Covers EmitFlattenProjection and EmitFusedDoubleFlatten (lines 1373-1484)

    [Theory]
    [InlineData("a[]", """{"a":[1,2,3]}""", "[1,2,3]")]
    [InlineData("a[][]", """{"a":[[1,2],[3,4]]}""", "[1,2,3,4]")]
    [InlineData("a[].b", """{"a":[{"b":1},{"b":2}]}""", "[1,2]")]
    public void Flatten_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- List projection + hash ----
    // Covers EmitFusedListProjectionHash (lines 1067-1156)

    [Theory]
    [InlineData("people[*].{name: name, age: age}", """{"people":[{"name":"Alice","age":30},{"name":"Bob","age":25}]}""")]
    [InlineData("items[*].{id: id}", """{"items":[{"id":1},{"id":2},{"id":3}]}""")]
    public void ListProjectionHash_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Projection + flatten fusion ----
    // Covers EmitFusedProjectionFlatten (lines 1283-1371)

    [Theory]
    [InlineData("data[*].items[]", """{"data":[{"items":[1,2]},{"items":[3,4]}]}""", "[1,2,3,4]")]
    [InlineData("groups[*].members[]", """{"groups":[{"members":["a","b"]},{"members":["c"]}]}""", "[\"a\",\"b\",\"c\"]")]
    public void ProjectionFlatten_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Terminal hash project (pipe with final hash) ----
    // Covers EmitTerminalHashProject (lines 698-806)

    [Theory]
    [InlineData("people[*] | [?active].{name: name}", """{"people":[{"name":"Alice","active":true},{"name":"Bob","active":false}]}""")]
    [InlineData("items[*] | [?price > `10`].{name: name, price: price}", """{"items":[{"name":"A","price":15},{"name":"B","price":5}]}""")]
    public void TerminalHashProject_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Slice expressions ----
    // Covers slice emission paths

    [Theory]
    [InlineData("[1:3]", "[0,1,2,3,4]", "[1,2]")]
    [InlineData("[-2:]", "[0,1,2,3,4]", "[3,4]")]
    [InlineData("[::2]", "[0,1,2,3,4]", "[0,2,4]")]
    [InlineData("[3:1:-1]", "[0,1,2,3,4]", "[3,2]")]
    public void Slice_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Filter projections ----
    // Covers EmitFilterProjection (lines 1486-1521)

    [Theory]
    [InlineData("items[?active]", """{"items":[{"active":true,"id":1},{"active":false,"id":2}]}""")]
    [InlineData("items[?price > `10`]", """{"items":[{"price":15},{"price":5},{"price":20}]}""")]
    public void FilterProjection_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Value projections ----

    [Theory]
    [InlineData("data.*.name", """{"data":{"a":{"name":"x"},"b":{"name":"y"}}}""")]
    public void ValueProjection_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Multi-select list ----

    [Theory]
    [InlineData("[a, b, c]", """{"a":1,"b":2,"c":3}""", "[1,2,3]")]
    public void MultiSelectList_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Multi-select hash ----

    [Theory]
    [InlineData("{x: a, y: b}", """{"a":1,"b":2}""")]
    public void MultiSelectHash_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Function calls: variadic and less-common ----
    // Covers merge, not_null, and other function paths

    [Theory]
    [InlineData("length(items)", """{"items":[1,2,3]}""", "3")]
    [InlineData("length('hello')", "null", "5")]
    public void LengthFunc_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("sort(items)", """{"items":[3,1,2]}""", "[1,2,3]")]
    [InlineData("reverse(items)", """{"items":[1,2,3]}""", "[3,2,1]")]
    public void SortReverse_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("sort_by(items, &age)", """{"items":[{"age":30,"n":"b"},{"age":20,"n":"a"}]}""")]
    [InlineData("max_by(items, &age)", """{"items":[{"age":30,"n":"b"},{"age":20,"n":"a"}]}""")]
    [InlineData("min_by(items, &age)", """{"items":[{"age":30,"n":"b"},{"age":20,"n":"a"}]}""")]
    public void SortByMaxByMinBy_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    [Theory]
    [InlineData("sum([a, b, c])", """{"a":10,"b":20,"c":30}""", "60")]
    [InlineData("sum([a, b])", """{"a":1,"b":2}""", "3")]
    public void SumInline_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("avg(items)", """{"items":[1,2,3,4,5]}""", "3")]
    [InlineData("ceil(`1.5`)", "null", "2")]
    [InlineData("floor(`1.5`)", "null", "1")]
    [InlineData("abs(`-5`)", "null", "5")]
    public void MathFuncs_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("contains('foobar', 'foo')", "null", "true")]
    [InlineData("contains(items, `2`)", """{"items":[1,2,3]}""", "true")]
    [InlineData("starts_with('foobar', 'foo')", "null", "true")]
    [InlineData("ends_with('foobar', 'bar')", "null", "true")]
    public void StringFuncs_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("join(', ', items)", """{"items":["a","b","c"]}""", "\"a, b, c\"")]
    public void JoinFunc_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("keys(data)", """{"data":{"a":1,"b":2}}""")]
    [InlineData("values(data)", """{"data":{"a":1,"b":2}}""")]
    public void KeysValues_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    [Theory]
    [InlineData("to_string(`42`)", "null", "\"42\"")]
    [InlineData("to_number('42')", "null", "42")]
    [InlineData("to_array('hello')", "null")]
    [InlineData("type('hello')", "null", "\"string\"")]
    [InlineData("type(`42`)", "null", "\"number\"")]
    public void ConversionFuncs_CG_MatchesRT(string expression, string data, string? expected = null)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("not_null(a, b, c)", """{"a":null,"b":null,"c":5}""", "5")]
    [InlineData("not_null(a, b)", """{"a":1,"b":2}""", "1")]
    public void NotNull_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Pipe expressions ----

    [Theory]
    [InlineData("items | [0]", """{"items":[10,20,30]}""", "10")]
    [InlineData("items[*].name | sort(@) | [0]", """{"items":[{"name":"c"},{"name":"a"},{"name":"b"}]}""", "\"a\"")]
    public void Pipe_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Comparison and logical operators ----

    [Theory]
    [InlineData("a == b", """{"a":1,"b":1}""", "true")]
    [InlineData("a != b", """{"a":1,"b":2}""", "true")]
    [InlineData("a < b", """{"a":1,"b":2}""", "true")]
    [InlineData("a > b", """{"a":3,"b":2}""", "true")]
    [InlineData("a <= b", """{"a":2,"b":2}""", "true")]
    [InlineData("a >= b", """{"a":2,"b":2}""", "true")]
    public void Comparison_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    [Theory]
    [InlineData("a && b", """{"a":true,"b":true}""", "true")]
    [InlineData("a || b", """{"a":false,"b":true}""", "true")]
    [InlineData("!a", """{"a":false}""", "true")]
    public void Logical_CG_MatchesRT(string expression, string data, string expected)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Literal expressions ----

    [Theory]
    [InlineData("`42`", "null", "42")]
    [InlineData("`\"hello\"`", "null", "\"hello\"")]
    [InlineData("`true`", "null", "true")]
    [InlineData("`{\"a\": 1}`", "null")]
    public void Literal_CG_MatchesRT(string expression, string data, string? expected = null)
    {
        AssertCGMatchesRT(expression, data, expected);
    }

    // ---- Round 2: Fused pipeline — sort | project ----
    // Covers EmitFusedPipeline, EmitCollectFromSource (L493-494), EmitCollectFromBuilder (L511-532)

    [Theory]
    [InlineData("sort(items) | [*].to_string(@)", """{"items":[3,1,2]}""")]
    [InlineData("sort_by(items, &age) | [*].name", """{"items":[{"name":"Bob","age":30},{"name":"Alice","age":20}]}""")]
    [InlineData("reverse(items) | [*].name", """{"items":[{"name":"a"},{"name":"b"},{"name":"c"}]}""")]
    public void PipelineSortThenProject_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — sort | terminal hash project ----
    // Covers EmitTerminalHashProject (L698-806)

    [Theory]
    [InlineData("sort(items) | [*].{v: to_string(@)}", """{"items":[3,1,2]}""")]
    [InlineData("sort_by(items, &age) | [*].{n: name, a: age}", """{"items":[{"name":"b","age":20},{"name":"a","age":10}]}""")]
    public void PipelineSortThenHash_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — filter with projection after barrier ----
    // Covers EmitStreamingStages Filter+Projection (L556-577)

    [Theory]
    [InlineData("sort(items) | [?@ > `2`].to_string(@)", """{"items":[3,1,4,1,5]}""")]
    public void PipelineBarrierThenFilterProject_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — flatten with projection after barrier ----
    // Covers EmitStreamingStages Flatten with projection (L590-616)

    [Theory]
    [InlineData("reverse(data) | [].name", """{"data":[{"name":"b"},{"name":"a"}]}""")]
    public void PipelineBarrierThenFlattenProject_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — flatten without projection after barrier ----
    // Covers EmitStreamingStages Flatten without projection (L617-634)

    [Theory]
    [InlineData("reverse(data) | []", """{"data":[[3,1],[2]]}""")]
    public void PipelineBarrierThenFlatten_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — map expression as first stage ----
    // Covers EmitStreamingStages MapExpr (L639-645)

    [Theory]
    [InlineData("map(&to_string(@), items) | sort(@) | [*].length(@)", """{"items":[100,2,30]}""")]
    public void PipelineMapThenBarrier_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Pipeline — non-terminal hash project ----
    // Covers EmitStreamingStages HashProject non-terminal (L648-653)

    [Theory]
    [InlineData("[*].{a: @} | [*].a", "[1,2,3]")]
    public void PipelineNonTerminalHash_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- Multi-stage pipeline — collect from builder ----
    // Covers EmitCollectFromBuilder (L511-532) with streaming before and after barrier

    [Theory]
    [InlineData("[*].age | sort(@) | [*].to_string(@)", """[{"age":30},{"age":10},{"age":20}]""")]
    public void PipelineMultiStageCollectFromBuilder_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    // ---- FlattenProjection with non-current right (non-identity) ----
    // Covers EmitFlattenProjection lines 1336-1361 (project each flattened element)

    [Theory]
    [InlineData("items[].address.city", """{"items":[{"address":{"city":"NYC"}},{"address":{"city":"LA"}}]}""")]
    [InlineData("data[].tags[0]", """{"data":[{"tags":["a","b"]},{"tags":["c"]}]}""")]
    public void FlattenWithPropertyAccess_CG_MatchesRT(string expression, string data)
    {
        AssertCGMatchesRT(expression, data);
    }

    /// <summary>
    /// Generates CG code for the expression, compiles it, executes it, then compares with RT.
    /// </summary>
    private void AssertCGMatchesRT(string expression, string data, string? expectedJson = null)
    {
        // RT evaluation
        JMESPathEvaluator evaluator = new();
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);
        JsonElement dataElement = JsonElement.ParseValue(dataUtf8);

        using JsonWorkspace rtWorkspace = JsonWorkspace.Create();
        JsonElement rtResult = evaluator.Search(expression, dataElement, rtWorkspace);
        string rtJson = rtResult.IsUndefined() ? "null" : rtResult.GetRawText();

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Data:       {data}");
        this.output.WriteLine($"RT:         {rtJson}");

        // If expected is provided, verify RT matches expected first
        if (expectedJson is not null)
        {
            AssertJsonEqual(expectedJson, rtJson);
        }

        // CG evaluation
        CompiledJMESPathExpression compiled = this.fixture.GetOrCompile(expression);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"CG code length: {compiled.GeneratedCode.Length}");
        }

        if (compiled.Method is null)
        {
            Assert.Fail($"CG compilation failed: {compiled.Error}");
            return;
        }

        using JsonWorkspace cgWorkspace = JsonWorkspace.Create();
        object?[] args = [dataElement, cgWorkspace];
        object? ret = compiled.Method.Invoke(null, args);
        JsonElement cgResult = ret is JsonElement el ? el : default;

        string cgJson = cgResult.IsUndefined() ? "null" : cgResult.GetRawText();
        this.output.WriteLine($"CG:         {cgJson}");

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
