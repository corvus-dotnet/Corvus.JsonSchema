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

    // ─── WrapWithStages on non-PathNode (lines 175-201) ─────
    // A variable with a filter predicate: $var[pred] produces a non-PathNode (VariableNode)
    // with Stages containing a FilterNode. This is the only way to hit WrapWithStages.

    [Fact]
    public void WrapWithStages_VariableWithFilter()
    {
        // $arr[pred] — VariableNode with FilterNode stage, hits WrapWithStages.
        // The SortNode branch (line 186) is dead code — parser never puts SortNode in Stages.
        string result = Eval("""( $arr := [1,2,3,4,5]; $arr[$ > 3] )""");
        Assert.Equal("[4,5]", result);
    }

    [Fact]
    public void WrapWithStages_VariableWithMultipleFilters()
    {
        // Multiple filter stages on a non-PathNode variable: $arr[pred1][pred2]
        string result = Eval("""( $arr := [1,2,3,4,5,6,7,8]; $arr[$ > 2][$ < 7] )""");
        Assert.Equal("[3,4,5,6]", result);
    }

    // ─── Focus binding @$var cross-join with multi-valued parent (lines 2978-2998) ─────
    // EvalFocusStep receives parentContext.Count > 1 when prior step produces
    // multi-valued sequence. The cross-join navigates the remaining path from
    // the parent context, filtering by the focus variable.

    [Fact]
    public void FocusBinding_MultiParentContext_CrossJoin()
    {
        // Multi-step path where the parent produces multiple elements for the focus step.
        // library.loans produces array (singleton), then @$l processes each loan,
        // navigating .books from the library context and filtering by $l.isbn.
        string data = """
        {
          "library": {
            "loans": [{"isbn": "A1"}, {"isbn": "B2"}, {"isbn": "C3"}],
            "books": [
              {"isbn": "A1", "title": "Alpha"},
              {"isbn": "B2", "title": "Beta"},
              {"isbn": "C3", "title": "Gamma"}
            ]
          }
        }
        """;
        string result = Eval("""library.loans@$l.books[isbn=$l.isbn].title""", data);
        Assert.Contains("Alpha", result);
        Assert.Contains("Beta", result);
        Assert.Contains("Gamma", result);
    }

    [Fact]
    public void FocusBinding_WithArrayFlattening()
    {
        // Focus binding where the focus context is the parent step.
        // In: orders.lines@$line.catalog[sku=$line.sku].name
        // The focus context for @$line is each "order" object, so .catalog must
        // be at the same level as .lines.
        string data = """
        {
          "orders": [
            {"lines": [{"sku": "X"}, {"sku": "Y"}], "catalog": [{"sku": "X", "name": "Widget"}, {"sku": "Y", "name": "Gadget"}]},
            {"lines": [{"sku": "Z"}], "catalog": [{"sku": "Z", "name": "Doohickey"}]}
          ]
        }
        """;
        string result = Eval("""orders.lines@$line.catalog[sku=$line.sku].name""", data);
        Assert.Contains("Widget", result);
        Assert.Contains("Gadget", result);
        Assert.Contains("Doohickey", result);
    }

    // ─── Sort continuation after sort step (lines 3057-3082, 4043-4061) ─────
    // When a sort step is NOT the last step, elements must continue through remaining steps.

    [Fact]
    public void SortWithContinuation_PropertyAfterSort()
    {
        // Account.Order.Product^($.Price).Name: sort is intermediate, .Name continues
        string data = """
        {
          "items": [
            {"price": 30, "name": "C"},
            {"price": 10, "name": "A"},
            {"price": 20, "name": "B"}
          ]
        }
        """;
        string result = Eval("items^(price).name", data);
        Assert.Equal("""["A","B","C"]""", result);
    }

    [Fact]
    public void SortWithContinuation_DeepPath()
    {
        // Sort on intermediate step, then navigate deeper
        string data = """
        {
          "store": {
            "shelves": [
              {"priority": 3, "books": [{"title": "C1"}, {"title": "C2"}]},
              {"priority": 1, "books": [{"title": "A1"}]},
              {"priority": 2, "books": [{"title": "B1"}, {"title": "B2"}]}
            ]
          }
        }
        """;
        string result = Eval("store.shelves^(priority).books.title", data);
        // Sorted by priority → shelf1, shelf2, shelf3 → their books' titles
        Assert.Contains("A1", result);
        Assert.Contains("B1", result);
        Assert.Contains("C1", result);
    }

    // ─── Index binding #$i on multi-element parent (lines 4088-4108) ─────
    // EvalIndexStep receives multi-element inputContext.

    [Fact]
    public void IndexBinding_MultiParentContext()
    {
        // groups.items produces multi-valued, then #$i binds index on multi-parent
        string data = """
        {
          "groups": [
            {"items": ["a", "b"]},
            {"items": ["c", "d"]}
          ]
        }
        """;
        // groups.items gives [a,b,c,d] as multi-valued, #$i indexes each
        string result = Eval("groups.items#$i[$i=0]", data);
        // Should return first element(s) at index 0 within each group
        Assert.Contains("a", result);
    }

    // ─── Sort stage with array flattening in ApplyStages (lines 5466-5501) ─────
    // When applying sort stages, input contains arrays that need flattening first.

    [Fact]
    public void SortStage_ArrayOfArraysFlattening()
    {
        // Path producing array-of-arrays, then sort stage flattens before sorting
        string data = """
        {
          "departments": [
            {"people": [{"name": "Charlie", "age": 30}]},
            {"people": [{"name": "Alice", "age": 25}, {"name": "Bob", "age": 35}]}
          ]
        }
        """;
        // departments.people produces arrays, [age>0] filters all (pass-through), ^(age) sorts
        string result = Eval("departments.people[age > 0]^(age).name", data);
        Assert.Equal("""["Alice","Charlie","Bob"]""", result);
    }

    [Fact]
    public void SortStage_ArrayFlattening_SingleResult()
    {
        // When sort produces single element (sortElements.Count <= 1), lines 5486-5494
        string data = """
        {
          "departments": [
            {"people": [{"name": "Alice", "age": 25}]}
          ]
        }
        """;
        string result = Eval("departments.people[age > 20]^(age).name", data);
        Assert.Equal("\"Alice\"", result);
    }

    // ─── Group-by with simple name pair optimization (lines 221-229) ─────
    // WrapWithGroupBy fast path: single pair where both key and value are simple names.
    // Requires a NON-PathNode expression with group-by (variables pass through ProcessAst
    // unchanged as VariableNode, which is not PathNode).

    [Fact]
    public void GroupBy_SimpleNamePair_NonPathVariable()
    {
        // $items{category: price} — variable (non-PathNode) with simple NameNode key + value
        // Hits WrapWithGroupBy (line 208) and the fast path at line 221.
        string data = """
        {
          "items": [
            {"category": "A", "price": 10},
            {"category": "B", "price": 20},
            {"category": "A", "price": 30}
          ]
        }
        """;
        string result = Eval("""( $items := items; $items{category: price} )""", data);
        // Groups by category, collects prices: {"A": [10,30], "B": 20}
        Assert.Contains("A", result);
        Assert.Contains("B", result);
    }

    [Fact]
    public void GroupBy_NonPath_FunctionResult()
    {
        // Group-by on a non-path expression (function result)
        // $append() returns a non-PathNode result; group-by applied to it hits WrapWithGroupBy.
        string result = Eval(
            """( $data := $append([{"g":"X","v":1}], [{"g":"Y","v":2}]); $data{g: v} )""");
        Assert.Contains("X", result);
        Assert.Contains("Y", result);
    }

    // ─── Sort stage on intermediate step (lines 3394-3412) ─────
    // Tuple-based path with sort at intermediate position.

    [Fact]
    public void SortOnIntermediateStep_WithLabels()
    {
        // Sort on an intermediate step where labels are involved
        string data = """
        {
          "library": {
            "books": [
              {"title": "B", "year": 2020},
              {"title": "A", "year": 2018},
              {"title": "C", "year": 2022}
            ]
          }
        }
        """;
        // Sort books by year, then get titles
        string result = Eval("library.books^(year).title", data);
        Assert.Equal("""["A","B","C"]""", result);
    }

    // ─── Multi-parent context in inner step (lines 3294-3314) ─────
    // When inner focus evaluation has parentContext.Count > 1.

    [Fact]
    public void InnerFocusStep_MultiParent()
    {
        // Complex cross-join where navigation correlates across collections.
        // The pattern orders.lines@$line.products[sku=$line.sku] produces a
        // cross-join where each line is matched against the catalog.
        string data = """
        {
          "data": {
            "refs": [{"code": "A"}, {"code": "B"}],
            "items": [
              {"code": "A", "value": 100},
              {"code": "B", "value": 200},
              {"code": "C", "value": 300}
            ]
          }
        }
        """;
        string result = Eval("""data.refs@$r.items[code=$r.code].value""", data);
        Assert.Contains("100", result);
        Assert.Contains("200", result);
        Assert.DoesNotContain("300", result);
    }

    // ─── Path with sort at step > 0 and continuation (line 3057) ─────

    [Fact]
    public void Sort_IntermediateStepWithArrayInput()
    {
        // store.products^(price)[0] — sort at step 1, then index at step 2
        string data = """
        {
          "store": {
            "products": [
              {"name": "Expensive", "price": 100},
              {"name": "Cheap", "price": 5},
              {"name": "Mid", "price": 50}
            ]
          }
        }
        """;
        string result = Eval("store.products^(price)[0].name", data);
        Assert.Equal("\"Cheap\"", result);
    }
}
