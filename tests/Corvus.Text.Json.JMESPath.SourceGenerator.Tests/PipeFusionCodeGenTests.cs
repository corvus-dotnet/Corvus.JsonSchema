// <copyright file="PipeFusionCodeGenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.JMESPath.SourceGenerator.Tests;

/// <summary>
/// Tests that verify correctness of source-generated code for fused pipe optimizations.
/// Each test exercises a specific fusion pattern through the code-generation path.
/// </summary>
public class PipeFusionCodeGenTests
{
    // Items: objects with n (name) and v (value) fields
    private const string ItemsJson = """[{"n":"A","v":3},{"n":"B","v":1},{"n":"C","v":2}]""";

    // Flat array of numbers
    private const string NumbersJson = """[5,1,3,4,2]""";

    // Nested arrays of numbers
    private const string NestedJson = """[[5,1],[3,4],[2]]""";

    // Nested arrays of objects
    private const string NestedObjectsJson = """[[{"x":1},{"x":2}],[{"x":3}]]""";

    // === Two streaming stages ===

    [Fact]
    public void FilterProject()
    {
        // [?v > `1`] | [*].n → filter v>1 keeps A(3),C(2), then project .n
        AssertCG(FilterProjectCG.Evaluate, ItemsJson, """["A","C"]""");
    }

    [Fact]
    public void FlattenFilter()
    {
        // [] | [?@ > `2`] → flatten to [5,1,3,4,2], filter >2
        AssertCG(FlattenFilterCG.Evaluate, NestedJson, """[5,3,4]""");
    }

    [Fact]
    public void FlattenProject()
    {
        // [] | [*].x → flatten to [{x:1},{x:2},{x:3}], project .x
        AssertCG(FlattenProjectCG.Evaluate, NestedObjectsJson, """[1,2,3]""");
    }

    [Fact]
    public void ProjectFilter()
    {
        // [*].v | [?@ > `1`] → project .v to [3,1,2], filter >1
        AssertCG(ProjectFilterCG.Evaluate, ItemsJson, """[3,2]""");
    }

    // === Streaming + Barrier ===

    [Fact]
    public void FilterSortBy()
    {
        // [?v > `1`] | sort_by(@, &v) → filter keeps A(3),C(2), sort by v → C(2),A(3)
        AssertCG(FilterSortByCG.Evaluate, ItemsJson, """[{"n":"C","v":2},{"n":"A","v":3}]""");
    }

    [Fact]
    public void ProjectSort()
    {
        // [*].v | sort(@) → [3,1,2] → [1,2,3]
        AssertCG(ProjectSortCG.Evaluate, ItemsJson, """[1,2,3]""");
    }

    [Fact]
    public void ProjectReverse()
    {
        // [*].v | reverse(@) → [3,1,2] → [2,1,3]
        AssertCG(ProjectReverseCG.Evaluate, ItemsJson, """[2,1,3]""");
    }

    [Fact]
    public void FlattenSort()
    {
        // [] | sort(@) → [5,1,3,4,2] → [1,2,3,4,5]
        AssertCG(FlattenSortCG.Evaluate, NestedJson, """[1,2,3,4,5]""");
    }

    // === Barrier + Streaming ===

    [Fact]
    public void SortByProject()
    {
        // sort_by(@, &v) | [*].n → sort by v: B(1),C(2),A(3), then project .n
        AssertCG(SortByProjectCG.Evaluate, ItemsJson, """["B","C","A"]""");
    }

    [Fact]
    public void SortFilter()
    {
        // sort(@) | [?@ > `2`] → [1,2,3,4,5] → [3,4,5]
        AssertCG(SortFilterCG.Evaluate, NumbersJson, """[3,4,5]""");
    }

    [Fact]
    public void ReverseProject()
    {
        // reverse(@) | [*].n → [C,B,A] → ["C","B","A"]
        AssertCG(ReverseProjectCG.Evaluate, ItemsJson, """["C","B","A"]""");
    }

    // === Barrier + Barrier ===

    [Fact]
    public void SortReverse()
    {
        // sort(@) | reverse(@) → [1,2,3,4,5] → [5,4,3,2,1]
        AssertCG(SortReverseCG.Evaluate, NumbersJson, """[5,4,3,2,1]""");
    }

    [Fact]
    public void SortByReverse()
    {
        // sort_by(@, &v) | reverse(@) → B(1),C(2),A(3) → A(3),C(2),B(1)
        AssertCG(SortByReverseCG.Evaluate, ItemsJson, """[{"n":"A","v":3},{"n":"C","v":2},{"n":"B","v":1}]""");
    }

    // === Three+ stages ===

    [Fact]
    public void FilterSortByHashProject()
    {
        // [?v > `1`] | sort_by(@, &v) | [*].{name: n, value: v}
        // filter: A(3),C(2) → sort: C(2),A(3) → hash: [{name:C,value:2},{name:A,value:3}]
        AssertCG(FilterSortByHashProjectCG.Evaluate, ItemsJson, """[{"name":"C","value":2},{"name":"A","value":3}]""");
    }

    [Fact]
    public void ProjectSortReverse()
    {
        // [*].v | sort(@) | reverse(@) → [3,1,2] → [1,2,3] → [3,2,1]
        AssertCG(ProjectSortReverseCG.Evaluate, ItemsJson, """[3,2,1]""");
    }

    [Fact]
    public void FlattenFilterSort()
    {
        // [] | [?@ > `2`] | sort(@) → [5,1,3,4,2] → [5,3,4] → [3,4,5]
        AssertCG(FlattenFilterSortCG.Evaluate, NestedJson, """[3,4,5]""");
    }

