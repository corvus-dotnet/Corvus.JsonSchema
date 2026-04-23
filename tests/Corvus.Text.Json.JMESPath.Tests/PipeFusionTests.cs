// <copyright file="PipeFusionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Tests that verify correctness of the runtime compiler's fused pipe optimizations.
/// The FusedPipePlanner classifies pipe stages as streaming (Filter, Project, Flatten, MapExpr)
/// or barrier (Sort, SortBy, Reverse) and fuses them into a single document materialization.
/// These tests cover each stage combination to ensure the fused path produces correct results.
/// </summary>
public class PipeFusionTests
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

    /// <summary>Filter | Project: filter then project field names.</summary>
    [Fact]
    public void FilterThenProject()
    {
        // [?v > `1`] | [*].n → filter v>1 keeps A(3),C(2), then project .n
        AssertRT(ItemsJson, "[?v > `1`] | [*].n", """["A","C"]""");
    }

    /// <summary>Flatten | Filter: flatten nested arrays then filter elements.</summary>
    [Fact]
    public void FlattenThenFilter()
    {
        // [] | [?@ > `2`] → flatten to [5,1,3,4,2], filter >2
        AssertRT(NestedJson, "[] | [?@ > `2`]", """[5,3,4]""");
    }

    /// <summary>Flatten | Project: flatten nested object arrays then project field.</summary>
    [Fact]
    public void FlattenThenProject()
    {
        // [] | [*].x → flatten to [{x:1},{x:2},{x:3}], project .x
        AssertRT(NestedObjectsJson, "[] | [*].x", """[1,2,3]""");
    }

    /// <summary>Project | Filter: project values then filter them.</summary>
    [Fact]
    public void ProjectThenFilter()
    {
        // [*].v | [?@ > `1`] → project .v to [3,1,2], filter >1
        AssertRT(ItemsJson, "[*].v | [?@ > `1`]", """[3,2]""");
    }

    // === Streaming + Barrier ===

    /// <summary>Filter | SortBy: filter then sort by field.</summary>
    [Fact]
    public void FilterThenSortBy()
    {
        // [?v > `1`] | sort_by(@, &v) → filter keeps A(3),C(2), sort by v → C(2),A(3)
        AssertRT(ItemsJson, "[?v > `1`] | sort_by(@, &v)", """[{"n":"C","v":2},{"n":"A","v":3}]""");
    }

    /// <summary>Project | Sort: project values then sort them.</summary>
    [Fact]
    public void ProjectThenSort()
    {
        // [*].v | sort(@) → [3,1,2] → [1,2,3]
        AssertRT(ItemsJson, "[*].v | sort(@)", """[1,2,3]""");
    }

    /// <summary>Project | Reverse: project values then reverse them.</summary>
    [Fact]
    public void ProjectThenReverse()
    {
        // [*].v | reverse(@) → [3,1,2] → [2,1,3]
        AssertRT(ItemsJson, "[*].v | reverse(@)", """[2,1,3]""");
    }

    /// <summary>Flatten | Sort: flatten nested arrays then sort.</summary>
    [Fact]
    public void FlattenThenSort()
    {
        // [] | sort(@) → [5,1,3,4,2] → [1,2,3,4,5]
        AssertRT(NestedJson, "[] | sort(@)", """[1,2,3,4,5]""");
    }

    // === Barrier + Streaming ===

    /// <summary>SortBy | Project: sort objects then project field names.</summary>
    [Fact]
    public void SortByThenProject()
    {
        // sort_by(@, &v) | [*].n → sort by v: B(1),C(2),A(3), then project .n
        AssertRT(ItemsJson, "sort_by(@, &v) | [*].n", """["B","C","A"]""");
    }

    /// <summary>Sort | Filter: sort numbers then filter.</summary>
    [Fact]
    public void SortThenFilter()
    {
        // sort(@) | [?@ > `2`] → [1,2,3,4,5] → [3,4,5]
        AssertRT(NumbersJson, "sort(@) | [?@ > `2`]", """[3,4,5]""");
    }

    /// <summary>Reverse | Project: reverse array then project field.</summary>
    [Fact]
    public void ReverseThenProject()
    {
        // reverse(@) | [*].n → [C,B,A] → ["C","B","A"]
        AssertRT(ItemsJson, "reverse(@) | [*].n", """["C","B","A"]""");
    }

    // === Barrier + Barrier ===

    /// <summary>Sort | Reverse: sort then reverse (descending sort).</summary>
    [Fact]
    public void SortThenReverse()
    {
        // sort(@) | reverse(@) → [1,2,3,4,5] → [5,4,3,2,1]
        AssertRT(NumbersJson, "sort(@) | reverse(@)", """[5,4,3,2,1]""");
    }

    /// <summary>SortBy | Reverse: sort objects by field then reverse.</summary>
    [Fact]
    public void SortByThenReverse()
    {
        // sort_by(@, &v) | reverse(@) → B(1),C(2),A(3) → A(3),C(2),B(1)
        AssertRT(ItemsJson, "sort_by(@, &v) | reverse(@)", """[{"n":"A","v":3},{"n":"C","v":2},{"n":"B","v":1}]""");
    }

    // === Three+ stages ===

    /// <summary>Filter | SortBy | HashProject: the ComplexQuery pattern.</summary>
    [Fact]
    public void FilterSortByHashProject()
    {
        // [?v > `1`] | sort_by(@, &v) | [*].{name: n, value: v}
        // filter: A(3),C(2) → sort: C(2),A(3) → hash: [{name:C,value:2},{name:A,value:3}]
        AssertRT(ItemsJson, "[?v > `1`] | sort_by(@, &v) | [*].{name: n, value: v}", """[{"name":"C","value":2},{"name":"A","value":3}]""");
    }

    /// <summary>Project | Sort | Reverse: project, sort ascending, reverse to descending.</summary>
    [Fact]
    public void ProjectSortReverse()
    {
        // [*].v | sort(@) | reverse(@) → [3,1,2] → [1,2,3] → [3,2,1]
        AssertRT(ItemsJson, "[*].v | sort(@) | reverse(@)", """[3,2,1]""");
    }

    /// <summary>Flatten | Filter | Sort: flatten, filter, then sort.</summary>
    [Fact]
    public void FlattenFilterSort()
    {
        // [] | [?@ > `2`] | sort(@) → [5,1,3,4,2] → [5,3,4] → [3,4,5]
        AssertRT(NestedJson, "[] | [?@ > `2`] | sort(@)", """[3,4,5]""");
    }

    /// <summary>Filter | Reverse | Project: filter, reverse order, project names.</summary>
    [Fact]
    public void FilterReverseProject()
    {
        // [?v > `1`] | reverse(@) | [*].n → A(3),C(2) → C(2),A(3) → ["C","A"]
        AssertRT(ItemsJson, "[?v > `1`] | reverse(@) | [*].n", """["C","A"]""");
    }

    /// <summary>SortBy | Reverse | HashProject: sort, reverse, project to new objects.</summary>
    [Fact]
    public void SortByReverseHashProject()
    {
        // sort_by(@, &v) | reverse(@) | [*].{name: n}
        // B(1),C(2),A(3) → A(3),C(2),B(1) → [{name:A},{name:C},{name:B}]
        AssertRT(ItemsJson, "sort_by(@, &v) | reverse(@) | [*].{name: n}", """[{"name":"A"},{"name":"C"},{"name":"B"}]""");
    }

    // === Terminal HashProject ===

    /// <summary>SortBy | HashProject: sort then project to new objects.</summary>
    [Fact]
    public void SortByHashProject()
    {
        // sort_by(@, &v) | [*].{name: n, val: v}
        // B(1),C(2),A(3) → [{name:B,val:1},{name:C,val:2},{name:A,val:3}]
        AssertRT(ItemsJson, "sort_by(@, &v) | [*].{name: n, val: v}", """[{"name":"B","val":1},{"name":"C","val":2},{"name":"A","val":3}]""");
    }

    /// <summary>Reverse | HashProject: reverse then project to new objects.</summary>
    [Fact]
    public void ReverseHashProject()
    {
        // reverse(@) | [*].{name: n} → [C,B,A] → [{name:C},{name:B},{name:A}]
        AssertRT(ItemsJson, "reverse(@) | [*].{name: n}", """[{"name":"C"},{"name":"B"},{"name":"A"}]""");
    }

    /// <summary>Filter | HashProject: filter then project to new objects.</summary>
    [Fact]
    public void FilterHashProject()
    {
        // [?v > `1`] | [*].{name: n, val: v} → A(3),C(2) → [{name:A,val:3},{name:C,val:2}]
        AssertRT(ItemsJson, "[?v > `1`] | [*].{name: n, val: v}", """[{"name":"A","val":3},{"name":"C","val":2}]""");
    }

    // === Non-terminal HashProject ===

    /// <summary>HashProject | Reverse: project to new objects then reverse (non-terminal hash).</summary>
    [Fact]
    public void HashProjectReverse()
    {
        // [*].{name: n, val: v} | reverse(@) → [{name:A,val:3},{name:B,val:1},{name:C,val:2}] → reversed
        AssertRT(ItemsJson, "[*].{name: n, val: v} | reverse(@)", """[{"name":"C","val":2},{"name":"B","val":1},{"name":"A","val":3}]""");
    }

    // === MapExpr ===

    /// <summary>Map | Sort: map expression then sort results.</summary>
    [Fact]
    public void MapThenSort()
    {
        // map(&v, @) | sort(@) → [3,1,2] → [1,2,3]
        AssertRT(ItemsJson, "map(&v, @) | sort(@)", """[1,2,3]""");
    }

    /// <summary>Map | Filter: map expression then filter results.</summary>
    [Fact]
    public void MapThenFilter()
    {
        // map(&v, @) | [?@ > `1`] → [3,1,2] → [3,2]
        AssertRT(ItemsJson, "map(&v, @) | [?@ > `1`]", """[3,2]""");
    }

    // === Edge cases ===

    /// <summary>Empty array propagation through a 3-stage fused pipeline.</summary>
    [Fact]
    public void EmptyThroughPipeline()
    {
        // [?v > `100`] | sort_by(@, &v) | [*].n → [] through entire pipeline
        AssertRT(ItemsJson, "[?v > `100`] | sort_by(@, &v) | [*].n", """[]""");
    }

    /// <summary>Single element propagation through a 3-stage fused pipeline.</summary>
    [Fact]
    public void SingleThroughPipeline()
    {
        // [?v > `2`] | sort_by(@, &v) | [*].n → single element [A(3)] through pipeline
        AssertRT(ItemsJson, "[?v > `2`] | sort_by(@, &v) | [*].n", """["A"]""");
    }

    // === Bail-out / Recursive fusion ===

    /// <summary>
    /// Recursive fusion: outer pipe bails (IndexNode not fusible),
    /// but inner [*].v | sort(@) is recursively fused.
    /// </summary>
    [Fact]
    public void RecursiveInnerFusion()
    {
        // [*].v | sort(@) | [-1] → project [3,1,2] → sort [1,2,3] → last element 3
        AssertRT(ItemsJson, "[*].v | sort(@) | [-1]", """3""");
    }

    private static void AssertRT(string json, string expression, string expectedJson)
    {
        Corvus.Text.Json.JsonElement data = Corvus.Text.Json.JsonElement.ParseValue(
            Encoding.UTF8.GetBytes(json));

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JMESPathEvaluator.Default.Search(expression, data, workspace);

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
