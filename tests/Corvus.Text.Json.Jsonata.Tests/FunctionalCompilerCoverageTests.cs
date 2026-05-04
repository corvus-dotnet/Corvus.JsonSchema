// <copyright file="FunctionalCompilerCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;
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

    // ─── Index binding variable (#$i syntax, lines 5085-5105) ─────────

    [Fact]
    public void IndexBinding_FilterByPosition()
    {
        // #$i creates an index binding variable that expands singleton arrays
        // and enables position-based filtering (lines 5092-5105)
        string data = """{"items": [10, 20, 30, 40, 50]}""";
        string result = Eval("items#$i[$i >= 2]", data);
        Assert.Equal("[30,40,50]", result);
    }

    [Fact]
    public void IndexBinding_FirstElement()
    {
        string data = """{"items": ["a", "b", "c", "d"]}""";
        string result = Eval("items#$i[$i = 0]", data);
        Assert.Equal("\"a\"", result);
    }

    [Fact]
    public void IndexBinding_LastElementExpression()
    {
        // Use a simple constant index instead of $count() which can't resolve in filter context
        string data = """{"items": [1, 2, 3, 4, 5]}""";
        string result = Eval("items#$i[$i = 4]", data);
        Assert.Equal("5", result);
    }

    [Fact]
    public void IndexBinding_WithObjectArray()
    {
        // Index binding on array of objects
        string data = """{"orders": [{"id": "A"}, {"id": "B"}, {"id": "C"}]}""";
        string result = Eval("orders#$i[$i > 0].id", data);
        Assert.Equal("""["B","C"]""", result);
    }

    // ─── Transform on multi-value sequence (lines 8287-8296) ──────────

    [Fact]
    public void Transform_MultiValueSequence()
    {
        // When ~> |pattern|update| operates on a multi-element sequence
        // (not a single array), the non-singleton path at lines 8287-8296 is taken.
        // To get a multi-value sequence, the LHS must resolve through a path that
        // navigates into array elements producing multiple results.
        string data = """
        {
          "orders": [
            {"product": {"type": "fruit", "name": "apple"}},
            {"product": {"type": "veg", "name": "carrot"}},
            {"product": {"type": "fruit", "name": "banana"}}
          ]
        }
        """;
        string result = Eval("""orders.product ~> |$[type="fruit"]|{"selected": true}|""", data);
        Assert.Contains("\"selected\"", result);
        Assert.Contains("true", result);
    }

    [Fact]
    public void Transform_MultiValueWithDelete()
    {
        // Transform with delete field on multiple path-resolved items
        string data = """
        {
          "records": [
            {"inner": {"name": "A", "temp": 1, "value": 10}},
            {"inner": {"name": "B", "temp": 2, "value": 20}}
          ]
        }
        """;
        string result = Eval("""records.inner ~> |$|{}, ["temp"]|""", data);
        Assert.DoesNotContain("\"temp\"", result);
        Assert.Contains("\"name\"", result);
        Assert.Contains("\"value\"", result);
    }

    // ─── Numeric path with filter stages (lines 5344-5359) ────────────

    [Fact]
    public void NumericIndex_AfterFilter()
    {
        // Combining filter and numeric index stages
        string data = """{"items": [1, 2, 3, 4, 5, 6, 7, 8]}""";
        string result = Eval("items[$>3][0]", data);
        Assert.Equal("4", result);
    }

    [Fact]
    public void NumericIndex_NegativeOnFilteredResult()
    {
        string data = """{"items": [10, 20, 30, 40, 50]}""";
        string result = Eval("items[$>=30][-1]", data);
        Assert.Equal("50", result);
    }

    // ─── Binary/Octal parsing via $number (exercises BuiltInFunctions paths) ───

    [Fact]
    public void Number_BinaryPrefix_LowerCase()
    {
        string result = Eval("""$number("0b1010")""");
        Assert.Equal("10", result);
    }

    [Fact]
    public void Number_BinaryPrefix_UpperCase()
    {
        string result = Eval("""$number("0B1111")""");
        Assert.Equal("15", result);
    }

    [Fact]
    public void Number_OctalPrefix_LowerCase()
    {
        string result = Eval("""$number("0o17")""");
        Assert.Equal("15", result);
    }

    [Fact]
    public void Number_OctalPrefix_UpperCase()
    {
        string result = Eval("""$number("0O777")""");
        Assert.Equal("511", result);
    }

    [Fact]
    public void Number_BinaryPrefix_InvalidDigits()
    {
        // "0b1234" has invalid binary digits — $number throws D3030
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0b1234")"""));
        Assert.Equal("D3030", ex.Code);
    }

    [Fact]
    public void Number_OctalPrefix_InvalidDigits()
    {
        // "0o89" has invalid octal digits — $number throws D3030
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0o89")"""));
        Assert.Equal("D3030", ex.Code);
    }

    // ─── Property lookup on array input (lines 1018-1031) ─────────────

    [Fact]
    public void PropertyLookup_OnArrayOfObjects()
    {
        // Looking up a field on an array iterates each element
        string result = Eval("""data.name""", """{"data":[{"name":"Alice"},{"name":"Bob"}]}""");
        Assert.Equal("""["Alice","Bob"]""", result);
    }

    [Fact]
    public void PropertyLookup_OnArrayWithMissingField()
    {
        // Some elements may not have the field
        string result = Eval("""data.name""", """{"data":[{"name":"Alice"},{"x":1},{"name":"Carol"}]}""");
        Assert.Equal("""["Alice","Carol"]""", result);
    }

    [Fact]
    public void PropertyLookup_RootIsArray()
    {
        // When root input IS an array, property access should iterate
        string result = Eval("name", """[{"name":"a"},{"name":"b"}]""");
        Assert.Equal("""["a","b"]""", result);
    }

    // ─── Sort by terms applied to path (CompileSortStage, lines 7954-8017) ───

    [Fact]
    public void Sort_ByTerms_Ascending()
    {
        string result = Eval("""items^(v)""", """{"items":[{"v":3},{"v":1},{"v":2}]}""");
        Assert.Equal("""[{"v":1},{"v":2},{"v":3}]""", result);
    }

    [Fact]
    public void Sort_ByTerms_Descending()
    {
        string result = Eval("""items^(>v)""", """{"items":[{"v":3},{"v":1},{"v":2}]}""");
        Assert.Equal("""[{"v":3},{"v":2},{"v":1}]""", result);
    }

    [Fact]
    public void Sort_ByTerms_MultiKey()
    {
        string result = Eval(
            """items^(a, >b)""",
            """{"items":[{"a":1,"b":3},{"a":2,"b":1},{"a":1,"b":2}]}""");
        Assert.Equal("""[{"a":1,"b":3},{"a":1,"b":2},{"a":2,"b":1}]""", result);
    }

    [Fact]
    public void Sort_ByTerms_SingleElement_ReturnsSame()
    {
        string result = Eval("""items^(v)""", """{"items":[{"v":1}]}""");
        Assert.Equal("""{"v":1}""", result);
    }

    [Fact]
    public void Sort_ByTerms_Strings()
    {
        string result = Eval(
            """items^(name)""",
            """{"items":[{"name":"banana"},{"name":"apple"},{"name":"cherry"}]}""");
        Assert.Equal("""[{"name":"apple"},{"name":"banana"},{"name":"cherry"}]""", result);
    }

    // ─── Focus variable + sort (triggers sort step with $var bound) ───

    [Fact]
    public void Focus_ThenSort()
    {
        // Account.Order^(Product.Price) — sort Orders by Product.Price
        string result = Eval(
            """Account.Order^(Product.Price)""",
            """{"Account":{"Order":[{"Product":{"Price":3}},{"Product":{"Price":1}},{"Product":{"Price":2}}]}}""");
        Assert.Equal("""[{"Product":{"Price":1}},{"Product":{"Price":2}},{"Product":{"Price":3}}]""", result);
    }

    // ─── Index binding (#$var) expansion (lines 5095-5108) ────────────

    [Fact]
    public void IndexBinding_OnArrayResult()
    {
        // #$i binds the position index to each element
        string result = Eval(
            """items#$i[$i=1]""",
            """{"items":["a","b","c"]}""");
        Assert.Equal("\"b\"", result);
    }

    [Fact]
    public void IndexBinding_OnFilteredArray()
    {
        string result = Eval(
            """items#$i[$i<=1]""",
            """{"items":["a","b","c","d"]}""");
        Assert.Equal("""["a","b"]""", result);
    }

    // ─── Array constructor paths (lines 6801-6836) ────────────────────

    [Fact]
    public void ArrayConstructor_WithSingletonValue()
    {
        string result = Eval("""[item]""", """{"item":"hello"}""");
        Assert.Equal("""["hello"]""", result);
    }

    [Fact]
    public void ArrayConstructor_WithNestedArray()
    {
        // Array within array constructor should flatten into the array
        string result = Eval("""[items]""", """{"items":["a","b"]}""");
        Assert.Equal("""["a","b"]""", result);
    }

    [Fact]
    public void ArrayConstructor_MultipleExpressions()
    {
        string result = Eval("""[a, b, c]""", """{"a":1,"b":2,"c":3}""");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void ArrayConstructor_MixedTypes()
    {
        string result = Eval("""[1, "two", true, null]""");
        Assert.Equal("""[1,"two",true,null]""", result);
    }

    // ─── String-to-number coercion in filter predicates (TryCoerceToNumber string path) ───

    [Fact]
    public void FilterPredicate_StringNumericIndex()
    {
        // A variable holding a numeric-string "2" used as filter predicate → index access
        string result = Eval(
            """( $idx := "2"; items[$idx] )""",
            """{"items":["a","b","c","d","e"]}""");
        Assert.Equal("\"c\"", result);
    }

    [Fact]
    public void FilterPredicate_HexStringIndex()
    {
        // A variable holding a hex string "0x2" used as filter predicate → index 2
        string result = Eval(
            """( $idx := "0x2"; items[$idx] )""",
            """{"items":["a","b","c","d","e"]}""");
        Assert.Equal("\"c\"", result);
    }

    [Fact]
    public void FilterPredicate_BinaryStringIndex()
    {
        // A variable holding a binary string "0b10" used as filter predicate → index 2
        string result = Eval(
            """( $idx := "0b10"; items[$idx] )""",
            """{"items":["a","b","c","d","e"]}""");
        Assert.Equal("\"c\"", result);
    }

    [Fact]
    public void FilterPredicate_OctalStringIndex()
    {
        // A variable holding an octal string "0o3" used as filter predicate → index 3
        string result = Eval(
            """( $idx := "0o3"; items[$idx] )""",
            """{"items":["a","b","c","d","e"]}""");
        Assert.Equal("\"d\"", result);
    }

    // ─── Transform operator (lines 121 dispatch, CompileTransform) ────

    [Fact]
    public void Transform_BasicPattern()
    {
        // ~> |pattern|transform| — transform matching objects
        string result = Eval(
            """$ ~> |Account.Order|{"total":Price * Quantity}|""",
            """{"Account":{"Order":{"Price":5,"Quantity":3}}}""");
        // The transform adds the "total" field
        Assert.Contains("\"total\":15", result);
    }
}