    [Fact]
    public void FilterReverseProject()
    {
        // [?v > `1`] | reverse(@) | [*].n → A(3),C(2) → C(2),A(3) → ["C","A"]
        AssertCG(FilterReverseProjectCG.Evaluate, ItemsJson, """["C","A"]""");
    }

    [Fact]
    public void SortByReverseHashProject()
    {
        // sort_by(@, &v) | reverse(@) | [*].{name: n}
        // B(1),C(2),A(3) → A(3),C(2),B(1) → [{name:A},{name:C},{name:B}]
        AssertCG(SortByReverseHashProjectCG.Evaluate, ItemsJson, """[{"name":"A"},{"name":"C"},{"name":"B"}]""");
    }

    // === Terminal HashProject ===

    [Fact]
    public void SortByHashProject()
    {
        // sort_by(@, &v) | [*].{name: n, val: v}
        // B(1),C(2),A(3) → [{name:B,val:1},{name:C,val:2},{name:A,val:3}]
        AssertCG(SortByHashProjectCG.Evaluate, ItemsJson, """[{"name":"B","val":1},{"name":"C","val":2},{"name":"A","val":3}]""");
    }

    [Fact]
    public void ReverseHashProject()
    {
        // reverse(@) | [*].{name: n} → [C,B,A] → [{name:C},{name:B},{name:A}]
        AssertCG(ReverseHashProjectCG.Evaluate, ItemsJson, """[{"name":"C"},{"name":"B"},{"name":"A"}]""");
    }

    [Fact]
    public void FilterHashProject()
    {
        // [?v > `1`] | [*].{name: n, val: v} → A(3),C(2) → [{name:A,val:3},{name:C,val:2}]
        AssertCG(FilterHashProjectCG.Evaluate, ItemsJson, """[{"name":"A","val":3},{"name":"C","val":2}]""");
    }

    // === Non-terminal HashProject ===

    [Fact]
    public void HashProjectReverse()
    {
        // [*].{name: n, val: v} | reverse(@) → [{name:A,val:3},{name:B,val:1},{name:C,val:2}] → reversed
        AssertCG(HashProjectReverseCG.Evaluate, ItemsJson, """[{"name":"C","val":2},{"name":"B","val":1},{"name":"A","val":3}]""");
    }

    // === MapExpr ===

    [Fact]
    public void MapSort()
    {
        // map(&v, @) | sort(@) → [3,1,2] → [1,2,3]
        AssertCG(MapSortCG.Evaluate, ItemsJson, """[1,2,3]""");
    }

    [Fact]
    public void MapFilter()
    {
        // map(&v, @) | [?@ > `1`] → [3,1,2] → [3,2]
        AssertCG(MapFilterCG.Evaluate, ItemsJson, """[3,2]""");
    }

    // === Edge cases ===

    [Fact]
    public void EmptyThroughPipeline()
    {
        // [?v > `100`] | sort_by(@, &v) | [*].n → [] through entire pipeline
        AssertCG(EmptyThroughPipelineCG.Evaluate, ItemsJson, """[]""");
    }

    [Fact]
    public void SingleThroughPipeline()
    {
        // [?v > `2`] | sort_by(@, &v) | [*].n → single element [A(3)] through pipeline
        AssertCG(SingleThroughPipelineCG.Evaluate, ItemsJson, """["A"]""");
    }

    // === Bail-out / Recursive fusion ===

    [Fact]
    public void RecursiveInnerFusion()
    {
        // [*].v | sort(@) | [-1] → outer pipe bails (IndexNode), inner [*].v | sort(@) fuses
        AssertCG(RecursiveInnerFusionCG.Evaluate, ItemsJson, """3""");
    }

    private delegate Corvus.Text.Json.JsonElement CgEvaluate(in Corvus.Text.Json.JsonElement data, JsonWorkspace workspace);

    private static void AssertCG(CgEvaluate evaluate, string json, string expectedJson)
    {
        using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc = ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement root = doc.RootElement;
        Corvus.Text.Json.JsonElement result = evaluate(in root, workspace);

        string resultJson = result.IsUndefined() ? "null" : result.GetRawText();
        AssertJsonEqual(expectedJson, resultJson);
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using JsonDocument expectedDoc = JsonDocument.Parse(expected);
        using JsonDocument actualDoc = JsonDocument.Parse(actual);
        Assert.True(
            JsonElementDeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"Expected: {expected}\nActual: {actual}");
    }

    private static bool JsonElementDeepEquals(System.Text.Json.JsonElement a, System.Text.Json.JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null:
            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
            case System.Text.Json.JsonValueKind.Undefined:
                return true;
            case System.Text.Json.JsonValueKind.Number:
                return a.GetDecimal() == b.GetDecimal();
            case System.Text.Json.JsonValueKind.String:
                return a.GetString() == b.GetString();
            case System.Text.Json.JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                {
                    return false;
                }

                for (int i = 0; i < a.GetArrayLength(); i++)
                {
                    if (!JsonElementDeepEquals(a[i], b[i]))
                    {
                        return false;
                    }
                }

                return true;
            case System.Text.Json.JsonValueKind.Object:
                var aProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (JsonProperty prop in a.EnumerateObject())
                {
                    aProps[prop.Name] = prop.Value;
                }

                var bProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (JsonProperty prop in b.EnumerateObject())
                {
                    bProps[prop.Name] = prop.Value;
                }

                if (aProps.Count != bProps.Count)
                {
                    return false;
                }

                foreach (var kvp in aProps)
                {
                    if (!bProps.TryGetValue(kvp.Key, out System.Text.Json.JsonElement bVal) ||
                        !JsonElementDeepEquals(kvp.Value, bVal))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return false;
        }
    }
}
