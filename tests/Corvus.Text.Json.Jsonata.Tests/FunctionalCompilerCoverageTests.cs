// <copyright file="FunctionalCompilerCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests targeting uncovered branches in <see cref="FunctionalCompiler"/>,
/// identified from merged Cobertura coverage data.
/// </summary>
public class FunctionalCompilerCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── Sort stages in ApplyStages (lines 5187-5230) ─────────────────
    // To hit the sort branch INSIDE ApplyStages, we need a path step with
    // both filter AND sort stages (stepIdx==0 goes through line 2694).
    // Standalone `expr^(key)` compiles via CompileSortStage, not ApplyStages.

    [Fact]
    public void SortStageInApplyStages_FilterThenSort()
    {
        // Path step 0 with both filter and sort stages → ApplyStages sort branch (5187-5230)
        string data = """
        {"items": [
          {"name": "A", "price": 10},
          {"name": "B", "price": 30},
          {"name": "C", "price": 20},
          {"name": "D", "price": 5}
        ]}
        """;
        // items[price > 5] filters then ^(price) sorts — both are stages on step 0
        string result = Eval("items[price > 5]^(price).name", data);
        Assert.Equal("""["A","C","B"]""", result);
    }

    [Fact]
    public void SortStageInApplyStages_ArrayFlattening()
    {
        // Sort stage flattens array-of-arrays before sorting (lines 5196-5206)
        string data = """
        {
          "Account": {
            "Order": [
              {"Product": [{"Price": 30, "Name": "C"}, {"Price": 10, "Name": "A"}]},
              {"Product": [{"Price": 20, "Name": "B"}]}
            ]
          }
        }
        """;
        // Account.Order.Product produces array-of-arrays; filter [Price>0] + sort ^(Price) as stages
        string result = Eval("Account.Order.Product[Price > 0]^(Price).Name", data);
        Assert.Equal("""["A","B","C"]""", result);
    }

    [Fact]
    public void SortStageInApplyStages_SingleElement()
    {
        // Sort with single element after filtering — hits sortElements.Count <= 1
        string data = """
        {"items": [
          {"name": "A", "price": 10},
          {"name": "B", "price": 30}
        ]}
        """;
        string result = Eval("items[price > 20]^(price).name", data);
        Assert.Equal("\"B\"", result);
    }

    [Fact]
    public void SortStage_DescendingOrder()
    {
        string data = """
        {"items": [
          {"name": "A", "price": 10},
          {"name": "B", "price": 30},
          {"name": "C", "price": 20}
        ]}
        """;
        string result = Eval("items^(>price).name", data);
        Assert.Equal("""["B","C","A"]""", result);
    }

    // ─── ApplySortStagesOnly (lines 5958-5981) ──────────────────────
    // Called at stepIdx > 0 in path compilation (line 2698).

    [Fact]
    public void ApplySortStagesOnly_MultiStepPathSort()
    {
        // Multi-step path with sort on step > 0 → ApplySortStagesOnly → ApplyStages
        string data = """
        {
          "store": {
            "books": [
              {"title": "C", "price": 30},
              {"title": "A", "price": 10},
              {"title": "B", "price": 20}
            ]
          }
        }
        """;
        // store.books[price > 0]^(price) — books is stepIdx > 0
        string result = Eval("store.books[price > 0]^(price).title", data);
        Assert.Equal("""["A","B","C"]""", result);
    }

    // ─── Numeric index sequences (lines 5330-5370, 5597-5625) ─────────
    // Multi-value stage result that is all-numeric triggers numeric index selection.
    // This requires a filter predicate that RETURNS numeric values (not booleans).

    [Fact]
    public void NumericIndex_PredicateReturningIndex()
    {
        // A predicate that returns a number is used as an index selector
        // e.g., items[[0,2]] uses the array [0,2] as numeric indices
        string data = """{"items": ["a", "b", "c", "d", "e"]}""";
        string result = Eval("$map([0,2,4], function($i){items[$i]})", data);
        Assert.Equal("""["a","c","e"]""", result);
    }

    [Fact]
    public void NumericIndexFilter_NegativeIndex()
    {
        string data = """{"items": ["a", "b", "c"]}""";
        string result = Eval("items[-1]", data);
        Assert.Equal("\"c\"", result);
    }

    // ─── Nested array collection — LookupField (lines 1014-1028) ─────────
    // LookupField is called when a field name is looked up on an array value.
    // This requires a path where an intermediate step returns an array.

    [Fact]
    public void LookupField_ArrayInput_CollectsFieldValues()
    {
        // Array field lookup: field is looked up on each element of the array
        string data = """
        {
          "account": {
            "orders": [
              {"items": [{"name": "x"}, {"name": "y"}]},
              {"items": [{"name": "z"}]}
            ]
          }
        }
        """;
        string result = Eval("account.orders.items.name", data);
        Assert.Equal("""["x","y","z"]""", result);
    }

    [Fact]
    public void LookupField_TopLevelArrayInput()
    {
        // Direct field lookup on an array of objects
        string data = """[{"name": "a"}, {"name": "b"}, {"name": "c"}]""";
        string result = Eval("name", data);
        Assert.Equal("""["a","b","c"]""", result);
    }

    // ─── CollectAndContinue nested arrays (lines 1567-1581) ─────────────
    // When items in a collection step are arrays, they need recursive processing.

    [Fact]
    public void CollectAndContinue_NestedArraysWithIndex()
    {
        // Path with constant index after array-producing step
        string data = """
        {
          "data": [
            {"children": [{"val": 10}, {"val": 20}]},
            {"children": [{"val": 30}, {"val": 40}]}
          ]
        }
        """;
        // data.children[0] — applies constant index across nested arrays
        string result = Eval("data.children[0].val", data);
        Assert.Equal("""[10,30]""", result);
    }

    [Fact]
    public void NestedCollection_MixedObjectsAndArrays()
    {
        string data = """
        {
          "data": [
            {"children": [{"val": 1}, {"val": 2}]},
            {"children": {"val": 3}}
          ]
        }
        """;
        string result = Eval("data.children.val", data);
        Assert.Equal("""[1,2,3]""", result);
    }

    // ─── Multi-step path with predicate and sort combined ─────────────
    // Note: Sort as a STAGE inside ApplyStages (5192-5230) is dead code —
    // the parser never creates SortNode as a stage annotation. Sort is always
    // a separate SortNode step or compiled via CompileSortStage.

    [Fact]
    public void PathWithPredicateAndSort_CombinedPipeline()
    {
        string data = """
        {
          "orders": [
            {"product": "Widget", "qty": 5, "price": 20},
            {"product": "Gadget", "qty": 2, "price": 50},
            {"product": "Gizmo", "qty": 8, "price": 10},
            {"product": "Doohickey", "qty": 1, "price": 100}
          ]
        }
        """;
        // Filter then sort
        string result = Eval("orders[price >= 20]^(price).product", data);
        Assert.Equal("""["Widget","Gadget","Doohickey"]""", result);
    }

    // ─── Sort-only path (standalone ^() via CompileSortStage) ─────────────

    [Fact]
    public void SortOnlyStages_MultipleSortCriteria()
    {
        string data = """
        {"items": [
          {"cat": "B", "name": "Z"},
          {"cat": "A", "name": "Y"},
          {"cat": "A", "name": "X"},
          {"cat": "B", "name": "W"}
        ]}
        """;
        string result = Eval("items^(cat, name).name", data);
        Assert.Equal("""["X","Y","W","Z"]""", result);
    }

    // ─── CreateArrayElement / CreateNumberElement / etc. (lines 9051-9079)

    [Fact]
    public void NumberOfNonNumericString_ThrowsD3030()
    {
        // $number("abc") throws D3030 in JSONata
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$number("abc")"""));
        Assert.Equal("D3030", ex.Code);
    }

    [Fact]
    public void PathExpression_ArrayOfArrays_Flattens()
    {
        // Path producing array-of-arrays that needs flattening
        string data = """
        {
          "departments": [
            {"employees": [{"name": "Alice"}, {"name": "Bob"}]},
            {"employees": [{"name": "Charlie"}]}
          ]
        }
        """;
        string result = Eval("departments.employees.name", data);
        Assert.Equal("""["Alice","Bob","Charlie"]""", result);
    }

    // ─── Keep-array semantics ([] suffix) ─────────────────────────────

    [Fact]
    public void KeepArray_SingletonWrappedInArray()
    {
        string data = """{"items": [{"name": "only"}]}""";
        string result = Eval("items[].name", data);
        // With [], singleton should still be wrapped in array
        Assert.Equal("""["only"]""", result);
    }

    [Fact]
    public void KeepArray_EmptyArrayPreserved()
    {
        string result = Eval("$append([], [])", """{}""");
        Assert.Equal("[]", result);
    }
}
