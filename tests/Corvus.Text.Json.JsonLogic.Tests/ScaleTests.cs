// <copyright file="ScaleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Scale and deep-nesting tests for the JsonLogic evaluator.
/// These exercise large data sets and deeply recursive rule structures.
/// </summary>
public class ScaleTests
{
    // ─── DEEPLY NESTED RULES ────────────────────────────────────────

    [Fact]
    public void DeeplyNestedIfElse_100Depth()
    {
        // Build: {"if": [{"==":[{"var":""},100]}, 100, {"if": [{"==":[{"var":""},99]}, 99, ... {"if":[{"==":[{"var":""},1]}, 1, 0]}]}]}
        const int depth = 100;
        StringBuilder sb = new();

        for (int i = depth; i >= 1; i--)
        {
            sb.Insert(0, $$"""{"if":[{"==":[{"var":""},{{i}}]},{{i}},""");
            sb.Append("]}");
        }

        // Innermost default is 0
        sb.Insert(
            sb.ToString().LastIndexOf(",") + 1,
            string.Empty);

        // Actually we need the innermost else to be 0 when no match.
        // Rebuild more carefully:
        sb.Clear();
        BuildNestedIf(sb, depth, 1);

        string rule = sb.ToString();
        string data = "50";

        string result = Evaluate(rule, data, maxDepth: 512);
        Assert.Equal("50", result);
    }

    [Fact]
    public void DeeplyNestedAndRules_100Depth()
    {
        const int depth = 100;
        StringBuilder sb = new();

        // Build from inside out: {"and":[true,{"and":[true,...true]}]}
        sb.Append("true");
        for (int i = 0; i < depth; i++)
        {
            sb.Insert(0, """{"and":[true,""");
            sb.Append("]}");
        }

        string rule = sb.ToString();
        string data = "null";

        string result = Evaluate(rule, data, maxDepth: 512);
        Assert.Equal("true", result);
    }

    [Fact]
    public void DeeplyNestedVarNavigation_50Depth()
    {
        const int depth = 50;

        // Build data: {"a":{"a":{"a":...{"a":42}...}}}
        StringBuilder dataSb = new();
        for (int i = 0; i < depth; i++)
        {
            dataSb.Append("""{"a":""");
        }

        dataSb.Append("42");

        for (int i = 0; i < depth; i++)
        {
            dataSb.Append('}');
        }

        // Build rule: {"var":"a.a.a...a"} with 50 segments
        string varPath = string.Join(".", Enumerable.Repeat("a", depth));
        string rule = $$"""{"var":"{{varPath}}"}""";

        string result = Evaluate(rule, dataSb.ToString());
        Assert.Equal("42", result);
    }

    // ─── LARGE ARRAY OPERATIONS ─────────────────────────────────────

    [Fact]
    public void MapOver10KItems()
    {
        const int count = 10_000;
        string data = BuildArrayData("items", count);
        string rule = """{"map":[{"var":"items"},{"*":[{"var":""},2]}]}""";

        string result = Evaluate(rule, data);

        // Parse result to verify
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        System.Text.Json.JsonElement root = doc.RootElement;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(count, root.GetArrayLength());

        // First element: 1*2=2, last element: 10000*2=20000
        Assert.Equal(2, root[0].GetInt32());
        Assert.Equal(20000, root[count - 1].GetInt32());
    }

    [Fact]
    public void FilterOver10KItems()
    {
        const int count = 10_000;
        string data = BuildArrayData("items", count);
        string rule = """{"filter":[{"var":"items"},{">":[{"var":""},5000]}]}""";

        string result = Evaluate(rule, data);

        using var doc = System.Text.Json.JsonDocument.Parse(result);
        System.Text.Json.JsonElement root = doc.RootElement;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(5000, root.GetArrayLength());
    }

    [Fact]
    public void ReduceOver10KItems()
    {
        const int count = 10_000;
        string data = BuildArrayData("items", count);
        string rule = """{"reduce":[{"var":"items"},{"+":[{"var":"current"},{"var":"accumulator"}]},0]}""";

        string result = Evaluate(rule, data);

        // Sum of 1..10000 = 10000 * 10001 / 2 = 50005000
        Assert.Equal("50005000", result);
    }

    // ─── HELPERS ────────────────────────────────────────────────────

    private static void BuildNestedIf(StringBuilder sb, int max, int current)
    {
        if (current > max)
        {
            sb.Append('0');
            return;
        }

        sb.Append($$"""{"if":[{"==":[{"var":""},{{current}}]},{{current}},""");
        BuildNestedIf(sb, max, current + 1);
        sb.Append("]}");
    }

    private static string BuildArrayData(string propertyName, int count)
    {
        StringBuilder sb = new();
        sb.Append($$"""{"{{propertyName}}":[""");

        for (int i = 1; i <= count; i++)
        {
            if (i > 1)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string Evaluate(string rule, string data, int maxDepth = 0)
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        JsonDocumentOptions options = maxDepth > 0
            ? new JsonDocumentOptions { MaxDepth = maxDepth }
            : default;

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8, options);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8, options);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement, workspace);

        return NormalizeJson(GetRawText(result));
    }

    private static string GetRawText(Corvus.Text.Json.JsonElement element)
    {
        if (element.IsNullOrUndefined())
        {
            return "null";
        }

        return element.GetRawText();
    }

    private static string NormalizeJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false }))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
